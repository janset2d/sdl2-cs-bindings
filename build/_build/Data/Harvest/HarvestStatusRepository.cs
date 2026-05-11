using Build.Host.Cake;
using Build.Host.Paths;
using Build.Runtime;
using Build.Targets.Harvest.Models;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.Harvest;

/// <summary>
/// File-backed repository for the per-RID rid-status JSON contract written by HarvestTask
/// and read by ConsolidateHarvestTask. Owns invalidation of stale per-RID payload + cross-RID
/// consolidated receipts before a fresh harvest run, and persistence of the
/// <see cref="RidHarvestStatus"/> success/error record. The schema stays byte-identical to
/// the established rid-status contract so consolidation and Pack consume the same bytes.
/// </summary>
public interface IHarvestStatusRepository
{
    void Invalidate(string libraryName, CancellationToken ct = default);

    Task WriteSuccessAsync(string libraryName, DeploymentStatistics statistics, CancellationToken ct = default);

    Task WriteErrorAsync(string libraryName, string errorMessage, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class HarvestStatusRepository(ICakeContext cakeContext, IPathService pathService, IRuntimeProfile runtimeProfile) : IHarvestStatusRepository
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));

    /// <summary>
    /// Cleans the current RID's payload (runtime tree + status file + RID-scoped license tree)
    /// and the cross-RID consolidated receipts (consolidated license dir + harvest-manifest +
    /// harvest-summary, plus their *.tmp staging siblings). Sibling RIDs' payloads are left
    /// intact so multi-RID harvests preserve each RID's evidence independently.
    /// </summary>
    public void Invalidate(string libraryName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ct.ThrowIfCancellationRequested();

        CleanCurrentRidPayload(libraryName);
        InvalidateCrossRidReceipts(libraryName);
    }

    /// <summary>
    /// Persists a success-flagged <see cref="RidHarvestStatus"/> record at
    /// <c>rid-status/{rid}.json</c> via <see cref="CakeJsonExtensions.WriteJsonAsync"/>. Counts
    /// roll up from the supplied <see cref="DeploymentStatistics"/>; the deployment-strategy
    /// enum is rendered as its enum-name string for cross-RID JSON parity.
    /// </summary>
    public async Task WriteSuccessAsync(string libraryName, DeploymentStatistics statistics, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentNullException.ThrowIfNull(statistics);
        ct.ThrowIfCancellationRequested();

        var record = new RidHarvestStatus
        {
            LibraryName = libraryName,
            Rid = _runtimeProfile.Rid,
            Triplet = _runtimeProfile.Triplet,
            Success = true,
            ErrorMessage = null,
            Timestamp = DateTimeOffset.UtcNow,
            Statistics = new HarvestStatistics
            {
                PrimaryFilesCount = statistics.PrimaryFiles.Count,
                RuntimeFilesCount = statistics.RuntimeFiles.Count,
                LicenseFilesCount = statistics.LicenseFiles.Count,
                DeployedPackagesCount = statistics.DeployedPackages.Count,
                FilteredPackagesCount = statistics.FilteredPackages.Count,
                DeploymentStrategy = statistics.DeploymentStrategy.ToString(),
            },
        };

        await WriteStatusAsync(libraryName, record).ConfigureAwait(false);
    }

    /// <summary>
    /// Persists a failure-flagged <see cref="RidHarvestStatus"/> record so consolidation observes
    /// the per-RID error rather than treating a missing rid-status as silent success.
    /// </summary>
    public async Task WriteErrorAsync(string libraryName, string errorMessage, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        ct.ThrowIfCancellationRequested();

        var record = new RidHarvestStatus
        {
            LibraryName = libraryName,
            Rid = _runtimeProfile.Rid,
            Triplet = _runtimeProfile.Triplet,
            Success = false,
            ErrorMessage = errorMessage,
            Timestamp = DateTimeOffset.UtcNow,
            Statistics = null,
        };

        await WriteStatusAsync(libraryName, record).ConfigureAwait(false);
    }

    private async Task WriteStatusAsync(string libraryName, RidHarvestStatus record)
    {
        var statusDir = _pathService.GetHarvestLibraryRidStatusDir(libraryName);
        _cakeContext.EnsureDirectoryExists(statusDir);

        var statusFile = _pathService.GetHarvestLibraryRidStatusFile(libraryName, _runtimeProfile.Rid);
        await _cakeContext.WriteJsonAsync(statusFile, record).ConfigureAwait(false);
    }

    private void CleanCurrentRidPayload(string libraryName)
    {
        var rid = _runtimeProfile.Rid;

        var ridRuntimeRoot = _pathService.GetHarvestLibraryRidRuntimesDir(libraryName, rid);
        EnsureWithinHarvestOutput(ridRuntimeRoot.FullPath);
        if (_cakeContext.DirectoryExists(ridRuntimeRoot))
        {
            _cakeContext.DeleteDirectory(ridRuntimeRoot, RecursiveForceDelete);
        }

        var ridStatusFile = _pathService.GetHarvestLibraryRidStatusFile(libraryName, rid);
        EnsureWithinHarvestOutput(ridStatusFile.FullPath);
        if (_cakeContext.FileExists(ridStatusFile))
        {
            _cakeContext.DeleteFile(ridStatusFile);
        }

        var ridLicenseRoot = _pathService.GetHarvestLibraryRidLicensesDir(libraryName, rid);
        EnsureWithinHarvestOutput(ridLicenseRoot.FullPath);
        if (_cakeContext.DirectoryExists(ridLicenseRoot))
        {
            _cakeContext.DeleteDirectory(ridLicenseRoot, RecursiveForceDelete);
        }
    }

    private void InvalidateCrossRidReceipts(string libraryName)
    {
        DeleteIfExists(_pathService.GetHarvestLibraryConsolidatedLicensesDir(libraryName));
        DeleteIfExists(_pathService.GetHarvestLibraryConsolidatedLicensesTempDir(libraryName));
        DeleteIfExists(_pathService.GetHarvestLibraryManifestFile(libraryName));
        DeleteIfExists(_pathService.GetHarvestLibraryManifestTempFile(libraryName));
        DeleteIfExists(_pathService.GetHarvestLibrarySummaryFile(libraryName));
        DeleteIfExists(_pathService.GetHarvestLibrarySummaryTempFile(libraryName));
    }

    private void DeleteIfExists(DirectoryPath path)
    {
        EnsureWithinHarvestOutput(path.FullPath);
        if (_cakeContext.DirectoryExists(path))
        {
            _cakeContext.DeleteDirectory(path, RecursiveForceDelete);
        }
    }

    private void DeleteIfExists(FilePath path)
    {
        EnsureWithinHarvestOutput(path.FullPath);
        if (_cakeContext.FileExists(path))
        {
            _cakeContext.DeleteFile(path);
        }
    }

    /// <summary>
    /// Defence-in-depth guard: every recursive-force-delete + file-delete dispatched from this
    /// repository must target a path under the harvest output root. Cheap insurance against a
    /// future <see cref="IPathService"/> bug that returns a path escaping the harvest tree —
    /// without this guard, a single misresolved path would trigger <c>rm -rf $repoRoot</c>.
    /// </summary>
    private void EnsureWithinHarvestOutput(string fullPath)
    {
        var harvestRoot = _pathService.HarvestOutput.FullPath;
        if (!fullPath.StartsWith(harvestRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException(
                $"HarvestStatusRepository refused to delete '{fullPath}': path falls outside the harvest output root '{harvestRoot}'. " +
                "This guard exists to prevent IPathService misresolution from escalating into a wider filesystem mutation.");
        }
    }

    private static readonly DeleteDirectorySettings RecursiveForceDelete = new() { Recursive = true, Force = true };
}
