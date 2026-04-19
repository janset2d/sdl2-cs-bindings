using Build.Domain.Packaging.Results;

namespace Build.Domain.Packaging;

public interface IPackageVersionResolver
{
    /// <summary>
    /// Resolves the normalized family version string from the raw CLI input.
    /// Future hook: resolution from MinVer-derived git tags when Cake orchestrates tag-driven releases.
    /// Returns a <see cref="PackageVersionResolutionResult"/> carrying either the normalized
    /// version or a typed error describing the invalid input.
    /// </summary>
    PackageVersionResolutionResult Resolve(string? rawVersion);
}
