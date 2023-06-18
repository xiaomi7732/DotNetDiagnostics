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