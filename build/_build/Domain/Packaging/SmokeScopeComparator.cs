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
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Path 1 (legacy / explicit): literal <PackageReference Include="Janset.SDL2.Core"/>
        // entries in the csproj. Supported for backwards compatibility + for consumers
        // that do not use the shared smoke family-list convention.
        foreach (var element in doc.Descendants()
            .Where(e => e.Name.LocalName.Equals("PackageReference", StringComparison.OrdinalIgnoreCase)))
        {
            var identity = element.Attribute("Include")?.Value;
            if (!string.IsNullOrWhiteSpace(identity) && identity.StartsWith(JansetPackagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                identities.Add(identity);
            }
        }

        // Path 2 (post-S1 canonical pattern): <JansetSmokeSdl{2,3}Families>Core;Image;…</…>
        // property. build/msbuild/Janset.Smoke.targets auto-expands the semicolon-
        // separated role list into Janset.SDL<N>.<Role> PackageReference items at
        // MSBuild eval time. Raw XML parsing cannot see that expansion, so we replicate
        // the role → package-id mapping here. Keeps drift detection accurate under the
        // authoring convention documented in docs/playbook/cross-platform-smoke-validation.md
        // §"Authoring New Smoke / Example Consumer Projects".
        ExpandFamilyListProperty(doc, "JansetSmokeSdl2Families", generation: "2", identities);
        ExpandFamilyListProperty(doc, "JansetSmokeSdl3Families", generation: "3", identities);

        return identities;
    }

    private static void ExpandFamilyListProperty(XDocument doc, string propertyName, string generation, HashSet<string> identities)
    {
        var propertyElement = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

        if (propertyElement is null || string.IsNullOrWhiteSpace(propertyElement.Value))
        {
            return;
        }

        var roles = propertyElement.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var role in roles)
        {
            identities.Add($"Janset.SDL{generation}.{role}");
        }
    }
}

public sealed record ScopeComparison(IReadOnlyList<string> Missing, IReadOnlyList<string> Unexpected)
{
    public bool IsMatch => Missing.Count == 0 && Unexpected.Count == 0;
}
