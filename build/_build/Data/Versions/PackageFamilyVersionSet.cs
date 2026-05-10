using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace Build.Data.Versions;

/// <summary>
/// Identifier for a package family declared in <c>build/manifest.json</c>'s
/// <c>package_families[]</c>. Format-agnostic non-empty string. Equality is
/// ordinal-exact; the manifest convention is canonical-lowercase, so case
/// drift in input is treated as a different family.
/// </summary>
public sealed record PackageFamilyId(string Value)
{
    public string Value { get; init; } = Validate(Value);

    public override string ToString() => Value;

    public static implicit operator string(PackageFamilyId id) => id.Value;

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}

/// <summary>
/// Iteration unit for <see cref="PackageFamilyVersionSet"/>. Pairs a package family
/// identifier with its NuGet version.
/// </summary>
public readonly record struct PackageFamilyVersion(PackageFamilyId Family, NuGetVersion Version);

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

internal sealed class PackageFamilyVersionSetJsonConverter : JsonConverter<PackageFamilyVersionSet>
{
    public override PackageFamilyVersionSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object for PackageFamilyVersionSet.");
        }

        var entries = new List<PackageFamilyVersion>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                try
                {
                    return new PackageFamilyVersionSet(entries);
                }
                catch (ArgumentException ex)
                {
                    throw new JsonException(ex.Message, ex);
                }
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected property name; got {reader.TokenType}.");
            }

            var rawFamily = reader.GetString() ?? throw new JsonException("Family identifier cannot be null.");

            PackageFamilyId family;
            try
            {
                family = new PackageFamilyId(rawFamily);
            }
            catch (ArgumentException ex)
            {
                throw new JsonException(ex.Message, ex);
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected string version value for family '{rawFamily}'; got {reader.TokenType}.");
            }

            var rawVersion = reader.GetString();
            if (!NuGetVersion.TryParse(rawVersion, out var version))
            {
                throw new JsonException($"Invalid NuGetVersion '{rawVersion}' for family '{rawFamily}'.");
            }

            entries.Add(new PackageFamilyVersion(family, version));
        }

        throw new JsonException("Unexpected end of input while reading PackageFamilyVersionSet.");
    }

    public override void Write(Utf8JsonWriter writer, PackageFamilyVersionSet value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        foreach (var family in value.Families)
        {
            writer.WriteString(family.Value, value.RequireVersion(family).ToNormalizedString());
        }

        writer.WriteEndObject();
    }
}
