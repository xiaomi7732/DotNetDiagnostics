namespace DotNet.Diagnostics.Counters.WebHooks;

public record RequestError
{
    public int StatusCode { get; init; } = 0;
    public string Message { get; init; } = default!;
}