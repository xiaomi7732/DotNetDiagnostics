using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNet.Diagnostics.Core;

public sealed class JobMatcherRunner<T> : BackgroundService
    where T : JobDetailsBase
{
    private bool _isDisposed = false;
    private readonly IEnumerable<IJobMatcher<T>> _matchers;
    private readonly ILogger _logger;
    private PeriodicTimer? _timer;

    public JobMatcherRunner(
        IEnumerable<IJobMatcher<T>> matchers,
        ILogger<JobMatcherRunner<T>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _matchers = matchers ?? Enumerable.Empty<IJobMatcher<T>>();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        if (!_matchers.Any())
        {
            _logger.LogInformation("There is no job matcher.");
            return;
        }
        else if (_matchers.Count() == 1)
        {
            _logger.LogInformation("1 job matcher in effective.");
        }
        else
        {
            _logger.LogWarning("There's more than 1 matchers in effective. Is it intended?");
        }

        _logger.LogInformation("Starting watch for jobs.");
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("1 iteration watch for jobs...");
            foreach (IJobMatcher<T> matcher in _matchers)
            {
                await matcher.MatchAndExecuteAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
            }
            _logger.LogDebug("Job match executed...");

            if (_timer is not null)
            {
                await _timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }

    public override void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

        base.Dispose();

        _timer?.Dispose();
        _timer = null;
    }
}