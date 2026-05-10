using Build.Targets.Harvest.Reporting;
using Build.Targets.Harvest.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.Harvest;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Harvest-stage collaborators: closure walker, artifact planner + deployer,
    /// and reporter. Cross-cutting concerns come from sibling groups: validators from
    /// <c>AddValidators()</c>; rid-status repository from <c>AddRepositories()</c>; the
    /// per-platform <see cref="Build.DependencyAnalysis.IRuntimeScanner"/> dispatch closure
    /// from <c>AddDependencyAnalysis()</c> (host-platform abstraction, not Harvest-owned).
    /// <see cref="HarvestTask"/> itself is discovered by Cake Frosting from <c>[TaskName]</c>
    /// metadata; do not register it here.
    /// </summary>
    public static IServiceCollection AddHarvest(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IBinaryClosureWalker, BinaryClosureWalker>();
        services.AddSingleton<IArtifactPlanner, ArtifactPlanner>();
        services.AddSingleton<IArtifactDeployer, ArtifactDeployer>();
        services.AddSingleton<HarvestReporter>();

        return services;
    }
}
