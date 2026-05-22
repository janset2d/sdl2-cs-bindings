using System.Runtime.InteropServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_GetBasePath();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_GetPrefPath([NativeTypeName("const char *")] byte* org, [NativeTypeName("const char *")] byte* app);
    }
}
