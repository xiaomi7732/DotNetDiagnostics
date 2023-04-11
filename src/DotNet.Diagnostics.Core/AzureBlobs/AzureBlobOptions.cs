namespace DotNet.Diagnostics.Core;

public abstract class AzureBlobOptions : TokenAuthOptions
{
    /// <summary>
    /// Gets or sets the connection string to the Azure Blob Storage.
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the service Uri for the storage.
    /// </summary>
    public Uri? ServiceUri { get; set; }
}