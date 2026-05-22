using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SDL2.SDL_ArrayOrder;
using static SDL2.SDL_BitmapOrder;
using static SDL2.SDL_PackedLayout;
using static SDL2.SDL_PackedOrder;
using static SDL2.SDL_PixelType;

namespace SDL2
{
    public enum SDL_PixelType
    {
        SDL_PIXELTYPE_UNKNOWN,
        SDL_PIXELTYPE_INDEX1,
        SDL_PIXELTYPE_INDEX4,
        SDL_PIXELTYPE_INDEX8,
        SDL_PIXELTYPE_PACKED8,
        SDL_PIXELTYPE_PACKED16,
        SDL_PIXELTYPE_PACKED32,
        SDL_PIXELTYPE_ARRAYU8,
        SDL_PIXELTYPE_ARRAYU16,
        SDL_PIXELTYPE_ARRAYU32,
        SDL_PIXELTYPE_ARRAYF16,
        SDL_PIXELTYPE_ARRAYF32,
        SDL_PIXELTYPE_INDEX2,
    }

    public enum SDL_BitmapOrder
    {
        SDL_BITMAPORDER_NONE,
        SDL_BITMAPORDER_4321,
        SDL_BITMAPORDER_1234,
    }

    public enum SDL_PackedOrder
    {
        SDL_PACKEDORDER_NONE,
        SDL_PACKEDORDER_XRGB,
        SDL_PACKEDORDER_RGBX,
        SDL_PACKEDORDER_ARGB,
        SDL_PACKEDORDER_RGBA,
        SDL_PACKEDORDER_XBGR,
        SDL_PACKEDORDER_BGRX,
        SDL_PACKEDORDER_ABGR,
        SDL_PACKEDORDER_BGRA,
    }

    public enum SDL_ArrayOrder
    {
        SDL_ARRAYORDER_NONE,
        SDL_ARRAYORDER_RGB,
        SDL_ARRAYORDER_RGBA,
        SDL_ARRAYORDER_ARGB,
        SDL_ARRAYORDER_BGR,
        SDL_ARRAYORDER_BGRA,
        SDL_ARRAYORDER_ABGR,
    }

    public enum SDL_PackedLayout
    {
        SDL_PACKEDLAYOUT_NONE,
        SDL_PACKEDLAYOUT_332,
        SDL_PACKEDLAYOUT_4444,
        SDL_PACKEDLAYOUT_1555,
        SDL_PACKEDLAYOUT_5551,
        SDL_PACKEDLAYOUT_565,
        SDL_PACKEDLAYOUT_8888,
        SDL_PACKEDLAYOUT_2101010,
        SDL_PACKEDLAYOUT_1010102,
    }

