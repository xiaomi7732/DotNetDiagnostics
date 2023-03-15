using Microsoft.Diagnostics.Tracing;

namespace DotNet.Diagnostics.Counters;

internal class IncrementingCounterPayload : ICounterPayload
{
    public string Name { get; init; } = default!;

    public double Value { get; init; }

    public string CounterType { get; init; } = "Sum";

    public string Provider { get; init; } = default!;

    public string DisplayName { get; init; } = default!;

    public string? Unit { get; init; }

    public DateTime Timestamp { get; init; }

    public float Interval { get; init; }

    public static bool TryParse(IDictionary<string, object> payloadFields, TraceEvent eventObject, out IncrementingCounterPayload? result)
    {
        if (payloadFields is null)
        {
            throw new ArgumentNullException(nameof(payloadFields));
        }

        if (eventObject is null)
        {
            throw new ArgumentNullException(nameof(eventObject));
        }

        if (!string.Equals((string?)payloadFields["CounterType"], "Sum", StringComparison.Ordinal))
        {
            // For IncrementingCounterPayload
            result = null;
            return false;
        }

        string name = payloadFields["Name"].ToString()!;
        double value = (double)payloadFields["Increment"];
        string? displayName = payloadFields["DisplayName"].ToString();
        string? displayUnits = payloadFields["DisplayUnits"].ToString();

        if (!float.TryParse(payloadFields["IntervalSec"].ToString(), out float interval))
        {
            interval = 1;
        }
        // In case these properties are not provided, set them to appropriate values.
        displayName = string.IsNullOrEmpty(displayName) ? name : displayName;

        result = new IncrementingCounterPayload()
        {
            Timestamp = eventObject.TimeStamp,
            Provider = eventObject.ProviderName,
            Name = name,
            DisplayName = displayName,
            Value = value,
            Unit = displayUnits,
            Interval = interval,
        };
        return true;
    }
}