namespace DotNet.Diagnostics.Counters.WebEndpoints;

public interface ICounterPayloadSet
{
    public Dictionary<string, string> Metadata { get; }
    public IReadOnlyCollection<ICounterPayload> Data { get; }
}