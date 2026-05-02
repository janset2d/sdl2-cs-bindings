using System.Diagnostics.CodeAnalysis;
using Build.Features.Preflight;
using Build.Host;
using Build.Host.Paths;
using Build.Shared.Manifest;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Features.Packaging.ArtifactSourceResolvers;

/// <summary>
/// Resolves the local <see cref="ArtifactProfile.Local"/> feed.
/// After <c>SetupLocalDev</c> writes packages to <c>artifacts/packages/</c>, this resolver
/// verifies each family's nupkg and stamps <c>build/msbuild/Janset.Local.props</c> so IDE
/// direct-restore paths can consume the packed set without a Cake invocation.
/// </summary>
public sealed class LocalArtifactSourceResolver(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig) : IArtifactSourceResolver
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));

    public ArtifactProfile Profile => ArtifactProfile.Local;

    public DirectoryPath LocalFeedPath => _pathService.PackagesOutput;

    [SuppressMessage("Major Code Smell", "S3267:Loops should be simplified with LINQ expressions",
        Justification = "The per-family loop carries side effects (cancellation, EnsurePackageExists for managed + native). Forcing LINQ would hide the sequence without shrinking the code.")]
    public Task PrepareFeedAsync(
        BuildContext context,
        IReadOnlyDictionary<string, NuGetVersion> versions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(versions);
        cancellationToken.ThrowIfCancellationRequested();

        if (versions.Count == 0)
        {
            throw new CakeException(
                "LocalArtifactSourceResolver.PrepareFeedAsync was invoked with an empty version mapping. " +
                "The caller (SetupLocalDevFlow in normal flows) must resolve a mapping before handing off.");
        }

        if (!_cakeContext.DirectoryExists(_pathService.PackagesOutput))
        {
            throw new CakeException(
                $"LocalArtifactSourceResolver cannot resolve the local feed because '{_pathService.PackagesOutput.FullPath}' does not exist. " +
                "Run 'SetupLocalDev --source=local --rid <rid>' so the Pack stage can materialise the feed before resolution.");
        }

        foreach (var (familyName, nuGetVersion) in versions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnsureFamilyIsConcrete(familyName);

            var version = nuGetVersion.ToNormalizedString();
            EnsurePackageExists(FamilyIdentifierConventions.ManagedPackageId(familyName), version);
            EnsurePackageExists(FamilyIdentifierConventions.NativePackageId(familyName), version);
        }

        _log.Information(
            "LocalArtifactSourceResolver verified {0} family/families against local feed '{1}'.",
            versions.Count,
            _pathService.PackagesOutput.FullPath);

        return Task.CompletedTask;
    }

    public async Task WriteConsumerOverrideAsync(
        BuildContext context,
        IReadOnlyDictionary<string, NuGetVersion> versions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(versions);
        cancellationToken.ThrowIfCancellationRequested();

        if (versions.Count == 0)
        {
            throw new CakeException(
                "LocalArtifactSourceResolver.WriteConsumerOverrideAsync was invoked with an empty version mapping. " +
                "Run PrepareFeedAsync first (or supply a non-empty mapping from the composing runner).");
        }

        await JansetLocalPropsWriter.WriteAsync(_cakeContext, _pathService, LocalFeedPath, versions);

        _log.Information("LocalArtifactSourceResolver wrote local override: {0}", _pathService.GetLocalPropsFile().FullPath);
        _log.Information("LocalArtifactSourceResolver local feed path: {0}", LocalFeedPath.FullPath);
    }

    private void EnsureFamilyIsConcrete(string familyName)
    {
        var family = _manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, familyName, StringComparison.OrdinalIgnoreCase));

        if (family is null)
        {
            throw new CakeException(
                $"LocalArtifactSourceResolver received unknown family '{familyName}'. Add it to build/manifest.json package_families[] or fix the caller's mapping.");
        }

        if (string.IsNullOrWhiteSpace(family.ManagedProject) || string.IsNullOrWhiteSpace(family.NativeProject))
        {
            throw new CakeException(
                $"LocalArtifactSourceResolver cannot resolve family '{family.Name}' because manifest.json does not declare both managed_project and native_project.");
        }
    }

    private void EnsurePackageExists(string packageId, string version)
    {
        var packagePath = _pathService.GetPackageOutputFile(packageId, version);
        if (_cakeContext.FileExists(packagePath))
        {
            return;
        }

        throw new CakeException(
            $"LocalArtifactSourceResolver expected package '{packagePath.GetFilename().FullPath}' in local feed '{_pathService.PackagesOutput.FullPath}', but it was not found. " +
            "Re-run 'SetupLocalDev --source=local --rid <rid>' so the Pack stage regenerates the feed.");
    }
}
