namespace Janset.SDL2.AbiTests.Infrastructure.Sdl;

internal static class NativeSizeConversion
{
#if NETFRAMEWORK
    internal static ulong ToUInt64(UIntPtr value) => value.ToUInt64();
#else
    internal static ulong ToUInt64(nuint value) => value;
#endif
}
