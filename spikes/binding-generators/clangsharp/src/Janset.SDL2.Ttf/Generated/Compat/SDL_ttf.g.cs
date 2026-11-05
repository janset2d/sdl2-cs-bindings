using System.Runtime.InteropServices;

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
        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const SDL_version *")]
        public static extern SDL_version* TTF_Linked_Version();

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_GetFreeTypeVersion(int* major, int* minor, int* patch);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_GetHarfBuzzVersion(int* major, int* minor, int* patch);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_ByteSwappedUNICODE(SDL_bool swapped);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_Init();

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern TTF_Font TTF_OpenFont([NativeTypeName("const char *")] byte* file, int ptsize);

        public static TTF_Font TTF_OpenFontIndex([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("long")] int index)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return TTF_OpenFontIndex_Win32(file, ptsize, (int)index);
            return TTF_OpenFontIndex_Unix64(file, ptsize, (nint)index);
        }

        [DllImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndex", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TTF_Font TTF_OpenFontIndex_Win32([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("long")] int index);

        [DllImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndex", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TTF_Font TTF_OpenFontIndex_Unix64([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("long")] nint index);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern TTF_Font TTF_OpenFontRW(SDL_RWops src, int freesrc, int ptsize);

        public static TTF_Font TTF_OpenFontIndexRW(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("long")] int index)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return TTF_OpenFontIndexRW_Win32(src, freesrc, ptsize, (int)index);
            return TTF_OpenFontIndexRW_Unix64(src, freesrc, ptsize, (nint)index);
        }

        [DllImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndexRW", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TTF_Font TTF_OpenFontIndexRW_Win32(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("long")] int index);

        [DllImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndexRW", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TTF_Font TTF_OpenFontIndexRW_Unix64(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("long")] nint index);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern TTF_Font TTF_OpenFontDPI([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        public static TTF_Font TTF_OpenFontIndexDPI([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("long")] int index, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return TTF_OpenFontIndexDPI_Win32(file, ptsize, (int)index, hdpi, vdpi);
            return TTF_OpenFontIndexDPI_Unix64(file, ptsize, (nint)index, hdpi, vdpi);
        }

        [DllImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndexDPI", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TTF_Font TTF_OpenFontIndexDPI_Win32([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("long")] int index, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [DllImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndexDPI", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TTF_Font TTF_OpenFontIndexDPI_Unix64([NativeTypeName("const char *")] byte* file, int ptsize, [NativeTypeName("long")] nint index, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern TTF_Font TTF_OpenFontDPIRW(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        public static TTF_Font TTF_OpenFontIndexDPIRW(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("long")] int index, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return TTF_OpenFontIndexDPIRW_Win32(src, freesrc, ptsize, (int)index, hdpi, vdpi);
            return TTF_OpenFontIndexDPIRW_Unix64(src, freesrc, ptsize, (nint)index, hdpi, vdpi);
        }

        [DllImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndexDPIRW", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TTF_Font TTF_OpenFontIndexDPIRW_Win32(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("long")] int index, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [DllImport("SDL2_ttf", EntryPoint = "TTF_OpenFontIndexDPIRW", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern TTF_Font TTF_OpenFontIndexDPIRW_Unix64(SDL_RWops src, int freesrc, int ptsize, [NativeTypeName("long")] nint index, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_SetFontSize(TTF_Font font, int ptsize);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_SetFontSizeDPI(TTF_Font font, int ptsize, [NativeTypeName("unsigned int")] uint hdpi, [NativeTypeName("unsigned int")] uint vdpi);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GetFontStyle([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_SetFontStyle(TTF_Font font, int style);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GetFontOutline([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_SetFontOutline(TTF_Font font, int outline);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GetFontHinting([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_SetFontHinting(TTF_Font font, int hinting);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GetFontWrappedAlign([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_SetFontWrappedAlign(TTF_Font font, int align);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_FontHeight([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_FontAscent([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_FontDescent([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_FontLineSkip([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_SetFontLineSkip(TTF_Font font, int lineskip);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GetFontKerning([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_SetFontKerning(TTF_Font font, int allowed);

        [return: NativeTypeName("long")]
        public static long TTF_FontFaces([NativeTypeName("const TTF_Font *")] TTF_Font font)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return TTF_FontFaces_Win32(font);
            return (long)TTF_FontFaces_Unix64(font);
        }

        [DllImport("SDL2_ttf", EntryPoint = "TTF_FontFaces", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int TTF_FontFaces_Win32([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", EntryPoint = "TTF_FontFaces", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern nint TTF_FontFaces_Unix64([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_FontFaceIsFixedWidth([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* TTF_FontFaceFamilyName([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* TTF_FontFaceStyleName([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GlyphIsProvided(TTF_Font font, [NativeTypeName("Uint16")] ushort ch);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GlyphIsProvided32(TTF_Font font, [NativeTypeName("Uint32")] uint ch);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GlyphMetrics(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, int* minx, int* maxx, int* miny, int* maxy, int* advance);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GlyphMetrics32(TTF_Font font, [NativeTypeName("Uint32")] uint ch, int* minx, int* maxx, int* miny, int* maxy, int* advance);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_SizeText(TTF_Font font, [NativeTypeName("const char *")] byte* text, int* w, int* h);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_SizeUTF8(TTF_Font font, [NativeTypeName("const char *")] byte* text, int* w, int* h);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_SizeUNICODE(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, int* w, int* h);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_MeasureText(TTF_Font font, [NativeTypeName("const char *")] byte* text, int measure_width, int* extent, int* count);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_MeasureUTF8(TTF_Font font, [NativeTypeName("const char *")] byte* text, int measure_width, int* extent, int* count);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_MeasureUNICODE(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, int measure_width, int* extent, int* count);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderText_Solid(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUTF8_Solid(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUNICODE_Solid(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderText_Solid_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUTF8_Solid_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUNICODE_Solid_Wrapped(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderGlyph_Solid(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderGlyph32_Solid(TTF_Font font, [NativeTypeName("Uint32")] uint ch, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderText_Shaded(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUTF8_Shaded(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUNICODE_Shaded(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderText_Shaded_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUTF8_Shaded_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUNICODE_Shaded_Wrapped(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderGlyph_Shaded(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderGlyph32_Shaded(TTF_Font font, [NativeTypeName("Uint32")] uint ch, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderText_Blended(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUTF8_Blended(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUNICODE_Blended(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderText_Blended_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUTF8_Blended_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUNICODE_Blended_Wrapped(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderGlyph_Blended(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderGlyph32_Blended(TTF_Font font, [NativeTypeName("Uint32")] uint ch, SDL_Color fg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderText_LCD(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUTF8_LCD(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUNICODE_LCD(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderText_LCD_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUTF8_LCD_Wrapped(TTF_Font font, [NativeTypeName("const char *")] byte* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderUNICODE_LCD_Wrapped(TTF_Font font, [NativeTypeName("const Uint16 *")] ushort* text, SDL_Color fg, SDL_Color bg, [NativeTypeName("Uint32")] uint wrapLength);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderGlyph_LCD(TTF_Font font, [NativeTypeName("Uint16")] ushort ch, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Surface* TTF_RenderGlyph32_LCD(TTF_Font font, [NativeTypeName("Uint32")] uint ch, SDL_Color fg, SDL_Color bg);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_CloseFont(TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void TTF_Quit();

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_WasInit();

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GetFontKerningSizeGlyphs(TTF_Font font, [NativeTypeName("Uint16")] ushort previous_ch, [NativeTypeName("Uint16")] ushort ch);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_GetFontKerningSizeGlyphs32(TTF_Font font, [NativeTypeName("Uint32")] uint previous_ch, [NativeTypeName("Uint32")] uint ch);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_SetFontSDF(TTF_Font font, SDL_bool on_off);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool TTF_GetFontSDF([NativeTypeName("const TTF_Font *")] TTF_Font font);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_SetFontDirection(TTF_Font font, TTF_Direction direction);

        [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int TTF_SetFontScriptName(TTF_Font font, [NativeTypeName("const char *")] byte* script);

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
