using System.Collections.Concurrent;
using DotNet.Diagnostics.Core;

namespace DotNet.Diagnostics.Counters.WebEndpoints;

internal sealed class EndpointSink : ISink<IDotNetCountersClient, ICounterPayload>, ICounterPayloadSet
{
    private EndpointSink(DotnetCountersProcessIdProvider processIdProvider)
    {
        _counterHolder = new ConcurrentDictionary<string, ICounterPayload>();
        _processIdProvider = processIdProvider ?? throw new ArgumentNullException(nameof(processIdProvider));
        _processIdProvider.ProcessChanged += OnProcessIdChanged;
    }
    public static EndpointSink Instance { get; } = new EndpointSink(DotnetCountersProcessIdProvider.Instance);

    private void OnProcessIdChanged(object? sender, int? newValue)
    {
        const string processIdKey = "processId";
        string processIdString = newValue == null ? string.Empty : newValue.ToString()!;
        if (Metadata.ContainsKey(processIdKey))
        {
            Metadata[processIdKey] = processIdString;
        }
        else
        {
            Metadata.TryAdd(processIdKey, processIdString);
        }
    }

    // ProviderName_CounterName - payload object
    private readonly ConcurrentDictionary<string, ICounterPayload> _counterHolder;
    private readonly DotnetCountersProcessIdProvider _processIdProvider;

    public IReadOnlyCollection<ICounterPayload> Data => _counterHolder is null || _counterHolder.Count == 0 ?
        new List<ICounterPayload>().AsReadOnly() :
        new List<ICounterPayload>(_counterHolder.Values).AsReadOnly();

    public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

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