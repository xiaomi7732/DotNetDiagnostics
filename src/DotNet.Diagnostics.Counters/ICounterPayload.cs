namespace DotNet.Diagnostics.Counters;

public interface ICounterPayload
{
    string Name { get; }

    double Value { get; }

    /// <summary>
    /// There are 2 types at this point, Sum and Mean. When it is Sum, the value is an incremental between the previous value and the current one. The actual value is the sum of the current to all previous ones.
    /// When it is mean, it is the current value.
    /// </summary>
    string CounterType { get; }

    string Provider { get; }

    string DisplayName { get; }

    string? Unit { get; }

    DateTime Timestamp { get; }

    float Interval { get; }
}
