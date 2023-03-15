namespace DotNet.Diagnostics.Counters.Sinks.LocalFile;

internal class LocalFileSinkOptions
{
    public string FileNamePrefix { get; set; } = "Counters";
    public string OutputFolder { get; set; } = "%TMP%";
}