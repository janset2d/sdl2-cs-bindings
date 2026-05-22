using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct _SDL_Joystick
    {
    }

    public enum SDL_JoystickType
    {
        SDL_JOYSTICK_TYPE_UNKNOWN,
        SDL_JOYSTICK_TYPE_GAMECONTROLLER,
        SDL_JOYSTICK_TYPE_WHEEL,
        SDL_JOYSTICK_TYPE_ARCADE_STICK,
        SDL_JOYSTICK_TYPE_FLIGHT_STICK,
        SDL_JOYSTICK_TYPE_DANCE_PAD,
        SDL_JOYSTICK_TYPE_GUITAR,
        SDL_JOYSTICK_TYPE_DRUM_KIT,
        SDL_JOYSTICK_TYPE_ARCADE_PAD,
        SDL_JOYSTICK_TYPE_THROTTLE,
    }

    public enum SDL_JoystickPowerLevel
    {
        SDL_JOYSTICK_POWER_UNKNOWN = -1,
        SDL_JOYSTICK_POWER_EMPTY,
        SDL_JOYSTICK_POWER_LOW,
        SDL_JOYSTICK_POWER_MEDIUM,
        SDL_JOYSTICK_POWER_FULL,
        SDL_JOYSTICK_POWER_WIRED,
        SDL_JOYSTICK_POWER_MAX,
    }

    public unsafe partial struct SDL_VirtualJoystickDesc
    {
        [NativeTypeName("Uint16")]
        public ushort version;

        [NativeTypeName("Uint16")]
        public ushort type;

        [NativeTypeName("Uint16")]
        public ushort naxes;

        [NativeTypeName("Uint16")]
        public ushort nbuttons;

        [NativeTypeName("Uint16")]
        public ushort nhats;

        [NativeTypeName("Uint16")]
        public ushort vendor_id;

        [NativeTypeName("Uint16")]
        public ushort product_id;

        [NativeTypeName("Uint16")]
        public ushort padding;

        [NativeTypeName("Uint32")]
        public uint button_mask;

        [NativeTypeName("Uint32")]
        public uint axis_mask;

        [NativeTypeName("const char *")]
        public byte* name;

        [NativeTypeName("void*")]
        public nint userdata;

        [NativeTypeName("void (*)(void *) __attribute__((cdecl))")]
        public IntPtr Update;

        [NativeTypeName("void (*)(void *, int) __attribute__((cdecl))")]
        public IntPtr SetPlayerIndex;

        [NativeTypeName("int (*)(void *, Uint16, Uint16) __attribute__((cdecl))")]
        public IntPtr Rumble;

        [NativeTypeName("int (*)(void *, Uint16, Uint16) __attribute__((cdecl))")]
        public IntPtr RumbleTriggers;

        [NativeTypeName("int (*)(void *, Uint8, Uint8, Uint8) __attribute__((cdecl))")]
        public IntPtr SetLED;

        [NativeTypeName("int (*)(void *, const void *, int) __attribute__((cdecl))")]
        public IntPtr SendEffect;
    }

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LockJoysticks();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_UnlockJoysticks();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_NumJoysticks();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_JoystickNameForIndex(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_JoystickPathForIndex(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickGetDevicePlayerIndex(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_JoystickGUID")]
        public static extern SDL_GUID SDL_JoystickGetDeviceGUID(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_JoystickGetDeviceVendor(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_JoystickGetDeviceProduct(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_JoystickGetDeviceProductVersion(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_JoystickType SDL_JoystickGetDeviceType(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_JoystickID")]
        public static extern int SDL_JoystickGetDeviceInstanceID(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Joystick *")]
        public static extern _SDL_Joystick* SDL_JoystickOpen(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Joystick *")]
        public static extern _SDL_Joystick* SDL_JoystickFromInstanceID([NativeTypeName("SDL_JoystickID")] int instance_id);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Joystick *")]
        public static extern _SDL_Joystick* SDL_JoystickFromPlayerIndex(int player_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickAttachVirtual(SDL_JoystickType type, int naxes, int nbuttons, int nhats);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickAttachVirtualEx([NativeTypeName("const SDL_VirtualJoystickDesc *")] SDL_VirtualJoystickDesc* desc);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickDetachVirtual(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_JoystickIsVirtual(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickSetVirtualAxis([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, int axis, [NativeTypeName("Sint16")] short value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickSetVirtualButton([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, int button, [NativeTypeName("Uint8")] byte value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickSetVirtualHat([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, int hat, [NativeTypeName("Uint8")] byte value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_JoystickName([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_JoystickPath([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickGetPlayerIndex([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_JoystickSetPlayerIndex([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, int player_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_JoystickGUID")]
        public static extern SDL_GUID SDL_JoystickGetGUID([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_JoystickGetVendor([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_JoystickGetProduct([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_JoystickGetProductVersion([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_JoystickGetFirmwareVersion([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_JoystickGetSerial([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_JoystickType SDL_JoystickGetType([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_JoystickGetGUIDString([NativeTypeName("SDL_JoystickGUID")] SDL_GUID guid, [NativeTypeName("char *")] byte* pszGUID, int cbGUID);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_JoystickGUID")]
        public static extern SDL_GUID SDL_JoystickGetGUIDFromString([NativeTypeName("const char *")] byte* pchGUID);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_GetJoystickGUIDInfo([NativeTypeName("SDL_JoystickGUID")] SDL_GUID guid, [NativeTypeName("Uint16 *")] ushort* vendor, [NativeTypeName("Uint16 *")] ushort* product, [NativeTypeName("Uint16 *")] ushort* version, [NativeTypeName("Uint16 *")] ushort* crc16);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_JoystickGetAttached([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_JoystickID")]
        public static extern int SDL_JoystickInstanceID([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickNumAxes([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickNumBalls([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickNumHats([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickNumButtons([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_JoystickUpdate();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickEventState(int state);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Sint16")]
        public static extern short SDL_JoystickGetAxis([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, int axis);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_JoystickGetAxisInitialState([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, int axis, [NativeTypeName("Sint16 *")] short* state);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint8")]
        public static extern byte SDL_JoystickGetHat([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, int hat);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickGetBall([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, int ball, int* dx, int* dy);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint8")]
        public static extern byte SDL_JoystickGetButton([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, int button);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickRumble([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, [NativeTypeName("Uint16")] ushort low_frequency_rumble, [NativeTypeName("Uint16")] ushort high_frequency_rumble, [NativeTypeName("Uint32")] uint duration_ms);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickRumbleTriggers([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, [NativeTypeName("Uint16")] ushort left_rumble, [NativeTypeName("Uint16")] ushort right_rumble, [NativeTypeName("Uint32")] uint duration_ms);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_JoystickHasLED([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_JoystickHasRumble([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_JoystickHasRumbleTriggers([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickSetLED([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, [NativeTypeName("Uint8")] byte red, [NativeTypeName("Uint8")] byte green, [NativeTypeName("Uint8")] byte blue);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickSendEffect([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick, [NativeTypeName("const void *")] nint data, int size);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_JoystickClose([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_JoystickPowerLevel SDL_JoystickCurrentPowerLevel([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [NativeTypeName("#define SDL_IPHONE_MAX_GFORCE 5.0")]
        public const double SDL_IPHONE_MAX_GFORCE = 5.0;

        [NativeTypeName("#define SDL_VIRTUAL_JOYSTICK_DESC_VERSION 1")]
        public const int SDL_VIRTUAL_JOYSTICK_DESC_VERSION = 1;

        [NativeTypeName("#define SDL_JOYSTICK_AXIS_MAX 32767")]
        public const int SDL_JOYSTICK_AXIS_MAX = 32767;

        [NativeTypeName("#define SDL_JOYSTICK_AXIS_MIN -32768")]
        public const int SDL_JOYSTICK_AXIS_MIN = -32768;

        [NativeTypeName("#define SDL_HAT_CENTERED 0x00")]
        public const int SDL_HAT_CENTERED = 0x00;

        [NativeTypeName("#define SDL_HAT_UP 0x01")]
        public const int SDL_HAT_UP = 0x01;

        [NativeTypeName("#define SDL_HAT_RIGHT 0x02")]
        public const int SDL_HAT_RIGHT = 0x02;

        [NativeTypeName("#define SDL_HAT_DOWN 0x04")]
        public const int SDL_HAT_DOWN = 0x04;

        [NativeTypeName("#define SDL_HAT_LEFT 0x08")]
        public const int SDL_HAT_LEFT = 0x08;

        [NativeTypeName("#define SDL_HAT_RIGHTUP (SDL_HAT_RIGHT|SDL_HAT_UP)")]
        public const int SDL_HAT_RIGHTUP = (0x02 | 0x01);

        [NativeTypeName("#define SDL_HAT_RIGHTDOWN (SDL_HAT_RIGHT|SDL_HAT_DOWN)")]
        public const int SDL_HAT_RIGHTDOWN = (0x02 | 0x04);

        [NativeTypeName("#define SDL_HAT_LEFTUP (SDL_HAT_LEFT|SDL_HAT_UP)")]
        public const int SDL_HAT_LEFTUP = (0x08 | 0x01);

        [NativeTypeName("#define SDL_HAT_LEFTDOWN (SDL_HAT_LEFT|SDL_HAT_DOWN)")]
        public const int SDL_HAT_LEFTDOWN = (0x08 | 0x04);
    }
}
