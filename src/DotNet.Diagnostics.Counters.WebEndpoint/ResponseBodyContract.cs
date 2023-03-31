namespace DotNet.Diagnostics.Counters.WebEndpoints;

public record ResponseBodyContract
{
    public bool IsEnabled { get; init; }
}