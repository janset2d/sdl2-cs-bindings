using SDL2;

namespace Compile.NetStandard;

/// <summary>
/// Compile-only sanity probe for the netstandard2.0 consumer slice. If this file
/// compiles, netstandard2.0 consumers can reference Janset.SDL2.Core and
/// Janset.SDL2.Image and resolve core types. Not executed — netstandard2.0 is a
/// contract, not a runtime.
/// </summary>
internal static class Probe
{
    public static bool CanReferenceSdlTypes()
    {
        return typeof(SDL.SDL_version) != null
            && typeof(SDL_image) != null;
    }
}
