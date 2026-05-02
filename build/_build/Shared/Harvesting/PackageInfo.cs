namespace Build.Shared.Harvesting;

/// <summary>
/// Vcpkg package metadata snapshot used by harvesting / binary-closure walking.
/// <see cref="OwnedFiles"/> stores canonical full-path strings (Cake-decoupled per ADR-004
/// §2.6); callers that need <c>Cake.Core.IO.FilePath</c> behavior wrap at the use site.
/// </summary>
public sealed record PackageInfo(string PackageName, string Triplet, IReadOnlyList<string> OwnedFiles, IReadOnlyList<string> DeclaredDependencies);
