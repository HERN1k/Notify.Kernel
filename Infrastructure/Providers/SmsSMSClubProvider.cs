using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Core.Models;
using Notify.Infrastructure.Serialization;

namespace Notify.Infrastructure.Providers
{
    public class SmsSMSClubProvider : INotificationProvider
    {
        private readonly HttpClient _httpClient;
        public MessageProvider Channel => MessageProvider.SMS;

        public SmsSMSClubProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> SendAsync(IEnumerable<NotificationItem> notifications, CancellationToken cancellationToken = default)
        {
            return true;
            /* SmsRequestDto payload = new SmsRequestDto() { 
                Phones = recipients, 
                Message = message
            };
            
            var response = await _httpClient.PostAsJsonAsync<SmsRequestDto>("https://api.smsclub.mobi/v2/sms/send", payload, AppJsonContext.Default.SmsRequestDto, cancellationToken);

            return response.IsSuccessStatusCode; */
        }
    }
}