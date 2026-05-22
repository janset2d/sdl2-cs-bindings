using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_version
    {
        [NativeTypeName("Uint8")]
        public byte major;

        [NativeTypeName("Uint8")]
        public byte minor;

        [NativeTypeName("Uint8")]
        public byte patch;
    }

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_GetVersion(SDL_version* ver);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetRevision();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int SDL_GetRevisionNumber();

        [NativeTypeName("#define SDL_MAJOR_VERSION 2")]
        public const int SDL_MAJOR_VERSION = 2;

        [NativeTypeName("#define SDL_MINOR_VERSION 32")]
        public const int SDL_MINOR_VERSION = 32;

        [NativeTypeName("#define SDL_PATCHLEVEL 10")]
        public const int SDL_PATCHLEVEL = 10;

        [NativeTypeName("#define SDL_COMPILEDVERSION SDL_VERSIONNUM(SDL_MAJOR_VERSION, SDL_MINOR_VERSION, SDL_PATCHLEVEL)")]
        public const int SDL_COMPILEDVERSION = ((2) * 1000 + (32) * 100 + (10));
    }
}
