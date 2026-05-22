using System.Runtime.InteropServices;

namespace SDL2
{
    public unsafe partial struct SDL_Locale
    {
        [NativeTypeName("const char *")]
        public byte* language;

        [NativeTypeName("const char *")]
        public byte* country;
    }

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Locale* SDL_GetPreferredLocales();
    }
}
