namespace Notify.Core.Enums
{
    public enum ExitCode
    {
        Success = 0,
        InvalidArguments = 1,
        ConfigurationError = 2,
        DatabaseError = 3,
        ProviderError = 4,
        InvalidOperation = 5,
        OperationCanceled = 6,
        FileNotFound = 7,
        InvalidConfig = 8,
        UnhandledException = 99
    }
}