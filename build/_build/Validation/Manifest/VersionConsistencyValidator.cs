using Build.Manifest;
using Build.Validation.Models;
using NuGet.Versioning;

namespace Build.Validation.Manifest;

public sealed class VersionConsistencyValidator : IVersionConsistencyValidator
{
    public VersionConsistencyValidation Validate(ManifestConfig manifest, VcpkgManifest vcpkgManifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(vcpkgManifest);

        var vcpkgOverrides = CreateVcpkgOverrideLookup(vcpkgManifest);

        var checks = manifest.LibraryManifests
            .Select(library => ValidateLibrary(library, vcpkgOverrides))
            .ToList();

        return new VersionConsistencyValidation(checks);
    }

    private static Dictionary<string, VcpkgOverride> CreateVcpkgOverrideLookup(VcpkgManifest vcpkgManifest)
    {
        if (vcpkgManifest.Overrides == null)
        {
            return new Dictionary<string, VcpkgOverride>(StringComparer.Ordinal);
        }

        return vcpkgManifest.Overrides.ToDictionary(overrideItem => overrideItem.Name, overrideItem => overrideItem, StringComparer.Ordinal);
    }

    private static LibraryVersionCheck ValidateLibrary(LibraryManifest library, Dictionary<string, VcpkgOverride> vcpkgOverrides)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(vcpkgOverrides);

        if (!vcpkgOverrides.TryGetValue(library.VcpkgName, out var vcpkgOverride))
        {
            return new LibraryVersionCheck(
                library.Name,
                library.VcpkgName,
                library.VcpkgVersion,
                library.VcpkgPortVersion,
                OverrideVersion: null,
                OverridePortVersion: null,
                LibraryVersionCheckStatus.MissingOverride);
        }

        if (!NuGetVersion.TryParse(library.VcpkgVersion, out var manifestVersion))
        {
            return new LibraryVersionCheck(
                library.Name,
                library.VcpkgName,
                library.VcpkgVersion,
                library.VcpkgPortVersion,
                vcpkgOverride.Version,
                vcpkgOverride.PortVersion,
                LibraryVersionCheckStatus.InvalidManifestVersion);
        }

        if (!NuGetVersion.TryParse(vcpkgOverride.Version, out var overrideVersion))
        {
            return new LibraryVersionCheck(
                library.Name,
                library.VcpkgName,
                library.VcpkgVersion,
                library.VcpkgPortVersion,
                vcpkgOverride.Version,
                vcpkgOverride.PortVersion,
                LibraryVersionCheckStatus.InvalidOverrideVersion);
        }

        var versionMatch = manifestVersion.Major == overrideVersion.Major &&
                           manifestVersion.Minor == overrideVersion.Minor &&
                           manifestVersion.Patch == overrideVersion.Patch;

        if (!versionMatch)
        {
            return new LibraryVersionCheck(
                library.Name,
                library.VcpkgName,
                library.VcpkgVersion,
                library.VcpkgPortVersion,
                vcpkgOverride.Version,
                vcpkgOverride.PortVersion,
                LibraryVersionCheckStatus.VersionMismatch);
        }

        var portVersion = vcpkgOverride.PortVersion ?? 0;
        if (library.VcpkgPortVersion != portVersion)
        {
            return new LibraryVersionCheck(
                library.Name,
                library.VcpkgName,
                library.VcpkgVersion,
                library.VcpkgPortVersion,
                vcpkgOverride.Version,
                portVersion,
                LibraryVersionCheckStatus.PortVersionMismatch);
        }

        return new LibraryVersionCheck(
            library.Name,
            library.VcpkgName,
            library.VcpkgVersion,
            library.VcpkgPortVersion,
            vcpkgOverride.Version,
            portVersion,
            LibraryVersionCheckStatus.Match);
    }
}
