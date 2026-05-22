namespace SDL2
{
    public static partial class SDLNative
    {
        [NativeTypeName("#define SDL_LIL_ENDIAN 1234")]
        public const int SDL_LIL_ENDIAN = 1234;

        [NativeTypeName("#define SDL_BIG_ENDIAN 4321")]
        public const int SDL_BIG_ENDIAN = 4321;

        [NativeTypeName("#define SDL_BYTEORDER SDL_LIL_ENDIAN")]
        public const int SDL_BYTEORDER = 1234;

        [NativeTypeName("#define SDL_FLOATWORDORDER SDL_BYTEORDER")]
        public const int SDL_FLOATWORDORDER = 1234;
    }
}
