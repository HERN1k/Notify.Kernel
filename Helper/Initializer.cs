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
using Serilog.Formatting.Compact; 
using Serilog.Sinks.SystemConsole.Themes;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Notify.Helper
{
    public sealed class Initializer
    {
        private readonly IHost _host;

        public Initializer(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            DateTime date = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows()
                        ? "FLE Standard Time"
                        : "Europe/Kyiv"
                )
            );

            IArgs argsParser = new ArgsParser(args);
            string logsPath = argsParser.Get("logs") ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(logsPath))
            {
                throw new ArgumentException("The '--logs' parameter is binding and cannot be left empty");
            }

            logsPath = Path.Combine(logsPath, string.Concat("notifier-", date.ToString("yyyy-MM-dd"), ".log"));

            string? logDir = Path.GetDirectoryName(logsPath);
            if (!string.IsNullOrEmpty(logDir))
            {
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                foreach (string file in Directory.GetFiles(logDir, "notifier-*.log"))
                {
                    if (File.GetLastWriteTime(file) < date.AddDays(-14))
                    {
                        File.Delete(file);
                    }
                }
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    theme: AnsiConsoleTheme.Code, 
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.File(
                    formatter: new CompactJsonFormatter(),     
                    path: logsPath
                )
                .CreateLogger();

            HostApplicationBuilder builder = new HostApplicationBuilder(args);
            
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog();

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
                }
            };

            IServiceCollection services = builder.Services;

            services.AddSingleton<IArgs>(argsParser);
            services.AddSingleton(appSettings);
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddTransient<IWorkflowRunner, WorkflowRunner>();
            services.AddTransient<IDbConnection>(_ => new MySqlConnection(appSettings.Database?.ConnectionString));
            services.AddTransient<IWorkflowEngine, WorkflowEngine>();

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

            services.AddTransient<Func<string, INotificationProvider>>(sp => key =>
            {
                if (Enum.TryParse<Notify.Core.Enums.MessageProvider>(key, ignoreCase: true, out var provider))
                {
                    return provider switch
                    {
                        Notify.Core.Enums.MessageProvider.SMS => sp.GetRequiredService<SmsSMSClubProvider>(),
                        Notify.Core.Enums.MessageProvider.Viber => sp.GetRequiredService<ViberSMSClubProvider>(),
                        Notify.Core.Enums.MessageProvider.Email => sp.GetRequiredService<EmailEsputnikProvider>(),
                        _ => throw new KeyNotFoundException($"Provider '{provider}' not supported")
                    };
                }

                throw new KeyNotFoundException($"Provider with key '{key}' not found");
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