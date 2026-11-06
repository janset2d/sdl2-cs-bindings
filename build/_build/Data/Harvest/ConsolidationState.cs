using System.Text.Json.Serialization;

namespace Build.Data.Harvest;

/// <summary>
/// Consolidation receipt recording the post-H1 license-union work performed by
/// <c>ConsolidateHarvestTask</c>. Lives inside <see cref="HarvestManifest"/> rather than
/// as a separate file so the existing "harvest-manifest.json present → Consolidate ran"
/// gate in <c>HarvestReadinessValidator</c> covers both manifest generation AND license
/// consolidation with a single file-existence check.
/// </summary>
public record ConsolidationState
{
    /// <summary>
    /// <c>true</c> when <c>licenses/_consolidated/</c> was populated by at least one
    /// successful RID's license evidence. <c>false</c> when zero RIDs contributed (valid
    /// shape, but Package should still refuse because there's nothing to pack).
    /// </summary>
    [JsonPropertyName("licenses_consolidated")]
    public required bool LicensesConsolidated { get; init; }

    /// <summary>
    /// Number of unique (package, fileName) license entries written under
    /// <c>licenses/_consolidated/</c>. Each entry corresponds to either a canonical
    /// identical-across-RIDs file or a set of per-RID divergent variants.
    /// </summary>
    [JsonPropertyName("license_entries_count")]
    public required int LicenseEntriesCount { get; init; }

    /// <summary>
    /// Audit record of license files whose content differed across contributing RIDs.
    /// Empty when all contributing RIDs agreed on byte-identical license text for every
    /// (package, fileName). Non-empty entries indicate attribution variance that operators
    /// should be aware of — both the canonical unsuffixed path and per-RID suffixed
    /// variants are omitted / written according to the consolidation writer's divergence rules.
    /// </summary>
    [JsonPropertyName("divergent_licenses")]
    public required IReadOnlyList<DivergentLicense> DivergentLicenses { get; init; }
}
