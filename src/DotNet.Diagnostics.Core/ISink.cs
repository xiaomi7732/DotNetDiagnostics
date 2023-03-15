namespace DotNet.Diagnostics.Core;

public interface ISink<TSource, TData>
{
    bool Submit(TData data);

    Task FlushAsync(CancellationToken cancellationToken);
}