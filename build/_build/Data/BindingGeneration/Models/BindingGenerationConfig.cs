using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

/// <summary>
/// Per-family binding-generation configuration deserialized from
/// <c>build/manifest.json library_manifests[].binding_generation</c>. Strongly-typed
/// surface — all field validation flows through <see cref="System.Text.Json"/>'s
/// <c>required</c>-keyword machinery via the shared
/// <see cref="Manifest.ManifestRepository"/> deserialization path; no
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

    [JsonPropertyName("parse_defines")] public ImmutableList<string> ParseDefines { get; init; } = [];
    [JsonPropertyName("clang_args")] public ImmutableList<string> ClangArgs { get; init; } = [];
    [JsonPropertyName("excluded_functions")] public ImmutableHashSet<string> ExcludedFunctions { get; init; } = [];
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

    [JsonPropertyName("macro_constants")]
    public MacroConstantPolicyConfig MacroConstants { get; init; } = new();

    [JsonPropertyName("deferred_declarations")]
    public ImmutableDictionary<string, DeferredDeclarationConfig> DeferredDeclarations { get; init; } = [];
    [JsonPropertyName("validators")] public ImmutableDictionary<string, bool> Validators { get; init; } = [];

    [JsonPropertyName("header_set")] public HeaderSetConfig? HeaderSet { get; init; }
    [JsonPropertyName("dynapi")] public DynapiConfig? Dynapi { get; init; }
}
