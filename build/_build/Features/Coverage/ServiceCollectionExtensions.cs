using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Coverage;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoverageFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICoverageThresholdValidator, CoverageThresholdValidator>();
        services.AddSingleton<CoverageCheckPipeline>();

        return services;
    }
}
