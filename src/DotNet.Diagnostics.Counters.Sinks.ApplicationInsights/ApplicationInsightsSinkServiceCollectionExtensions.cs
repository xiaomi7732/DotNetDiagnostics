using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.Counters.Sinks.AzureBlob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.Builder;

public static class ApplicationInsightsSinkServiceCollectionExtensions
{
    public static IServiceCollection AddDotNetCounterApplicationInsightsSink(this IServiceCollection services)
    {
        services.TryAddSingleton<ApplicationInsightsSink>();
        services.AddSingleton<ISink<IDotNetCountersClient, ICounterPayload>>(p => p.GetRequiredService<ApplicationInsightsSink>());
        services.AddHostedService<SinkBackgroundService<ApplicationInsightsSink>>();

        return services;
    }
}