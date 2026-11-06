using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;
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
