using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest;
using Build.Host.Paths;
using Cake.Core;

namespace Build.Targets.GenerateBindings.PostProcess.Config;

internal sealed class FamilyConfig
{
    private readonly Dictionary<string, BindingGenerationConfig> _families;
    private readonly IReadOnlyList<string> _order;
    private readonly BindingGenerationSettings _settings;

    public FamilyConfig(IBindingGenerationConfigRepository repository)
    {
        _settings = repository.LoadGlobalSettings();
        var profiles = repository
            .EnumerateEnabledFamilies()
            .Select(id => repository.Load(id).Value)
            .ToList();
        _order = [.. profiles.Select(RoutingKey)];
        _families = profiles.ToDictionary(RoutingKey, StringComparer.Ordinal);
    }

    public static Task<FamilyConfig> LoadAsync(ICakeContext context, IPathService paths)
        => Task.FromResult(new FamilyConfig(new BindingGenerationConfigRepository(new ManifestRepository(context, paths.GetManifestFile()))));

    private BindingGenerationConfig Family(string family) => _families[family];

    public string Namespace(string family) => Family(family).ManagedNamespace;
    public string PublicClass(string family) => Family(family).PrimaryClassName;
    public string RawClass(string family) => Family(family).RawClassName;
    public string ProjectDir(string family) => new Cake.Core.IO.FilePath(Family(family).GenerationProject).GetDirectory().GetDirectoryName();
    public bool OwnerMode(string family) => Family(family).OwnerMode;
    public string PackageFamilyId(string family) => Family(family).FamilyId;

    /// <summary>Legacy postprocess routing keys in explicit generation order.</summary>
    public IReadOnlyList<string> Families()
        => _order;

    private static string RoutingKey(BindingGenerationConfig profile)
        => profile.FamilyId[(profile.FamilyId.IndexOf('-', StringComparison.Ordinal) + 1)..];

    /// <summary>Map of project_dir -> family id, replacing the hardcoded directory switch.</summary>
    public IReadOnlyDictionary<string, string> ProjectDirToFamily()
        => Families().ToDictionary(ProjectDir, f => f, StringComparer.OrdinalIgnoreCase);

    /// <summary>Map of namespace -> family id, replacing the hardcoded namespace switch.</summary>
    public IReadOnlyDictionary<string, string> NamespaceToFamily()
        => Families().ToDictionary(Namespace, f => f, StringComparer.Ordinal);

    public HashSet<string> FlagsAllowList(string family)
        => Family(family).FlagsEnums.AllowList.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);

    public IReadOnlyList<string> ClongMethods(string family)
        => Family(family).ClongMethods;

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

    private static void AppendOpaque(BindingGenerationConfig family, HashSet<string> autoDetect, HashSet<string> forceOpaque)
    {
        autoDetect.UnionWith(family.OpaqueHandles.AutoDetectWellKnown.Select(entry => entry.Name));
        forceOpaque.UnionWith(family.OpaqueHandles.ForceOpaqueExceptions.Select(entry => entry.Name));
    }

    public IReadOnlyList<PlatformView> PlatformViews()
        => [.. _settings.PlatformViews.Select(view => new PlatformView(view.Name, view.SupportedOs, view.Defines))];
}
