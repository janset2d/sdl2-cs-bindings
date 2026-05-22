using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{
    public static unsafe partial class SDLNative
    {

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows10.0.10240.0")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_WinRTRunApp([NativeTypeName("SDL_main_func")] delegate* unmanaged[Cdecl]<int, byte**, int> mainFunction, [NativeTypeName("void*")] nint reserved);
    }
}
