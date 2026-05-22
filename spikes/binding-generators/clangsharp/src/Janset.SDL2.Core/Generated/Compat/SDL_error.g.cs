using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_errorcode
    {
        SDL_ENOMEM,
        SDL_EFREAD,
        SDL_EFWRITE,
        SDL_EFSEEK,
        SDL_UNSUPPORTED,
        SDL_LASTERROR,
    }

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SetError([NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetError();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_GetErrorMsg([NativeTypeName("char *")] byte* errstr, int maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_ClearError();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_Error(SDL_errorcode code);
    }
}
