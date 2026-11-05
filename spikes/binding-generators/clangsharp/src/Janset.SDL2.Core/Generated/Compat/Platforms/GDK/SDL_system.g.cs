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
        public static extern int SDL_GDKGetTaskQueue([NativeTypeName("XTaskQueueHandle *")] nint* outTaskQueue);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GDKGetDefaultUser([NativeTypeName("XUserHandle *")] nint* outUserHandle);
    }
}
