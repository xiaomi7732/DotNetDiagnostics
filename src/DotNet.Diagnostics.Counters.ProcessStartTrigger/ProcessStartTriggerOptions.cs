namespace DotNet.Diagnostics.Counters.Triggers;

public class ProcessStartTriggerOptions
{
    public const string DefaultSectionName = "ProcessStart";
    public bool IsEnabled { get; set; } = true;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(5);
}