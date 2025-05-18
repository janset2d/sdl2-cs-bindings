using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Modules.Harvesting.Models;

public record VcpkgInstalledPackageOutput
{
    [JsonPropertyName("results")] public required ImmutableDictionary<string, VcpkgInstalledResult> Results { get; init; }
}

public record VcpkgInstalledResult
{
    [JsonPropertyName("version-string")] public required string VersionString { get; init; }

    [JsonPropertyName("port-version")] public required int PortVersion { get; init; }

    [JsonPropertyName("triplet")] public required string Triplet { get; init; }

    [JsonPropertyName("abi")] public string? Abi { get; init; }

    [JsonPropertyName("dependencies")] public required IImmutableList<string> Dependencies { get; init; }

    [JsonPropertyName("features")] public IImmutableList<string>? Features { get; init; }

    [JsonPropertyName("usage")] public string? Usage { get; init; }

    [JsonPropertyName("owns")] public required IImmutableList<string> Owns { get; init; }
}
