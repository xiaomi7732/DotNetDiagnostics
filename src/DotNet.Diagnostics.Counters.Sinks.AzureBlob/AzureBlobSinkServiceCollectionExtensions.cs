using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.Counters.Sinks;
using DotNet.Diagnostics.Counters.Sinks.AzureBlob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.Builder;

public static class AzureBlobSinkServiceCollectionExtensions
{
    public static IServiceCollection AddDotNetCounterAzureBlobSink(this IServiceCollection services, string sectionName = "DotNetCounterAzureBlobSink")
    {
        services.AddOptions<AzureBlobSinkOptions>().Configure<IConfiguration>((opt, config) =>
        {
            config.GetSection(sectionName).Bind(opt);
        });

        services.TryAddSingleton<WebAppContext>(_ => WebAppContext.Instance);
        services.TryAddSingleton<AzureBlobSink>();
        services.TryAddSingleton<ISink<IDotNetCountersClient, ICounterPayload>>( p => p.GetRequiredService<AzureBlobSink>());
        services.AddHostedService<SinkBackgroundService<AzureBlobSink>>();
        services.TryAddSingleton<IPayloadWriter, CSVPayloadWriter>();

        return services;
    }
}