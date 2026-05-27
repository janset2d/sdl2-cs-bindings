using System.Runtime.InteropServices;

namespace SDL2.Gfx
{
    public partial struct FPSmanager
    {
        [NativeTypeName("Uint32")]
        public uint framecount;

        public float rateticks;

        [NativeTypeName("Uint32")]
        public uint baseticks;

        [NativeTypeName("Uint32")]
        public uint lastticks;

        [NativeTypeName("Uint32")]
        public uint rate;
    }

    internal static unsafe partial class SDL2_gfxNative
    {
        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_initFramerate(FPSmanager* manager);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_setFramerate(FPSmanager* manager, [NativeTypeName("Uint32")] uint rate);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_getFramerate(FPSmanager* manager);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_getFramecount(FPSmanager* manager);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_framerateDelay(FPSmanager* manager);

        [NativeTypeName("#define FPS_UPPER_LIMIT 200")]
        public const int FPS_UPPER_LIMIT = 200;

        [NativeTypeName("#define FPS_LOWER_LIMIT 1")]
        public const int FPS_LOWER_LIMIT = 1;

        [NativeTypeName("#define FPS_DEFAULT 30")]
        public const int FPS_DEFAULT = 30;
    }
}
