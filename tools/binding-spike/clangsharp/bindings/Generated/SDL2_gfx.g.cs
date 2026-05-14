using System;
using System.Runtime.InteropServices;

namespace Janset.Spike.SDL2.Gfx
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

    public static unsafe partial class SDL_gfx
    {
        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int pixelColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int pixelRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int hlineColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int hlineRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int vlineColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int vlineRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int rectangleColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int rectangleRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int roundedRectangleColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int roundedRectangleRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int boxColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int boxRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int roundedBoxColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int roundedBoxRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int lineColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int lineRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aalineColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aalineRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int thickLineColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte width, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int thickLineRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Uint8")] byte width, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int circleColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int circleRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int arcColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int arcRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aacircleColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aacircleRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledCircleColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short r, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledCircleRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int ellipseColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int ellipseRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aaellipseColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aaellipseRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledEllipseColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledEllipseRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rx, [NativeTypeName("Sint16")] short ry, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int pieColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int pieRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledPieColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledPieRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("Sint16")] short rad, [NativeTypeName("Sint16")] short start, [NativeTypeName("Sint16")] short end, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int trigonColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int trigonRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aatrigonColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aatrigonRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledTrigonColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledTrigonRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x1, [NativeTypeName("Sint16")] short y1, [NativeTypeName("Sint16")] short x2, [NativeTypeName("Sint16")] short y2, [NativeTypeName("Sint16")] short x3, [NativeTypeName("Sint16")] short y3, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int polygonColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int polygonRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aapolygonColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int aapolygonRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledPolygonColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int filledPolygonRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int texturedPolygon([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, [NativeTypeName("SDL_Surface*")] IntPtr texture, int texture_dx, int texture_dy);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int bezierColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, int s, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int bezierRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("const Sint16 *")] short* vx, [NativeTypeName("const Sint16 *")] short* vy, int n, int s, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void gfxPrimitivesSetFont([NativeTypeName("const void *")] void* fontdata, [NativeTypeName("Uint32")] uint cw, [NativeTypeName("Uint32")] uint ch);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void gfxPrimitivesSetFontRotation([NativeTypeName("Uint32")] uint rotation);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int characterColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("char")] sbyte c, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int characterRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("char")] sbyte c, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int stringColor([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("const char *")] sbyte* s, [NativeTypeName("Uint32")] uint color);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int stringRGBA([NativeTypeName("SDL_Renderer*")] IntPtr renderer, [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y, [NativeTypeName("const char *")] sbyte* s, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [NativeTypeName("#define SDL2_GFXPRIMITIVES_MAJOR 1")]
        public const int SDL2_GFXPRIMITIVES_MAJOR = 1;

        [NativeTypeName("#define SDL2_GFXPRIMITIVES_MINOR 0")]
        public const int SDL2_GFXPRIMITIVES_MINOR = 0;

        [NativeTypeName("#define SDL2_GFXPRIMITIVES_MICRO 4")]
        public const int SDL2_GFXPRIMITIVES_MICRO = 4;

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

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Surface*")]
        public static extern IntPtr rotozoomSurface([NativeTypeName("SDL_Surface*")] IntPtr src, double angle, double zoom, int smooth);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Surface*")]
        public static extern IntPtr rotozoomSurfaceXY([NativeTypeName("SDL_Surface*")] IntPtr src, double angle, double zoomx, double zoomy, int smooth);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void rotozoomSurfaceSize(int width, int height, double angle, double zoom, int* dstwidth, int* dstheight);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void rotozoomSurfaceSizeXY(int width, int height, double angle, double zoomx, double zoomy, int* dstwidth, int* dstheight);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Surface*")]
        public static extern IntPtr zoomSurface([NativeTypeName("SDL_Surface*")] IntPtr src, double zoomx, double zoomy, int smooth);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void zoomSurfaceSize(int width, int height, double zoomx, double zoomy, int* dstwidth, int* dstheight);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Surface*")]
        public static extern IntPtr shrinkSurface([NativeTypeName("SDL_Surface*")] IntPtr src, int factorx, int factory);

        [DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Surface*")]
        public static extern IntPtr rotateSurface90Degrees([NativeTypeName("SDL_Surface*")] IntPtr src, int numClockwiseTurns);

        [NativeTypeName("#define SMOOTHING_OFF 0")]
        public const int SMOOTHING_OFF = 0;

        [NativeTypeName("#define SMOOTHING_ON 1")]
        public const int SMOOTHING_ON = 1;
    }
}
