using System.Diagnostics.CodeAnalysis;

namespace DotNet.Diagnostics.Counters;

internal static class EventCounterItemComparer
{
    public static EventCounterItemNameOrdinalComparer ByNameOrdinal { get; } = new EventCounterItemNameOrdinalComparer();
}

internal class EventCounterItemNameOrdinalComparer : IEqualityComparer<EventCounterItem>
{
    public bool Equals(EventCounterItem? x, EventCounterItem? y)
    {
        if (x?.Name is null || y?.Name is null)
        {
            return false;
        }

        if (object.ReferenceEquals(x, y))
        {
            return true;
        }

        return string.Equals(x.Name, y.Name);
    }

    public int GetHashCode([DisallowNull] EventCounterItem obj)
    {
        return obj.Name.GetHashCode();
    }
}