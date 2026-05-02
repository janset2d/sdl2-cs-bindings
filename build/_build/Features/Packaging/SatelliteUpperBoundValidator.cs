using System.Diagnostics.CodeAnalysis;
using Build.Shared.Manifest;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Features.Packaging;

public static class SatelliteUpperBoundValidator
{
    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "G56 validation intentionally keeps parse, manifest resolution, and bound checks in one path for full diagnostic context.")]
    public static PackageValidationCheck Validate(
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
            return BuildFailure(
                family,
                managedPackagePath,
                dependencyPackageId,
                "<missing>",
                $"G56: managed package '{managedPackagePath.GetFilename().FullPath}' is missing cross-family dependency '{dependencyPackageId}' required by depends_on '{dependencyFamily}'.");
        }

        if (!VersionRange.TryParse(dependencyVersionExpression, out var range))
        {
            return BuildFailure(
                family,
                managedPackagePath,
                "<NuGet version range>",
                dependencyVersionExpression,
                $"G56: dependency '{dependencyPackageId}' in '{managedPackagePath.GetFilename().FullPath}' has non-parseable version range '{dependencyVersionExpression}'.");
        }

        var dependencyFamilyConfig = manifestConfig.PackageFamilies.SingleOrDefault(
            candidate => string.Equals(candidate.Name, dependencyFamily, StringComparison.OrdinalIgnoreCase));

        if (dependencyFamilyConfig is null)
        {
            return BuildFailure(
                family,
                managedPackagePath,
                dependencyFamily,
                "<missing>",
                $"G56: depends_on entry '{dependencyFamily}' for family '{family.Name}' does not exist in manifest package_families[].");
        }

        var dependencyLibrary = manifestConfig.LibraryManifests.SingleOrDefault(
            candidate => string.Equals(candidate.Name, dependencyFamilyConfig.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (dependencyLibrary is null)
        {
            return BuildFailure(
                family,
                managedPackagePath,
                dependencyFamilyConfig.LibraryRef,
                "<missing>",
                $"G56: dependency family '{dependencyFamily}' maps to library_ref '{dependencyFamilyConfig.LibraryRef}' that does not exist in manifest library_manifests[].");
        }

        if (!NuGetVersion.TryParse(dependencyLibrary.VcpkgVersion, out var dependencyLibraryVersion))
        {
            return BuildFailure(
                family,
                managedPackagePath,
                "<valid manifest library version>",
                dependencyLibrary.VcpkgVersion,
                $"G56: dependency family '{dependencyFamily}' has non-semantic manifest vcpkg_version '{dependencyLibrary.VcpkgVersion}'.");
        }

        var expectedUpper = new NuGetVersion(dependencyLibraryVersion.Major + 1, 0, 0);

        var valid = range.MaxVersion is not null &&
                    range.MaxVersion == expectedUpper &&
                    !range.IsMaxInclusive;

        if (valid)
        {
            return new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: managedPackagePath,
                Kind: PackageValidationCheckKind.SatelliteCrossFamilyUpperBound,
                IsValid: true,
                ExpectedValue: $"< {expectedUpper}",
                ActualValue: dependencyVersionExpression,
                ErrorMessage: null);
        }

        return BuildFailure(
            family,
            managedPackagePath,
            $"< {expectedUpper}",
            dependencyVersionExpression,
                $"G56: dependency '{dependencyPackageId}' in managed package '{managedPackagePath.GetFilename().FullPath}' must declare explicit upper bound '< {expectedUpper}' (derived from upstream major {dependencyLibraryVersion.Major}). Actual expression: '{dependencyVersionExpression}'.");
    }

    private static PackageValidationCheck BuildFailure(
        PackageFamilyConfig family,
        FilePath managedPackagePath,
        string expected,
        string actual,
        string message)
    {
        return new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: managedPackagePath,
            Kind: PackageValidationCheckKind.SatelliteCrossFamilyUpperBound,
            IsValid: false,
            ExpectedValue: expected,
            ActualValue: actual,
            ErrorMessage: message);
    }
}
