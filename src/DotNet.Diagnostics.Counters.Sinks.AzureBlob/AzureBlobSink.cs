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
    private readonly Dictionary<string, MemoryStream> _cache = new Dictionary<string, MemoryStream>(StringComparer.Ordinal);
    private string? _currentStreamKey = null;

    private readonly Channel<ICounterPayload> _workingQueue = Channel.CreateUnbounded<ICounterPayload>(new UnboundedChannelOptions()
    {
        SingleReader = true,
        SingleWriter = false,
    });

    // This makes sure the flushing doesn't happen when the writer's writing the stream.
    private SemaphoreSlim? _semaphoreSlim = new SemaphoreSlim(1, 1);

    private readonly AzureBlobSinkOptions _options;
    private readonly IPayloadWriter _payloadWriter;
    private readonly WebAppContext _webAppContext;
    private readonly ILogger _logger;

    private readonly BlobContainerClient _blobContainerClient;

    public AzureBlobSink(
        IPayloadWriter payloadWriter,
        WebAppContext webAppContext,
        IOptions<AzureBlobSinkOptions> options,
        ILogger<AzureBlobSink> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _payloadWriter = payloadWriter ?? throw new ArgumentNullException(nameof(payloadWriter));
        _webAppContext = webAppContext ?? throw new ArgumentNullException(nameof(webAppContext));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        BlobServiceClient blobServiceClient;
        if (!string.IsNullOrEmpty(_options.ConnectionString))
        {
            blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        }
        else
        {
            if (_options.ServiceUri is null)
            {
                throw new InvalidOperationException("Connection string or ServiceUri can't both be null. Did you miss some configuration?");
            }

            DefaultAzureCredentialOptions defaultAzureCredentialOptions = new DefaultAzureCredentialOptions()
            {
                ManagedIdentityClientId = _options.ManagedIdentityClientId,
                ExcludeInteractiveBrowserCredential = true,
            };
            blobServiceClient = new BlobServiceClient(_options.ServiceUri, new DefaultAzureCredential());
        }
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
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
        _logger.LogInformation("Ensure blob container exists: {containerName}", _blobContainerClient.Name);
        await _blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Blob container exists.");

        await StartReadingQueueAsync(cancellationToken).ConfigureAwait(false);
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
            _workingQueue.Writer.TryComplete();
            // No cancellation token this time.
            await PumpTheChannelAsync(default).ConfigureAwait(false);
            throw;
        }
    }

    private async Task PumpTheChannelAsync(CancellationToken cancellationToken)
    {
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
            string blobName = GetBlobName(data.Timestamp);
            AppendBlobClient blobClient = _blobContainerClient.GetAppendBlobClient(blobName);

            // Check existing leads to a HEAD operation, keep it minimum.
            bool isNew = string.IsNullOrEmpty(_currentStreamKey) && !(await blobClient.ExistsAsync(cancellationToken));

            // New
            if (!_cache.ContainsKey(blobName))
            {
                _logger.LogInformation("Open writing: {blobName}", blobName);
                _currentStreamKey = blobName;
                _cache[_currentStreamKey] = new MemoryStream();
            }

            // Or switch stream
            if (!string.IsNullOrEmpty(_currentStreamKey) && !string.Equals(_currentStreamKey, blobName, StringComparison.Ordinal))
            {
                try
                {
                    // Flush the cache before switching.
                    await AppendBlobAsync().ConfigureAwait(false);
                }
                finally
                {
                    _logger.LogInformation("Open new file for writing: {blobName}", blobName);
                    _currentStreamKey = blobName;
                    _cache[blobName] = new MemoryStream();
                }
            }

            if (string.IsNullOrEmpty(_currentStreamKey))
            {
                return;
            }

            MemoryStream outputCache = _cache[_currentStreamKey];
            if (isNew)
            {
                await WriteHeaderAsync(outputCache, cancellationToken).ConfigureAwait(false);
            }
            await WriteDataAsync(outputCache, data, cancellationToken).ConfigureAwait(false);

            // Persistent every once a while (100K in size)
            if (outputCache.Length > 100 * 1024)
            {
                _logger.LogInformation("Persistent data to Azure Storage.");
                await AppendBlobAsync(_currentStreamKey);
            }
        }
        catch (OperationCanceledException cancel) when (cancel.CancellationToken == cancellationToken)
        {
            _logger.LogDebug("Writing data operation canceled by the user.");
            throw;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    private async Task AppendBlobAsync()
    {
        string[] keys = _cache.Keys.ToArray();
        foreach (string key in keys)
        {
            await AppendBlobAsync(key);
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
                AppendBlobClient blobClient = _blobContainerClient.GetAppendBlobClient(cacheKey);
                block.Seek(0, SeekOrigin.Begin);
                _logger.LogDebug("Appending {size} bytes of data to blob: {blobName}", block.Length, cacheKey);
                await blobClient.CreateIfNotExistsAsync().ConfigureAwait(false);
                await blobClient.AppendBlockAsync(block).ConfigureAwait(false);
                await block.FlushAsync().ConfigureAwait(false);
                await block.DisposeAsync().ConfigureAwait(false);
                _cache.Remove(cacheKey);
                return;
            }
            catch (Exception ex)
            {
                if (retry == 0)
                {
                    await block.DisposeAsync().ConfigureAwait(false);
                    _cache.Remove(cacheKey);
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

        string machineId = string.IsNullOrEmpty(_webAppContext.SiteInstanceId) ? Environment.MachineName : _webAppContext.SiteInstanceId;

        string prefix = string.Join("_", (object)_options.FileNamePrefix, machineId, timestamp.ToUniversalTime().ToString("yyyyMMddHH", CultureInfo.InvariantCulture)).Trim('_');

        string blobName = prefix + fileExtension;
        return blobName;
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

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

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

        _semaphoreSlim?.Dispose();
        _semaphoreSlim = null;
    }
}