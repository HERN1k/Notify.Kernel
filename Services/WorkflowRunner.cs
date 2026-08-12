using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Helper;

namespace Notify.Services
{
    public sealed class WorkflowRunner : IWorkflowRunner
    {
        private readonly IArgs _args;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowRunner> _logger;

        public WorkflowRunner(
            IArgs args,
            IServiceProvider serviceProvider,
            ILogger<WorkflowRunner> logger)
        {
            _args = args;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<ExitCode> RunAsync()
        {
            string workflowsPath = _args.Get("workflows") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(workflowsPath) || !Directory.Exists(workflowsPath))
            {
                throw new ArgumentException("The '--workflows' parameter is binding and cannot be left empty or point to a non-existing directory");
            }

            string fullWorkflowsPath = Path.GetFullPath(workflowsPath.Trim());
            _logger.LogStartingProcessing(fullWorkflowsPath);

            Matcher matcher = new Matcher();
            matcher.AddInclude("**/*.yml");
            matcher.AddInclude("**/*.yaml");
            matcher.AddExclude("**/_*");

            DirectoryInfo directoryInfo = new DirectoryInfo(fullWorkflowsPath);
            IEnumerable<string> workflowPaths = matcher
                .Execute(new DirectoryInfoWrapper(directoryInfo)).Files
                .Select(f => Path.Combine(fullWorkflowsPath, f.Path));

            ExitCode exitCode = ExitCode.Success;

            foreach (string workflowPath in workflowPaths)
            {
                using (IServiceScope workflowScope = _serviceProvider.CreateScope())
                {
                    IWorkflowEngine engine = workflowScope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

                    exitCode = await engine.ExecuteAsync(workflowPath);
                }

                if (exitCode == ExitCode.UnhandledException)
                {
                    break;
                }
            }

            _logger.LogCompletedProcessing(exitCode);
            return exitCode;
        }
    }
}