using Microsoft.Extensions.Logging;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Core.Models;
using Notify.Helper;
using Notify.Infrastructure.Serialization;

namespace Notify.Infrastructure.Providers
{
    public class ViberSMSClubProvider : INotificationProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ViberSMSClubProvider> _logger;

        public MessageProvider Channel => MessageProvider.Viber;

        public ViberSMSClubProvider(HttpClient httpClient, ILogger<ViberSMSClubProvider> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> SendAsync(IEnumerable<NotificationItem> notifications, CancellationToken cancellationToken = default)
        {
            NotificationItem[] messages = notifications.ToArray();

            if (messages.Length == 0)
            {
                return true;
            }

            try
            {
                ViberRequestDto payload = new ViberRequestDto()
                {
                    Phones = messages.Select(m => m.Recipient.Trim()),
                    Message = messages[0].Body.Trim()
                };

                using (
                    HttpResponseMessage response = await _httpClient.PostAsJsonAsync<ViberRequestDto>(
                        requestUri: "vibers/send",
                        value: payload,
                        jsonTypeInfo: AppJsonContext.Default.ViberRequestDto,
                        cancellationToken: cancellationToken
                    )
                ) {
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }

                    string errorResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    _logger.LogViberSMSClubDispatchFailed((int)response.StatusCode, errorResponseBody, messages.Length);

                    return false;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogViberSMSClubDispatchWasCanceled();
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogHTTPNetworkErrorWhileCallingViberSMSClubAPI(ex, messages.Length);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogUnexpectedErrorOccurredDuringViberSMSClubDispatch(ex, messages.Length);
                return false;
            }
        }
    }
}