namespace Notify.Core.Models
{
    public sealed class EmailRequestDto
    {
        public List<string> Emails { get; set; } = new List<string>();
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}