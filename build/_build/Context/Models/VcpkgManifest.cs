using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Context.Models;

public sealed record VcpkgManifest
{
    [JsonPropertyName("overrides")]
    public IImmutableList<VcpkgOverride>? Overrides { get; init; }
}

public sealed record VcpkgOverride
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("port-version")]
    public int? PortVersion { get; init; }
}
