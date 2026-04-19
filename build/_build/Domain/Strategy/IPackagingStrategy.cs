using Build.Domain.Strategy.Models;

namespace Build.Domain.Strategy;

/// <summary>
/// Defines the packaging model for native library distribution.
/// Determines which libraries are expected as dynamic dependencies
/// versus statically baked into satellite DLLs.
/// </summary>
public interface IPackagingStrategy
{
    /// <summary>
    /// The packaging model this strategy represents.
    /// </summary>
    PackagingModel Model { get; }

    /// <summary>
    /// Returns <c>true</c> if the given vcpkg package name is the core SDL library
    /// (the only allowed external dynamic dependency for satellite packages in hybrid mode).
    /// </summary>
    bool IsCoreLibrary(string vcpkgName);
}
