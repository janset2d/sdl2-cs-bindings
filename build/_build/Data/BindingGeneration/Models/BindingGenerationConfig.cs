using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Cake.Core.IO;

namespace Build.Data.BindingGeneration.Models;

/// <summary>Family generation policy; resolved identity is stamped by the repository, not persisted twice.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BindingGenerationConfig
{
    private static readonly ImmutableHashSet<string> DefaultExcludedFunctions = [with(StringComparer.Ordinal)];

    [JsonIgnore] public string FamilyId { get; init; } = string.Empty;
    [JsonIgnore] public string LibraryName { get; init; } = string.Empty;
    [JsonIgnore] public string LibraryVersion { get; init; } = string.Empty;
    [JsonIgnore] public string RawClassName => PrimaryClassName + "Native";
    [JsonIgnore] public string GeneratedRoot => new FilePath(GenerationProject).GetDirectory().Combine("Generated").FullPath;

    [JsonPropertyName("enabled")] public required bool Enabled { get; init; }
    [JsonPropertyName("managed_namespace")] public required string ManagedNamespace { get; init; }
    [JsonPropertyName("primary_class_name")] public required string PrimaryClassName { get; init; }
    [JsonPropertyName("owned_prefixes")] public required ImmutableList<string> OwnedPrefixes { get; init; }
    [JsonPropertyName("generation_project")] public string GenerationProject { get; init; } = string.Empty;
    [JsonPropertyName("rsp")] public string Rsp { get; init; } = string.Empty;
    [JsonPropertyName("owner_mode")] public bool OwnerMode { get; init; }
    [JsonPropertyName("platform_sensitive_headers")] public ImmutableList<string> PlatformSensitiveHeaders { get; init; } = [];
    [JsonPropertyName("opaque_handles")] public OpaqueHandlesConfig OpaqueHandles { get; init; } = new();
    [JsonPropertyName("flags_enums")] public FlagsEnumsConfig FlagsEnums { get; init; } = new();
    [JsonPropertyName("clong_methods")] public ImmutableList<string> ClongMethods { get; init; } = [];
    [JsonPropertyName("excluded_functions")] public ImmutableHashSet<string> ExcludedFunctions { get; init; } = DefaultExcludedFunctions;
    [JsonPropertyName("required_functions")] public ImmutableList<RequiredFunctionConfig> RequiredFunctions { get; init; } = [];

    /// <summary>
    /// Hand-curated declarations of public constants that survive only in headers
    /// excluded from the per-header parse loop. SDL.h is intentionally excluded
    /// because umbrella parsing collapses the SDL2 header set into one translation
    /// unit and reintroduces intrinsic-header and platform-conditioned parse failures
    /// that per-header parsing avoids.
    /// SDL2 contributes the <c>SDL_INIT_*</c> macros declared exclusively in SDL.h
    /// plus explicitly promoted string-like macros that Stage 1 treats as core
    /// API readiness probes.
    /// Optional with empty-default; satellite placeholder entries leave it absent.
    /// These declarations preserve the required surface even when a header is
    /// deliberately excluded from parsing.
    /// </summary>
    [JsonPropertyName("required_constants")] public ImmutableList<RequiredConstantConfig> RequiredConstants { get; init; } = [];

    [JsonPropertyName("validators")] public ImmutableDictionary<string, bool> Validators { get; init; } = [];

    [JsonPropertyName("header_set")] public HeaderSetConfig? HeaderSet { get; init; }
    [JsonPropertyName("dynapi")] public DynapiConfig? Dynapi { get; init; }
}
