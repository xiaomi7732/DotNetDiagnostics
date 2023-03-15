namespace DotNet.Diagnostics.Counters.Sinks.LocalFile;

internal class LocalFileSinkOptions
{
    public bool ForceCustomFilePathInAzureAppService { get; set; } = false;
    public string FileNamePrefix { get; set; } = "Counters";
    public string OutputFolder { get; set; } = "%TMP%";
}