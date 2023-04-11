namespace DotNet.Diagnostics.Core.Utilities;

public sealed class EnvVarMatcher
{
    public static EnvVarMatcher Instance { get; } = new EnvVarMatcher();
    private EnvVarMatcher()
    { }

    public bool MatchAll(IDictionary<string, string> filters) => filters.All(pair => Match(pair.Key, pair.Value));

    public bool Match(string key, string value)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
        {
            return false;
        }

        string? actual = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(actual))
        {
            return false;
        }

        return string.Equals(value, actual, StringComparison.Ordinal);
    }
}