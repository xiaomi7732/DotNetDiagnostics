namespace DotNet.Diagnostics.Counters;

public class DotNetCountersOptions
{
    /// <summary>
    /// Gets a default dotnet counters options.
    /// </summary>
    /// <returns></returns>
    public static DotNetCountersOptions Default = new DotNetCountersOptions();

    /// <summary>
    /// Gets or sets the interval between .NET Counter events.
    /// Default to 1 second per read.
    /// </summary>
    public int IntervalSec { get; set; } = 1;

    /// <summary>
    /// Custom event counters when provided.
    public CustomEventCounters? CustomEventCounters { get; set; } = default;
}