using System.Diagnostics.CodeAnalysis;
using Build.Context;
using Build.Context.Models;
using Build.Domain.Paths;
using Build.Domain.Preflight;
using Build.Domain.Publishing.Models;
using Build.Infrastructure.DotNet;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Application.Publishing;

public sealed class PublishTaskRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig,
    INuGetFeedClient feedClient)
{
    // local.<timestamp> is the suffix shape SetupLocalDevTaskRunner stamps onto local-pack
    // versions. Refusing to push these prevents an operator stumbling --target PublishStaging
    // after --source=local and shipping ephemeral local builds to the staging feed.
    private const string LocalSuffixPrefix = "local.";

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly INuGetFeedClient _feedClient = feedClient ?? throw new ArgumentNullException(nameof(feedClient));

    [SuppressMessage("Major Code Smell", "S3267:Loops should be simplified with LINQ expressions",
        Justification = "Per-family side effects: cancellation, two awaited pushes, structured logging.")]
    public async Task RunAsync(BuildContext context, PublishRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
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
                "PublishTaskRunner pushing '{0}' = {1} ({2} + {3}).",
                family.Name,
                version.ToNormalizedString(),
                managedPackageId,
                nativePackageId);

            await _feedClient.PushAsync(request.FeedUrl, request.AuthToken, managedNupkg, cancellationToken);
            await _feedClient.PushAsync(request.FeedUrl, request.AuthToken, nativeNupkg, cancellationToken);
        }

        _log.Information(
            "PublishTaskRunner pushed {0} family/families to '{1}'.",
            concreteFamilies.Count,
            request.FeedUrl);
    }

    private static void ValidateRequest(PublishRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FeedUrl))
        {
            throw new CakeException("PublishTaskRunner requires a non-empty FeedUrl. Wire it through the task wrapper (e.g. PublishStagingTask).");
        }

        if (string.IsNullOrWhiteSpace(request.AuthToken))
        {
            throw new CakeException("PublishTaskRunner requires a non-empty AuthToken. Set GH_TOKEN (or GITHUB_TOKEN) before invoking PublishStaging.");
        }

        if (request.Versions.Count == 0)
        {
            throw new CakeException("PublishTaskRunner requires at least one --explicit-version entry. Push scope is defined by the version mapping.");
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
                    $"PublishTaskRunner received unknown family '{familyName}'. Add it to manifest.json package_families[] or fix the explicit-version mapping.");

            if (string.IsNullOrWhiteSpace(family.ManagedProject) || string.IsNullOrWhiteSpace(family.NativeProject))
            {
                throw new CakeException(
                    $"PublishTaskRunner cannot publish family '{family.Name}' because manifest.json does not declare both managed_project and native_project. Placeholder families cannot be pushed.");
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
                $"PublishTaskRunner refused to push '{familyName}' at version '{version.ToNormalizedString()}': prerelease label starts with 'local.'. " +
                "Local-pack output (--source=local) must never reach the staging feed. Run a fresh CI pack or re-pack with a non-local --explicit-version suffix.");
        }
    }

    private FilePath ResolveAndEnsureNupkg(DirectoryPath packagesDir, string packageId, NuGetVersion version)
    {
        var nupkgFile = packagesDir.CombineWithFilePath($"{packageId}.{version.ToNormalizedString()}.nupkg");
        if (!_cakeContext.FileExists(nupkgFile))
        {
            throw new CakeException(
                $"PublishTaskRunner expected '{nupkgFile.GetFilename().FullPath}' in '{packagesDir.FullPath}' but it was not found. " +
                "Run --target Package first or fix the --explicit-version / --versions-file mapping.");
        }

        return nupkgFile;
    }
}
