using DotNet.Diagnostics.Core;

namespace DotNet.Diagnostics.JobDispatchers.AzureBlob;

public class AzureBlobJobOptions : AzureBlobOptions
{
    public const string DefaultSectionName = "AzureBlob";

    /// <summary>
    /// Gets or sets the name of the container for jobs.
    /// </summary>
    /// <value></value>
    public string ContainerName { get; set; } = "jobs";

    /// <summary>
    /// Gets or sets the folder name for pending jobs.
    /// </summary>
    public string PendingFolder { get; set; } = "pending";

    /// <summary>
    /// Gets or sets the folder name for finished jobs.
    /// </summary>
    public string DoneFolder { get; set; } = "done";
}