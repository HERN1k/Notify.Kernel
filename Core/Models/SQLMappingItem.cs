namespace Notify.Core.Models
{
    public sealed class SQLMappingItem
    {
        public string Token { get; private set; }
        public string ParamName { get; private set; }
        public object? Value {
            get
            {
                if (field == null) 
                {
                    return DBNull.Value;
                }

                return field;
            }
            private set;
        }

        public SQLMappingItem(string token, string paramName, object value) 
        {
            this.Token      = token;
            this.ParamName  = paramName;
            this.Value      = value;
        }
    }
}