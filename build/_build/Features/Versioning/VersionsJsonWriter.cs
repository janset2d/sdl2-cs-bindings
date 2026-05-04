using Build.Host.Cake;
using Build.Host.Paths;
using Cake.Core;
using Cake.Core.Diagnostics;
using NuGet.Versioning;

namespace Build.Features.Versioning;

/// <summary>
/// Writes the resolved family→version mapping to <c>artifacts/resolve-versions/versions.json</c>.
/// Both <c>ResolveVersionsFromManifestTask</c> and <c>ResolveVersionsFromExplicitTask</c> inject
/// this helper to produce the same flat-JSON shape consumed by stage targets via
/// <c>--versions-file</c>.
/// </summary>
public sealed class VersionsJsonWriter(ICakeContext cakeContext, IPathService pathService, ICakeLog log)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    /// <summary>
    /// Serializes <paramref name="mapping"/> as a flat <c>{family-id: semver-string}</c>
    /// JSON object, sorted by key (case-insensitive) with NuGet-normalized version strings,
    /// and writes it to <c>IPathService.GetResolveVersionsOutputFile()</c>.
    /// </summary>
    public async Task WriteAsync(IReadOnlyDictionary<string, NuGetVersion> mapping, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ct.ThrowIfCancellationRequested();

        var serializable = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (family, version) in mapping)
        {
            serializable[family] = version.ToNormalizedString();
        }

        var outputFile = _pathService.GetResolveVersionsOutputFile();
        await _cakeContext.WriteJsonAsync(outputFile, serializable);

        ct.ThrowIfCancellationRequested();

        var inlineJson = _cakeContext.SerializeJson(serializable);
        _log.Information("ResolveVersions wrote {0} family/version entries to {1}.", serializable.Count, outputFile.FullPath);
        _log.Information("{0}", inlineJson);
    }
}
