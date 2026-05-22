using System.Runtime.InteropServices;
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
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_LinuxSetThreadPriority([NativeTypeName("Sint64")] long threadID, int priority);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("linux")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_LinuxSetThreadPriorityAndPolicy([NativeTypeName("Sint64")] long threadID, int sdlPriority, int schedPolicy);
    }
}
