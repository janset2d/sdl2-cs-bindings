using System.Diagnostics.CodeAnalysis;
using Build.Data.Versions;
using Build.Host;
using Build.Host.Paths;
using Build.Targets.PublishStaging.Services;
using Build.Manifest;
using Build.Validation.Conventions;
using Build.Versioning;
using Cake.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using NuGet.Versioning;

namespace Build.Targets.PublishStaging;

/// <summary>
/// Pushes a packed family pair (managed + native nupkg) to the GitHub Packages staging
/// feed. Re-inlined orchestration per ADR §5 — pre-migration <c>PublishPipeline</c> was a
/// thin wrapper around <see cref="INuGetFeedClient"/> with manifest scope filter and
/// local-suffix guard; folding it into the task surface clarifies the operator-visible
/// flow without losing testability (V2 scenarios consume <c>INuGetFeedClient</c> as a
/// substitute).
/// </summary>
[TaskName("PublishStaging")]
[TaskDescription("Pushes packed nupkgs to the GitHub Packages staging feed.")]
[SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded",
    Justification = "Internal feed URL is part of the release-lifecycle contract, not operator-tunable.")]
public sealed class PublishStagingTask : AsyncFrostingTask<BuildContext>
{
    private const string GitHubPackagesFeedUrl = "https://nuget.pkg.github.com/janset2d/index.json";
    private const string LocalSuffixPrefix = "local.";

    private static readonly string[] AuthEnvVarChain = ["GH_TOKEN", "GITHUB_TOKEN"];

    private readonly ICakeContext _cakeContext;
    private readonly ICakeLog _log;
    private readonly INuGetFeedClient _feedClient;
    private readonly IPathService _pathService;
    private readonly ManifestConfig _manifestConfig;
    private readonly IVersionFileRepository _versionFileRepository;

    public PublishStagingTask(
        ICakeContext cakeContext,
        ICakeLog log,
        INuGetFeedClient feedClient,
        IPathService pathService,
        ManifestConfig manifestConfig,
        IVersionFileRepository versionFileRepository)
    {
        _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _feedClient = feedClient ?? throw new ArgumentNullException(nameof(feedClient));
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
        _versionFileRepository = versionFileRepository ?? throw new ArgumentNullException(nameof(versionFileRepository));
    }

    [SuppressMessage("Major Code Smell", "S3267:Loops should be simplified with LINQ expressions",
        Justification = "Per-family side effects: two awaited pushes, structured logging.")]
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.VersionsFilePath is null)
        {
            throw new CakeException(
                "PublishStaging requires --versions-file <path>. " +
                "Run --target ResolveVersions first to produce a versions.json, " +
                "then re-run with --versions-file artifacts/resolve-versions/versions.json.");
        }

        var familyVersions = _versionFileRepository.Load(context.VersionsFilePath);
        if (familyVersions.Count == 0)
        {
            throw new CakeException(
                "PublishStaging requires a non-empty version mapping. " +
                "Re-run --target ResolveVersionsFromManifest or --target ResolveVersionsFromExplicit.");
        }

        var authToken = ResolveAuthToken();
        var concreteFamilies = ResolveConcreteFamiliesInScope(familyVersions);

        foreach (var family in concreteFamilies)
        {
            var version = familyVersions.RequireVersion(new PackageFamilyId(family.Name));
            EnsureNotLocalSuffix(family.Name, version);

            var managedPackageId = FamilyIdentifierConventions.ManagedPackageId(family.Name);
            var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

            var managedNupkg = ResolveAndEnsureNupkg(_pathService.PackagesOutput, managedPackageId, version);
            var nativeNupkg = ResolveAndEnsureNupkg(_pathService.PackagesOutput, nativePackageId, version);

            _log.Information(
                "PublishStaging pushing '{0}' = {1} ({2} + {3}).",
                family.Name,
                version.ToNormalizedString(),
                managedPackageId,
                nativePackageId);

            await _feedClient.PushAsync(GitHubPackagesFeedUrl, authToken, managedNupkg, CancellationToken.None);
            await _feedClient.PushAsync(GitHubPackagesFeedUrl, authToken, nativeNupkg, CancellationToken.None);
        }

        _log.Information("PublishStaging pushed {0} family/families to '{1}'.", concreteFamilies.Count, GitHubPackagesFeedUrl);
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
            $"PublishStaging requires a GitHub Packages auth token. Set one of: {string.Join(", ", AuthEnvVarChain)}. " +
            "CI: release.yml maps secrets.GITHUB_TOKEN into GH_TOKEN automatically. " +
            "Local escape hatch: 'gh auth token' produces a usable value (PAT with write:packages scope works too).");
    }

    private List<PackageFamilyConfig> ResolveConcreteFamiliesInScope(PackageFamilyVersionSet versions)
    {
        var selected = new List<PackageFamilyConfig>(versions.Count);
        foreach (var entry in versions)
        {
            var familyName = entry.Family.Value;
            var family = _manifestConfig.PackageFamilies.SingleOrDefault(c =>
                string.Equals(c.Name, familyName, StringComparison.OrdinalIgnoreCase))
                ?? throw new CakeException(
                    $"PublishStaging received unknown family '{familyName}'. Add it to manifest.json package_families[] or fix the explicit-version mapping.");

            if (string.IsNullOrWhiteSpace(family.ManagedProject) || string.IsNullOrWhiteSpace(family.NativeProject))
            {
                throw new CakeException(
                    $"PublishStaging cannot publish family '{family.Name}': manifest.json does not declare both managed_project and native_project. Placeholder families cannot be pushed.");
            }

            selected.Add(family);
        }
        return selected;
    }

    private static void EnsureNotLocalSuffix(string familyName, NuGetVersion version)
    {
        // local.<timestamp> is the suffix shape ResolveVersions stamps onto local-pack
        // versions. Refusing to push these prevents an operator stumbling --target
        // PublishStaging after a local pack and shipping ephemeral local builds to the
        // staging feed.
        if (version.IsPrerelease && version.Release.StartsWith(LocalSuffixPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException(
                $"PublishStaging refused to push '{familyName}' at version '{version.ToNormalizedString()}': prerelease label starts with 'local.'. " +
                "Local-pack output (--source=local) must never reach the staging feed. Run a fresh CI pack or re-pack with a non-local --explicit-version suffix.");
        }
    }

    private FilePath ResolveAndEnsureNupkg(DirectoryPath packagesDir, string packageId, NuGetVersion version)
    {
        var nupkgFile = packagesDir.CombineWithFilePath($"{packageId}.{version.ToNormalizedString()}.nupkg");
        if (!_cakeContext.FileExists(nupkgFile))
        {
            throw new CakeException(
                $"PublishStaging expected '{nupkgFile.GetFilename().FullPath}' in '{packagesDir.FullPath}' but it was not found. " +
                "Run --target Package first or fix the --explicit-version / --versions-file mapping.");
        }
        return nupkgFile;
    }
}
