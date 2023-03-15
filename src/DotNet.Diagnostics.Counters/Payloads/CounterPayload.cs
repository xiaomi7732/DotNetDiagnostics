using Microsoft.Diagnostics.Tracing;

namespace DotNet.Diagnostics.Counters;
internal class CounterPayload : ICounterPayload
{
    public string Name { get; init; } = default!;

    public string DisplayName { get; init; } = default!;

    public string? Unit { get; init; } = default!;

    public double Value { get; init; }

    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public float Interval { get; init; }

    public string CounterType { get; init; } = default!;

    public string Provider { get; init; } = default!;

    public static bool TryParse(IDictionary<string, object> payloadFields, TraceEvent eventObject, out CounterPayload? result)
    {
        if (payloadFields is null)
        {
            throw new ArgumentNullException(nameof(payloadFields));
        }

        if (eventObject is null)
        {
            throw new ArgumentNullException(nameof(eventObject));
        }

        if (string.Equals((string?)payloadFields["CounterType"], "Sum", StringComparison.Ordinal))
        {
            // For IncrementingCounterPayload
            result = null;
            return false;
        }

        string name = payloadFields["Name"].ToString()!;
        double value = (double)payloadFields["Mean"];
        string? displayName = payloadFields["DisplayName"].ToString();
        string? displayUnits = payloadFields["DisplayUnits"].ToString();
        if (!float.TryParse(payloadFields["IntervalSec"].ToString(), out float intervalSec))
        {
            intervalSec = 1;
        }

        // In case these properties are not provided, set them to appropriate values.
        displayName = string.IsNullOrEmpty(displayName) ? name : displayName;

        result = new CounterPayload()
        {
            Timestamp = eventObject.TimeStamp,
            Provider = eventObject.ProviderName,
            Name = name,
            DisplayName = displayName,
            Value = value,
            Unit = displayUnits,
            Interval = intervalSec,
        };
        return true;
    }
}
