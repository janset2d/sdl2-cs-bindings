using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_EventType
    {
        SDL_FIRSTEVENT = 0,
        SDL_QUIT = 0x100,
        SDL_APP_TERMINATING,
        SDL_APP_LOWMEMORY,
        SDL_APP_WILLENTERBACKGROUND,
        SDL_APP_DIDENTERBACKGROUND,
        SDL_APP_WILLENTERFOREGROUND,
        SDL_APP_DIDENTERFOREGROUND,
        SDL_LOCALECHANGED,
        SDL_DISPLAYEVENT = 0x150,
        SDL_WINDOWEVENT = 0x200,
        SDL_SYSWMEVENT,
        SDL_KEYDOWN = 0x300,
        SDL_KEYUP,
        SDL_TEXTEDITING,
        SDL_TEXTINPUT,
        SDL_KEYMAPCHANGED,
        SDL_TEXTEDITING_EXT,
        SDL_MOUSEMOTION = 0x400,
        SDL_MOUSEBUTTONDOWN,
        SDL_MOUSEBUTTONUP,
        SDL_MOUSEWHEEL,
        SDL_JOYAXISMOTION = 0x600,
        SDL_JOYBALLMOTION,
        SDL_JOYHATMOTION,
        SDL_JOYBUTTONDOWN,
        SDL_JOYBUTTONUP,
        SDL_JOYDEVICEADDED,
        SDL_JOYDEVICEREMOVED,
        SDL_JOYBATTERYUPDATED,
        SDL_CONTROLLERAXISMOTION = 0x650,
        SDL_CONTROLLERBUTTONDOWN,
        SDL_CONTROLLERBUTTONUP,
        SDL_CONTROLLERDEVICEADDED,
        SDL_CONTROLLERDEVICEREMOVED,
        SDL_CONTROLLERDEVICEREMAPPED,
        SDL_CONTROLLERTOUCHPADDOWN,
        SDL_CONTROLLERTOUCHPADMOTION,
        SDL_CONTROLLERTOUCHPADUP,
        SDL_CONTROLLERSENSORUPDATE,
        SDL_CONTROLLERUPDATECOMPLETE_RESERVED_FOR_SDL3,
        SDL_CONTROLLERSTEAMHANDLEUPDATED,
        SDL_FINGERDOWN = 0x700,
        SDL_FINGERUP,
        SDL_FINGERMOTION,
        SDL_DOLLARGESTURE = 0x800,
        SDL_DOLLARRECORD,
        SDL_MULTIGESTURE,
        SDL_CLIPBOARDUPDATE = 0x900,
        SDL_DROPFILE = 0x1000,
        SDL_DROPTEXT,
        SDL_DROPBEGIN,
        SDL_DROPCOMPLETE,
        SDL_AUDIODEVICEADDED = 0x1100,
        SDL_AUDIODEVICEREMOVED,
        SDL_SENSORUPDATE = 0x1200,
        SDL_RENDER_TARGETS_RESET = 0x2000,
        SDL_RENDER_DEVICE_RESET,
        SDL_POLLSENTINEL = 0x7F00,
        SDL_USEREVENT = 0x8000,
        SDL_LASTEVENT = 0xFFFF,
    }

    public partial struct SDL_CommonEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;
    }

    public partial struct SDL_DisplayEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint display;

        [NativeTypeName("Uint8")]
        public byte @event;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Uint8")]
        public byte padding2;

        [NativeTypeName("Uint8")]
        public byte padding3;

        [NativeTypeName("Sint32")]
        public int data1;
    }

    public partial struct SDL_WindowEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint windowID;

        [NativeTypeName("Uint8")]
        public byte @event;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Uint8")]
        public byte padding2;

        [NativeTypeName("Uint8")]
        public byte padding3;

        [NativeTypeName("Sint32")]
        public int data1;

        [NativeTypeName("Sint32")]
        public int data2;
    }

    public partial struct SDL_KeyboardEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint windowID;

        [NativeTypeName("Uint8")]
        public byte state;

        [NativeTypeName("Uint8")]
        public byte repeat;

        [NativeTypeName("Uint8")]
        public byte padding2;

        [NativeTypeName("Uint8")]
        public byte padding3;

        public SDL_Keysym keysym;
    }

    public partial struct SDL_TextEditingEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint windowID;

        [NativeTypeName("char[32]")]
        public _text_e__FixedBuffer text;

        [NativeTypeName("Sint32")]
        public int start;

        [NativeTypeName("Sint32")]
        public int length;

        [InlineArray(32)]
        public partial struct _text_e__FixedBuffer
        {
            public byte e0;
        }
    }

    public unsafe partial struct SDL_TextEditingExtEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint windowID;

        [NativeTypeName("char *")]
        public byte* text;

        [NativeTypeName("Sint32")]
        public int start;

        [NativeTypeName("Sint32")]
        public int length;
    }

    public partial struct SDL_TextInputEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint windowID;

        [NativeTypeName("char[32]")]
        public _text_e__FixedBuffer text;

        [InlineArray(32)]
        public partial struct _text_e__FixedBuffer
        {
            public byte e0;
        }
    }

    public partial struct SDL_MouseMotionEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint windowID;

        [NativeTypeName("Uint32")]
        public uint which;

        [NativeTypeName("Uint32")]
        public uint state;

        [NativeTypeName("Sint32")]
        public int x;

        [NativeTypeName("Sint32")]
        public int y;

        [NativeTypeName("Sint32")]
        public int xrel;

        [NativeTypeName("Sint32")]
        public int yrel;
    }

    public partial struct SDL_MouseButtonEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint windowID;

        [NativeTypeName("Uint32")]
        public uint which;

        [NativeTypeName("Uint8")]
        public byte button;

        [NativeTypeName("Uint8")]
        public byte state;

        [NativeTypeName("Uint8")]
        public byte clicks;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Sint32")]
        public int x;

        [NativeTypeName("Sint32")]
        public int y;
    }

    public partial struct SDL_MouseWheelEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint windowID;

        [NativeTypeName("Uint32")]
        public uint which;

        [NativeTypeName("Sint32")]
        public int x;

        [NativeTypeName("Sint32")]
        public int y;

        [NativeTypeName("Uint32")]
        public uint direction;

        public float preciseX;

        public float preciseY;

        [NativeTypeName("Sint32")]
        public int mouseX;

        [NativeTypeName("Sint32")]
        public int mouseY;
    }

    public partial struct SDL_JoyAxisEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_JoystickID")]
        public int which;

        [NativeTypeName("Uint8")]
        public byte axis;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Uint8")]
        public byte padding2;

        [NativeTypeName("Uint8")]
        public byte padding3;

        [NativeTypeName("Sint16")]
        public short value;

        [NativeTypeName("Uint16")]
        public ushort padding4;
    }

    public partial struct SDL_JoyBallEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_JoystickID")]
        public int which;

        [NativeTypeName("Uint8")]
        public byte ball;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Uint8")]
        public byte padding2;

        [NativeTypeName("Uint8")]
        public byte padding3;

        [NativeTypeName("Sint16")]
        public short xrel;

        [NativeTypeName("Sint16")]
        public short yrel;
    }

    public partial struct SDL_JoyHatEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_JoystickID")]
        public int which;

        [NativeTypeName("Uint8")]
        public byte hat;

        [NativeTypeName("Uint8")]
        public byte value;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Uint8")]
        public byte padding2;
    }

    public partial struct SDL_JoyButtonEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_JoystickID")]
        public int which;

        [NativeTypeName("Uint8")]
        public byte button;

        [NativeTypeName("Uint8")]
        public byte state;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Uint8")]
        public byte padding2;
    }

    public partial struct SDL_JoyDeviceEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Sint32")]
        public int which;
    }

    public partial struct SDL_JoyBatteryEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_JoystickID")]
        public int which;

        public SDL_JoystickPowerLevel level;
    }

    public partial struct SDL_ControllerAxisEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_JoystickID")]
        public int which;

        [NativeTypeName("Uint8")]
        public byte axis;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Uint8")]
        public byte padding2;

        [NativeTypeName("Uint8")]
        public byte padding3;

        [NativeTypeName("Sint16")]
        public short value;

        [NativeTypeName("Uint16")]
        public ushort padding4;
    }

    public partial struct SDL_ControllerButtonEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_JoystickID")]
        public int which;

        [NativeTypeName("Uint8")]
        public byte button;

        [NativeTypeName("Uint8")]
        public byte state;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Uint8")]
        public byte padding2;
    }

    public partial struct SDL_ControllerDeviceEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Sint32")]
        public int which;
    }

    public partial struct SDL_ControllerTouchpadEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_JoystickID")]
        public int which;

        [NativeTypeName("Sint32")]
        public int touchpad;

        [NativeTypeName("Sint32")]
        public int finger;

        public float x;

        public float y;

        public float pressure;
    }

    public partial struct SDL_ControllerSensorEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_JoystickID")]
        public int which;

        [NativeTypeName("Sint32")]
        public int sensor;

        [NativeTypeName("float[3]")]
        public _data_e__FixedBuffer data;

        [NativeTypeName("Uint64")]
        public ulong timestamp_us;

        [InlineArray(3)]
        public partial struct _data_e__FixedBuffer
        {
            public float e0;
        }
    }

    public partial struct SDL_AudioDeviceEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint which;

        [NativeTypeName("Uint8")]
        public byte iscapture;

        [NativeTypeName("Uint8")]
        public byte padding1;

        [NativeTypeName("Uint8")]
        public byte padding2;

        [NativeTypeName("Uint8")]
        public byte padding3;
    }

    public partial struct SDL_TouchFingerEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_TouchID")]
        public long touchId;

        [NativeTypeName("SDL_FingerID")]
        public long fingerId;

        public float x;

        public float y;

        public float dx;

        public float dy;

        public float pressure;

        [NativeTypeName("Uint32")]
        public uint windowID;
    }

    public partial struct SDL_MultiGestureEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_TouchID")]
        public long touchId;

        public float dTheta;

        public float dDist;

        public float x;

        public float y;

        [NativeTypeName("Uint16")]
        public ushort numFingers;

        [NativeTypeName("Uint16")]
        public ushort padding;
    }

    public partial struct SDL_DollarGestureEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("SDL_TouchID")]
        public long touchId;

        [NativeTypeName("SDL_GestureID")]
        public long gestureId;

        [NativeTypeName("Uint32")]
        public uint numFingers;

        public float error;

        public float x;

        public float y;
    }

    public unsafe partial struct SDL_DropEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("char *")]
        public byte* file;

        [NativeTypeName("Uint32")]
        public uint windowID;
    }

    public partial struct SDL_SensorEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Sint32")]
        public int which;

        [NativeTypeName("float[6]")]
        public _data_e__FixedBuffer data;

        [NativeTypeName("Uint64")]
        public ulong timestamp_us;

        [InlineArray(6)]
        public partial struct _data_e__FixedBuffer
        {
            public float e0;
        }
    }

    public partial struct SDL_QuitEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;
    }

    public partial struct SDL_UserEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        [NativeTypeName("Uint32")]
        public uint windowID;

        [NativeTypeName("Sint32")]
        public int code;

        [NativeTypeName("void*")]
        public nint data1;

        [NativeTypeName("void*")]
        public nint data2;
    }

    public partial struct SDL_SysWMmsg
    {
    }

    public unsafe partial struct SDL_SysWMEvent
    {
        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("Uint32")]
        public uint timestamp;

        public SDL_SysWMmsg* msg;
    }

    [StructLayout(LayoutKind.Explicit)]
    public partial struct SDL_Event
    {
        [FieldOffset(0)]
        [NativeTypeName("Uint32")]
        public uint type;

        [FieldOffset(0)]
        public SDL_CommonEvent common;

        [FieldOffset(0)]
        public SDL_DisplayEvent display;

        [FieldOffset(0)]
        public SDL_WindowEvent window;

        [FieldOffset(0)]
        public SDL_KeyboardEvent key;

        [FieldOffset(0)]
        public SDL_TextEditingEvent edit;

        [FieldOffset(0)]
        public SDL_TextEditingExtEvent editExt;

        [FieldOffset(0)]
        public SDL_TextInputEvent text;

        [FieldOffset(0)]
        public SDL_MouseMotionEvent motion;

        [FieldOffset(0)]
        public SDL_MouseButtonEvent button;

        [FieldOffset(0)]
        public SDL_MouseWheelEvent wheel;

        [FieldOffset(0)]
        public SDL_JoyAxisEvent jaxis;

        [FieldOffset(0)]
        public SDL_JoyBallEvent jball;

        [FieldOffset(0)]
        public SDL_JoyHatEvent jhat;

        [FieldOffset(0)]
        public SDL_JoyButtonEvent jbutton;

        [FieldOffset(0)]
        public SDL_JoyDeviceEvent jdevice;

        [FieldOffset(0)]
        public SDL_JoyBatteryEvent jbattery;

        [FieldOffset(0)]
        public SDL_ControllerAxisEvent caxis;

        [FieldOffset(0)]
        public SDL_ControllerButtonEvent cbutton;

        [FieldOffset(0)]
        public SDL_ControllerDeviceEvent cdevice;

        [FieldOffset(0)]
        public SDL_ControllerTouchpadEvent ctouchpad;

        [FieldOffset(0)]
        public SDL_ControllerSensorEvent csensor;

        [FieldOffset(0)]
        public SDL_AudioDeviceEvent adevice;

        [FieldOffset(0)]
        public SDL_SensorEvent sensor;

        [FieldOffset(0)]
        public SDL_QuitEvent quit;

        [FieldOffset(0)]
        public SDL_UserEvent user;

        [FieldOffset(0)]
        public SDL_SysWMEvent syswm;

        [FieldOffset(0)]
        public SDL_TouchFingerEvent tfinger;

        [FieldOffset(0)]
        public SDL_MultiGestureEvent mgesture;

        [FieldOffset(0)]
        public SDL_DollarGestureEvent dgesture;

        [FieldOffset(0)]
        public SDL_DropEvent drop;

        [FieldOffset(0)]
        [NativeTypeName("Uint8[56]")]
        public _padding_e__FixedBuffer padding;

        [InlineArray(56)]
        public partial struct _padding_e__FixedBuffer
        {
            public byte e0;
        }
    }

    public enum SDL_eventaction
    {
        SDL_ADDEVENT,
        SDL_PEEKEVENT,
        SDL_GETEVENT,
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_PumpEvents();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_PeepEvents(SDL_Event* events, int numevents, SDL_eventaction action, [NativeTypeName("Uint32")] uint minType, [NativeTypeName("Uint32")] uint maxType);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_HasEvent([NativeTypeName("Uint32")] uint type);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_HasEvents([NativeTypeName("Uint32")] uint minType, [NativeTypeName("Uint32")] uint maxType);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_FlushEvent([NativeTypeName("Uint32")] uint type);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_FlushEvents([NativeTypeName("Uint32")] uint minType, [NativeTypeName("Uint32")] uint maxType);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_PollEvent(SDL_Event* @event);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_WaitEvent(SDL_Event* @event);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_WaitEventTimeout(SDL_Event* @event, int timeout);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_PushEvent(SDL_Event* @event);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetEventFilter([NativeTypeName("SDL_EventFilter")] delegate* unmanaged[Cdecl]<nint, SDL_Event*, int> filter, [NativeTypeName("void*")] nint userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GetEventFilter([NativeTypeName("SDL_EventFilter *")] delegate* unmanaged[Cdecl]<nint, SDL_Event*, int>* filter, [NativeTypeName("void **")] nint* userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_AddEventWatch([NativeTypeName("SDL_EventFilter")] delegate* unmanaged[Cdecl]<nint, SDL_Event*, int> filter, [NativeTypeName("void*")] nint userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DelEventWatch([NativeTypeName("SDL_EventFilter")] delegate* unmanaged[Cdecl]<nint, SDL_Event*, int> filter, [NativeTypeName("void*")] nint userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_FilterEvents([NativeTypeName("SDL_EventFilter")] delegate* unmanaged[Cdecl]<nint, SDL_Event*, int> filter, [NativeTypeName("void*")] nint userdata);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint8")]
        public static partial byte SDL_EventState([NativeTypeName("Uint32")] uint type, int state);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_RegisterEvents(int numevents);

        [NativeTypeName("#define SDL_RELEASED 0")]
        public const int SDL_RELEASED = 0;

        [NativeTypeName("#define SDL_PRESSED 1")]
        public const int SDL_PRESSED = 1;

        [NativeTypeName("#define SDL_TEXTEDITINGEVENT_TEXT_SIZE (32)")]
        public const int SDL_TEXTEDITINGEVENT_TEXT_SIZE = (32);

        [NativeTypeName("#define SDL_TEXTINPUTEVENT_TEXT_SIZE (32)")]
        public const int SDL_TEXTINPUTEVENT_TEXT_SIZE = (32);

        [NativeTypeName("#define SDL_QUERY -1")]
        public const int SDL_QUERY = -1;

        [NativeTypeName("#define SDL_IGNORE 0")]
        public const int SDL_IGNORE = 0;

        [NativeTypeName("#define SDL_DISABLE 0")]
        public const int SDL_DISABLE = 0;

        [NativeTypeName("#define SDL_ENABLE 1")]
        public const int SDL_ENABLE = 1;
    }
}
