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

        [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "{MessageProvider} {Provider} dispatch failed with HTTP {StatusCode}. Response: {ResponseBody}. Count: {Count}")]
        public static partial void LogAPIProviderDispatchFailed(this ILogger logger, MessageProvider messageProvider, string provider, int statusCode, string responseBody, int count);

        [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "{MessageProvider} {Provider} dispatch was canceled by CancellationToken")]
        public static partial void LogAPIProviderDispatchWasCanceled(this ILogger logger, MessageProvider messageProvider, string provider);

        [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "HTTP network error while calling {MessageProvider} {Provider} API. Count: {Count}")]
        public static partial void LogHTTPNetworkErrorWhileCallingAPI(this ILogger logger, Exception ex, MessageProvider messageProvider, string provider, int count);

        [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Unexpected error occurred during {MessageProvider} {Provider} dispatch. Count: {Count}")]
        public static partial void LogUnexpectedErrorOccurredDuringDispatch(this ILogger logger, Exception ex, MessageProvider messageProvider, string provider, int count);

        [LoggerMessage(EventId = 13, Level = LogLevel.Error, Message = "Failed to execute workflow: {Path}")]
        public static partial void LogWorkflowExecutionFailed(this ILogger logger, Exception ex, string path);

        [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "{Provider}: Bulk {MessageProvider} successfully dispatched to {Count} recipient. Response: {Response}")]
        public static partial void LogBulkMessagesSuccessfullyDispatched(this ILogger logger, string provider, MessageProvider messageProvider, int count, string response);

        [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Workflow completed for name: {WorkflowName}")]
        public static partial void LogWorkflowCompleted(this ILogger logger, string workflowName);

        [LoggerMessage(EventId = 16, Level = LogLevel.Warning, Message = "PHP task callback '{Callback}' failed with status code {StatusCode}. Response: {ResponseBody}")]
        public static partial void LogPhpTaskFailed(this ILogger logger, string callback, int statusCode, string responseBody);

        [LoggerMessage(EventId = 17, Level = LogLevel.Warning, Message = "PHP task execution for '{Callback}' was canceled")]
        public static partial void LogPhpTaskCanceled(this ILogger logger, string callback);

        [LoggerMessage(EventId = 18, Level = LogLevel.Error, Message = "Network error occurred while triggering PHP task '{Callback}'")]
        public static partial void LogPhpTaskNetworkError(this ILogger logger, Exception exception, string callback);

        [LoggerMessage(EventId = 19, Level = LogLevel.Error, Message = "Unexpected error occurred while triggering PHP task '{Callback}'")]
        public static partial void LogPhpTaskUnexpectedError(this ILogger logger, Exception exception, string callback);

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