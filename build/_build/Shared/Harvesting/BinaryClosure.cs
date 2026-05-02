namespace Build.Shared.Harvesting;

/// <summary>
/// Immutable result of dependency discovery.
/// <para>
/// • <see cref="Nodes"/> contains one entry per binary that was discovered.<br/>
///    Each <see cref="BinaryNode"/> tells us:<br/>
///     – the file path (canonical full-path string; Cake-decoupled per ADR-004 §2.6)<br/>
///     – the package that <em>owns</em> the file (from vcpkg "owns" metadata)<br/>
///     – the package whose binary triggered the runtime scan that discovered it (<c>OriginPackage</c>).<br/>
///     That extra field lets higher layers decide, for example, to drop all binaries that were
///     pulled in transitively through the core library when harvesting a satellite package,
///     without having to re-run any scans.
/// <br/><br/>
/// • <see cref="Packages"/> is the set of every package reached via
///   (vcpkg declared deps ∪ runtime scan paths).<br/>
/// • <see cref="PrimaryFiles"/> contains all files that match the primary binary patterns
///   for the target library (e.g., entire symlink chain on Unix systems).<br/>
/// </para>
/// <para>
/// Path-typed members are <see cref="string"/> (canonical Cake <c>FilePath.FullPath</c> form)
/// rather than <c>Cake.Core.IO.FilePath</c> — Shared/ types must not depend on Cake per
/// ADR-004 §2.6. Callers that need <c>FilePath</c> behavior wrap at the use site.
/// </para>
/// </summary>
public sealed record BinaryClosure(IReadOnlySet<string> PrimaryFiles, IReadOnlyList<BinaryNode> Nodes, IReadOnlySet<string> Packages)
{
    public IReadOnlyCollection<string> AllBinaries()
    {
        return [.. Nodes.Select(b => b.Path)];
    }

    public bool IsPrimaryFile(string path) => PrimaryFiles.Contains(path);
}

public sealed record BinaryNode(string Path, string OwnerPackage, string OriginPackage);
