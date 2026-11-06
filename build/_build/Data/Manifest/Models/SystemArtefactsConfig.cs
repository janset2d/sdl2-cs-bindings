using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;

public record SystemArtefactsConfig
{
    [JsonPropertyName("windows")] public required WindowsSystemArtefacts Windows { get; init; }
    [JsonPropertyName("linux")] public required LinuxSystemArtefacts Linux { get; init; }
    [JsonPropertyName("osx")] public required OsxSystemArtefacts Osx { get; init; }
}
