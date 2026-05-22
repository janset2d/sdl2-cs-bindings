using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public enum WindowShapeMode
    {
        ShapeModeDefault,
        ShapeModeBinarizeAlpha,
        ShapeModeReverseBinarizeAlpha,
        ShapeModeColorKey,
    }

    [StructLayout(LayoutKind.Explicit)]
    public partial struct SDL_WindowShapeParams
    {
        [FieldOffset(0)]
        [NativeTypeName("Uint8")]
        public byte binarizationCutoff;

        [FieldOffset(0)]
        public SDL_Color colorKey;
    }

    public partial struct SDL_WindowShapeMode
    {
        public WindowShapeMode mode;

        public SDL_WindowShapeParams parameters;
    }

    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Window* SDL_CreateShapedWindow([NativeTypeName("const char *")] byte* title, [NativeTypeName("unsigned int")] uint x, [NativeTypeName("unsigned int")] uint y, [NativeTypeName("unsigned int")] uint w, [NativeTypeName("unsigned int")] uint h, [NativeTypeName("Uint32")] uint flags);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IsShapedWindow([NativeTypeName("const SDL_Window *")] SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowShape(SDL_Window* window, SDL_Surface* shape, SDL_WindowShapeMode* shape_mode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetShapedWindowMode(SDL_Window* window, SDL_WindowShapeMode* shape_mode);

        [NativeTypeName("#define SDL_NONSHAPEABLE_WINDOW -1")]
        public const int SDL_NONSHAPEABLE_WINDOW = -1;

        [NativeTypeName("#define SDL_INVALID_SHAPE_ARGUMENT -2")]
        public const int SDL_INVALID_SHAPE_ARGUMENT = -2;

        [NativeTypeName("#define SDL_WINDOW_LACKS_SHAPE -3")]
        public const int SDL_WINDOW_LACKS_SHAPE = -3;
    }
}
