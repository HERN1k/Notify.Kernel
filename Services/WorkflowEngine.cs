using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Core.Models;
using Notify.Core.Models.Yaml;
using Notify.Helper;
using Stateless;
using VYaml.Serialization;

namespace Notify.Services
{
    public class WorkflowEngine : IWorkflowEngine
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly Func<string, INotificationProvider> _providerFactory;
        private readonly ILogger<WorkflowEngine> _logger;

        private string _workflowName = string.Empty;
        private WorkflowRootConfig? _config;
        private int _maxAttempts;

        public WorkflowEngine(
            INotificationRepository notificationRepository,
            ICustomerRepository customerRepository,
            Func<string, INotificationProvider> providerFactory,
            ILogger<WorkflowEngine> logger)
        {
            _notificationRepository = notificationRepository;
            _customerRepository = customerRepository;
            _providerFactory = providerFactory;
            _logger = logger;
        }

        public async Task<ExitCode> ExecuteAsync(string workflowPath)
        {
            if (!File.Exists(workflowPath))
            {
                _logger.LogError("YAML config file not found: {Path}", workflowPath);
                return ExitCode.FileNotFound;
            }

            _workflowName = Path.GetFileNameWithoutExtension(workflowPath);
            _logger.LogRunWorkflow(_workflowName);

            byte[] yamlBytes = await File.ReadAllBytesAsync(workflowPath);

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
                _logger.LogError("Failed to deserialize YAML workflow configuration.");
                return ExitCode.InvalidConfig;
            }

            if (!_config.Enabled)
            {
                _logger.LogInformation("Workflow {Name} is disabled.", _config.Name);
                return ExitCode.Success;
            }

            _maxAttempts = _config.Schedule?.Count ?? 0;

            await ProcessEventsAsync();
            await SetFirstSendAfterAsync();
            await RecoverStuckTasksAsync();

            int batchLimit = _config.BatchLimit > 0 ? _config.BatchLimit : 100;
            DateTime now = DateTime.Now; // need Kiev

            // 1. Fetch pending tasks ready for sending
            var tasks = await _notificationRepository.GetPendingTasksAsync(_config.Name, _maxAttempts, now, batchLimit);
            if (!tasks.Any())
            {
                return ExitCode.Success;
            }

            // 2. Atomically claim tasks for processing
            var taskIds = tasks.Select(t => t.NotificationQueueId).ToList();
            var claimedIds = await _notificationRepository.ClaimTasksAsync(_config.Name, taskIds);
            if (!claimedIds.Any())
            {
                return ExitCode.Success;
            }

            var claimedSet = new HashSet<long>(claimedIds);
            var tasksToProcess = tasks.Where(t => claimedSet.Contains(t.NotificationQueueId)).ToList();

            string providerKey = !string.IsNullOrEmpty(_config.Provider) ? _config.Provider : "viber";
            bool isEmailProvider = providerKey.Equals("email", StringComparison.OrdinalIgnoreCase);

            var tasksToSend = new List<NotificationTask>();
            var notifications = new List<NotificationItem>();

            foreach (var task in tasksToProcess)
            {
                // Validation: Contact Info
                if (isEmailProvider && string.IsNullOrWhiteSpace(task.Email))
                {
                    await ApplyTransitionAndCancelAsync(task, "Customer email address not found.");
                    continue;
                }
                if (!isEmailProvider && string.IsNullOrWhiteSpace(task.Telephone))
                {
                    await ApplyTransitionAndCancelAsync(task, "Customer phone number not found.");
                    continue;
                }

                // Validation: Dynamic Conditions
                if (!await ValidateConditionsAsync(task))
                {
                    await ApplyTransitionAndCancelAsync(task, "Conditions not met.");
                    continue;
                }

                int attemptNumber = task.Attempts + 1;
                string payload = ResolvePayload(providerKey, task, attemptNumber);

                if (string.IsNullOrWhiteSpace(payload))
                {
                    await ApplyTransitionAndCancelAsync(task, $"Empty payload for attempt #{attemptNumber}");
                    continue;
                }

                task.Payload = payload;
                var stateMachine = BuildStateMachine(task.Status);

                if (stateMachine.CanFire("start_processing"))
                {
                    stateMachine.Fire("start_processing");
                    task.Status = stateMachine.State;

                    tasksToSend.Add(task);
                    notifications.Add(CreateNotificationObject(providerKey, task, payload));
                }
            }

            if (!tasksToSend.Any())
            {
                return ExitCode.Success;
            }

