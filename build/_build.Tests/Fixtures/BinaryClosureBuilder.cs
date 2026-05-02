using Build.Features.Harvesting;
using Cake.Core.IO;

namespace Build.Tests.Fixtures;

/// <summary>
/// Fluent builder for constructing <see cref="BinaryClosure"/> test fixtures.
/// </summary>
public sealed class BinaryClosureBuilder
{
    private readonly HashSet<FilePath> _primaryFiles = [];
    private readonly List<BinaryNode> _nodes = [];
    private readonly HashSet<string> _packages = new(StringComparer.OrdinalIgnoreCase);

    public BinaryClosureBuilder AddPrimaryFile(string path, string ownerPackage)
    {
        var filePath = new FilePath(path);
        _primaryFiles.Add(filePath);
        _nodes.Add(new BinaryNode(filePath, ownerPackage, ownerPackage));
        _packages.Add(ownerPackage);
        return this;
    }

    public BinaryClosureBuilder AddRuntimeDependency(string path, string ownerPackage, string originPackage)
    {
        var filePath = new FilePath(path);
        _nodes.Add(new BinaryNode(filePath, ownerPackage, originPackage));
        _packages.Add(ownerPackage);
        _packages.Add(originPackage);
        return this;
    }

    public BinaryClosure Build()
    {
        return new BinaryClosure(_primaryFiles, _nodes, _packages);
    }
}
