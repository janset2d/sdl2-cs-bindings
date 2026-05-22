using System.Runtime.InteropServices;
using static SDL2.SDL_bool;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public partial struct SDL_Point
    {
        public int x;

        public int y;
    }

    public partial struct SDL_FPoint
    {
        public float x;

        public float y;
    }

    public partial struct SDL_Rect
    {
        public int x;

        public int y;

        public int w;

        public int h;
    }

    public partial struct SDL_FRect
    {
        public float x;

        public float y;

        public float w;

        public float h;
    }

    internal static unsafe partial class SDLNative
    {
        public static SDL_bool SDL_PointInRect([NativeTypeName("const SDL_Point *")] SDL_Point* p, [NativeTypeName("const SDL_Rect *")] SDL_Rect* r)
        {
            return ((p->x >= r->x) && (p->x < (r->x + r->w)) && (p->y >= r->y) && (p->y < (r->y + r->h))) ? SDL_TRUE : SDL_FALSE;
        }

        public static SDL_bool SDL_RectEmpty([NativeTypeName("const SDL_Rect *")] SDL_Rect* r)
        {
            return ((r == null) || (r->w <= 0) || (r->h <= 0)) ? SDL_TRUE : SDL_FALSE;
        }

        public static SDL_bool SDL_RectEquals([NativeTypeName("const SDL_Rect *")] SDL_Rect* a, [NativeTypeName("const SDL_Rect *")] SDL_Rect* b)
        {
            return ((a) != null && (b) != null && (a->x == b->x) && (a->y == b->y) && (a->w == b->w) && (a->h == b->h)) ? SDL_TRUE : SDL_FALSE;
        }

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_HasIntersection([NativeTypeName("const SDL_Rect *")] SDL_Rect* A, [NativeTypeName("const SDL_Rect *")] SDL_Rect* B);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IntersectRect([NativeTypeName("const SDL_Rect *")] SDL_Rect* A, [NativeTypeName("const SDL_Rect *")] SDL_Rect* B, SDL_Rect* result);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_UnionRect([NativeTypeName("const SDL_Rect *")] SDL_Rect* A, [NativeTypeName("const SDL_Rect *")] SDL_Rect* B, SDL_Rect* result);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_EnclosePoints([NativeTypeName("const SDL_Point *")] SDL_Point* points, int count, [NativeTypeName("const SDL_Rect *")] SDL_Rect* clip, SDL_Rect* result);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IntersectRectAndLine([NativeTypeName("const SDL_Rect *")] SDL_Rect* rect, int* X1, int* Y1, int* X2, int* Y2);

        public static SDL_bool SDL_PointInFRect([NativeTypeName("const SDL_FPoint *")] SDL_FPoint* p, [NativeTypeName("const SDL_FRect *")] SDL_FRect* r)
        {
            return ((p->x >= r->x) && (p->x < (r->x + r->w)) && (p->y >= r->y) && (p->y < (r->y + r->h))) ? SDL_TRUE : SDL_FALSE;
        }

        public static SDL_bool SDL_FRectEmpty([NativeTypeName("const SDL_FRect *")] SDL_FRect* r)
        {
            return ((r == null) || (r->w <= 0.0f) || (r->h <= 0.0f)) ? SDL_TRUE : SDL_FALSE;
        }

        public static SDL_bool SDL_FRectEqualsEpsilon([NativeTypeName("const SDL_FRect *")] SDL_FRect* a, [NativeTypeName("const SDL_FRect *")] SDL_FRect* b, [NativeTypeName("const float")] float epsilon)
        {
            return ((a) != null && (b) != null && ((a == b) || ((SDL_fabsf(a->x - b->x) <= epsilon) && (SDL_fabsf(a->y - b->y) <= epsilon) && (SDL_fabsf(a->w - b->w) <= epsilon) && (SDL_fabsf(a->h - b->h) <= epsilon)))) ? SDL_TRUE : SDL_FALSE;
        }

        public static SDL_bool SDL_FRectEquals([NativeTypeName("const SDL_FRect *")] SDL_FRect* a, [NativeTypeName("const SDL_FRect *")] SDL_FRect* b)
        {
            return SDL_FRectEqualsEpsilon(a, b, 1.1920928955078125e-07F);
        }

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_HasIntersectionF([NativeTypeName("const SDL_FRect *")] SDL_FRect* A, [NativeTypeName("const SDL_FRect *")] SDL_FRect* B);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IntersectFRect([NativeTypeName("const SDL_FRect *")] SDL_FRect* A, [NativeTypeName("const SDL_FRect *")] SDL_FRect* B, SDL_FRect* result);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_UnionFRect([NativeTypeName("const SDL_FRect *")] SDL_FRect* A, [NativeTypeName("const SDL_FRect *")] SDL_FRect* B, SDL_FRect* result);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_EncloseFPoints([NativeTypeName("const SDL_FPoint *")] SDL_FPoint* points, int count, [NativeTypeName("const SDL_FRect *")] SDL_FRect* clip, SDL_FRect* result);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IntersectFRectAndLine([NativeTypeName("const SDL_FRect *")] SDL_FRect* rect, float* X1, float* Y1, float* X2, float* Y2);
    }
}
