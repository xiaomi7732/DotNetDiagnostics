using Microsoft.Extensions.Logging;

namespace DotNet.Diagnostics.Counters;

public class CSVPayloadWriter : IPayloadWriter
{
    private readonly ILogger<CSVPayloadWriter> _logger;

    public CSVPayloadWriter(ILogger<CSVPayloadWriter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task WriteAsync(Stream toStream, ICounterPayload payload, CancellationToken cancellationToken)
    {
        using (StreamWriter streamWriter = new StreamWriter(toStream, leaveOpen: true))
        {
            string line = GetCSVLine(payload);
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Writing CSV line: {line}", line);
            }
            await streamWriter.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    private string GetCSVLine(ICounterPayload payload)
    {
        return string.Join(
            ",",
            payload.Timestamp.ToString("O"),
            payload.Provider,
            payload.DisplayName,
            payload.Value,
            payload.Unit);
    }
}