using Build.Targets.Harvest.Reporting;
using Build.Targets.Harvest.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.Harvest;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Harvest-stage collaborators: closure walker, artifact planner + deployer, and
    /// reporter. Cross-cutting validators (<c>IHybridStaticLeakValidator</c>,
    /// <c>IHarvestPreconditionsValidator</c>) come from <c>AddValidators()</c>; the rid-status
    /// repository (<c>IHarvestStatusRepository</c>) comes from <c>AddRepositories()</c> per the
    /// repository-cohort rule (root <c>Build.Repositories</c>, interface-bound, mock + sociable
    /// tests). <see cref="HarvestTask"/> itself is discovered by Cake Frosting from
    /// <c>[TaskName]</c> metadata; do not register it here.
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
