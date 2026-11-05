using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2.Gfx
{
    internal static unsafe partial class SDL2_gfxNative
    {
        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* rotozoomSurface(SDL_Surface* src, double angle, double zoom, int smooth);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* rotozoomSurfaceXY(SDL_Surface* src, double angle, double zoomx, double zoomy, int smooth);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void rotozoomSurfaceSize(int width, int height, double angle, double zoom, int* dstwidth, int* dstheight);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void rotozoomSurfaceSizeXY(int width, int height, double angle, double zoomx, double zoomy, int* dstwidth, int* dstheight);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* zoomSurface(SDL_Surface* src, double zoomx, double zoomy, int smooth);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void zoomSurfaceSize(int width, int height, double zoomx, double zoomy, int* dstwidth, int* dstheight);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* shrinkSurface(SDL_Surface* src, int factorx, int factory);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* rotateSurface90Degrees(SDL_Surface* src, int numClockwiseTurns);

        [NativeTypeName("#define SMOOTHING_OFF 0")]
        public const int SMOOTHING_OFF = 0;

        [NativeTypeName("#define SMOOTHING_ON 1")]
        public const int SMOOTHING_ON = 1;
    }
}
