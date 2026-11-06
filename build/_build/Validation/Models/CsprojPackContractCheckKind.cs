namespace Build.Validation.Models;

public enum CsprojPackContractCheckKind
{
    /// <summary>G7 — Native ProjectReference path resolves to manifest's native_project.</summary>
    NativeProjectReferencePathMatchesManifest,

    /// <summary>G6 — csproj PackageId matches canonical Janset.SDL{Major}.{Role}[.Native] convention.</summary>
    PackageIdMatchesCanonicalConvention,

    /// <summary>G17 — package_families[].depends_on references existing family identifiers.</summary>
    DependsOnReferencesExistingFamily,

    /// <summary>G18 — package_families[].library_ref references existing library_manifests[].name.</summary>
    LibraryRefReferencesExistingLibrary,

    /// <summary>Project file referenced from manifest does not exist on disk.</summary>
    CsprojFileExists,
}
