namespace DotNet.Diagnostics.Counters;

public interface IPayloadHeaderWriter
{
    Task WriteHeaderAsync(Stream writeTo, CancellationToken cancellationToken);
}