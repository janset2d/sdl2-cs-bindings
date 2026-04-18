using SDL2;

namespace Compile.NetStandard;

/// <summary>
/// Compile-only sanity probe for the netstandard2.0 consumer slice. The purpose is
/// <b>compile-time binding check</b>: if this file compiles, netstandard2.0 consumers
/// can reference <c>Janset.SDL2.*</c> and resolve the wrapper types. The project is
/// never executed — netstandard2.0 is a contract, not a runtime.
/// </summary>
internal static class Probe
{
    /// <summary>
    /// Forces the compiler to resolve each wrapper type through its netstandard2.0
    /// surface. No runtime semantics — <c>typeof</c> is a compile-time operator and
    /// the field is intentionally unused.
    /// </summary>
    private static readonly Type[] _surface =
    [
        typeof(SDL.SDL_version),
        typeof(SDL_image),
        typeof(SDL_mixer),
        typeof(SDL_ttf),
        typeof(SDL_gfx),
    ];
}
