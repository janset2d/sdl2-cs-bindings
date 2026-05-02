using Build.Shared.Manifest;
using NuGet.Versioning;

namespace Build.Features.Preflight;

/// <summary>
/// Guardrail G54 (ADR-001 + ADR-003): every entry in the resolved per-family version mapping
/// must align with the family's upstream library major/minor from <c>manifest.json
/// library_manifests[].vcpkg_version</c>. Strict-minor alignment applies unconditionally —
/// each mapping entry is an explicit per-family assertion, not a shared scalar. Invoked in
/// two places: <c>PreflightPipeline</c> (pre-pipeline gate) and <c>ExplicitVersionProvider</c>
/// (provider-entry guard). Same implementation, two call sites — keeps G54 semantics identical
/// regardless of who supplies the mapping.
/// </summary>
public sealed class UpstreamVersionAlignmentValidator : IUpstreamVersionAlignmentValidator
{
    public UpstreamVersionAlignmentResult Validate(
        ManifestConfig manifestConfig,
        IReadOnlyDictionary<string, NuGetVersion> versions)
    {
        ArgumentNullException.ThrowIfNull(manifestConfig);
        ArgumentNullException.ThrowIfNull(versions);

        var checks = new List<UpstreamVersionAlignmentCheck>(versions.Count);
        checks.AddRange(ValidateUniqueManifestKeys(manifestConfig));

        if (checks.Count > 0)
        {
            return UpstreamVersionAlignmentResult.Fail(new UpstreamVersionAlignmentValidation(checks));
        }

        foreach (var (requestedFamily, parsedFamilyVersion) in versions)
        {
            checks.Add(ValidateEntry(manifestConfig, requestedFamily, parsedFamilyVersion));
        }

        var validation = new UpstreamVersionAlignmentValidation(checks);

        return validation.HasErrors
            ? UpstreamVersionAlignmentResult.Fail(validation)
            : UpstreamVersionAlignmentResult.Pass(validation);
    }

    private static List<UpstreamVersionAlignmentCheck> ValidateUniqueManifestKeys(ManifestConfig manifestConfig)
    {
        var checks = new List<UpstreamVersionAlignmentCheck>();

        checks.AddRange(manifestConfig.PackageFamilies
            .Where(family => !string.IsNullOrWhiteSpace(family.Name))
            .GroupBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: group.Key,
                LibraryRef: null,
                FamilyVersion: "<not evaluated>",
                UpstreamVersion: null,
                Status: UpstreamVersionAlignmentCheckStatus.DuplicateFamilyName,
                ErrorMessage: $"G54: manifest package_families[] contains duplicate family name '{group.Key}' (matched case-insensitively). Family names must be unique before upstream version alignment can run.")));

        checks.AddRange(manifestConfig.LibraryManifests
            .Where(library => !string.IsNullOrWhiteSpace(library.Name))
            .GroupBy(library => library.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: "<manifest>",
                LibraryRef: group.Key,
                FamilyVersion: "<not evaluated>",
                UpstreamVersion: null,
                Status: UpstreamVersionAlignmentCheckStatus.DuplicateLibraryName,
                ErrorMessage: $"G54: manifest library_manifests[] contains duplicate library name '{group.Key}' (matched case-insensitively). Library names must be unique before upstream version alignment can run.")));

        return checks;
    }

    private static UpstreamVersionAlignmentCheck ValidateEntry(
        ManifestConfig manifestConfig,
        string requestedFamily,
        NuGetVersion parsedFamilyVersion)
    {
        var normalizedFamilyVersion = parsedFamilyVersion.ToNormalizedString();

        var family = manifestConfig.PackageFamilies.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, requestedFamily, StringComparison.OrdinalIgnoreCase));

        if (family is null)
        {
            return new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: requestedFamily,
                LibraryRef: null,
                FamilyVersion: normalizedFamilyVersion,
                UpstreamVersion: null,
                Status: UpstreamVersionAlignmentCheckStatus.FamilyNotFound,
                ErrorMessage: $"G54: family '{requestedFamily}' was not found in manifest package_families[].");
        }

        var library = manifestConfig.LibraryManifests.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (library is null)
        {
            return new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: family.Name,
                LibraryRef: family.LibraryRef,
                FamilyVersion: normalizedFamilyVersion,
                UpstreamVersion: null,
                Status: UpstreamVersionAlignmentCheckStatus.LibraryRefNotFound,
                ErrorMessage:
                $"G54: family '{family.Name}' library_ref '{family.LibraryRef}' does not resolve to any library_manifests[].name entry.");
        }

        if (!NuGetVersion.TryParse(library.VcpkgVersion, out var upstreamVersion))
        {
            return new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: family.Name,
                LibraryRef: family.LibraryRef,
                FamilyVersion: normalizedFamilyVersion,
                UpstreamVersion: library.VcpkgVersion,
                Status: UpstreamVersionAlignmentCheckStatus.InvalidUpstreamVersion,
                ErrorMessage:
                $"G54: manifest library '{library.Name}' has non-semantic vcpkg_version '{library.VcpkgVersion}'.");
        }

        if (parsedFamilyVersion.Major != upstreamVersion.Major)
        {
            return new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: family.Name,
                LibraryRef: family.LibraryRef,
                FamilyVersion: normalizedFamilyVersion,
                UpstreamVersion: library.VcpkgVersion,
                Status: UpstreamVersionAlignmentCheckStatus.VersionMismatch,
                ErrorMessage:
                $"G54: family '{family.Name}' version '{parsedFamilyVersion}' major ({parsedFamilyVersion.Major}) does not match upstream '{library.VcpkgVersion}' major ({upstreamVersion.Major}).");
        }

        if (parsedFamilyVersion.Minor != upstreamVersion.Minor)
        {
            return new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: family.Name,
                LibraryRef: family.LibraryRef,
                FamilyVersion: normalizedFamilyVersion,
                UpstreamVersion: library.VcpkgVersion,
                Status: UpstreamVersionAlignmentCheckStatus.VersionMismatch,
                ErrorMessage:
                $"G54: family '{family.Name}' version '{parsedFamilyVersion}' minor ({parsedFamilyVersion.Minor}) does not match upstream '{library.VcpkgVersion}' minor ({upstreamVersion.Minor}).");
        }

        return new UpstreamVersionAlignmentCheck(
            FamilyIdentifier: family.Name,
            LibraryRef: family.LibraryRef,
            FamilyVersion: normalizedFamilyVersion,
            UpstreamVersion: library.VcpkgVersion,
            Status: UpstreamVersionAlignmentCheckStatus.Match,
            ErrorMessage: null);
    }
}
