using System;

namespace SDL2
{
    internal static partial class SDLNative
    {
        [NativeTypeName("#define SDL_REVISION_NUMBER 0")]
        public const int SDL_REVISION_NUMBER = 0;

        [NativeTypeName("#define SDL_REVISION \"SDL-2.32.10-no-vcs\"")]
        public static ReadOnlySpan<byte> SDL_REVISION => new byte[] { 0x53, 0x44, 0x4C, 0x2D, 0x32, 0x2E, 0x33, 0x32, 0x2E, 0x31, 0x30, 0x2D, 0x6E, 0x6F, 0x2D, 0x76, 0x63, 0x73, 0x00 };
    }
}
