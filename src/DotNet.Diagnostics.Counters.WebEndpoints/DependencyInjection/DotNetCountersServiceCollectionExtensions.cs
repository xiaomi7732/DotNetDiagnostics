using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.Core.Utilities;
using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.Counters.WebEndpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class DotNetCountersServiceCollectionExtensions
{
    public static DotNetCountersPipelineBuilder AddDotNetCounters(this IServiceCollection services, string sectionName = "DotNetCounters")
    {
        Action<IServiceCollection> actions = (services =>
        {
            services.AddOptions<DotNetCountersWebhookOptions>().Configure<IConfiguration>((opt, config) =>
            {
                config.GetSection(sectionName).Bind(opt);
            });

            services.AddOptions<DotNetCountersOptions>().Configure<IConfiguration>((opt, config) =>
            {
                config.GetSection(sectionName).Bind(opt);
            });

            services.TryAddSingleton<DotNetCountEventCounterManager>();
            services.TryAddSingleton<DotnetCountersProcessIdProvider>(_ => DotnetCountersProcessIdProvider.Instance);
            services.TryAddSingleton<IDotNetCountersClient, DotNetCountersClient>();
            services.TryAddSingleton<EnvVarMatcher>(_ => EnvVarMatcher.Instance);

            services.TryAddSingleton<ICounterPayloadSet>(_ => EndpointSink.Instance);
            services.TryAddSingleton<ISink<IDotNetCountersClient, ICounterPayload>>(p => (ISink<IDotNetCountersClient, ICounterPayload>)p.GetRequiredService<ICounterPayloadSet>());
        });

        return new DotNetCountersPipelineBuilder(actions, services, sectionName);
    }
}