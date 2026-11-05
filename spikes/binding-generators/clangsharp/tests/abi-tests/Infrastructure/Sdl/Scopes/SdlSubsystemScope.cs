using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;

internal sealed class SdlSubsystemScope : IDisposable
{
    private readonly uint _flags;

    public SdlSubsystemScope(uint flags)
    {
        _flags = flags;

        if (SDLNative.SDL_InitSubSystem(flags) != 0)
        {
            throw new InvalidOperationException($"SDL_InitSubSystem failed: {SdlError.Current}");
        }
    }

    public void Dispose()
    {
        SDLNative.SDL_QuitSubSystem(_flags);
    }
}
