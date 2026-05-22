using System;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{
    public static unsafe partial class SDLNative
    {
        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("ios")]
        #endif
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_UIKitRunApp(int argc, [NativeTypeName("char *[]")] byte** argv, [NativeTypeName("SDL_main_func")] IntPtr mainFunction);
    }
}
