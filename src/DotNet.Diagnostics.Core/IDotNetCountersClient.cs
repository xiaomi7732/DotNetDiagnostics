namespace DotNet.Diagnostics.Core;
public interface IDotNetCountersClient
{
    Task<bool> EnableAsync(CancellationToken cancellationToken);
    Task<bool> DisableAsync(CancellationToken cancellationToken);
}