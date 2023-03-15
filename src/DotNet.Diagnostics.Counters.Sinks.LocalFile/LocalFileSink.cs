using System.Threading.Channels;
using DotNet.Diagnostics.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.Sinks.LocalFile;

internal sealed class LocalFileSink : ISink<IDotNetCountersClient, ICounterPayload>, IAsyncDisposable
{
    private readonly LocalFileSinkOptions _options;
    private static FileStream? _currentStream = null;
    private readonly Channel<ICounterPayload> _workingQueue = Channel.CreateUnbounded<ICounterPayload>(new UnboundedChannelOptions()
    {
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly IPayloadWriter _payloadWriter;
    private readonly ILogger _logger;

    public LocalFileSink(
        IOptions<LocalFileSinkOptions> options,
        IPayloadWriter payloadWriter,
        ILogger<LocalFileSink> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _payloadWriter = payloadWriter ?? throw new ArgumentNullException(nameof(payloadWriter));
    }

    public bool Submit(ICounterPayload data)
    {
        bool success = _workingQueue.Writer.TryWrite(data);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Payload submitted. Result: {success}", success);
        }
        return success;
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        await foreach (ICounterPayload data in _workingQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriterDataAsync(data, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriterDataAsync(ICounterPayload payload, CancellationToken cancellationToken)
    {
        string fullFileName = GetFullFileName(payload.Timestamp);

        FileStreamOptions fileStreamOptions = new FileStreamOptions()
        {
            Mode = FileMode.Append,
            Access = FileAccess.Write,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        };

        if (_currentStream is null)
        {
            _logger.LogInformation("Open writing: {fileName}", fullFileName);
            _currentStream = File.Open(fullFileName, fileStreamOptions);
            await WriteHeaderAsync(_currentStream, cancellationToken).ConfigureAwait(false);
        }

        if (!string.Equals(_currentStream.Name, fullFileName, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _currentStream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected disposing the last output: {fileName}. Data file might be corrupted.", _currentStream.Name);
            }
            finally
            {
                _logger.LogInformation("Open new file for writing: {fileName}", fullFileName);
                _currentStream = File.Open(fullFileName, fileStreamOptions);
                await WriteHeaderAsync(_currentStream, cancellationToken).ConfigureAwait(false);
            }
        }

        await WriteDataAsync(_currentStream, payload, cancellationToken).ConfigureAwait(false);
    }

    private string GetFullFileName(DateTime timestamp)
        => Path.Combine(
            Environment.ExpandEnvironmentVariables(_options.OutputFolder),
            $"{_options.FileNamePrefix}.{timestamp.ToString("yyyyMMddHH")}.csv"
        );

    private Task WriteDataAsync(Stream writeTo, ICounterPayload data, CancellationToken cancellationToken)
        => _payloadWriter.WriteAsync(writeTo, data, cancellationToken);

    private Task WriteHeaderAsync(Stream writeTo, CancellationToken cancellationToken)
    {
        if (writeTo.Position != 0)
        {
            return Task.CompletedTask;
        }
        return _payloadWriter switch
        {
            IPayloadHeaderWriter headerWriter => headerWriter.WriteHeaderAsync(writeTo, cancellationToken),
            _ => Task.CompletedTask,
        };
    }


    public async ValueTask DisposeAsync()
    {
        if (_currentStream is not null)
        {
            await _currentStream.DisposeAsync();
            _currentStream.Dispose();
        }
        _currentStream = null;
    }

    public Task FlushAsync(CancellationToken cancellationToken) =>
        _currentStream is null ? Task.CompletedTask : _currentStream.FlushAsync(cancellationToken);
}