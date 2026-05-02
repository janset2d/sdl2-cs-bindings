using Build.Features.Preflight;
using Build.Host.Configuration;
using Build.Shared.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Versioning;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVersioningFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Stage tasks consume already-resolved explicit versions only. ResolveVersions handles
        // every release shape upstream of stages: manifest+suffix dispatch, explicit dispatch,
        // targeted family-tag push, and meta-tag train push. Downstream jobs feed the resolved
        // versions.json back in via --explicit-version / --versions-file.
        services.AddSingleton<IPackageVersionProvider>(provider =>
        {
            var manifest = provider.GetRequiredService<ManifestConfig>();
            var upstreamVersionAlignmentValidator = provider.GetRequiredService<IUpstreamVersionAlignmentValidator>();
            var packageBuildConfig = provider.GetRequiredService<PackageBuildConfiguration>();
            return new ExplicitVersionProvider(manifest, upstreamVersionAlignmentValidator, packageBuildConfig.ExplicitVersions);
        });

        services.AddSingleton<ResolveVersionsPipeline>();

        return services;
    }
}
