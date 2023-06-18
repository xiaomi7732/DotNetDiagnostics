using DotNet.Diagnostics.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.Sinks.ApplicationInsights;

public class ApplicationInsightsSink : SinkBase<IDotNetCountersClient, ICounterPayload>
{
    private TelemetryClient? _telemetryClient;
    private TelemetryConfiguration? _telemetryConfiguration;
    private readonly ILogger _logger;

    public ApplicationInsightsSink(
        IOptions<TelemetryConfiguration> telemetryConfiguration,
        ILogger<ApplicationInsightsSink> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetryConfiguration = telemetryConfiguration?.Value;

        if (_telemetryConfiguration is not null)
        {
            _telemetryClient = new TelemetryClient(_telemetryConfiguration);
        }
        else
        {
            _logger.LogInformation("Application Insights is not enabled. It is required before this sink works. See https://learn.microsoft.com/en-us/azure/azure-monitor/app/asp-net-core?tabs=netcorenew%2Cnetcore6 for details.");
        }
    }


    protected override async Task<bool> OnStartingAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();

        if (_telemetryConfiguration is null || _telemetryClient is null)
        {
            _logger.LogInformation("Application Insights is not configured.");
            return false;
        }

        string instrumentationKey = _telemetryConfiguration.InstrumentationKey;
        if (string.IsNullOrEmpty(instrumentationKey))
        {
            _logger.LogWarning("Instrumentation key is required. Have you configured the application insights instrumentation key or connection string correctly?");
            _telemetryConfiguration = null;
            _telemetryClient = null;

            return false;
        }

        _logger.LogInformation("Start sending data to application insights by instrumentation key: {iKey}", _telemetryConfiguration.InstrumentationKey);
        return true;
    }

    protected override async Task<bool> OnStoppingAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (_telemetryConfiguration is null || _telemetryClient is null)
        {
            // without telemetry configuration, or telemetry client, there's nothing to enable or disable.
            return false;
        }

        await _telemetryClient.FlushAsync(cancellationToken).ConfigureAwait(false);
        _telemetryConfiguration.TelemetryChannel?.Flush();
        return true;
    }

    protected override bool OnSubmit(ICounterPayload data)
    {
        if (_telemetryClient is null)
        {
            _logger.LogError("Telemetry client is expected to exist before the sink could be turned on.");
            return false;
        }

        _telemetryClient.TrackEvent(CreateTelemetry(data));
        return true;
    }

    private EventTelemetry CreateTelemetry(ICounterPayload data)
    {
        EventTelemetry eventTelemetry = new EventTelemetry("OpenDotnetDiagnosticsCounter")
        {
            Timestamp = data.Timestamp,
        };

        eventTelemetry.Properties.TryAdd("Name", data.Name);
        eventTelemetry.Properties.TryAdd("DisplayName", data.DisplayName);
        eventTelemetry.Properties.TryAdd("CounterType", data.CounterType);
        eventTelemetry.Properties.TryAdd("Provider", data.Provider);
        if (!string.IsNullOrEmpty(data.Unit))
        {
            eventTelemetry.Properties.TryAdd("Unit", data.Unit);
        }
        eventTelemetry.Metrics.TryAdd("Value", data.Value);

        return eventTelemetry;
    }
}