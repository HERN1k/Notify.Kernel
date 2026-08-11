using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Core.Models;
using Notify.Infrastructure.Serialization;

namespace Notify.Infrastructure.Providers
{
    public class ViberSMSClubProvider : INotificationProvider
    {
        private readonly HttpClient _httpClient;
        public MessageProvider Channel => MessageProvider.Viber;

        public ViberSMSClubProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> SendAsync(List<string> recipients, string message, CancellationToken cancellationToken = default)
        {
            ViberRequestDto payload = new ViberRequestDto()
            {
                Phones = recipients,
                Message = message
            };

            var response = await _httpClient.PostAsJsonAsync<ViberRequestDto>("https://api.smsclub.mobi/v2/sms/send", payload, AppJsonContext.Default.ViberRequestDto, cancellationToken);

            return response.IsSuccessStatusCode;
        }
    }
}