namespace DotNet.Diagnostics.Counters;

public interface ICounterPayload
{
    string Name { get; }

    double Value { get; }

    string CounterType { get; }

    string Provider { get; }

    string DisplayName { get; }

    string? Unit { get; }

    DateTime Timestamp { get; }

    float Interval { get; }
}
