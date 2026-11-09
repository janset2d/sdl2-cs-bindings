using System.Runtime.InteropServices;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl;

internal sealed unsafe class PinnedUtf8 : IDisposable
{
    private readonly GCHandle _handle;

    public PinnedUtf8(byte[] bytes)
    {
        _handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
    }

    public byte* Pointer => (byte*)_handle.AddrOfPinnedObject();

    public void Dispose()
    {
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }
    }
}
