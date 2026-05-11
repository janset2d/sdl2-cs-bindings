// File-scope CA1031 suppression: the per-library catch is intentionally broad to aggregate
// every failure into a single end-of-run CakeException rather than aborting the whole
// consolidation run on the first non-operational exception. Each caught Exception's message
// funnels into the failures list; the operator gets a complete picture across all libraries.
#pragma warning disable CA1031

using Build.Data.Harvest;
using Build.Host;
using Build.Host.Paths;
using Build.Targets.ConsolidateHarvest.Reporting;
using Build.Targets.ConsolidateHarvest.Services;
using Cake.Common.IO;
using Cake.Core;
using Cake.Frosting;

namespace Build.Targets.ConsolidateHarvest;

/// <summary>
/// Cake target that merges the per-RID harvest receipts produced by Harvest into a single
/// per-library <c>harvest-manifest.json</c> + <c>licenses/_consolidated/</c> tree. Owns the
/// staged-replace orchestration directly: precondition gate (harvest output present), then
/// per-library walk through status load → license union → manifest write → atomic swap. Failures
/// aggregate across libraries so the operator sees the full picture in one report. Direct
/// successor to the retired pre-migration ConsolidateHarvestPipeline.
/// </summary>
[TaskName("ConsolidateHarvest")]
[TaskDescription("Merges per-RID harvest receipts into per-library harvest-manifest.json + consolidated license tree")]
public sealed class ConsolidateHarvestTask(
    IHarvestManifestRepository harvestManifestRepository,
    LicenseUnionWriter licenseWriter,
    StagedArtifactSwapper swapper,
    ConsolidateHarvestReporter reporter,
    ICakeContext cakeContext,
    IPathService pathService) : AsyncFrostingTask<BuildContext>
{
    private readonly IHarvestManifestRepository _harvestManifestRepository = harvestManifestRepository ?? throw new ArgumentNullException(nameof(harvestManifestRepository));
    private readonly LicenseUnionWriter _licenseWriter = licenseWriter ?? throw new ArgumentNullException(nameof(licenseWriter));
    private readonly StagedArtifactSwapper _swapper = swapper ?? throw new ArgumentNullException(nameof(swapper));
    private readonly ConsolidateHarvestReporter _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var harvestOutputBase = _pathService.HarvestOutput;
        if (!_cakeContext.DirectoryExists(harvestOutputBase))
        {
            throw new CakeException(
                $"ConsolidateHarvest precondition failed: harvest output root '{harvestOutputBase.FullPath}' is missing. " +
                "Run '--target Harvest' first, or fetch the per-RID harvest artifacts into this path when consolidating in a multi-runner CI pipeline.");
        }

        var libraryDirs = _cakeContext.GetDirectories($"{harvestOutputBase}/*");
        if (libraryDirs.Count == 0)
        {
            throw new CakeException(
                $"ConsolidateHarvest precondition failed: '{harvestOutputBase.FullPath}' contains no library directories. " +
                "Run '--target Harvest' first, or fetch the per-RID harvest artifacts into this path when consolidating in a multi-runner CI pipeline.");
        }

        _reporter.LogStarting(harvestOutputBase.FullPath);

        var failures = new List<(string Library, string Message)>();
        foreach (var libraryDir in libraryDirs)
        {
            var libraryName = libraryDir.GetDirectoryName();
            var failureMessage = await TryConsolidateLibraryAsync(libraryName).ConfigureAwait(false);
            if (failureMessage is not null)
            {
                failures.Add((libraryName, failureMessage));
            }
        }

        if (failures.Count > 0)
        {
            var summary = string.Join("; ", failures.Select(failure => $"{failure.Library}: {failure.Message}"));
            throw new CakeException(
                $"ConsolidateHarvest failed for {failures.Count} library/libraries — {summary}. Stale receipts (if any) were not overwritten; re-run Harvest + ConsolidateHarvest after resolving the underlying errors.");
        }

        _reporter.LogCompleted();
    }

    private async Task<string?> TryConsolidateLibraryAsync(string libraryName)
    {
        _reporter.StartLibrary(libraryName);

        try
        {
            var ridStatuses = await _harvestManifestRepository.LoadRidStatusesAsync(libraryName).ConfigureAwait(false);
            if (ridStatuses is null)
            {
                _reporter.SkipLibraryNoRidStatus(libraryName, "No RID status files found");
                return null;
            }

            _reporter.LogLoadedRidStatuses(libraryName, ridStatuses.Count);

            // Phase 1 of the staged replace: write everything to .tmp siblings so the old
            // _consolidated/ + harvest-manifest + harvest-summary survive any mid-flight crash.
            var consolidationState = await _licenseWriter.WriteUnionAsync(libraryName, ridStatuses).ConfigureAwait(false);
            var manifest = BuildManifest(libraryName, ridStatuses, consolidationState);
            await _harvestManifestRepository.WriteManifestTempAsync(libraryName, manifest).ConfigureAwait(false);

            // Phase 2: atomic swap each .tmp into its final location. Crash between operations
            // leaves the workspace in a recoverable state — the next Consolidate run sees the
            // missing final + present .tmp, treats the library as fresh, and re-stages.
            _swapper.SwapAtomic(
                _pathService.GetHarvestLibraryConsolidatedLicensesDir(libraryName),
                _pathService.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName));
            _swapper.SwapAtomic(
                _pathService.GetHarvestLibraryManifestFile(libraryName),
                _pathService.GetHarvestLibraryManifestTempFile(libraryName));
            _swapper.SwapAtomic(
                _pathService.GetHarvestLibrarySummaryFile(libraryName),
                _pathService.GetHarvestLibrarySummaryTempFile(libraryName));

            _reporter.FinishLibrary(libraryName);
            return null;
        }
        catch (Exception ex)
        {
            _reporter.ReportLibraryFailure(libraryName, ex.Message, ex);
            CleanupTempArtifacts(libraryName);
            return ex.Message;
        }
    }

    private static HarvestManifest BuildManifest(string libraryName, IReadOnlyList<RidHarvestStatus> ridStatuses, ConsolidationState consolidationState)
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
    /// Best-effort cleanup of the <c>.tmp</c> artifacts after a library consolidation failure.
    /// Keeps the workspace from accumulating orphan temp state between runs. Failures here are
    /// non-fatal — the next HarvestStatusRepository invalidation pass will retry cleanup at
    /// the start of a fresh harvest.
    /// </summary>
    private void CleanupTempArtifacts(string libraryName)
    {
        try
        {
            var consolidatedTemp = _pathService.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName);
            if (_cakeContext.DirectoryExists(consolidatedTemp))
            {
                _cakeContext.DeleteDirectory(consolidatedTemp, new DeleteDirectorySettings { Recursive = true, Force = true });
            }

            var manifestTemp = _pathService.GetHarvestLibraryManifestTempFile(libraryName);
            if (_cakeContext.FileExists(manifestTemp))
            {
                _cakeContext.DeleteFile(manifestTemp);
            }

            var summaryTemp = _pathService.GetHarvestLibrarySummaryTempFile(libraryName);
            if (_cakeContext.FileExists(summaryTemp))
            {
                _cakeContext.DeleteFile(summaryTemp);
            }
        }
        catch (Exception ex)
        {
            _reporter.WarnCleanupFailure(libraryName, ex.Message);
        }
    }
}
