using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;
public record WindowsSystemArtefacts
{
    [JsonPropertyName("system_dlls")] public ImmutableList<string> SystemDlls { get; init; } = [];
}
