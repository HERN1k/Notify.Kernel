namespace Notify.Core.Models
{
    public sealed class SmsRequestDto
    {
        public List<string> Phones { get; set; } = new List<string>();
        public string Message { get; set; } = string.Empty;
    }
}