#pragma warning disable MA0045

using Build.Host.Cake;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Integrations.DependencyAnalysis;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Cake.Core;
using Cake.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Host;

/// <summary>
/// Composition-root grouping for Host-tier services (ADR-004 §2.11 + §2.12):
/// path resolution, runtime profile, manifest-derived singletons, and the
/// per-platform <see cref="IRuntimeScanner"/> dispatch closure. Manifest-derived
/// configs (<see cref="ManifestConfig"/>, <see cref="RuntimeConfig"/>,
/// <see cref="SystemArtefactsConfig"/>) live here because they are loaded once
/// at startup from <c>build/manifest.json</c> via <see cref="IPathService"/> +
/// <see cref="ICakeContext"/>, both Host-tier resolutions.
/// <para>
/// Takes <see cref="ParsedArguments"/> directly because <see cref="IPathService"/>
/// composes its layout from CLI overrides (<c>--vcpkg-dir</c>, <c>--vcpkg-installed-dir</c>)
/// before any DI resolution can happen.
/// </para>
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHostBuildingBlocks(this IServiceCollection services, ParsedArguments parsedArgs)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(parsedArgs);

        services.AddSingleton<IPathService>(provider =>
        {
            var repositoryConfiguration = provider.GetRequiredService<RepositoryConfiguration>();
            var cakeLogger = provider.GetRequiredService<ICakeLog>();
            return new PathService(repositoryConfiguration, parsedArgs, cakeLogger);
        });

        services.AddSingleton<IRuntimeProfile>(sp =>
        {
            var runtimeConfig = sp.GetRequiredService<RuntimeConfig>();
            var systemArtefactsConfig = sp.GetRequiredService<SystemArtefactsConfig>();
            var vcpkgConfiguration = sp.GetRequiredService<VcpkgConfiguration>();
            var cakeEnvironment = sp.GetRequiredService<ICakeEnvironment>();

            var rid = vcpkgConfiguration.Rid
                .Match<string>(
                    _ => cakeEnvironment.Platform.Rid(),
                    configRid => configRid.Value);

            var runtimeInfo = runtimeConfig.Runtimes.Single(r => string.Equals(r.Rid, rid, StringComparison.Ordinal));

            return new RuntimeProfile(runtimeInfo, systemArtefactsConfig);
        });

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

        // Single manifest.json load — schema v2.1 merges runtimes + system_exclusions
        // + library_manifests + package_families. Loaded via IPathService + ICakeContext
        // (both Host-tier) once per invocation.
        services.AddSingleton<ManifestConfig>(provider =>
        {
            var ctx = provider.GetRequiredService<ICakeContext>();
            var pathService = provider.GetRequiredService<IPathService>();

            var manifestFile = pathService.GetManifestFile();
            return ctx.ToJson<ManifestConfig>(manifestFile);
        });

        services.AddSingleton<RuntimeConfig>(provider =>
        {
            var manifest = provider.GetRequiredService<ManifestConfig>();

            return manifest.Runtimes.Count == 0
                ? throw new InvalidOperationException("manifest.json requires a non-empty runtimes section.")
                : new RuntimeConfig { Runtimes = manifest.Runtimes };
        });

        services.AddSingleton<SystemArtefactsConfig>(provider =>
        {
            var manifest = provider.GetRequiredService<ManifestConfig>();

            return manifest.SystemExclusions;
        });

        return services;
    }
}
