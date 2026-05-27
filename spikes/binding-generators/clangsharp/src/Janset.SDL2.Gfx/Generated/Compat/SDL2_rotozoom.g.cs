using System.Runtime.InteropServices;

namespace SDL2.Gfx
{
    internal static unsafe partial class SDL2_gfxNative
    {
        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* rotozoomSurface(SDL_Surface* src, double angle, double zoom, int smooth);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* rotozoomSurfaceXY(SDL_Surface* src, double angle, double zoomx, double zoomy, int smooth);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void rotozoomSurfaceSize(int width, int height, double angle, double zoom, int* dstwidth, int* dstheight);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void rotozoomSurfaceSizeXY(int width, int height, double angle, double zoomx, double zoomy, int* dstwidth, int* dstheight);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* zoomSurface(SDL_Surface* src, double zoomx, double zoomy, int smooth);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void zoomSurfaceSize(int width, int height, double zoomx, double zoomy, int* dstwidth, int* dstheight);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* shrinkSurface(SDL_Surface* src, int factorx, int factory);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* rotateSurface90Degrees(SDL_Surface* src, int numClockwiseTurns);

        [NativeTypeName("#define SMOOTHING_OFF 0")]
        public const int SMOOTHING_OFF = 0;

        [NativeTypeName("#define SMOOTHING_ON 1")]
        public const int SMOOTHING_ON = 1;
    }
}
