using System.Diagnostics.Tracing;

namespace DotNet.Diagnostics.Counters;

/// <summary>
/// A sets of well known event counters.
/// Refer to https://learn.microsoft.com/en-us/dotnet/core/diagnostics/available-counters for details.
/// </summary>
internal static class KnownEventCounters
{
    public static IEnumerable<EventCounterItem> GetWellKnownProviders() => CreateKnownCounters();

    private static IEnumerable<EventCounterItem> CreateKnownCounters()
    {
        yield return new EventCounterItem()
        {
            Name = "System.Runtime",
            Keywords = "0xffffffff",
            EventLevel = EventLevel.Verbose,
        };

        yield return new EventCounterItem()
        {
            Name = "Microsoft.AspNetCore.Hosting",
            Keywords = "0x0",
            EventLevel = EventLevel.Informational,
        };

        yield return new EventCounterItem()
        {
            Name = "Microsoft-AspNetCore-Server-Kestrel",
            Keywords = "0x0",
            EventLevel = EventLevel.Informational,
        };

        yield return new EventCounterItem()
        {
            Name = "System.Net.Http",
            Keywords = "0x0",
            EventLevel = EventLevel.Informational,
        };

        yield return new EventCounterItem()
        {
            Name = "System.Net.NameResolution",
            Keywords = "0x0",
            EventLevel = EventLevel.Informational,
        };

        yield return new EventCounterItem()
        {
            Name = "System.Net.Security",
            Keywords = "0x0",
            EventLevel = EventLevel.Informational,
        };

        yield return new EventCounterItem()
        {
            Name = "System.Net.Sockets",
            Keywords = "0x0",
            EventLevel = EventLevel.Informational,
        };
    }
}