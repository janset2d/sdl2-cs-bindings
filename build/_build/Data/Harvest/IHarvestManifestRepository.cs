namespace Build.Data.Harvest;

/// <summary>
/// File-backed repository for the consolidated harvest JSON contracts produced by
/// ConsolidateHarvest and consumed by Package. Owns rid-status reads plus staged manifest /
/// summary writes; target services own aggregation and license-union policy.
/// </summary>
public interface IHarvestManifestRepository
{
    public Task<IReadOnlyList<RidHarvestStatus>?> LoadRidStatusesAsync(string libraryName, CancellationToken ct = default);

    public Task<HarvestManifest> LoadManifestAsync(string libraryName, CancellationToken ct = default);

    public Task WriteManifestTempAsync(string libraryName, HarvestManifest manifest, CancellationToken ct = default);
}
