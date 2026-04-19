using System.Globalization;
using Build.Context.Models;
using Build.Domain.Preflight.Models;
using Build.Domain.Preflight.Results;
using Cake.Core.IO;

namespace Build.Domain.Preflight;

public sealed class VersionConsistencyValidator : IVersionConsistencyValidator
{
    public VersionConsistencyResult Validate(ManifestConfig manifest, VcpkgManifest vcpkgManifest, FilePath manifestPath, FilePath vcpkgManifestPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(vcpkgManifest);
        ArgumentNullException.ThrowIfNull(manifestPath);
        ArgumentNullException.ThrowIfNull(vcpkgManifestPath);

        var vcpkgOverrides = CreateVcpkgOverrideLookup(vcpkgManifest);

        var checks = manifest.LibraryManifests
            .Select(library => ValidateLibrary(library, vcpkgOverrides))
            .ToList();

        var validation = new VersionConsistencyValidation(manifestPath, vcpkgManifestPath, checks);

        return validation.HasErrors
            ? VersionConsistencyResult.Fail(validation)
            : VersionConsistencyResult.Pass(validation);
    }

    internal static (int Major, int Minor, int Patch) ParseSemanticVersion(string version)
    {
        var cleanVersion = version.Split(['+', '-'], 2)[0];

        var parts = cleanVersion.Split('.');
        if (parts.Length < 3 ||
            !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(parts[2], CultureInfo.InvariantCulture, out var patch))
        {
            throw new ArgumentException($"Invalid semantic version format: {version}", nameof(version));
        }

        return (major, minor, patch);
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

        if (!TryParseSemanticVersion(library.VcpkgVersion, out var manifestVersion))
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

        if (!TryParseSemanticVersion(vcpkgOverride.Version, out var overrideVersion))
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

    private static bool TryParseSemanticVersion(string version, out (int Major, int Minor, int Patch) parsedVersion)
    {
        try
        {
            parsedVersion = ParseSemanticVersion(version);
            return true;
        }
        catch (ArgumentException)
        {
            parsedVersion = default;
            return false;
        }
    }
}
