namespace DotNet.Diagnostics.Counters;

public class DotNetCountersOptions
{
    /// <summary>
    /// Gets or sets the interval between .NET Counter events.
    /// Default to 1 second per read.
    /// </summary>
    public int IntervalSec { get; set; } = 1;

    public static DotNetCountersOptions Default = new DotNetCountersOptions();
}