using System.Runtime.InteropServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetPlatform();

        [NativeTypeName("#define HAVE_WINAPIFAMILY_H 1")]
        public const int HAVE_WINAPIFAMILY_H = 1;

        [NativeTypeName("#define WINAPI_FAMILY_WINRT (!WINAPI_FAMILY_PARTITION(WINAPI_PARTITION_DESKTOP) && WINAPI_FAMILY_PARTITION(WINAPI_PARTITION_APP))")]
        public const bool WINAPI_FAMILY_WINRT = (!((100 == 100)) && ((100 == 100 || 100 == 2 || 100 == 3)));

        [NativeTypeName("#define SDL_WINAPI_FAMILY_PHONE (WINAPI_FAMILY == WINAPI_FAMILY_PHONE_APP)")]
        public const bool SDL_WINAPI_FAMILY_PHONE = (100 == 3);

        [NativeTypeName("#define __WINDOWS__ 1")]
        public const int __WINDOWS__ = 1;

        [NativeTypeName("#define __WIN32__ 1")]
        public const int __WIN32__ = 1;
    }
}
