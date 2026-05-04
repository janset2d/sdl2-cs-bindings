using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Versioning;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVersioningFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Two task-per-source pattern: ResolveVersionsFromManifestTask and
        // ResolveVersionsFromExplicitTask self-register via Cake.Frosting [TaskName]
        // attribute discovery. Only the shared output writer needs DI here; both tasks
        // inject it to produce identical versions.json shape consumed by stage targets.
        services.AddSingleton<VersionsJsonWriter>();

        return services;
    }
}
