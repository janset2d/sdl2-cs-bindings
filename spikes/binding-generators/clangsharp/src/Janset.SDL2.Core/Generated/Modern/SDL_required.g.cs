using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [NativeTypeName("#define SDL_INIT_TIMER 0x00000001U")]
        public const uint SDL_INIT_TIMER = 0x00000001U;

        [NativeTypeName("#define SDL_INIT_AUDIO 0x00000010U")]
        public const uint SDL_INIT_AUDIO = 0x00000010U;

        [NativeTypeName("#define SDL_INIT_VIDEO 0x00000020U")]
        public const uint SDL_INIT_VIDEO = 0x00000020U;

        [NativeTypeName("#define SDL_INIT_JOYSTICK 0x00000200U")]
        public const uint SDL_INIT_JOYSTICK = 0x00000200U;

        [NativeTypeName("#define SDL_INIT_HAPTIC 0x00001000U")]
        public const uint SDL_INIT_HAPTIC = 0x00001000U;

        [NativeTypeName("#define SDL_INIT_GAMECONTROLLER 0x00002000U")]
        public const uint SDL_INIT_GAMECONTROLLER = 0x00002000U;

        [NativeTypeName("#define SDL_INIT_EVENTS 0x00004000U")]
        public const uint SDL_INIT_EVENTS = 0x00004000U;

        [NativeTypeName("#define SDL_INIT_SENSOR 0x00008000U")]
        public const uint SDL_INIT_SENSOR = 0x00008000U;

        [NativeTypeName("#define SDL_INIT_NOPARACHUTE 0x00100000U")]
        public const uint SDL_INIT_NOPARACHUTE = 0x00100000U;

        [NativeTypeName("#define SDL_INIT_EVERYTHING SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO | SDL_INIT_EVENTS | SDL_INIT_JOYSTICK | SDL_INIT_HAPTIC | SDL_INIT_GAMECONTROLLER | SDL_INIT_SENSOR")]
        public const uint SDL_INIT_EVERYTHING = SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO | SDL_INIT_EVENTS | SDL_INIT_JOYSTICK | SDL_INIT_HAPTIC | SDL_INIT_GAMECONTROLLER | SDL_INIT_SENSOR;

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_Init([NativeTypeName("Uint32")] uint flags);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_InitSubSystem([NativeTypeName("Uint32")] uint flags);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_QuitSubSystem([NativeTypeName("Uint32")] uint flags);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_WasInit([NativeTypeName("Uint32")] uint flags);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_Quit();
    }
}
