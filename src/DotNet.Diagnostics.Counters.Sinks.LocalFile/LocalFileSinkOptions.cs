namespace DotNet.Diagnostics.Counters.Sinks.LocalFile;

public class LocalFileSinkOptions
{
    public const string DefaultSectionName = "LocalFile";

    public bool ForceCustomFilePathInAzureAppService { get; set; } = false;
    public string FileNamePrefix { get; set; } = "Counters";
    public string OutputFolder { get; set; } = "%TMP%";
}