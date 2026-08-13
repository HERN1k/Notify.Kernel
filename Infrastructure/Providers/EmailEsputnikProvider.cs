using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Core.Models;
using Notify.Infrastructure.Serialization;

namespace Notify.Infrastructure.Providers
{
    public class EmailEsputnikProvider : INotificationProvider
    {
        private readonly HttpClient _httpClient;
        public MessageProvider Channel => MessageProvider.Email;

        public EmailEsputnikProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> SendAsync(IEnumerable<NotificationItem> notifications, CancellationToken cancellationToken = default)
        {
            return true;
            /* EmailRequestDto payload = new EmailRequestDto()
            {
                Emails = recipients,
                Subject = "",
                Body = message
            };

            var response = await _httpClient.PostAsJsonAsync<EmailRequestDto>("https://api.smsclub.mobi/v2/sms/send", payload, AppJsonContext.Default.EmailRequestDto, cancellationToken);

            return response.IsSuccessStatusCode; */
        }
    }
}
