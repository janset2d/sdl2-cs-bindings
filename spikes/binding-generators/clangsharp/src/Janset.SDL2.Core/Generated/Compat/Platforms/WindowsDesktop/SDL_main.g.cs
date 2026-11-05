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
        public static extern int SDL_RegisterApp([NativeTypeName("const char *")] byte* name, [NativeTypeName("Uint32")] uint style, [NativeTypeName("void*")] nint hInst);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_UnregisterApp();
    }
}
