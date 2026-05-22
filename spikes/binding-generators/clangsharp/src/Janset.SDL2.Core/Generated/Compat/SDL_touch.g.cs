using System.Runtime.InteropServices;

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
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GetNumTouchDevices();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_TouchID")]
        public static extern long SDL_GetTouchDevice(int index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetTouchName(int index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_TouchDeviceType SDL_GetTouchDeviceType([NativeTypeName("SDL_TouchID")] long touchID);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GetNumTouchFingers([NativeTypeName("SDL_TouchID")] long touchID);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Finger* SDL_GetTouchFinger([NativeTypeName("SDL_TouchID")] long touchID, int index);

        [NativeTypeName("#define SDL_TOUCH_MOUSEID ((Uint32)-1)")]
        public const uint SDL_TOUCH_MOUSEID = unchecked((uint)(-1));

        [NativeTypeName("#define SDL_MOUSE_TOUCHID ((Sint64)-1)")]
        public const long SDL_MOUSE_TOUCHID = ((long)(-1));
    }
}
