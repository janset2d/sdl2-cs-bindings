using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_MetalView")]
        public static partial nint SDL_Metal_CreateView(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_Metal_DestroyView([NativeTypeName("SDL_MetalView")] nint view);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_Metal_GetLayer([NativeTypeName("SDL_MetalView")] nint view);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_Metal_GetDrawableSize(SDL_Window* window, int* w, int* h);
    }
}
