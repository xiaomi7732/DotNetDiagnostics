namespace DotNet.Diagnostics.Counters.WebEndpoints;

public record RequestBodyContract
{
    /// <summary>
    /// Gets or sets the expected new state of the dotnet-counters.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets or sets the environment variable filter for jobs
    /// </summary>
    public IDictionary<string, string> EnvVarFilters { get; set; } = new Dictionary<string, string>();
}