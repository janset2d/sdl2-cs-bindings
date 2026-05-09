using Build.Targets.ConsolidateHarvest.Reporting;
using Build.Targets.ConsolidateHarvest.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.ConsolidateHarvest;

/// <summary>
/// Registers the ConsolidateHarvest target's collaborators. Three services + one reporter,
/// each scoped to a single responsibility (RID-status load + manifest build/write, license
/// union with divergence detection, atomic file/dir swap) so the task body reads as a
/// linear staged-replace narrative.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidateHarvest(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<HarvestArtifactMerger>();
        services.AddSingleton<LicenseUnionWriter>();
        services.AddSingleton<StagedArtifactSwapper>();
        services.AddSingleton<ConsolidateHarvestReporter>();

        return services;
    }
}
