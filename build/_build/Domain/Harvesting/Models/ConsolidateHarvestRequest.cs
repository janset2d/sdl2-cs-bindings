namespace Build.Domain.Harvesting.Models;

/// <summary>
/// ADR-003 §3.2 per-stage request marker for <c>ConsolidateHarvestTaskRunner</c>. The
/// consolidation step reads every per-RID status file under the harvest staging root and
/// writes the merged <c>harvest-manifest.json</c> + <c>licenses/_consolidated/</c> tree. All
/// paths derive from <c>IPathService</c>; the request is a parameterless marker that keeps
/// stage-runner contracts uniform (every runner takes a request + a cancellation token).
/// </summary>
public sealed record ConsolidateHarvestRequest;
