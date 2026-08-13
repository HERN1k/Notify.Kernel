namespace Notify.Core.Configuration
{
    public record AppConfiguration
    {
        public DatabaseConfiguration? Database { get; init; }
        public ProvidersConfiguration? Providers { get; init; }
        public PHPClientConfiguration? PHPClient { get; init; }
    }

    public record DatabaseConfiguration
    {
        public string? ConnectionString { get; init; }
    }

    public record ProvidersConfiguration
    {
        public string? SMSClub { get; init; }
        public string? Esputnik { get; init; }
    }

    public record PHPClientConfiguration
    {
        public string? Domain { get; init; }
        public string? Token { get; init; }
    }
}