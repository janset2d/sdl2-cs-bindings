using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

/// <summary>
/// Hand-curated public constant declaration recovered from a header that's
/// excluded from the per-header parse loop. Mirrors the pattern of
/// <see cref="RequiredFunctionConfig"/>: the manifest carries the explicit
/// declaration, including its managed type, value, and source header.
/// <see cref="Kind"/> describes the macro shape for selecting the C# output form.
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
