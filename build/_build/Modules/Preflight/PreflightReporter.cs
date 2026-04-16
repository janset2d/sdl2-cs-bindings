using Build.Modules.Contracts;
using Build.Modules.Preflight.Models;
using Cake.Core;
using Cake.Core.Diagnostics;

namespace Build.Modules.Preflight;

public sealed class PreflightReporter(ICakeContext cakeContext) : IPreflightReporter
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    private ICakeLog Log => _cakeContext.Log;

    public void ReportRunStart()
    {
        Log.Information("🔍 Running pre-flight checks...");
        Log.Information("ℹ️ Scope: version consistency + runtime strategy coherence.");
        Log.Information("ℹ️ Deferred to Stream C: package-family integrity, dynamic matrix, and CI artifact-flow gates.");
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

    public void ReportStrategyCoherence(StrategyCoherenceValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        foreach (var check in validation.Checks)
        {
            Log.Information("🔄 Checking strategy coherence for RID {0} ({1})...", check.Rid, check.Triplet);

            if (check.IsValid)
            {
                Log.Information("  ✅ Strategy coherence confirmed: {0}", check.ResolvedModel);
                continue;
            }

            Log.Error("  ❌ Strategy coherence mismatch for RID {0}: {1}", check.Rid, check.ErrorMessage);
        }

        Log.Information("");
        if (validation.HasErrors)
        {
            Log.Error("❌ Pre-flight check FAILED - Found strategy coherence mismatches");
            Log.Error("   Fix runtimes[].strategy and runtimes[].triplet alignment in manifest.json");
            return;
        }

        Log.Information("✅ Strategy coherence check PASSED - All {0} runtimes are coherent", validation.CheckedRuntimes);
    }
}
