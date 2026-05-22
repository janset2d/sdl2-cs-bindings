using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_RendererFlags
    {
        SDL_RENDERER_SOFTWARE = 0x00000001,
        SDL_RENDERER_ACCELERATED = 0x00000002,
        SDL_RENDERER_PRESENTVSYNC = 0x00000004,
        SDL_RENDERER_TARGETTEXTURE = 0x00000008,
    }

    public unsafe partial struct SDL_RendererInfo
    {
        [NativeTypeName("const char *")]
        public byte* name;

        [NativeTypeName("Uint32")]
        public uint flags;

        [NativeTypeName("Uint32")]
        public uint num_texture_formats;

        [NativeTypeName("Uint32[16]")]
        public _texture_formats_e__FixedBuffer texture_formats;

        public int max_texture_width;

        public int max_texture_height;

        [InlineArray(16)]
        public partial struct _texture_formats_e__FixedBuffer
        {
            public uint e0;
        }
    }

    public partial struct SDL_Vertex
    {
        public SDL_FPoint position;

        public SDL_Color color;

        public SDL_FPoint tex_coord;
    }

    public enum SDL_ScaleMode
    {
        SDL_ScaleModeNearest,
        SDL_ScaleModeLinear,
        SDL_ScaleModeBest,
    }

    public enum SDL_TextureAccess
    {
        SDL_TEXTUREACCESS_STATIC,
        SDL_TEXTUREACCESS_STREAMING,
        SDL_TEXTUREACCESS_TARGET,
    }

    public enum SDL_TextureModulate
    {
        SDL_TEXTUREMODULATE_NONE = 0x00000000,
        SDL_TEXTUREMODULATE_COLOR = 0x00000001,
        SDL_TEXTUREMODULATE_ALPHA = 0x00000002,
    }

    public enum SDL_RendererFlip
    {
        SDL_FLIP_NONE = 0x00000000,
        SDL_FLIP_HORIZONTAL = 0x00000001,
        SDL_FLIP_VERTICAL = 0x00000002,
    }

    public partial struct SDL_Renderer
    {
    }

    public partial struct SDL_Texture
    {
    }

    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetNumRenderDrivers();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetRenderDriverInfo(int index, SDL_RendererInfo* info);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_CreateWindowAndRenderer(int width, int height, [NativeTypeName("Uint32")] uint window_flags, SDL_Window** window, SDL_Renderer** renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Renderer* SDL_CreateRenderer(SDL_Window* window, int index, [NativeTypeName("Uint32")] uint flags);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Renderer* SDL_CreateSoftwareRenderer(SDL_Surface* surface);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Renderer* SDL_GetRenderer(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Window* SDL_RenderGetWindow(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetRendererInfo(SDL_Renderer* renderer, SDL_RendererInfo* info);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetRendererOutputSize(SDL_Renderer* renderer, int* w, int* h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Texture* SDL_CreateTexture(SDL_Renderer* renderer, [NativeTypeName("Uint32")] uint format, int access, int w, int h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Texture* SDL_CreateTextureFromSurface(SDL_Renderer* renderer, SDL_Surface* surface);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_QueryTexture(SDL_Texture* texture, [NativeTypeName("Uint32 *")] uint* format, int* access, int* w, int* h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetTextureColorMod(SDL_Texture* texture, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetTextureColorMod(SDL_Texture* texture, [NativeTypeName("Uint8 *")] byte* r, [NativeTypeName("Uint8 *")] byte* g, [NativeTypeName("Uint8 *")] byte* b);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetTextureAlphaMod(SDL_Texture* texture, [NativeTypeName("Uint8")] byte alpha);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetTextureAlphaMod(SDL_Texture* texture, [NativeTypeName("Uint8 *")] byte* alpha);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetTextureBlendMode(SDL_Texture* texture, SDL_BlendMode blendMode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetTextureBlendMode(SDL_Texture* texture, SDL_BlendMode* blendMode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetTextureScaleMode(SDL_Texture* texture, SDL_ScaleMode scaleMode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetTextureScaleMode(SDL_Texture* texture, SDL_ScaleMode* scaleMode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetTextureUserData(SDL_Texture* texture, [NativeTypeName("void*")] nint userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_GetTextureUserData(SDL_Texture* texture);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_UpdateTexture(SDL_Texture* texture, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect, [NativeTypeName("const void *")] nint pixels, int pitch);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_UpdateYUVTexture(SDL_Texture* texture, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect, [NativeTypeName("const Uint8 *")] byte* Yplane, int Ypitch, [NativeTypeName("const Uint8 *")] byte* Uplane, int Upitch, [NativeTypeName("const Uint8 *")] byte* Vplane, int Vpitch);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_UpdateNVTexture(SDL_Texture* texture, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect, [NativeTypeName("const Uint8 *")] byte* Yplane, int Ypitch, [NativeTypeName("const Uint8 *")] byte* UVplane, int UVpitch);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_LockTexture(SDL_Texture* texture, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect, [NativeTypeName("void **")] nint* pixels, int* pitch);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_LockTextureToSurface(SDL_Texture* texture, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect, SDL_Surface** surface);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_UnlockTexture(SDL_Texture* texture);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_RenderTargetSupported(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetRenderTarget(SDL_Renderer* renderer, SDL_Texture* texture);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Texture* SDL_GetRenderTarget(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderSetLogicalSize(SDL_Renderer* renderer, int w, int h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_RenderGetLogicalSize(SDL_Renderer* renderer, int* w, int* h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderSetIntegerScale(SDL_Renderer* renderer, SDL_bool enable);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_RenderGetIntegerScale(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderSetViewport(SDL_Renderer* renderer, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_RenderGetViewport(SDL_Renderer* renderer, SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderSetClipRect(SDL_Renderer* renderer, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_RenderGetClipRect(SDL_Renderer* renderer, SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_RenderIsClipEnabled(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderSetScale(SDL_Renderer* renderer, float scaleX, float scaleY);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_RenderGetScale(SDL_Renderer* renderer, float* scaleX, float* scaleY);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_RenderWindowToLogical(SDL_Renderer* renderer, int windowX, int windowY, float* logicalX, float* logicalY);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_RenderLogicalToWindow(SDL_Renderer* renderer, float logicalX, float logicalY, int* windowX, int* windowY);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetRenderDrawColor(SDL_Renderer* renderer, [NativeTypeName("Uint8")] byte r, [NativeTypeName("Uint8")] byte g, [NativeTypeName("Uint8")] byte b, [NativeTypeName("Uint8")] byte a);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetRenderDrawColor(SDL_Renderer* renderer, [NativeTypeName("Uint8 *")] byte* r, [NativeTypeName("Uint8 *")] byte* g, [NativeTypeName("Uint8 *")] byte* b, [NativeTypeName("Uint8 *")] byte* a);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetRenderDrawBlendMode(SDL_Renderer* renderer, SDL_BlendMode blendMode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetRenderDrawBlendMode(SDL_Renderer* renderer, SDL_BlendMode* blendMode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderClear(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawPoint(SDL_Renderer* renderer, int x, int y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawPoints(SDL_Renderer* renderer, [NativeTypeName("const SDL_Point *")] SDL_Point* points, int count);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawLine(SDL_Renderer* renderer, int x1, int y1, int x2, int y2);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawLines(SDL_Renderer* renderer, [NativeTypeName("const SDL_Point *")] SDL_Point* points, int count);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawRect(SDL_Renderer* renderer, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawRects(SDL_Renderer* renderer, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rects, int count);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderFillRect(SDL_Renderer* renderer, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderFillRects(SDL_Renderer* renderer, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rects, int count);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderCopy(SDL_Renderer* renderer, SDL_Texture* texture, [NativeTypeName("const SDL_Rect *")] SDL_Rect* srcrect, [NativeTypeName("const SDL_Rect *")] SDL_Rect* dstrect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderCopyEx(SDL_Renderer* renderer, SDL_Texture* texture, [NativeTypeName("const SDL_Rect *")] SDL_Rect* srcrect, [NativeTypeName("const SDL_Rect *")] SDL_Rect* dstrect, [NativeTypeName("const double")] double angle, [NativeTypeName("const SDL_Point *")] SDL_Point* center, [NativeTypeName("const SDL_RendererFlip")] SDL_RendererFlip flip);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawPointF(SDL_Renderer* renderer, float x, float y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawPointsF(SDL_Renderer* renderer, [NativeTypeName("const SDL_FPoint *")] SDL_FPoint* points, int count);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawLineF(SDL_Renderer* renderer, float x1, float y1, float x2, float y2);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawLinesF(SDL_Renderer* renderer, [NativeTypeName("const SDL_FPoint *")] SDL_FPoint* points, int count);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawRectF(SDL_Renderer* renderer, [NativeTypeName("const SDL_FRect *")] SDL_FRect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderDrawRectsF(SDL_Renderer* renderer, [NativeTypeName("const SDL_FRect *")] SDL_FRect* rects, int count);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderFillRectF(SDL_Renderer* renderer, [NativeTypeName("const SDL_FRect *")] SDL_FRect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderFillRectsF(SDL_Renderer* renderer, [NativeTypeName("const SDL_FRect *")] SDL_FRect* rects, int count);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderCopyF(SDL_Renderer* renderer, SDL_Texture* texture, [NativeTypeName("const SDL_Rect *")] SDL_Rect* srcrect, [NativeTypeName("const SDL_FRect *")] SDL_FRect* dstrect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderCopyExF(SDL_Renderer* renderer, SDL_Texture* texture, [NativeTypeName("const SDL_Rect *")] SDL_Rect* srcrect, [NativeTypeName("const SDL_FRect *")] SDL_FRect* dstrect, [NativeTypeName("const double")] double angle, [NativeTypeName("const SDL_FPoint *")] SDL_FPoint* center, [NativeTypeName("const SDL_RendererFlip")] SDL_RendererFlip flip);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderGeometry(SDL_Renderer* renderer, SDL_Texture* texture, [NativeTypeName("const SDL_Vertex *")] SDL_Vertex* vertices, int num_vertices, [NativeTypeName("const int *")] int* indices, int num_indices);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderGeometryRaw(SDL_Renderer* renderer, SDL_Texture* texture, [NativeTypeName("const float *")] float* xy, int xy_stride, [NativeTypeName("const SDL_Color *")] SDL_Color* color, int color_stride, [NativeTypeName("const float *")] float* uv, int uv_stride, int num_vertices, [NativeTypeName("const void *")] nint indices, int num_indices, int size_indices);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderReadPixels(SDL_Renderer* renderer, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect, [NativeTypeName("Uint32")] uint format, [NativeTypeName("void*")] nint pixels, int pitch);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_RenderPresent(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DestroyTexture(SDL_Texture* texture);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DestroyRenderer(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderFlush(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GL_BindTexture(SDL_Texture* texture, float* texw, float* texh);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GL_UnbindTexture(SDL_Texture* texture);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_RenderGetMetalLayer(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_RenderGetMetalCommandEncoder(SDL_Renderer* renderer);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RenderSetVSync(SDL_Renderer* renderer, int vsync);
    }
}
