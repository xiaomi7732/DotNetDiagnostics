using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters;

public class DotNetCountEventCounterManager
{
    private readonly DotNetCountersOptions _options;
    private readonly ILogger _logger;

    public IReadOnlyCollection<EventCounterItem> EventCounters { get; }

    public DotNetCountEventCounterManager(
        IOptions<DotNetCountersOptions> options,
        ILogger<DotNetCountEventCounterManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        EventCounters = new List<EventCounterItem>(CreateEventCounterItems(_options.CustomEventCounters)).AsReadOnly();
    }

    private IEnumerable<EventCounterItem> CreateEventCounterItems(CustomEventCounters? customEventCounters)
    {
        List<EventCounterItem> result = GetCustomProviders(customEventCounters).ToList();
        
        if(customEventCounters?.ClearDefaultEventCounters == true)
        {
            // The user decide not to use any default provider.
            return result;
        }

        // Append default providers if it is not there yet.
        foreach(EventCounterItem item in GetDefaultProviders())
        {
            if(!result.Contains(item, EventCounterItemComparer.ByNameOrdinal))
            {
                result.Add(item);
            }
        }

        return result;
    }

    public bool IsEnabled(string providerName, string metricsName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            throw new ArgumentException($"'{nameof(providerName)}' cannot be null or empty.", nameof(providerName));
        }

        if (string.IsNullOrEmpty(metricsName))
        {
            throw new ArgumentException($"'{nameof(metricsName)}' cannot be null or empty.", nameof(metricsName));
        }

        EventCounterItem? item = EventCounters.FirstOrDefault(p => string.Equals(p.Name, providerName, StringComparison.Ordinal));
        if(item is null)
        {
            _logger.LogWarning("How is provider {providerName} enabled?", providerName);
            return false;
        }

        // Filter is not configured for the provider, default to allow all metrics
        if(item.Filters is null || !item.Filters.Any())
        {
            return true;
        }

        // If there's a hit
        return item.Filters.Any(item => string.Equals(item, metricsName, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<EventCounterItem> GetCustomProviders(CustomEventCounters? customEventCounters)
    {
        if (customEventCounters?.Items is null || !customEventCounters.Items.Any())
        {
            return Enumerable.Empty<EventCounterItem>();
        }
        return customEventCounters.Items;
    }

    private IEnumerable<EventCounterItem> GetDefaultProviders()
    {
        return KnownEventCounters.GetWellKnownProviders();
    }
}