using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.Counters.Sinks.LocalFile;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.Builder;

public static class LocalFileSinkServiceCollectionExtensions
{
    public static DotNetCountersPipelineBuilder WithLocalFileSink(
        this DotNetCountersPipelineBuilder builder,
        string sinksSectionName = SinkOptions.DefaultSectionName,
        string sectionName = LocalFileSinkOptions.DefaultSectionName)
    {
        builder.AppendAction(services =>
        {
            services.AddDotNetCounterLocalFileSink(builder.SectionName, sinksSectionName, sectionName);
        });
        return builder;
    }

    private static IServiceCollection AddDotNetCounterLocalFileSink(this IServiceCollection services, string baseSectionName, string sinksSectionName, string sectionName)
    {
        services.AddOptions<LocalFileSinkOptions>().Configure<IConfiguration>((opt, configure) =>
        {
            configure.GetSection(baseSectionName).GetSection(sinksSectionName).GetSection(sectionName).Bind(opt);
        });

        services.TryAddSingleton<WebAppContext>(_ => WebAppContext.Instance);
        services.AddSingleton<LoggingFileNameProvider>();
        services.AddSingleton<LocalFileSink>();
        services.AddSingleton<ISink<IDotNetCountersClient, ICounterPayload>>(p => p.GetRequiredService<LocalFileSink>());
        services.AddHostedService<SinkBackgroundService<LocalFileSink>>();
        services.TryAddSingleton<IPayloadWriter, CSVPayloadWriter>();

        return services;
    }
}