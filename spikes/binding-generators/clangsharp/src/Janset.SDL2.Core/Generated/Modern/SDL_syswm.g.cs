using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_SYSWM_TYPE
    {
        SDL_SYSWM_UNKNOWN,
        SDL_SYSWM_WINDOWS,
        SDL_SYSWM_X11,
        SDL_SYSWM_DIRECTFB,
        SDL_SYSWM_COCOA,
        SDL_SYSWM_UIKIT,
        SDL_SYSWM_WAYLAND,
        SDL_SYSWM_MIR,
        SDL_SYSWM_WINRT,
        SDL_SYSWM_ANDROID,
        SDL_SYSWM_VIVANTE,
        SDL_SYSWM_OS2,
        SDL_SYSWM_HAIKU,
        SDL_SYSWM_KMSDRM,
        SDL_SYSWM_RISCOS,
    }

    internal static partial class SDLNative
    {
        [NativeTypeName("#define SDL_METALVIEW_TAG 255")]
        public const int SDL_METALVIEW_TAG = 255;
    }
}
