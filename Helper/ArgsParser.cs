using Notify.Core.Abstractions;

namespace Notify.Helper
{
    public class ArgsParser : IArgs
    {
        private readonly Dictionary<string, string> _flags = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _positional = new();

        public ArgsParser(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.StartsWith("-"))
                {
                    var key = arg.TrimStart('-');

                    var equalsIndex = key.IndexOf('=');
                    if (equalsIndex != -1)
                    {
                        var val = key[(equalsIndex + 1)..];
                        key = key[..equalsIndex];
                        _flags[key] = val;
                        continue;
                    }

                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        _flags[key] = args[++i];
                    }
                    else
                    {
                        _flags[key] = "true";
                    }
                }
                else
                {
                    _positional.Add(arg);
                }
            }
        }

        public string? Get(string key, string? defaultValue = null)
            => _flags.TryGetValue(key, out var val) ? val : defaultValue;

        public bool HasFlag(string key)
            => _flags.ContainsKey(key);

        public int GetInt(string key, int defaultValue = 0)
            => _flags.TryGetValue(key, out var val) && int.TryParse(val, out var res) ? res : defaultValue;

        public IReadOnlyList<string> Positional => _positional;
    }
}