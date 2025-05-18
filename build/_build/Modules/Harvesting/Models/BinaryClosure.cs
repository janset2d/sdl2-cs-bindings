using Cake.Core.IO;

namespace Build.Modules.Harvesting.Models;

/// <summary>
/// Immutable result of dependency discovery.
/// <para>
/// • <see cref="Nodes"/> contains one entry per binary that was discovered.<br/>
///    Each <see cref="BinaryNode"/> tells us:<br/>
///     – the file path<br/>
///     – the package that <em>owns</em> the file (from vcpkg “owns” metadata)<br/>
///     – the package whose binary triggered the runtime scan that discovered it (<c>OriginPackage</c>).<br/>
///     That extra field lets higher layers decide, for example, to drop all binaries that were
///     pulled in transitively through the core library when harvesting a satellite package,
///     without having to re-run any scans.
/// <br/><br/>
/// • <see cref="Packages"/> is the set of every package reached via
///   (vcpkg declared deps ∪ runtime scan paths).<br/>
/// </para>
/// </summary>
public sealed record BinaryClosure(FilePath PrimaryBinary, IReadOnlyList<BinaryNode> Nodes, IReadOnlySet<string> Packages)
{
    public IReadOnlyCollection<FilePath> AllBinaries()
    {
        return [.. Nodes.Select(b => b.Path)];
    }
}

public sealed record BinaryNode(FilePath Path, string OwnerPackage, string OriginPackage);
