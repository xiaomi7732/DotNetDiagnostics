// Copied from https://github.com/dotnet/extensions/blob/3dc5e9a24865ab84fce6fc078fce4bd7cfcab5c7/src/Logging/Logging.AzureAppServices/src/WebAppContext.cs

namespace DotNet.Diagnostics.Counters.Sinks.LocalFile;

/// <summary>
/// Represents the default implementation of <see cref="IWebAppContext"/>.
/// </summary>
internal sealed class WebAppContext
{
    /// <summary>
    /// Gets the default instance of the WebApp context.
    /// </summary>
    public static WebAppContext Instance { get; } = new WebAppContext();

    private WebAppContext() { }

    /// <inheritdoc />
    public string? HomeFolder { get; } = Environment.GetEnvironmentVariable("HOME");

    /// <inheritdoc />
    public string? SiteName { get; } = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");

    /// <inheritdoc />
    public string? SiteInstanceId { get; } = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");

    /// <inheritdoc />
    public bool IsRunningInAzureWebApp => !string.IsNullOrEmpty(HomeFolder) &&
                                          !string.IsNullOrEmpty(SiteName);
}