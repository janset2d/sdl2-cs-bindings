using Build.Targets.ConsolidateHarvest.Reporting;
using Build.Targets.ConsolidateHarvest.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.ConsolidateHarvest;

/// <summary>
/// Registers the ConsolidateHarvest target's collaborators. File-backed manifest IO is owned
/// by Data; this target keeps license union, atomic file/dir swap, and reporting services so
/// the task body reads as a linear staged-replace narrative.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidateHarvest(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<LicenseUnionWriter>();
        services.AddSingleton<StagedArtifactSwapper>();
        services.AddSingleton<ConsolidateHarvestReporter>();

        return services;
    }
}
