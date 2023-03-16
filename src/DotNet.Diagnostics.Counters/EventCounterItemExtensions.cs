using System.Globalization;
using Microsoft.Diagnostics.NETCore.Client;

namespace DotNet.Diagnostics.Counters;

internal static class EventCounterItemExtensions
{
    public static EventPipeProvider? ToEventPipeProvider(this EventCounterItem item)
    {
        if (item is null)
        {
            return null;
        }

        string keywordsString = item.Keywords;
        if (string.IsNullOrEmpty(keywordsString))
        {
            keywordsString = "0x0";
        }

        // Could throw FormatException or Overflow Exception. Pretty obvious for debugging.
        long keywords = Convert.ToInt64(item.Keywords, fromBase: 16);

        return new EventPipeProvider(
            item.Name,
            item.EventLevel,
            keywords,
            new Dictionary<string, string>()
            {
                ["EventCounterIntervalSec"] = item.IntervalInSeconds.ToString(CultureInfo.InvariantCulture),
            });
    }
}