using Build.Manifest;
using Build.Results;
using Cake.Core.IO;

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
