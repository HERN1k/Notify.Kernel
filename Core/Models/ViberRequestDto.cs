using System.Text.Json.Serialization;

namespace Notify.Core.Models
{
    public class ViberRequestDto
    {
        [JsonPropertyName("phones")]
        public string[] Phones { get; set; } = Array.Empty<string>();

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("sender")]
        public string Sender { get; init; } = "ASSOL";
    }
}