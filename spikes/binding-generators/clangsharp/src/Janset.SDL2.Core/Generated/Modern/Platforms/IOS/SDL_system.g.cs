using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("ios")]
        #endif
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_iPhoneSetAnimationCallback(SDL_Window* window, int interval, [NativeTypeName("SDL_iOSAnimationCallback")] delegate* unmanaged[Cdecl]<nint, void> callback, [NativeTypeName("void*")] nint callbackParam);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("ios")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_iPhoneSetEventPump(SDL_bool enabled);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("ios")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_OnApplicationDidChangeStatusBarOrientation();
    }
}
