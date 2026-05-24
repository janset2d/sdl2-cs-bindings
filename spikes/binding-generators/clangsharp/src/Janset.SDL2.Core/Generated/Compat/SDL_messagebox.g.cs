using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_MessageBoxFlags
    {
        SDL_MESSAGEBOX_ERROR = 0x00000010,
        SDL_MESSAGEBOX_WARNING = 0x00000020,
        SDL_MESSAGEBOX_INFORMATION = 0x00000040,
        SDL_MESSAGEBOX_BUTTONS_LEFT_TO_RIGHT = 0x00000080,
        SDL_MESSAGEBOX_BUTTONS_RIGHT_TO_LEFT = 0x00000100,
    }

    public enum SDL_MessageBoxButtonFlags
    {
        SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT = 0x00000001,
        SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT = 0x00000002,
    }

    public unsafe partial struct SDL_MessageBoxButtonData
    {
        [NativeTypeName("Uint32")]
        public uint flags;

        public int buttonid;

        [NativeTypeName("const char *")]
        public byte* text;
    }

    public partial struct SDL_MessageBoxColor
    {
        [NativeTypeName("Uint8")]
        public byte r;

        [NativeTypeName("Uint8")]
        public byte g;

        [NativeTypeName("Uint8")]
        public byte b;
    }

    public enum SDL_MessageBoxColorType
    {
        SDL_MESSAGEBOX_COLOR_BACKGROUND,
        SDL_MESSAGEBOX_COLOR_TEXT,
        SDL_MESSAGEBOX_COLOR_BUTTON_BORDER,
        SDL_MESSAGEBOX_COLOR_BUTTON_BACKGROUND,
        SDL_MESSAGEBOX_COLOR_BUTTON_SELECTED,
        SDL_MESSAGEBOX_COLOR_MAX,
    }

    public partial struct SDL_MessageBoxColorScheme
    {
        [NativeTypeName("SDL_MessageBoxColor[5]")]
        public _colors_e__FixedBuffer colors;

        public partial struct _colors_e__FixedBuffer
        {
            public SDL_MessageBoxColor e0;
            public SDL_MessageBoxColor e1;
            public SDL_MessageBoxColor e2;
            public SDL_MessageBoxColor e3;
            public SDL_MessageBoxColor e4;

            public unsafe ref SDL_MessageBoxColor this[int index]
            {
                get
                {
                    fixed (SDL_MessageBoxColor* pThis = &e0)
                    {
                        return ref pThis[index];
                    }
                }
            }
        }
    }

    public unsafe partial struct SDL_MessageBoxData
    {
        [NativeTypeName("Uint32")]
        public uint flags;

        public SDL_Window* window;

        [NativeTypeName("const char *")]
        public byte* title;

        [NativeTypeName("const char *")]
        public byte* message;

        public int numbuttons;

        [NativeTypeName("const SDL_MessageBoxButtonData *")]
        public SDL_MessageBoxButtonData* buttons;

        [NativeTypeName("const SDL_MessageBoxColorScheme *")]
        public SDL_MessageBoxColorScheme* colorScheme;
    }

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_ShowMessageBox([NativeTypeName("const SDL_MessageBoxData *")] SDL_MessageBoxData* messageboxdata, int* buttonid);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_ShowSimpleMessageBox([NativeTypeName("Uint32")] uint flags, [NativeTypeName("const char *")] byte* title, [NativeTypeName("const char *")] byte* message, SDL_Window window);
    }
}
