namespace DotNet.Diagnostics.Core;

public interface IJobDispatcher<T>
    where T: JobDetailsBase
{
    Task<bool> DispatchAsync(T jobDetails, CancellationToken cancellationToken);
}