using Microsoft.Extensions.Logging;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Core.Models;
using Notify.Helper;
using Notify.Infrastructure.Serialization;

namespace Notify.Infrastructure.Providers
{
    public class EmailEsputnikProvider : INotificationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmailEsputnikProvider> _logger;

        public MessageProvider Channel => MessageProvider.Email;

        public EmailEsputnikProvider(HttpClient httpClient, ILogger<EmailEsputnikProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> SendAsync(IEnumerable<NotificationItem> notifications, CancellationToken ct = default)
        {
            NotificationItem[] messages = notifications.ToArray();

            if (messages.Length == 0)
            {
                return true;
            }

            try
            {
                EmailRequestDto payload = new EmailRequestDto()
                {
                    Emails = messages.Select(m => m.Recipient.Trim()).ToArray(),
                    Subject = messages[0].Subject.Trim(),
                    HtmlText = messages[0].Body.Trim(),
                    PlainText = HtmlUtils.HTMLToPlainText(messages[0].Body.Trim())
                };

                using (
                    HttpResponseMessage response = await _httpClient.PostAsJsonAsync<EmailRequestDto>(
                        requestUri: "message/email",
                        value: payload,
                        jsonTypeInfo: AppJsonContext.Default.EmailRequestDto,
                        cancellationToken: ct
                    )
                )
                {
                    string responseBody = await response.Content.ReadAsStringAsync(ct);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogBulkMessagesSuccessfullyDispatched("Esputnik", Channel, payload.Emails.Length, responseBody);
                        return true;
                    }

                    _logger.LogAPIProviderDispatchFailed(Channel, "Esputnik", (int)response.StatusCode, responseBody, messages.Length);

                    return false;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogAPIProviderDispatchWasCanceled(Channel, "Esputnik");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogHTTPNetworkErrorWhileCallingAPI(ex, Channel, "Esputnik", messages.Length);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogUnexpectedErrorOccurredDuringDispatch(ex, Channel, "Esputnik", messages.Length);
                return false;
            }
        }
    }
}
