using System.Runtime.InteropServices;

namespace SDL2
{
    public unsafe partial struct SDL_GUID
    {
        [NativeTypeName("Uint8[16]")]
        public fixed byte data[16];
    }

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_GUIDToString(SDL_GUID guid, [NativeTypeName("char *")] byte* pszGUID, int cbGUID);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_GUID SDL_GUIDFromString([NativeTypeName("const char *")] byte* pchGUID);
    }
}
