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
        [SupportedOSPlatform("android")]
        #endif
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_AndroidGetJNIEnv();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_AndroidGetActivity();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetAndroidSDKVersion();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IsAndroidTV();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IsChromebook();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IsDeXMode();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_AndroidBackButton();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_AndroidGetInternalStoragePath();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_AndroidGetExternalStorageState();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_AndroidGetExternalStoragePath();

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_AndroidRequestPermission([NativeTypeName("const char *")] byte* permission);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_AndroidShowToast([NativeTypeName("const char *")] byte* message, int duration, int gravity, int xoffset, int yoffset);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("android")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_AndroidSendMessage([NativeTypeName("Uint32")] uint command, int param1);

        [NativeTypeName("#define SDL_ANDROID_EXTERNAL_STORAGE_READ 0x01")]
        public const int SDL_ANDROID_EXTERNAL_STORAGE_READ = 0x01;

        [NativeTypeName("#define SDL_ANDROID_EXTERNAL_STORAGE_WRITE 0x02")]
        public const int SDL_ANDROID_EXTERNAL_STORAGE_WRITE = 0x02;
    }
}
