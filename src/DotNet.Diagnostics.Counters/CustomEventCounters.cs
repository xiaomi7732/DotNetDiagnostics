namespace DotNet.Diagnostics.Counters;

/// <summary>
/// Data contract to take in custom event counters in <see cref="DotNetCountersOptions" />.
/// </summary>
public class CustomEventCounters
{
    /// <summary>
    /// Gets or sets whether to clear default event counters.
    /// When sets to true, only custom event counters will be used.
    /// When sets to false, custom event counters will be appended to the default event counters.
    /// In such case, when both provided, but with different settings on the keywords, or event levels, or filters,
    /// the custom settings wins.
    /// </summary>
    public bool ClearDefaultEventCounters { get; set; } = false;

    /// <summary>
    /// The custom event counter configurations.
    /// </summary>
    public IEnumerable<EventCounterItem> Items { get; set; } = Enumerable.Empty<EventCounterItem>();
}