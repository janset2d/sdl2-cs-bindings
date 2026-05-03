using System.Diagnostics.CodeAnalysis;
using Build.Features.Preflight;
using Build.Host.Paths;
using Build.Integrations.NuGet;
using Build.Shared.Manifest;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Features.Publishing;

public sealed class PublishPipeline(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig,
    INuGetFeedClient feedClient)
{
    // local.<timestamp> is the suffix shape ResolveVersions stamps onto local-pack versions
    // when invoked with --suffix=local.<ts>. Refusing to push these prevents an operator
    // stumbling --target PublishStaging after a local pack and shipping ephemeral local
    // builds to the staging feed.
    private const string LocalSuffixPrefix = "local.";

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly INuGetFeedClient _feedClient = feedClient ?? throw new ArgumentNullException(nameof(feedClient));

    [SuppressMessage("Major Code Smell", "S3267:Loops should be simplified with LINQ expressions",
        Justification = "Per-family side effects: cancellation, two awaited pushes, structured logging.")]
    public async Task RunAsync(PublishRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateRequest(request);

        var concreteFamilies = ResolveConcreteFamiliesInScope(request.Versions);

        foreach (var family in concreteFamilies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var version = request.Versions[family.Name];
            EnsureNotLocalSuffix(family.Name, version);

            var managedPackageId = FamilyIdentifierConventions.ManagedPackageId(family.Name);
            var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

            var managedNupkg = ResolveAndEnsureNupkg(_pathService.PackagesOutput, managedPackageId, version);
            var nativeNupkg = ResolveAndEnsureNupkg(_pathService.PackagesOutput, nativePackageId, version);

            _log.Information(
                "PublishPipeline pushing '{0}' = {1} ({2} + {3}).",
                family.Name,
                version.ToNormalizedString(),
                managedPackageId,
                nativePackageId);

            await _feedClient.PushAsync(request.FeedUrl, request.AuthToken, managedNupkg, cancellationToken);
            await _feedClient.PushAsync(request.FeedUrl, request.AuthToken, nativeNupkg, cancellationToken);
        }

        _log.Information(
            "PublishPipeline pushed {0} family/families to '{1}'.",
            concreteFamilies.Count,
            request.FeedUrl);
    }

    private static void ValidateRequest(PublishRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FeedUrl))
        {
            throw new CakeException("PublishPipeline requires a non-empty FeedUrl. Wire it through the task wrapper (e.g. PublishStagingTask).");
        }

        if (string.IsNullOrWhiteSpace(request.AuthToken))
        {
            throw new CakeException("PublishPipeline requires a non-empty AuthToken. Set GH_TOKEN (or GITHUB_TOKEN) before invoking PublishStaging.");
        }

        if (request.Versions.Count == 0)
        {
            throw new CakeException("PublishPipeline requires at least one --explicit-version entry. Push scope is defined by the version mapping.");
        }
    }

    private List<PackageFamilyConfig> ResolveConcreteFamiliesInScope(IReadOnlyDictionary<string, NuGetVersion> versions)
    {
        var selected = new List<PackageFamilyConfig>(versions.Count);
        foreach (var familyName in versions.Keys)
        {
            var family = _manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, familyName, StringComparison.OrdinalIgnoreCase))
                ?? throw new CakeException(
                    $"PublishPipeline received unknown family '{familyName}'. Add it to manifest.json package_families[] or fix the explicit-version mapping.");

            if (string.IsNullOrWhiteSpace(family.ManagedProject) || string.IsNullOrWhiteSpace(family.NativeProject))
            {
                throw new CakeException(
                    $"PublishPipeline cannot publish family '{family.Name}' because manifest.json does not declare both managed_project and native_project. Placeholder families cannot be pushed.");
            }

            selected.Add(family);
        }

        return selected;
    }

    private static void EnsureNotLocalSuffix(string familyName, NuGetVersion version)
    {
        if (version.IsPrerelease && version.Release.StartsWith(LocalSuffixPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException(
                $"PublishPipeline refused to push '{familyName}' at version '{version.ToNormalizedString()}': prerelease label starts with 'local.'. " +
                "Local-pack output (--source=local) must never reach the staging feed. Run a fresh CI pack or re-pack with a non-local --explicit-version suffix.");
        }
    }

    private FilePath ResolveAndEnsureNupkg(DirectoryPath packagesDir, string packageId, NuGetVersion version)
    {
        var nupkgFile = packagesDir.CombineWithFilePath($"{packageId}.{version.ToNormalizedString()}.nupkg");
        if (!_cakeContext.FileExists(nupkgFile))
        {
            throw new CakeException(
                $"PublishPipeline expected '{nupkgFile.GetFilename().FullPath}' in '{packagesDir.FullPath}' but it was not found. " +
                "Run --target Package first or fix the --explicit-version / --versions-file mapping.");
        }

        return nupkgFile;
    }
}
