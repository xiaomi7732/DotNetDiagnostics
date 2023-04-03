using DotNet.Diagnostics.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.Sinks.AzureBlob;

public class ApplicationInsightsSink : ISink<IDotNetCountersClient, ICounterPayload>
{
    private readonly TelemetryClient? _telemetryClient;
    private readonly TelemetryConfiguration? _telemetryConfiguration;
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

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (_telemetryConfiguration is null)
        {
            return;
        }

        if (_telemetryClient is not null)
        {
            await _telemetryClient.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        _telemetryConfiguration.TelemetryChannel?.Flush();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();

        if (_telemetryConfiguration is null || _telemetryClient is null)
        {
            _logger.LogInformation("Application Insights is not configured.");
            return;
        }

        _logger.LogInformation("Start sending data to application insights by instrumentation key: {iKey}", _telemetryConfiguration.InstrumentationKey);
    }

    public bool Submit(ICounterPayload data)
    {
        if (_telemetryClient is null)
        {
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