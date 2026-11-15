using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PeerEvidenceConfig
{
    [JsonPropertyName("display_name")] public required string DisplayName { get; init; }
    [JsonPropertyName("sdl2_cs_file")] public required string Sdl2CsFile { get; init; }
}
