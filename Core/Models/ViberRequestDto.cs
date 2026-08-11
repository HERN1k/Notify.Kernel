namespace Notify.Core.Models
{
    public class ViberRequestDto
    {
        public List<string> Phones { get; set; } = new List<string>();
        public string Message { get; set; } = string.Empty;
    }
}