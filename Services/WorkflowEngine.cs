using Microsoft.Extensions.Logging;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Core.Models;
using Notify.Core.Models.Yaml;
using Notify.Helper;
using Stateless;
using System.Collections.Immutable;
using System.Data;
using System.Text.RegularExpressions;
using VYaml.Serialization;

namespace Notify.Services
{
    public partial class WorkflowEngine : IWorkflowEngine
    {
        private readonly IRepository _repository;
        private readonly Func<MessageProvider, INotificationProvider> _providerFactory;
        private readonly HttpClient _httpClient;
        private readonly ILogger<WorkflowEngine> _logger;

        private string _workflowName = string.Empty;
        private WorkflowRootConfig? _config;
        private int _maxAttempts;

        [GeneratedRegex(@"^([+-]?\d+)\s*(hour|hours|day|days|month|months)$", RegexOptions.IgnoreCase)]
        private static partial Regex ModifyRuleRegex();

        [GeneratedRegex(@"[\uD800-\uDBFF][\uDC00-\uDFFF]|[\u2600-\u27BF\uFE00-\uFE0F\u200D]")]
        private static partial Regex RenderCleanPayloadRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex RenderCleanPayloadSpacesRegex();

        public WorkflowEngine(IRepository repository, Func<MessageProvider, INotificationProvider> providerFactory, HttpClient httpClient, ILogger<WorkflowEngine> logger)
        {
            _repository = repository;
            _providerFactory = providerFactory;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ExitCode> ExecuteAsync(string workflowPath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(workflowPath))
            {
                _logger.LogYAMLConfigNotFound(workflowPath);
                return ExitCode.FileNotFound;
            }

            _workflowName = Path.GetFileNameWithoutExtension(workflowPath);
            _logger.LogRunWorkflow(_workflowName);

            byte[] yamlBytes = await File.ReadAllBytesAsync(workflowPath, ct);

            _config = YamlSerializer.Deserialize<WorkflowRootConfig>(yamlBytes, new YamlSerializerOptions
            {
                Resolver = CompositeResolver.Create(
                    new IYamlFormatter[]
                    {
                        FlexibleStringListFormatter.Instance,
                        MessageStepFormatter.Instance
                    },
                    new IYamlFormatterResolver[]
                    {
                        GeneratedResolver.Instance,
                        StandardResolver.Instance
                    }
                )
            });

            if (_config == null)
            {
                _logger.LogFailedDeserializeYAMLWorkflowConfiguration();
                return ExitCode.InvalidConfig;
            }

            if (!_config.Enabled)
            {
                _logger.LogWorkflowIsDisabled(_config.Name);
                return ExitCode.Success;
            }

            if (_config.Callbacks != null && _config.Callbacks.TryGetValue("onEnter", out string? onEnterCallback) && !string.IsNullOrEmpty(onEnterCallback))
            {
                await TriggerPhpTaskAsync(onEnterCallback, ct);
            }

            _maxAttempts = _config.Schedule?.Count ?? 0;

            await ProcessEventsAsync(ct);
            await SetFirstSendAfterAsync(ct);
            await RecoverStuckTasksAsync(ct);

            // 1. Fetch pending tasks ready for sending
            IEnumerable<NotificationQueueItem> tasks = await _repository.GetPendingTasksAsync(_config.Name, _maxAttempts, Program.DateTimeKiev, _config.BatchLimit > 0 ? _config.BatchLimit : 100, ct);
            if (!tasks.Any())
            {
                return ExitCode.Success;
            }

            ct.ThrowIfCancellationRequested();

            // 2. Atomically claim tasks for processing
            IReadOnlySet<int> claimedIdsSet = await _repository.ClaimTasksAsync(_config.Name, tasks.Select(t => t.NotificationQueueId), ct);
            if (!claimedIdsSet.Any())
            {
                return ExitCode.Success;
            }

            ImmutableList<NotificationQueueItem> itemsToProcess = tasks.Where(t => claimedIdsSet.Contains(t.NotificationQueueId)).ToImmutableList();

            if (string.IsNullOrEmpty(_config.Provider) || !Enum.TryParse(_config.Provider, true, out MessageProvider providerKey))
            {
                throw new KeyNotFoundException($"Provider with key '{_config.Provider ?? "null"}' not found");
            }

            List<NotificationTask> tasksToSend = new List<NotificationTask>();
            List<NotificationItem> notifications = new List<NotificationItem>();

            foreach (NotificationQueueItem item in itemsToProcess)
            {
                ct.ThrowIfCancellationRequested();

                NotificationTask task = new NotificationTask(item);

                if (providerKey.Equals(MessageProvider.Email) && string.IsNullOrWhiteSpace(task.Email))
                {
                    await ApplyTransitionAndCancelAsync(task, "Customer email address not found", ct);
                    continue;
                }

                if (!providerKey.Equals(MessageProvider.Email) && string.IsNullOrWhiteSpace(task.Telephone))
                {
                    await ApplyTransitionAndCancelAsync(task, "Customer phone number not found", ct);
                    continue;
                }

                if (!await ValidateConditionsAsync(task, ct))
                {
                    await ApplyTransitionAndCancelAsync(task, "Conditions not met", ct);
                    continue;
                }

                task.SetAttempts(task.Attempts + 1);
                string payload = ResolvePayload(providerKey, task, (int)task.Attempts);

                if (string.IsNullOrWhiteSpace(payload))
                {
                    await ApplyTransitionAndCancelAsync(task, string.Concat("Empty payload for attempt #", ((int)task.Attempts).ToString()), ct);
                    continue;
                }

                task.SetPayload(payload);
                StateMachine<string, string> stateMachine = BuildStateMachine(task.Status);

                if (stateMachine.CanFire("start_processing"))
                {
                    stateMachine.Fire("start_processing");
                    task.Status = stateMachine.State;

                    tasksToSend.Add(task);
                    notifications.Add(CreateNotificationObject(providerKey, task, payload));
                }
            }

            if (tasksToSend.Count == 0)
            {
                return ExitCode.Success;
            }

            // 3. Batch Sending via Provider
            bool success = await ProcessBatchAsync(providerKey, tasksToSend, notifications, ct);

            if (_config.Callbacks != null && _config.Callbacks.TryGetValue("onLeave", out string? onLeaveCallback) && !string.IsNullOrEmpty(onLeaveCallback))
            {
                await TriggerPhpTaskAsync(onLeaveCallback, ct);
            }

            _logger.LogWorkflowCompleted(_workflowName);

            return success ? ExitCode.Success : ExitCode.InvalidOperation;
        }

