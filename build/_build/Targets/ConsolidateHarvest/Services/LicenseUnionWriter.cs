using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Build.Harvesting;
using Build.Host.Paths;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Targets.ConsolidateHarvest.Services;

/// <summary>
/// Unions per-RID license trees into a single canonical <c>licenses/_consolidated.tmp/</c>
/// directory for a library. Same byte-content across RIDs collapses to one canonical
/// <c>{package}/{fileName}</c>; divergent content (different copyright text per RID) emits
/// per-RID variants (<c>{package}/{name}.{rid}{ext}</c>) plus a <see cref="DivergentLicense"/>
/// receipt entry so the operator can audit attribution variance. Writes only to the
/// <c>.tmp</c> staging dir; the swap into <c>_consolidated/</c> is owned by
/// <see cref="StagedArtifactSwapper"/>.
/// </summary>
public sealed class LicenseUnionWriter(ICakeContext cakeContext, IPathService pathService)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ICakeLog _log = (cakeContext ?? throw new ArgumentNullException(nameof(cakeContext))).Log;

    /// <summary>
    /// Stages the consolidated license tree for <paramref name="libraryName"/> based on
    /// <paramref name="ridStatuses"/>. Only successful RIDs contribute; zero successful RIDs
    /// or zero candidate license files yield a <see cref="ConsolidationState"/> with
    /// <see cref="ConsolidationState.LicensesConsolidated"/> = <see langword="false"/>.
    /// </summary>
    public async Task<ConsolidationState> WriteUnionAsync(string libraryName, IReadOnlyList<RidHarvestStatus> ridStatuses, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentNullException.ThrowIfNull(ridStatuses);
        ct.ThrowIfCancellationRequested();

        var licensesRoot = _pathService.GetHarvestLibraryLicensesDir(libraryName);
        var consolidatedTempRoot = _pathService.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName);

        // Phase 1 staging: wipe any leftover tmp from a previous crash and start fresh.
        // The real _consolidated/ stays untouched — StagedArtifactSwapper handles the
        // replacement only after the whole write phase succeeded.
        if (_cakeContext.DirectoryExists(consolidatedTempRoot))
        {
            _cakeContext.DeleteDirectory(consolidatedTempRoot, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        var successfulRids = ridStatuses.Where(s => s.Success).Select(s => s.Rid).ToList();
        if (successfulRids.Count == 0)
        {
            return EmptyState();
        }

        var entries = CollectLicenseCandidates(licensesRoot, successfulRids);
        if (entries.Count == 0)
        {
            return EmptyState();
        }

        _cakeContext.EnsureDirectoryExists(consolidatedTempRoot);

        var divergences = new List<DivergentLicense>();
        foreach (var ((package, fileName), candidates) in entries)
        {
            ct.ThrowIfCancellationRequested();
            var divergence = await WriteConsolidatedEntryAsync(consolidatedTempRoot, package, fileName, candidates, ct).ConfigureAwait(false);
            if (divergence is not null)
            {
                divergences.Add(divergence);
            }
        }

        _log.Information(
            "Staged {0} unique license entries across {1} RID(s) into {2}.",
            entries.Count,
            successfulRids.Count,
            consolidatedTempRoot);

        return new ConsolidationState
        {
            LicensesConsolidated = true,
            LicenseEntriesCount = entries.Count,
            DivergentLicenses = divergences,
        };
    }

    private static ConsolidationState EmptyState() => new()
    {
        LicensesConsolidated = false,
        LicenseEntriesCount = 0,
        DivergentLicenses = [],
    };

    private Dictionary<(string Package, string FileName), List<LicenseCandidate>> CollectLicenseCandidates(
        DirectoryPath licensesRoot,
        List<string> successfulRids)
    {
        var entries = new Dictionary<(string Package, string FileName), List<LicenseCandidate>>();

        foreach (var rid in successfulRids)
        {
            var ridRoot = licensesRoot.Combine(rid);
            if (!_cakeContext.DirectoryExists(ridRoot))
            {
                continue;
            }

            foreach (var licenseFile in _cakeContext.GetFiles($"{ridRoot}/**/*"))
            {
                var segments = GetRelativePathSegments(ridRoot, licenseFile);
                if (segments.Count < 2)
                {
                    continue;
                }

                var package = segments[0];
                var fileName = licenseFile.GetFilename().FullPath;
                var key = (package, fileName);

                if (!entries.TryGetValue(key, out var candidates))
                {
                    candidates = [];
                    entries[key] = candidates;
                }

                candidates.Add(new LicenseCandidate(rid, licenseFile));
            }
        }

        return entries;
    }

    private async Task<DivergentLicense?> WriteConsolidatedEntryAsync(
        DirectoryPath consolidatedRoot,
        string package,
        string fileName,
        List<LicenseCandidate> candidates,
        CancellationToken ct)
    {
        var packageDir = consolidatedRoot.Combine(package);
        _cakeContext.EnsureDirectoryExists(packageDir);

        var hashed = new List<HashedLicenseCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var sha = await ComputeSha256Async(candidate.Path, ct).ConfigureAwait(false);
            hashed.Add(new HashedLicenseCandidate(candidate.Rid, candidate.Path, sha));
        }

        var distinctHashes = hashed.Select(h => h.Sha).Distinct(StringComparer.Ordinal).Count();
        if (distinctHashes == 1)
        {
            var canonicalPath = packageDir.CombineWithFilePath(fileName);
            await CopyFileAsync(hashed[0].Path, canonicalPath, ct).ConfigureAwait(false);
            return null;
        }

        _log.Warning(
            "License divergence detected for package '{0}' file '{1}' across RIDs ({2}); writing per-RID variants.",
            package,
            fileName,
            string.Join(", ", hashed.Select(h => h.Rid)));

        // Use Cake's FilePath helpers (not System.IO.Path) so the pack stays consistent
        // with Cake's path handling rules (forward slashes, invariant culture).
        var fileNamePath = new FilePath(fileName);
        var nameWithoutExtension = fileNamePath.GetFilenameWithoutExtension().FullPath;
        var extension = fileNamePath.GetExtension() ?? string.Empty;
        foreach (var variant in hashed)
        {
            var variantName = $"{nameWithoutExtension}.{variant.Rid}{extension}";
            var variantPath = packageDir.CombineWithFilePath(variantName);
            await CopyFileAsync(variant.Path, variantPath, ct).ConfigureAwait(false);
        }

        return new DivergentLicense
        {
            Package = package,
            FileName = fileName,
            Rids = hashed.Select(h => h.Rid).OrderBy(rid => rid, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    private static List<string> GetRelativePathSegments(DirectoryPath root, FilePath file)
    {
        var rootSegments = root.Segments;
        var fileSegments = file.Segments;

        var relative = new List<string>();
        for (var i = rootSegments.Length; i < fileSegments.Length; i++)
        {
            relative.Add(fileSegments[i]);
        }

        return relative;
    }

    private async Task<string> ComputeSha256Async(FilePath path, CancellationToken ct)
    {
        // Route through ICakeContext.FileSystem so FakeFileSystem-backed tests can exercise
        // the consolidation logic with in-memory files.
        var file = _cakeContext.FileSystem.GetFile(path);
        await using var stream = file.OpenRead();
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct).ConfigureAwait(false);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private async Task CopyFileAsync(FilePath source, FilePath target, CancellationToken ct)
    {
        var sourceFile = _cakeContext.FileSystem.GetFile(source);
        var targetFile = _cakeContext.FileSystem.GetFile(target);

        await using var inputStream = sourceFile.OpenRead();
        await using var outputStream = targetFile.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await inputStream.CopyToAsync(outputStream, ct).ConfigureAwait(false);
    }

    private sealed record LicenseCandidate(string Rid, FilePath Path);

    private sealed record HashedLicenseCandidate(string Rid, FilePath Path, string Sha);
}
