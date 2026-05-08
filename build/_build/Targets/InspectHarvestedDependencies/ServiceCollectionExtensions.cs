using Build.Targets.InspectHarvestedDependencies.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.InspectHarvestedDependencies;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInspectHarvestedDependenciesTarget(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<HarvestPayloadInspector>();
        return services;
    }
}
