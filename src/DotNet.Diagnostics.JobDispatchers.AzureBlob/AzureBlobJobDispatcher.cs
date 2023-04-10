using System.Net.Mime;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DotNet.Diagnostics.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.JobDispatchers.AzureBlob;

public class AzureBlobJobDispatcher : IJobDispatcher<DotNetCountersJobDetail>
{
    private readonly AzureBlobJobOptions _options;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly JsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly ILogger _logger;

    public AzureBlobJobDispatcher(
        AzureBlobClientBuilder blobClientBuilder,
        IOptions<AzureBlobJobOptions> options,
        TokenCredential<AzureBlobJobDispatcher> tokenCredential,
        JsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        ILogger<AzureBlobJobDispatcher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (blobClientBuilder is null)
        {
            throw new ArgumentNullException(nameof(blobClientBuilder));
        }
        _jsonSerializerOptionsProvider = jsonSerializerOptionsProvider ?? throw new ArgumentNullException(nameof(jsonSerializerOptionsProvider));
        BlobServiceClient blobServiceClient = blobClientBuilder.WithAzureBlobOptions(_options).Build();
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
    }

    public async Task<bool> DispatchAsync(DotNetCountersJobDetail jobDetails, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Dispatching job...");
        await _blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        string blobName = GetBlobName();
        BlobClient blobClient = _blobContainerClient.GetBlobClient(blobName);

        _logger.LogDebug("Uploading data...");
        BinaryData data = new BinaryData(jobDetails, _jsonSerializerOptionsProvider.Default);
        BlobContentInfo info = await blobClient.UploadAsync(
            data,
            overwrite: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Data uploaded.");

        bool result = info is not null;

        _logger.LogDebug("Setting up content type header...");
        await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders()
        {
            ContentType = MediaTypeNames.Application.Json,
        }).ConfigureAwait(false);
        _logger.LogDebug("Set up content type header.");

        _logger.LogInformation("Finished dispatch job at {blobName}. Result: {dispatchResult}", blobName, result);
        return result;
    }

    private string GetBlobName()
    {
        string blobName = string.Join('/', _options.PendingFolder, "dotnet-counters", Guid.NewGuid().ToString("D"));
        return blobName;
    }
}