using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters;

public sealed class DotNetCountersClient : IDotNetCountersClient, IAsyncDisposable
{
    private readonly DiagnosticsClient _diagnosticsClient;
    private EventPipeSession? _eventPipeSession;
    private EventPipeEventSource? _eventPipeEventSource;
    private readonly DotNetCountersOptions _options;
    private readonly ILogger _logger;

    public DotNetCountersClient(
        IOptions<DotNetCountersOptions> options,
        ILogger<DotNetCountersClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

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
        try
        {
            if (_eventPipeSession is not null)
            {
                await _eventPipeSession.StopAsync(cancellationToken);
                return true;
            }
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
            await DisposeCoreAsync().ConfigureAwait(false);
        }
        return false;
    }

    public Task<bool> EnableAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }

        _ = Task.Run(() =>
        {
            try
            {
                _eventPipeSession = _diagnosticsClient.StartEventPipeSession(GetEventPipeProviders(), requestRundown: false, circularBufferMB: 10);
                _diagnosticsClient.ResumeRuntime();
                _eventPipeEventSource = new EventPipeEventSource(_eventPipeSession.EventStream);
                _eventPipeEventSource.Dynamic.All += DynamicAllMonitor;
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

        return Task.FromResult(true);
    }

    private void DynamicAllMonitor(TraceEvent traceEventObject)
    {
        _logger.LogDebug("Get trace event object: {name}", traceEventObject.EventName);

        if (traceEventObject.EventName.Equals("EventCounters", StringComparison.Ordinal))
        {
            IDictionary<string, object> payloadVal = (IDictionary<string, object>)(traceEventObject.PayloadValue(0));
            IDictionary<string, object> payloadFields = (IDictionary<string, object>)(payloadVal["Payload"]);
            dynamic y = payloadVal["Payload"];

            // If it's not a counter we asked for, ignore it.
            // if (!filter.Filter(traceEventObject.ProviderName, payloadFields["Name"].ToString())) return;

            // ICounterPayload payload = payloadFields["CounterType"].Equals("Sum") ? (ICounterPayload)new IncrementingCounterPayload(payloadFields, _interval) : (ICounterPayload)new CounterPayload(payloadFields);
            // _renderer.CounterPayloadReceived(traceEventObject.ProviderName, payload, pauseCmdSet);

            foreach (KeyValuePair<string, object> item in payloadFields)
            {
                _logger.LogInformation("Key: {key}", item.Key);
                _logger.LogInformation("Value: {value}", item.Value);
            }
        }
    }

    private IEnumerable<EventPipeProvider> GetEventPipeProviders()
    {
        string intervalInSecondsString = _options.IntervalSec.ToString(CultureInfo.InvariantCulture);

        yield return new EventPipeProvider("System.Runtime", EventLevel.Verbose, 0xffffffff, new Dictionary<string, string>
        {
            ["EventCounterIntervalSec"] = intervalInSecondsString,
        });
        yield return new EventPipeProvider("Microsoft.AspNetCore.Hosting", EventLevel.Informational, 0x0, new Dictionary<string, string>
        {
            ["EventCounterIntervalSec"] = intervalInSecondsString,
        });
    }

    public async ValueTask DisposeAsync()
    {
        await DisableAsync(cancellationToken: default);
        await DisposeCoreAsync();
    }

    private ValueTask DisposeCoreAsync()
    {
        _eventPipeSession?.Dispose();
        _eventPipeSession = null;

        _eventPipeEventSource?.Dispose();
        _eventPipeEventSource = null;

        return ValueTask.CompletedTask;
    }
}