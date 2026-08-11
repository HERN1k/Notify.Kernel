namespace Notify.Core.Abstractions
{
    public interface IWorkflowEngine
    {
        Task<int> ExecuteAsync(string workflowName);
    }
}