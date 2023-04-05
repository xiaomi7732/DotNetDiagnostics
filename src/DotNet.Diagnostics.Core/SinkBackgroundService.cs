using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNet.Diagnostics.Core;

public class SinkBackgroundService<T> : BackgroundService
    where T : ISink
{
    private readonly T _sink;
    private readonly ILogger _logger;

    public SinkBackgroundService(T sink, ILogger<SinkBackgroundService<T>> logger)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Start sink background service");
            await Task.Yield();
            await _sink.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == stoppingToken)
        {
            _logger.LogInformation("Sink terminated by the user.");
            throw;
        }
    }
}