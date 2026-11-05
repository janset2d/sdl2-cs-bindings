using System.Runtime.InteropServices;

namespace SDL2.Gfx
{
    internal static unsafe partial class SDL2_gfxNative
    {
        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterMMXdetect();

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_imageFilterMMXoff();

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_imageFilterMMXon();

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterAdd([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterMean([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterSub([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterAbsDiff([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterMult([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterMultNor([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterMultDivby2([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterMultDivby4([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterBitAnd([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterBitOr([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterDiv([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterBitNegation([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterAddByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte C);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterAddUint([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned int")] uint C);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterAddByteToHalf([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte C);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterSubByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte C);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterSubUint([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned int")] uint C);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterShiftRight([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterShiftRightUint([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterMultByByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte C);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterShiftRightAndMultByByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N, [NativeTypeName("unsigned char")] byte C);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterShiftLeftByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterShiftLeftUint([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterShiftLeft([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterBinarizeUsingThreshold([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte T);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterClipToRange([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte Tmin, [NativeTypeName("unsigned char")] byte Tmax);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_imageFilterNormalizeLinear([NativeTypeName("unsigned char *")] byte* Src, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, int Cmin, int Cmax, int Nmin, int Nmax);
    }
}
