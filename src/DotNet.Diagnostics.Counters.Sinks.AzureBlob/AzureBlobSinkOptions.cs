using DotNet.Diagnostics.Core;

namespace DotNet.Diagnostics.Counters.Sinks.AzureBlob;

public class AzureBlobSinkOptions : AzureBlobOptions
{
    public const string DefaultSectionName="AzureBlob";

    /// <summary>
    /// Gets or sets the container name for the sink output.
    /// </summary>
    public string ContainerName { get; set; } = "dotnet-counters";

    /// <summary>
    /// Gets or sets the prefix for blob name.
    /// </summary>
    public string FileNamePrefix { get; set; } = "";

}