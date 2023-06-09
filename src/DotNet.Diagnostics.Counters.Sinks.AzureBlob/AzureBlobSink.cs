using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Channels;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using DotNet.Diagnostics.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.Sinks.AzureBlob;

public sealed class AzureBlobSink : ISink<IDotNetCountersClient, ICounterPayload>, IAsyncDisposable
{
    private bool _isDisposed = false;
    private readonly ConcurrentDictionary<string, MemoryStream> _cache = new ConcurrentDictionary<string, MemoryStream>(StringComparer.Ordinal);

    // Assuming there won't be to much pressure to hold to many items.
    private const int _maxCacheSize = 100 * 1024;

    // private string? _currentStreamKey = null;

    private Channel<ICounterPayload>? _workingQueue = null;

    // This makes sure the flushing doesn't happen when the writer's writing the stream.
    private SemaphoreSlim? _semaphoreSlim = new SemaphoreSlim(1, 1);

    private readonly AzureBlobSinkOptions _options;
    private readonly IPayloadWriter _payloadWriter;
    private readonly WebAppContext _webAppContext;
    private readonly AzureBlobClientBuilder _blobClientBuilder;
    private readonly ILogger _logger;

    private readonly Lazy<BlobContainerClient> _blobContainerClient;

    public AzureBlobSink(
        IPayloadWriter payloadWriter,
        WebAppContext webAppContext,
        AzureBlobClientBuilder blobClientBuilder,
        IOptions<AzureBlobSinkOptions> options,
        ILogger<AzureBlobSink> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _payloadWriter = payloadWriter ?? throw new ArgumentNullException(nameof(payloadWriter));
        _webAppContext = webAppContext ?? throw new ArgumentNullException(nameof(webAppContext));
        _blobClientBuilder = blobClientBuilder ?? throw new ArgumentNullException(nameof(blobClientBuilder));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _blobContainerClient = new Lazy<BlobContainerClient>(() =>
        {
            BlobServiceClient blobServiceClient = _blobClientBuilder.AddAzureBlobOptions(_options).Build();
            return blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_isDisposed || _semaphoreSlim is null)
        {
            return;
        }

        _logger.LogDebug("Acquiring semaphore for flushing.");
        await _semaphoreSlim!.WaitAsync().ConfigureAwait(false);
        _logger.LogDebug("Acquired semaphore for flushing.");
        try
        {
            await AppendBlobAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Ensure blob container exists: {containerName}", _options.ContainerName);
            await _blobContainerClient.Value.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Blob container exists.");

            _workingQueue = Channel.CreateUnbounded<ICounterPayload>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false,
            });
            await StartReadingQueueAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogError(ex, "Failed starting the sink. Have you configured the client id for the managed identity?");
        }
    }

    /// <summary>
    /// Start watching the queue.
    /// Notes, the cancellation token is not provided on purpose. Once start, always reading to the end of the channel.
    /// </summary>
    /// <returns></returns>
    private async Task StartReadingQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PumpTheChannelAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _workingQueue?.Writer.TryComplete();
            // No cancellation token this time.
            await PumpTheChannelAsync(default).ConfigureAwait(false);
            throw;
        }
    }

    private async Task PumpTheChannelAsync(CancellationToken cancellationToken)
    {
        if (_workingQueue is null)
        {
            return;
        }

        await foreach (ICounterPayload data in _workingQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteDataAsync(data, default).ConfigureAwait(false);
        }
    }

    private async Task WriteDataAsync(ICounterPayload data, CancellationToken cancellationToken)
    {
        bool isTraceEnabled = _logger.IsEnabled(LogLevel.Trace);
        if (isTraceEnabled)
        {
            _logger.LogTrace("Acquiring semaphore writing data...");
        }
        await _semaphoreSlim!.WaitAsync(cancellationToken);
        if (isTraceEnabled)
        {
            _logger.LogTrace("Acquired semaphore writing data...");
        }

        try
        {
            string cacheKey = GetBlobName(data.Timestamp);
            MemoryStream stream = _cache.GetOrAdd(cacheKey, key => new MemoryStream());

            if (stream.Length == 0)
            {
                await WriteHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            await WriteDataAsync(stream, data, cancellationToken).ConfigureAwait(false);

            // Persistent every once a while (100K in size)
            if (GetTotalCacheSize() > _maxCacheSize)
            {
                _logger.LogInformation("Persistent data to Azure Storage.");
                await AppendBlobAsync();
            }
        }
        catch (OperationCanceledException cancel) when (cancel.CancellationToken == cancellationToken)
        {
            _logger.LogDebug("Writing data operation canceled by the user.");
            throw;
        }
        finally
        {
            _logger.LogTrace("Release semaphore.");
            _semaphoreSlim.Release();
        }
    }

    private long GetTotalCacheSize()
    {
        return _cache.Values.Sum(s => s.Length);
    }

    private async Task AppendBlobAsync()
    {
        string[] keys = _cache.Keys.ToArray();
        foreach (string key in keys)
        {
            await AppendBlobAsync(key).ConfigureAwait(false);
        }
    }

    private async Task AppendBlobAsync(string cacheKey)
    {
        MemoryStream block = _cache[cacheKey];

        int retry = 3;

        while (retry-- >= 0)
        {
            try
            {
                AppendBlobClient blobClient = _blobContainerClient.Value.GetAppendBlobClient(cacheKey);
                block.Seek(0, SeekOrigin.Begin);
                _logger.LogDebug("Appending {size} bytes of data to blob: {blobName}", block.Length, cacheKey);
                await blobClient.CreateIfNotExistsAsync().ConfigureAwait(false);
                await blobClient.AppendBlockAsync(block).ConfigureAwait(false);
                await block.FlushAsync().ConfigureAwait(false);
                await block.DisposeAsync().ConfigureAwait(false);
                _cache.TryRemove(cacheKey, out _);
                return;
            }
            catch (Exception ex)
            {
                if (retry == 0)
                {
                    await block.DisposeAsync().ConfigureAwait(false);
                    _cache.TryRemove(cacheKey, out _);
                    throw;
                }

                _logger.LogError(ex, "Failed persistent cache to Azure storage. Will retry.");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }

    private Task WriteDataAsync(Stream stream, ICounterPayload data, CancellationToken cancellationToken)
        => _payloadWriter.WriteAsync(stream, data, cancellationToken);

    private Task WriteHeaderAsync(Stream writeTo, CancellationToken cancellationToken)
    {
        return _payloadWriter switch
        {
            IPayloadHeaderWriter headerWriter => headerWriter.WriteHeaderAsync(writeTo, cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    private string GetBlobName(DateTime timestamp)
    {
        const string fileExtension = ".csv";

        string fileName = _options.FileNamePrefix;

        string machineId = string.IsNullOrEmpty(_webAppContext.SiteName) ? Environment.MachineName : _webAppContext.SiteName;
        if (!string.IsNullOrEmpty(_webAppContext.SiteInstanceId))
        {
            machineId += '_' + _webAppContext.SiteInstanceId;
        }

        string prefix = string.Join("_", (object)_options.FileNamePrefix, machineId, timestamp.ToUniversalTime().ToString("yyyyMMddHH", CultureInfo.InvariantCulture)).Trim('_');

        string blobName = prefix + fileExtension;
        return blobName;
    }

    public bool Submit(ICounterPayload data)
    {
        if (_workingQueue is null)
        {
            _logger.LogTrace("Sink {name} is not enabled.", nameof(AzureBlobSink));
            return false;
        }

        bool success = _workingQueue.Writer.TryWrite(data);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Payload submitted. Result: {success}", success);
        }
        return success;
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

        if (_workingQueue is not null)
        {
            _logger.LogInformation("Flush the buffer and send data to storage...");
            bool writerCompleted = _workingQueue.Writer.TryComplete();
            if (writerCompleted)
            {
                _logger.LogDebug("Writer completed");
            }
            else
            {
                _logger.LogDebug("Writer failed completing. Maybe already completed before?");
            }

            _logger.LogDebug("Channel writer got completed: {result}", writerCompleted);
            // The last batch
            _logger.LogDebug("Writer completed.");

            await _workingQueue.Reader.Completion.ConfigureAwait(false);
            _logger.LogDebug("Channel reader completed.");

            await FlushAsync(default).ConfigureAwait(false);
        }

        _semaphoreSlim?.Dispose();
        _semaphoreSlim = null;
    }
}