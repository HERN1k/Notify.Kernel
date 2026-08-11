using Dapper;
using Notify.Core.Abstractions;
using Notify.Core.Models;
using System.Data;
using System.Data.Common;
using System.Reflection.Metadata;

namespace Notify.Infrastructure.Data
{
    public sealed class CustomerRepository : ICustomerRepository
    {
        private readonly IDbConnection _dbConnection;

        public CustomerRepository(IDbConnection dbConnection)
        {
            this._dbConnection = dbConnection;
        }

        public async Task<CustomerDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            if (this._dbConnection is not DbConnection dbConn)
            {
                throw new InvalidOperationException("Connection must inherit from DbConnection");
            }

            if (dbConn.State != ConnectionState.Open)
            {
                await dbConn.OpenAsync(cancellationToken);
            }

            await using (DbCommand cmd = dbConn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM `customer` WHERE `customer_id` = @Id;";

                DbParameter param = cmd.CreateParameter();
                param.ParameterName = "@Id";
                param.Value = id;
                cmd.Parameters.Add(param);

                await using (DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken)) 
                {
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        return new CustomerDto(reader);
                    }

                    return null;
                }
            }
        }
    }
}