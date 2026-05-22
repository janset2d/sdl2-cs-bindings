using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public partial struct SDL_Cursor
    {
    }

    public enum SDL_SystemCursor
    {
        SDL_SYSTEM_CURSOR_ARROW,
        SDL_SYSTEM_CURSOR_IBEAM,
        SDL_SYSTEM_CURSOR_WAIT,
        SDL_SYSTEM_CURSOR_CROSSHAIR,
        SDL_SYSTEM_CURSOR_WAITARROW,
        SDL_SYSTEM_CURSOR_SIZENWSE,
        SDL_SYSTEM_CURSOR_SIZENESW,
        SDL_SYSTEM_CURSOR_SIZEWE,
        SDL_SYSTEM_CURSOR_SIZENS,
        SDL_SYSTEM_CURSOR_SIZEALL,
        SDL_SYSTEM_CURSOR_NO,
        SDL_SYSTEM_CURSOR_HAND,
        SDL_NUM_SYSTEM_CURSORS,
    }

    public enum SDL_MouseWheelDirection
    {
        SDL_MOUSEWHEEL_NORMAL,
        SDL_MOUSEWHEEL_FLIPPED,
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Window* SDL_GetMouseFocus();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_GetMouseState(int* x, int* y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_GetGlobalMouseState(int* x, int* y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_GetRelativeMouseState(int* x, int* y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_WarpMouseInWindow(SDL_Window* window, int x, int y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_WarpMouseGlobal(int x, int y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetRelativeMouseMode(SDL_bool enabled);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_CaptureMouse(SDL_bool enabled);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GetRelativeMouseMode();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Cursor* SDL_CreateCursor([NativeTypeName("const Uint8 *")] byte* data, [NativeTypeName("const Uint8 *")] byte* mask, int w, int h, int hot_x, int hot_y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Cursor* SDL_CreateColorCursor(SDL_Surface* surface, int hot_x, int hot_y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Cursor* SDL_CreateSystemCursor(SDL_SystemCursor id);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetCursor(SDL_Cursor* cursor);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Cursor* SDL_GetCursor();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Cursor* SDL_GetDefaultCursor();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_FreeCursor(SDL_Cursor* cursor);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_ShowCursor(int toggle);

        [NativeTypeName("#define SDL_BUTTON_LEFT 1")]
        public const int SDL_BUTTON_LEFT = 1;

        [NativeTypeName("#define SDL_BUTTON_MIDDLE 2")]
        public const int SDL_BUTTON_MIDDLE = 2;

        [NativeTypeName("#define SDL_BUTTON_RIGHT 3")]
        public const int SDL_BUTTON_RIGHT = 3;

        [NativeTypeName("#define SDL_BUTTON_X1 4")]
        public const int SDL_BUTTON_X1 = 4;

        [NativeTypeName("#define SDL_BUTTON_X2 5")]
        public const int SDL_BUTTON_X2 = 5;

        [NativeTypeName("#define SDL_BUTTON_LMASK SDL_BUTTON(SDL_BUTTON_LEFT)")]
        public const int SDL_BUTTON_LMASK = (1 << ((1) - 1));

        [NativeTypeName("#define SDL_BUTTON_MMASK SDL_BUTTON(SDL_BUTTON_MIDDLE)")]
        public const int SDL_BUTTON_MMASK = (1 << ((2) - 1));

        [NativeTypeName("#define SDL_BUTTON_RMASK SDL_BUTTON(SDL_BUTTON_RIGHT)")]
        public const int SDL_BUTTON_RMASK = (1 << ((3) - 1));

        [NativeTypeName("#define SDL_BUTTON_X1MASK SDL_BUTTON(SDL_BUTTON_X1)")]
        public const int SDL_BUTTON_X1MASK = (1 << ((4) - 1));

        [NativeTypeName("#define SDL_BUTTON_X2MASK SDL_BUTTON(SDL_BUTTON_X2)")]
        public const int SDL_BUTTON_X2MASK = (1 << ((5) - 1));
    }
}
