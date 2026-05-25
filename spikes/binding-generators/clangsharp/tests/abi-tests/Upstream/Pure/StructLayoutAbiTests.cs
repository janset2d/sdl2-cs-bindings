using System;
using System.Runtime.InteropServices;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using SDL2;

namespace Janset.SDL2.AbiTests.Upstream.Pure;

public sealed class StructLayoutAbiTests
{
    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlAudio)]
    public async Task SDLAudioSpec_Should_Match_Header_Struct_Layout()
    {
        await AssertOffset<SDL_AudioSpec>(nameof(SDL_AudioSpec.freq), 0);
        await AssertOffset<SDL_AudioSpec>(nameof(SDL_AudioSpec.format), 4);
        await AssertOffset<SDL_AudioSpec>(nameof(SDL_AudioSpec.channels), 6);
        await AssertOffset<SDL_AudioSpec>(nameof(SDL_AudioSpec.silence), 7);
        await AssertOffset<SDL_AudioSpec>(nameof(SDL_AudioSpec.samples), 8);
        await AssertOffset<SDL_AudioSpec>(nameof(SDL_AudioSpec.padding), 10);
        await AssertOffset<SDL_AudioSpec>(nameof(SDL_AudioSpec.size), 12);
        await AssertOffset<SDL_AudioSpec>(nameof(SDL_AudioSpec.callback), 16);
        await AssertOffset<SDL_AudioSpec>(nameof(SDL_AudioSpec.userdata), 16 + IntPtr.Size);
        int actualSize;
        unsafe
        {
            actualSize = sizeof(SDL_AudioSpec);
        }

        await Assert.That(actualSize).IsEqualTo(16 + (2 * IntPtr.Size));
    }

    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlSurface)]
    public async Task SDLSurface_Should_Match_Header_Struct_Layout()
    {
        int pointerSize = IntPtr.Size;
        int formatOffset = Align(4, pointerSize);
        int wOffset = formatOffset + pointerSize;
        int pixelsOffset = Align(wOffset + 12, pointerSize);
        int userdataOffset = pixelsOffset + pointerSize;
        int lockedOffset = userdataOffset + pointerSize;
        int listBlitmapOffset = Align(lockedOffset + 4, pointerSize);
        int clipRectOffset = listBlitmapOffset + pointerSize;
        int mapOffset = clipRectOffset + Marshal.SizeOf<SDL_Rect>();
        int refcountOffset = mapOffset + pointerSize;

        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.flags), 0);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.format), formatOffset);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.w), wOffset);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.h), wOffset + 4);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.pitch), wOffset + 8);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.pixels), pixelsOffset);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.userdata), userdataOffset);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.locked), lockedOffset);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.list_blitmap), listBlitmapOffset);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.clip_rect), clipRectOffset);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.map), mapOffset);
        await AssertOffset<SDL_Surface>(nameof(SDL_Surface.refcount), refcountOffset);
        int actualSize;
        unsafe
        {
            actualSize = sizeof(SDL_Surface);
        }

        await Assert.That(actualSize).IsEqualTo(Align(refcountOffset + 4, pointerSize));
    }

    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlEvents)]
    public async Task SDLEvent_Should_Match_Header_Union_Layout()
    {
        await AssertOffset<SDL_Event>(nameof(SDL_Event.type), 0);
        await AssertOffset<SDL_Event>(nameof(SDL_Event.user), 0);
        await AssertOffset<SDL_Event>(nameof(SDL_Event.padding), 0);
        int actualSize;
        unsafe
        {
            actualSize = sizeof(SDL_Event);
        }

        await Assert.That(actualSize).IsEqualTo(56);
    }

    private static async Task AssertOffset<T>(string fieldName, int expectedOffset)
    {
        int actualOffset = Marshal.OffsetOf<T>(fieldName).ToInt32();

        await Assert.That(actualOffset).IsEqualTo(expectedOffset);
    }

    private static int Align(int value, int alignment)
    {
        return ((value + alignment) - 1) / alignment * alignment;
    }
}
