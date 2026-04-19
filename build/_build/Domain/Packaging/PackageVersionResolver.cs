using Build.Domain.Packaging.Models;
using Build.Domain.Packaging.Results;
using NuGet.Versioning;

namespace Build.Domain.Packaging;

public sealed class PackageVersionResolver : IPackageVersionResolver
{
    public PackageVersionResolutionResult Resolve(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return new PackageVersionResolutionError(
                "Package task requires --family-version=<semver>. D-local pack orchestration does not guess or auto-derive a shipping version.",
                rawInput: null);
        }

        if (!NuGetVersion.TryParse(rawVersion, out var version))
        {
            return new PackageVersionResolutionError(
                $"Package task received invalid --family-version value '{rawVersion}'. Provide a valid NuGet SemVer string.",
                rawInput: rawVersion);
        }

        return new PackageVersion(version.ToNormalizedString());
    }
}