        private async Task ProcessEventsAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (_config?.Events == null ||
                _config.Events.Triggers.Count == 0 ||
                string.IsNullOrEmpty(_config.Events.Code) ||
                string.IsNullOrEmpty(_config.Events.Action))
            {
                return;
            }

            IEnumerable<string> triggers = _config.Events.Triggers.Distinct().Select(t => t.Value).OrderBy(t => t);
            IEnumerable<string> existingTriggers = await _repository.GetEventTriggersAsync(_config.Events.Code, _config.Events.Action, ct);

            if (existingTriggers.SequenceEqual(triggers))
            {
                return;
            }

            await _repository.SyncEventsAsync(_config.Events.Code, _config.Events.Action, triggers, ct);
        }

        private async Task SetFirstSendAfterAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            IEnumerable<NotificationQueueItem> uninitializedTasks = await _repository.GetUninitializedTasksAsync(_config!.Name, ct);

            if (!uninitializedTasks.Any())
            {
                return;
            }

            Dictionary<long, DateTime> updates = new Dictionary<long, DateTime>();
            foreach (NotificationQueueItem task in uninitializedTasks)
            {
                DateTime sendAfter = CalculateSendAfter(1, task.DateAdded);
                updates[task.NotificationQueueId] = sendAfter;
            }

