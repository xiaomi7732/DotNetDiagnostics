namespace DotNet.Diagnostics.Counters.WebEndpoints;

public interface ICounterPayloadSet
{
    public IReadOnlyCollection<ICounterPayload> Data { get; }
}