using System.Diagnostics.CodeAnalysis;
using Build.Features.LocalDev;
using Build.Features.Preflight;
using Build.Host;
using Build.Host.Paths;
using Build.Integrations.NuGet;
using Build.Shared.Manifest;
using Cake.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Features.Packaging.ArtifactSourceResolvers;

/// <summary>
/// PD-5 read path: pulls latest published nupkgs from the GitHub Packages internal
/// feed for <c>SetupLocalDev --source=remote</c>. Stateful between
/// <see cref="PrepareFeedAsync"/> and <see cref="WriteConsumerOverrideAsync"/> —
/// <see cref="SetupLocalDevFlow"/> hands an empty version mapping for
/// non-Local profiles, so the resolver discovers internally and caches the result
/// for the override-write companion call.
/// </summary>
[SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded",
    Justification = "Internal feed URL is part of the release-lifecycle contract, not operator-tunable.")]
public sealed class RemoteArtifactSourceResolver(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig,
    INuGetFeedClient feedClient) : IArtifactSourceResolver
{
    private const string GitHubPackagesFeedUrl = "https://nuget.pkg.github.com/janset2d/index.json";

    // GH_TOKEN matches the gh CLI convention; release.yml maps secrets.GITHUB_TOKEN
    // into GH_TOKEN. GITHUB_TOKEN is a passive fallback for environments that
    // already export the GitHub Actions default name.
    private static readonly string[] AuthEnvVarChain = ["GH_TOKEN", "GITHUB_TOKEN"];

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly INuGetFeedClient _feedClient = feedClient ?? throw new ArgumentNullException(nameof(feedClient));

    private IReadOnlyDictionary<string, NuGetVersion>? _resolvedVersions;

    public ArtifactProfile Profile => ArtifactProfile.RemoteInternal;

    public DirectoryPath LocalFeedPath => _pathService.PackagesOutput;

    [SuppressMessage("Major Code Smell", "S3267:Loops should be simplified with LINQ expressions",
        Justification = "Per-family side effects: cancellation, two awaited feed lookups, two awaited downloads, dictionary populate.")]
    public async Task PrepareFeedAsync(
        BuildContext context,
        IReadOnlyDictionary<string, NuGetVersion> versions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(versions);
        cancellationToken.ThrowIfCancellationRequested();

        var authToken = ResolveAuthToken();
        var concreteFamilies = ResolveConcreteFamilies();

        // Wipe so a prior --source=local pack's nupkgs do not sit alongside what we
        // just pulled (the feed dir is shared between profiles by design).
        WipeLocalFeed();
        _cakeContext.EnsureDirectoryExists(_pathService.PackagesOutput);

        var resolved = new SortedDictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in concreteFamilies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var managedPackageId = FamilyIdentifierConventions.ManagedPackageId(family.Name);
            var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

            var managedVersion = await _feedClient.GetLatestVersionAsync(
                GitHubPackagesFeedUrl, authToken, managedPackageId, includePrerelease: true, cancellationToken)
                ?? throw new CakeException(
                    $"RemoteArtifactSourceResolver could not find any published version of '{managedPackageId}' on the internal feed ({GitHubPackagesFeedUrl}). " +
                    "Either publish the family via '--target PublishStaging' first, or fall back to '--source=local' for this run.");

            var nativeVersion = await _feedClient.GetLatestVersionAsync(
                GitHubPackagesFeedUrl, authToken, nativePackageId, includePrerelease: true, cancellationToken)
                ?? throw new CakeException(
                    $"RemoteArtifactSourceResolver could not find any published version of '{nativePackageId}' on the internal feed ({GitHubPackagesFeedUrl}). " +
                    $"Managed '{managedPackageId}' is at {managedVersion.ToNormalizedString()} but the native is missing — release them as a coherent family.");

            // ADR-001 D-3seg: managed and native always release at the same version.
            if (!VersionComparer.Default.Equals(managedVersion, nativeVersion))
            {
                throw new CakeException(
                    $"RemoteArtifactSourceResolver detected a family-version invariant violation for '{family.Name}': " +
                    $"managed '{managedPackageId}' is at {managedVersion.ToNormalizedString()}, native '{nativePackageId}' is at {nativeVersion.ToNormalizedString()}. " +
                    "Repair the internal feed before re-running '--source=remote'.");
            }

            _log.Information("RemoteArtifactSourceResolver discovered '{0}' = {1}.", family.Name, managedVersion.ToNormalizedString());

            await _feedClient.DownloadAsync(GitHubPackagesFeedUrl, authToken, managedPackageId, managedVersion, _pathService.PackagesOutput, cancellationToken);
            await _feedClient.DownloadAsync(GitHubPackagesFeedUrl, authToken, nativePackageId, nativeVersion, _pathService.PackagesOutput, cancellationToken);

            resolved[family.Name] = managedVersion;
        }

        _resolvedVersions = resolved;

        // Parity with the Local profile: emit versions.json so witness scripts and
        // standalone PackageConsumerSmoke invocations can route through --versions-file.
        await VersionsFileWriter.WriteAsync(_cakeContext, _pathService, resolved);

        _log.Information(
            "RemoteArtifactSourceResolver pulled {0} family/families from '{1}' into '{2}'.",
            resolved.Count,
            GitHubPackagesFeedUrl,
            _pathService.PackagesOutput.FullPath);
    }

    public async Task WriteConsumerOverrideAsync(
        BuildContext context,
        IReadOnlyDictionary<string, NuGetVersion> versions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(versions);
        cancellationToken.ThrowIfCancellationRequested();

        // Caller-supplied mapping wins; otherwise fall back to the cache PrepareFeedAsync populated.
        var effectiveVersions = versions.Count > 0
            ? versions
            : _resolvedVersions
              ?? throw new CakeException(
                  "RemoteArtifactSourceResolver.WriteConsumerOverrideAsync was called before PrepareFeedAsync populated " +
                  "the version mapping. Run PrepareFeedAsync first or pass a non-empty mapping explicitly.");

        await JansetLocalPropsWriter.WriteAsync(_cakeContext, _pathService, LocalFeedPath, effectiveVersions);

        _log.Information("RemoteArtifactSourceResolver wrote local override: {0}", _pathService.GetLocalPropsFile().FullPath);
        _log.Information("RemoteArtifactSourceResolver local feed path: {0}", LocalFeedPath.FullPath);
    }

    private string ResolveAuthToken()
    {
        foreach (var envVar in AuthEnvVarChain)
        {
            var value = _cakeContext.EnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new CakeException(
            $"RemoteArtifactSourceResolver requires a GitHub Packages auth token. Set one of: {string.Join(", ", AuthEnvVarChain)}. " +
            "Local dev: 'gh auth token' produces a usable value (a PAT with read:packages scope works too). " +
            "CI: release.yml maps ${{ secrets.GITHUB_TOKEN }} into GH_TOKEN automatically.");
    }

    private List<PackageFamilyConfig> ResolveConcreteFamilies()
    {
        var concreteFamilies = _manifestConfig.PackageFamilies
            .Where(family => !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject))
            .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (concreteFamilies.Count == 0)
        {
            throw new CakeException(
                "RemoteArtifactSourceResolver could not find any concrete package family (both managed_project and native_project non-null) in manifest.json.");
        }

        return concreteFamilies;
    }

    private void WipeLocalFeed()
    {
        if (!_cakeContext.DirectoryExists(_pathService.PackagesOutput))
        {
            return;
        }

        _log.Verbose("RemoteArtifactSourceResolver wiping local feed dir: {0}", _pathService.PackagesOutput.FullPath);
        _cakeContext.DeleteDirectory(_pathService.PackagesOutput, new DeleteDirectorySettings { Recursive = true, Force = true });
    }
}
