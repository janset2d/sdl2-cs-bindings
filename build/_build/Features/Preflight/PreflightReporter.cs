using Build.Results;
using Build.Shared.Packaging;
using Build.Shared.Versioning;
using Cake.Core;
using Cake.Core.Diagnostics;

namespace Build.Features.Preflight;

public sealed class PreflightReporter(ICakeContext cakeContext)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    private ICakeLog Log => _cakeContext.Log;

    public void ReportRunStart()
    {
        Log.Information("🔍 Running pre-flight checks...");
        Log.Information("ℹ️ Scope: version consistency + hybrid-static overlay coherence + core identity + upstream version alignment + csproj pack contract.");
        // Dynamic matrix and CI artifact-flow gates are checked downstream in the CI pipeline.
    }

    public void ReportVersionConsistency(VersionConsistencyValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        Log.Information("Checking manifest: {0}", validation.ManifestPath);
        Log.Information("Checking vcpkg manifest: {0}", validation.VcpkgManifestPath);

        foreach (var check in validation.Checks)
        {
            Log.Information("🔄 Checking {0} ({1})...", check.LibraryName, check.VcpkgName);

            switch (check.Status)
            {
                case LibraryVersionCheckStatus.Match:
                    Log.Information("  ✅ Version consistency confirmed: {0}#{1}", check.ManifestVersion, check.ManifestPortVersion);
                    break;
                case LibraryVersionCheckStatus.MissingOverride:
                    Log.Information("  ℹ️  No vcpkg override found - using builtin-baseline");
                    break;
                case LibraryVersionCheckStatus.InvalidManifestVersion:
                    Log.Error("  ❌ Invalid manifest version for {0}: {1}", check.LibraryName, check.ManifestVersion);
                    break;
                case LibraryVersionCheckStatus.InvalidOverrideVersion:
                    Log.Error("  ❌ Invalid vcpkg override version for {0}: {1}", check.LibraryName, check.OverrideVersion ?? "<null>");
                    break;
                case LibraryVersionCheckStatus.VersionMismatch:
                    Log.Error("  ❌ Version mismatch for {0}:", check.LibraryName);
                    Log.Error("     manifest.json: {0}", check.ManifestVersion);
                    Log.Error("     vcpkg.json:    {0}", check.OverrideVersion);
                    break;
                case LibraryVersionCheckStatus.PortVersionMismatch:
                    Log.Error("  ❌ Port version mismatch for {0}:", check.LibraryName);
                    Log.Error("     manifest.json: {0}", check.ManifestPortVersion);
                    Log.Error("     vcpkg.json:    {0}", check.OverridePortVersion ?? 0);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported library version check status '{check.Status}'.");
            }
        }

        Log.Information("");
        if (validation.HasErrors)
        {
            Log.Error("❌ Pre-flight check FAILED - Found version inconsistencies");
            Log.Error("   Please update manifest.json or vcpkg.json to align versions");
            Log.Error("   The manifest.json should be the single source of truth for intended versions");
            return;
        }

        Log.Information("✅ Pre-flight check PASSED - All {0} libraries have consistent versions", validation.CheckedLibraries);
        Log.Information("   manifest.json and vcpkg.json are properly aligned");
    }

    public void ReportHybridStaticOverlay(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        Log.Information("");
        Log.Information("🔄 Checking hybrid-static overlay coherence (G16)...");

        foreach (var error in report.Errors)
        {
            Log.Error("  ❌ {0}: {1}", error.Name, error.Message);
        }

        Log.Information("");
        if (!report.IsValid)
        {
            Log.Error("❌ Pre-flight check FAILED - {0} hybrid-static overlay violation(s) detected (G16)", report.Errors.Count);
            Log.Error("   Verify manifest.runtimes[].triplet ends with '-hybrid' AND vcpkg-overlay-triplets/<triplet>.cmake exists.");
            return;
        }

        Log.Information("✅ Hybrid-static overlay check PASSED - all runtime triplets have valid hybrid overlay files");
    }

    public void ReportCoreLibraryIdentity(CoreLibraryIdentityValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        var check = validation.Check;
        Log.Information("🔄 Checking core library identity coherence...");

        switch (check.Status)
        {
            case CoreLibraryIdentityCheckStatus.Match:
                Log.Information("  ✅ Core library identity confirmed: '{0}' (library_manifests + packaging_config aligned)", check.ManifestCoreVcpkgName);
                break;
            case CoreLibraryIdentityCheckStatus.InvalidCoreLibraryManifestCount:
                Log.Error("  ❌ library_manifests[core_lib=true] count is {0} (expected exactly 1)", check.CoreLibraryManifestCount);
                break;
            case CoreLibraryIdentityCheckStatus.PackagingConfigCoreLibraryMismatch:
                Log.Error("  ❌ Core library identity drift:");
                Log.Error("     library_manifests[core_lib=true].vcpkg_name: {0}", check.ManifestCoreVcpkgName ?? "<null>");
                Log.Error("     packaging_config.core_library:                {0}", check.PackagingConfigCoreLibrary);
                break;
            default:
                throw new InvalidOperationException($"Unsupported core library identity check status '{check.Status}'.");
        }

        Log.Information("");
        if (validation.HasErrors)
        {
            Log.Error("❌ Pre-flight check FAILED - Core library identity drift detected (G49)");
            Log.Error("   Align library_manifests[core_lib=true].vcpkg_name with packaging_config.core_library in manifest.json");
            return;
        }

        Log.Information("✅ Core library identity check PASSED - manifest declares a single coherent core library");
    }

    public void ReportUpstreamVersionAlignment(UpstreamVersionAlignmentValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        Log.Information("");
        Log.Information("🔄 Checking upstream version alignment (G54)...");

        foreach (var check in validation.Checks)
        {
            if (check.Status == UpstreamVersionAlignmentCheckStatus.Match)
            {
                Log.Information(
                    "  ✅ {0}: family version '{1}' aligns with upstream '{2}'.",
                    check.FamilyIdentifier,
                    check.FamilyVersion,
                    check.UpstreamVersion);
            }
            else
            {
                Log.Error(
                    "  ❌ {0}: {1}",
                    check.FamilyIdentifier,
                    check.ErrorMessage ?? "unknown upstream alignment failure");
            }
        }

        Log.Information("");
        if (validation.HasErrors)
        {
            Log.Error("❌ Pre-flight check FAILED - Upstream version alignment violations detected (G54)");
            Log.Error("   Align --family-version major/minor with manifest library_manifests[].vcpkg_version major/minor.");
            return;
        }

        Log.Information("✅ Upstream version alignment check PASSED - {0} family row(s) evaluated", validation.CheckedFamilies);
    }

    public void ReportCsprojPackContract(CsprojPackContractValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        Log.Information("");
        Log.Information("🔍 Csproj pack contract — checking {0} families across {1} csproj(s)...", validation.CheckedFamilies, validation.CheckedCsprojs);

        var checksByCsproj = validation.Checks
            .GroupBy(c => string.IsNullOrEmpty(c.CsprojRelativePath) ? c.FamilyIdentifier : c.CsprojRelativePath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in checksByCsproj)
        {
            var allValid = group.All(c => c.IsValid);
            var symbol = allValid ? "✅" : "❌";
            Log.Information("  {0} {1}", symbol, group.Key);

            foreach (var check in group.Where(c => !c.IsValid))
            {
                Log.Error("       [{0}] {1}", check.Kind, check.ErrorMessage);
                Log.Verbose("       expected: {0}", check.ExpectedValue ?? "<n/a>");
                Log.Verbose("       actual:   {0}", check.ActualValue ?? "<n/a>");
            }
        }

        Log.Information("");
        if (validation.HasErrors)
        {
            var failedCount = validation.Checks.Count(c => !c.IsValid);
            Log.Error("❌ Pre-flight check FAILED - {0} csproj pack contract violation(s) detected", failedCount);
            Log.Error("   Review the canonical pack-contract rules (G6, G7, G17, G18) for details.");
            Log.Error("   Review the family identifier conventions for family name format guidance.");
            return;
        }

        Log.Information("✅ Csproj pack contract check PASSED - all {0} families conform to canonical shape", validation.CheckedFamilies);
    }

    public void ReportG58CrossFamilyResolvability(G58CrossFamilyValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        Log.Information("");
        Log.Information("🔄 Checking cross-family dependency resolvability (G58)...");

        if (validation.Checks.Count == 0)
        {
            Log.Information("  ℹ️ No cross-family dependencies in scope — no G58 checks to run.");
            Log.Information("");
            Log.Information("✅ G58 cross-family dependency resolvability PASSED - no dependencies to resolve");
            return;
        }

        foreach (var check in validation.Checks)
        {
            if (check.IsError)
            {
                Log.Error(
                    "  ❌ {0} → {1}: {2}",
                    check.DependentFamily,
                    check.DependencyFamily,
                    check.ErrorMessage ?? "unknown G58 failure");
            }
            else
            {
                Log.Information(
                    "  ✅ {0} → {1} ({2}, lower bound {3}).",
                    check.DependentFamily,
                    check.DependencyFamily,
                    check.Status,
                    check.ExpectedMinVersion);
            }
        }

        Log.Information("");
        if (validation.HasErrors)
        {
            var failedCount = validation.Checks.Count(check => check.IsError);
            Log.Error("❌ Pre-flight check FAILED - {0} G58 cross-family resolvability violation(s) detected", failedCount);
            Log.Error("   Either include the dependency family in --explicit-version / --scope, or use the Pack-stage feed-probe surface when available.");
            return;
        }

        Log.Information("✅ G58 cross-family dependency resolvability PASSED - {0} dependency/dependencies all resolvable within scope", validation.Checks.Count);
    }
}
