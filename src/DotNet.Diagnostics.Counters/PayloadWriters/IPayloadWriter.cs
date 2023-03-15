namespace DotNet.Diagnostics.Counters;

public interface IPayloadWriter
{
    Task WriteAsync(Stream toStream, ICounterPayload payload, CancellationToken cancellationToken);
}