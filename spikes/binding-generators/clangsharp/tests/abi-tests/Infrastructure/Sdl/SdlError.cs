using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl;

internal static unsafe class SdlError
{
    public static string Current => SdlUtf8.FromNullTerminated(SDLNative.SDL_GetError());

    public static void Clear() => SDLNative.SDL_ClearError();
}
