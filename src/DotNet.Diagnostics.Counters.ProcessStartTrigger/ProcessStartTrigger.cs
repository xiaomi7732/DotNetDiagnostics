using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.Triggers;

public class ProcessStartTrigger : BackgroundService
{
    private readonly ProcessStartTriggerOptions _options;
    private readonly IDotNetCountersClient _dotnetCounters;
    private readonly ILogger<ProcessStartTrigger> _logger;

    public ProcessStartTrigger(
        IOptions<ProcessStartTriggerOptions> options,
        IDotNetCountersClient dotnetCountersClient,
        ILogger<ProcessStartTrigger> logger
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _dotnetCounters = dotnetCountersClient ?? throw new ArgumentNullException(nameof(dotnetCountersClient));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(!_options.IsEnabled)
        {
            _logger.LogInformation("Dotnet-counter process start trigger is disabled.");
            return;
        }

        _logger.LogInformation("Start dotent-counter with the process, delay for {initialDelay}", _options.InitialDelay);
        await Task.Delay(_options.InitialDelay, stoppingToken).ConfigureAwait(false);
        await _dotnetCounters.EnableAsync(stoppingToken).ConfigureAwait(false);
        _logger.LogDebug("Dotnet-counters started with the process.", _options.InitialDelay);
    }
}