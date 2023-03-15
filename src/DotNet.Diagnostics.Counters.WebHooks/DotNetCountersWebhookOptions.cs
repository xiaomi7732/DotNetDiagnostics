namespace DotNet.Diagnostics.Counters.WebHooks;

public class DotNetCountersWebhookOptions
{
    public string Endpoint { get; set; } = "/dotnet-counter";

    public string InvokingSecret { get; set; } = "1123";

    public DotNetCountersOptions CounterOptions { get; set; } = DotNetCountersOptions.Default;
}