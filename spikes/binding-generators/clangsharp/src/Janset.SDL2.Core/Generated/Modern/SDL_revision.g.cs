using System;

namespace SDL2
{
    internal static partial class SDLNative
    {
        [NativeTypeName("#define SDL_REVISION_NUMBER 0")]
        public const int SDL_REVISION_NUMBER = 0;

        [NativeTypeName("#define SDL_REVISION \"SDL-2.32.10-no-vcs\"")]
        public static ReadOnlySpan<byte> SDL_REVISION => "SDL-2.32.10-no-vcs"u8;
    }
}
