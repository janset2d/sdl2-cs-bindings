#pragma warning disable MA0045

using Build.Host.Cake;
using Build.Host.Paths;
using Build.Data.Manifest.Models;
using Build.Runtime;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Host;

/// <summary>
/// Composition-root grouping for Host-tier services:
/// path resolution, runtime profile, and manifest-derived singletons. Manifest-derived
/// configs (<see cref="ManifestConfig"/>, <see cref="RuntimeConfig"/>,
/// <see cref="SystemArtefactsConfig"/>) live here because they are loaded once
/// at startup from <c>build/manifest.json</c> via <see cref="IPathService"/> +
/// <see cref="ICakeContext"/>, both Host-tier resolutions. <c>IRuntimeScanner</c> dispatch
/// (host-platform abstraction) lives in the <c>Build.DependencyAnalysis</c> root concept.
/// <para>
/// Takes <see cref="ParsedArguments"/> + the resolved repository root directly because
/// <see cref="IPathService"/> composes its layout from CLI overrides
/// (<c>--vcpkg-dir</c>, <c>--vcpkg-installed-dir</c>) before any DI resolution can happen.
/// </para>
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHostBuildingBlocks(
        this IServiceCollection services,
        ParsedArguments parsedArgs,
        DirectoryPath repoRoot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(parsedArgs);
        ArgumentNullException.ThrowIfNull(repoRoot);

        services.AddSingleton<IPathService>(provider =>
        {
            var cakeLogger = provider.GetRequiredService<ICakeLog>();
            return new PathService(repoRoot, parsedArgs, cakeLogger);
        });

        services.AddSingleton<IRuntimeProfile>(sp =>
        {
            var runtimeConfig = sp.GetRequiredService<RuntimeConfig>();
            var systemArtefactsConfig = sp.GetRequiredService<SystemArtefactsConfig>();
            var cakeEnvironment = sp.GetRequiredService<ICakeEnvironment>();

            // RID resolution: --rid CLI override wins; otherwise fall back to the host's
            // platform default. VcpkgConfiguration retired in S15 (P9) — its OneOf-based
            // Option<string> wrapper around the same value was indirection without payoff.
            var rid = string.IsNullOrWhiteSpace(parsedArgs.Rid)
                ? cakeEnvironment.Platform.Rid()
                : parsedArgs.Rid;

            var runtimeInfo = runtimeConfig.Runtimes.Single(r => string.Equals(r.Rid, rid, StringComparison.Ordinal));

            return new RuntimeProfile(runtimeInfo, systemArtefactsConfig);
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

        services.AddSingleton(parsedArgs);

        return services;
    }
}
