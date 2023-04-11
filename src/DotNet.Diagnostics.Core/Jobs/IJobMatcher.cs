namespace DotNet.Diagnostics.Core;

public interface IJobMatcher<T>
    where T: JobDetailsBase
{
    Task<T?> MatchAndExecuteAsync(CancellationToken cancellationToken);
}