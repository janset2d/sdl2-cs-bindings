using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_AssertState
    {
        SDL_ASSERTION_RETRY,
        SDL_ASSERTION_BREAK,
        SDL_ASSERTION_ABORT,
        SDL_ASSERTION_IGNORE,
        SDL_ASSERTION_ALWAYS_IGNORE,
    }

    public unsafe partial struct SDL_AssertData
    {
        public int always_ignore;

        [NativeTypeName("unsigned int")]
        public uint trigger_count;

        [NativeTypeName("const char *")]
        public byte* condition;

        [NativeTypeName("const char *")]
        public byte* filename;

        public int linenum;

        [NativeTypeName("const char *")]
        public byte* function;

        [NativeTypeName("const struct SDL_AssertData *")]
        public SDL_AssertData* next;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate SDL_AssertState SDL_AssertionHandler([NativeTypeName("const SDL_AssertData *")] SDL_AssertData* data, [NativeTypeName("void*")] nint userdata);

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_AssertState SDL_ReportAssertion(SDL_AssertData* param0, [NativeTypeName("const char *")] byte* param1, [NativeTypeName("const char *")] byte* param2, int param3);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_SetAssertionHandler([NativeTypeName("SDL_AssertionHandler")] IntPtr handler, [NativeTypeName("void*")] nint userdata);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_AssertionHandler")]
        public static extern IntPtr SDL_GetDefaultAssertionHandler();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_AssertionHandler")]
        public static extern IntPtr SDL_GetAssertionHandler([NativeTypeName("void **")] nint* puserdata);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const SDL_AssertData *")]
        public static extern SDL_AssertData* SDL_GetAssertionReport();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_ResetAssertionReport();
    }
}
