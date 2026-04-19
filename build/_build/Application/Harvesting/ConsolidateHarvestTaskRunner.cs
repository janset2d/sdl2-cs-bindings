#pragma warning disable CA1031

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Build.Context;
using Build.Domain.Harvesting;
using Build.Domain.Harvesting.Models;
using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Application.Harvesting;

public sealed class ConsolidateHarvestTaskRunner
{
    private readonly JsonSerializerOptions _jsonOptions = HarvestJsonContract.Options;

    public async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var harvestOutputBase = context.Paths.HarvestOutput;
        _ = _jsonOptions;
        context.Log.Information("Consolidating harvest RID status files from: {0}", harvestOutputBase);

        if (!context.DirectoryExists(harvestOutputBase))
        {
            context.Log.Warning("Harvest output directory does not exist: {0}", harvestOutputBase);
            return;
        }

        // Get all library directories using glob pattern
        var libraryDirs = context.GetDirectories($"{harvestOutputBase}/*");

        if (libraryDirs.Count == 0)
        {
            context.Log.Warning("No library directories found in harvest output");
            return;
        }

        // Aggregate failures across libraries so operators see every problem in one run
        // instead of fixing one and re-running to find the next. Task fails fatally at the
        // end if any library hit an error — pre-H1 code swallowed per-library exceptions
        // and ended with "completed successfully" which hid compliance regressions.
        var failures = new List<(string Library, string Message)>();

        foreach (var libraryDir in libraryDirs)
        {
            var libraryName = libraryDir.GetDirectoryName();
            var failureMessage = await TryConsolidateLibraryAsync(context, libraryName);
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

        context.Log.Information("Harvest consolidation completed successfully");
    }

    private static async Task<string?> TryConsolidateLibraryAsync(BuildContext context, string libraryName)
    {
        try
        {
            var ridStatuses = await LoadRidStatusesAsync(context, libraryName);
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
            var consolidationState = await ConsolidateLicensesToTempAsync(context, libraryName, ridStatuses);

            var manifest = GenerateHarvestManifest(libraryName, ridStatuses, consolidationState);
            await WriteManifestAndSummaryTempAsync(context, libraryName, manifest);

            SwapTempArtifactsIntoPlace(context, libraryName);
            return null;
        }
        catch (Exception ex)
        {
            context.Log.Error("Failed to consolidate harvest for library {0}: {1}", libraryName, ex.Message);
            context.Log.Verbose("Consolidation error details: {0}", ex);
            CleanupTempArtifacts(context, libraryName);
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
    private static void SwapTempArtifactsIntoPlace(BuildContext context, string libraryName)
    {
        var paths = context.Paths;
        var consolidatedFinal = paths.GetHarvestLibraryConsolidatedLicensesDir(libraryName);
        var consolidatedTemp = paths.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName);

        if (context.DirectoryExists(consolidatedTemp))
        {
            if (context.DirectoryExists(consolidatedFinal))
            {
                context.DeleteDirectory(consolidatedFinal, new DeleteDirectorySettings { Recursive = true, Force = true });
            }

            context.FileSystem.GetDirectory(consolidatedTemp).Move(consolidatedFinal);
            context.Log.Verbose("Swapped consolidated license tree for {0}: {1}", libraryName, consolidatedFinal);
        }

        SwapFileIntoPlace(context, paths.GetHarvestLibraryManifestTempFile(libraryName), paths.GetHarvestLibraryManifestFile(libraryName), libraryName);
        SwapFileIntoPlace(context, paths.GetHarvestLibrarySummaryTempFile(libraryName), paths.GetHarvestLibrarySummaryFile(libraryName), libraryName);
    }

    private static void SwapFileIntoPlace(BuildContext context, FilePath tempPath, FilePath finalPath, string libraryName)
    {
        if (!context.FileExists(tempPath))
        {
            return;
        }

        if (context.FileExists(finalPath))
        {
            context.DeleteFile(finalPath);
        }

        context.FileSystem.GetFile(tempPath).Move(finalPath);
        context.Log.Verbose("Swapped {0} for {1}: {2}", finalPath.GetFilename().FullPath, libraryName, finalPath);
    }

    /// <summary>
    /// Best-effort cleanup of the <c>.tmp</c> artifacts after a library consolidation
    /// failure. Keeps the workspace from accumulating orphan temp state between runs.
    /// Failures to clean up are non-fatal — the next <c>HarvestTask.InvalidateCrossRidReceipts</c>
    /// run will retry cleanup at invalidation time.
    /// </summary>
    private static void CleanupTempArtifacts(BuildContext context, string libraryName)
    {
        try
        {
            var paths = context.Paths;

            var consolidatedTemp = paths.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName);
            if (context.DirectoryExists(consolidatedTemp))
            {
                context.DeleteDirectory(consolidatedTemp, new DeleteDirectorySettings { Recursive = true, Force = true });
            }

            var manifestTemp = paths.GetHarvestLibraryManifestTempFile(libraryName);
            if (context.FileExists(manifestTemp))
            {
                context.DeleteFile(manifestTemp);
            }

            var summaryTemp = paths.GetHarvestLibrarySummaryTempFile(libraryName);
            if (context.FileExists(summaryTemp))
            {
                context.DeleteFile(summaryTemp);
            }
        }
        catch (Exception ex)
        {
            context.Log.Warning("Best-effort tmp cleanup failed for {0}: {1}", libraryName, ex.Message);
        }
    }

