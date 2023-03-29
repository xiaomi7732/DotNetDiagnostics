namespace DotNet.Diagnostics.Counters.Sinks;

public class AzureBlobSinkOptions
{
    /// <summary>
    /// Gets or sets the connection string to the Azure Blob Storage.
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the client id for managed identity.
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }

    /// <summary>
    /// Gets or sets the service Uri for the storage.
    /// </summary>
    public Uri? ServiceUri { get; set; }

    /// <summary>
    /// Gets or sets the container name for the sink output.
    /// </summary>
    public string ContainerName { get; set; } = "dotnet-counters";

    /// <summary>
    /// Gets or sets the prefix for blob name.
    /// </summary>
    public string FileNamePrefix { get; set; } = "";

}