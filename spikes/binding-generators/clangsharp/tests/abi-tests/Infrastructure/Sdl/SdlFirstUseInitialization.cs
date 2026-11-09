using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl;

public static class SdlFirstUseInitialization
{
    /// <summary>Initializes SDL once before parallel tests to avoid tick and error-slot first-use races.</summary>
    [Before(HookType.Assembly)]
    public static void InitializeLazyProcessState()
    {
        _ = SDLNative.SDL_GetTicks64();
        SDLNative.SDL_ClearError();
    }
}
