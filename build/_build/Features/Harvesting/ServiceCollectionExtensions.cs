using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Harvesting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHarvestingFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IBinaryClosureWalker, BinaryClosureWalker>();
        services.AddSingleton<IArtifactPlanner, ArtifactPlanner>();
        services.AddSingleton<IArtifactDeployer, ArtifactDeployer>();

        services.AddSingleton<HarvestPipeline>();
        services.AddSingleton<NativeSmokePipeline>();
        services.AddSingleton<ConsolidateHarvestPipeline>();

        return services;
    }
}
