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
    /// excluded from the per-header parse loop. SDL.h is intentionally excluded
    /// because umbrella parsing collapses the SDL2 header set into one translation
    /// unit and reintroduces intrinsic-header and platform-conditioned parse failures
    /// that per-header parsing avoids.
    /// SDL2 contributes the <c>SDL_INIT_*</c> macros declared exclusively in SDL.h
    /// plus explicitly promoted string-like macros that Stage 1 treats as core
    /// API readiness probes.
    /// Optional with empty-default; satellite placeholder entries leave it absent.
    /// Maintenance rationale lives in
    /// <c>docs/playbook/binding-generator-maintenance.md</c> §"Parse-time configuration
    /// surface (per family)" alongside <c>required_functions</c>.
    /// </summary>
    [JsonPropertyName("required_constants")] public ImmutableList<RequiredConstantConfig> RequiredConstants { get; init; } = [];

    [JsonPropertyName("macro_constants")]
    public MacroConstantPolicyConfig MacroConstants { get; init; } = new();

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
/// Describes the C macro shape recovered into a binding constant. Both literal
/// numeric macros and C-computed numeric expressions can still emit as
/// <c>public const</c> when the right-hand side is a valid C# compile-time
/// constant expression, such as SDL2's <c>SDL_INIT_EVERYTHING</c> bitwise OR.
/// Serializes as a JSON string (<c>"Literal"</c> / <c>"Computed"</c>) via the
/// converter on the type.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConstantKind
{
    /// <summary>Compile-time literal — emit as <c>public const &lt;type&gt; NAME = &lt;value&gt;;</c>.</summary>
    Literal,

    /// <summary>C macro expression — emit as <c>public const</c> when the value is a valid C# compile-time expression.</summary>
    Computed,
}

/// <summary>
/// Hand-curated public constant declaration recovered from a header that's
/// excluded from the per-header parse loop. Mirrors the pattern of
/// <see cref="RequiredFunctionConfig"/>: the manifest carries the explicit
/// declaration, the translator merges it into the Neutral view, and
/// <c>ConstantEmitter</c> uses <see cref="Kind"/> as macro-shape metadata while
/// selecting the C# output form.
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

    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("allow_stale")] public bool AllowStale { get; init; }
}

public sealed record MacroConstantPolicyConfig
{
    [JsonPropertyName("excluded")]
    public ImmutableDictionary<string, ManualMacroConstantPolicyEntry> Excluded { get; init; } =
        ImmutableDictionary<string, ManualMacroConstantPolicyEntry>.Empty;

    [JsonPropertyName("overrides")]
    public ImmutableDictionary<string, MacroConstantOverrideConfig> Overrides { get; init; } =
        ImmutableDictionary<string, MacroConstantOverrideConfig>.Empty;
}

public sealed record ManualMacroConstantPolicyEntry
{
    [JsonPropertyName("reason")] public required string Reason { get; init; }
    [JsonPropertyName("allow_stale")] public bool AllowStale { get; init; }
}

public sealed record MacroConstantOverrideConfig
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("value")] public required string Value { get; init; }
    [JsonPropertyName("source_header")] public required string SourceHeader { get; init; }
    [JsonPropertyName("kind")] public required ConstantKind Kind { get; init; }
    [JsonPropertyName("reason")] public required string Reason { get; init; }
    [JsonPropertyName("allow_stale")] public bool AllowStale { get; init; }
}

public sealed record DynapiConfig
{
    [JsonPropertyName("exports_glob")] public required string ExportsGlob { get; init; }
}
