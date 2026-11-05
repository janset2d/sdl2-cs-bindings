using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2.Gfx
{
    internal static unsafe partial class SDL2_gfxNative
    {
        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterMMXdetect();

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_imageFilterMMXoff();

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_imageFilterMMXon();

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterAdd([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterMean([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterSub([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterAbsDiff([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterMult([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterMultNor([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterMultDivby2([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterMultDivby4([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterBitAnd([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterBitOr([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterDiv([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Src2, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterBitNegation([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterAddByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte C);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterAddUint([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned int")] uint C);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterAddByteToHalf([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte C);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterSubByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte C);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterSubUint([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned int")] uint C);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterShiftRight([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterShiftRightUint([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterMultByByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte C);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterShiftRightAndMultByByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N, [NativeTypeName("unsigned char")] byte C);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterShiftLeftByte([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterShiftLeftUint([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterShiftLeft([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte N);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterBinarizeUsingThreshold([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte T);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterClipToRange([NativeTypeName("unsigned char *")] byte* Src1, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, [NativeTypeName("unsigned char")] byte Tmin, [NativeTypeName("unsigned char")] byte Tmax);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_imageFilterNormalizeLinear([NativeTypeName("unsigned char *")] byte* Src, [NativeTypeName("unsigned char *")] byte* Dest, [NativeTypeName("unsigned int")] uint length, int Cmin, int Cmax, int Nmin, int Nmax);
    }
}
