using System.Text.Json;

namespace Janset.SDL2.PostProcess.Config;

internal sealed record PlatformView(string Name, string SupportedOs, IReadOnlyList<string> Defines);

internal sealed class FamilyConfig
{
    private readonly JsonElement _root;

    private FamilyConfig(JsonElement root) => _root = root;

    private const string ExpectedSchemaVersion = "1.0";

    public static FamilyConfig Load(string? startDir = null)
    {
        var path = FamilyConfigLocator.Resolve(startDir);
        // Clone the root so it survives JsonDocument disposal; dispose the doc.
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement.Clone();
        // Preserve the schema-version guard the retired roster loaders enforced,
        // so a future schema bump fails loudly instead of silently mis-parsing.
        var schema = root.GetProperty("schema_version").GetString();
        if (!string.Equals(schema, ExpectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"family-config.json has schema_version '{schema}', expected '{ExpectedSchemaVersion}'.");
        }
        return new FamilyConfig(root);
    }

    private JsonElement Family(string family)
    {
        if (!_root.GetProperty("families").TryGetProperty(family, out var entry))
        {
            throw new InvalidDataException($"family-config.json is missing family '{family}'.");
        }
        return entry;
    }

    public string Namespace(string family) => Family(family).GetProperty("namespace").GetString()!;
    public string ProjectDir(string family) => Family(family).GetProperty("project_dir").GetString()!;
    public bool OwnerMode(string family) => Family(family).GetProperty("owner_mode").GetBoolean();

    /// <summary>All family ids declared in the config, in document order.</summary>
    public IReadOnlyList<string> Families()
        => _root.GetProperty("families").EnumerateObject().Select(p => p.Name).ToList();

    /// <summary>Map of project_dir -> family id, replacing the hardcoded directory switch.</summary>
    public IReadOnlyDictionary<string, string> ProjectDirToFamily()
        => Families().ToDictionary(ProjectDir, f => f, StringComparer.OrdinalIgnoreCase);

    /// <summary>Map of namespace -> family id, replacing the hardcoded namespace switch.</summary>
    public IReadOnlyDictionary<string, string> NamespaceToFamily()
        => Families().ToDictionary(Namespace, f => f, StringComparer.Ordinal);

    public HashSet<string> FlagsAllowList(string family)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in Family(family).GetProperty("flags_enums").GetProperty("allow_list").EnumerateArray())
        {
            set.Add(e.GetProperty("name").GetString()!);
        }
        return set;
    }

    public IReadOnlyList<string> ClongMethods(string family)
        => Family(family).GetProperty("clong_methods").EnumerateArray().Select(e => e.GetString()!).ToList();

    /// <summary>Opaque handle name sets for a family; optionally union Core's handles (satellite pull).</summary>
    public (HashSet<string> AutoDetect, HashSet<string> ForceOpaque) OpaqueHandles(string family, bool includeCoreHandles)
    {
        var autoDetect = new HashSet<string>(StringComparer.Ordinal);
        var forceOpaque = new HashSet<string>(StringComparer.Ordinal);
        AppendOpaque(Family(family), autoDetect, forceOpaque);
        if (includeCoreHandles && !family.Equals("core", StringComparison.Ordinal))
        {
            AppendOpaque(Family("core"), autoDetect, forceOpaque);
        }
        return (autoDetect, forceOpaque);
    }

    private static void AppendOpaque(JsonElement family, HashSet<string> autoDetect, HashSet<string> forceOpaque)
    {
        var opaque = family.GetProperty("opaque_handles");
        foreach (var e in opaque.GetProperty("auto_detect_well_known").EnumerateArray())
        {
            autoDetect.Add(e.GetProperty("name").GetString()!);
        }
        foreach (var e in opaque.GetProperty("force_opaque_exceptions").EnumerateArray())
        {
            forceOpaque.Add(e.GetProperty("name").GetString()!);
        }
    }

    public IReadOnlyList<PlatformView> PlatformViews()
        => _root.GetProperty("global").GetProperty("platform_views").EnumerateArray()
            .Select(v => new PlatformView(
                v.GetProperty("name").GetString()!,
                v.GetProperty("supported_os").GetString()!,
                v.GetProperty("defines").EnumerateArray().Select(d => d.GetString()!).ToList()))
            .ToList();
}
