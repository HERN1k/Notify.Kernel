using Dapper;
using MySqlConnector;

namespace Notify
{
    internal class Program
    {
        private static readonly string connectionString = "Server=127.0.0.1;Port=3306;Database=assol;Uid=root2;Pwd=root2;";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Приложение запущено с Runtime Async!");

            await TestAsync();
        }

        private static async Task TestAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                Console.WriteLine("Успешно подключились к MySQL в OpenServer!");

                string selectSql = "SELECT customer_id AS CustomerId, firstname, lastname, telephone, email FROM customer WHERE customer_id = @CustomerId;";
                var customers = await connection.QueryAsync<CustomerDto>(selectSql, new { CustomerId = 40301 });

                foreach (var customer in customers)
                {
                    Console.WriteLine($"ID: {customer.CustomerId} | Firstname: {customer.Firstname} | Lastname: {customer.Lastname} | Telephone: {customer.Telephone} | Email: {customer.Email}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения или выполнения: {ex.Message}");
            }
        }
    }

    public class CustomerDto
    {
        public int CustomerId { get; set; }
        public string Firstname { get; set; } = string.Empty;
        public string Lastname { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}