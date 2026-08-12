using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Notify.Core.Abstractions;
using Notify.Core.Enums;
using Notify.Helper;

namespace Notify
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            ExitCode exitCode = ExitCode.Success;
            IHost? host = null;
            ILogger<Program>? logger = null;

            try
            {
                Initializer initializer = new Initializer(args);
                host = initializer.Host;

                await host.StartAsync();

                logger = host.Services.GetRequiredService<ILogger<Program>>();

                IWorkflowRunner runner = host.Services.GetRequiredService<IWorkflowRunner>();

                exitCode = await runner.RunAsync();
            }
            catch (ArgumentException ex)
            {
                if (logger != null) logger.LogInvalidArgument(ex);
                else Log.Error(ex, "Invalid argument provided during startup");

                exitCode = ExitCode.InvalidArguments;
            }
            catch (OperationCanceledException ex)
            {
                logger?.LogOperationCanceled(ex);
                exitCode = ExitCode.OperationCanceled;
            }
            catch (KeyNotFoundException ex)
            {
                logger?.LogConfigurationError(ex);
                exitCode = ExitCode.ConfigurationError;
            }
            catch (InvalidOperationException ex)
            {
                logger?.LogInvalidOperation(ex);
                exitCode = ExitCode.InvalidOperation;
            }
            catch (Exception ex)
            {
                if (logger != null) logger.LogUnhandledException(ex);
                else Log.Fatal(ex, "Unhandled critical error during application startup");

                exitCode = ExitCode.UnhandledException;
            }
            finally
            {
                if (host != null)
                {
                    await host.StopAsync();
                    host.Dispose();
                }

                await Log.CloseAndFlushAsync();
            }

            return (int)exitCode;
        }
    }
}