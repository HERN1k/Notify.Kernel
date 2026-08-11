using Microsoft.Extensions.DependencyInjection;
using Notify.Core.Abstractions;
using Notify.Helper;

namespace Notify
{
    internal class Program
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        static async Task Main(string[] args)
        {
            Initializer init = new Initializer(args);

            Program.ServiceProvider = init.GetServiceProvider();

            var factory = Program.ServiceProvider.GetRequiredService<Func<string, INotificationProvider>>();

            var emailProvider = factory("email");
            var viberProvider = factory("viber");
            var smsProvider = factory("sms");

            Console.WriteLine("Ready!"); 

            var db = Program.ServiceProvider.GetRequiredService<ICustomerRepository>();

            var c = await db.GetByIdAsync(40301);

            Console.WriteLine(c);
            Console.ReadLine();
        }
    }
}