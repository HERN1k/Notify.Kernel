using Notify.Core.Enums;

namespace Notify.Core.Models
{
    public sealed class NotificationItem
    {
        public MessageProvider Provider { get; private set; } = MessageProvider.Viber;
        public string Recipient { get; private set; } = string.Empty;
        public string Subject { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;

        public NotificationItem(MessageProvider provider, string recipient, string subject, string body) 
        {
            this.Provider = provider;
            this.Recipient = recipient;
            this.Subject = subject;
            this.Body = body;
        }
    }
}