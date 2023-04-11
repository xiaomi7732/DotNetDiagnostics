using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.Core.Utilities;
using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.JobDispatchers.AzureBlob;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.JobDispatchers;

public class AzureBlobJobMatcher : IJobMatcher<DotNetCountersJobDetail>
{
    private readonly AzureBlobJobOptions _options;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly EnvVarMatcher _jobMatcher;
    private readonly JobNameProvider _jobNameProvider;
    private readonly JsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly IDotNetCountersClient _dotnetCountersClient;
    private readonly ILogger _logger;

    public AzureBlobJobMatcher(
        AzureBlobClientBuilder blobClientBuilder,
        IOptions<AzureBlobJobOptions> options,
        TokenCredential<AzureBlobJobDispatcher> tokenCredential,    // Using the same credential as dispatcher
        EnvVarMatcher jobMatcher,
        JobNameProvider jobNameProvider,
        JsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        IDotNetCountersClient dotnetCountersClient,
        ILogger<AzureBlobJobMatcher> logger
        )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (blobClientBuilder is null)
        {
            throw new ArgumentNullException(nameof(blobClientBuilder));
        }
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _jobMatcher = jobMatcher ?? throw new ArgumentNullException(nameof(jobMatcher));
        _jobNameProvider = jobNameProvider ?? throw new ArgumentNullException(nameof(jobNameProvider));
        _jsonSerializerOptionsProvider = jsonSerializerOptionsProvider ?? throw new ArgumentNullException(nameof(jsonSerializerOptionsProvider));
        _dotnetCountersClient = dotnetCountersClient ?? throw new ArgumentNullException(nameof(dotnetCountersClient));
        BlobServiceClient blobServiceClient = blobClientBuilder.WithAzureBlobOptions(_options).Build();
        _blobContainerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
    }

    public async Task<DotNetCountersJobDetail?> MatchAndExecuteAsync(CancellationToken cancellationToken)
    {
        string prefix = _jobNameProvider.GetFolder(_options.PendingFolder);
        List<(DateTime CreateOn, DotNetCountersJobDetail Job, BlobItem Blob)> myJobs = new List<(DateTime, DotNetCountersJobDetail, BlobItem)>();
        try
        {
            IAsyncEnumerable<Page<BlobItem>> resultSegment = _blobContainerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken)
                .AsPages(continuationToken: default, pageSizeHint: 20);

            await foreach (Page<BlobItem> blobPage in resultSegment)
            {
                foreach (BlobItem blobItem in blobPage.Values)
                {
                    (DateTime, DotNetCountersJobDetail, BlobItem)? myJobDetail = await GetMyJobsAsync(blobItem, cancellationToken).ConfigureAwait(false);
                    if (myJobDetail.HasValue)
                    {
                        myJobs.Add(myJobDetail.Value);
                    }
                }
            }
        }
        catch (RequestFailedException e)
        {
            _logger.LogError(e, "Failed fetching job blob.");
            return null;
        }

        if (myJobs.Count == 0)
        {
            _logger.LogInformation("No job matched.");
            return null;
        }

        (DateTime _, DotNetCountersJobDetail effectiveJob, BlobItem jobBlob) = myJobs.MaxBy((item) => item.CreateOn);

        BlobClient blobClient = _blobContainerClient.GetBlobClient(jobBlob.Name);
        BlobLeaseClient blobLeaseClient = new BlobLeaseClient(blobClient);

        try
        {
            BlobLease lease = await blobLeaseClient.AcquireAsync(TimeSpan.FromSeconds(30), cancellationToken: cancellationToken).ConfigureAwait(false);
            await ExecuteJobAsync(effectiveJob.IsEnabled, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException e)
        {
            _logger.LogError(e, "Failed acquire lease for blob: {blobName}", jobBlob.Name);
        }
        finally
        {
            await blobLeaseClient.ReleaseAsync().ConfigureAwait(false);
        }

        foreach (var myJob in myJobs)
        {
            BlobClient myJobBlobClient = _blobContainerClient.GetBlobClient(myJob.Blob.Name);
            BlobLeaseClient myJobLeaseClient = new BlobLeaseClient(myJobBlobClient);

            try
            {
                BlobLease myJobLease = await myJobLeaseClient.AcquireAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                await MarkCompleteAsync(myJobLease, myJobBlobClient).ConfigureAwait(false);
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Can't mark job complete for blob: {blobName}", myJobBlobClient.Name);
            }
        }

        return effectiveJob;
    }

    private async Task<bool> MarkCompleteAsync(BlobLease lease, BlobClient pendingJob)
    {
        return await pendingJob.DeleteIfExistsAsync(conditions: new BlobRequestConditions() { LeaseId = lease.LeaseId }).ConfigureAwait(false);
    }

    private async Task<(DateTime CreatedOn, DotNetCountersJobDetail JobDetail, BlobItem BlobItem)?> GetMyJobsAsync(BlobItem blobItem, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _blobContainerClient.GetBlobClient(blobItem.Name);

        BlobDownloadResult result = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);

        // No blob item downloaded
        if (result is null || result.Details.ContentLength == 0)
        {
            return null;
        }

        // It is not dotnet counter job details?
        DotNetCountersJobDetail? jobDetail = await JsonSerializer.DeserializeAsync<DotNetCountersJobDetail>(result.Content.ToStream(), _jsonSerializerOptionsProvider.Default, cancellationToken).ConfigureAwait(false);
        if (jobDetail is null)
        {
            return null;
        }

        if (jobDetail.Filter is null)
        {
            _logger.LogError("Got a dispatched job without filter. How could that happen? Job path: {blobName}", blobItem.Name);
            return null;
        }

        // Does not match
        if (!_jobMatcher.MatchAll(jobDetail.Filter))
        {
            _logger.LogDebug("Job {jobPath} is not for this instance.", blobItem.Name);
            return null;
        }

        // It is a hit
        _logger.LogInformation("Execute job by {blobName}", blobItem.Name);
        return (result.Details.CreatedOn.UtcDateTime, jobDetail, blobItem);
    }

    private Task ExecuteJobAsync(bool isEnabled, CancellationToken cancellationToken)
    {
        if (isEnabled)
        {
            return _dotnetCountersClient.EnableAsync(cancellationToken: cancellationToken);
        }
        else
        {
            return _dotnetCountersClient.DisableAsync(cancellationToken: cancellationToken);
        }
    }
}
