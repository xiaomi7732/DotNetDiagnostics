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

public sealed class AzureBlobSink : SinkBase<IDotNetCountersClient, ICounterPayload>, IAsyncDisposable
{
    private bool _isDisposed = false;
    private readonly ConcurrentDictionary<string, MemoryStream> _cache = new ConcurrentDictionary<string, MemoryStream>(StringComparer.Ordinal);

    // Assuming there won't be to much pressure to hold to many items.
    private const int _maxCacheSize = 100 * 1024;

    private Channel<ICounterPayload>? _workingQueue = null;

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

    public override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.InitializeAsync(cancellationToken);
            _logger.LogInformation("Ensure blob container exists: {containerName}", _options.ContainerName);
            await _blobContainerClient.Value.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Blob container exists.");

        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogError(ex, "Failed initialize the sink. Have you configured the client id for the managed identity?");
        }
    }

    protected override Task<bool> OnStartingAsync(CancellationToken cancellationToken)
    {
        _workingQueue = Channel.CreateUnbounded<ICounterPayload>(new UnboundedChannelOptions()
        {
            SingleReader = true,
            SingleWriter = false,
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await StartReadingQueueAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception reading working queue.");
            }
        }, cancellationToken);
        return Task.FromResult(true);
    }

    protected override async Task<bool> OnStoppingAsync(CancellationToken cancellationToken)
    {
        if (_isDisposed || _workingQueue is null)
        {
            return false;
        }

        if (!_workingQueue.Writer.TryComplete())
        {
            _logger.LogDebug("Writing queue already completed?");
        }

        await _workingQueue.Reader.Completion.ConfigureAwait(false);
        await FlushBlobsAsync().ConfigureAwait(false);
        return true;
    }

    protected override bool OnSubmit(ICounterPayload data)
    {
        if (_workingQueue is null)
        {
            _logger.LogTrace("How is sink {name} not enabled?", nameof(AzureBlobSink));
            return false;
        }

        bool success = _workingQueue.Writer.TryWrite(data);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Payload submitted. Result: {success}", success);
        }
        return success;
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
                await FlushBlobsAsync();
            }
        }
        catch (OperationCanceledException cancel) when (cancel.CancellationToken == cancellationToken)
        {
            _logger.LogDebug("Writing data operation canceled by the user.");
            throw;
        }
    }

    private long GetTotalCacheSize()
    {
        return _cache.Values.Sum(s => s.Length);
    }

    private async Task FlushBlobsAsync()
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

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

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

            await StopAsync(default).ConfigureAwait(false);
        }

        Dispose();
        _isDisposed = true;
    }
}