using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Notify.Core.Abstractions;
using Notify.Core.Configuration;
using Notify.Infrastructure.Data;
using Notify.Infrastructure.Providers;
using Notify.Services;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact; 
using Serilog.Sinks.SystemConsole.Themes;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Notify.Helper
{
    public sealed class Initializer
    {
        private readonly IHost _host;

        public Initializer(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            IArgs argsParser = new ArgsParser(args);
            string logsPath = argsParser.Get("logs") ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(logsPath))
            {
                throw new ArgumentException("The '--logs' parameter is binding and cannot be left empty");
            }

            logsPath = Path.Combine(logsPath, string.Concat("notifier-", Program.DateTimeKiev.ToString("yyyy-MM-dd"), ".log"));

            string? logDir = Path.GetDirectoryName(logsPath);
            if (!string.IsNullOrEmpty(logDir))
            {
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                foreach (string file in Directory.GetFiles(logDir, "notifier-*.log"))
                {
                    if (File.GetLastWriteTime(file) < Program.DateTimeKiev.AddDays(-14))
                    {
                        File.Delete(file);
                    }
                }
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
                .MinimumLevel.Override("Polly", LogEventLevel.Warning)
                .WriteTo.Console(
                    theme: AnsiConsoleTheme.Code, 
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    formatter: new CompactJsonFormatter(),     
                    path: logsPath,
                    shared: true
                )
                .CreateLogger();
            
            HostApplicationBuilder builder = new HostApplicationBuilder(args);
            
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger, dispose: false);

            IConfigurationManager config = builder.Configuration;
            
            AppConfiguration appSettings = new AppConfiguration()
            {
                Database = new DatabaseConfiguration()
                {
                    ConnectionString = config["Database:ConnectionString"]
                },
                Providers = new ProvidersConfiguration()
                {
                    Esputnik = config["Providers:Esputnik"],
                    SMSClub = config["Providers:SMSClub"]
                },
                PHPClient = new PHPClientConfiguration()
                {
                    Domain = config["PHPClient:Domain"],
                    Token = config["PHPClient:Token"]
                }
            };

            if (appSettings.PHPClient == null || string.IsNullOrEmpty(appSettings.PHPClient.Domain) || string.IsNullOrEmpty(appSettings.PHPClient.Token))
            {
                throw new ArgumentException("The 'PHPClient' configuration is empty");
            }

            IServiceCollection services = builder.Services;

            services.AddSingleton<IArgs>(argsParser);
            services.AddSingleton(appSettings);
            services.AddScoped<IRepository, Repository>();
            services.AddTransient<IWorkflowRunner, WorkflowRunner>();
            services.AddTransient<IDbConnection>(_ => new MySqlConnection(appSettings.Database?.ConnectionString));

            services.AddHttpClient<IWorkflowEngine, WorkflowEngine>(client =>
            {
                client.BaseAddress = new Uri(appSettings.PHPClient.Domain);
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("X-Internal-Secret", appSettings.PHPClient.Token);
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddStandardResilienceHandler();

            services.AddHttpClient<EmailEsputnikProvider>(client =>
            {
                client.BaseAddress = new Uri("https://esputnik.com/api/v1/");
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                if (!string.IsNullOrEmpty(appSettings.Providers?.Esputnik))
                {
                    string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(appSettings.Providers.Esputnik));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddStandardResilienceHandler();

            services.AddHttpClient<ViberSMSClubProvider>(client =>
            {
                client.BaseAddress = new Uri("https://im.smsclub.mobi/");
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                if (!string.IsNullOrEmpty(appSettings.Providers?.SMSClub))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.Providers.SMSClub);
                }
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddStandardResilienceHandler();

            services.AddHttpClient<SmsSMSClubProvider>(client =>
            {
                client.BaseAddress = new Uri("https://im.smsclub.mobi/");
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                if (!string.IsNullOrEmpty(appSettings.Providers?.SMSClub))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appSettings.Providers.SMSClub);
                }
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddStandardResilienceHandler();

            services.AddTransient<Func<Notify.Core.Enums.MessageProvider, INotificationProvider>>(sp => provider =>
            {
                return provider switch
                {
                    Notify.Core.Enums.MessageProvider.SMS => sp.GetRequiredService<SmsSMSClubProvider>(),
                    Notify.Core.Enums.MessageProvider.Viber => sp.GetRequiredService<ViberSMSClubProvider>(),
                    Notify.Core.Enums.MessageProvider.Email => sp.GetRequiredService<EmailEsputnikProvider>(),
                    _ => throw new KeyNotFoundException($"Provider '{provider}' not supported")
                };
            });

            this._host = builder.Build(); 
        }

        public IServiceProvider Services => _host.Services;

        public IHost Host => _host;

        private static SocketsHttpHandler CreateHandler() => new SocketsHttpHandler()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
    }
}