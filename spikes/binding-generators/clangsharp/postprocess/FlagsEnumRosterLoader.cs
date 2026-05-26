using System.Text.Json;

namespace Janset.SDL2.PostProcess;

internal static class FlagsEnumRosterLoader
{
    private const string ExpectedSchemaVersion = "2.0";

    public static HashSet<string> LoadForFamily(string rosterPath, string family)
    {
        if (!File.Exists(rosterPath))
        {
            throw new FileNotFoundException(
                $"Flags-enum roster not found at: {rosterPath}. Expected at <repo>/spikes/binding-generators/clangsharp/policy/flags-enum-roster.json.",
                rosterPath);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(rosterPath));
        var root = document.RootElement;
        var schemaVersion = GetRequiredProperty(root, "schema_version", rosterPath).GetString();
        if (!string.Equals(schemaVersion, ExpectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Flags-enum roster '{rosterPath}' has schema_version '{schemaVersion}', expected '{ExpectedSchemaVersion}'.");
        }

        var families = GetRequiredProperty(root, "families", rosterPath);
        if (!families.TryGetProperty(family, out var familyEntry))
        {
            throw new InvalidDataException($"Flags-enum roster '{rosterPath}' is missing family '{family}'.");
        }

        var allowListElement = GetRequiredProperty(familyEntry, "allow_list", $"{rosterPath} families.{family}");
        if (allowListElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Flags-enum roster '{rosterPath}' family '{family}' allow_list must be an array.");
        }

        var allowList = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in allowListElement.EnumerateArray())
        {
            var name = GetRequiredProperty(entry, "name", $"{rosterPath} families.{family}.allow_list[]").GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidDataException($"Flags-enum roster '{rosterPath}' family '{family}' contains an allow_list entry without a non-empty name.");
            }

            allowList.Add(name);
        }

        return allowList;
    }

    public static string ResolveRosterPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "spikes",
                "binding-generators",
                "clangsharp",
                "policy",
                "flags-enum-roster.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate flags-enum-roster.json by walking ancestors. Expected at <repo>/spikes/binding-generators/clangsharp/policy/flags-enum-roster.json.");
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string propertyName, string source)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new InvalidDataException($"Flags-enum roster '{source}' is missing required property '{propertyName}'.");
        }

        return property;
    }
}
