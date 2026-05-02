using Build.Shared.Harvesting;

namespace Build.Tests.Fixtures;

/// <summary>
/// Fluent builder for constructing <see cref="BinaryClosure"/> test fixtures.
/// Path-typed members are <see cref="string"/> per ADR-004 §2.6 Cake-decoupling
/// (Adım 13.1).
/// </summary>
public sealed class BinaryClosureBuilder
{
    private readonly HashSet<string> _primaryFiles = new(StringComparer.Ordinal);
    private readonly List<BinaryNode> _nodes = [];
    private readonly HashSet<string> _packages = new(StringComparer.OrdinalIgnoreCase);

    public BinaryClosureBuilder AddPrimaryFile(string path, string ownerPackage)
    {
        _primaryFiles.Add(path);
        _nodes.Add(new BinaryNode(path, ownerPackage, ownerPackage));
        _packages.Add(ownerPackage);
        return this;
    }

    public BinaryClosureBuilder AddRuntimeDependency(string path, string ownerPackage, string originPackage)
    {
        _nodes.Add(new BinaryNode(path, ownerPackage, originPackage));
        _packages.Add(ownerPackage);
        _packages.Add(originPackage);
        return this;
    }

    public BinaryClosure Build()
    {
        return new BinaryClosure(_primaryFiles, _nodes, _packages);
    }
}
