using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters;

public class CSVPayloadWriter : IPayloadWriter, IPayloadHeaderWriter
{
    private readonly DotNetCountersOptions _options;
    private readonly ILogger _logger;

    public CSVPayloadWriter(
        IOptions<DotNetCountersOptions> options,
        ILogger<CSVPayloadWriter> logger
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task WriteAsync(Stream toStream, ICounterPayload payload, CancellationToken cancellationToken)
    {
        await WriteLine(toStream, payload, _options.IntervalSec, GetCSVLine, cancellationToken);
    }

    public async Task WriteHeaderAsync(Stream writeTo, CancellationToken cancellationToken)
    {
        await WriteLine(writeTo, payload: null, _options.IntervalSec, GetCSVHeader, cancellationToken);
    }

    private string? GetCSVHeader(ICounterPayload? payload, int _)
    {
        return string.Join(",", "Timestamp", "Provider Name", "Display Name", "Value", "Unit", "CounterType");
    }

    private string? GetCSVLine(ICounterPayload? payload, int intervalInSeconds)
    {
        if (payload is null)
        {
            return null;
        }

        string? displayUnit = payload.Unit;

        if (string.IsNullOrEmpty(displayUnit))
        {
            displayUnit = "Count";
        }

        if (!string.IsNullOrEmpty(displayUnit) && payload.CounterType == CounterType.Sum.ToString())
        {
            displayUnit += $" / {intervalInSeconds} sec";
        }

        return string.Join(
            ",",
            payload.Timestamp.ToString("O"),
            payload.Provider,
            payload.DisplayName,
            payload.Value,
            displayUnit,
            payload.CounterType);
    }

    private async Task WriteLine(Stream toStream, ICounterPayload? payload, int intervalInSeconds, Func<ICounterPayload?, int, string?> getLine, CancellationToken cancellationToken)
    {
        using (StreamWriter streamWriter = new StreamWriter(toStream, leaveOpen: true))
        {
            string? line = getLine(payload, intervalInSeconds);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Writing CSV line: {line}", line);
            }

            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            await streamWriter.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}