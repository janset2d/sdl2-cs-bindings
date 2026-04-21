using Build.Context.Models;
using Build.Domain.Preflight.Models;
using Build.Domain.Preflight.Results;
using NuGet.Versioning;

namespace Build.Domain.Preflight;

/// <summary>
/// Guardrail G54 (ADR-001 + ADR-003): every entry in the resolved per-family version mapping
/// must align with the family's upstream library major/minor from <c>manifest.json
/// library_manifests[].vcpkg_version</c>. Strict-minor alignment applies unconditionally —
/// each mapping entry is an explicit per-family assertion, not a shared scalar. Invoked in
/// two places: <c>PreflightTaskRunner</c> (pre-pipeline gate) and <c>ExplicitVersionProvider</c>
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

        foreach (var (requestedFamily, parsedFamilyVersion) in versions)
        {
            checks.Add(ValidateEntry(manifestConfig, requestedFamily, parsedFamilyVersion));
        }

        var validation = new UpstreamVersionAlignmentValidation(checks);

        return validation.HasErrors
            ? UpstreamVersionAlignmentResult.Fail(validation)
            : UpstreamVersionAlignmentResult.Pass(validation);
    }

    private static UpstreamVersionAlignmentCheck ValidateEntry(
        ManifestConfig manifestConfig,
        string requestedFamily,
        NuGetVersion parsedFamilyVersion)
    {
        var normalizedFamilyVersion = parsedFamilyVersion.ToNormalizedString();

        var family = manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
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

        var library = manifestConfig.LibraryManifests.SingleOrDefault(candidate =>
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
