using Microsoft.Extensions.Logging;

namespace DotNet.Diagnostics.Counters;

public class DummyDotNetCountersClient : IDotNetCountersClient
{
    private readonly ILogger _logger;

    public DummyDotNetCountersClient(ILogger<DummyDotNetCountersClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> DisableAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DotNet counters disabled.");
        return Task.FromResult(false);
    }

    public Task<bool> EnableAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DotNet counters enabled.");
        return Task.FromResult(true);
    }
}