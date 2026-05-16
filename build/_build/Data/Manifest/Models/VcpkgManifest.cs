using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;

/// <summary>
/// Projection of a <c>vcpkg.json</c> manifest. The schema is the same across the
/// repository-root manifest (dependency declaration with <c>overrides[]</c>) and
/// individual port manifests (<c>vcpkg-overlay-ports/&lt;port&gt;/</c> or
/// <c>external/vcpkg/ports/&lt;port&gt;/</c>) — every field is optional and each
/// caller reads whichever ones it cares about.
/// </summary>
public sealed record VcpkgManifest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("port-version")]
    public int? PortVersion { get; init; }

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
