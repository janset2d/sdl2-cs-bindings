using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;

// Keyed driver tests leave no persistent hint, so reset restores their unset initial state.
internal sealed class SdlDriverScope : IDisposable
{
    private readonly string _name;

    public unsafe SdlDriverScope(string name, string value)
    {
        _name = name;
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        using PinnedUtf8 pinnedValue = SdlUtf8.Pin(value);

        SDL_bool result = SDLNative.SDL_SetHintWithPriority(pinnedName.Pointer, pinnedValue.Pointer, SDL_HintPriority.SDL_HINT_OVERRIDE);
        if (result != SDL_bool.SDL_TRUE)
        {
            throw new InvalidOperationException($"SDL_SetHintWithPriority failed for {name}: {SdlError.Current}");
        }
    }

    public unsafe void Dispose()
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(_name);
        SDLNative.SDL_ResetHint(pinnedName.Pointer);
    }
}
