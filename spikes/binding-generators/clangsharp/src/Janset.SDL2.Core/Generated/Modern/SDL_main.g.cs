using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_main(int argc, [NativeTypeName("char *[]")] byte** argv);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetMainReady();
    }
}
