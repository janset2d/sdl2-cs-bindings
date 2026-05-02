using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Maintenance;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMaintenanceFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<CleanArtifactsPipeline>();
        services.AddSingleton<CompileSolutionPipeline>();

        return services;
    }
}
