using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public enum SDL_PowerState
    {
        SDL_POWERSTATE_UNKNOWN,
        SDL_POWERSTATE_ON_BATTERY,
        SDL_POWERSTATE_NO_BATTERY,
        SDL_POWERSTATE_CHARGING,
        SDL_POWERSTATE_CHARGED,
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_PowerState SDL_GetPowerInfo(int* seconds, int* percent);
    }
}
