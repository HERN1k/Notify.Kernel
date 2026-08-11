using System.Text.Json.Serialization;
using Notify.Core.Models;

namespace Notify.Infrastructure.Serialization
{
    [JsonSerializable(typeof(CustomerDto))]
    [JsonSerializable(typeof(SmsRequestDto))]
    [JsonSerializable(typeof(SmsResponseDto))] 
    [JsonSerializable(typeof(ViberRequestDto))]
    [JsonSerializable(typeof(ViberResponseDto))]
    [JsonSerializable(typeof(EmailRequestDto))]
    [JsonSerializable(typeof(EmailResponseDto))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}