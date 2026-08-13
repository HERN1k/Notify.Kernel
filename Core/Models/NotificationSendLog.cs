namespace Notify.Core.Models
{
    public sealed class NotificationSendLog
    {
        public int Id { get; set; }
        public int NotificationQueueId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string Telephone { get; set; } = string.Empty;
        public string Action { get; set; } = "abandoned_cart";
        public byte AttemptNumber { get; set; } = 1;
        public string Payload { get; set; } = string.Empty;
        public string Status { get; set; } = "sent";
        public string? ErrorMessage { get; set; }
        public DateTime ScheduledAt { get; set; }
        public DateTime SentAt { get; set; } = Program.DateTimeKiev;
    }
}