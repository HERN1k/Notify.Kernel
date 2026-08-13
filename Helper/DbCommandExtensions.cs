using System.Data.Common;
using System.Text.RegularExpressions;

namespace Notify.Helper
{
    public static class DbCommandExtensions
    {
        public static string AddParam<T>(this DbCommand cmd, string name, T value)
        {
            DbParameter param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            cmd.Parameters.Add(param);
            return name;
        }

        public static string ToUnsafeFullSql(this DbCommand cmd)
        {
            string sql = cmd.CommandText;

            IOrderedEnumerable<DbParameter> parameters = cmd.Parameters
                .Cast<DbParameter>()
                .OrderByDescending(p => p.ParameterName.Length);

            foreach (DbParameter p in parameters)
            {
                string valueStr = p.Value switch
                {
                    null or DBNull => "NULL",
                    DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                    string s => $"'{s.Replace("'", "''")}'",
                    bool b => b ? "1" : "0",
                    _ => p.Value?.ToString() ?? "NULL"
                };

                sql = System.Text.RegularExpressions.Regex.Replace(
                    sql,
                    Regex.Escape(p.ParameterName) + @"\b",
                    valueStr
                );
            }

            return sql;
        }
    }
}