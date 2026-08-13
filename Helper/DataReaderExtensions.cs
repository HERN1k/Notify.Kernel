using System.Data;

namespace Notify.Helper
{
    public static class DataReaderExtensions
    {
        public static bool HasColumn(this IDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static string GetStringOrDefault(this IDataReader reader, string columnName, string defaultValue = "")
        {
            if (!reader.HasColumn(columnName))
            {
                return defaultValue;
            }

            int ordinal = reader.GetOrdinal(columnName);

            return reader.IsDBNull(ordinal) 
                ? defaultValue 
                : reader.GetString(ordinal);
        }
    }
}