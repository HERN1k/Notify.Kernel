using Notify.Core.Abstractions;
using Notify.Core.Enums;

namespace Notify.Services
{
    public class WorkflowEngine : IWorkflowEngine
    {
        private readonly IArgs _args;
        private readonly Dictionary<MessageProvider, INotificationProvider> _providers;

        private string _workflowName = string.Empty;

        public WorkflowEngine(IArgs args, IEnumerable<INotificationProvider> providers)
        {
            this._args = args;
            this._providers = providers.ToDictionary(p => p.Channel, p => p);
        }

        public async Task<ExitCode> ExecuteAsync(string workflowPath)
        {
            Console.WriteLine(workflowPath);

            this._workflowName = Path.GetFileNameWithoutExtension(workflowPath);

            Console.WriteLine(this._workflowName);

            return ExitCode.Success;
        }
    }
}
