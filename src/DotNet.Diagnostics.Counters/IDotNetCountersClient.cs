namespace DotNet.Diagnostics.Counters;
public interface IDotNetCountersClient
{
    /// <summary>
    /// Enables dotnet-counters for a process.
    /// </summary>
    /// <param name="processId">The process id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True when the a new session is enabled. False if no new session was created. Usually, that is due to there's another active dotnet-counters session.</returns>
    Task<bool> EnableAsync(int processId, CancellationToken cancellationToken);

    /// <summary>
    /// Disable the current dotnet-counter session.
    /// </summary>
    Task<bool> DisableAsync(CancellationToken cancellationToken);
}