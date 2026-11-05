using System;
using System.Runtime.InteropServices;

namespace SDL2
{

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_GUIDToString(Guid guid, [NativeTypeName("char *")] byte* pszGUID, int cbGUID);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Guid SDL_GUIDFromString([NativeTypeName("const char *")] byte* pchGUID);
    }
}
