namespace DotNet.Diagnostics.Core;

public interface ISink
{
    Task FlushAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts listening for data.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);
}

public interface ISink<TSource, TData> : ISink
{
    bool Submit(TData data);
}