using System.Text.Json;
using Build.Shared.Manifest;
using Build.Shared.Runtime;

namespace Build.Tests.Fixtures;

public static class RuntimeProfileFixture
{
    private static readonly Lazy<SystemArtefactsConfig> CachedArtefacts = new(LoadSystemExclusionsFromManifest);

    public static RuntimeProfile CreateWindows(string rid = "win-x64", string triplet = "x64-windows-hybrid")
    {
        var runtimeInfo = new RuntimeInfo { Rid = rid, Triplet = triplet, Strategy = ResolveStrategy(triplet), Runner = "windows-latest" };
        return new RuntimeProfile(runtimeInfo, CachedArtefacts.Value);
    }

    public static RuntimeProfile CreateLinux(string rid = "linux-x64", string triplet = "x64-linux-hybrid")
    {
        var runtimeInfo = new RuntimeInfo { Rid = rid, Triplet = triplet, Strategy = ResolveStrategy(triplet), Runner = "ubuntu-24.04" };
        return new RuntimeProfile(runtimeInfo, CachedArtefacts.Value);
    }

    public static RuntimeProfile CreateMacOS(string rid = "osx-x64", string triplet = "x64-osx-hybrid")
    {
        var runtimeInfo = new RuntimeInfo { Rid = rid, Triplet = triplet, Strategy = ResolveStrategy(triplet), Runner = "macos-15-intel" };
        return new RuntimeProfile(runtimeInfo, CachedArtefacts.Value);
    }

    public static SystemArtefactsConfig SystemArtefacts => CachedArtefacts.Value;

    private static SystemArtefactsConfig LoadSystemExclusionsFromManifest()
    {
        var manifestJson = WorkspaceFiles.ReadAllText(WorkspaceFiles.ManifestPath);
        var manifest = JsonSerializer.Deserialize<ManifestConfig>(manifestJson)
            ?? throw new InvalidOperationException("Failed to deserialize manifest.json");

        return manifest.SystemExclusions;
    }

    private static string ResolveStrategy(string triplet)
    {
        return triplet.Contains("-hybrid", StringComparison.OrdinalIgnoreCase)
            ? "hybrid-static"
            : "pure-dynamic";
    }
}
