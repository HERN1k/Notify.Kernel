using Notify.Helper;
using System.Data;

namespace Notify.Core.Models
{
    public sealed class NotificationQueueItem
    {
        public int NotificationQueueId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public int? CustomerGroupId { get; set; }
        public string Telephone { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public byte Attempts { get; set; }
        public string Action { get; set; } = string.Empty;
        public DateTime? SendAfter { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime DateModified { get; set; }
        public string Ip { get; set; } = string.Empty;
        public string Firstname { get; set; } = string.Empty;
        public string Lastname { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Enums.LanguageCode LanguageCode { get; set; } = Enums.LanguageCode.UK;

        public NotificationQueueItem() { }

        public NotificationQueueItem(IDataReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            NotificationQueueId = reader.GetInt32(reader.GetOrdinal("notification_queue_id"));
            SessionId = reader.GetString(reader.GetOrdinal("session_id"));

            int customerIdOrdinal = reader.GetOrdinal("customer_id");
            CustomerId = reader.IsDBNull(customerIdOrdinal)
                ? null
                : reader.GetInt32(customerIdOrdinal);

            int customerGroupIdOrdinal = reader.GetOrdinal("customer_group_id");
            CustomerGroupId = reader.IsDBNull(customerGroupIdOrdinal)
                ? null
                : reader.GetInt32(customerGroupIdOrdinal);

            Telephone = reader.GetString(reader.GetOrdinal("telephone"));
            Payload = reader.GetString(reader.GetOrdinal("payload"));
            Status = reader.GetString(reader.GetOrdinal("status"));

            Attempts = Convert.ToByte(reader.GetValue(reader.GetOrdinal("attempts")));

            Action = reader.GetString(reader.GetOrdinal("action"));

            int sendAfterOrdinal = reader.GetOrdinal("send_after");
            SendAfter = reader.IsDBNull(sendAfterOrdinal)
                ? null
                : reader.GetDateTime(sendAfterOrdinal);

            DateAdded = reader.GetDateTime(reader.GetOrdinal("date_added"));
            DateModified = reader.GetDateTime(reader.GetOrdinal("date_modified"));
            Ip = reader.GetString(reader.GetOrdinal("ip"));

            Firstname = reader.GetStringOrDefault("firstname");
            Lastname = reader.GetStringOrDefault("lastname");
            Email = reader.GetStringOrDefault("email");

            if (reader.HasColumn("language_id"))
            {
                int langOrdinal = reader.GetOrdinal("language_id");
                int? langId = reader.IsDBNull(langOrdinal) 
                    ? null 
                    : reader.GetInt32(langOrdinal);

                LanguageCode = langId switch
                {
                    1 => Enums.LanguageCode.RU,
                    3 => Enums.LanguageCode.UK,
                    _ => Enums.LanguageCode.UK
                };
            }
        }
    }
}