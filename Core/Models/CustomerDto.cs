using Notify.Infrastructure.Serialization;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Notify.Core.Models
{
    public sealed record CustomerDto
    {
        private static readonly AppJsonContext PrettyJsonContext = new(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        public int CustomerId { get; init; }
        public int CustomerGroupId { get; init; }
        public int StoreId { get; init; }
        public int LanguageId { get; init; }
        public string Firstname { get; init; } = string.Empty;
        public string Lastname { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public DateTime VerifyEmailDate { get; init; }
        public string Telephone { get; init; } = string.Empty;
        public DateTime VerifyTelephoneDate { get; init; }
        public string Fax { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Salt { get; init; } = string.Empty;
        public string? Cart { get; init; }
        public string? Wishlist { get; init; }
        public bool Newsletter { get; init; }
        public int AddressId { get; init; }
        public string CustomField { get; init; } = string.Empty;
        public string ExtendedAuth { get; init; } = string.Empty;
        public string Ip { get; init; } = string.Empty;
        public bool Status { get; init; }
        public bool Approved { get; init; }
        public bool Safe { get; init; }
        public string Token { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public DateTime DateAdded { get; init; }
        public string AssolShopId { get; init; } = string.Empty;

        public CustomerDto() { }

        public CustomerDto(IDataRecord record)
        {
            CustomerId = Convert.ToInt32(record["customer_id"]);
            CustomerGroupId = Convert.ToInt32(record["customer_group_id"]);
            StoreId = Convert.ToInt32(record["store_id"]);
            LanguageId = Convert.ToInt32(record["language_id"]);
            Firstname = record["firstname"].ToString() ?? string.Empty;
            Lastname = record["lastname"].ToString() ?? string.Empty;
            Email = record["email"].ToString() ?? string.Empty;
            VerifyEmailDate = Convert.ToDateTime(record["verify_email_date"]);
            Telephone = record["telephone"].ToString() ?? string.Empty;
            VerifyTelephoneDate = Convert.ToDateTime(record["verify_telephone_date"]);
            Fax = record["fax"].ToString() ?? string.Empty;
            Password = record["password"].ToString() ?? string.Empty;
            Salt = record["salt"].ToString() ?? string.Empty;
            int cartOrdinal = record.GetOrdinal("cart");
            Cart = record.IsDBNull(cartOrdinal) ? null : record.GetString(cartOrdinal);
            int wishlistOrdinal = record.GetOrdinal("wishlist");
            Wishlist = record.IsDBNull(wishlistOrdinal) ? null : record.GetString(wishlistOrdinal);
            Newsletter = Convert.ToBoolean(record["newsletter"]);
            AddressId = Convert.ToInt32(record["address_id"]);
            CustomField = record["custom_field"].ToString() ?? string.Empty;
            ExtendedAuth = record["extended_auth"].ToString() ?? string.Empty;
            Ip = record["ip"].ToString() ?? string.Empty;
            Status = Convert.ToBoolean(record["status"]);
            Approved = Convert.ToBoolean(record["approved"]);
            Safe = Convert.ToBoolean(record["safe"]);
            Token = record["token"].ToString() ?? string.Empty;
            Code = record["code"].ToString() ?? string.Empty;
            DateAdded = Convert.ToDateTime(record["date_added"]);
            AssolShopId = record["assol_shop_id"].ToString() ?? string.Empty;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, PrettyJsonContext.CustomerDto);
        }
    }
}