            // 3. Batch Sending via Provider
            bool success = await ProcessBatchAsync(providerKey, tasksToSend, notifications);
            return success ? ExitCode.Success : ExitCode.InvalidOperation;
        }

        private async Task ProcessEventsAsync()
        {
            if (_config?.Events == null ||
                !_config.Events.Triggers.Any() ||
                string.IsNullOrEmpty(_config.Events.Code) ||
                string.IsNullOrEmpty(_config.Events.Action))
            {
                return;
            }

            var triggers = _config.Events.Triggers.Distinct().OrderBy(t => t).ToList();
            var existingTriggers = await _notificationRepository.GetEventTriggersAsync(_config.Events.Code, _config.Events.Action);

            if (existingTriggers.SequenceEqual(triggers))
            {
                return;
            }

            await _notificationRepository.SyncEventsAsync(_config.Events.Code, _config.Events.Action, triggers);
        }

        private async Task SetFirstSendAfterAsync()
        {
            var uninitializedTasks = await _notificationRepository.GetUninitializedTasksAsync(_config!.Name);
            if (!uninitializedTasks.Any()) return;

            var updates = new Dictionary<long, DateTime>();
            foreach (var task in uninitializedTasks)
            {
                DateTime sendAfter = CalculateSendAfter(1, task.DateAdded);
                updates[task.NotificationQueueId] = sendAfter;
            }

            await _notificationRepository.BatchUpdateSendAfterAsync(updates);
        }

        private async Task RecoverStuckTasksAsync()
        {
            await _notificationRepository.RecoverStuckTasksAsync(_config!.Name, TimeSpan.FromMinutes(10));
        }

        private async Task<bool> ValidateConditionsAsync(NotificationTask task)
        {
            if (_config?.Conditions == null || !_config.Conditions.Any())
            {
                return true;
            }

            foreach (var cond in _config.Conditions.Values)
            {
                if (string.IsNullOrEmpty(cond.Query)) continue;

                int count = await _notificationRepository.ExecuteConditionQueryAsync(cond.Query, task);
                if (count < cond.MinCount)
                {
                    return false;
                }
            }

            return true;
        }

        private string ResolvePayload(string provider, NotificationTask task, int attempt)
        {
            return provider.ToLowerInvariant() switch
            {
                "viber" => RenderViberPayload(task, attempt),
                "sms" => RenderSmsPayload(task, attempt),
                "email" => RenderEmailPayload(task, attempt),
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

            // Remove Emojis & normalize spaces
            string pattern = @"[\u1F600-\u1F64F\u1F300-\u1F5FF\u1F680-\u1F6FF\u1F1E0-\u1F1FF\u1F900-\u1F9FF\u1FA70-\u1FAFF\u2600-\u26FF\u2700-\u27BF\uFE00-\uFE0F\u200D]";
            string cleanText = Regex.Replace(payload, pattern, string.Empty);
            cleanText = Regex.Replace(cleanText, @"\s+", " ");

            return cleanText.Trim();
        }

        private string RenderEmailPayload(NotificationTask task, int attempt)
        {
            var template = ResolveTemplate(attempt, task);
            if (template == null) return string.Empty;

            string lang = task.LanguageCode ?? "uk";
            string text = ResolveLocalizedField(template.Text, lang)?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(text)) return string.Empty;

            var replacements = GetReplacements(task);
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

        private MessageVariant? ResolveTemplate(int attempt, NotificationTask task)
        {
            if (_config?.Message == null || !_config.Message.ContainsKey(attempt))
            {
                return _config?.Message?.Values.LastOrDefault()?.Values.FirstOrDefault();
            }

            var variants = _config.Message[attempt];
            if (!variants.Any()) return null;

            // A/B testing selection
            var firstVariant = variants.Values.First();
            if (!string.IsNullOrEmpty(firstVariant.Experiment))
            {
                string group = AssignCustomerGroup(task.Telephone ?? task.CustomerId.ToString(), firstVariant.Experiment);
                if (variants.TryGetValue(group, out var selectedVariant))
                {
                    return selectedVariant;
                }
            }

            return firstVariant;
        }

        private string AssignCustomerGroup(string seed, string experimentName)
        {
            // Deterministic hash assignment for A/B testing
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed + experimentName));
            return (hash[0] % 2 == 0) ? "A" : "B";
        }

        private string RenderMessage(NotificationTask task, int attempt)
        {
            var template = ResolveTemplate(attempt, task);
            if (template == null) return string.Empty;

            string lang = task.LanguageCode ?? "uk";
            string text = ResolveLocalizedField(template.Text, lang)?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(text)) return string.Empty;

            var replacements = GetReplacements(task);
            string message = ReplacePlaceholders(text, replacements);

            if (!string.IsNullOrEmpty(template.ButtonUrl))
            {
                string url = ReplacePlaceholders(template.ButtonUrl, replacements);
                message += $"\n\n{url}";
            }

            return message;
        }

        private Dictionary<string, string> GetReplacements(NotificationTask task)
        {
            return new Dictionary<string, string>
            {
                { "{first_name}", !string.IsNullOrEmpty(task.Firstname) ? task.Firstname : "покупець" },
                { "{last_name}", task.Lastname ?? string.Empty },
                { "{email}", task.Email ?? string.Empty },
                { "{phone}", task.Telephone ?? string.Empty },
                { "{customer_id}", task.CustomerId.ToString() }
            };
        }

        private string ReplacePlaceholders(string input, Dictionary<string, string> replacements)
        {
            foreach (var kvp in replacements)
            {
                input = input.Replace(kvp.Key, kvp.Value);
            }
            return input;
        }

        private string? ResolveLocalizedField(Dictionary<string, string>? field, string languageCode)
        {
            if (field == null || !field.Any()) return null;
            if (field.TryGetValue(languageCode, out var val)) return val;
            return field.Values.FirstOrDefault();
        }

        private async Task<bool> ProcessBatchAsync(string providerKey, List<NotificationTask> tasks, List<NotificationItem> notifications)
        {
            try
            {
                var provider = _providerFactory(providerKey);
                int chunkSize = _config?.ChunkSize > 0 ? _config.ChunkSize : 90;
                int chunkDelay = _config?.ChunkDelay > 0 ? _config.ChunkDelay : 1;

                var chunks = notifications.Chunk(chunkSize).ToList();

                for (int i = 0; i < chunks.Count; i++)
                {
                    await provider.SendBulkAsync(chunks[i]);

                    if (i < chunks.Count - 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(chunkDelay));
                    }
                }

                await FinalizeBatchAsync(tasks);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch sending failed for workflow {Workflow}", _config?.Name);

                // Revert status to pending upon failure
                var taskIds = tasks.Select(t => t.NotificationQueueId).ToList();
                await _notificationRepository.ResetTaskStatusAsync(taskIds, "pending");
                return false;
            }
        }

        private async Task FinalizeBatchAsync(List<NotificationTask> tasks)
        {
            var completedUpdates = new Dictionary<long, string>();
            var pendingUpdates = new Dictionary<long, (DateTime SendAfter, string Payload)>();
            var logEntries = new List<NotificationSendLog>();

            DateTime now = DateTime.Now;

            foreach (var task in tasks)
            {
                int attemptsDone = task.Attempts; // Current attempts after claim
                var stateMachine = BuildStateMachine(task.Status);

                logEntries.Add(new NotificationSendLog
                {
                    NotificationQueueId = task.NotificationQueueId,
                    SessionId = task.SessionId,
                    CustomerId = task.CustomerId,
                    Telephone = task.Telephone,
                    Action = _config!.Name,
                    AttemptNumber = attemptsDone,
                    Payload = task.Payload,
                    Status = "sent",
                    ScheduledAt = task.SendAfter ?? task.DateAdded,
                    SentAt = now
                });

                if (attemptsDone >= _maxAttempts)
                {
                    if (stateMachine.CanFire("complete"))
                    {
                        stateMachine.Fire("complete");
                    }
                    completedUpdates[task.NotificationQueueId] = task.Payload;
                }
                else
                {
                    if (stateMachine.CanFire("schedule_next"))
                    {
                        stateMachine.Fire("schedule_next");
                    }
                    DateTime nextSendAfter = CalculateSendAfter(attemptsDone + 1, task.DateAdded);
                    pendingUpdates[task.NotificationQueueId] = (nextSendAfter, task.Payload);
                }
            }

            if (completedUpdates.Any())
            {
                await _notificationRepository.BatchCompleteTasksAsync(completedUpdates);
            }

            if (pendingUpdates.Any())
            {
                await _notificationRepository.BatchRescheduleTasksAsync(pendingUpdates);
            }

            await _notificationRepository.LogSendBatchAsync(logEntries);
        }

        private async Task ApplyTransitionAndCancelAsync(NotificationTask task, string reason)
        {
            var stateMachine = BuildStateMachine(task.Status);
            if (stateMachine.CanFire("cancel"))
            {
                stateMachine.Fire("cancel");
            }

            await _notificationRepository.CancelTaskAsync(task.NotificationQueueId, reason);
            _logger.LogWarning("Task #{TaskId} cancelled ({Status}). Reason: {Reason}", task.NotificationQueueId, stateMachine.State, reason);
        }

        private DateTime CalculateSendAfter(int attempt, DateTime createdAt)
        {
            if (_config?.Schedule == null || !_config.Schedule.ContainsKey(attempt))
            {
                throw new ArgumentException($"No schedule rule for attempt {attempt}");
            }

            var rule = _config.Schedule[attempt];
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

        private DateTime ParseModifyRule(DateTime baseDate, string modify)
        {
            var match = Regex.Match(modify.Trim(), @"^([+-]?\d+)\s*(hour|hours|day|days|month|months)$", RegexOptions.IgnoreCase);
            if (!match.Success) return baseDate;

            int value = int.Parse(match.Groups[1].Value);
            string unit = match.Groups[2].Value.ToLowerInvariant();

            return unit switch
            {
                "hour" or "hours" => baseDate.AddHours(value),
                "day" or "days" => baseDate.AddDays(value),
                "month" or "months" => baseDate.AddMonths(value),
                _ => baseDate
            };
        }

        private StateMachine<string, string> BuildStateMachine(string currentState)
        {
            if (_config == null) throw new InvalidOperationException("Configuration is not loaded.");

            var machine = new StateMachine<string, string>(currentState);

            foreach (var (transitionName, transitionConfig) in _config.Workflow.Transitions)
            {
                foreach (string fromPlace in transitionConfig.From)
                {
                    machine.Configure(fromPlace)
                        .Permit(transitionName, transitionConfig.To);
                }
            }

            return machine;
        }

        private NotificationItem CreateNotificationObject(string provider, NotificationTask task, string payload)
        {
            string lang = task.LanguageCode ?? "uk";
            string subject = ResolveEmailSubject(task, task.Attempts);

            return new NotificationItem
            {
                Provider = provider,
                Recipient = provider.Equals("email", StringComparison.OrdinalIgnoreCase) ? task.Email! : task.Telephone!,
                Subject = subject,
                Body = payload
            };
        }

        private string ResolveEmailSubject(NotificationTask task, int attempt)
        {
            var template = ResolveTemplate(attempt, task);
            if (template != null)
            {
                string? localizedSubject = ResolveLocalizedField(template.Subject, task.LanguageCode ?? "uk");
                if (!string.IsNullOrEmpty(localizedSubject)) return localizedSubject;
            }

            return _config?.Subject ?? "Повідомлення";
        }
    }
}

