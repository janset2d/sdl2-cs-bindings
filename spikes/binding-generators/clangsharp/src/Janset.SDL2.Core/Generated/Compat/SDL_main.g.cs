using System.Runtime.InteropServices;

namespace SDL2
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate int SDL_main_func(int argc, [NativeTypeName("char *[]")] byte** argv);

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_main(int argc, [NativeTypeName("char *[]")] byte** argv);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_SetMainReady();
    }
}
