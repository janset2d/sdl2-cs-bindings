using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public partial struct _SDL_GameController
    {
    }

    public enum SDL_GameControllerType
    {
        SDL_CONTROLLER_TYPE_UNKNOWN = 0,
        SDL_CONTROLLER_TYPE_XBOX360,
        SDL_CONTROLLER_TYPE_XBOXONE,
        SDL_CONTROLLER_TYPE_PS3,
        SDL_CONTROLLER_TYPE_PS4,
        SDL_CONTROLLER_TYPE_NINTENDO_SWITCH_PRO,
        SDL_CONTROLLER_TYPE_VIRTUAL,
        SDL_CONTROLLER_TYPE_PS5,
        SDL_CONTROLLER_TYPE_AMAZON_LUNA,
        SDL_CONTROLLER_TYPE_GOOGLE_STADIA,
        SDL_CONTROLLER_TYPE_NVIDIA_SHIELD,
        SDL_CONTROLLER_TYPE_NINTENDO_SWITCH_JOYCON_LEFT,
        SDL_CONTROLLER_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT,
        SDL_CONTROLLER_TYPE_NINTENDO_SWITCH_JOYCON_PAIR,
        SDL_CONTROLLER_TYPE_MAX,
    }

    public enum SDL_GameControllerBindType
    {
        SDL_CONTROLLER_BINDTYPE_NONE = 0,
        SDL_CONTROLLER_BINDTYPE_BUTTON,
        SDL_CONTROLLER_BINDTYPE_AXIS,
        SDL_CONTROLLER_BINDTYPE_HAT,
    }

    public partial struct SDL_GameControllerButtonBind
    {
        public SDL_GameControllerBindType bindType;

        [NativeTypeName("__AnonymousRecord_SDL_gamecontroller_L96_C5")]
        public _value_e__Union value;

        [StructLayout(LayoutKind.Explicit)]
        public partial struct _value_e__Union
        {
            [FieldOffset(0)]
            public int button;

            [FieldOffset(0)]
            public int axis;

            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_SDL_gamecontroller_L100_C9")]
            public _hat_e__Struct hat;

            public partial struct _hat_e__Struct
            {
                public int hat;

                public int hat_mask;
            }
        }
    }

    public enum SDL_GameControllerAxis
    {
        SDL_CONTROLLER_AXIS_INVALID = -1,
        SDL_CONTROLLER_AXIS_LEFTX,
        SDL_CONTROLLER_AXIS_LEFTY,
        SDL_CONTROLLER_AXIS_RIGHTX,
        SDL_CONTROLLER_AXIS_RIGHTY,
        SDL_CONTROLLER_AXIS_TRIGGERLEFT,
        SDL_CONTROLLER_AXIS_TRIGGERRIGHT,
        SDL_CONTROLLER_AXIS_MAX,
    }

    public enum SDL_GameControllerButton
    {
        SDL_CONTROLLER_BUTTON_INVALID = -1,
        SDL_CONTROLLER_BUTTON_A,
        SDL_CONTROLLER_BUTTON_B,
        SDL_CONTROLLER_BUTTON_X,
        SDL_CONTROLLER_BUTTON_Y,
        SDL_CONTROLLER_BUTTON_BACK,
        SDL_CONTROLLER_BUTTON_GUIDE,
        SDL_CONTROLLER_BUTTON_START,
        SDL_CONTROLLER_BUTTON_LEFTSTICK,
        SDL_CONTROLLER_BUTTON_RIGHTSTICK,
        SDL_CONTROLLER_BUTTON_LEFTSHOULDER,
        SDL_CONTROLLER_BUTTON_RIGHTSHOULDER,
        SDL_CONTROLLER_BUTTON_DPAD_UP,
        SDL_CONTROLLER_BUTTON_DPAD_DOWN,
        SDL_CONTROLLER_BUTTON_DPAD_LEFT,
        SDL_CONTROLLER_BUTTON_DPAD_RIGHT,
        SDL_CONTROLLER_BUTTON_MISC1,
        SDL_CONTROLLER_BUTTON_PADDLE1,
        SDL_CONTROLLER_BUTTON_PADDLE2,
        SDL_CONTROLLER_BUTTON_PADDLE3,
        SDL_CONTROLLER_BUTTON_PADDLE4,
        SDL_CONTROLLER_BUTTON_TOUCHPAD,
        SDL_CONTROLLER_BUTTON_MAX,
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerAddMappingsFromRW(SDL_RWops* rw, int freerw);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerAddMapping([NativeTypeName("const char *")] byte* mappingString);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerNumMappings();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_GameControllerMappingForIndex(int mapping_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_GameControllerMappingForGUID([NativeTypeName("SDL_JoystickGUID")] SDL_GUID guid);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_GameControllerMapping([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_IsGameController(int joystick_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GameControllerNameForIndex(int joystick_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GameControllerPathForIndex(int joystick_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_GameControllerType SDL_GameControllerTypeForIndex(int joystick_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_GameControllerMappingForDeviceIndex(int joystick_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_GameController *")]
        public static partial _SDL_GameController* SDL_GameControllerOpen(int joystick_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_GameController *")]
        public static partial _SDL_GameController* SDL_GameControllerFromInstanceID([NativeTypeName("SDL_JoystickID")] int joyid);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_GameController *")]
        public static partial _SDL_GameController* SDL_GameControllerFromPlayerIndex(int player_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GameControllerName([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GameControllerPath([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_GameControllerType SDL_GameControllerGetType([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerGetPlayerIndex([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GameControllerSetPlayerIndex([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, int player_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint16")]
        public static partial ushort SDL_GameControllerGetVendor([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint16")]
        public static partial ushort SDL_GameControllerGetProduct([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint16")]
        public static partial ushort SDL_GameControllerGetProductVersion([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint16")]
        public static partial ushort SDL_GameControllerGetFirmwareVersion([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GameControllerGetSerial([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint64")]
        public static partial ulong SDL_GameControllerGetSteamHandle([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GameControllerGetAttached([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_Joystick *")]
        public static partial _SDL_Joystick* SDL_GameControllerGetJoystick([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerEventState(int state);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GameControllerUpdate();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_GameControllerAxis SDL_GameControllerGetAxisFromString([NativeTypeName("const char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GameControllerGetStringForAxis(SDL_GameControllerAxis axis);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_GameControllerButtonBind SDL_GameControllerGetBindForAxis([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_GameControllerAxis axis);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GameControllerHasAxis([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_GameControllerAxis axis);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Sint16")]
        public static partial short SDL_GameControllerGetAxis([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_GameControllerAxis axis);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_GameControllerButton SDL_GameControllerGetButtonFromString([NativeTypeName("const char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GameControllerGetStringForButton(SDL_GameControllerButton button);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_GameControllerButtonBind SDL_GameControllerGetBindForButton([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_GameControllerButton button);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GameControllerHasButton([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_GameControllerButton button);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint8")]
        public static partial byte SDL_GameControllerGetButton([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_GameControllerButton button);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerGetNumTouchpads([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerGetNumTouchpadFingers([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, int touchpad);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerGetTouchpadFinger([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, int touchpad, int finger, [NativeTypeName("Uint8 *")] byte* state, float* x, float* y, float* pressure);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GameControllerHasSensor([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_SensorType type);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerSetSensorEnabled([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_SensorType type, SDL_bool enabled);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GameControllerIsSensorEnabled([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_SensorType type);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_GameControllerGetSensorDataRate([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_SensorType type);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerGetSensorData([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_SensorType type, float* data, int num_values);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerGetSensorDataWithTimestamp([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_SensorType type, [NativeTypeName("Uint64 *")] ulong* timestamp, float* data, int num_values);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerRumble([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, [NativeTypeName("Uint16")] ushort low_frequency_rumble, [NativeTypeName("Uint16")] ushort high_frequency_rumble, [NativeTypeName("Uint32")] uint duration_ms);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerRumbleTriggers([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, [NativeTypeName("Uint16")] ushort left_rumble, [NativeTypeName("Uint16")] ushort right_rumble, [NativeTypeName("Uint32")] uint duration_ms);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GameControllerHasLED([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GameControllerHasRumble([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_GameControllerHasRumbleTriggers([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerSetLED([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, [NativeTypeName("Uint8")] byte red, [NativeTypeName("Uint8")] byte green, [NativeTypeName("Uint8")] byte blue);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GameControllerSendEffect([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, [NativeTypeName("const void *")] nint data, int size);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GameControllerClose([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GameControllerGetAppleSFSymbolsNameForButton([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_GameControllerButton button);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GameControllerGetAppleSFSymbolsNameForAxis([NativeTypeName("SDL_GameController *")] _SDL_GameController* gamecontroller, SDL_GameControllerAxis axis);
    }
}
