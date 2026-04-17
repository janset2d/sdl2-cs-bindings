using Cake.Core.IO;

namespace Build.Modules.Packaging.Models;

/// <summary>
/// Enumerates the post-S1 nuspec guardrails checked by <c>IPackageOutputValidator</c>.
/// Each constant maps 1:1 to a guardrail in
/// <c>docs/knowledge-base/release-guardrails.md</c>.
/// </summary>
public enum PackageValidationCheckKind
{
    /// <summary>G26/G27 — canonical metadata (id, version, authors, license, icon, repository commit).</summary>
    CanonicalMetadataMatches,

    /// <summary>G21 — within-family and cross-family dependencies emitted as bare minimum range.</summary>
    FamilyDependencyMinimumRange,

    /// <summary>G22 — nuspec dependency groups agree across every target framework.</summary>
    DependencyGroupsConsistentAcrossFrameworks,

    /// <summary>G23 — managed and native package <c>&lt;version&gt;</c> elements match.</summary>
    WithinFamilyVersionCoherence,

    /// <summary>G25 — managed symbol (.snupkg) package produced with valid contents.</summary>
    ManagedSymbolsPackageValid,

    /// <summary>Foundational load error — nuspec missing / unreadable / malformed.</summary>
    NuspecLoad,

    /// <summary>Project metadata completeness pre-check (TFMs / authors / license / icon present).</summary>
    ProjectMetadataComplete,
}

/// <summary>
/// A single guardrail observation produced by <c>IPackageOutputValidator</c>. Mirrors the
/// <c>CsprojPackContractCheck</c> shape so task-layer reporters can render a uniform table.
/// </summary>
public sealed record PackageValidationCheck(
    string FamilyIdentifier,
    FilePath? PackagePath,
    PackageValidationCheckKind Kind,
    bool IsValid,
    string? ExpectedValue,
    string? ActualValue,
    string? ErrorMessage)
{
    public bool IsError => !IsValid;
}

/// <summary>
/// Aggregate of every guardrail check produced for a single <c>Validate</c> invocation.
/// Carries the full report so the task layer can log per-check violations and operators
/// see the complete failure set instead of only the first tripped guardrail.
/// </summary>
public sealed record PackageValidation(IReadOnlyList<PackageValidationCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    public int CheckedFamilies => Checks
        .Select(check => check.FamilyIdentifier)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
