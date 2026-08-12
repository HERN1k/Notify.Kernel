using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Notify.Core.Abstractions;
using Notify.Core.Configuration;
using Notify.Infrastructure.Data;
using Notify.Infrastructure.Providers;
using Notify.Services;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Notify.Helper
{
    public sealed class Initializer
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ServiceCollection _serviceDescriptors;
        
        public Initializer(string[] args)
        {
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            ArgsParser argsParser = new ArgsParser(args);

            ConfigurationBuilder configBuilder = new ConfigurationBuilder();
            configBuilder.SetBasePath(AppContext.BaseDirectory);
            configBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            string appSettingsDev = "appsettings.dev.json"; 
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, appSettingsDev)))
            {
                configBuilder.AddJsonFile(appSettingsDev, optional: true, reloadOnChange: true);
            }

            IConfigurationRoot config = configBuilder.Build();

            AppConfiguration appSettings = new AppConfiguration()
            {
                Database = new DatabaseConfiguration() 
                {
                    ConnectionString = config["Database:ConnectionString"]
                },
                Providers = new ProvidersConfiguration() 
                {
                    Esputnik = config["Providers:Esputnik"],
                    SMSClub  = config["Providers:SMSClub"]
                }
            };
            
            this._serviceDescriptors = new ServiceCollection();

            this._serviceDescriptors.AddSingleton<IArgs>(argsParser);

            this._serviceDescriptors.AddSingleton(appSettings);
            this._serviceDescriptors.AddSingleton<IConfiguration>(config);

            this._serviceDescriptors.AddTransient<IWorkflowEngine, WorkflowEngine>();

            this._serviceDescriptors.AddTransient<IDbConnection>((sp) => new MySqlConnection(appSettings.Database?.ConnectionString));
            this._serviceDescriptors.AddScoped<ICustomerRepository, CustomerRepository>();

            this._serviceDescriptors.AddHttpClient<EmailEsputnikProvider>(client =>
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

            this._serviceDescriptors.AddHttpClient<ViberSMSClubProvider>(client =>
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

            this._serviceDescriptors.AddHttpClient<SmsSMSClubProvider>(client =>
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

            this._serviceDescriptors.AddSingleton<Func<string, INotificationProvider>>(sp => key =>
            {
                if (Enum.TryParse<Notify.Core.Enums.MessageProvider>(key, ignoreCase: true, out var provider))
                {
                    return provider switch
                    {
                        Notify.Core.Enums.MessageProvider.SMS   => sp.GetRequiredService<SmsSMSClubProvider>(),
                        Notify.Core.Enums.MessageProvider.Viber => sp.GetRequiredService<ViberSMSClubProvider>(),
                        Notify.Core.Enums.MessageProvider.Email => sp.GetRequiredService<EmailEsputnikProvider>(),
                        _ => throw new KeyNotFoundException($"Provider '{provider}' not supported.")
                    };
                }

                throw new KeyNotFoundException($"Provider with key '{key}' not found.");
            });

            this._serviceProvider = this._serviceDescriptors.BuildServiceProvider();
        }

        public ServiceProvider GetServiceProvider() => this._serviceProvider;

        public ServiceCollection GetServiceDescriptors() => this._serviceDescriptors;

        private static SocketsHttpHandler CreateHandler() => new SocketsHttpHandler()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
    }
}