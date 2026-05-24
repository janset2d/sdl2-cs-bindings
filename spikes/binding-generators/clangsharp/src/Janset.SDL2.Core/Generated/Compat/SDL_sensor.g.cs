using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_Sensor
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

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LockSensors();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_UnlockSensors();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_NumSensors();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_SensorGetDeviceName(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_SensorType SDL_SensorGetDeviceType(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SensorGetDeviceNonPortableType(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_SensorID")]
        public static extern int SDL_SensorGetDeviceInstanceID(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Sensor* SDL_SensorOpen(int device_index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_Sensor* SDL_SensorFromInstanceID([NativeTypeName("SDL_SensorID")] int instance_id);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_SensorGetName(SDL_Sensor* sensor);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_SensorType SDL_SensorGetType(SDL_Sensor* sensor);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SensorGetNonPortableType(SDL_Sensor* sensor);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_SensorID")]
        public static extern int SDL_SensorGetInstanceID(SDL_Sensor* sensor);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SensorGetData(SDL_Sensor* sensor, float* data, int num_values);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SensorGetDataWithTimestamp(SDL_Sensor* sensor, [NativeTypeName("Uint64 *")] ulong* timestamp, float* data, int num_values);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_SensorClose(SDL_Sensor* sensor);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_SensorUpdate();

        [NativeTypeName("#define SDL_STANDARD_GRAVITY 9.80665f")]
        public const float SDL_STANDARD_GRAVITY = 9.80665f;
    }
}
