using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_LoadObject([NativeTypeName("const char *")] byte* sofile);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_LoadFunction([NativeTypeName("void*")] nint handle, [NativeTypeName("const char *")] byte* name);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_UnloadObject([NativeTypeName("void*")] nint handle);
    }
}
