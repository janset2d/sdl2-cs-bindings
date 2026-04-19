using System.Diagnostics.CodeAnalysis;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Preflight.Models;
using Build.Domain.Preflight.Results;
using NuGet.Versioning;

namespace Build.Domain.Preflight;

/// <summary>
/// Guardrail G54 (ADR-001): family version major/minor must align with the family upstream
/// library major/minor from manifest.json.
/// <para>
/// Current packaging orchestration accepts a single <c>--family-version</c> for multiple
/// families. In that multi-family shape, strict minor alignment is deferred until per-family
/// version overrides land (tracked separately). This validator still enforces major alignment
/// and performs strict major+minor alignment when packing a single family.
/// </para>
/// </summary>
public sealed class UpstreamVersionAlignmentValidator : IUpstreamVersionAlignmentValidator
{
    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "G54 family/library resolution + version-compatibility states are intentionally evaluated in one flow to preserve per-family diagnostic context.")]
    public UpstreamVersionAlignmentResult Validate(ManifestConfig manifestConfig, PackageBuildConfiguration packageBuildConfiguration)
    {
        ArgumentNullException.ThrowIfNull(manifestConfig);
        ArgumentNullException.ThrowIfNull(packageBuildConfiguration);

        var requestedFamilies = packageBuildConfiguration.Families.Count == 0
            ? manifestConfig.PackageFamilies.Select(family => family.Name).ToList()
            : packageBuildConfiguration.Families.ToList();

        var checks = new List<UpstreamVersionAlignmentCheck>();

        if (string.IsNullOrWhiteSpace(packageBuildConfiguration.FamilyVersion))
        {
            checks.AddRange(requestedFamilies.Select(family => new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: family,
                LibraryRef: null,
                FamilyVersion: null,
                UpstreamVersion: null,
                Status: UpstreamVersionAlignmentCheckStatus.SkippedNoFamilyVersion,
                ErrorMessage: null)));

            var skippedValidation = new UpstreamVersionAlignmentValidation(checks);
            return UpstreamVersionAlignmentResult.Pass(skippedValidation);
        }

        if (!NuGetVersion.TryParse(packageBuildConfiguration.FamilyVersion, out var parsedFamilyVersion))
        {
            checks.AddRange(requestedFamilies.Select(family => new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: family,
                LibraryRef: null,
                FamilyVersion: packageBuildConfiguration.FamilyVersion,
                UpstreamVersion: null,
                Status: UpstreamVersionAlignmentCheckStatus.InvalidFamilyVersion,
                ErrorMessage:
                $"G54: --family-version '{packageBuildConfiguration.FamilyVersion}' is not a valid NuGet SemVer value.")));

            var invalidVersionValidation = new UpstreamVersionAlignmentValidation(checks);
            return UpstreamVersionAlignmentResult.Fail(invalidVersionValidation);
        }

        var strictMinorAlignmentEnabled = requestedFamilies.Count == 1;

        foreach (var requestedFamily in requestedFamilies)
        {
            var family = manifestConfig.PackageFamilies.SingleOrDefault(
                candidate => string.Equals(candidate.Name, requestedFamily, StringComparison.OrdinalIgnoreCase));

            if (family is null)
            {
                checks.Add(new UpstreamVersionAlignmentCheck(
                    FamilyIdentifier: requestedFamily,
                    LibraryRef: null,
                    FamilyVersion: parsedFamilyVersion.ToNormalizedString(),
                    UpstreamVersion: null,
                    Status: UpstreamVersionAlignmentCheckStatus.FamilyNotFound,
                    ErrorMessage: $"G54: family '{requestedFamily}' was not found in manifest package_families[]."));
                continue;
            }

            var library = manifestConfig.LibraryManifests.SingleOrDefault(
                candidate => string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

            if (library is null)
            {
                checks.Add(new UpstreamVersionAlignmentCheck(
                    FamilyIdentifier: family.Name,
                    LibraryRef: family.LibraryRef,
                    FamilyVersion: parsedFamilyVersion.ToNormalizedString(),
                    UpstreamVersion: null,
                    Status: UpstreamVersionAlignmentCheckStatus.LibraryRefNotFound,
                    ErrorMessage:
                    $"G54: family '{family.Name}' library_ref '{family.LibraryRef}' does not resolve to any library_manifests[].name entry."));
                continue;
            }

            if (!NuGetVersion.TryParse(library.VcpkgVersion, out var upstreamVersion))
            {
                checks.Add(new UpstreamVersionAlignmentCheck(
                    FamilyIdentifier: family.Name,
                    LibraryRef: family.LibraryRef,
                    FamilyVersion: parsedFamilyVersion.ToNormalizedString(),
                    UpstreamVersion: library.VcpkgVersion,
                    Status: UpstreamVersionAlignmentCheckStatus.InvalidUpstreamVersion,
                    ErrorMessage:
                    $"G54: manifest library '{library.Name}' has non-semantic vcpkg_version '{library.VcpkgVersion}'."));
                continue;
            }

            var majorMatch = parsedFamilyVersion.Major == upstreamVersion.Major;
            var minorMatch = parsedFamilyVersion.Minor == upstreamVersion.Minor;

            if (!majorMatch)
            {
                checks.Add(new UpstreamVersionAlignmentCheck(
                    FamilyIdentifier: family.Name,
                    LibraryRef: family.LibraryRef,
                    FamilyVersion: parsedFamilyVersion.ToNormalizedString(),
                    UpstreamVersion: library.VcpkgVersion,
                    Status: UpstreamVersionAlignmentCheckStatus.VersionMismatch,
                    ErrorMessage:
                    $"G54: family '{family.Name}' version '{parsedFamilyVersion}' major ({parsedFamilyVersion.Major}) does not match upstream '{library.VcpkgVersion}' major ({upstreamVersion.Major})."));
                continue;
            }

            if (strictMinorAlignmentEnabled)
            {
                if (!minorMatch)
                {
                    checks.Add(new UpstreamVersionAlignmentCheck(
                        FamilyIdentifier: family.Name,
                        LibraryRef: family.LibraryRef,
                        FamilyVersion: parsedFamilyVersion.ToNormalizedString(),
                        UpstreamVersion: library.VcpkgVersion,
                        Status: UpstreamVersionAlignmentCheckStatus.VersionMismatch,
                        ErrorMessage:
                        $"G54: family '{family.Name}' version '{parsedFamilyVersion}' minor ({parsedFamilyVersion.Minor}) does not match upstream '{library.VcpkgVersion}' minor ({upstreamVersion.Minor})."));
                }
                else
                {
                    checks.Add(new UpstreamVersionAlignmentCheck(
                        FamilyIdentifier: family.Name,
                        LibraryRef: family.LibraryRef,
                        FamilyVersion: parsedFamilyVersion.ToNormalizedString(),
                        UpstreamVersion: library.VcpkgVersion,
                        Status: UpstreamVersionAlignmentCheckStatus.Match,
                        ErrorMessage: null));
                }

                continue;
            }

            checks.Add(new UpstreamVersionAlignmentCheck(
                FamilyIdentifier: family.Name,
                LibraryRef: family.LibraryRef,
                FamilyVersion: parsedFamilyVersion.ToNormalizedString(),
                UpstreamVersion: library.VcpkgVersion,
                Status: minorMatch
                    ? UpstreamVersionAlignmentCheckStatus.Match
                    : UpstreamVersionAlignmentCheckStatus.SkippedMinorAlignmentForMultiFamilyPack,
                ErrorMessage: null));
        }

        var validation = new UpstreamVersionAlignmentValidation(checks);

        return validation.HasErrors
            ? UpstreamVersionAlignmentResult.Fail(validation)
            : UpstreamVersionAlignmentResult.Pass(validation);
    }
}
