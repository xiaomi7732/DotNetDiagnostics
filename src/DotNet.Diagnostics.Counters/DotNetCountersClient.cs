using System.Diagnostics;
using DotNet.Diagnostics.Core;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters;

public sealed class DotNetCountersClient : IDotNetCountersClient, IAsyncDisposable
{
    private bool _isEnabled = false;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private readonly DiagnosticsClient _diagnosticsClient;
    private EventPipeSession? _eventPipeSession;
    private EventPipeEventSource? _eventPipeEventSource;
    private Stream? _outputStream;
    private readonly DotNetCountersOptions _options;
    private readonly IEnumerable<ISink<IDotNetCountersClient, ICounterPayload>> _outputSinks;
    private readonly DotNetCountEventCounterManager _eventCounterManager;
    private readonly ILogger _logger;

    public DotNetCountersClient(
        IEnumerable<ISink<IDotNetCountersClient, ICounterPayload>>? outputSinks,
        DotNetCountEventCounterManager eventCounterManager,
        IOptions<DotNetCountersOptions> options,
        ILogger<DotNetCountersClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _eventCounterManager = eventCounterManager ?? throw new ArgumentNullException(nameof(eventCounterManager));

        _outputSinks = outputSinks ?? Enumerable.Empty<ISink<IDotNetCountersClient, ICounterPayload>>();
        if (!_outputSinks.Any())
        {
            _logger.LogWarning("There's no output sink.");
        }
        else
        {
            _logger.LogInformation("There are {count} output sinks configured.", _outputSinks.Count());
        }

        int processId = int.MinValue;
        using (Process p = Process.GetCurrentProcess())
        {
            processId = p.Id;
        }

        if (processId < 0)
        {
            throw new InvalidOperationException($"Can't start dotnet-counter for process {processId}");
        }

        _diagnosticsClient = new DiagnosticsClient(processId);
    }

    public async Task<bool> DisableAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Disabling dotnet-counters...");
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isEnabled)
            {
                _logger.LogInformation("No active dotnet-counter to disable.");
                return false;
            }

            if (_eventPipeSession is not null)
            {
                await _eventPipeSession.StopAsync(cancellationToken);
                foreach (var sink in _outputSinks)
                {
                    try
                    {
                        await sink.FlushAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error flushing sink of {sinkType}", sink.GetType());
                    }
                }
                _logger.LogInformation("dotnet-counters disabled...");

            }
            _isEnabled = false;
            return true;
        }
        catch (EndOfStreamException ex)
        {
            // If the app we're monitoring exits abruptly, this may throw in which case we just swallow the exception and exit gracefully.
            _logger.LogDebug(ex, "Gracefully exit the application.");
        }
        // We may time out if the process ended before we sent StopTracing command. We can just exit in that case.
        catch (TimeoutException ex)
        {
            _logger.LogDebug(ex, "Exiting dotnet counters timed-out.");
        }
        // On Unix platforms, we may actually get a PNSE since the pipe is gone with the process, and Runtime Client Library
        // does not know how to distinguish a situation where there is no pipe to begin with, or where the process has exited
        // before dotnet-counters and got rid of a pipe that once existed.
        // Since we are catching this in StopMonitor() we know that the pipe once existed (otherwise the exception would've 
        // been thrown in StartMonitor directly)
        catch (PlatformNotSupportedException)
        {
        }
        // On non-abrupt exits, the socket may be already closed by the runtime and we won't be able to send a stop request through it. 
        catch (ServerNotAvailableException)
        {
        }
        finally
        {
            _lock.Release();
            await DisposeCoreAsync().ConfigureAwait(false);
        }
        return false;
    }

    public async Task<bool> EnableAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Enabling dotnet-counters...");
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isEnabled)
            {
                _logger.LogInformation("There is already a running dotnet-counter.");
                return false;
            }
            _ = Task.Run(() =>
            {
                try
                {
                    _outputStream = new MemoryStream();
                    _eventPipeSession = _diagnosticsClient.StartEventPipeSession(GetEventPipeProviders(), requestRundown: false, circularBufferMB: 10);
                    _diagnosticsClient.ResumeRuntime();
                    _eventPipeEventSource = new EventPipeEventSource(_eventPipeSession.EventStream);
                    _eventPipeEventSource.Dynamic.All += DynamicAllMonitor;
                    _logger.LogInformation("dotnet-counters enabled.");
                    _eventPipeEventSource.Process();
                }
                catch (DiagnosticsClientException ex)
                {
                    _logger.LogError(ex, "Failed to start the counter session: {message}", ex.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unknown error: {message}", ex.ToString());
                }
            }, cancellationToken);
            _isEnabled = true;
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void DynamicAllMonitor(TraceEvent traceEventObject)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Get trace event object: {name}", traceEventObject.EventName);
        }

        if (traceEventObject.EventName.Equals("EventCounters", StringComparison.Ordinal))
        {
            IDictionary<string, object> payloadVal = (IDictionary<string, object>)(traceEventObject.PayloadValue(0));
            IDictionary<string, object> payloadFields = (IDictionary<string, object>)(payloadVal["Payload"]);

            string? metricsName = payloadFields["Name"].ToString();
            if (string.IsNullOrEmpty(metricsName) || !IsEnabled(traceEventObject.ProviderName, metricsName))
            {
                // The metrics doesn't have a name or it is not there in the filter.
                return;
            }

            ICounterPayload payload;
            if (CounterPayload.TryParse(payloadFields, traceEventObject, out CounterPayload? counterPayload))
            {
                payload = counterPayload!;
            }
            else if (IncrementingCounterPayload.TryParse(payloadFields, traceEventObject, out IncrementingCounterPayload? incrementingCounterPayload))
            {
                payload = incrementingCounterPayload!;
            }
            else
            {
                _logger.LogError("Payload can't be parsed: {payload}", string.Join(",", payloadFields.Select(item => string.Join("=", item.Key, item.Value))));
                return;
            }

            // Write to sinks
            foreach (ISink<IDotNetCountersClient, ICounterPayload> sink in _outputSinks)
            {
                // TODO: Extend this to support json writer.
                // Writer header
                try
                {
                    sink.Submit(payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error writing payload to sink of {sinkType}", sink.GetType());
                }
            }
        }
    }

    private IEnumerable<EventPipeProvider> GetEventPipeProviders()
        => _eventCounterManager.EventCounters.Select(item => item.ToEventPipeProvider()).OfType<EventPipeProvider>();

    public async ValueTask DisposeAsync()
    {
        await DisableAsync(cancellationToken: default);
        await DisposeCoreAsync();
        _lock.Dispose();
    }

    private bool IsEnabled(string providerName, string metricsName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            _logger.LogError("How can provider name be null for event pipe events?");
            return false;
        }

        if (string.IsNullOrEmpty(metricsName))
        {
            _logger.LogError("How can metrics name be null for event counter events?");
            return false;
        }

        return _eventCounterManager.IsEnabled(providerName, metricsName);
    }

    private async ValueTask DisposeCoreAsync()
    {
        _eventPipeSession?.Dispose();
        _eventPipeSession = null;

        _eventPipeEventSource?.Dispose();
        _eventPipeEventSource = null;

        if (_outputStream is not null)
        {
            await _outputStream.DisposeAsync().ConfigureAwait(false);
        }
        _outputStream = null;

    }
}