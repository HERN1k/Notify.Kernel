using System.Text.Json.Serialization;

namespace Notify.Core.Models
{
    public sealed class SmsRequestDto
    {
        [JsonPropertyName("phone")]
        public string[] Phones { get; set; } = Array.Empty<string>();

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("src_addr")]
        public string Sender { get; init; } = "ASSOL";
    }
}