/* using Microsoft.Extensions.Logging;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Core.Models.Yaml;
using Notify.Helper;
using Stateless;
using VYaml.Serialization;

namespace Notify.Services
{
    public class WorkflowEngine : IWorkflowEngine
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly Func<string, INotificationProvider> _providerFactory;
        private readonly ILogger<WorkflowEngine> _logger;

        private string _workflowName = string.Empty;
        private WorkflowRootConfig? _config;

        public WorkflowEngine(
            ICustomerRepository customerRepository,
            Func<string, INotificationProvider> providerFactory,
            ILogger<WorkflowEngine> logger)
        {
            this._customerRepository = customerRepository;
            this._providerFactory = providerFactory;
            this._logger = logger;
        }

        public async Task<ExitCode> ExecuteAsync(string workflowPath)
        {
            this._workflowName = Path.GetFileNameWithoutExtension(workflowPath);

            this._logger.LogRunWorkflow(this._workflowName);

            var customer = await _customerRepository.GetByIdAsync(40301);

            var provider = _providerFactory("email");

            Console.WriteLine(customer);

            byte[] yamlBytes = await File.ReadAllBytesAsync(workflowPath);

            this._config = YamlSerializer.Deserialize<WorkflowRootConfig>(yamlBytes, new YamlSerializerOptions()
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

            Console.WriteLine(this._config.ToString());

            return ExitCode.Success;
        }

        private StateMachine<string, string> BuildStateMachine(string currentState)
        {
            if (this._config == null)
            {
                throw new InvalidOperationException();
            }

            // Передаємо поточний стан (place) та тип тригера (переходу)
            StateMachine<string, string> machine = new StateMachine<string, string>(currentState);

            // Налаштовуємо всі transitions з вашого YAML var (transitionName, transitionConfig)
            foreach (KeyValuePair<string, TransitionConfig> kvp in this._config.Workflow.Transitions)
            {
                foreach (string fromPlace in kvp.Value.From)
                {
                    StateMachine<string, string>.StateConfiguration stateConfig = machine.Configure(fromPlace);

                    // Додаємо перехід з умовою або без
                    stateConfig.Permit(kvp.Key, kvp.Value.To);
                }
            }

            return machine;
        }
    }
}*/