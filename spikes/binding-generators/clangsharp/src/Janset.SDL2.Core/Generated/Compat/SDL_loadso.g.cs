using System.Runtime.InteropServices;

namespace SDL2
{
    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_LoadObject([NativeTypeName("const char *")] byte* sofile);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_LoadFunction([NativeTypeName("void*")] nint handle, [NativeTypeName("const char *")] byte* name);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_UnloadObject([NativeTypeName("void*")] nint handle);
    }
}
