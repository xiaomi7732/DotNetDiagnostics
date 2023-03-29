using Microsoft.Extensions.Hosting;

namespace DotNet.Diagnostics.Core;

public class SinkBackgroundService<T> : BackgroundService
    where T : ISink
{
    private readonly T _sink;

    public SinkBackgroundService(T sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await _sink.StartAsync(stoppingToken);
    }
}