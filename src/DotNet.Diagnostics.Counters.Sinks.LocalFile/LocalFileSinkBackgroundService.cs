using Microsoft.Extensions.Hosting;

namespace DotNet.Diagnostics.Counters.Sinks.LocalFile;

internal class LocalFileSinkBackgroundService : BackgroundService
{
    private readonly LocalFileSink _sink;

    public LocalFileSinkBackgroundService(LocalFileSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await _sink.Start(stoppingToken);
    }
}