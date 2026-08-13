using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Helper;
using System.Collections.Concurrent;

namespace Notify.Services
{
    public sealed class WorkflowRunner : IWorkflowRunner
    {
        private readonly IArgs _args;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowRunner> _logger;

        public WorkflowRunner(IArgs args, IServiceProvider serviceProvider, ILogger<WorkflowRunner> logger)
        {
            _args = args;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<ExitCode> RunAsync(CancellationToken ct = default)
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

            if (!workflowPaths.Any())
            {
                _logger.LogCompletedProcessing(ExitCode.Success);
                return ExitCode.Success;
            }

            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount - 1),
                CancellationToken = ct
            };

            ConcurrentBag<ExitCode> exitCodes = new ConcurrentBag<ExitCode>();

            try
            {
                await Parallel.ForEachAsync(workflowPaths, options, async (workflowPath, token) =>
                {
                    using (IServiceScope workflowScope = _serviceProvider.CreateScope())
                    {
                        try
                        {
                            IWorkflowEngine engine = workflowScope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

                            ExitCode code = await engine.ExecuteAsync(workflowPath, token);

                            exitCodes.Add(code);
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            exitCodes.Add(ExitCode.OperationCanceled);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWorkflowExecutionFailed(ex, workflowPath);
                            exitCodes.Add(ExitCode.UnhandledException);
                        }
                    }
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogCompletedProcessing(ExitCode.OperationCanceled);
                return ExitCode.OperationCanceled;
            }

            ExitCode finalExitCode = exitCodes.Contains(ExitCode.UnhandledException)
                ? ExitCode.UnhandledException
                : exitCodes.FirstOrDefault(code => code != ExitCode.Success, ExitCode.Success);
            
            _logger.LogCompletedProcessing(finalExitCode);
            return finalExitCode;
        }
    }
}