namespace Build.Features.Preflight;

public enum CsprojPackContractCheckKind
{
    /// <summary>G7 — Native ProjectReference path resolves to manifest's native_project.</summary>
    NativeProjectReferencePathMatchesManifest,

    /// <summary>G4 — csproj MinVerTagPrefix equals manifest's tag_prefix + "-".</summary>
    MinVerTagPrefixMatchesManifest,

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
