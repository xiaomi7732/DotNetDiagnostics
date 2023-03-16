using System.Diagnostics.Tracing;

namespace DotNet.Diagnostics.Counters;


public class EventCounterItem
{
    public string Name { get; set; } = default!;

    public EventLevel EventLevel { get; set; }

    public string Keywords { get; set; } = "0x0";

    public IEnumerable<string> Filters { get; set; } = Enumerable.Empty<string>();

    /// <summary>
    /// Gets or sets the interval for events.
    /// </summary>
    public int IntervalInSeconds { get; set; } = 1;
}