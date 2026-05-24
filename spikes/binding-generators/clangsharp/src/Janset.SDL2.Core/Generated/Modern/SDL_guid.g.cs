using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2
{

    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GUIDToString(Guid guid, [NativeTypeName("char *")] byte* pszGUID, int cbGUID);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Guid SDL_GUIDFromString([NativeTypeName("const char *")] byte* pchGUID);
    }
}
