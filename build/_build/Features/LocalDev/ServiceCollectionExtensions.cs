using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.LocalDev;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the LocalDev orchestration feature. Per ADR-004 §2.5, this is the only
    /// designated multi-feature compose feature in the build host — it consumes pipelines
    /// from sibling features (Preflight, Vcpkg, Harvesting, Packaging) via the
    /// architecture-test invariant #4 allowlist exception. The feature is registered last
    /// in the composition root so that every dependency it pulls (sibling pipelines, the
    /// active <see cref="IArtifactSourceResolver"/>) is already configured.
    /// </summary>
    public static IServiceCollection AddLocalDevFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SetupLocalDevFlow>();

        return services;
    }
}
