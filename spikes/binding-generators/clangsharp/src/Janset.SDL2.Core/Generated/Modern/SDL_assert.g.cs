using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_AssertState SDL_ReportAssertion(SDL_AssertData* param0, [NativeTypeName("const char *")] byte* param1, [NativeTypeName("const char *")] byte* param2, int param3);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetAssertionHandler([NativeTypeName("SDL_AssertionHandler")] delegate* unmanaged[Cdecl]<SDL_AssertData*, nint, SDL_AssertState> handler, [NativeTypeName("void*")] nint userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_AssertionHandler")]
        public static partial delegate* unmanaged[Cdecl]<SDL_AssertData*, nint, SDL_AssertState> SDL_GetDefaultAssertionHandler();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_AssertionHandler")]
        public static partial delegate* unmanaged[Cdecl]<SDL_AssertData*, nint, SDL_AssertState> SDL_GetAssertionHandler([NativeTypeName("void **")] nint* puserdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const SDL_AssertData *")]
        public static partial SDL_AssertData* SDL_GetAssertionReport();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_ResetAssertionReport();
    }
}
