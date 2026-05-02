namespace Build.Features.Harvesting;

/// <summary>
/// Request marker for <c>ConsolidateHarvestPipeline</c>.
/// Consolidation reads every per-RID status file under the harvest staging root and writes
/// the merged <c>harvest-manifest.json</c> plus <c>licenses/_consolidated/</c>. Paths derive
/// from <c>IPathService</c>, so the request itself is parameterless.
/// </summary>
public sealed record ConsolidateHarvestRequest;
