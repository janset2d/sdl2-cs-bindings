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

        // PackageConsumerSmokeTask discovered by Cake via [TaskName].
        // IPackageConsumerSmokePreconditionsValidator registered by AddValidators().
        // SmokeScopeComparator is a static class — no registration.
        // IProjectMetadataReader + IDotNetRuntimeEnvironment registered by AddIntegrations() (P10).

        return services;
    }
}
