using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

/// <summary>
/// Per-family binding-generation configuration deserialized from
/// <c>build/manifest.json library_manifests[].binding_generation</c>. Strongly-typed
/// surface — all field validation flows through <see cref="System.Text.Json"/>'s
/// <c>required</c>-keyword machinery via the shared
/// <see cref="Build.Data.Manifest.ManifestRepository"/> deserialization path; no
/// hand-rolled JSON walking. Missing required fields surface as
/// <see cref="Cake.Core.CakeException"/> from <c>ManifestRepository.Load()</c> at the
/// point of manifest read.
/// </summary>
public sealed record BindingGenerationConfig
{
    /// <summary>
    /// Family identifier (kebab-case, e.g. <c>sdl2-core</c>). Not present in JSON;
    /// derived from the parent <c>library_manifests[].name</c> and stamped by
    /// <see cref="BindingGenerationConfigRepository.Load"/> via <c>with</c>-expression
    /// before returning to consumers.
    /// </summary>
    [JsonIgnore] public string FamilyId { get; init; } = string.Empty;

    [JsonPropertyName("enabled")] public required bool Enabled { get; init; }
    [JsonPropertyName("managed_namespace")] public required string ManagedNamespace { get; init; }
    [JsonPropertyName("primary_class_name")] public required string PrimaryClassName { get; init; }
    [JsonPropertyName("platform_catalog")] public required string PlatformCatalogId { get; init; }
    [JsonPropertyName("owned_prefixes")] public required ImmutableList<string> OwnedPrefixes { get; init; }

    // Optional with empty defaults — only meaningful when Enabled=true; satellite
    // placeholders (Enabled=false) skip these so the manifest stays compact for
    // stage-2-placeholder entries.
    [JsonPropertyName("parse_defines")] public ImmutableList<string> ParseDefines { get; init; } = [];
    [JsonPropertyName("clang_args")] public ImmutableList<string> ClangArgs { get; init; } = [];
    [JsonPropertyName("excluded_functions")] public ImmutableHashSet<string> ExcludedFunctions { get; init; } = [];
    [JsonPropertyName("required_functions")] public ImmutableList<RequiredFunctionConfig> RequiredFunctions { get; init; } = [];
    [JsonPropertyName("deferred_declarations")] public ImmutableDictionary<string, DeferredDeclarationConfig> DeferredDeclarations { get; init; } = ImmutableDictionary<string, DeferredDeclarationConfig>.Empty;
    [JsonPropertyName("validators")] public ImmutableDictionary<string, bool> Validators { get; init; } = ImmutableDictionary<string, bool>.Empty;

    // Nullable — only present when Enabled=true. Consumer short-circuits on Enabled
    // before reading these, so null-on-disabled is the natural shape.
    [JsonPropertyName("header_set")] public HeaderSetConfig? HeaderSet { get; init; }
    [JsonPropertyName("dynapi")] public DynapiConfig? Dynapi { get; init; }
}

public sealed record HeaderSetConfig
{
    [JsonPropertyName("include_dir_glob")] public required string IncludeDirGlob { get; init; }
    [JsonPropertyName("header_glob")] public required string HeaderGlob { get; init; }
    [JsonPropertyName("excluded_headers")] public ImmutableList<string> ExcludedHeaders { get; init; } = [];
    [JsonPropertyName("excluded_header_prefixes")] public ImmutableList<string> ExcludedHeaderPrefixes { get; init; } = [];
}

public sealed record RequiredFunctionConfig
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("return_type")] public required string ReturnType { get; init; }
    [JsonPropertyName("source_header")] public required string SourceHeader { get; init; }
    [JsonPropertyName("parameters")] public ImmutableList<RequiredFunctionParameter> Parameters { get; init; } = [];
}

public sealed record RequiredFunctionParameter
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
}

public sealed record DeferredDeclarationConfig
{
    [JsonPropertyName("category")] public required string Category { get; init; }
    [JsonPropertyName("reason")] public required string Reason { get; init; }
}

public sealed record DynapiConfig
{
    [JsonPropertyName("exports_glob")] public required string ExportsGlob { get; init; }
}
