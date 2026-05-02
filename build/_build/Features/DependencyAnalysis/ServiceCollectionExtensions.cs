using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.DependencyAnalysis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDependencyAnalysisFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<OtoolAnalyzePipeline>();

        return services;
    }
}
