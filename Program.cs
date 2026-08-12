using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Helper;

namespace Notify
{
    internal class Program
    {
        private static ExitCode _exitCode = ExitCode.Success;
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        static async Task<int> Main(string[] args)
        {
            try
            {
                Initializer init = new Initializer(args);

                ServiceProvider = init.GetServiceProvider();

                IArgs parser = ServiceProvider.GetRequiredService<IArgs>();

                string workflowsPath = parser.Get("workflows") ?? string.Empty;

                if (string.IsNullOrEmpty(workflowsPath))
                {
                    throw new ArgumentNullException("--workflows");
                }

                string fullWorkflowsPath = Path.GetFullPath(workflowsPath.Trim());

                Matcher matcher = new Matcher();
                matcher.AddInclude("**/*.yml");
                matcher.AddInclude("**/*.yaml");
                matcher.AddExclude("**/_*");

                IEnumerable<string> workflowPaths = matcher
                    .Execute(new DirectoryInfoWrapper(new DirectoryInfo(fullWorkflowsPath))).Files
                    .Select(f => Path.Combine(fullWorkflowsPath, f.Path));

                foreach (string workflowPath in workflowPaths)
                {
                    _exitCode = ExitCode.UnhandledException;

                    using (IServiceScope workflowEngineScope = ServiceProvider.CreateScope())
                    {
                        IWorkflowEngine engine = workflowEngineScope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

                        _exitCode = await engine.ExecuteAsync(workflowPath);
                    }

                    if (_exitCode.Equals(ExitCode.UnhandledException))
                    {
                        break;
                    }
                }
            }
            catch (ArgumentNullException ex)
            {
                // Log this exception
                _exitCode = ExitCode.InvalidArguments;
            }
            catch (OperationCanceledException ex)
            {
                // Log this exception
                _exitCode = ExitCode.OperationCanceled;
            }
            catch (KeyNotFoundException ex)
            {
                // Log this exception
                _exitCode = ExitCode.ConfigurationError;
            }
            catch (InvalidOperationException)
            {
                // Log this exception
                _exitCode = ExitCode.InvalidOperation;
            }
            catch (Exception ex)
            {
                // Log this exception
                _exitCode = ExitCode.UnhandledException;
            }

            return (int)_exitCode;

            /* var factory = Program.ServiceProvider.GetRequiredService<Func<string, INotificationProvider>>();

            var emailProvider = factory("email");
            var viberProvider = factory("viber");
            var smsProvider = factory("sms");

            Console.WriteLine("Ready!");

            IArgs parser = Program.ServiceProvider.GetRequiredService<IArgs>();

            Console.WriteLine(parser.Get("workflows"));

            var db = Program.ServiceProvider.GetRequiredService<ICustomerRepository>();

            var c = await db.GetByIdAsync(40301);

            Console.WriteLine(c); */
        }
    }
}