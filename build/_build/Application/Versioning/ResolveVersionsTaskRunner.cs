using System.Text.Json;
using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Paths;
using Build.Domain.Preflight;
using Build.Domain.Versioning;
using Cake.Core;
using Cake.Core.Diagnostics;
using NuGet.Versioning;

namespace Build.Application.Versioning;

/// <summary>
/// Runner for the <c>ResolveVersions</c> target.
/// It resolves a single family/version mapping inside the build host, writes that
/// mapping to disk as flat JSON, and lets downstream jobs consume the same file
/// instead of re-resolving versions independently.
/// <para>
/// Supported sources are <c>manifest</c>, <c>git-tag</c>, and <c>meta-tag</c>.
/// <c>--explicit-version</c> remains a direct stage-target input and does not flow
/// through this runner.
/// </para>
/// </summary>
public sealed class ResolveVersionsTaskRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig,
    VersioningConfiguration versioningConfiguration,
    IUpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly VersioningConfiguration _versioningConfiguration = versioningConfiguration ?? throw new ArgumentNullException(nameof(versioningConfiguration));
    private readonly IUpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator = upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var source = _versioningConfiguration.VersionSource;
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new CakeException(
                "ResolveVersions requires --version-source. Allowed values: manifest | git-tag | meta-tag. " +
                "For operator-supplied mappings, pass --explicit-version directly on stage targets.");
        }

        var scope = BuildScope(_versioningConfiguration.Scope);

        var mapping = source.ToLowerInvariant() switch
        {
            "manifest" => await ResolveFromManifestAsync(scope, cancellationToken),
            "git-tag" => await ResolveFromGitTagAsync(scope, cancellationToken),
            "meta-tag" => await ResolveFromMetaTagAsync(scope, cancellationToken),
            "explicit" => throw new CakeException(
                "ResolveVersions --version-source=explicit is not supported. Stage targets consume " +
                "--explicit-version directly; ResolveVersions exists to emit a mapping that CI downstream " +
                "jobs feed back in via --explicit-version."),
            _ => throw new CakeException(
                $"ResolveVersions --version-source='{source}' is not recognized. Allowed values: manifest | git-tag | meta-tag."),
        };

        await WriteMappingAsync(mapping, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, NuGetVersion>> ResolveFromManifestAsync(
        HashSet<string> scope,
        CancellationToken cancellationToken)
    {
        var suffix = _versioningConfiguration.Suffix;
        if (string.IsNullOrWhiteSpace(suffix))
        {
            throw new CakeException(
                "ResolveVersions --version-source=manifest requires --suffix. Example: " +
                "--suffix=ci.$GITHUB_RUN_ID or --suffix=local.$(date -u +%Y%m%dT%H%M%SZ).");
        }

        var provider = new ManifestVersionProvider(_manifestConfig, suffix);
        return await provider.ResolveAsync(scope, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, NuGetVersion>> ResolveFromGitTagAsync(
        HashSet<string> scope,
        CancellationToken cancellationToken)
    {
        if (scope.Count != 1)
        {
            throw new CakeException(
                "ResolveVersions --version-source=git-tag requires exactly one --scope <family>. " +
                "Targeted release is single-family by construction; for multi-family coordinated " +
                "releases use --version-source=meta-tag.");
        }

        var familyId = scope.First();
        var provider = new GitTagVersionProvider(
            _manifestConfig,
            _upstreamVersionAlignmentValidator,
            _cakeContext,
            _pathService.RepoRoot,
            new GitTagScope.Targeted(familyId));

        return await provider.ResolveAsync(scope, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, NuGetVersion>> ResolveFromMetaTagAsync(
        HashSet<string> scope,
        CancellationToken cancellationToken)
    {
        var provider = new GitTagVersionProvider(
            _manifestConfig,
            _upstreamVersionAlignmentValidator,
            _cakeContext,
            _pathService.RepoRoot,
            new GitTagScope.Train());

        return await provider.ResolveAsync(scope, cancellationToken);
    }

    private async Task WriteMappingAsync(IReadOnlyDictionary<string, NuGetVersion> mapping, CancellationToken cancellationToken)
    {
        // Flat JSON shape: {family-id: semver-string}. Sort keys for deterministic output
        // and write NuGet-normalized version strings so downstream consumers can round-trip
        // through NuGetVersion.Parse.
        var serializable = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (family, version) in mapping)
        {
            serializable[family] = version.ToNormalizedString();
        }

        var outputFile = _pathService.GetResolveVersionsOutputFile();
        await _cakeContext.WriteJsonAsync(outputFile, serializable);

        cancellationToken.ThrowIfCancellationRequested();

        var inlineJson = _cakeContext.SerializeJson(serializable);
        _log.Information("ResolveVersions wrote {0} family/version entries to {1}.", serializable.Count, outputFile.FullPath);
        _log.Information("{0}", inlineJson);
    }

    private static HashSet<string> BuildScope(IReadOnlyList<string> rawScope)
    {
        var scope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rawScope.Count == 0)
        {
            return scope;
        }

        foreach (var entry in rawScope)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            scope.Add(entry.Trim());
        }

        return scope;
    }
}
