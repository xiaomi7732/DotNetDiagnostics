using System.Collections.Concurrent;
using DotNet.Diagnostics.Core;

namespace DotNet.Diagnostics.Counters.WebEndpoints;

internal sealed class EndpointSink : ISink<IDotNetCountersClient, ICounterPayload>, ICounterPayloadSet
{
    private EndpointSink()
    {
        _counterHolder = new ConcurrentDictionary<string, ICounterPayload>();
    }
    public static EndpointSink Instance { get; } = new EndpointSink();


    // ProviderName_CounterName - payload object
    private readonly ConcurrentDictionary<string, ICounterPayload> _counterHolder;

    public IReadOnlyCollection<ICounterPayload> Data => _counterHolder is null || _counterHolder.Count == 0 ?
        new List<ICounterPayload>().AsReadOnly() :
        new List<ICounterPayload>(_counterHolder.Values).AsReadOnly();

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        // Do nothing
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public bool Submit(ICounterPayload data)
    {
        string key = GetKey(data);
        ICounterPayload newValue = _counterHolder.AddOrUpdate(key, addValueFactory: k => data, (k, oldValue) => data);
        return object.ReferenceEquals(newValue, data);
    }

    private string GetKey(ICounterPayload payload)
    {
        return $"{payload.Provider}#.#{payload.Name}";
    }
}