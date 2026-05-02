#pragma warning disable CA1031

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Shared.Harvesting;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Features.Harvesting;

public sealed class ConsolidateHarvestPipeline(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly JsonSerializerOptions _jsonOptions = HarvestJsonContract.Options;

    public async Task RunAsync(ConsolidateHarvestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var harvestOutputBase = _pathService.HarvestOutput;
        _ = _jsonOptions;
        _log.Information("Consolidating harvest RID status files from: {0}", harvestOutputBase);

        var libraryDirs = EnsureConsolidationInputsReady(harvestOutputBase);

        var failures = new List<(string Library, string Message)>();

        foreach (var libraryDir in libraryDirs)
        {
            var libraryName = libraryDir.GetDirectoryName();
            var failureMessage = await TryConsolidateLibraryAsync(libraryName);
            if (failureMessage is not null)
            {
                failures.Add((libraryName, failureMessage));
            }
        }

        if (failures.Count > 0)
        {
            var summary = string.Join("; ", failures.Select(failure => $"{failure.Library}: {failure.Message}"));
            throw new Cake.Core.CakeException(
                $"ConsolidateHarvest failed for {failures.Count} library/libraries — {summary}. Stale receipts (if any) were not overwritten; re-run Harvest + ConsolidateHarvest after resolving the underlying errors.");
        }

        _log.Information("Harvest consolidation completed successfully");
    }

    private List<DirectoryPath> EnsureConsolidationInputsReady(DirectoryPath harvestOutputBase)
    {
        if (!_cakeContext.DirectoryExists(harvestOutputBase))
        {
            throw new Cake.Core.CakeException(
                $"ConsolidateHarvest precondition failed: harvest output root '{harvestOutputBase.FullPath}' is missing. " +
                "Run '--target Harvest' first, or fetch the per-RID harvest artifacts into this path when consolidating in a multi-runner CI pipeline.");
        }

        var libraryDirs = _cakeContext.GetDirectories($"{harvestOutputBase}/*");
        if (libraryDirs.Count == 0)
        {
            throw new Cake.Core.CakeException(
                $"ConsolidateHarvest precondition failed: '{harvestOutputBase.FullPath}' contains no library directories. " +
                "Run '--target Harvest' first, or fetch the per-RID harvest artifacts into this path when consolidating in a multi-runner CI pipeline.");
        }

        return libraryDirs.ToList();
    }

    private async Task<string?> TryConsolidateLibraryAsync(string libraryName)
    {
        try
        {
            var ridStatuses = await LoadRidStatusesAsync(libraryName);
            if (ridStatuses is null)
            {
                return null;
            }

            // Staged-replace swap (not truly atomic — IFile.Move / IDirectory.Move don't
            // have replace overloads so the final step is delete-then-move). Phase 1
            // writes everything to .tmp siblings so the old receipt + old _consolidated/
            // survive any mid-flight crash. Phase 2 (SwapTempArtifactsIntoPlace)
            // deletes old + Moves tmp → final. If we crash between Phase 1 and Phase 2,
            // old state is preserved; Package's gate keeps trusting the previous valid
            // receipt until the next Consolidate run retries.
            var consolidationState = await ConsolidateLicensesToTempAsync(libraryName, ridStatuses);

            var manifest = GenerateHarvestManifest(libraryName, ridStatuses, consolidationState);
            await WriteManifestAndSummaryTempAsync(libraryName, manifest);

            SwapTempArtifactsIntoPlace(libraryName);
            return null;
        }
        catch (Exception ex)
        {
            _log.Error("Failed to consolidate harvest for library {0}: {1}", libraryName, ex.Message);
            _log.Verbose("Consolidation error details: {0}", ex);
            CleanupTempArtifacts(libraryName);
            return ex.Message;
        }
    }

    /// <summary>
    /// Phase 2 of the staged replace: deletes the old <c>_consolidated/</c> +
    /// <c>harvest-manifest.json</c> + <c>harvest-summary.json</c>, then moves each
    /// <c>.tmp</c> sibling into the final location. Crash between delete-old and move-tmp
    /// is self-healing: the next Consolidate run sees missing final artifacts, treats the
    /// library as fresh, and retries. Package's gate rejects the missing state in the
    /// meantime.
    /// </summary>
    private void SwapTempArtifactsIntoPlace(string libraryName)
    {
        var paths = _pathService;
        var consolidatedFinal = paths.GetHarvestLibraryConsolidatedLicensesDir(libraryName);
        var consolidatedTemp = paths.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName);

        if (_cakeContext.DirectoryExists(consolidatedTemp))
        {
            if (_cakeContext.DirectoryExists(consolidatedFinal))
            {
                _cakeContext.DeleteDirectory(consolidatedFinal, new DeleteDirectorySettings { Recursive = true, Force = true });
            }

            _cakeContext.FileSystem.GetDirectory(consolidatedTemp).Move(consolidatedFinal);
            _log.Verbose("Swapped consolidated license tree for {0}: {1}", libraryName, consolidatedFinal);
        }

        SwapFileIntoPlace(paths.GetHarvestLibraryManifestTempFile(libraryName), paths.GetHarvestLibraryManifestFile(libraryName), libraryName);
        SwapFileIntoPlace(paths.GetHarvestLibrarySummaryTempFile(libraryName), paths.GetHarvestLibrarySummaryFile(libraryName), libraryName);
    }

    private void SwapFileIntoPlace(FilePath tempPath, FilePath finalPath, string libraryName)
    {
        if (!_cakeContext.FileExists(tempPath))
        {
            return;
        }

        if (_cakeContext.FileExists(finalPath))
        {
            _cakeContext.DeleteFile(finalPath);
        }

        _cakeContext.FileSystem.GetFile(tempPath).Move(finalPath);
        _log.Verbose("Swapped {0} for {1}: {2}", finalPath.GetFilename().FullPath, libraryName, finalPath);
    }

    /// <summary>
    /// Best-effort cleanup of the <c>.tmp</c> artifacts after a library consolidation
    /// failure. Keeps the workspace from accumulating orphan temp state between runs.
    /// Failures to clean up are non-fatal — the next <c>HarvestTask.InvalidateCrossRidReceipts</c>
    /// run will retry cleanup at invalidation time.
    /// </summary>
    private void CleanupTempArtifacts(string libraryName)
    {
        try
        {
            var paths = _pathService;

            var consolidatedTemp = paths.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName);
            if (_cakeContext.DirectoryExists(consolidatedTemp))
            {
                _cakeContext.DeleteDirectory(consolidatedTemp, new DeleteDirectorySettings { Recursive = true, Force = true });
            }

            var manifestTemp = paths.GetHarvestLibraryManifestTempFile(libraryName);
            if (_cakeContext.FileExists(manifestTemp))
            {
                _cakeContext.DeleteFile(manifestTemp);
            }

            var summaryTemp = paths.GetHarvestLibrarySummaryTempFile(libraryName);
            if (_cakeContext.FileExists(summaryTemp))
            {
                _cakeContext.DeleteFile(summaryTemp);
            }
        }
        catch (Exception ex)
        {
            _log.Warning("Best-effort tmp cleanup failed for {0}: {1}", libraryName, ex.Message);
        }
    }

    private async Task<List<RidHarvestStatus>?> LoadRidStatusesAsync(string libraryName)
    {
        var ridStatusDir = _pathService.GetHarvestLibraryRidStatusDir(libraryName);

        if (!_cakeContext.DirectoryExists(ridStatusDir))
        {
            _log.Information("No RID status directory found for library: {0}", libraryName);
            return null;
        }

        var ridStatusFiles = _cakeContext.GetFiles($"{ridStatusDir}/*.json");
        if (ridStatusFiles.Count == 0)
        {
            _log.Warning("No RID status files found for library: {0}", libraryName);
            return null;
        }

        _log.Information("Found {0} RID status files for library: {1}", ridStatusFiles.Count, libraryName);

        var ridStatuses = new List<RidHarvestStatus>();
        var invalidStatusFiles = new List<string>();
        foreach (var statusFile in ridStatusFiles)
        {
            try
            {
                var jsonContent = await _cakeContext.ReadAllTextAsync(statusFile);
                var ridStatus = CakeJsonExtensions.DeserializeJson<RidHarvestStatus>(jsonContent, HarvestJsonContract.Options);
                if (ridStatus != null)
                {
                    ridStatuses.Add(ridStatus);
                    _log.Verbose("Loaded RID status for {0}: {1}", ridStatus.Rid, ridStatus.Success ? "Success" : "Failed");
                }
            }
            catch (Exception ex)
            {
                invalidStatusFiles.Add($"{statusFile.GetFilename().FullPath}: {ex.Message}");
            }
        }

        if (invalidStatusFiles.Count > 0)
        {
            throw new Cake.Core.CakeException(
                $"RID status directory '{ridStatusDir.FullPath}' for library '{libraryName}' contains {invalidStatusFiles.Count} unreadable or invalid file(s): {string.Join("; ", invalidStatusFiles)}. " +
                "ConsolidateHarvest refuses to continue because silently dropping a RID status can shrink the consolidated license set and produce a false-green compliance surface.");
        }

        if (ridStatuses.Count == 0)
        {
            _log.Warning("No valid RID status data found for library: {0}", libraryName);
            return null;
        }

        return ridStatuses;
    }

    private async Task WriteManifestAndSummaryTempAsync(string libraryName, HarvestManifest manifest)
    {
        // Phase 1 of the staged replace: write to .tmp.json siblings so the old manifest +
        // summary survive any mid-flight crash. Phase 2 (SwapTempArtifactsIntoPlace) moves
        // these into final position once the whole library's consolidation succeeded.
        var paths = _pathService;

        var manifestTempPath = paths.GetHarvestLibraryManifestTempFile(libraryName);
        await _cakeContext.WriteJsonAsync(manifestTempPath, manifest, HarvestJsonContract.Options);
        _log.Verbose("Wrote harvest manifest (tmp) for {0}: {1}", libraryName, manifestTempPath);

        var summaryTempPath = paths.GetHarvestLibrarySummaryTempFile(libraryName);
        await _cakeContext.WriteJsonAsync(summaryTempPath, manifest.Summary, HarvestJsonContract.Options);
        _log.Verbose("Wrote harvest summary (tmp) for {0}: {1}", libraryName, summaryTempPath);
    }

    private async Task<ConsolidationState> ConsolidateLicensesToTempAsync(string libraryName, List<RidHarvestStatus> ridStatuses)
    {
        var paths = _pathService;
        var licensesRoot = paths.GetHarvestLibraryLicensesDir(libraryName);
        var consolidatedTempRoot = paths.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName);

        // Phase 1 staging: wipe any leftover tmp from a previous crash and start fresh.
        // The real _consolidated/ stays untouched — SwapTempArtifactsIntoPlace handles the
        // replacement only after the whole write phase succeeded.
        if (_cakeContext.DirectoryExists(consolidatedTempRoot))
        {
            _cakeContext.DeleteDirectory(consolidatedTempRoot, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        var successfulRids = ridStatuses.Where(s => s.Success).Select(s => s.Rid).ToList();
        if (successfulRids.Count == 0)
        {
            _log.Verbose("No successful RIDs for license consolidation.");
            return new ConsolidationState
            {
                LicensesConsolidated = false,
                LicenseEntriesCount = 0,
                DivergentLicenses = [],
            };
        }

        var entries = CollectLicenseCandidates(licensesRoot, successfulRids);
        if (entries.Count == 0)
        {
            _log.Verbose("No per-RID license files found under {0}.", licensesRoot);
            return new ConsolidationState
            {
                LicensesConsolidated = false,
                LicenseEntriesCount = 0,
                DivergentLicenses = [],
            };
        }

        _cakeContext.EnsureDirectoryExists(consolidatedTempRoot);

        var divergences = new List<DivergentLicense>();
        foreach (var ((package, fileName), candidates) in entries)
        {
            var divergence = await WriteConsolidatedEntryAsync(consolidatedTempRoot, package, fileName, candidates);
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
                    candidates = new List<LicenseCandidate>();
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
        List<LicenseCandidate> candidates)
    {
        var packageDir = consolidatedRoot.Combine(package);
        _cakeContext.EnsureDirectoryExists(packageDir);

        var hashed = new List<HashedLicenseCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var sha = await ComputeSha256Async(candidate.Path);
            hashed.Add(new HashedLicenseCandidate(candidate.Rid, candidate.Path, sha));
        }

        var distinctHashes = hashed.Select(h => h.Sha).Distinct(StringComparer.Ordinal).Count();
        if (distinctHashes == 1)
        {
            var canonicalPath = packageDir.CombineWithFilePath(fileName);
            await CopyFileAsync(hashed[0].Path, canonicalPath);
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
            await CopyFileAsync(variant.Path, variantPath);
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

    private async Task<string> ComputeSha256Async(FilePath path)
    {
        // Route through ICakeContext.FileSystem so FakeFileSystem-backed tests can exercise
        // the consolidation logic with in-memory files.
        var file = _cakeContext.FileSystem.GetFile(path);
        await using var stream = file.OpenRead();
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private async Task CopyFileAsync(FilePath source, FilePath target)
    {
        var sourceFile = _cakeContext.FileSystem.GetFile(source);
        var targetFile = _cakeContext.FileSystem.GetFile(target);

        await using var inputStream = sourceFile.OpenRead();
        await using var outputStream = targetFile.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await inputStream.CopyToAsync(outputStream);
    }

    private sealed record LicenseCandidate(string Rid, FilePath Path);

    private sealed record HashedLicenseCandidate(string Rid, FilePath Path, string Sha);

    private static HarvestManifest GenerateHarvestManifest(string libraryName, List<RidHarvestStatus> ridStatuses, ConsolidationState consolidationState)
    {
        var successfulRids = ridStatuses.Where(r => r.Success).ToList();
        var failedRids = ridStatuses.Where(r => !r.Success).ToList();

        var summary = new HarvestSummary
        {
            TotalRids = ridStatuses.Count,
            SuccessfulRids = successfulRids.Count,
            FailedRids = failedRids.Count,
            SuccessRate = ridStatuses.Count > 0 ? (double)successfulRids.Count / ridStatuses.Count : 0.0,
        };

        return new HarvestManifest
        {
            LibraryName = libraryName,
            GeneratedTimestamp = DateTimeOffset.UtcNow,
            Rids = ridStatuses.AsReadOnly(),
            Summary = summary,
            Consolidation = consolidationState,
        };
    }
}
