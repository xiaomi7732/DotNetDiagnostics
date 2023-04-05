using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.Counters.Sinks.AzureBlob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.Builder;

public static class AzureBlobSinkServiceCollectionExtensions
{
    public static DotNetCountersPipelineBuilder WithAzureBlobSink(
        this DotNetCountersPipelineBuilder builder,
        string sinksSectionName = SinkOptions.DefaultSectionName,
        string sectionName = AzureBlobSinkOptions.DefaultSectionName)
    {
        builder.AppendAction(services =>
        {
            services.AddDotNetCounterAzureBlobSink(builder.SectionName, sinksSectionName, sectionName);
        });
        return builder;
    }

    private static IServiceCollection AddDotNetCounterAzureBlobSink(this IServiceCollection services, string baseSectionName, string sinksSectionName, string sectionName)
    {
        services.AddOptions<AzureBlobSinkOptions>().Configure<IConfiguration>((opt, config) =>
        {
            config.GetSection(baseSectionName).GetSection(sinksSectionName).GetSection(sectionName).Bind(opt);
        });

        services.TryAddSingleton<WebAppContext>(_ => WebAppContext.Instance);
        services.TryAddSingleton<AzureBlobSink>();
        services.AddSingleton<ISink<IDotNetCountersClient, ICounterPayload>>(p => p.GetRequiredService<AzureBlobSink>());
        services.AddHostedService<SinkBackgroundService<AzureBlobSink>>();
        services.TryAddSingleton<IPayloadWriter, CSVPayloadWriter>();

        return services;
    }
}