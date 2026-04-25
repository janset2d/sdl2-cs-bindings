using System.Globalization;
using Build.Application.Harvesting;
using Build.Application.Preflight;
using Build.Application.Vcpkg;
using Build.Application.Versioning;
using Build.Context;
using Build.Context.Models;
using Build.Domain.Harvesting.Models;
using Build.Domain.Packaging.Models;
using Build.Domain.Paths;
using Build.Domain.Preflight.Models;
using Build.Domain.Runtime;
using Cake.Core;
using Cake.Core.Diagnostics;
using NuGet.Versioning;

namespace Build.Application.Packaging;

/// <summary>
/// Composes the local-dev pipeline for the <see cref="ArtifactProfile.Local"/> profile:
/// resolves a <c>local.*</c>-suffixed version mapping via <see cref="ManifestVersionProvider"/>,
/// runs <c>Preflight</c> → <c>EnsureVcpkg</c> → <c>Harvest</c> → <c>ConsolidateHarvest</c>
/// → <c>Pack</c>, and hands the resolved mapping to <see cref="IArtifactSourceResolver"/>
/// for verify + consumer-override stamping. Non-local profiles delegate straight to the
/// resolver, which currently reports those profiles as unsupported.
/// </summary>
/// <remarks>
/// Native C++ smoke validation is deliberately <b>not</b> part of this composition — it
/// requires CMake + a platform C/C++ toolchain (MSVC Developer shell on Windows), which
/// is orthogonal to feed materialization. Devs iterating on managed bindings should not
/// need a native toolchain to run <c>SetupLocalDev --source=local</c>. Per-RID native
/// payload validation stays with the dedicated <c>NativeSmoke</c> target and the CI
/// harvest matrix.
/// </remarks>
public sealed class SetupLocalDevTaskRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig,
    IRuntimeProfile runtimeProfile,
    IArtifactSourceResolver artifactSourceResolver,
    PreflightTaskRunner preflightTaskRunner,
    EnsureVcpkgDependenciesTaskRunner ensureVcpkgDependenciesTaskRunner,
    HarvestTaskRunner harvestTaskRunner,
    ConsolidateHarvestTaskRunner consolidateHarvestTaskRunner,
    IPackageTaskRunner packageTaskRunner)
{
    private static readonly IReadOnlyDictionary<string, NuGetVersion> EmptyVersions =
        new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly IArtifactSourceResolver _artifactSourceResolver = artifactSourceResolver ?? throw new ArgumentNullException(nameof(artifactSourceResolver));
    private readonly PreflightTaskRunner _preflightTaskRunner = preflightTaskRunner ?? throw new ArgumentNullException(nameof(preflightTaskRunner));
    private readonly EnsureVcpkgDependenciesTaskRunner _ensureVcpkgDependenciesTaskRunner = ensureVcpkgDependenciesTaskRunner ?? throw new ArgumentNullException(nameof(ensureVcpkgDependenciesTaskRunner));
    private readonly HarvestTaskRunner _harvestTaskRunner = harvestTaskRunner ?? throw new ArgumentNullException(nameof(harvestTaskRunner));
    private readonly ConsolidateHarvestTaskRunner _consolidateHarvestTaskRunner = consolidateHarvestTaskRunner ?? throw new ArgumentNullException(nameof(consolidateHarvestTaskRunner));
    private readonly IPackageTaskRunner _packageTaskRunner = packageTaskRunner ?? throw new ArgumentNullException(nameof(packageTaskRunner));

    public async Task RunAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        _log.Information("SetupLocalDev started with source profile '{0}'.", _artifactSourceResolver.Profile);

        if (_artifactSourceResolver.Profile != ArtifactProfile.Local)
        {
            // Non-local profiles route to the resolver directly; the resolver owns the
            // profile-specific error surface.
            await _artifactSourceResolver.PrepareFeedAsync(context, EmptyVersions, cancellationToken);
            await _artifactSourceResolver.WriteConsumerOverrideAsync(context, EmptyVersions, cancellationToken);
            return;
        }

        var mapping = await ResolveLocalMappingAsync(cancellationToken);
        var harvestLibraries = context.Vcpkg.Libraries.ToList();

        // Emit the same versions.json shape that ResolveVersionsTaskRunner writes in CI
        // so that smoke-witness and PackageConsumerSmoke can read versions after SetupLocalDev.
        await WriteVersionsJsonAsync(mapping);

        await _preflightTaskRunner.RunAsync(context, new PreflightRequest(mapping), cancellationToken);
        _ensureVcpkgDependenciesTaskRunner.Run(context);
        await _harvestTaskRunner.RunAsync(context, new HarvestRequest(_runtimeProfile.Rid, harvestLibraries), cancellationToken);
        await _consolidateHarvestTaskRunner.RunAsync(context, new ConsolidateHarvestRequest(), cancellationToken);
        await _packageTaskRunner.RunAsync(context, new PackRequest(mapping), cancellationToken);

        await _artifactSourceResolver.PrepareFeedAsync(context, mapping, cancellationToken);
        await _artifactSourceResolver.WriteConsumerOverrideAsync(context, mapping, cancellationToken);

        _log.Information("SetupLocalDev completed. Smoke local override and package feed are ready.");
    }

    private async Task<IReadOnlyDictionary<string, NuGetVersion>> ResolveLocalMappingAsync(CancellationToken cancellationToken)
    {
        var concreteFamilies = _manifestConfig.PackageFamilies
            .Where(family => !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject))
            .ToList();

        if (concreteFamilies.Count == 0)
        {
            throw new CakeException(
                "SetupLocalDev could not find any concrete package family (managed_project + native_project) in manifest package_families[].");
        }

        var timestampToken = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
        var suffix = string.Create(CultureInfo.InvariantCulture, $"local.{timestampToken}");
        var provider = new ManifestVersionProvider(_manifestConfig, suffix);

        var scope = concreteFamilies
            .Select(family => family.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return await provider.ResolveAsync(scope, cancellationToken);
    }

    private async Task WriteVersionsJsonAsync(IReadOnlyDictionary<string, NuGetVersion> mapping)
    {
        var serializable = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (family, version) in mapping)
        {
            serializable[family] = version.ToNormalizedString();
        }

        var outputFile = _pathService.GetResolveVersionsOutputFile();
        await _cakeContext.WriteJsonAsync(outputFile, serializable);

        _log.Information(
            "SetupLocalDev wrote {0} family/version entries to {1}.",
            serializable.Count,
            outputFile.FullPath);
    }
}
