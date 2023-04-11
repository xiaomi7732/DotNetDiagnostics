namespace DotNet.Diagnostics.Counters;

public sealed class JobNameProvider
{
    private JobNameProvider()
    {
    }
    public static JobNameProvider Instance { get; } = new JobNameProvider();

    public string GetFullName(string baseFolder, char separator = '/')
        => string.Join(separator, GetFolder(baseFolder, separator), GetFileName());

    public string GetFolder(string baseFolder, char separator = '/')
        => string.Join(separator, baseFolder, "dotnet-counters");

    private string GetFileName()
        => Guid.NewGuid().ToString("D");
}
