using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.Counters.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.Builder;

public static class ProcessStartTriggerServiceCollectionExtensions
{
    public static DotNetCountersPipelineBuilder WithProcessStartTrigger(
        this DotNetCountersPipelineBuilder builder,
        string triggersSectionName = TriggerOptions.DefaultSectionName,
        string sectionName = ProcessStartTriggerOptions.DefaultSectionName)
    {
        builder.AppendAction(services =>
        {
            AddDotNetCounterProcessStartTrigger(services, builder.SectionName, triggersSectionName, sectionName);
        });
        return builder;
    }

    private static IServiceCollection AddDotNetCounterProcessStartTrigger(this IServiceCollection services, string baseSectionName, string triggersSectionName, string sectionName)
    {
        services.AddOptions<ProcessStartTriggerOptions>().Configure<IConfiguration>((opt, configuration) =>
        {
            configuration.GetSection(baseSectionName).GetSection(triggersSectionName).GetSection(sectionName).Bind(opt);
        });

        services.TryAddSingleton<IDotNetCountersClient, DotNetCountersClient>();
        services.AddHostedService<ProcessStartTrigger>();

        return services;
    }
}