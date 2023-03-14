using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.Counters.WebHooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class DotNetCountersServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="IDotNetCounterClient"/> to the container, using the provided delegate to register
    /// health checks.
    /// </summary>
    /// <remarks>
    /// This operation is idempotent - multiple invocations will still only result in a single
    /// <see cref="HealthCheckService"/> instance in the <see cref="IServiceCollection"/>. It can be invoked
    /// multiple times in order to get access to the <see cref="IHealthChecksBuilder"/> in multiple places.
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the <see cref="HealthCheckService"/> to.</param>
    /// <returns>An instance of <see cref="IHealthChecksBuilder"/> from which health checks can be registered.</returns>
    public static IServiceCollection AddDotNetCounters(this IServiceCollection services, string configurationSectionName = "DotNetCounters")
    {
        services.TryAddSingleton<IDotNetCountersClient, DummyDotNetCountersClient>();
        services.AddOptions<DotNetCountersWebhookOptions>().Configure<IConfiguration>((opt, config) =>
        {
            config.GetSection(configurationSectionName).Bind(opt);
        });
        return services;
    }
}