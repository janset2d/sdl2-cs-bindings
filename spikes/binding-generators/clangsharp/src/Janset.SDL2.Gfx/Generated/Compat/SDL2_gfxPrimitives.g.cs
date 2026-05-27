using System.Runtime.InteropServices;

namespace SDL2.Gfx
{
    internal static unsafe partial class SDL2_gfxNative
    {
        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int pixelColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int pixelRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int hlineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int hlineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int vlineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int vlineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int rectangleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int rectangleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int roundedRectangleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int roundedRectangleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int boxColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int boxRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int roundedBoxColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int roundedBoxRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int lineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int lineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aalineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aalineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int thickLineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte width, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int thickLineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte width, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int circleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int circleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int arcColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int arcRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aacircleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aacircleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledCircleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short r, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledCircleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int ellipseColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int ellipseRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aaellipseColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aaellipseRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledEllipseColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledEllipseRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int pieColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int pieRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledPieColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledPieRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int trigonColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int trigonRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aatrigonColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aatrigonRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledTrigonColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledTrigonRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int polygonColor(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int polygonRGBA(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aapolygonColor(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aapolygonRGBA(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledPolygonColor(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledPolygonRGBA(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int texturedPolygon(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, SDL_Surface* texture, int texture_dx, int texture_dy);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int bezierColor(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, int s, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int bezierRGBA(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, int s, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void gfxPrimitivesSetFont([NativeTypeName("const void *")] nint fontdata, [NativeTypeName("Uint32")] uint cw, [NativeTypeName("Uint32")] uint ch);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void gfxPrimitivesSetFontRotation([NativeTypeName("Uint32")] uint rotation);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int characterColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("char")] byte c, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int characterRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("char")] byte c, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int stringColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("const char *")] byte* s, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int stringRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("const char *")] byte* s, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [NativeTypeName("#define SDL2_GFXPRIMITIVES_MAJOR 1")]
        public const int SDL2_GFXPRIMITIVES_MAJOR = 1;

        [NativeTypeName("#define SDL2_GFXPRIMITIVES_MINOR 0")]
        public const int SDL2_GFXPRIMITIVES_MINOR = 0;

        [NativeTypeName("#define SDL2_GFXPRIMITIVES_MICRO 4")]
        public const int SDL2_GFXPRIMITIVES_MICRO = 4;
    }
}
