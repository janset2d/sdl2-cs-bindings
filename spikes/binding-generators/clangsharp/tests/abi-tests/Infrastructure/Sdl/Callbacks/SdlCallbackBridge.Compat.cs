#if NET462
using System.Runtime.InteropServices;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Infrastructure.Callbacks;

internal sealed unsafe class SdlEventWatchCounter : IDisposable
{
    private readonly nint _countAddress;
    private readonly SDL_EventFilter _filter;
    private readonly IntPtr _filterPointer;
    private bool _disposed;

    private SdlEventWatchCounter()
    {
        _countAddress = Marshal.AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(_countAddress, 0);
        _filter = EventWatchCallback;
        _filterPointer = Marshal.GetFunctionPointerForDelegate(_filter);
        SDL_AddEventWatch(_filterPointer, _countAddress);
    }

    public static SdlEventWatchCounter ForUserEvents() => new();

    public int Count => Marshal.ReadInt32(_countAddress);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SDL_DelEventWatch(_filterPointer, _countAddress);
        Marshal.FreeHGlobal(_countAddress);
        GC.KeepAlive(_filter);
        _disposed = true;
    }

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
