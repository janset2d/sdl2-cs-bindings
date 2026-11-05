using System.Text;
using Janset.SDL2.AbiTests.Infrastructure;
using Janset.SDL2.AbiTests.Infrastructure.Assets;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Assets;

public sealed class OwnershipFreeAbiTests
{
    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    public async Task SDLLoadFileRw_Should_Return_Sdl_Owned_Buffer_And_Close_Source_When_FreeSrc_Is_One()
    {
        using AbiTempDirectory temp = new();
        byte[] expected = Encoding.ASCII.GetBytes("owned by SDL");
        string path = temp.GetFilePath("load-file-rw.bin");
        File.WriteAllBytes(path, expected);

        LoadFileResult result = LoadFileWithTransferredSource(path, expected.Length);

        await Assert.That(result.PointerIsNotNull).IsTrue();
        await Assert.That(result.Size).IsEqualTo((ulong)expected.Length);
        await Assert.That(result.Bytes.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlRwops)]
    [Category(AbiCategories.SdlSurface)]
    public async Task SDLLoadBmpRw_Should_Leave_Source_Open_When_FreeSrc_Is_Zero()
    {
        BmpNonTransferResult result = LoadBmpWithoutTransferringSource(UpstreamSdlAsset.Path("sample.bmp"));

        await Assert.That(result.SurfaceWasNotNull).IsTrue();
        await Assert.That(result.SeekPosition).IsEqualTo(0L);
        await Assert.That(result.ReadObjects).IsEqualTo(2UL);
        await Assert.That(result.Header).IsEqualTo("BM");
        await SdlAssert.Success(result.CloseResult, "SDL_RWclose");
    }

    private static unsafe LoadFileResult LoadFileWithTransferredSource(string path, int expectedLength)
    {
        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 readMode = SdlUtf8.Pin("rb");

        SDL_RWops rwops = SDL_RWFromFile(pinnedPath.Pointer, readMode.Pointer);
        if (rwops.IsNull)
        {
            throw new InvalidOperationException($"SDL_RWFromFile failed: {SdlError.Current}");
        }

        nint buffer = 0;
        var dataSize = NativeSize(0);

        try
        {
            buffer = SDL_LoadFile_RW(rwops, &dataSize, 1);
            ulong actualSize = ToUInt64(dataSize);
            byte[] bytes = Array.Empty<byte>();
            if (buffer != 0 && actualSize >= (ulong)expectedLength)
            {
                bytes = new byte[expectedLength];
                new ReadOnlySpan<byte>((void*)buffer, expectedLength).CopyTo(bytes);
            }

            return new LoadFileResult(buffer != 0, actualSize, bytes);
        }
        finally
        {
            if (buffer != 0)
            {
                SDL_free(buffer);
            }
        }
    }

    private static unsafe BmpNonTransferResult LoadBmpWithoutTransferringSource(string path)
    {
        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 readMode = SdlUtf8.Pin("rb");

        SDL_RWops source = SDL_RWFromFile(pinnedPath.Pointer, readMode.Pointer);
        if (source.IsNull)
        {
            throw new InvalidOperationException($"SDL_RWFromFile failed: {SdlError.Current}");
        }

        SDL_Surface* surface = null;
        long seekPosition = -1;
        ulong readObjects = 0;
        int closeResult;
        byte[] header = new byte[2];

        try
        {
            surface = SDL_LoadBMP_RW(source, 0);
            if (surface is null)
            {
                throw new InvalidOperationException($"SDL_LoadBMP_RW failed: {SdlError.Current}");
            }

            fixed (byte* headerPointer = header)
            {
                seekPosition = SDL_RWseek(source, 0, RW_SEEK_SET);
                readObjects = ToUInt64(SDL_RWread(source, (nint)headerPointer, NativeSize(1), NativeSize(header.Length)));
            }
        }
        finally
        {
            if (surface is not null)
            {
                SDL_FreeSurface(surface);
            }

            closeResult = SDL_RWclose(source);
        }

        return new BmpNonTransferResult(true, seekPosition, readObjects, Encoding.ASCII.GetString(header), closeResult);
    }

#if NET462
    private static UIntPtr NativeSize(int value) => new((uint)value);

    private static ulong ToUInt64(UIntPtr value) => value.ToUInt64();
#else
    private static nuint NativeSize(int value) => (nuint)value;

    private static ulong ToUInt64(nuint value) => checked((ulong)value);
#endif

    private sealed record LoadFileResult(bool PointerIsNotNull, ulong Size, byte[] Bytes);

    private sealed record BmpNonTransferResult(bool SurfaceWasNotNull, long SeekPosition, ulong ReadObjects, string Header, int CloseResult);
}
