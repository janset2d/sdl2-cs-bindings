using Build.Data.Harvest;
using Build.Host.Paths;
using Build.Data.Manifest.Models;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

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

/// <inheritdoc />
public sealed class HarvestReadinessValidator(
    ICakeContext cakeContext,
    IPathService pathService,
    IHarvestManifestRepository harvestManifestRepository,
    ICakeLog log) : IHarvestReadinessValidator
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly IHarvestManifestRepository _harvestManifestRepository = harvestManifestRepository ?? throw new ArgumentNullException(nameof(harvestManifestRepository));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public async Task EnsureReadyAsync(PackageFamilyConfig family, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(family);

        var manifestPath = _pathService.GetHarvestLibraryManifestFile(family.LibraryRef);

        if (!_cakeContext.FileExists(manifestPath))
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because harvest manifest '{manifestPath.FullPath}' is missing. Run Harvest + ConsolidateHarvest for library '{family.LibraryRef}' first.");
        }

        var harvestManifest = await _harvestManifestRepository.LoadManifestAsync(family.LibraryRef, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        AssertConsolidationReceiptValid(family, manifestPath, harvestManifest);
        AssertPayloadSubtreesPopulated(family);

        var successfulRids = harvestManifest.Rids
            .Where(ridStatus => ridStatus.Success)
            .Select(ridStatus => ridStatus.Rid)
            .Order(StringComparer.OrdinalIgnoreCase);

        _log.Information("Family '{0}' will pack harvest payload for successful RIDs: {1}", family.Name, string.Join(", ", successfulRids));
    }

    private void AssertConsolidationReceiptValid(PackageFamilyConfig family, FilePath manifestPath, HarvestManifest harvestManifest)
    {
        if (harvestManifest.Summary.SuccessfulRids == 0)
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because harvest manifest '{manifestPath.FullPath}' reports zero successful RIDs.");
        }

        // H1 gate (G51 partner): harvest-manifest must carry a consolidation receipt and
        // that receipt must declare a non-zero license payload. Absence means either the
        // manifest is pre-H1 legacy (no consolidation section) OR Consolidate was invoked
        // against zero successful RIDs. Either way, Pack cannot proceed — the native csproj
        // reads from licenses/_consolidated/ and would ship a nupkg with no attribution.
        if (harvestManifest.Consolidation is null)
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because harvest manifest '{manifestPath.FullPath}' lacks a consolidation receipt. " +
                "Re-run ConsolidateHarvest — Harvest invalidates the consolidated view on every RID run, and only Consolidate regenerates it with the license-union receipt attached.");
        }

        if (!harvestManifest.Consolidation.LicensesConsolidated || harvestManifest.Consolidation.LicenseEntriesCount == 0)
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because the consolidation receipt in '{manifestPath.FullPath}' reports zero license entries " +
                $"(LicensesConsolidated={harvestManifest.Consolidation.LicensesConsolidated}, LicenseEntriesCount={harvestManifest.Consolidation.LicenseEntriesCount}). " +
                "A native pack without third-party license attribution breaks the compliance surface — re-run Harvest + ConsolidateHarvest and confirm at least one successful RID contributed license files.");
        }

        if (harvestManifest.Consolidation.DivergentLicenses.Count > 0)
        {
            _log.Warning(
                "Family '{0}' pack will ship {1} divergent license entries across RIDs (auditable via harvest-manifest.json).",
                family.Name,
                harvestManifest.Consolidation.DivergentLicenses.Count);
        }
    }

    /// <summary>
    /// Post-H1 gate: validate the exact subtrees the native csproj consumes
    /// (see <c>src/native/Directory.Build.props</c>). Runtimes lives at the library-level
    /// <c>runtimes/</c> dir; third-party license attribution comes exclusively from
    /// <c>licenses/_consolidated/</c> produced by <c>ConsolidateHarvestTask</c>'s staged
    /// swap. Top-level <c>licenses/</c> is NOT checked — per-RID evidence under
    /// <c>licenses/{rid}/</c> would satisfy a naive parent-level non-empty check while
    /// shipping an empty license payload on the pack side.
    /// </summary>
    private void AssertPayloadSubtreesPopulated(PackageFamilyConfig family)
    {
        var payloadDirectories = new[]
        {
            _pathService.GetHarvestLibraryRuntimesDir(family.LibraryRef),
            _pathService.GetHarvestLibraryConsolidatedLicensesDir(family.LibraryRef),
        };

        foreach (var payloadSource in payloadDirectories)
        {
            if (!_cakeContext.DirectoryExists(payloadSource))
            {
                throw new CakeException(
                    $"Package task cannot pack family '{family.Name}' because harvest payload directory '{payloadSource.FullPath}' is missing.");
            }

            var payloadFiles = _cakeContext.GetFiles($"{payloadSource}/**/*");
            if (payloadFiles.Count == 0)
            {
                throw new CakeException(
                    $"Package task cannot pack family '{family.Name}' because harvest payload directory '{payloadSource.FullPath}' is empty.");
            }
        }
    }
}
