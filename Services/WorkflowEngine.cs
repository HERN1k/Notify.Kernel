using Notify.Core.Abstractions;

namespace Notify.Services
{
    public class WorkflowEngine : IWorkflowEngine
    {
        private readonly Dictionary<string, INotificationProvider> _providers;

        public WorkflowEngine(IEnumerable<INotificationProvider> providers)
        {
            _providers = providers.ToDictionary(p => p.Channel.ToString(), p => p, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<int> ExecuteAsync(string workflowName)
        {
            await Task.Delay(500);

            return 0;
        }
    }
}
