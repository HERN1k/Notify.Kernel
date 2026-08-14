using Microsoft.Extensions.Logging;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Core.Models;
using Notify.Helper;
using Notify.Infrastructure.Serialization;
using System.Net.Http.Json;

namespace Notify.Infrastructure.Providers
{
    public class SmsSMSClubProvider : INotificationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SmsSMSClubProvider> _logger;

        public MessageProvider Channel => MessageProvider.SMS;

        public SmsSMSClubProvider(HttpClient httpClient, ILogger<SmsSMSClubProvider> logger)
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
                SmsRequestDto payload = new SmsRequestDto()
                {
                    Phones = messages.Select(m => m.Recipient.Trim()).ToArray(),
                    Message = messages[0].Body.Trim()
                };

                using (
                    HttpResponseMessage response = await _httpClient.PostAsJsonAsync<SmsRequestDto>(
                        requestUri: "sms/send",
                        value: payload,
                        jsonTypeInfo: AppJsonContext.Default.SmsRequestDto,
                        cancellationToken: ct
                    )
                )
                {
                    string responseBody = await response.Content.ReadAsStringAsync(ct);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogBulkMessagesSuccessfullyDispatched("SMSClub", Channel, payload.Phones.Length, responseBody);
                        return true;
                    }

                    _logger.LogAPIProviderDispatchFailed(Channel, "SMSClub", (int)response.StatusCode, responseBody, messages.Length);

                    return false;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogAPIProviderDispatchWasCanceled(Channel, "SMSClub");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogHTTPNetworkErrorWhileCallingAPI(ex, Channel, "SMSClub", messages.Length);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogUnexpectedErrorOccurredDuringDispatch(ex, Channel, "SMSClub", messages.Length);
                return false;
            }
        }
    }
}