    [NativeTypeName("int")]
    public enum SDL_PixelFormatEnum : uint
    {
        SDL_PIXELFORMAT_UNKNOWN,
        SDL_PIXELFORMAT_INDEX1LSB = ((1 << 28) | ((SDL_PIXELTYPE_INDEX1) << 24) | ((SDL_BITMAPORDER_4321) << 20) | ((0) << 16) | ((1) << 8) | ((0) << 0)),
        SDL_PIXELFORMAT_INDEX1MSB = ((1 << 28) | ((SDL_PIXELTYPE_INDEX1) << 24) | ((SDL_BITMAPORDER_1234) << 20) | ((0) << 16) | ((1) << 8) | ((0) << 0)),
        SDL_PIXELFORMAT_INDEX2LSB = ((1 << 28) | ((SDL_PIXELTYPE_INDEX2) << 24) | ((SDL_BITMAPORDER_4321) << 20) | ((0) << 16) | ((2) << 8) | ((0) << 0)),
        SDL_PIXELFORMAT_INDEX2MSB = ((1 << 28) | ((SDL_PIXELTYPE_INDEX2) << 24) | ((SDL_BITMAPORDER_1234) << 20) | ((0) << 16) | ((2) << 8) | ((0) << 0)),
        SDL_PIXELFORMAT_INDEX4LSB = ((1 << 28) | ((SDL_PIXELTYPE_INDEX4) << 24) | ((SDL_BITMAPORDER_4321) << 20) | ((0) << 16) | ((4) << 8) | ((0) << 0)),
        SDL_PIXELFORMAT_INDEX4MSB = ((1 << 28) | ((SDL_PIXELTYPE_INDEX4) << 24) | ((SDL_BITMAPORDER_1234) << 20) | ((0) << 16) | ((4) << 8) | ((0) << 0)),
        SDL_PIXELFORMAT_INDEX8 = ((1 << 28) | ((SDL_PIXELTYPE_INDEX8) << 24) | ((0) << 20) | ((0) << 16) | ((8) << 8) | ((1) << 0)),
        SDL_PIXELFORMAT_RGB332 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED8) << 24) | ((SDL_PACKEDORDER_XRGB) << 20) | ((SDL_PACKEDLAYOUT_332) << 16) | ((8) << 8) | ((1) << 0)),
        SDL_PIXELFORMAT_XRGB4444 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_XRGB) << 20) | ((SDL_PACKEDLAYOUT_4444) << 16) | ((12) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_RGB444 = SDL_PIXELFORMAT_XRGB4444,
        SDL_PIXELFORMAT_XBGR4444 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_XBGR) << 20) | ((SDL_PACKEDLAYOUT_4444) << 16) | ((12) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_BGR444 = SDL_PIXELFORMAT_XBGR4444,
        SDL_PIXELFORMAT_XRGB1555 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_XRGB) << 20) | ((SDL_PACKEDLAYOUT_1555) << 16) | ((15) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_RGB555 = SDL_PIXELFORMAT_XRGB1555,
        SDL_PIXELFORMAT_XBGR1555 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_XBGR) << 20) | ((SDL_PACKEDLAYOUT_1555) << 16) | ((15) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_BGR555 = SDL_PIXELFORMAT_XBGR1555,
        SDL_PIXELFORMAT_ARGB4444 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_ARGB) << 20) | ((SDL_PACKEDLAYOUT_4444) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_RGBA4444 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_RGBA) << 20) | ((SDL_PACKEDLAYOUT_4444) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_ABGR4444 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_ABGR) << 20) | ((SDL_PACKEDLAYOUT_4444) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_BGRA4444 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_BGRA) << 20) | ((SDL_PACKEDLAYOUT_4444) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_ARGB1555 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_ARGB) << 20) | ((SDL_PACKEDLAYOUT_1555) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_RGBA5551 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_RGBA) << 20) | ((SDL_PACKEDLAYOUT_5551) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_ABGR1555 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_ABGR) << 20) | ((SDL_PACKEDLAYOUT_1555) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_BGRA5551 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_BGRA) << 20) | ((SDL_PACKEDLAYOUT_5551) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_RGB565 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_XRGB) << 20) | ((SDL_PACKEDLAYOUT_565) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_BGR565 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED16) << 24) | ((SDL_PACKEDORDER_XBGR) << 20) | ((SDL_PACKEDLAYOUT_565) << 16) | ((16) << 8) | ((2) << 0)),
        SDL_PIXELFORMAT_RGB24 = ((1 << 28) | ((SDL_PIXELTYPE_ARRAYU8) << 24) | ((SDL_ARRAYORDER_RGB) << 20) | ((0) << 16) | ((24) << 8) | ((3) << 0)),
        SDL_PIXELFORMAT_BGR24 = ((1 << 28) | ((SDL_PIXELTYPE_ARRAYU8) << 24) | ((SDL_ARRAYORDER_BGR) << 20) | ((0) << 16) | ((24) << 8) | ((3) << 0)),
        SDL_PIXELFORMAT_XRGB8888 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED32) << 24) | ((SDL_PACKEDORDER_XRGB) << 20) | ((SDL_PACKEDLAYOUT_8888) << 16) | ((24) << 8) | ((4) << 0)),
        SDL_PIXELFORMAT_RGB888 = SDL_PIXELFORMAT_XRGB8888,
        SDL_PIXELFORMAT_RGBX8888 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED32) << 24) | ((SDL_PACKEDORDER_RGBX) << 20) | ((SDL_PACKEDLAYOUT_8888) << 16) | ((24) << 8) | ((4) << 0)),
        SDL_PIXELFORMAT_XBGR8888 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED32) << 24) | ((SDL_PACKEDORDER_XBGR) << 20) | ((SDL_PACKEDLAYOUT_8888) << 16) | ((24) << 8) | ((4) << 0)),
        SDL_PIXELFORMAT_BGR888 = SDL_PIXELFORMAT_XBGR8888,
        SDL_PIXELFORMAT_BGRX8888 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED32) << 24) | ((SDL_PACKEDORDER_BGRX) << 20) | ((SDL_PACKEDLAYOUT_8888) << 16) | ((24) << 8) | ((4) << 0)),
        SDL_PIXELFORMAT_ARGB8888 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED32) << 24) | ((SDL_PACKEDORDER_ARGB) << 20) | ((SDL_PACKEDLAYOUT_8888) << 16) | ((32) << 8) | ((4) << 0)),
        SDL_PIXELFORMAT_RGBA8888 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED32) << 24) | ((SDL_PACKEDORDER_RGBA) << 20) | ((SDL_PACKEDLAYOUT_8888) << 16) | ((32) << 8) | ((4) << 0)),
        SDL_PIXELFORMAT_ABGR8888 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED32) << 24) | ((SDL_PACKEDORDER_ABGR) << 20) | ((SDL_PACKEDLAYOUT_8888) << 16) | ((32) << 8) | ((4) << 0)),
        SDL_PIXELFORMAT_BGRA8888 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED32) << 24) | ((SDL_PACKEDORDER_BGRA) << 20) | ((SDL_PACKEDLAYOUT_8888) << 16) | ((32) << 8) | ((4) << 0)),
        SDL_PIXELFORMAT_ARGB2101010 = ((1 << 28) | ((SDL_PIXELTYPE_PACKED32) << 24) | ((SDL_PACKEDORDER_ARGB) << 20) | ((SDL_PACKEDLAYOUT_2101010) << 16) | ((32) << 8) | ((4) << 0)),
        SDL_PIXELFORMAT_RGBA32 = SDL_PIXELFORMAT_ABGR8888,
        SDL_PIXELFORMAT_ARGB32 = SDL_PIXELFORMAT_BGRA8888,
        SDL_PIXELFORMAT_BGRA32 = SDL_PIXELFORMAT_ARGB8888,
        SDL_PIXELFORMAT_ABGR32 = SDL_PIXELFORMAT_RGBA8888,
        SDL_PIXELFORMAT_RGBX32 = SDL_PIXELFORMAT_XBGR8888,
        SDL_PIXELFORMAT_XRGB32 = SDL_PIXELFORMAT_BGRX8888,
        SDL_PIXELFORMAT_BGRX32 = SDL_PIXELFORMAT_XRGB8888,
        SDL_PIXELFORMAT_XBGR32 = SDL_PIXELFORMAT_RGBX8888,
        SDL_PIXELFORMAT_YV12 = (unchecked(((uint)((byte)('Y')) << 0) | ((uint)((byte)('V')) << 8) | ((uint)((byte)('1')) << 16) | ((uint)((byte)('2')) << 24))),
        SDL_PIXELFORMAT_IYUV = (unchecked(((uint)((byte)('I')) << 0) | ((uint)((byte)('Y')) << 8) | ((uint)((byte)('U')) << 16) | ((uint)((byte)('V')) << 24))),
        SDL_PIXELFORMAT_YUY2 = (unchecked(((uint)((byte)('Y')) << 0) | ((uint)((byte)('U')) << 8) | ((uint)((byte)('Y')) << 16) | ((uint)((byte)('2')) << 24))),
        SDL_PIXELFORMAT_UYVY = (unchecked(((uint)((byte)('U')) << 0) | ((uint)((byte)('Y')) << 8) | ((uint)((byte)('V')) << 16) | ((uint)((byte)('Y')) << 24))),
        SDL_PIXELFORMAT_YVYU = (unchecked(((uint)((byte)('Y')) << 0) | ((uint)((byte)('V')) << 8) | ((uint)((byte)('Y')) << 16) | ((uint)((byte)('U')) << 24))),
        SDL_PIXELFORMAT_NV12 = (unchecked(((uint)((byte)('N')) << 0) | ((uint)((byte)('V')) << 8) | ((uint)((byte)('1')) << 16) | ((uint)((byte)('2')) << 24))),
        SDL_PIXELFORMAT_NV21 = (unchecked(((uint)((byte)('N')) << 0) | ((uint)((byte)('V')) << 8) | ((uint)((byte)('2')) << 16) | ((uint)((byte)('1')) << 24))),
        SDL_PIXELFORMAT_EXTERNAL_OES = (unchecked(((uint)((byte)('O')) << 0) | ((uint)((byte)('E')) << 8) | ((uint)((byte)('S')) << 16) | ((uint)((byte)(' ')) << 24))),
    }

    public partial struct SDL_Color
    {
        [NativeTypeName("Uint8")]
        public byte r;

        [NativeTypeName("Uint8")]
        public byte g;

        [NativeTypeName("Uint8")]
        public byte b;

        [NativeTypeName("Uint8")]
        public byte a;
    }

    public unsafe partial struct SDL_Palette
    {
        public int ncolors;

        public SDL_Color* colors;

        [NativeTypeName("Uint32")]
        public uint version;

        public int refcount;
    }

    public unsafe partial struct SDL_PixelFormat
    {
        [NativeTypeName("Uint32")]
        public uint format;

        public SDL_Palette* palette;

        [NativeTypeName("Uint8")]
        public byte BitsPerPixel;

        [NativeTypeName("Uint8")]
        public byte BytesPerPixel;

        [NativeTypeName("Uint8[2]")]
        public _padding_e__FixedBuffer padding;

        [NativeTypeName("Uint32")]
        public uint Rmask;

        [NativeTypeName("Uint32")]
        public uint Gmask;

        [NativeTypeName("Uint32")]
        public uint Bmask;

        [NativeTypeName("Uint32")]
        public uint Amask;

        [NativeTypeName("Uint8")]
        public byte Rloss;

        [NativeTypeName("Uint8")]
        public byte Gloss;

        [NativeTypeName("Uint8")]
        public byte Bloss;

        [NativeTypeName("Uint8")]
        public byte Aloss;

        [NativeTypeName("Uint8")]
        public byte Rshift;

        [NativeTypeName("Uint8")]
        public byte Gshift;

        [NativeTypeName("Uint8")]
        public byte Bshift;

        [NativeTypeName("Uint8")]
        public byte Ashift;

        public int refcount;

        [NativeTypeName("struct SDL_PixelFormat *")]
        public SDL_PixelFormat* next;

        [InlineArray(2)]
        public partial struct _padding_e__FixedBuffer
        {
            public byte e0;
        }
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetPixelFormatName([NativeTypeName("Uint32")] uint format);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_PixelFormatEnumToMasks([NativeTypeName("Uint32")] uint format, int* bpp, [NativeTypeName("Uint32 *")] uint* Rmask, [NativeTypeName("Uint32 *")] uint* Gmask, [NativeTypeName("Uint32 *")] uint* Bmask, [NativeTypeName("Uint32 *")] uint* Amask);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_MasksToPixelFormatEnum(int bpp, [NativeTypeName("Uint32")] uint Rmask, [NativeTypeName("Uint32")] uint Gmask, [NativeTypeName("Uint32")] uint Bmask, [NativeTypeName("Uint32")] uint Amask);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_PixelFormat* SDL_AllocFormat([NativeTypeName("Uint32")] uint pixel_format);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_FreeFormat(SDL_PixelFormat* format);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Palette* SDL_AllocPalette(int ncolors);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetPixelFormatPalette(SDL_PixelFormat* format, SDL_Palette* palette);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetPaletteColors(SDL_Palette* palette, [NativeTypeName("const SDL_Color *")] SDL_Color* colors, int firstcolor, int ncolors);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_FreePalette(SDL_Palette* palette);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_MapRGB([NativeTypeName("const SDL_PixelFormat *")] SDL_PixelFormat* format, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_MapRGBA([NativeTypeName("const SDL_PixelFormat *")] SDL_PixelFormat* format, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GetRGB([NativeTypeName("Uint32")] uint pixel, [NativeTypeName("const SDL_PixelFormat *")] SDL_PixelFormat* format, [NativeTypeName("Uint8 *")] byte* r, [NativeTypeName("Uint8 *")] byte* g, [NativeTypeName("Uint8 *")] byte* b);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GetRGBA([NativeTypeName("Uint32")] uint pixel, [NativeTypeName("const SDL_PixelFormat *")] SDL_PixelFormat* format, [NativeTypeName("Uint8 *")] byte* r, [NativeTypeName("Uint8 *")] byte* g, [NativeTypeName("Uint8 *")] byte* b, [NativeTypeName("Uint8 *")] byte* a);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_CalculateGammaRamp(float gamma, [NativeTypeName("Uint16 *")] ushort* ramp);

        [NativeTypeName("#define SDL_ALPHA_OPAQUE 255")]
        public const int SDL_ALPHA_OPAQUE = 255;

        [NativeTypeName("#define SDL_ALPHA_TRANSPARENT 0")]
        public const int SDL_ALPHA_TRANSPARENT = 0;
    }
}
