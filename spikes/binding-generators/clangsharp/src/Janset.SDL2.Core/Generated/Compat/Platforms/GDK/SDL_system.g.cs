using System;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{

    public partial struct XTaskQueueObject
    {
    }

    public partial struct XUser
    {
    }

    public static unsafe partial class SDLNative
    {

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GDKGetTaskQueue([NativeTypeName("XTaskQueueHandle *")] XTaskQueueObject** outTaskQueue);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GDKGetDefaultUser([NativeTypeName("XUserHandle *")] XUser** outUserHandle);
    }
}
