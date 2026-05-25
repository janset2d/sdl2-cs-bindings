#if !NET462
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl.Callbacks;

internal sealed unsafe class SdlEventWatchCounter : IDisposable
{
    private readonly nint _countAddress;
    private bool _disposed;

    private SdlEventWatchCounter()
    {
        _countAddress = Marshal.AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(_countAddress, 0);
        SDLNative.SDL_AddEventWatch(&EventWatchCallback, _countAddress);
    }

    public static SdlEventWatchCounter ForUserEvents() => new();

    public int Count => Marshal.ReadInt32(_countAddress);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SDLNative.SDL_DelEventWatch(&EventWatchCallback, _countAddress);
        Marshal.FreeHGlobal(_countAddress);
        _disposed = true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int EventWatchCallback(nint userdata, SDL_Event* @event)
    {
        if (@event->type == (uint)SDL_EventType.SDL_USEREVENT)
        {
            (*(int*)userdata)++;
        }

        return 1;
    }
}
#endif
