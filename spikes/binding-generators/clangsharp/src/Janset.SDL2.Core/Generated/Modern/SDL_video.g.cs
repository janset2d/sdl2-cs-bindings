using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public partial struct SDL_DisplayMode
    {
        [NativeTypeName("Uint32")]
        public uint format;

        public int w;

        public int h;

        public int refresh_rate;

        [NativeTypeName("void*")]
        public nint driverdata;
    }

    public partial struct SDL_Window
    {
    }

    public enum SDL_WindowFlags
    {
        SDL_WINDOW_FULLSCREEN = 0x00000001,
        SDL_WINDOW_OPENGL = 0x00000002,
        SDL_WINDOW_SHOWN = 0x00000004,
        SDL_WINDOW_HIDDEN = 0x00000008,
        SDL_WINDOW_BORDERLESS = 0x00000010,
        SDL_WINDOW_RESIZABLE = 0x00000020,
        SDL_WINDOW_MINIMIZED = 0x00000040,
        SDL_WINDOW_MAXIMIZED = 0x00000080,
        SDL_WINDOW_MOUSE_GRABBED = 0x00000100,
        SDL_WINDOW_INPUT_FOCUS = 0x00000200,
        SDL_WINDOW_MOUSE_FOCUS = 0x00000400,
        SDL_WINDOW_FULLSCREEN_DESKTOP = (SDL_WINDOW_FULLSCREEN | 0x00001000),
        SDL_WINDOW_FOREIGN = 0x00000800,
        SDL_WINDOW_ALLOW_HIGHDPI = 0x00002000,
        SDL_WINDOW_MOUSE_CAPTURE = 0x00004000,
        SDL_WINDOW_ALWAYS_ON_TOP = 0x00008000,
        SDL_WINDOW_SKIP_TASKBAR = 0x00010000,
        SDL_WINDOW_UTILITY = 0x00020000,
        SDL_WINDOW_TOOLTIP = 0x00040000,
        SDL_WINDOW_POPUP_MENU = 0x00080000,
        SDL_WINDOW_KEYBOARD_GRABBED = 0x00100000,
        SDL_WINDOW_VULKAN = 0x10000000,
        SDL_WINDOW_METAL = 0x20000000,
        SDL_WINDOW_INPUT_GRABBED = SDL_WINDOW_MOUSE_GRABBED,
    }

    public enum SDL_WindowEventID
    {
        SDL_WINDOWEVENT_NONE,
        SDL_WINDOWEVENT_SHOWN,
        SDL_WINDOWEVENT_HIDDEN,
        SDL_WINDOWEVENT_EXPOSED,
        SDL_WINDOWEVENT_MOVED,
        SDL_WINDOWEVENT_RESIZED,
        SDL_WINDOWEVENT_SIZE_CHANGED,
        SDL_WINDOWEVENT_MINIMIZED,
        SDL_WINDOWEVENT_MAXIMIZED,
        SDL_WINDOWEVENT_RESTORED,
        SDL_WINDOWEVENT_ENTER,
        SDL_WINDOWEVENT_LEAVE,
        SDL_WINDOWEVENT_FOCUS_GAINED,
        SDL_WINDOWEVENT_FOCUS_LOST,
        SDL_WINDOWEVENT_CLOSE,
        SDL_WINDOWEVENT_TAKE_FOCUS,
        SDL_WINDOWEVENT_HIT_TEST,
        SDL_WINDOWEVENT_ICCPROF_CHANGED,
        SDL_WINDOWEVENT_DISPLAY_CHANGED,
    }

    public enum SDL_DisplayEventID
    {
        SDL_DISPLAYEVENT_NONE,
        SDL_DISPLAYEVENT_ORIENTATION,
        SDL_DISPLAYEVENT_CONNECTED,
        SDL_DISPLAYEVENT_DISCONNECTED,
        SDL_DISPLAYEVENT_MOVED,
    }

    public enum SDL_DisplayOrientation
    {
        SDL_ORIENTATION_UNKNOWN,
        SDL_ORIENTATION_LANDSCAPE,
        SDL_ORIENTATION_LANDSCAPE_FLIPPED,
        SDL_ORIENTATION_PORTRAIT,
        SDL_ORIENTATION_PORTRAIT_FLIPPED,
    }

    public enum SDL_FlashOperation
    {
        SDL_FLASH_CANCEL,
        SDL_FLASH_BRIEFLY,
        SDL_FLASH_UNTIL_FOCUSED,
    }

    public enum SDL_GLattr
    {
        SDL_GL_RED_SIZE,
        SDL_GL_GREEN_SIZE,
        SDL_GL_BLUE_SIZE,
        SDL_GL_ALPHA_SIZE,
        SDL_GL_BUFFER_SIZE,
        SDL_GL_DOUBLEBUFFER,
        SDL_GL_DEPTH_SIZE,
        SDL_GL_STENCIL_SIZE,
        SDL_GL_ACCUM_RED_SIZE,
        SDL_GL_ACCUM_GREEN_SIZE,
        SDL_GL_ACCUM_BLUE_SIZE,
        SDL_GL_ACCUM_ALPHA_SIZE,
        SDL_GL_STEREO,
        SDL_GL_MULTISAMPLEBUFFERS,
        SDL_GL_MULTISAMPLESAMPLES,
        SDL_GL_ACCELERATED_VISUAL,
        SDL_GL_RETAINED_BACKING,
        SDL_GL_CONTEXT_MAJOR_VERSION,
        SDL_GL_CONTEXT_MINOR_VERSION,
        SDL_GL_CONTEXT_EGL,
        SDL_GL_CONTEXT_FLAGS,
        SDL_GL_CONTEXT_PROFILE_MASK,
        SDL_GL_SHARE_WITH_CURRENT_CONTEXT,
        SDL_GL_FRAMEBUFFER_SRGB_CAPABLE,
        SDL_GL_CONTEXT_RELEASE_BEHAVIOR,
        SDL_GL_CONTEXT_RESET_NOTIFICATION,
        SDL_GL_CONTEXT_NO_ERROR,
        SDL_GL_FLOATBUFFERS,
    }

    public enum SDL_GLprofile
    {
        SDL_GL_CONTEXT_PROFILE_CORE = 0x0001,
        SDL_GL_CONTEXT_PROFILE_COMPATIBILITY = 0x0002,
        SDL_GL_CONTEXT_PROFILE_ES = 0x0004,
    }

    public enum SDL_GLcontextFlag
    {
        SDL_GL_CONTEXT_DEBUG_FLAG = 0x0001,
        SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG = 0x0002,
        SDL_GL_CONTEXT_ROBUST_ACCESS_FLAG = 0x0004,
        SDL_GL_CONTEXT_RESET_ISOLATION_FLAG = 0x0008,
    }

    public enum SDL_GLcontextReleaseFlag
    {
        SDL_GL_CONTEXT_RELEASE_BEHAVIOR_NONE = 0x0000,
        SDL_GL_CONTEXT_RELEASE_BEHAVIOR_FLUSH = 0x0001,
    }

    public enum SDL_GLContextResetNotification
    {
        SDL_GL_CONTEXT_RESET_NO_NOTIFICATION = 0x0000,
        SDL_GL_CONTEXT_RESET_LOSE_CONTEXT = 0x0001,
    }

    public enum SDL_HitTestResult
    {
        SDL_HITTEST_NORMAL,
        SDL_HITTEST_DRAGGABLE,
        SDL_HITTEST_RESIZE_TOPLEFT,
        SDL_HITTEST_RESIZE_TOP,
        SDL_HITTEST_RESIZE_TOPRIGHT,
        SDL_HITTEST_RESIZE_RIGHT,
        SDL_HITTEST_RESIZE_BOTTOMRIGHT,
        SDL_HITTEST_RESIZE_BOTTOM,
        SDL_HITTEST_RESIZE_BOTTOMLEFT,
        SDL_HITTEST_RESIZE_LEFT,
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetNumVideoDrivers();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetVideoDriver(int index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_VideoInit([NativeTypeName("const char *")] byte* driver_name);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_VideoQuit();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetCurrentVideoDriver();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetNumVideoDisplays();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetDisplayName(int displayIndex);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetDisplayBounds(int displayIndex, SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetDisplayUsableBounds(int displayIndex, SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetDisplayDPI(int displayIndex, float* ddpi, float* hdpi, float* vdpi);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_DisplayOrientation SDL_GetDisplayOrientation(int displayIndex);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetNumDisplayModes(int displayIndex);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetDisplayMode(int displayIndex, int modeIndex, SDL_DisplayMode* mode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetDesktopDisplayMode(int displayIndex, SDL_DisplayMode* mode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetCurrentDisplayMode(int displayIndex, SDL_DisplayMode* mode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_DisplayMode* SDL_GetClosestDisplayMode(int displayIndex, [NativeTypeName("const SDL_DisplayMode *")] SDL_DisplayMode* mode, SDL_DisplayMode* closest);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetPointDisplayIndex([NativeTypeName("const SDL_Point *")] SDL_Point* point);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetRectDisplayIndex([NativeTypeName("const SDL_Rect *")] SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetWindowDisplayIndex(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowDisplayMode(SDL_Window* window, [NativeTypeName("const SDL_DisplayMode *")] SDL_DisplayMode* mode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetWindowDisplayMode(SDL_Window* window, SDL_DisplayMode* mode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_GetWindowICCProfile(SDL_Window* window, [NativeTypeName("size_t *")] nuint* size);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_GetWindowPixelFormat(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Window* SDL_CreateWindow([NativeTypeName("const char *")] byte* title, int x, int y, int w, int h, [NativeTypeName("Uint32")] uint flags);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Window* SDL_CreateWindowFrom([NativeTypeName("const void *")] nint data);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_GetWindowID(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Window* SDL_GetWindowFromID([NativeTypeName("Uint32")] uint id);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_GetWindowFlags(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowTitle(SDL_Window* window, [NativeTypeName("const char *")] byte* title);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetWindowTitle(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowIcon(SDL_Window* window, SDL_Surface* icon);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_SetWindowData(SDL_Window* window, [NativeTypeName("const char *")] byte* name, [NativeTypeName("void*")] nint userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_GetWindowData(SDL_Window* window, [NativeTypeName("const char *")] byte* name);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowPosition(SDL_Window* window, int x, int y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GetWindowPosition(SDL_Window* window, int* x, int* y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowSize(SDL_Window* window, int w, int h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GetWindowSize(SDL_Window* window, int* w, int* h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetWindowBordersSize(SDL_Window* window, int* top, int* left, int* bottom, int* right);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GetWindowSizeInPixels(SDL_Window* window, int* w, int* h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowMinimumSize(SDL_Window* window, int min_w, int min_h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GetWindowMinimumSize(SDL_Window* window, int* w, int* h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowMaximumSize(SDL_Window* window, int max_w, int max_h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GetWindowMaximumSize(SDL_Window* window, int* w, int* h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowBordered(SDL_Window* window, SDL_bool bordered);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowResizable(SDL_Window* window, SDL_bool resizable);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowAlwaysOnTop(SDL_Window* window, SDL_bool on_top);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_ShowWindow(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_HideWindow(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_RaiseWindow(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_MaximizeWindow(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_MinimizeWindow(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_RestoreWindow(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowFullscreen(SDL_Window* window, [NativeTypeName("Uint32")] uint flags);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_HasWindowSurface(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* SDL_GetWindowSurface(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_UpdateWindowSurface(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_UpdateWindowSurfaceRects(SDL_Window* window, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rects, int numrects);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_DestroyWindowSurface(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowGrab(SDL_Window* window, SDL_bool grabbed);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowKeyboardGrab(SDL_Window* window, SDL_bool grabbed);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowMouseGrab(SDL_Window* window, SDL_bool grabbed);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GetWindowGrab(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GetWindowKeyboardGrab(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GetWindowMouseGrab(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Window* SDL_GetGrabbedWindow();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowMouseRect(SDL_Window* window, [NativeTypeName("const SDL_Rect *")] SDL_Rect* rect);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const SDL_Rect *")]
        public static partial SDL_Rect* SDL_GetWindowMouseRect(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowBrightness(SDL_Window* window, float brightness);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_GetWindowBrightness(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowOpacity(SDL_Window* window, float opacity);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetWindowOpacity(SDL_Window* window, float* out_opacity);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowModalFor(SDL_Window* modal_window, SDL_Window* parent_window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowInputFocus(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowGammaRamp(SDL_Window* window, [NativeTypeName("const Uint16 *")] ushort* red, [NativeTypeName("const Uint16 *")] ushort* green, [NativeTypeName("const Uint16 *")] ushort* blue);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetWindowGammaRamp(SDL_Window* window, [NativeTypeName("Uint16 *")] ushort* red, [NativeTypeName("Uint16 *")] ushort* green, [NativeTypeName("Uint16 *")] ushort* blue);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetWindowHitTest(SDL_Window* window, [NativeTypeName("SDL_HitTest")] delegate* unmanaged[Cdecl]<SDL_Window*, SDL_Point*, nint, SDL_HitTestResult> callback, [NativeTypeName("void*")] nint callback_data);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_FlashWindow(SDL_Window* window, SDL_FlashOperation operation);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DestroyWindow(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IsScreenSaverEnabled();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_EnableScreenSaver();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DisableScreenSaver();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GL_LoadLibrary([NativeTypeName("const char *")] byte* path);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_GL_GetProcAddress([NativeTypeName("const char *")] byte* proc);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GL_UnloadLibrary();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GL_ExtensionSupported([NativeTypeName("const char *")] byte* extension);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GL_ResetAttributes();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GL_SetAttribute(SDL_GLattr attr, int value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GL_GetAttribute(SDL_GLattr attr, int* value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_GLContext")]
        public static partial nint SDL_GL_CreateContext(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GL_MakeCurrent(SDL_Window* window, [NativeTypeName("SDL_GLContext")] nint context);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Window* SDL_GL_GetCurrentWindow();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_GLContext")]
        public static partial nint SDL_GL_GetCurrentContext();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GL_GetDrawableSize(SDL_Window* window, int* w, int* h);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GL_SetSwapInterval(int interval);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GL_GetSwapInterval();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GL_SwapWindow(SDL_Window* window);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GL_DeleteContext([NativeTypeName("SDL_GLContext")] nint context);

        [NativeTypeName("#define SDL_WINDOWPOS_UNDEFINED_MASK 0x1FFF0000u")]
        public const uint SDL_WINDOWPOS_UNDEFINED_MASK = 0x1FFF0000U;

        [NativeTypeName("#define SDL_WINDOWPOS_UNDEFINED SDL_WINDOWPOS_UNDEFINED_DISPLAY(0)")]
        public const uint SDL_WINDOWPOS_UNDEFINED = (0x1FFF0000U | (0));

        [NativeTypeName("#define SDL_WINDOWPOS_CENTERED_MASK 0x2FFF0000u")]
        public const uint SDL_WINDOWPOS_CENTERED_MASK = 0x2FFF0000U;

        [NativeTypeName("#define SDL_WINDOWPOS_CENTERED SDL_WINDOWPOS_CENTERED_DISPLAY(0)")]
        public const uint SDL_WINDOWPOS_CENTERED = (0x2FFF0000U | (0));
    }
}
