using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Context.Models;

public record SystemArtefactsConfig
{
    [JsonPropertyName("windows")]
    public required WindowsSystemArtefacts Windows { get; init; }

    [JsonPropertyName("linux")]
    public required LinuxSystemArtefacts Linux { get; init; }

    [JsonPropertyName("osx")]
    public required OsxSystemArtefacts Osx { get; init; }
}

public record WindowsSystemArtefacts
{
    [JsonPropertyName("system_dlls")]
    public ImmutableList<string> SystemDlls { get; init; } = [];
}

public record LinuxSystemArtefacts
{
    [JsonPropertyName("system_libraries")]
    public ImmutableList<string> SystemLibraries { get; init; } = [];
}

public record OsxSystemArtefacts
{
    [JsonPropertyName("system_libraries")]
    public ImmutableList<string> SystemLibraries { get; init; } = [];
}
