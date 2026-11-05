using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2.Ttf
{

    public enum TTF_Direction
    {
        TTF_DIRECTION_LTR = 0,
        TTF_DIRECTION_RTL,
        TTF_DIRECTION_TTB,
        TTF_DIRECTION_BTT,
    }

    internal static unsafe partial class SDL_ttfNative
    {
        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const SDL_version *")]
        public static partial SDL_version* TTF_Linked_Version();

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_GetFreeTypeVersion(int* major, int* minor, int* patch);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_GetHarfBuzzVersion(int* major, int* minor, int* patch);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_ByteSwappedUNICODE(SDL_bool swapped);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_Init();

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial TTF_Font TTF_OpenFont([NativeTypeName("const char *")] byte* file, int ptsize);

        [LibraryImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndex")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial TTF_Font TTF_OpenFontIndex([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("long")] CLong index);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial TTF_Font TTF_OpenFontRW(SDL_RWops src, int freesrc, int ptsize);

        [LibraryImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndexRW")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial TTF_Font TTF_OpenFontIndexRW(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("long")] CLong index);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial TTF_Font TTF_OpenFontDPI([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [LibraryImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndexDPI")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial TTF_Font TTF_OpenFontIndexDPI([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("long")] CLong index, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial TTF_Font TTF_OpenFontDPIRW(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [LibraryImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndexDPIRW")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial TTF_Font TTF_OpenFontIndexDPIRW(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("long")] CLong index, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_SetFontSize(TTF_Font font, int ptsize);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_SetFontSizeDPI(TTF_Font font, int ptsize, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GetFontStyle([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_SetFontStyle(TTF_Font font, int style);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GetFontOutline([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_SetFontOutline(TTF_Font font, int outline);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GetFontHinting([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_SetFontHinting(TTF_Font font, int hinting);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GetFontWrappedAlign([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_SetFontWrappedAlign(TTF_Font font, int align);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_FontHeight([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_FontAscent([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_FontDescent([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_FontLineSkip([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_SetFontLineSkip(TTF_Font font, int lineskip);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GetFontKerning([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_SetFontKerning(TTF_Font font, int allowed);

        [LibraryImport("SDL2_ttf", EntryPoint = "TTF_FontFaces")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial CLong TTF_FontFaces([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_FontFaceIsFixedWidth([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* TTF_FontFaceFamilyName([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* TTF_FontFaceStyleName([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GlyphIsProvided(TTF_Font font, [NativeTypeName("Uint16")] ushort ch);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GlyphIsProvided32(TTF_Font font, [NativeTypeName("Uint32")] uint ch);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GlyphMetrics(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, int* minx, int* maxx, int* miny, int* maxy, int* advance);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GlyphMetrics32(TTF_Font font, [NativeTypeName("Uint32")] uint ch, int* minx, int* maxx, int* miny, int* maxy, int* advance);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_SizeText(TTF_Font font, [NativeTypeName("const char *")] byte* text, int* w, int* h);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_SizeUTF8(TTF_Font font, [NativeTypeName("const char *")] byte* text, int* w, int* h);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_SizeUNICODE(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, int* w, int* h);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_MeasureText(TTF_Font font, [NativeTypeName("const char *")] byte* text, int measure_width, int* extent, int* count);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_MeasureUTF8(TTF_Font font, [NativeTypeName("const char *")] byte* text, int measure_width, int* extent, int* count);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_MeasureUNICODE(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, int measure_width, int* extent, int* count);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderText_Solid(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUTF8_Solid(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUNICODE_Solid(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderText_Solid_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUTF8_Solid_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUNICODE_Solid_Wrapped(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderGlyph_Solid(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderGlyph32_Solid(TTF_Font font, [NativeTypeName("Uint32")] uint ch, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderText_Shaded(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUTF8_Shaded(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUNICODE_Shaded(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderText_Shaded_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUTF8_Shaded_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUNICODE_Shaded_Wrapped(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderGlyph_Shaded(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderGlyph32_Shaded(TTF_Font font, [NativeTypeName("Uint32")] uint ch, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderText_Blended(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUTF8_Blended(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUNICODE_Blended(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderText_Blended_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUTF8_Blended_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUNICODE_Blended_Wrapped(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderGlyph_Blended(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderGlyph32_Blended(TTF_Font font, [NativeTypeName("Uint32")] uint ch, SDL_Color fg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderText_LCD(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUTF8_LCD(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUNICODE_LCD(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderText_LCD_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUTF8_LCD_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderUNICODE_LCD_Wrapped(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderGlyph_LCD(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* TTF_RenderGlyph32_LCD(TTF_Font font, [NativeTypeName("Uint32")] uint ch, SDL_Color fg, SDL_Color bg);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_CloseFont(TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void TTF_Quit();

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_WasInit();

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GetFontKerningSizeGlyphs(TTF_Font font, [NativeTypeName("Uint16")] ushort previous_ch, [NativeTypeName("Uint16")] ushort ch);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_GetFontKerningSizeGlyphs32(TTF_Font font, [NativeTypeName("Uint32")] uint previous_ch, [NativeTypeName("Uint32")] uint ch);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_SetFontSDF(TTF_Font font, SDL_bool on_off);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool TTF_GetFontSDF([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_SetFontDirection(TTF_Font font, TTF_Direction direction);

        [LibraryImport("SDL2_ttf")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int TTF_SetFontScriptName(TTF_Font font, [NativeTypeName("const char *")] byte* script);

        [NativeTypeName("#define SDL_TTF_MAJOR_VERSION 2")]
        public const int SDL_TTF_MAJOR_VERSION = 2;

        [NativeTypeName("#define SDL_TTF_MINOR_VERSION 24")]
        public const int SDL_TTF_MINOR_VERSION = 24;

        [NativeTypeName("#define SDL_TTF_PATCHLEVEL 0")]
        public const int SDL_TTF_PATCHLEVEL = 0;

        [NativeTypeName("#define TTF_MAJOR_VERSION SDL_TTF_MAJOR_VERSION")]
        public const int TTF_MAJOR_VERSION = 2;

        [NativeTypeName("#define TTF_MINOR_VERSION SDL_TTF_MINOR_VERSION")]
        public const int TTF_MINOR_VERSION = 24;

        [NativeTypeName("#define TTF_PATCHLEVEL SDL_TTF_PATCHLEVEL")]
        public const int TTF_PATCHLEVEL = 0;

        [NativeTypeName("#define SDL_TTF_COMPILEDVERSION SDL_VERSIONNUM(SDL_TTF_MAJOR_VERSION, SDL_TTF_MINOR_VERSION, SDL_TTF_PATCHLEVEL)")]
        public const int SDL_TTF_COMPILEDVERSION = ((2) * 1000 + (24) * 100 + (0));

        [NativeTypeName("#define UNICODE_BOM_NATIVE 0xFEFF")]
        public const int UNICODE_BOM_NATIVE = 0xFEFF;

        [NativeTypeName("#define UNICODE_BOM_SWAPPED 0xFFFE")]
        public const int UNICODE_BOM_SWAPPED = 0xFFFE;

        [NativeTypeName("#define TTF_STYLE_NORMAL 0x00")]
        public const int TTF_STYLE_NORMAL = 0x00;

        [NativeTypeName("#define TTF_STYLE_BOLD 0x01")]
        public const int TTF_STYLE_BOLD = 0x01;

        [NativeTypeName("#define TTF_STYLE_ITALIC 0x02")]
        public const int TTF_STYLE_ITALIC = 0x02;

        [NativeTypeName("#define TTF_STYLE_UNDERLINE 0x04")]
        public const int TTF_STYLE_UNDERLINE = 0x04;

        [NativeTypeName("#define TTF_STYLE_STRIKETHROUGH 0x08")]
        public const int TTF_STYLE_STRIKETHROUGH = 0x08;

        [NativeTypeName("#define TTF_HINTING_NORMAL 0")]
        public const int TTF_HINTING_NORMAL = 0;

        [NativeTypeName("#define TTF_HINTING_LIGHT 1")]
        public const int TTF_HINTING_LIGHT = 1;

        [NativeTypeName("#define TTF_HINTING_MONO 2")]
        public const int TTF_HINTING_MONO = 2;

        [NativeTypeName("#define TTF_HINTING_NONE 3")]
        public const int TTF_HINTING_NONE = 3;

        [NativeTypeName("#define TTF_HINTING_LIGHT_SUBPIXEL 4")]
        public const int TTF_HINTING_LIGHT_SUBPIXEL = 4;

        [NativeTypeName("#define TTF_WRAPPED_ALIGN_LEFT 0")]
        public const int TTF_WRAPPED_ALIGN_LEFT = 0;

        [NativeTypeName("#define TTF_WRAPPED_ALIGN_CENTER 1")]
        public const int TTF_WRAPPED_ALIGN_CENTER = 1;

        [NativeTypeName("#define TTF_WRAPPED_ALIGN_RIGHT 2")]
        public const int TTF_WRAPPED_ALIGN_RIGHT = 2;
    }
}
