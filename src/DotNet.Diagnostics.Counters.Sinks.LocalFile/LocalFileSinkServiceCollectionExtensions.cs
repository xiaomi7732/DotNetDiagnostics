using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.Counters.Sinks.LocalFile;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

public static class LocalFileSinkServiceCollectionExtensions
{
    public static IServiceCollection AddDotNetCounterLocalFileSink(this IServiceCollection services, string sectionName = "DotNetCounterLocalFileSink")
    {
        services.AddOptions<LocalFileSinkOptions>().Configure<IConfiguration>((opt, configure) =>
        {
            configure.GetSection(sectionName).Bind(opt);
        });

        services.AddSingleton<WebAppContext>(_ => WebAppContext.Instance);
        services.AddSingleton<LoggingFileNameProvider>();
        services.AddSingleton<LocalFileSink>();
        services.AddSingleton<ISink<IDotNetCountersClient, ICounterPayload>>(p => p.GetRequiredService<LocalFileSink>());
        services.AddHostedService<LocalFileSinkBackgroundService>();
        services.AddSingleton<IPayloadWriter, CSVPayloadWriter>();

        return services;
    }
}