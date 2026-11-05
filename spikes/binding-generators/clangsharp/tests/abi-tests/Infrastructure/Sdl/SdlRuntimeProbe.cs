using System.Runtime.InteropServices;
using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl;

internal static unsafe class SdlRuntimeProbe
{
    private delegate int DriverCountProvider();

    private delegate byte* DriverNameProvider(int index);

    private static readonly Lazy<string[]> VideoDriversLazy = new(() => EnumerateDrivers(
        SDLNative.SDL_GetNumVideoDrivers,
        SDLNative.SDL_GetVideoDriver));

    private static readonly Lazy<string[]> AudioDriversLazy = new(() => EnumerateDrivers(
        SDLNative.SDL_GetNumAudioDrivers,
        SDLNative.SDL_GetAudioDriver));

    public static string FrameworkDescription => RuntimeInformation.FrameworkDescription;

    public static Architecture ProcessArchitecture => RuntimeInformation.ProcessArchitecture;

    public static Architecture OperatingSystemArchitecture => RuntimeInformation.OSArchitecture;

    public static string RuntimeIdentifier
    {
        get
        {
#if NET6_0_OR_GREATER
            return RuntimeInformation.RuntimeIdentifier;
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-net462" : "net462";
#endif
        }
    }

    public static string SdlPlatform => SdlUtf8.FromNullTerminated(SDLNative.SDL_GetPlatform());

    public static IReadOnlyList<string> VideoDrivers => VideoDriversLazy.Value;

    public static IReadOnlyList<string> AudioDrivers => AudioDriversLazy.Value;

    public static bool HasVideoDriver(string driverName) => ContainsDriver(VideoDriversLazy.Value, driverName);

    public static bool HasAudioDriver(string driverName) => ContainsDriver(AudioDriversLazy.Value, driverName);

    public static string MissingVideoDriverMessage(string driverName) => MissingDriverMessage("video", driverName, VideoDriversLazy.Value);

    public static string MissingAudioDriverMessage(string driverName) => MissingDriverMessage("audio", driverName, AudioDriversLazy.Value);

    private static string[] EnumerateDrivers(DriverCountProvider getCount, DriverNameProvider getDriver)
    {
        int count = getCount();
        if (count <= 0)
        {
            return [];
        }

        var drivers = new string[count];
        for (int index = 0; index < count; index++)
        {
            drivers[index] = SdlUtf8.FromNullTerminated(getDriver(index));
        }

        return drivers;
    }

    private static bool ContainsDriver(IReadOnlyList<string> drivers, string driverName)
    {
        return drivers.Any(driver => string.Equals(driver, driverName, StringComparison.OrdinalIgnoreCase));
    }

    private static string MissingDriverMessage(string driverKind, string driverName, IReadOnlyList<string> availableDrivers)
    {
        string available = availableDrivers.Count == 0 ? "<none>" : string.Join(", ", availableDrivers);
        return $"SDL {driverKind} driver '{driverName}' is not available. Available {driverKind} drivers: {available}.";
    }
}
