namespace Notify.Core.Models
{
    public sealed class NotificationTask
    {
        public NotificationQueueItem Item { get; }

        public string Status { get; set; }

        public NotificationTask(NotificationQueueItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            Item = item;
            Status = string.IsNullOrWhiteSpace(item.Status) ? "pending" : item.Status;
        }

        public int Id => Item.NotificationQueueId;
        public string SessionId => Item.SessionId;
        public int? CustomerId => Item.CustomerId;
        public int? CustomerGroupId => Item.CustomerGroupId;
        public string Telephone => Item.Telephone;
        public string Payload => Item.Payload;
        public byte Attempts => Item.Attempts;
        public string Action => Item.Action;
        public DateTime? SendAfter => Item.SendAfter;
        public DateTime DateAdded => Item.DateAdded;
        public DateTime DateModified => Item.DateModified;
        public string Ip => Item.Ip;
        public string Firstname => Item.Firstname;
        public string Lastname => Item.Lastname;
        public string Email => Item.Email;
        public Enums.LanguageCode LanguageCode => Item.LanguageCode;

        public void SetAttempts(int attempts)
        {
            Item.Attempts = (byte)attempts;
        }

        public void SetPayload(string payload) 
        {
            if (!string.IsNullOrWhiteSpace(payload)) 
            {
                Item.Payload = payload;
            }
        }
    }
}