using Microsoft.Extensions.Logging;
using Notify.Core.Enums;

namespace Notify.Helper
{
    public static partial class LogExtensions
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting workflow engine for path: {WorkflowsPath}")]
        public static partial void LogStartingProcessing(this ILogger logger, string workflowsPath);

        [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Starting workflow for name: {WorkflowName}")]
        public static partial void LogRunWorkflow(this ILogger logger, string workflowName);

        [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Workflows processing completed with exit code: {ExitCode}")]
        public static partial void LogCompletedProcessing(this ILogger logger, ExitCode exitCode);



        [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Invalid arguments provided")]
        public static partial void LogInvalidArgument(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Operation was canceled")]
        public static partial void LogOperationCanceled(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Configuration error encountered")]
        public static partial void LogConfigurationError(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Invalid operation executed")]
        public static partial void LogInvalidOperation(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 1004, Level = LogLevel.Critical, Message = "Unhandled exception occurred during execution")]
        public static partial void LogUnhandledException(this ILogger logger, Exception ex);
    }
}