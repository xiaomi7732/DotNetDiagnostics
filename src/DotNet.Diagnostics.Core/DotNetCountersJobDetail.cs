namespace DotNet.Diagnostics.Core;

public class DotNetCountersJobDetail : JobDetailsBase
{
    public IDictionary<string, string>? Filter { get; set; }
    public bool IsEnabled { get; set; }
}