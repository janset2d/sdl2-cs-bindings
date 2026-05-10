using Build.Targets.PackageConsumerSmoke.Reporting;
using Build.Targets.PackageConsumerSmoke.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.PackageConsumerSmoke;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPackageConsumerSmoke(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<DotNetSmokeRunner>();
        services.AddSingleton<MonoAvailabilityProbe>();
        services.AddSingleton<PackageConsumerSmokeReporter>();
        services.AddSingleton<IDotNetRuntimeEnvironment, DotNetRuntimeEnvironment>();

        // PackageConsumerSmokeTask discovered by Cake via [TaskName].
        // IPackageConsumerSmokePreconditionsValidator registered by AddValidators().
        // SmokeScopeComparator is a static class — no registration.
        // IProjectMetadataReader registered by AddPackage() (Build.Data.ProjectMetadata,
        // shared cross-target reader).

        return services;
    }
}
