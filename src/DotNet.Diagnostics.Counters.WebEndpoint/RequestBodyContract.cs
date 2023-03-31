namespace DotNet.Diagnostics.Counters.WebEndpoints;

public record RequestBodyContract
{
    public bool IsEnabled { get; init; }
    public string InvokingSecret { get; set; } = string.Empty;
}