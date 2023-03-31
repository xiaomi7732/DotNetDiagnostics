namespace DotNet.Diagnostics.Counters.WebEndpoints;

public record RequestError
{
    public int StatusCode { get; init; } = 0;
    public string Message { get; init; } = default!;
}