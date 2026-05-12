#pragma warning disable MA0045

using Build.Data.Manifest;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Host.Runtime;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Host;

/// <summary>
/// Composition-root grouping for Host-tier services:
/// path resolution and runtime profile. Manifest loading flows through
/// <see cref="IManifestRepository"/> so file-backed data access stays in
/// <c>Data\Manifest</c>; Host does not expose the loaded manifest as ambient state.
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
            var manifest = sp.GetRequiredService<IManifestRepository>().Load();
            var cakeEnvironment = sp.GetRequiredService<ICakeEnvironment>();

            // RID resolution: --rid CLI override wins; otherwise fall back to the host's
            // platform default.
            var rid = string.IsNullOrWhiteSpace(parsedArgs.Rid)
                ? cakeEnvironment.Platform.Rid()
                : parsedArgs.Rid;

            if (manifest.Runtimes.Count == 0)
            {
                throw new InvalidOperationException("manifest.json requires a non-empty runtimes section.");
            }

            var runtimeInfo = manifest.Runtimes.Single(r => string.Equals(r.Rid, rid, StringComparison.Ordinal));

            return new RuntimeProfile(runtimeInfo, manifest.SystemExclusions);
        });

        services.AddSingleton(parsedArgs);

        return services;
    }
}
