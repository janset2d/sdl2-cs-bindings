#pragma warning disable CA1031

using Build.Harvesting;
using Build.Host.Cake;
using Build.Host.Paths;
using Cake.Common.IO;
using Cake.Core;

namespace Build.Targets.ConsolidateHarvest.Services;

/// <summary>
/// Loads per-RID <see cref="RidHarvestStatus"/> JSON files for a single library, builds the
/// consolidated <see cref="HarvestManifest"/> from them + a license consolidation outcome,
/// and writes the manifest + summary to their <c>.tmp</c> staging siblings. The .tmp →
/// final swap is owned by <see cref="StagedArtifactSwapper"/>; the merger never touches the
/// final paths so the staged-replace contract stays intact.
/// </summary>
public sealed class HarvestArtifactMerger(ICakeContext cakeContext, IPathService pathService)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    /// <summary>
    /// Returns the parsed RID status records for <paramref name="libraryName"/>, or
    /// <see langword="null"/> when the rid-status directory is missing OR contains no
    /// JSON files OR every file parsed empty (treated as "library not yet harvested,
    /// skip rather than fail aggregate run"). Throws <see cref="CakeException"/> when
    /// any file is unreadable / invalid JSON — silently dropping a RID would shrink the
    /// consolidated license set and produce a false-green compliance surface.
    /// </summary>
    public async Task<IReadOnlyList<RidHarvestStatus>?> LoadRidStatusesAsync(string libraryName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ct.ThrowIfCancellationRequested();

        var ridStatusDir = _pathService.GetHarvestLibraryRidStatusDir(libraryName);
        if (!_cakeContext.DirectoryExists(ridStatusDir))
        {
            return null;
        }

        var ridStatusFiles = _cakeContext.GetFiles($"{ridStatusDir}/*.json");
        if (ridStatusFiles.Count == 0)
        {
            return null;
        }

        var ridStatuses = new List<RidHarvestStatus>();
        var invalidStatusFiles = new List<string>();
        foreach (var statusFile in ridStatusFiles)
        {
            try
            {
                var jsonContent = await _cakeContext.ReadAllTextAsync(statusFile).ConfigureAwait(false);
                var ridStatus = CakeJsonExtensions.DeserializeJson<RidHarvestStatus>(jsonContent);
                if (ridStatus != null)
                {
                    ridStatuses.Add(ridStatus);
                }
            }
            catch (Exception ex)
            {
                invalidStatusFiles.Add($"{statusFile.GetFilename().FullPath}: {ex.Message}");
            }
        }

        if (invalidStatusFiles.Count > 0)
        {
            throw new CakeException(
                $"RID status directory '{ridStatusDir.FullPath}' for library '{libraryName}' contains {invalidStatusFiles.Count} unreadable or invalid file(s): {string.Join("; ", invalidStatusFiles)}. " +
                "ConsolidateHarvest refuses to continue because silently dropping a RID status can shrink the consolidated license set and produce a false-green compliance surface.");
        }

        return ridStatuses.Count == 0 ? null : ridStatuses;
    }

    /// <summary>
    /// Builds a <see cref="HarvestManifest"/> from the loaded per-RID statuses + the license
    /// consolidation outcome. Pure data transform; no I/O. The summary aggregates total /
    /// successful / failed RID counts + a success-rate fraction.
    /// </summary>
    public static HarvestManifest BuildManifest(string libraryName, IReadOnlyList<RidHarvestStatus> ridStatuses, ConsolidationState consolidationState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentNullException.ThrowIfNull(ridStatuses);
        ArgumentNullException.ThrowIfNull(consolidationState);

        var successfulRids = ridStatuses.Count(r => r.Success);
        var failedRids = ridStatuses.Count - successfulRids;

        return new HarvestManifest
        {
            LibraryName = libraryName,
            GeneratedTimestamp = DateTimeOffset.UtcNow,
            Rids = ridStatuses,
            Summary = new HarvestSummary
            {
                TotalRids = ridStatuses.Count,
                SuccessfulRids = successfulRids,
                FailedRids = failedRids,
                SuccessRate = ridStatuses.Count > 0 ? (double)successfulRids / ridStatuses.Count : 0.0,
            },
            Consolidation = consolidationState,
        };
    }

    /// <summary>
    /// Phase 1 of the staged replace: writes <paramref name="manifest"/> to its <c>.tmp.json</c>
    /// sibling + the embedded summary to <c>harvest-summary.tmp.json</c>. The old final
    /// stays untouched until <see cref="StagedArtifactSwapper"/> runs the move.
    /// </summary>
    public async Task WriteManifestTempAsync(string libraryName, HarvestManifest manifest, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentNullException.ThrowIfNull(manifest);
        ct.ThrowIfCancellationRequested();

        var manifestTempPath = _pathService.GetHarvestLibraryManifestTempFile(libraryName);
        await _cakeContext.WriteJsonAsync(manifestTempPath, manifest).ConfigureAwait(false);

        var summaryTempPath = _pathService.GetHarvestLibrarySummaryTempFile(libraryName);
        await _cakeContext.WriteJsonAsync(summaryTempPath, manifest.Summary).ConfigureAwait(false);
    }
}
