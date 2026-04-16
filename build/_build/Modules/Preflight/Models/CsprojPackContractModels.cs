using Cake.Core.IO;

namespace Build.Modules.Preflight.Models;

public enum CsprojPackContractCheckKind
{
    /// <summary>G7 — Native ProjectReference path resolves to manifest's native_project.</summary>
    NativeProjectReferencePathMatchesManifest,

    /// <summary>G1 — Native ProjectReference carries PrivateAssets="all".</summary>
    NativeProjectReferenceHasPrivateAssetsAll,

    /// <summary>G2 — Matching PackageReference to the Native PackageId exists.</summary>
    NativePackageReferenceExists,

    /// <summary>G3 — Matching PackageVersion uses bracket notation [...].</summary>
    NativePackageVersionUsesBracketNotation,

    /// <summary>G4 — csproj MinVerTagPrefix equals manifest's tag_prefix + "-".</summary>
    MinVerTagPrefixMatchesManifest,

    /// <summary>G5 — csproj declares the canonical Sdl{Major}{Role}FamilyVersion property.</summary>
    FamilyVersionPropertyDeclared,

    /// <summary>G8 — Family-version property uses canonical $(Version) chain with 0.0.0-restore sentinel fallback.</summary>
    FamilyVersionPropertyHasSentinelFallback,

    /// <summary>G6 — csproj PackageId matches canonical Janset.SDL{Major}.{Role}[.Native] convention.</summary>
    PackageIdMatchesCanonicalConvention,

    /// <summary>G17 — package_families[].depends_on references existing family identifiers.</summary>
    DependsOnReferencesExistingFamily,

    /// <summary>G18 — package_families[].library_ref references existing library_manifests[].name.</summary>
    LibraryRefReferencesExistingLibrary,

    /// <summary>Project file referenced from manifest does not exist on disk.</summary>
    CsprojFileExists,
}

public sealed record CsprojPackContractCheck(
    string FamilyIdentifier,
    string CsprojRelativePath,
    CsprojPackContractCheckKind Kind,
    bool IsValid,
    string? ExpectedValue,
    string? ActualValue,
    string? ErrorMessage)
{
    public bool IsError => !IsValid;
}

public sealed record CsprojPackContractValidation(IReadOnlyList<CsprojPackContractCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    public int CheckedCsprojs => Checks
        .Where(c => !string.IsNullOrEmpty(c.CsprojRelativePath))
        .Select(c => c.CsprojRelativePath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int CheckedFamilies => Checks
        .Select(c => c.FamilyIdentifier)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
