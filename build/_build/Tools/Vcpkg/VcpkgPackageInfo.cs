namespace Build.Tools.Vcpkg;

/// <summary>
/// Vcpkg package metadata snapshot used by harvesting and binary closure walking.
/// <see cref="OwnedFiles"/> stores canonical full-path strings; callers that need
/// <c>Cake.Core.IO.FilePath</c> behavior wrap at the use site.
/// </summary>
public sealed record VcpkgPackageInfo(
    string PackageName,
    string Triplet,
    IReadOnlyList<string> OwnedFiles,
    IReadOnlyList<string> DeclaredDependencies);
