using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;

internal sealed class SdlHintScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previousValue;

    public SdlHintScope(string name, string value)
    {
        _name = name;
        _previousValue = GetValue(name);
        SetValue(name, value);
    }

    public string? CurrentValue => GetValue(_name);

    public void Dispose()
    {
        if (_previousValue is null)
        {
            ResetValue(_name);
            return;
        }

        SetValue(_name, _previousValue);
    }

    private static unsafe string? GetValue(string name)
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        byte* value = SDLNative.SDL_GetHint(pinnedName.Pointer);

        return value is null ? null : SdlUtf8.FromNullTerminated(value);
    }

    private static unsafe void SetValue(string name, string value)
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        using PinnedUtf8 pinnedValue = SdlUtf8.Pin(value);

        SDL_bool result = SDLNative.SDL_SetHint(pinnedName.Pointer, pinnedValue.Pointer);
        if (result != SDL_bool.SDL_TRUE)
        {
            throw new InvalidOperationException($"SDL_SetHint failed for {name}: {SdlError.Current}");
        }
    }

    private static unsafe void ResetValue(string name)
    {
        using PinnedUtf8 pinnedName = SdlUtf8.Pin(name);
        SDLNative.SDL_ResetHint(pinnedName.Pointer);
    }
}
