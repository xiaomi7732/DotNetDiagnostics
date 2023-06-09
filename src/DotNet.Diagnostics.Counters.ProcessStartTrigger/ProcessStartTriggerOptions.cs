using DotNet.Diagnostics.Core;

namespace DotNet.Diagnostics.Counters.Triggers;

public class ProcessStartTriggerOptions : TriggerOptions
{
    public new const string DefaultSectionName = "ProcessStart";
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(5);
}