using System;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GDKRunApp([NativeTypeName("SDL_main_func")] IntPtr mainFunction, [NativeTypeName("void*")] nint reserved);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_GDKSuspendComplete();
    }
}
