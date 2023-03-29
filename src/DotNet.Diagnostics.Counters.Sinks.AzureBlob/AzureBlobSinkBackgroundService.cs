using Microsoft.Extensions.Hosting;

namespace DotNet.Diagnostics.Counters.Sinks.AzureBlob;

internal class AzureBlobSinkBackgroundService : BackgroundService
{
    private readonly AzureBlobSink _sink;

    public AzureBlobSinkBackgroundService(AzureBlobSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await _sink.StartAsync(stoppingToken);
    }
}