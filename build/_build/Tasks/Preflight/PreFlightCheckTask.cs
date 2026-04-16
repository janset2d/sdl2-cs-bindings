/*
 * SPDX-FileCopyrightText: 2025 James Williamson <james@semick.dev>
 * SPDX-License-Identifier: MIT
 */

#pragma warning disable MA0045

using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Build.Context;
using Build.Context.Models;
using Build.Modules.Strategy;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks.Preflight;

/// <summary>
/// Pre-flight validation task that checks version consistency between manifest.json and vcpkg.json,
/// and validates strategy coherence for runtime entries in manifest.json.
/// This task ensures that the intended native library versions in manifest.json match
/// the actual vcpkg overrides before starting any build operations.
/// </summary>
[TaskName("PreFlightCheck")]
[TaskDescription("Validates manifest-vcpkg version consistency and runtime strategy coherence (partial gate)")]
public sealed class PreFlightCheckTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.Log.Information("🔍 Running pre-flight checks...");
        context.Log.Information("ℹ️ Scope: version consistency + runtime strategy coherence.");
        context.Log.Information("ℹ️ Deferred to Stream C: package-family integrity, dynamic matrix, and CI artifact-flow gates.");

        var manifest = ValidateVersionConsistency(context);
        ValidateStrategyCoherence(context, manifest);
    }

    private static ManifestConfig ValidateVersionConsistency(BuildContext context)
    {
        var manifestPath = context.Paths.GetManifestFile();
        var vcpkgManifestPath = context.Paths.RepoRoot.CombineWithFilePath("vcpkg.json");

        context.Log.Information("Checking manifest: {0}", manifestPath);
        context.Log.Information("Checking vcpkg manifest: {0}", vcpkgManifestPath);

        // Load and parse manifest.json
        var manifest = LoadManifestFile(context, manifestPath);

        // Load and parse vcpkg.json
        var vcpkgManifest = LoadVcpkgManifestFile(context, vcpkgManifestPath);

        // Validate consistency
        ValidateLibraryVersions(context, manifest, vcpkgManifest);

        return manifest;
    }

    private static ManifestConfig LoadManifestFile(BuildContext context, FilePath manifestPath)
    {
        try
        {
            return context.ToJson<ManifestConfig>(manifestPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"❌ Failed to load manifest.json: {ex.Message}", ex);
        }
    }

    private static VcpkgManifest LoadVcpkgManifestFile(BuildContext context, FilePath vcpkgManifestPath)
    {
        try
        {
            return context.ToJson<VcpkgManifest>(vcpkgManifestPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"❌ Failed to load vcpkg.json: {ex.Message}", ex);
        }
    }

    private static void ValidateLibraryVersions(BuildContext context, ManifestConfig manifest, VcpkgManifest vcpkgManifest)
    {
        // Create lookup of vcpkg overrides
        var vcpkgOverrides = CreateVcpkgOverrideLookup(vcpkgManifest);

        // Validate each library in manifest
        var validationResults = ValidateEachLibrary(context, manifest, vcpkgOverrides);

        // Report final results
        ReportValidationResults(context, validationResults);
    }

    private static Dictionary<string, VcpkgOverride> CreateVcpkgOverrideLookup(VcpkgManifest vcpkgManifest)
    {
        if (vcpkgManifest.Overrides == null)
        {
            return new Dictionary<string, VcpkgOverride>(StringComparer.Ordinal);
        }

        return vcpkgManifest.Overrides.ToDictionary(o => o.Name, o => o, StringComparer.Ordinal);
    }

    private static void ValidateStrategyCoherence(BuildContext context, ManifestConfig manifest)
    {
        if (manifest.Runtimes.Count == 0)
        {
            throw new InvalidOperationException(
                "manifest.json requires a non-empty runtimes section for strategy coherence validation.");
        }

        var results = ValidateEachRuntime(context, manifest.Runtimes);
        ReportStrategyValidationResults(context, results);
    }

    private static StrategyValidationResults ValidateEachRuntime(BuildContext context, IImmutableList<RuntimeInfo> runtimes)
    {
        var hasErrors = false;
        var checkedRuntimes = 0;

        foreach (var runtime in runtimes)
        {
            checkedRuntimes++;
            context.Log.Information("🔄 Checking strategy coherence for RID {0} ({1})...", runtime.Rid, runtime.Triplet);

            try
            {
                var model = StrategyResolver.Resolve(runtime);
                context.Log.Information("  ✅ Strategy coherence confirmed: {0}", model);
            }
            catch (InvalidOperationException ex)
            {
                hasErrors = true;
                context.Log.Error("  ❌ Strategy coherence mismatch for RID {0}: {1}", runtime.Rid, ex.Message);
            }
        }

        return new StrategyValidationResults(hasErrors, checkedRuntimes);
    }

    private static ValidationResults ValidateEachLibrary(BuildContext context, ManifestConfig manifest, Dictionary<string, VcpkgOverride> vcpkgOverrides)
    {
        var hasErrors = false;
        var checkedLibraries = 0;

        foreach (var library in manifest.LibraryManifests)
        {
            checkedLibraries++;
            context.Log.Information("🔄 Checking {0} ({1})...", library.Name, library.VcpkgName);

            if (!vcpkgOverrides.TryGetValue(library.VcpkgName, out var vcpkgOverride))
            {
                // No override means using builtin-baseline, which is acceptable
                context.Log.Information("  ℹ️  No vcpkg override found - using builtin-baseline");
                continue;
            }

            var libraryValidationResult = ValidateSingleLibrary(context, library, vcpkgOverride);
            if (!libraryValidationResult)
            {
                hasErrors = true;
            }
        }

        return new ValidationResults(hasErrors, checkedLibraries);
    }

    private static bool ValidateSingleLibrary(BuildContext context, LibraryManifest library, VcpkgOverride vcpkgOverride)
    {
        // Compare versions (Major.Minor.Patch only)
        var manifestVersion = ParseSemanticVersion(library.VcpkgVersion);
        var vcpkgVersion = ParseSemanticVersion(vcpkgOverride.Version);

        var versionMatch = manifestVersion.Major == vcpkgVersion.Major &&
                           manifestVersion.Minor == vcpkgVersion.Minor &&
                           manifestVersion.Patch == vcpkgVersion.Patch;

        var portVersionMatch = library.VcpkgPortVersion == (vcpkgOverride.PortVersion ?? 0);

        if (!versionMatch)
        {
            LogVersionMismatch(context, library, vcpkgOverride);
            return false;
        }

        if (!portVersionMatch)
        {
            LogPortVersionMismatch(context, library, vcpkgOverride);
            return false;
        }

        context.Log.Information("  ✅ Version consistency confirmed: {0}#{1}", library.VcpkgVersion, library.VcpkgPortVersion);
        return true;
    }

    private static void LogVersionMismatch(BuildContext context, LibraryManifest library, VcpkgOverride vcpkgOverride)
    {
        context.Log.Error("  ❌ Version mismatch for {0}:", library.Name);
        context.Log.Error("     manifest.json: {0}", library.VcpkgVersion);
        context.Log.Error("     vcpkg.json:    {0}", vcpkgOverride.Version);
    }

    private static void LogPortVersionMismatch(BuildContext context, LibraryManifest library, VcpkgOverride vcpkgOverride)
    {
        context.Log.Error("  ❌ Port version mismatch for {0}:", library.Name);
        context.Log.Error("     manifest.json: {0}", library.VcpkgPortVersion);
        context.Log.Error("     vcpkg.json:    {0}", vcpkgOverride.PortVersion ?? 0);
    }

    private static void ReportValidationResults(BuildContext context, ValidationResults results)
    {
        context.Log.Information("");
        if (results.HasErrors)
        {
            context.Log.Error("❌ Pre-flight check FAILED - Found version inconsistencies");
            context.Log.Error("   Please update manifest.json or vcpkg.json to align versions");
            context.Log.Error("   The manifest.json should be the single source of truth for intended versions");
            throw new InvalidOperationException("Version consistency validation failed");
        }

        context.Log.Information("✅ Pre-flight check PASSED - All {0} libraries have consistent versions", results.CheckedLibraries);
        context.Log.Information("   manifest.json and vcpkg.json are properly aligned");
    }

    private static void ReportStrategyValidationResults(BuildContext context, StrategyValidationResults results)
    {
        context.Log.Information("");
        if (results.HasErrors)
        {
            context.Log.Error("❌ Pre-flight check FAILED - Found strategy coherence mismatches");
            context.Log.Error("   Fix runtimes[].strategy and runtimes[].triplet alignment in manifest.json");
            throw new InvalidOperationException("Strategy coherence validation failed");
        }

        context.Log.Information("✅ Strategy coherence check PASSED - All {0} runtimes are coherent", results.CheckedRuntimes);
    }

    /// <summary>
    /// Parses a semantic version string to extract Major.Minor.Patch components.
    /// Ignores build metadata and pre-release suffixes.
    /// </summary>
    internal static (int Major, int Minor, int Patch) ParseSemanticVersion(string version)
    {
        // Remove any build metadata (+something) or pre-release (-something)
        var cleanVersion = version.Split(['+', '-'], 2)[0];

        var parts = cleanVersion.Split('.');
        if (parts.Length < 3 ||
            !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(parts[2], CultureInfo.InvariantCulture, out var patch))
        {
            throw new ArgumentException($"Invalid semantic version format: {version}", nameof(version));
        }

        return (major, minor, patch);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ValidationResults(bool HasErrors, int CheckedLibraries);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct StrategyValidationResults(bool HasErrors, int CheckedRuntimes);
}

/// <summary>
/// Minimal model for vcpkg.json structure needed for version validation
/// </summary>
public record VcpkgManifest
{
    [JsonPropertyName("overrides")]
    public IImmutableList<VcpkgOverride>? Overrides { get; init; }
}

public record VcpkgOverride
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("port-version")]
    public int? PortVersion { get; init; }
}
