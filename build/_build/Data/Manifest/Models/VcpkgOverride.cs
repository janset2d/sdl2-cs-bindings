using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;

public sealed record VcpkgOverride
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("port-version")]
    public int? PortVersion { get; init; }
}
