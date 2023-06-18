using System.Threading.Channels;
using DotNet.Diagnostics.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.Sinks.LocalFile;

public sealed class LocalFileSink : SinkBase<IDotNetCountersClient, ICounterPayload>, IAsyncDisposable
{
    private bool _isDisposed = false;
    private readonly LocalFileSinkOptions _options;
    private static FileStream? _currentStream = null;
    private readonly Channel<ICounterPayload> _workingQueue = Channel.CreateUnbounded<ICounterPayload>(new UnboundedChannelOptions()
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly IPayloadWriter _payloadWriter;
    private readonly LoggingFileNameProvider _fileNameProvider;
    private readonly ILogger _logger;

    public LocalFileSink(
        IOptions<LocalFileSinkOptions> options,
        IPayloadWriter payloadWriter,
        LoggingFileNameProvider fileNameProvider,
        ILogger<LocalFileSink> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _payloadWriter = payloadWriter ?? throw new ArgumentNullException(nameof(payloadWriter));
        _fileNameProvider = fileNameProvider ?? throw new ArgumentNullException(nameof(fileNameProvider));
    }

    private async Task StartWatchingQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PumpTheChannelAsync(cancellationToken);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _workingQueue.Writer.TryComplete();
            // Clean up the queue.
            await PumpTheChannelAsync(default).ConfigureAwait(false); // No cancellation token this time.
            throw;
        }
    }

    private async Task PumpTheChannelAsync(CancellationToken cancellationToken)
    {
        await foreach (ICounterPayload data in _workingQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteDataAsync(data, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteDataAsync(ICounterPayload payload, CancellationToken cancellationToken)
    {
        string fullFileName = GetFullFileName(payload.Timestamp);
        Directory.CreateDirectory(Path.GetDirectoryName(fullFileName)!);

        FileStreamOptions fileStreamOptions = new FileStreamOptions()
        {
            Mode = FileMode.Append,
            Access = FileAccess.Write,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        };

        if (_currentStream is null)
        {
            _logger.LogDebug("Output file is not opened yet. Open writing: {fileName}", fullFileName);
            _currentStream = await TryOpenFileStreamAsync(fullFileName, fileStreamOptions, cancellationToken).ConfigureAwait(false);
            if (_currentStream is null)
            {
                return;
            }
            _logger.LogInformation("File opened successfully for writing: {fileName}", fullFileName);
            await WriteHeaderAsync(_currentStream, default).ConfigureAwait(false);
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
                await WriteHeaderAsync(_currentStream, default).ConfigureAwait(false);
            }
        }

        await WriteDataAsync(_currentStream, payload, default).ConfigureAwait(false);
    }

    private async Task<FileStream?> TryOpenFileStreamAsync(string fullPath, FileStreamOptions options, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        try
        {
            FileStream fileStream = File.Open(fullPath, options);
            return fileStream;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error opening target file: {fullPath}", fullPath);
        }
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        return null;
    }

    private string GetFullFileName(DateTime timestamp)
        => _fileNameProvider.GetFullFileName(timestamp, ".csv");

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
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;


        _logger.LogInformation("Flush the buffer and send data to storage...");

        _workingQueue.Writer.TryComplete();
        _logger.LogDebug("Writer completed.");
        await _workingQueue.Reader.Completion.ConfigureAwait(false);
        _logger.LogDebug("Reader completed.");

        if (_currentStream is not null)
        {
            await _currentStream.DisposeAsync();
            _currentStream.Dispose();
        }
        _currentStream = null;

        Dispose(true);
    }

    protected override async Task<bool> OnStartingAsync(CancellationToken cancellationToken)
    {
        await StartWatchingQueueAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    protected override async Task<bool> OnStoppingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Sink");

        _workingQueue.Writer.Complete();
        await _workingQueue.Reader.Completion.ConfigureAwait(false);

        if (_currentStream is not null)
        {
            await _currentStream.DisposeAsync().ConfigureAwait(false);
            _currentStream.Dispose();
            _currentStream = null;
        }

        return true;
    }

    protected override bool OnSubmit(ICounterPayload data)
    {
        bool success = _workingQueue.Writer.TryWrite(data);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Payload submitted. Result: {success}", success);
        }
        return success;
    }
}