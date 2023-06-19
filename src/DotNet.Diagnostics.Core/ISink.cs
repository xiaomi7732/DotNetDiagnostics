namespace DotNet.Diagnostics.Core;

public interface ISink
{
    /// <summary>
    /// Starts listening for data.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops listening for data.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Initialize the sink. This is expected to run only once at the beginning of the lifecycle of the sink.
    /// This allows asynchronous initialization.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken);
}

public interface ISink<TSource, TData> : ISink
{
    /// <summary>
    /// Submits a payload to the sink.
    /// </summary>
    /// <param name="data"></param>
    /// <returns>True when the data is submitted successfully. Otherwise, false.</returns>
    bool Submit(TData data);
}