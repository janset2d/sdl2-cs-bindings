using System.Runtime.InteropServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_MetalView")]
        public static extern nint SDL_Metal_CreateView(SDL_Window window);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_Metal_DestroyView([NativeTypeName("SDL_MetalView")] nint view);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_Metal_GetLayer([NativeTypeName("SDL_MetalView")] nint view);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_Metal_GetDrawableSize(SDL_Window window, int* w, int* h);
    }
}
