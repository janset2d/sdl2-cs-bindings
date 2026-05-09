using Build.Shared.Manifest;

namespace Build.Validation.Packaging;

/// <summary>
/// Gates Pack-stage execution against ConsolidateHarvest output for the family's
/// library_ref. Throws <c>CakeException</c> with full diagnostics on failure
/// (gate semantics; no return value). Failure modes: harvest manifest missing,
/// consolidation receipt missing, zero successful RIDs, zero license entries,
/// payload subtree (runtimes/ or licenses/_consolidated/) missing or empty.
/// </summary>
public interface IHarvestReadinessValidator
{
    Task EnsureReadyAsync(PackageFamilyConfig family, CancellationToken ct);
}
