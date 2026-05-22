using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct _SDL_Haptic
    {
    }

    public unsafe partial struct SDL_HapticDirection
    {
        [NativeTypeName("Uint8")]
        public byte type;

        [NativeTypeName("Sint32[3]")]
        public fixed int dir[3];
    }

    public partial struct SDL_HapticConstant
    {
        [NativeTypeName("Uint16")]
        public ushort type;

        public SDL_HapticDirection direction;

        [NativeTypeName("Uint32")]
        public uint length;

        [NativeTypeName("Uint16")]
        public ushort delay;

        [NativeTypeName("Uint16")]
        public ushort button;

        [NativeTypeName("Uint16")]
        public ushort interval;

        [NativeTypeName("Sint16")]
        public short level;

        [NativeTypeName("Uint16")]
        public ushort attack_length;

        [NativeTypeName("Uint16")]
        public ushort attack_level;

        [NativeTypeName("Uint16")]
        public ushort fade_length;

        [NativeTypeName("Uint16")]
        public ushort fade_level;
    }

    public partial struct SDL_HapticPeriodic
    {
        [NativeTypeName("Uint16")]
        public ushort type;

        public SDL_HapticDirection direction;

        [NativeTypeName("Uint32")]
        public uint length;

        [NativeTypeName("Uint16")]
        public ushort delay;

        [NativeTypeName("Uint16")]
        public ushort button;

        [NativeTypeName("Uint16")]
        public ushort interval;

        [NativeTypeName("Uint16")]
        public ushort period;

        [NativeTypeName("Sint16")]
        public short magnitude;

        [NativeTypeName("Sint16")]
        public short offset;

        [NativeTypeName("Uint16")]
        public ushort phase;

        [NativeTypeName("Uint16")]
        public ushort attack_length;

        [NativeTypeName("Uint16")]
        public ushort attack_level;

        [NativeTypeName("Uint16")]
        public ushort fade_length;

        [NativeTypeName("Uint16")]
        public ushort fade_level;
    }

    public unsafe partial struct SDL_HapticCondition
    {
        [NativeTypeName("Uint16")]
        public ushort type;

        public SDL_HapticDirection direction;

        [NativeTypeName("Uint32")]
        public uint length;

        [NativeTypeName("Uint16")]
        public ushort delay;

        [NativeTypeName("Uint16")]
        public ushort button;

        [NativeTypeName("Uint16")]
        public ushort interval;

        [NativeTypeName("Uint16[3]")]
        public fixed ushort right_sat[3];

        [NativeTypeName("Uint16[3]")]
        public fixed ushort left_sat[3];

        [NativeTypeName("Sint16[3]")]
        public fixed short right_coeff[3];

        [NativeTypeName("Sint16[3]")]
        public fixed short left_coeff[3];

        [NativeTypeName("Uint16[3]")]
        public fixed ushort deadband[3];

        [NativeTypeName("Sint16[3]")]
        public fixed short center[3];
    }

    public partial struct SDL_HapticRamp
    {
        [NativeTypeName("Uint16")]
        public ushort type;

        public SDL_HapticDirection direction;

        [NativeTypeName("Uint32")]
        public uint length;

        [NativeTypeName("Uint16")]
        public ushort delay;

        [NativeTypeName("Uint16")]
        public ushort button;

        [NativeTypeName("Uint16")]
        public ushort interval;

        [NativeTypeName("Sint16")]
        public short start;

        [NativeTypeName("Sint16")]
        public short end;

        [NativeTypeName("Uint16")]
        public ushort attack_length;

        [NativeTypeName("Uint16")]
        public ushort attack_level;

        [NativeTypeName("Uint16")]
        public ushort fade_length;

        [NativeTypeName("Uint16")]
        public ushort fade_level;
    }

    public partial struct SDL_HapticLeftRight
    {
        [NativeTypeName("Uint16")]
        public ushort type;

        [NativeTypeName("Uint32")]
        public uint length;

        [NativeTypeName("Uint16")]
        public ushort large_magnitude;

        [NativeTypeName("Uint16")]
        public ushort small_magnitude;
    }

    public unsafe partial struct SDL_HapticCustom
    {
        [NativeTypeName("Uint16")]
        public ushort type;

        public SDL_HapticDirection direction;

        [NativeTypeName("Uint32")]
        public uint length;

        [NativeTypeName("Uint16")]
        public ushort delay;

        [NativeTypeName("Uint16")]
        public ushort button;

        [NativeTypeName("Uint16")]
        public ushort interval;

        [NativeTypeName("Uint8")]
        public byte channels;

        [NativeTypeName("Uint16")]
        public ushort period;

        [NativeTypeName("Uint16")]
        public ushort samples;

        [NativeTypeName("Uint16 *")]
        public ushort* data;

        [NativeTypeName("Uint16")]
        public ushort attack_length;

        [NativeTypeName("Uint16")]
        public ushort attack_level;

        [NativeTypeName("Uint16")]
        public ushort fade_length;

        [NativeTypeName("Uint16")]
        public ushort fade_level;
    }

    [StructLayout(LayoutKind.Explicit)]
    public partial struct SDL_HapticEffect
    {
        [FieldOffset(0)]
        [NativeTypeName("Uint16")]
        public ushort type;

        [FieldOffset(0)]
        public SDL_HapticConstant constant;

        [FieldOffset(0)]
        public SDL_HapticPeriodic periodic;

        [FieldOffset(0)]
        public SDL_HapticCondition condition;

        [FieldOffset(0)]
        public SDL_HapticRamp ramp;

        [FieldOffset(0)]
        public SDL_HapticLeftRight leftright;

        [FieldOffset(0)]
        public SDL_HapticCustom custom;
    }

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_NumHaptics();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_HapticName(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Haptic *")]
        public static extern _SDL_Haptic* SDL_HapticOpen(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticOpened(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticIndex([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_MouseIsHaptic();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Haptic *")]
        public static extern _SDL_Haptic* SDL_HapticOpenFromMouse();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_JoystickIsHaptic([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_Haptic *")]
        public static extern _SDL_Haptic* SDL_HapticOpenFromJoystick([NativeTypeName("SDL_Joystick *")] _SDL_Joystick* joystick);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_HapticClose([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticNumEffects([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticNumEffectsPlaying([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("unsigned int")]
        public static extern uint SDL_HapticQuery([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticNumAxes([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticEffectSupported([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, SDL_HapticEffect* effect);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticNewEffect([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, SDL_HapticEffect* effect);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticUpdateEffect([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, int effect, SDL_HapticEffect* data);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticRunEffect([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, int effect, [NativeTypeName("Uint32")] uint iterations);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticStopEffect([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, int effect);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_HapticDestroyEffect([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, int effect);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticGetEffectStatus([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, int effect);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticSetGain([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, int gain);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticSetAutocenter([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, int autocenter);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticPause([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticUnpause([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticStopAll([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticRumbleSupported([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticRumbleInit([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticRumblePlay([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic, float strength, [NativeTypeName("Uint32")] uint length);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_HapticRumbleStop([NativeTypeName("SDL_Haptic *")] _SDL_Haptic* haptic);

        [NativeTypeName("#define SDL_HAPTIC_CONSTANT (1u<<0)")]
        public const uint SDL_HAPTIC_CONSTANT = (1U << 0);

        [NativeTypeName("#define SDL_HAPTIC_SINE (1u<<1)")]
        public const uint SDL_HAPTIC_SINE = (1U << 1);

        [NativeTypeName("#define SDL_HAPTIC_LEFTRIGHT (1u<<2)")]
        public const uint SDL_HAPTIC_LEFTRIGHT = (1U << 2);

        [NativeTypeName("#define SDL_HAPTIC_TRIANGLE (1u<<3)")]
        public const uint SDL_HAPTIC_TRIANGLE = (1U << 3);

        [NativeTypeName("#define SDL_HAPTIC_SAWTOOTHUP (1u<<4)")]
        public const uint SDL_HAPTIC_SAWTOOTHUP = (1U << 4);

        [NativeTypeName("#define SDL_HAPTIC_SAWTOOTHDOWN (1u<<5)")]
        public const uint SDL_HAPTIC_SAWTOOTHDOWN = (1U << 5);

        [NativeTypeName("#define SDL_HAPTIC_RAMP (1u<<6)")]
        public const uint SDL_HAPTIC_RAMP = (1U << 6);

        [NativeTypeName("#define SDL_HAPTIC_SPRING (1u<<7)")]
        public const uint SDL_HAPTIC_SPRING = (1U << 7);

        [NativeTypeName("#define SDL_HAPTIC_DAMPER (1u<<8)")]
        public const uint SDL_HAPTIC_DAMPER = (1U << 8);

        [NativeTypeName("#define SDL_HAPTIC_INERTIA (1u<<9)")]
        public const uint SDL_HAPTIC_INERTIA = (1U << 9);

        [NativeTypeName("#define SDL_HAPTIC_FRICTION (1u<<10)")]
        public const uint SDL_HAPTIC_FRICTION = (1U << 10);

        [NativeTypeName("#define SDL_HAPTIC_CUSTOM (1u<<11)")]
        public const uint SDL_HAPTIC_CUSTOM = (1U << 11);

        [NativeTypeName("#define SDL_HAPTIC_GAIN (1u<<12)")]
        public const uint SDL_HAPTIC_GAIN = (1U << 12);

        [NativeTypeName("#define SDL_HAPTIC_AUTOCENTER (1u<<13)")]
        public const uint SDL_HAPTIC_AUTOCENTER = (1U << 13);

        [NativeTypeName("#define SDL_HAPTIC_STATUS (1u<<14)")]
        public const uint SDL_HAPTIC_STATUS = (1U << 14);

        [NativeTypeName("#define SDL_HAPTIC_PAUSE (1u<<15)")]
        public const uint SDL_HAPTIC_PAUSE = (1U << 15);

        [NativeTypeName("#define SDL_HAPTIC_POLAR 0")]
        public const int SDL_HAPTIC_POLAR = 0;

        [NativeTypeName("#define SDL_HAPTIC_CARTESIAN 1")]
        public const int SDL_HAPTIC_CARTESIAN = 1;

        [NativeTypeName("#define SDL_HAPTIC_SPHERICAL 2")]
        public const int SDL_HAPTIC_SPHERICAL = 2;

        [NativeTypeName("#define SDL_HAPTIC_STEERING_AXIS 3")]
        public const int SDL_HAPTIC_STEERING_AXIS = 3;

        [NativeTypeName("#define SDL_HAPTIC_INFINITY 4294967295U")]
        public const uint SDL_HAPTIC_INFINITY = 4294967295U;
    }
}