            await _repository.BatchUpdateSendAfterAsync(updates, ct);
        }

        private async Task RecoverStuckTasksAsync(CancellationToken ct = default)
        {
            await _repository.RecoverStuckTasksAsync(_config!.Name, Program.DateTimeKiev.AddMinutes(-10), ct);
        }

        private async Task<bool> ValidateConditionsAsync(NotificationTask task, CancellationToken ct = default)
        {
            if (_config?.Conditions == null || !_config.Conditions.Any())
            {
                return true;
            }

            foreach (ConditionConfig cond in _config.Conditions.Values)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(cond.Query))
                {
                    continue;
                }

                int count = await _repository.ExecuteConditionQueryAsync(cond.Query, task, ct);

                if (count < cond.MinCount)
                {
                    return false;
                }
            }

            return true;
        }

        private string ResolvePayload(MessageProvider provider, NotificationTask task, int attempt)
        {
            return provider switch
            {
                MessageProvider.Viber => RenderViberPayload(task, attempt),
                MessageProvider.SMS => RenderSmsPayload(task, attempt),
                MessageProvider.Email => RenderEmailPayload(task, attempt),
                _ => throw new ArgumentException($"Unsupported provider: {provider}")
            };
        }

        private string RenderViberPayload(NotificationTask task, int attempt)
        {
            return RenderMessage(task, attempt);
        }

        private string RenderSmsPayload(NotificationTask task, int attempt)
        {
            string payload = RenderMessage(task, attempt);

            string cleanText = RenderCleanPayloadRegex().Replace(payload, string.Empty);
            cleanText = RenderCleanPayloadSpacesRegex().Replace(cleanText, " ");

            return cleanText.Trim();
        }

        private string RenderEmailPayload(NotificationTask task, int attempt)
        {
            MessageVariantConfig? template = ResolveTemplate(attempt, task);

            if (template == null)
            {
                return string.Empty;
            }

            LanguageCode lang = task.LanguageCode;
            string text = ResolveLocalizedField(template.Text, lang)?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            Dictionary<string, string> replacements = GetReplacements(task);
            string bodyText = ReplacePlaceholders(text, replacements);
            string bodyHtml = bodyText.Replace("\n", "<br>");

            string imageHtml = string.Empty;
            string? imageUrl = ResolveLocalizedField(template.ImageUrl, lang);
            if (!string.IsNullOrEmpty(imageUrl))
            {
                imageUrl = ReplacePlaceholders(imageUrl, replacements);
                imageHtml = $@"<tr><td align=""center"" style=""padding-bottom:24px;"">
                    <img src=""{System.Net.WebUtility.HtmlEncode(imageUrl)}"" alt="""" width=""520"" style=""width:100%;max-width:520px;height:auto;display:block;border-radius:8px;border:0;outline:none;text-decoration:none;"">
                </td></tr>";
            }

            string buttonHtml = string.Empty;
            if (!string.IsNullOrEmpty(template.ButtonUrl))
            {
                string buttonUrl = ReplacePlaceholders(template.ButtonUrl, replacements);
                string buttonText = ResolveLocalizedField(template.ButtonText, lang) ?? "Перейти";

                buttonHtml = $@"<tr><td align=""center"" style=""padding-top:28px;padding-bottom:8px;"">
                    <!--[if mso]>
                    <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word"" href=""{System.Net.WebUtility.HtmlEncode(buttonUrl)}"" style=""height:46px;v-text-anchor:middle;width:200px;"" arcsize=""13%"" stroke=""f"" fillcolor=""#019f01"">
                    <w:anchorlock/>
                    <center style=""color:#ffffff;font-family:Arial,sans-serif;font-size:15px;font-weight:bold;"">{System.Net.WebUtility.HtmlEncode(buttonText)}</center>
                    </v:roundrect>
                    <![endif]-->
                    <!--[if !mso]><!-->
                    <a href=""{System.Net.WebUtility.HtmlEncode(buttonUrl)}"" target=""_blank"" style=""background-color:#019f01;color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:14px 32px;border-radius:6px;display:inline-block;text-align:center;box-shadow:0 2px 4px rgba(1, 159, 1, 0.25);"">
                    {System.Net.WebUtility.HtmlEncode(buttonText)}
                    </a>
                    <!--<![endif]-->
                </td></tr>";
            }

            return $@"<!DOCTYPE html>
                    <html lang=""{lang}"">
                    <head>
                        <meta charset=""UTF-8"">
                        <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                        <title>Notification</title>
                    </head>
                    <body style=""margin:0;padding:32px 0;background-color:#f4f5f7;font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;"">
                        <table role=""presentation"" width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f5f7;"">
                            <tr>
                                <td align=""center"" style=""padding:0 12px;"">
                                    <table role=""presentation"" width=""600"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;margin:0 auto;"">
                                        <tr><td style=""background-color:#019f01;height:4px;border-top-left-radius:10px;border-top-right-radius:10px;"">&nbsp;</td></tr>
                                        <tr>
                                            <td style=""background-color:#ffffff;border-bottom-left-radius:10px;border-bottom-right-radius:10px;padding:36px 40px;box-shadow:0 4px 12px rgba(0,0,0,0.05);border:1px solid #e5e7eb;border-top:none;"">
                                                <table role=""presentation"" width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                                    {imageHtml}
                                                    <tr><td style=""font-size:15px;line-height:1.65;color:#374151;word-break:break-word;"">{bodyHtml}</td></tr>
                                                    {buttonHtml}
                                                </table>
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
                    </body>
                    </html>";
        }

        private MessageVariantConfig? ResolveTemplate(int attempt, NotificationTask task)
        {
            if (_config?.Message == null || !_config.Message.TryGetValue(attempt, out MessageStepConfig? variants) || variants == null)
            {
                return _config?.Message?.Values.FirstOrDefault()?.Variants.FirstOrDefault().Value;
            }

            if (variants.Variants.Count == 0)
            {
                return null;
            }

            // A/B testing selection
            MessageVariantConfig firstVariant = variants.Variants.First().Value;
            if (!string.IsNullOrEmpty(firstVariant.Experiment))
            {
                string group = AssignCustomerGroup(task.Telephone ?? task.Email ?? task.CustomerId.ToString() ?? string.Empty, firstVariant.Experiment);

                if (variants.Variants.TryGetValue(group, out var selectedVariant))
                {
                    return selectedVariant;
                }
            }

            return firstVariant;
        }

        private static string AssignCustomerGroup(string seed, string experimentName)
        {
            // Deterministic hash assignment for A/B testing
            using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed + experimentName));
            return (hash[0] % 2 == 0) ? "A" : "B";
        }

        private string RenderMessage(NotificationTask task, int attempt)
        {
            MessageVariantConfig? template = ResolveTemplate(attempt, task);
            if (template == null)
            {
                return string.Empty;
            }

            string text = ResolveLocalizedField(template.Text, task.LanguageCode)?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            Dictionary<string, string> replacements = GetReplacements(task);
            string message = ReplacePlaceholders(text, replacements);

            if (!string.IsNullOrEmpty(template.ButtonUrl))
            {
                string url = ReplacePlaceholders(template.ButtonUrl, replacements);
                message += $"\n\n{url}";
            }

            return message;
        }

        private static Dictionary<string, string> GetReplacements(NotificationTask task)
        {
            return new Dictionary<string, string>
            {
                { "{first_name}", !string.IsNullOrEmpty(task.Firstname) ? task.Firstname : "покупець" },
                { "{last_name}", task.Lastname ?? string.Empty },
                { "{email}", task.Email ?? string.Empty },
                { "{phone}", task.Telephone ?? string.Empty },
                { "{customer_id}", task.CustomerId?.ToString() ?? string.Empty }
            };
        }

        private static string ReplacePlaceholders(string input, Dictionary<string, string> replacements)
        {
            foreach (KeyValuePair<string, string> kvp in replacements)
            {
                input = input.Replace(kvp.Key, kvp.Value);
            }

            return input;
        }

        private static string? ResolveLocalizedField(Dictionary<string, string?>? field, LanguageCode languageCode)
        {
            if (field == null || field.Count == 0)
            {
                return null;
            }

            if (field.TryGetValue(languageCode.ToString().ToLowerInvariant(), out string? val) && !string.IsNullOrEmpty(val))
            {
                return val;
            }

            return field.Values.FirstOrDefault();
        }

        private async Task<bool> ProcessBatchAsync(MessageProvider providerKey, List<NotificationTask> tasks, List<NotificationItem> notifications, CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                INotificationProvider provider = _providerFactory(providerKey);
                int chunkSize = _config?.ChunkSize > 0 ? _config.ChunkSize : 90;
                int chunkDelay = _config?.ChunkDelay > 0 ? _config.ChunkDelay : 1;

                NotificationItem[][] chunks = notifications
                    .GroupBy(n => n.Body)
                    .SelectMany(group => group.Chunk(chunkSize))
                    .ToArray();

                for (int i = 0; i < chunks.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    await provider.SendAsync(chunks[i], ct);

                    if (i < chunks.Length - 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(chunkDelay), ct);
                    }
                }

                await FinalizeBatchAsync(tasks, ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogBatchSendingFailed(ex, _config?.Name ?? "null");

                // Revert status to pending upon failure
                await _repository.ResetTaskStatusAsync(tasks.Select(t => t.Id), ct: CancellationToken.None);
                return false;
            }
        }

        private async Task FinalizeBatchAsync(List<NotificationTask> tasks, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            Dictionary<int, string> completedUpdates = new Dictionary<int, string>();
            Dictionary<int, (DateTime SendAfter, string Payload)> pendingUpdates = new Dictionary<int, (DateTime SendAfter, string Payload)>();
            List<NotificationSendLog> logEntries = new List<NotificationSendLog>();

            foreach (NotificationTask task in tasks)
            {
                int attemptsDone = task.Attempts;
                StateMachine<string, string> stateMachine = BuildStateMachine(task.Status);

                logEntries.Add(new NotificationSendLog()
                {
                    NotificationQueueId = task.Id,
                    SessionId = task.SessionId,
                    CustomerId = task.CustomerId,
                    Telephone = !string.IsNullOrEmpty(task.Telephone) ? task.Telephone : task.Email,
                    Action = _config!.Name,
                    AttemptNumber = (byte)attemptsDone,
                    Payload = task.Payload,
                    Status = "sent",
                    ScheduledAt = task.SendAfter ?? task.DateAdded
                });

                if (attemptsDone >= _maxAttempts)
                {
                    if (stateMachine.CanFire("complete"))
                    {
                        stateMachine.Fire("complete");
                    }

                    completedUpdates[task.Id] = task.Payload;
                }
                else
                {
                    if (stateMachine.CanFire("schedule_next"))
                    {
                        stateMachine.Fire("schedule_next");
                    }

                    DateTime nextSendAfter = CalculateSendAfter(attemptsDone + 1, task.DateAdded);
                    pendingUpdates[task.Id] = (nextSendAfter, task.Payload);
                }
            }

            if (completedUpdates.Count > 0)
            {
                await _repository.BatchCompleteTasksAsync(completedUpdates, ct);
            }

            if (pendingUpdates.Count > 0)
            {
                await _repository.BatchRescheduleTasksAsync(pendingUpdates, ct);
            }

            await _repository.LogSendBatchAsync(logEntries, ct);
        }

        private async Task ApplyTransitionAndCancelAsync(NotificationTask task, string reason, CancellationToken ct = default)
        {
            StateMachine<string, string> stateMachine = BuildStateMachine(task.Status);

            if (stateMachine.CanFire("cancel"))
            {
                stateMachine.Fire("cancel");
            }

            await _repository.CancelTaskAsync(task.Id, ct);

            _logger.TaskCancelledWithReason(task.Id, stateMachine.State, reason);
        }

        private DateTime CalculateSendAfter(int attempt, DateTime createdAt)
        {
            if (_config?.Schedule == null || !_config.Schedule.TryGetValue(attempt, out ScheduleStepConfig? rule) || rule == null)
            {
                throw new ArgumentException($"No schedule rule for attempt {attempt}");
            }

            DateTime date = createdAt;

            if (!string.IsNullOrEmpty(rule.Modify))
            {
                date = ParseModifyRule(date, rule.Modify);
            }

            if (!string.IsNullOrEmpty(rule.Time) && TimeSpan.TryParse(rule.Time, out var timeOfDay))
            {
                date = date.Date.Add(timeOfDay);
            }

            return date;
        }

        private static DateTime ParseModifyRule(DateTime baseDate, string modify)
        {
            Match match = ModifyRuleRegex().Match(modify.Trim());

            if (!match.Success)
            {
                return baseDate;
            }

            int value = int.Parse(match.Groups[1].Value);
            string unit = match.Groups[2].Value.ToLowerInvariant();

            return unit switch
            {
                "second" or "seconds" => baseDate.AddSeconds(value),
                "minute" or "minutes" => baseDate.AddMinutes(value),
                "hour" or "hours" => baseDate.AddHours(value),
                "day" or "days" => baseDate.AddDays(value),
                "month" or "months" => baseDate.AddMonths(value),
                "year" or "years" => baseDate.AddYears(value),
                _ => baseDate
            };
        }

        private StateMachine<string, string> BuildStateMachine(string currentState)
        {
            if (_config == null)
            {
                throw new InvalidOperationException("Configuration is not loaded.");
            }

            StateMachine<string, string> machine = new StateMachine<string, string>(currentState);

            foreach (KeyValuePair<string, TransitionConfig> kvp in _config.Workflow.Transitions)
            {
                foreach (string fromPlace in kvp.Value.From)
                {
                    machine.Configure(fromPlace).Permit(kvp.Key, kvp.Value.To);
                }
            }

            return machine;
        }

        private NotificationItem CreateNotificationObject(MessageProvider provider, NotificationTask task, string payload)
        {
            return new NotificationItem(
                provider: provider,
                recipient: provider.Equals(MessageProvider.Email) ? task.Email! : task.Telephone!,
                subject: ResolveEmailSubject(task, task.Attempts),
                body: payload
            );
        }

        private string ResolveEmailSubject(NotificationTask task, int attempt)
        {
            MessageVariantConfig? template = ResolveTemplate(attempt, task);

            if (template != null)
            {
                string? localizedSubject = ResolveLocalizedField(template.Subject, task.LanguageCode);

                if (!string.IsNullOrEmpty(localizedSubject))
                {
                    return localizedSubject;
                }
            }

            return _config?.Subject ?? "Повідомлення";
        }

        private async Task<bool> TriggerPhpTaskAsync(string callback, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(callback))
            {
                return false;
            }

            try
            {
                using (HttpResponseMessage response = await _httpClient.GetAsync(string.Concat("?name=", Uri.EscapeDataString(callback.Trim())), ct))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }

                    string errorResponseBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogPhpTaskFailed(callback, (int)response.StatusCode, errorResponseBody);

                    return false;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogPhpTaskCanceled(callback);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogPhpTaskNetworkError(ex, callback);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogPhpTaskUnexpectedError(ex, callback);
                return false;
            }
        }
    }
}