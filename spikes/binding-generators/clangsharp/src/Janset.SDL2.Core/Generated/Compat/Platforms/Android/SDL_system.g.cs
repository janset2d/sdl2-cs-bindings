using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_AndroidGetJNIEnv();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_AndroidGetActivity();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GetAndroidSDKVersion();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_IsAndroidTV();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_IsChromebook();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_IsDeXMode();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_AndroidBackButton();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_AndroidGetInternalStoragePath();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AndroidGetExternalStorageState();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_AndroidGetExternalStoragePath();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_AndroidRequestPermission([NativeTypeName("const char *")] byte* permission);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AndroidShowToast([NativeTypeName("const char *")] byte* message, int duration, int gravity, int xoffset, int yoffset);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AndroidSendMessage([NativeTypeName("Uint32")] uint command, int param1);

        [NativeTypeName("#define SDL_ANDROID_EXTERNAL_STORAGE_READ 0x01")]
        public const int SDL_ANDROID_EXTERNAL_STORAGE_READ = 0x01;

        [NativeTypeName("#define SDL_ANDROID_EXTERNAL_STORAGE_WRITE 0x02")]
        public const int SDL_ANDROID_EXTERNAL_STORAGE_WRITE = 0x02;
    }
}
