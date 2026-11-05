using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_LogCategory
    {
        SDL_LOG_CATEGORY_APPLICATION,
        SDL_LOG_CATEGORY_ERROR,
        SDL_LOG_CATEGORY_ASSERT,
        SDL_LOG_CATEGORY_SYSTEM,
        SDL_LOG_CATEGORY_AUDIO,
        SDL_LOG_CATEGORY_VIDEO,
        SDL_LOG_CATEGORY_RENDER,
        SDL_LOG_CATEGORY_INPUT,
        SDL_LOG_CATEGORY_TEST,
        SDL_LOG_CATEGORY_RESERVED1,
        SDL_LOG_CATEGORY_RESERVED2,
        SDL_LOG_CATEGORY_RESERVED3,
        SDL_LOG_CATEGORY_RESERVED4,
        SDL_LOG_CATEGORY_RESERVED5,
        SDL_LOG_CATEGORY_RESERVED6,
        SDL_LOG_CATEGORY_RESERVED7,
        SDL_LOG_CATEGORY_RESERVED8,
        SDL_LOG_CATEGORY_RESERVED9,
        SDL_LOG_CATEGORY_RESERVED10,
        SDL_LOG_CATEGORY_CUSTOM,
    }

    public enum SDL_LogPriority
    {
        SDL_LOG_PRIORITY_VERBOSE = 1,
        SDL_LOG_PRIORITY_DEBUG,
        SDL_LOG_PRIORITY_INFO,
        SDL_LOG_PRIORITY_WARN,
        SDL_LOG_PRIORITY_ERROR,
        SDL_LOG_PRIORITY_CRITICAL,
        SDL_NUM_LOG_PRIORITIES,
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void SDL_LogOutputFunction([NativeTypeName("void*")] nint userdata, int category, SDL_LogPriority priority, [NativeTypeName("const char *")] byte* message);

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogSetAllPriority(SDL_LogPriority priority);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogSetPriority(int category, SDL_LogPriority priority);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_LogPriority SDL_LogGetPriority(int category);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogResetPriorities();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_Log([NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogVerbose(int category, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogDebug(int category, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogInfo(int category, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogWarn(int category, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogError(int category, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogCritical(int category, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogMessage(int category, SDL_LogPriority priority, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogGetOutputFunction([NativeTypeName("SDL_LogOutputFunction *")] IntPtr* callback, [NativeTypeName("void **")] nint* userdata);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LogSetOutputFunction([NativeTypeName("SDL_LogOutputFunction")] IntPtr callback, [NativeTypeName("void*")] nint userdata);

        [NativeTypeName("#define SDL_MAX_LOG_MESSAGE 4096")]
        public const int SDL_MAX_LOG_MESSAGE = 4096;
    }
}
