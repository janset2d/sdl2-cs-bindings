using Build.Targets.Harvest.Models;

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
    public void Invalidate(string libraryName, CancellationToken ct = default);

    public Task WriteSuccessAsync(string libraryName, DeploymentStatistics statistics, CancellationToken ct = default);

    public Task WriteErrorAsync(string libraryName, string errorMessage, CancellationToken ct = default);
}
