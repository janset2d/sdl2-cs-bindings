using System.Xml.Linq;

namespace Build.Domain.Packaging;

/// <summary>
/// Compares the set of <c>Janset.SDL&lt;N&gt;.&lt;Role&gt;</c> <c>PackageReference</c> identities
/// declared in a smoke consumer's <c>.csproj</c> against the manifest-driven expected set
/// (the application-layer <c>PackageConsumerSmokeRunner</c> derives expected from <c>manifest.json</c>
/// package_families[] filtered to families that declare both managed and native projects).
/// <para>
/// Guards against scope drift: if a family graduates out of placeholder state (becomes
/// concrete in the manifest) without a matching PackageReference entry in the smoke csproj,
/// the smoke runner auto-expands its scope but the csproj stays static — producing a
/// green-but-meaningless smoke result. This comparator fires before any dotnet invocation
/// so the drift surfaces with an actionable error rather than silent false-confidence.
/// </para>
/// <para>
/// Pure: input is csproj XML text + expected identity list, output is a diff. No I/O; the
/// runner handles file loading via <c>ICakeContext.FileSystem</c>.
/// </para>
/// </summary>
public static class SmokeScopeComparator
{
    private const string JansetPackagePrefix = "Janset.SDL";

    public static ScopeComparison Compare(string csprojXml, IEnumerable<string> expectedManagedPackageIds)
    {
        ArgumentException.ThrowIfNullOrEmpty(csprojXml);
        ArgumentNullException.ThrowIfNull(expectedManagedPackageIds);

        var actual = ParseJansetPackageReferences(csprojXml);
        var expected = expectedManagedPackageIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = expected
            .Except(actual, StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unexpected = actual
            .Except(expected, StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ScopeComparison(missing, unexpected);
    }

    private static HashSet<string> ParseJansetPackageReferences(string csprojXml)
    {
        var doc = XDocument.Parse(csprojXml);

        return doc.Descendants()
            .Where(element => element.Name.LocalName.Equals("PackageReference", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Attribute("Include")?.Value)
            .Where(identity => !string.IsNullOrWhiteSpace(identity) && identity!.StartsWith(JansetPackagePrefix, StringComparison.OrdinalIgnoreCase))
            .Select(identity => identity!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record ScopeComparison(IReadOnlyList<string> Missing, IReadOnlyList<string> Unexpected)
{
    public bool IsMatch => Missing.Count == 0 && Unexpected.Count == 0;
}
