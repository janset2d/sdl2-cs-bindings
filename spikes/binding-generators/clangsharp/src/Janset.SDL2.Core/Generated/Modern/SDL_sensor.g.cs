using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public partial struct _SDL_Sensor
    {
    }

    public enum SDL_SensorType
    {
        SDL_SENSOR_INVALID = -1,
        SDL_SENSOR_UNKNOWN,
        SDL_SENSOR_ACCEL,
        SDL_SENSOR_GYRO,
        SDL_SENSOR_ACCEL_L,
        SDL_SENSOR_GYRO_L,
        SDL_SENSOR_ACCEL_R,
        SDL_SENSOR_GYRO_R,
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_LockSensors();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_UnlockSensors();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_NumSensors();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_SensorGetDeviceName(int device_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_SensorType SDL_SensorGetDeviceType(int device_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SensorGetDeviceNonPortableType(int device_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_SensorID")]
        public static partial int SDL_SensorGetDeviceInstanceID(int device_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_Sensor *")]
        public static partial _SDL_Sensor* SDL_SensorOpen(int device_index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_Sensor *")]
        public static partial _SDL_Sensor* SDL_SensorFromInstanceID([NativeTypeName("SDL_SensorID")] int instance_id);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_SensorGetName([NativeTypeName("SDL_Sensor *")] _SDL_Sensor* sensor);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_SensorType SDL_SensorGetType([NativeTypeName("SDL_Sensor *")] _SDL_Sensor* sensor);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SensorGetNonPortableType([NativeTypeName("SDL_Sensor *")] _SDL_Sensor* sensor);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_SensorID")]
        public static partial int SDL_SensorGetInstanceID([NativeTypeName("SDL_Sensor *")] _SDL_Sensor* sensor);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SensorGetData([NativeTypeName("SDL_Sensor *")] _SDL_Sensor* sensor, float* data, int num_values);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SensorGetDataWithTimestamp([NativeTypeName("SDL_Sensor *")] _SDL_Sensor* sensor, [NativeTypeName("Uint64 *")] ulong* timestamp, float* data, int num_values);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SensorClose([NativeTypeName("SDL_Sensor *")] _SDL_Sensor* sensor);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SensorUpdate();

        [NativeTypeName("#define SDL_STANDARD_GRAVITY 9.80665f")]
        public const float SDL_STANDARD_GRAVITY = 9.80665f;
    }
}
