using DotNet.Diagnostics.Core;
using Microsoft.Extensions.Options;

namespace DotNet.Diagnostics.Counters.Sinks.LocalFile;

public class LoggingFileNameProvider
{
    private readonly LocalFileSinkOptions _options;
    private readonly WebAppContext _webAppContext;

    public LoggingFileNameProvider(
        WebAppContext webAppContext,
        IOptions<LocalFileSinkOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _webAppContext = webAppContext ?? throw new ArgumentNullException(nameof(webAppContext));
    }

    public string GetFullFileName(DateTime timestamp, string fileExtension = ".csv")
    {
        return new FileInfo(Path.Combine(GetLoggingFolder(), GetLoggingFileName(timestamp, fileExtension))).FullName;
    }

    private string GetLoggingFileName(DateTime timestamp, string fileExtension = ".csv")
    {
        if (!fileExtension.StartsWith(".", StringComparison.Ordinal) && !string.IsNullOrEmpty(fileExtension))
        {
            fileExtension = "." + fileExtension;
        }

        if (_webAppContext.IsRunningInAzureWebApp && !_options.ForceCustomFilePathInAzureAppService)
        {
            return $"{_options.FileNamePrefix}_{_webAppContext.SiteName}_{_webAppContext.SiteInstanceId}_{timestamp.ToUniversalTime().ToString("yyyyMMddHH")}{fileExtension}";
        }
        else
        {
            return $"{_options.FileNamePrefix}_{timestamp.ToUniversalTime().ToString("yyyyMMddHH")}{fileExtension}";
        }
    }

    private string GetLoggingFolder()
    {
        if (_webAppContext.IsRunningInAzureWebApp && !_options.ForceCustomFilePathInAzureAppService)
        {
            // %HOME%/LogFiles/Application/
            return Path.Combine(_webAppContext.HomeFolder!, "LogFiles", "Application");
        }
        else
        {
            return Environment.ExpandEnvironmentVariables(_options.OutputFolder);
        }
    }
}