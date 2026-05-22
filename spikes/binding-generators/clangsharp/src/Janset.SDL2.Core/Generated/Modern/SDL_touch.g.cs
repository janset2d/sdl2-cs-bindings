using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public enum SDL_TouchDeviceType
    {
        SDL_TOUCH_DEVICE_INVALID = -1,
        SDL_TOUCH_DEVICE_DIRECT,
        SDL_TOUCH_DEVICE_INDIRECT_ABSOLUTE,
        SDL_TOUCH_DEVICE_INDIRECT_RELATIVE,
    }

    public partial struct SDL_Finger
    {
        [NativeTypeName("SDL_FingerID")]
        public long id;

        public float x;

        public float y;

        public float pressure;
    }

    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetNumTouchDevices();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_TouchID")]
        public static partial long SDL_GetTouchDevice(int index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetTouchName(int index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_TouchDeviceType SDL_GetTouchDeviceType([NativeTypeName("SDL_TouchID")] long touchID);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetNumTouchFingers([NativeTypeName("SDL_TouchID")] long touchID);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Finger* SDL_GetTouchFinger([NativeTypeName("SDL_TouchID")] long touchID, int index);

        [NativeTypeName("#define SDL_TOUCH_MOUSEID ((Uint32)-1)")]
        public const uint SDL_TOUCH_MOUSEID = unchecked((uint)(-1));

        [NativeTypeName("#define SDL_MOUSE_TOUCHID ((Sint64)-1)")]
        public const long SDL_MOUSE_TOUCHID = ((long)(-1));
    }
}
