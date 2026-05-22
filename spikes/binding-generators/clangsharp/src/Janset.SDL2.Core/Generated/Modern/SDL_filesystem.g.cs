using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_GetBasePath();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_GetPrefPath([NativeTypeName("const char *")] byte* org, [NativeTypeName("const char *")] byte* app);
    }
}
