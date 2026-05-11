using Build.Host.Cake;
using Build.Targets.Harvest.Models;
using Build.Targets.Harvest.Reporting;
using Build.Targets.Harvest.Services;
using Cake.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.Harvest;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Harvest-stage collaborators: closure walker, artifact planner + deployer,
    /// and reporter. Cross-cutting concerns come from sibling groups: validators from
    /// <c>AddValidators()</c>; rid-status repository from <c>AddData()</c>; the
    /// per-platform <see cref="IRuntimeScanner"/> dispatch is registered here because
    /// runtime dependency scanning is part of the Harvest closure-walking boundary.
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

        services.AddSingleton<IRuntimeScanner>(provider =>
        {
            var env = provider.GetRequiredService<ICakeEnvironment>();
            var context = provider.GetRequiredService<ICakeContext>();

            var currentRid = env.Platform.Rid();
            return currentRid switch
            {
                Rids.WinX64 or Rids.WinX86 or Rids.WinArm64 => new WindowsDumpbinScanner(context),
                Rids.LinuxX64 or Rids.LinuxArm64 => new LinuxLddScanner(context),
                Rids.OsxX64 or Rids.OsxArm64 => new MacOtoolScanner(context),
                _ => throw new NotSupportedException($"Unsupported OS for IRuntimeScanner: {currentRid}"),
            };
        });

        return services;
    }
}
