using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2.Gfx
{
    internal static unsafe partial class SDL2_gfxNative
    {
        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int pixelColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int pixelRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int hlineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int hlineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int vlineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int vlineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int rectangleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int rectangleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int roundedRectangleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int roundedRectangleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int boxColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int boxRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int roundedBoxColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int roundedBoxRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int lineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int lineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aalineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aalineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int thickLineColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte width, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int thickLineRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte width, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int circleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int circleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int arcColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int arcRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aacircleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aacircleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledCircleColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short r, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledCircleRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int ellipseColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int ellipseRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aaellipseColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aaellipseRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledEllipseColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledEllipseRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int pieColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int pieRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledPieColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledPieRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int trigonColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int trigonRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aatrigonColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aatrigonRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledTrigonColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledTrigonRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int polygonColor(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int polygonRGBA(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aapolygonColor(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int aapolygonRGBA(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledPolygonColor(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int filledPolygonRGBA(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int texturedPolygon(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, SDL_Surface* texture, int texture_dx, int texture_dy);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int bezierColor(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, int s, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int bezierRGBA(SDL_Renderer renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, int s, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void gfxPrimitivesSetFont([NativeTypeName("const void *")] nint fontdata, [NativeTypeName("Uint32")] uint cw, [NativeTypeName("Uint32")] uint ch);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void gfxPrimitivesSetFontRotation([NativeTypeName("Uint32")] uint rotation);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int characterColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("char")] byte c, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int characterRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("char")] byte c, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int stringColor(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("const char *")] byte* s, [NativeTypeName("Uint32")] uint color);

        [LibraryImport("SDL2_gfx")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int stringRGBA(SDL_Renderer renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("const char *")] byte* s, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [NativeTypeName("#define SDL2_GFXPRIMITIVES_MAJOR 1")]
        public const int SDL2_GFXPRIMITIVES_MAJOR = 1;

        [NativeTypeName("#define SDL2_GFXPRIMITIVES_MINOR 0")]
        public const int SDL2_GFXPRIMITIVES_MINOR = 0;

        [NativeTypeName("#define SDL2_GFXPRIMITIVES_MICRO 4")]
        public const int SDL2_GFXPRIMITIVES_MICRO = 4;
    }
}
