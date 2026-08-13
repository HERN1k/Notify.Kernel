using Notify.Core.Enums;

namespace Notify.Core.Abstractions
{
    public interface IWorkflowRunner
    {
        Task<ExitCode> RunAsync(CancellationToken ct = default);
    }
}