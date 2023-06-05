namespace DotNet.Diagnostics.Counters;

public sealed class DotnetCountersProcessIdProvider
{
    private object _lock = new object();

    private DotnetCountersProcessIdProvider()
    {
    }

    public static DotnetCountersProcessIdProvider Instance { get; } = new DotnetCountersProcessIdProvider();

    /// <summary>
    /// Raises when process id changed.
    /// </summary>
    public event EventHandler<int?>? ProcessChanged;

    /// <summary>
    /// Get the target process id for .net counters.
    /// </summary>
    public int? CurrentProcessId { get; private set; }

    /// <summary>
    /// Sets the current process id. This method is thread safe.
    /// The new value is echoed back.
    /// </summary>
    internal int? SetCurrentProcessId(int? newValue)
    {
        lock (_lock)
        {
            if(CurrentProcessId != newValue)
            {
                CurrentProcessId = newValue;
                RaiseProcessIdChanged(newValue);
            }

            return CurrentProcessId;
        }
    }

    internal void RemoveCurrentProcessId() => SetCurrentProcessId(null);

    private void RaiseProcessIdChanged(int? newValue)
    {
        ProcessChanged?.Invoke(this, newValue);
    }
}