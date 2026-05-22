using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{

    public enum SDL_WinRT_Path
    {
        SDL_WINRT_PATH_INSTALLED_LOCATION,
        SDL_WINRT_PATH_LOCAL_FOLDER,
        SDL_WINRT_PATH_ROAMING_FOLDER,
        SDL_WINRT_PATH_TEMP_FOLDER,
    }

    public enum SDL_WinRT_DeviceFamily
    {
        SDL_WINRT_DEVICEFAMILY_UNKNOWN,
        SDL_WINRT_DEVICEFAMILY_DESKTOP,
        SDL_WINRT_DEVICEFAMILY_MOBILE,
        SDL_WINRT_DEVICEFAMILY_XBOX,
    }

    public static unsafe partial class SDLNative
    {

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows10.0.10240.0")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const wchar_t *")]
        public static partial ushort* SDL_WinRTGetFSPathUNICODE(SDL_WinRT_Path pathType);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows10.0.10240.0")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_WinRTGetFSPathUTF8(SDL_WinRT_Path pathType);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows10.0.10240.0")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_WinRT_DeviceFamily SDL_WinRTGetDeviceFamily();
    }
}
