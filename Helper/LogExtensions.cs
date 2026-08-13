using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notify.Core.Enums;
using System.Diagnostics.Tracing;
using System.Dynamic;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "YAML config file not found: {WorkflowPath}")]
        public static partial void LogYAMLConfigNotFound(this ILogger logger, string workflowPath);

        [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to deserialize YAML workflow configuration")]
        public static partial void LogFailedDeserializeYAMLWorkflowConfiguration(this ILogger logger);

        [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Workflow {Name} is disabled")]
        public static partial void LogWorkflowIsDisabled(this ILogger logger, string name);

        [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Task #{TaskId} cancelled ({Status}). Reason: {Reason}")]
        public static partial void TaskCancelledWithReason(this ILogger logger, int taskId, string status, string reason);

        [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Batch sending failed for workflow {Workflow}")]
        public static partial void LogBatchSendingFailed(this ILogger logger, Exception ex, string workflow);

        [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Viber SMSClub dispatch failed with HTTP {StatusCode}. Response: {ResponseBody}. Count: {Count}")]
        public static partial void LogViberSMSClubDispatchFailed(this ILogger logger, int statusCode, string responseBody, int count);

        [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Viber SMSClub dispatch was canceled by CancellationToken")]
        public static partial void LogViberSMSClubDispatchWasCanceled(this ILogger logger);

        [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "HTTP network error while calling Viber SMSClub API. Count: {Count}")]
        public static partial void LogHTTPNetworkErrorWhileCallingViberSMSClubAPI(this ILogger logger, Exception ex, int count);

        [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Unexpected error occurred during Viber SMSClub dispatch. Count: {Count}")]
        public static partial void LogUnexpectedErrorOccurredDuringViberSMSClubDispatch(this ILogger logger, Exception ex, int count);

        // 


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