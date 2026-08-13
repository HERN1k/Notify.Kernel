using System.Text.Json.Serialization;

namespace Notify.Core.Models
{
    public sealed class EmailRequestDto
    {
        [JsonPropertyName("from")]
        public string From { get; init; } = "email@assol.in.ua";

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("htmlText")]
        public string HtmlText { get; set; } = string.Empty;

        [JsonPropertyName("plainText")]
        public string? PlainText { get; set; }

        [JsonPropertyName("emails")]
        public string[] Emails { get; set; } = Array.Empty<string>();
    }
}