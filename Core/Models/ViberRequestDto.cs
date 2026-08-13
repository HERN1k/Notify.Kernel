using System.Text.Json.Serialization;

namespace Notify.Core.Models
{
    public class ViberRequestDto
    {
        [JsonPropertyName("phones")]
        public IEnumerable<string> Phones { get; set; } = Enumerable.Empty<string>();

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("sender")]
        public string Sender { get; init; } = "ASSOL";
    }
}