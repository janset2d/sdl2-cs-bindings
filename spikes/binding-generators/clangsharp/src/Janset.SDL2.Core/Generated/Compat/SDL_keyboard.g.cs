using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_Keysym
    {
        public SDL_Scancode scancode;

        [NativeTypeName("SDL_Keycode")]
        public int sym;

        [NativeTypeName("Uint16")]
        public ushort mod;

        [NativeTypeName("Uint32")]
        public uint unused;
    }

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Window* SDL_GetKeyboardFocus();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const Uint8 *")]
        public static extern byte* SDL_GetKeyboardState(int* numkeys);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_ResetKeyboard();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Keymod SDL_GetModState();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_SetModState(SDL_Keymod modstate);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Keycode")]
        public static extern int SDL_GetKeyFromScancode(SDL_Scancode scancode);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Scancode SDL_GetScancodeFromKey([NativeTypeName("SDL_Keycode")] int key);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetScancodeName(SDL_Scancode scancode);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Scancode SDL_GetScancodeFromName([NativeTypeName("const char *")] byte* name);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetKeyName([NativeTypeName("SDL_Keycode")] int key);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Keycode")]
        public static extern int SDL_GetKeyFromName([NativeTypeName("const char *")] byte* name);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_StartTextInput();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_IsTextInputActive();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_StopTextInput();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_ClearComposition();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_IsTextInputShown();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_SetTextInputRect([NativeTypeName("const SDL_Rect *")] SDL_Rect* rect);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_HasScreenKeyboardSupport();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_IsScreenKeyboardShown(SDL_Window* window);
    }
}
