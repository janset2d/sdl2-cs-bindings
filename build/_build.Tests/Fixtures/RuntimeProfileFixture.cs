using System.Text.Json;
using Build.Context.Models;
using Build.Modules;

namespace Build.Tests.Fixtures;

public static class RuntimeProfileFixture
{
    private static readonly Lazy<SystemArtefactsConfig> CachedArtefacts = new(LoadSystemArtefactsFromJson);

    public static RuntimeProfile CreateWindows(string rid = "win-x64", string triplet = "x64-windows-hybrid")
    {
        var runtimeInfo = new RuntimeInfo { Rid = rid, Triplet = triplet, Runner = "windows-latest" };
        return new RuntimeProfile(runtimeInfo, CachedArtefacts.Value);
    }

    public static RuntimeProfile CreateLinux(string rid = "linux-x64", string triplet = "x64-linux-hybrid")
    {
        var runtimeInfo = new RuntimeInfo { Rid = rid, Triplet = triplet, Runner = "ubuntu-24.04" };
        return new RuntimeProfile(runtimeInfo, CachedArtefacts.Value);
    }

    public static RuntimeProfile CreateMacOS(string rid = "osx-x64", string triplet = "x64-osx-hybrid")
    {
        var runtimeInfo = new RuntimeInfo { Rid = rid, Triplet = triplet, Runner = "macos-15-intel" };
        return new RuntimeProfile(runtimeInfo, CachedArtefacts.Value);
    }

    public static SystemArtefactsConfig SystemArtefacts => CachedArtefacts.Value;

    private static SystemArtefactsConfig LoadSystemArtefactsFromJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "system_artefacts.json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<SystemArtefactsConfig>(stream)
            ?? throw new InvalidOperationException("Failed to deserialize system_artefacts.json");
    }
}
