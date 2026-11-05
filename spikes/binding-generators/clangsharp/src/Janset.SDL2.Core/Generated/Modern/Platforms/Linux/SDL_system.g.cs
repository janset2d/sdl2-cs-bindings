using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{
    internal static partial class SDLNative
    {
        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("linux")]
        #endif
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_LinuxSetThreadPriority([NativeTypeName("Sint64")] long threadID, int priority);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("linux")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_LinuxSetThreadPriorityAndPolicy([NativeTypeName("Sint64")] long threadID, int sdlPriority, int schedPolicy);
    }
}