    private static async Task<List<RidHarvestStatus>?> LoadRidStatusesAsync(BuildContext context, string libraryName)
    {
        var ridStatusDir = context.Paths.GetHarvestLibraryRidStatusDir(libraryName);

        if (!context.DirectoryExists(ridStatusDir))
        {
            context.Log.Information("No RID status directory found for library: {0}", libraryName);
            return null;
        }

        var ridStatusFiles = context.GetFiles($"{ridStatusDir}/*.json");
        if (ridStatusFiles.Count == 0)
        {
            context.Log.Warning("No RID status files found for library: {0}", libraryName);
            return null;
        }

        context.Log.Information("Found {0} RID status files for library: {1}", ridStatusFiles.Count, libraryName);

        var ridStatuses = new List<RidHarvestStatus>();
        var invalidStatusFiles = new List<string>();
        foreach (var statusFile in ridStatusFiles)
        {
            try
            {
                var jsonContent = await context.ReadAllTextAsync(statusFile);
                var ridStatus = JsonSerializer.Deserialize<RidHarvestStatus>(jsonContent, HarvestJsonContract.Options);
                if (ridStatus != null)
                {
                    ridStatuses.Add(ridStatus);
                    context.Log.Verbose("Loaded RID status for {0}: {1}", ridStatus.Rid, ridStatus.Success ? "Success" : "Failed");
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
            context.Log.Warning("No valid RID status data found for library: {0}", libraryName);
            return null;
        }

        return ridStatuses;
    }

    private static async Task WriteManifestAndSummaryTempAsync(BuildContext context, string libraryName, HarvestManifest manifest)
    {
        // Phase 1 of the staged replace: write to .tmp.json siblings so the old manifest +
        // summary survive any mid-flight crash. Phase 2 (SwapTempArtifactsIntoPlace) moves
        // these into final position once the whole library's consolidation succeeded.
        var paths = context.Paths;

        var manifestTempPath = paths.GetHarvestLibraryManifestTempFile(libraryName);
        var manifestJson = JsonSerializer.Serialize(manifest, HarvestJsonContract.Options);
        await context.WriteAllTextAsync(manifestTempPath, manifestJson);
        context.Log.Verbose("Wrote harvest manifest (tmp) for {0}: {1}", libraryName, manifestTempPath);

        var summaryTempPath = paths.GetHarvestLibrarySummaryTempFile(libraryName);
        var summaryJson = JsonSerializer.Serialize(manifest.Summary, HarvestJsonContract.Options);
        await context.WriteAllTextAsync(summaryTempPath, summaryJson);
        context.Log.Verbose("Wrote harvest summary (tmp) for {0}: {1}", libraryName, summaryTempPath);
    }

    private static async Task<ConsolidationState> ConsolidateLicensesToTempAsync(BuildContext context, string libraryName, List<RidHarvestStatus> ridStatuses)
    {
        var paths = context.Paths;
        var licensesRoot = paths.GetHarvestLibraryLicensesDir(libraryName);
        var consolidatedTempRoot = paths.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName);

        // Phase 1 staging: wipe any leftover tmp from a previous crash and start fresh.
        // The real _consolidated/ stays untouched — SwapTempArtifactsIntoPlace handles the
        // replacement only after the whole write phase succeeded.
        if (context.DirectoryExists(consolidatedTempRoot))
        {
            context.DeleteDirectory(consolidatedTempRoot, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        var successfulRids = ridStatuses.Where(s => s.Success).Select(s => s.Rid).ToList();
        if (successfulRids.Count == 0)
        {
            context.Log.Verbose("No successful RIDs for license consolidation.");
            return new ConsolidationState
            {
                LicensesConsolidated = false,
                LicenseEntriesCount = 0,
                DivergentLicenses = [],
            };
        }

        var entries = CollectLicenseCandidates(context, licensesRoot, successfulRids);
        if (entries.Count == 0)
        {
            context.Log.Verbose("No per-RID license files found under {0}.", licensesRoot);
            return new ConsolidationState
            {
                LicensesConsolidated = false,
                LicenseEntriesCount = 0,
                DivergentLicenses = [],
            };
        }

        context.EnsureDirectoryExists(consolidatedTempRoot);

        var divergences = new List<DivergentLicense>();
        foreach (var ((package, fileName), candidates) in entries)
        {
            var divergence = await WriteConsolidatedEntryAsync(context, consolidatedTempRoot, package, fileName, candidates);
            if (divergence is not null)
            {
                divergences.Add(divergence);
            }
        }

        context.Log.Information(
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

    private static Dictionary<(string Package, string FileName), List<LicenseCandidate>> CollectLicenseCandidates(
        BuildContext context,
        DirectoryPath licensesRoot,
        List<string> successfulRids)
    {
        var entries = new Dictionary<(string Package, string FileName), List<LicenseCandidate>>();

        foreach (var rid in successfulRids)
        {
            var ridRoot = licensesRoot.Combine(rid);
            if (!context.DirectoryExists(ridRoot))
            {
                continue;
            }

            foreach (var licenseFile in context.GetFiles($"{ridRoot}/**/*"))
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

    private static async Task<DivergentLicense?> WriteConsolidatedEntryAsync(
        BuildContext context,
        DirectoryPath consolidatedRoot,
        string package,
        string fileName,
        List<LicenseCandidate> candidates)
    {
        var packageDir = consolidatedRoot.Combine(package);
        context.EnsureDirectoryExists(packageDir);

        var hashed = new List<HashedLicenseCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var sha = await ComputeSha256Async(context, candidate.Path);
            hashed.Add(new HashedLicenseCandidate(candidate.Rid, candidate.Path, sha));
        }

        var distinctHashes = hashed.Select(h => h.Sha).Distinct(StringComparer.Ordinal).Count();
        if (distinctHashes == 1)
        {
            var canonicalPath = packageDir.CombineWithFilePath(fileName);
            await CopyFileAsync(context, hashed[0].Path, canonicalPath);
            return null;
        }

        context.Log.Warning(
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
            await CopyFileAsync(context, variant.Path, variantPath);
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

    private static async Task<string> ComputeSha256Async(BuildContext context, FilePath path)
    {
        // Route through ICakeContext.FileSystem so FakeFileSystem-backed tests can exercise
        // the consolidation logic with in-memory files.
        var file = context.FileSystem.GetFile(path);
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

    private static async Task CopyFileAsync(BuildContext context, FilePath source, FilePath target)
    {
        var sourceFile = context.FileSystem.GetFile(source);
        var targetFile = context.FileSystem.GetFile(target);

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
