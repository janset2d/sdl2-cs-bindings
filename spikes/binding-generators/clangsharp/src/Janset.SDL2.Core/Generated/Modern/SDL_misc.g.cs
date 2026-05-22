using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_OpenURL([NativeTypeName("const char *")] byte* url);
    }
}
