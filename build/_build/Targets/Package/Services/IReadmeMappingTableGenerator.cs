using Build.Data.Manifest.Models;

namespace Build.Targets.Package.Services;

/// <summary>
/// Generator (G57) — upserts the README mapping table block during packing. Runs once per
/// packaging invocation (not per family) because the block reflects the manifest state, not
/// a single family's state.
/// </summary>
public interface IReadmeMappingTableGenerator
{
    public Task UpdateAsync(ManifestConfig manifestConfig, CancellationToken ct = default);
}
