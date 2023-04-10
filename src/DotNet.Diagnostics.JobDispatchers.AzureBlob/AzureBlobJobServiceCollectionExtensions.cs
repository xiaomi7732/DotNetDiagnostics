using DotNet.Diagnostics.Core;
using DotNet.Diagnostics.JobDispatchers.AzureBlob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.Builder;

public static class AzureBlobJobServiceCollectionExtensions
{
    public static DotNetCountersPipelineBuilder WithAzureBlobJobDispatcher(
        this DotNetCountersPipelineBuilder builder,
        string jobsSectionName = JobOptions.DefaultSectionName,
        string sectionName = AzureBlobJobOptions.DefaultSectionName)
    {
        builder.AppendAction(services =>
        {
            services.AddDotNetCountersAzureBlobJobDispatcher(builder.SectionName, jobsSectionName, sectionName);
        });
        return builder;
    }

    private static IServiceCollection AddDotNetCountersAzureBlobJobDispatcher(
        this IServiceCollection services,
        string baseSectionName,
        string jobsSectionName,
        string sectionName
        )
    {
        services.AddOptions<AzureBlobJobOptions>().Configure<IConfiguration>((opt, configuration) =>
        {
            configuration.GetSection(baseSectionName).GetSection(jobsSectionName).GetSection(sectionName).Bind(opt);
        });

        services.TryAddSingleton<JsonSerializerOptionsProvider>(_ => JsonSerializerOptionsProvider.Instance);
        services.AddSingleton<TokenCredential<AzureBlobJobDispatcher>, AzureBlobJobTokenCredential>();
        services.TryAddTransient<AzureBlobClientBuilder>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IJobDispatcher<DotNetCountersJobDetail>, AzureBlobJobDispatcher>());
        return services;
    }
}
