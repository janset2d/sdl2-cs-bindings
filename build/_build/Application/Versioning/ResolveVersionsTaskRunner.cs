using System.Text.Json;
using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Paths;
using Cake.Core;
using Cake.Core.Diagnostics;
using NuGet.Versioning;

namespace Build.Application.Versioning;

/// <summary>
/// Runner for the ADR-003 <c>ResolveVersions</c> target. The build-host version-resolution
/// entrypoint per ADR-003 §3.1 ownership invariant — CI supplies trigger context only, the
/// build host resolves the mapping and writes it to disk as the canonical flat JSON shape for
/// downstream consumers (CI <c>needs:</c> outputs, local inspection).
/// <para>
/// Slice B1 implements the <c>manifest</c> source only. <c>git-tag</c> and <c>meta-tag</c>
/// sources land in Slice C with <c>GitTagVersionProvider</c>. <c>explicit</c> source is a
/// convenience pass-through for the operator's <c>--explicit-version</c> entries; it lands in
/// Slice B1's <c>PackageBuildConfiguration</c> reshape when <c>ExplicitVersions</c> becomes
/// the single source for stage-level version input.
/// </para>
/// </summary>
public sealed class ResolveVersionsTaskRunner(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    ManifestConfig manifestConfig,
    VersioningConfiguration versioningConfiguration)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly VersioningConfiguration _versioningConfiguration = versioningConfiguration ?? throw new ArgumentNullException(nameof(versioningConfiguration));

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var source = _versioningConfiguration.VersionSource;
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new CakeException(
                "ResolveVersions requires --version-source. Allowed values: manifest | explicit | git-tag | meta-tag. " +
                "Slice B1 implements 'manifest'; other sources land in later slices.");
        }

        var scope = BuildScope(_versioningConfiguration.Scope);

        var mapping = source.ToLowerInvariant() switch
        {
            "manifest" => await ResolveFromManifestAsync(scope, cancellationToken),
            "explicit" => throw new CakeException(
                "ResolveVersions --version-source=explicit is scheduled for Slice B1 closure, once " +
                "PackageBuildConfiguration.ExplicitVersions lands. For now, stage targets accept " +
                "--explicit-version directly without going through ResolveVersions."),
            "git-tag" or "meta-tag" => throw new CakeException(
                $"ResolveVersions --version-source={source} lands in Slice C (GitTagVersionProvider)."),
            _ => throw new CakeException(
                $"ResolveVersions --version-source='{source}' is not recognized. Allowed values: manifest | explicit | git-tag | meta-tag."),
        };

        await WriteMappingAsync(mapping, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, NuGetVersion>> ResolveFromManifestAsync(
        IReadOnlySet<string> scope,
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

    private async Task WriteMappingAsync(IReadOnlyDictionary<string, NuGetVersion> mapping, CancellationToken cancellationToken)
    {
        // Canonical flat shape (ADR-003 §3.1, plan §5.3): {family-id: semver-string}.
        // Sorted by family identifier for deterministic output; NuGet-normalized version
        // strings so downstream consumers can round-trip via NuGetVersion.Parse.
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
