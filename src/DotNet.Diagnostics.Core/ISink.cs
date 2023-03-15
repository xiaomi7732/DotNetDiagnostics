namespace DotNet.Diagnostics.Core;

public interface ISink<TSource, TData>
{
    
    void Submit(TData data);
}