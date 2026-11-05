using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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
        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_initFramerate(FPSmanager* manager);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_setFramerate(FPSmanager* manager, [NativeTypeName("Uint32")] uint rate);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_getFramerate(FPSmanager* manager);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_getFramecount(FPSmanager* manager);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_framerateDelay(FPSmanager* manager);

        [NativeTypeName("#define FPS_UPPER_LIMIT 200")]
        public const int FPS_UPPER_LIMIT = 200;

        [NativeTypeName("#define FPS_LOWER_LIMIT 1")]
        public const int FPS_LOWER_LIMIT = 1;

        [NativeTypeName("#define FPS_DEFAULT 30")]
        public const int FPS_DEFAULT = 30;
    }
}
