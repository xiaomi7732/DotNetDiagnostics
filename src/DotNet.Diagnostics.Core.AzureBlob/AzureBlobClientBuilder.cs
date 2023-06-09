using Azure.Identity;
using Azure.Storage.Blobs;

namespace DotNet.Diagnostics.Core;

public class AzureBlobClientBuilder
{
    private AzureBlobOptions? _options;

    public BlobServiceClient Build()
    {
        AzureBlobOptions options = VerifyOptions();

        if (!string.IsNullOrEmpty(options.ConnectionString))
        {
            return BuildByConnectionString(options.ConnectionString);
        }

        return BuildByTokenCredential();
    }

    public AzureBlobClientBuilder AddAzureBlobOptions(AzureBlobOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    private BlobServiceClient BuildByConnectionString(string connectionString)
    {
        return new BlobServiceClient(connectionString);
    }

    private BlobServiceClient BuildByTokenCredential()
    {
        AzureBlobOptions options = VerifyOptions();
        DefaultAzureCredentialOptions defaultAzureCredentialOptions = new DefaultAzureCredentialOptions()
        {
            ManagedIdentityClientId = options.ManagedIdentityClientId,
            ExcludeInteractiveBrowserCredential = true,
        };
        return new BlobServiceClient(options.ServiceUri, new DefaultAzureCredential(defaultAzureCredentialOptions));
    }

    private AzureBlobOptions VerifyOptions()
    {
        if (_options is null)
        {
            throw new InvalidOperationException("Can't build Azure Blob Client by token credential. Did you call `AddAzureBlobOptions` first?");
        }

        if (string.IsNullOrEmpty(_options.ConnectionString) && _options.ServiceUri is null)
        {
            throw new InvalidOperationException("Connection string or ServiceUri can't both be null. Did you miss some configuration?");
        }

        return _options;
    }
}
