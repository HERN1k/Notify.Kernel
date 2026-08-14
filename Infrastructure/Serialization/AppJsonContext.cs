using Notify.Core.Configuration;
using Notify.Core.Models;
using System.Text.Json.Serialization;

namespace Notify.Infrastructure.Serialization
{
    [JsonSerializable(typeof(CustomerDto))]
    [JsonSerializable(typeof(SmsRequestDto))]
    [JsonSerializable(typeof(SmsResponseDto))] 
    [JsonSerializable(typeof(ViberRequestDto))]
    [JsonSerializable(typeof(ViberResponseDto))]
    [JsonSerializable(typeof(EmailRequestDto))]
    [JsonSerializable(typeof(EmailResponseDto))]
    [JsonSerializable(typeof(AppConfiguration))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}