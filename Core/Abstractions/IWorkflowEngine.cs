using Notify.Core.Enums;

namespace Notify.Core.Abstractions
{
    public interface IWorkflowEngine
    {
        Task<ExitCode> ExecuteAsync(string workflowPath);
    }
}