namespace DotNet.Diagnostics.Counters.WebHooks;

public record RequestBodyContract
{
    public bool IsEnabled { get; init; }
}