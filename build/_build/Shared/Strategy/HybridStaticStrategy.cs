namespace Build.Shared.Strategy;

/// <summary>
/// Packaging strategy for hybrid-static mode: transitive dependencies are statically baked
/// into satellite DLLs. Only the core SDL library remains as an external dynamic dependency.
/// </summary>
public sealed class HybridStaticStrategy(string coreLibraryName) : IPackagingStrategy
{
    private readonly string _coreLibraryName = coreLibraryName ?? throw new ArgumentNullException(nameof(coreLibraryName));

    /// <inheritdoc />
    public PackagingModel Model => PackagingModel.HybridStatic;

    /// <inheritdoc />
    public bool IsCoreLibrary(string vcpkgName)
    {
        return string.Equals(_coreLibraryName, vcpkgName, StringComparison.OrdinalIgnoreCase);
    }
}
