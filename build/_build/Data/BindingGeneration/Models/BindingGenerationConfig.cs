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

    /// <summary>
    /// Hand-curated declarations of public constants that survive only in headers
    /// excluded from the per-header parse loop (currently SDL.h — the umbrella TU is
    /// excluded for failure-isolation reasons documented in friction #7 of
    /// <c>docs/binding-autogen/research/binding-autogen-spike-findings.md</c>).
    /// SDL2 contributes the 10 <c>SDL_INIT_*</c> macros declared exclusively in SDL.h.
    /// Optional with empty-default; satellite placeholder entries leave it absent.
    /// Maintenance rationale lives in
    /// <c>docs/playbook/binding-generator-maintenance.md</c> §"Parse-time configuration
    /// surface (per family)" alongside <c>required_functions</c>.
    /// </summary>
    [JsonPropertyName("required_constants")] public ImmutableList<RequiredConstantConfig> RequiredConstants { get; init; } = [];

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

/// <summary>
/// Discriminates compile-time-literal constants (emit as <c>public const</c>)
/// from runtime-computed expressions (emit as <c>public static readonly</c>).
/// C# disallows non-literal expressions in <c>const</c>, so compound macros like
/// SDL2's <c>SDL_INIT_EVERYTHING</c> (bitwise-OR of other constants) MUST use
/// <c>static readonly</c>. Serializes as a JSON string (<c>"Literal"</c> /
/// <c>"Computed"</c>) via the converter on the type.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConstantKind
{
    /// <summary>Compile-time literal — emit as <c>public const &lt;type&gt; NAME = &lt;value&gt;;</c>.</summary>
    Literal,

    /// <summary>Runtime-computed expression — emit as <c>public static readonly &lt;type&gt; NAME = &lt;value&gt;;</c>.</summary>
    Computed,
}

/// <summary>
/// Hand-curated public constant declaration recovered from a header that's
/// excluded from the per-header parse loop. Mirrors the pattern of
/// <see cref="RequiredFunctionConfig"/>: the manifest carries the explicit
/// declaration, the translator merges it into the Neutral view at Phase 3D,
/// and <c>CsConstantEmitter</c> at Phase 3E switches on <see cref="Kind"/> to
/// pick between <c>public const</c> and <c>public static readonly</c> emit.
/// </summary>
public sealed record RequiredConstantConfig
{
    /// <summary>Identifier as it appears in C (e.g. <c>SDL_INIT_TIMER</c>).</summary>
    [JsonPropertyName("name")] public required string Name { get; init; }

    /// <summary>Managed type to emit (e.g. <c>"uint"</c>).</summary>
    [JsonPropertyName("type")] public required string Type { get; init; }

    /// <summary>
    /// Literal value (for <see cref="ConstantKind.Literal"/>) OR computed expression
    /// (for <see cref="ConstantKind.Computed"/>). Emitted verbatim into the .g.cs
    /// — must be valid C# syntax for the declared <see cref="Type"/>. The two cases
    /// share the field because <c>const</c> vs <c>static readonly</c> is the only
    /// semantic distinction; both consume a right-hand-side expression.
    /// </summary>
    [JsonPropertyName("value")] public required string Value { get; init; }

    /// <summary>Origin header (e.g. <c>"SDL.h"</c>). Documentation/audit only.</summary>
    [JsonPropertyName("source_header")] public required string SourceHeader { get; init; }

    /// <summary>See <see cref="ConstantKind"/>.</summary>
    [JsonPropertyName("kind")] public required ConstantKind Kind { get; init; }
}

public sealed record DynapiConfig
{
    [JsonPropertyName("exports_glob")] public required string ExportsGlob { get; init; }
}
