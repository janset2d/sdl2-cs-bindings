using Build.Domain.Strategy.Models;

namespace Build.Domain.Strategy;

/// <summary>
/// Packaging strategy for pure-dynamic mode: all transitive dependencies are shipped
/// as separate dynamic libraries. Legacy model used when hybrid overlay triplets are not available.
/// </summary>
public sealed class PureDynamicStrategy(string coreLibraryName) : IPackagingStrategy
{
    private readonly string _coreLibraryName = coreLibraryName ?? throw new ArgumentNullException(nameof(coreLibraryName));

    /// <inheritdoc />
    public PackagingModel Model => PackagingModel.PureDynamic;

    /// <inheritdoc />
    public bool IsCoreLibrary(string vcpkgName)
    {
        return string.Equals(_coreLibraryName, vcpkgName, StringComparison.OrdinalIgnoreCase);
    }
}
