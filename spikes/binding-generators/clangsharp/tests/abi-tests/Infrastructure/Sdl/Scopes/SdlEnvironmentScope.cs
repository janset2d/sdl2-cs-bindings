using System.Runtime.InteropServices;
using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;

internal sealed class SdlEnvironmentScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previousValue;

    public SdlEnvironmentScope(string name, string value)
    {
        _name = name;
        _previousValue = GetValue(name);
        SetValue(name, value);
    }

    public void Dispose()
    {
        SetValue(_name, _previousValue);
    }

    private static unsafe string? GetValue(string name)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Environment.GetEnvironmentVariable(name);
        }

        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        byte* value = SDLNative.SDL_getenv(pinnedName.Pointer);
        return value is null ? null : SdlUtf8.FromNullTerminated(value);
    }

    private static unsafe void SetValue(string name, string? value)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Environment.SetEnvironmentVariable(name, value);
            return;
        }

        // .NET's Unix environment copy is not the native environment SDL reads.
        // Use unsetenv for absence: setenv with an empty string leaves a value present.
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        using PinnedUtf8? pinnedValue = value is null ? null : SdlUtf8.Pin(value);
        int result = pinnedValue is null
            ? UnsetEnvironmentVariable(pinnedName.Pointer)
            : SetEnvironmentVariable(pinnedName.Pointer, pinnedValue.Pointer, 1);
        if (result != 0)
        {
            throw new InvalidOperationException($"Could not update native environment variable '{name}'.");
        }
    }

    [DllImport("libc", EntryPoint = "setenv", CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe int SetEnvironmentVariable(byte* name, byte* value, int overwrite);

    [DllImport("libc", EntryPoint = "unsetenv", CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe int UnsetEnvironmentVariable(byte* name);
}
