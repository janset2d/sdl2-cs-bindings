using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace Build.Data.Versions;

/// <summary>
/// Typed family→version mapping. The canonical shape used at every task/service
/// boundary in the build host. Immutable; two sets with the same contents are equal
/// regardless of input order. Throws on duplicate family at construction.
/// </summary>
[JsonConverter(typeof(PackageFamilyVersionSetJsonConverter))]
public sealed record PackageFamilyVersionSet : IReadOnlyCollection<PackageFamilyVersion>
{
    public static PackageFamilyVersionSet Empty { get; } = new([]);

    private readonly Dictionary<PackageFamilyId, NuGetVersion> _versions;

    public PackageFamilyVersionSet(IEnumerable<PackageFamilyVersion> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _versions = [];

        foreach (var entry in entries)
        {
            if (!_versions.TryAdd(entry.Family, entry.Version))
            {
                throw new ArgumentException($"Duplicate family '{entry.Family.Value}' in PackageFamilyVersionSet input.", nameof(entries));
            }
        }

        Families = [.. _versions.Keys.OrderBy(static id => id.Value, StringComparer.Ordinal)];
    }

    public int Count => _versions.Count;

    public IReadOnlyList<PackageFamilyId> Families { get; }

    public bool Contains(PackageFamilyId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _versions.ContainsKey(id);
    }

    public bool TryGetVersion(PackageFamilyId id, [NotNullWhen(true)] out NuGetVersion? version)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _versions.TryGetValue(id, out version);
    }

    public NuGetVersion RequireVersion(PackageFamilyId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _versions.TryGetValue(id, out var version)
            ? version
            : throw new KeyNotFoundException($"No version found for package family '{id.Value}' in PackageFamilyVersionSet.");
    }

    public IEnumerator<PackageFamilyVersion> GetEnumerator() => Families.Select(family => new PackageFamilyVersion(family, _versions[family])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Custom equality so two sets with the same contents in different input order are equal.
    // The default record-class equality compares the underlying dictionary by reference.
    public bool Equals(PackageFamilyVersionSet? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (_versions.Count != other._versions.Count)
        {
            return false;
        }

        foreach (var (family, version) in _versions)
        {
            if (!other._versions.TryGetValue(family, out var otherVersion))
            {
                return false;
            }

            if (!version.Equals(otherVersion))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var family in Families)
        {
            hash.Add(family);
            hash.Add(_versions[family]);
        }

        return hash.ToHashCode();
    }
}
