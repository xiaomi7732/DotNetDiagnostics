using System.Collections.Concurrent;
using DotNet.Diagnostics.Core;

namespace DotNet.Diagnostics.Counters.WebEndpoints;

internal sealed class EndpointSink : SinkBase<IDotNetCountersClient, ICounterPayload>, ICounterPayloadSet
{
    // ProviderName_CounterName - payload object
    private readonly ConcurrentDictionary<string, ICounterPayload> _counterHolder;
    private readonly DotnetCountersProcessIdProvider _processIdProvider;

    private EndpointSink(DotnetCountersProcessIdProvider processIdProvider)
    {
        _counterHolder = new ConcurrentDictionary<string, ICounterPayload>();
        _processIdProvider = processIdProvider ?? throw new ArgumentNullException(nameof(processIdProvider));
        _processIdProvider.ProcessChanged += OnProcessIdChanged;
    }
    public static EndpointSink Instance { get; } = new EndpointSink(DotnetCountersProcessIdProvider.Instance);

    public IReadOnlyCollection<ICounterPayload> Data => _counterHolder is null || _counterHolder.Count == 0 ?
        new List<ICounterPayload>().AsReadOnly() :
        new List<ICounterPayload>(_counterHolder.Values).AsReadOnly();

    public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

    private string GetKey(ICounterPayload payload)
    {
        return $"{payload.Provider}#.#{payload.Name}";
    }

    protected override Task<bool> OnStartingAsync(CancellationToken cancellationToken)
    {
        _counterHolder.Clear();
        return Task.FromResult(true);
    }

    protected override Task<bool> OnStoppingAsync(CancellationToken cancellationToken)
    {
        _counterHolder.Clear();
        return Task.FromResult(true);
    }

    protected override bool OnSubmit(ICounterPayload data)
    {
        string key = GetKey(data);
        ICounterPayload newValue = _counterHolder.AddOrUpdate(key, addValueFactory: k => data, (k, oldValue) => data);
        return object.ReferenceEquals(newValue, data);
    }

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
}