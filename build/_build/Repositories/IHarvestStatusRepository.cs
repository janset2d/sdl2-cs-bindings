using Build.Targets.Harvest.Models;

namespace Build.Repositories;

/// <summary>
/// File-backed repository for the per-RID rid-status JSON contract written by HarvestTask
/// and consumed by ConsolidateHarvestTask. Three operations: invalidate stale per-RID payload
/// + cross-RID consolidated receipts before a fresh harvest run, persist a success-flagged
/// status record after a clean harvest, persist a failure-flagged status record so
/// consolidation observes the per-RID error rather than treating a missing rid-status as
/// silent success. <see cref="DeploymentStatistics"/> currently lives under
/// <c>Build.Targets.Harvest.Models</c>; promotion to <c>Build.Harvesting</c> for clean
/// cross-namespace layering is parking-lot follow-up.
/// </summary>
public interface IHarvestStatusRepository
{
    void Invalidate(string libraryName, CancellationToken ct = default);

    Task WriteSuccessAsync(string libraryName, DeploymentStatistics statistics, CancellationToken ct = default);

    Task WriteErrorAsync(string libraryName, string errorMessage, CancellationToken ct = default);
}
