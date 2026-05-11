using System.Diagnostics.CodeAnalysis;
using Build.Data.Manifest.Models;
using Build.Results;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Validation.Packaging;

/// <summary>
/// Cross-family upper-bound enforcement (G56). For each cross-family managed-package
/// dependency declared in <see cref="PackageFamilyConfig.DependsOn"/>, asserts the version
/// expression carries an explicit upper bound of <c>&lt; (UpstreamMajor + 1).0.0</c> derived
/// from the dependency family's manifest <c>vcpkg_version</c>. Returns <see langword="null"/>
/// when the dependency is well-formed; returns a single <see cref="ValidationCheck"/> with
/// <see cref="ValidationSeverity.Error"/> otherwise. Consumed by
/// <see cref="PackageOutputValidator"/>.
/// </summary>
public interface ISatelliteUpperBoundValidator
{
    ValidationCheck? Validate(
        PackageFamilyConfig family,
        FilePath managedPackagePath,
        string dependencyFamily,
        string dependencyPackageId,
        string? dependencyVersionExpression,
        ManifestConfig manifestConfig);
}

public sealed class SatelliteUpperBoundValidator : ISatelliteUpperBoundValidator
{
    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "G56 validation intentionally keeps parse, manifest resolution, and bound checks in one path for full diagnostic context.")]
    public ValidationCheck? Validate(
        PackageFamilyConfig family,
        FilePath managedPackagePath,
        string dependencyFamily,
        string dependencyPackageId,
        string? dependencyVersionExpression,
        ManifestConfig manifestConfig)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(managedPackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyPackageId);
        ArgumentNullException.ThrowIfNull(manifestConfig);

        if (string.IsNullOrWhiteSpace(dependencyVersionExpression))
        {
            return Failure($"G56: managed package '{managedPackagePath.GetFilename().FullPath}' is missing cross-family dependency '{dependencyPackageId}' required by depends_on '{dependencyFamily}'.");
        }

        if (!VersionRange.TryParse(dependencyVersionExpression, out var range))
        {
            return Failure($"G56: dependency '{dependencyPackageId}' in '{managedPackagePath.GetFilename().FullPath}' has non-parseable version range '{dependencyVersionExpression}'.");
        }

        var dependencyFamilyConfig = manifestConfig.PackageFamilies.SingleOrDefault(
            candidate => string.Equals(candidate.Name, dependencyFamily, StringComparison.OrdinalIgnoreCase));

        if (dependencyFamilyConfig is null)
        {
            return Failure($"G56: depends_on entry '{dependencyFamily}' for family '{family.Name}' does not exist in manifest package_families[].");
        }

        var dependencyLibrary = manifestConfig.LibraryManifests.SingleOrDefault(
            candidate => string.Equals(candidate.Name, dependencyFamilyConfig.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (dependencyLibrary is null)
        {
            return Failure($"G56: dependency family '{dependencyFamily}' maps to library_ref '{dependencyFamilyConfig.LibraryRef}' that does not exist in manifest library_manifests[].");
        }

        if (!NuGetVersion.TryParse(dependencyLibrary.VcpkgVersion, out var dependencyLibraryVersion))
        {
            return Failure($"G56: dependency family '{dependencyFamily}' has non-semantic manifest vcpkg_version '{dependencyLibrary.VcpkgVersion}'.");
        }

        var expectedUpper = new NuGetVersion(dependencyLibraryVersion.Major + 1, 0, 0);

        var valid = range.MaxVersion is not null &&
                    range.MaxVersion == expectedUpper &&
                    !range.IsMaxInclusive;

        if (valid)
        {
            return null;
        }

        return Failure($"G56: dependency '{dependencyPackageId}' in managed package '{managedPackagePath.GetFilename().FullPath}' must declare explicit upper bound '< {expectedUpper}' (derived from upstream major {dependencyLibraryVersion.Major}). Actual expression: '{dependencyVersionExpression}'.");

        static ValidationCheck Failure(string message)
            => new("Cross-family upper bound", ValidationSeverity.Error, message, Code: "G56");
    }
}
