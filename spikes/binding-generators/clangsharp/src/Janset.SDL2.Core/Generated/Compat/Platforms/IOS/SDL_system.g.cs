using System;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SDL_iOSAnimationCallback([NativeTypeName("void*")] nint param0);

    internal static unsafe partial class SDLNative
    {
        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("ios")]
        #endif
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_iPhoneSetAnimationCallback(SDL_Window* window, int interval, [NativeTypeName("SDL_iOSAnimationCallback")] IntPtr callback, [NativeTypeName("void*")] nint callbackParam);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("ios")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_iPhoneSetEventPump(SDL_bool enabled);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("ios")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_OnApplicationDidChangeStatusBarOrientation();
    }
}
