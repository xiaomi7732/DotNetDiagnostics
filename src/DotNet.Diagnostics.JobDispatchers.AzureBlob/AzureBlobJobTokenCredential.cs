using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.JobDispatchers.AzureBlob;

public class AzureBlobJobTokenCredential : TokenCredentialBase<AzureBlobJobDispatcher, AzureBlobJobOptions>
{
    public AzureBlobJobTokenCredential(IOptions<AzureBlobJobOptions> options)
        : base(options.Value)
    {
    }
}