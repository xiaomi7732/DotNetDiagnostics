using System.Diagnostics.CodeAnalysis;
using DotNet.Diagnostics.Counters;
using DotNet.Diagnostics.Counters.WebHooks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Builder;

public static class DotNetCountersEndpointRouteBuilderExtensions
{
    private const string DefaultDisplayName = "DotNet Counters";

    /// <summary>
    /// Adds a dotnet-counters endpoint to the <see cref="IEndpointRouteBuilder"/> with the specified template.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the dotnet counters endpoint to.</param>
    /// <param name="pattern">The URL pattern of the dotnet counters endpoint.</param>
    /// <returns>A convention routes for the dotnet counters endpoint.</returns>
    public static IEndpointConventionBuilder MapDotNetCounters(
       this IEndpointRouteBuilder endpoints,
       string pattern)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return MapDotNetCountersCore(endpoints, pattern, null);
    }

    /// <summary>
    /// Adds a dotnet counters endpoint to the <see cref="IEndpointRouteBuilder"/> with the specified template and options.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the dotnet counters endpoint to.</param>
    /// <param name="pattern">The URL pattern of the dotnet counters endpoint.</param>
    /// <param name="options">A <see cref="HealthCheckOptions"/> used to configure the dotnet counters.</param>
    /// <returns>A convention routes for the dotnet counters endpoint.</returns>
    public static IEndpointConventionBuilder MapDotNetCounters(
       this IEndpointRouteBuilder endpoints,
       string pattern,
       DotNetCountersWebhookOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(options);

        return MapDotNetCountersCore(endpoints, pattern, options);
    }

    [UnconditionalSuppressMessage("Trimmer", "IL2026",
        Justification = "MapHealthChecksCore only RequireUnreferencedCode if the RequestDelegate has a Task<T> return type which is not the case here.")]
    private static IEndpointConventionBuilder MapDotNetCountersCore(IEndpointRouteBuilder endpoints, string pattern, DotNetCountersWebhookOptions? options)
    {
        if(endpoints.ServiceProvider.GetService(typeof(IDotNetCountersClient)) == null)
        {
            throw new InvalidOperationException($"Unable to find service {nameof(IDotNetCountersClient)} in {nameof(IServiceCollection)}.");
        }

        var args = options != null ? new[] { Options.Create(options) } : Array.Empty<object>();

        var pipeline = endpoints.CreateApplicationBuilder()
           .UseMiddleware<DotNetCounterMiddleware>(args)
           .Build();

        return endpoints.Map(pattern, pipeline).WithDisplayName(DefaultDisplayName);
    }
}