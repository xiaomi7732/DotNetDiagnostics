using Microsoft.Extensions.Logging;

namespace DotNet.Diagnostics.Counters;

public class CSVPayloadWriter : IPayloadWriter, IPayloadHeaderWriter
{
    private readonly ILogger<CSVPayloadWriter> _logger;

    public CSVPayloadWriter(ILogger<CSVPayloadWriter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task WriteAsync(Stream toStream, ICounterPayload payload, CancellationToken cancellationToken)
    {
        await WriteLine(toStream, payload, GetCSVLine, cancellationToken);
    }

    public async Task WriteHeaderAsync(Stream writeTo, CancellationToken cancellationToken)
    {
        await WriteLine(writeTo, null, GetCSVHeader, cancellationToken);
    }

    private string? GetCSVHeader(ICounterPayload? payload)
    {
        return string.Join(",", "Timestamp", "Provider Name", "Display Name", "Value", "Unit");
    }

    private string? GetCSVLine(ICounterPayload? payload)
    {
        if (payload is null)
        {
            return null;
        }

        return string.Join(
            ",",
            payload.Timestamp.ToString("O"),
            payload.Provider,
            payload.DisplayName,
            payload.Value,
            payload.Unit);
    }

    private async Task WriteLine(Stream toStream, ICounterPayload? payload, Func<ICounterPayload?, string?> getLine, CancellationToken cancellationToken)
    {
        using (StreamWriter streamWriter = new StreamWriter(toStream, leaveOpen: true))
        {
            string? line = getLine(payload);

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