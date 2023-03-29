using System.Globalization;
using System.Threading.Channels;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using DotNet.Diagnostics.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.Sinks.AzureBlob;

internal sealed class AzureBlobSink : ISink<IDotNetCountersClient, ICounterPayload>, IAsyncDisposable
{
    private (string Name, Stream Stream)? _currentStream = null;

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
        await _semaphoreSlim!.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentStream is not null)
            {
                await _currentStream.Value.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
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

        await foreach (ICounterPayload data in _workingQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await WriteDataAsync(data, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteDataAsync(ICounterPayload data, CancellationToken cancellationToken)
    {
        await _semaphoreSlim!.WaitAsync(cancellationToken);
        try
        {
            string blobName = GetBlobName(data.Timestamp);
            AppendBlobClient blobClient = _blobContainerClient.GetAppendBlobClient(blobName);

            // New
            if (_currentStream is null)
            {
                _logger.LogInformation("Open writing: {blobName}", blobName);
                _currentStream = (blobName, await blobClient.OpenWriteAsync(overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false));
                await WriteHeaderAsync(_currentStream.Value.Stream, cancellationToken).ConfigureAwait(false);
            }

            // Switch stream
            if (!string.Equals(_currentStream.Value.Name, blobName, StringComparison.Ordinal))
            {
                try
                {
                    await _currentStream.Value.Stream.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected exception disposing last output: {blobName}. Data file might be corrupted.", _currentStream?.Name);
                }
                finally
                {
                    _logger.LogInformation("Open new file for writing: {blobName}", blobName);
                    _currentStream = (blobName, await blobClient.OpenWriteAsync(overwrite: false, cancellationToken: cancellationToken).ConfigureAwait(false));
                    await WriteHeaderAsync(_currentStream.Value.Stream, cancellationToken).ConfigureAwait(false);
                }
            }

            await WriteDataAsync(_currentStream.Value.Stream, data, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphoreSlim.Release();
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

        string prefix = string.Join("_", (object)_options.FileNamePrefix, machineId, timestamp.ToString("yyyyMMddHH", CultureInfo.InvariantCulture)).Trim('_');

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
        if (_currentStream?.Stream is not null)
        {
            await _currentStream.Value.Stream.DisposeAsync();
            _currentStream = null;
        }

        _semaphoreSlim?.Dispose();
        _semaphoreSlim = null;
    }
}