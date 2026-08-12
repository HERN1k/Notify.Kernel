namespace Notify.Core.Abstractions
{
    public interface IArgs
    {
        string? Get(string key, string? defaultValue = null);
        bool HasFlag(string key);
        int GetInt(string key, int defaultValue = 0);
        IReadOnlyList<string> Positional { get; }
    }
}