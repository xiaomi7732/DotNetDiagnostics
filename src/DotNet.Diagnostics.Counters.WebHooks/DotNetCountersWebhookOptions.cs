namespace DotNet.Diagnostics.Counters.WebHooks;

public class DotNetCountersWebhookOptions
{
    public string Endpoint { get; set; } = "/dotnet-counter";

    public DotNetCountersOptions CounterOptions { get; set; } = DotNetCountersOptions.Default;
}