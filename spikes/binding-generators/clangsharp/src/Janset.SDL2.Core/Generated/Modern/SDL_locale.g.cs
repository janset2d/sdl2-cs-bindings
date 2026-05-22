using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Locale* SDL_GetPreferredLocales();
    }
}
