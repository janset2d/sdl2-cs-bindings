using System.Globalization;
using Build.Features.Harvesting;
using Build.Features.Packaging;
using Build.Features.Packaging.ArtifactSourceResolvers;
using Build.Features.Preflight;
using Build.Features.Vcpkg;
using Build.Features.Versioning;
using Build.Host;
using Build.Host.Paths;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Cake.Core;
using Cake.Core.Diagnostics;
using NuGet.Versioning;

namespace Build.Features.LocalDev;

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
public sealed class SetupLocalDevFlow(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig,
    IRuntimeProfile runtimeProfile,
    IArtifactSourceResolver artifactSourceResolver,
    PreflightPipeline preflightPipeline,
    EnsureVcpkgDependenciesPipeline ensureVcpkgDependenciesPipeline,
    HarvestPipeline harvestPipeline,
    ConsolidateHarvestPipeline consolidateHarvestPipeline,
    IPackagePipeline packagePipeline)
{
    private static readonly IReadOnlyDictionary<string, NuGetVersion> EmptyVersions =
        new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly IArtifactSourceResolver _artifactSourceResolver = artifactSourceResolver ?? throw new ArgumentNullException(nameof(artifactSourceResolver));
    private readonly PreflightPipeline _preflightPipeline = preflightPipeline ?? throw new ArgumentNullException(nameof(preflightPipeline));
    private readonly EnsureVcpkgDependenciesPipeline _ensureVcpkgDependenciesPipeline = ensureVcpkgDependenciesPipeline ?? throw new ArgumentNullException(nameof(ensureVcpkgDependenciesPipeline));
    private readonly HarvestPipeline _harvestPipeline = harvestPipeline ?? throw new ArgumentNullException(nameof(harvestPipeline));
    private readonly ConsolidateHarvestPipeline _consolidateHarvestPipeline = consolidateHarvestPipeline ?? throw new ArgumentNullException(nameof(consolidateHarvestPipeline));
    private readonly IPackagePipeline _packagePipeline = packagePipeline ?? throw new ArgumentNullException(nameof(packagePipeline));

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
        var harvestLibraries = context.Options.Vcpkg.Libraries.ToList();

        // Emit the same versions.json shape that ResolveVersionsPipeline writes in CI
        // so that smoke-witness and PackageConsumerSmoke can read versions after SetupLocalDev.
        await VersionsFileWriter.WriteAsync(_cakeContext, _pathService, mapping);
        _log.Information(
            "SetupLocalDev wrote {0} family/version entries to {1}.",
            mapping.Count,
            _pathService.GetResolveVersionsOutputFile().FullPath);

        await _preflightPipeline.RunAsync(context, new PreflightRequest(mapping), cancellationToken);
        _ensureVcpkgDependenciesPipeline.Run(context);
        await _harvestPipeline.RunAsync(context, new HarvestRequest(_runtimeProfile.Rid, harvestLibraries), cancellationToken);
        await _consolidateHarvestPipeline.RunAsync(context, new ConsolidateHarvestRequest(), cancellationToken);
        await _packagePipeline.RunAsync(context, new PackRequest(mapping), cancellationToken);

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

}
