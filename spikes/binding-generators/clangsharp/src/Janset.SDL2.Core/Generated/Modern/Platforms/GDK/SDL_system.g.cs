using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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

    internal static unsafe partial class SDLNative
    {

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GDKGetTaskQueue([NativeTypeName("XTaskQueueHandle *")] XTaskQueueObject** outTaskQueue);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GDKGetDefaultUser([NativeTypeName("XUserHandle *")] XUser** outUserHandle);
    }
}
