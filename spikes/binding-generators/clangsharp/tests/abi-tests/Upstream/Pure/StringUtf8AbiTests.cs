using System.Text;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Pure;

public sealed class StringUtf8AbiTests
{
    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    public async Task SDLStringLengthFunctions_Should_Report_Utf8_Bytes_And_Bmp_Characters()
    {
        const string value = "A\u00E7\u20AC";

        LengthResult result = MeasureStringLengths(value);

        await Assert.That(result.ByteLength).IsEqualTo((ulong)Encoding.UTF8.GetByteCount(value));
        await Assert.That(result.CharacterLength).IsEqualTo((ulong)value.Length);
        await Assert.That(result.ByteLength).IsNotEqualTo(result.CharacterLength);
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    public async Task SDLStrdup_Should_Return_Owned_NullTerminated_Utf8_Copy()
    {
        const string value = "SDL \u00E7\u20AC copy";

        StrdupResult result = DuplicateString(value);

        await Assert.That(result.PointerWasNotNull).IsTrue();
        await Assert.That(result.DecodedValue).IsEqualTo(value);
    }

    private static unsafe LengthResult MeasureStringLengths(string value)
    {
        using PinnedUtf8 pinned = SdlUtf8.Pin(value);

        return new LengthResult(
            ToUInt64(SDL_strlen(pinned.Pointer)),
            ToUInt64(SDL_utf8strlen(pinned.Pointer)));
    }

    private static unsafe StrdupResult DuplicateString(string value)
    {
        using PinnedUtf8 pinned = SdlUtf8.Pin(value);

        byte* duplicate = SDL_strdup(pinned.Pointer);
        try
        {
            return duplicate is null
                ? new StrdupResult(false, string.Empty)
                : new StrdupResult(true, SdlUtf8.FromNullTerminated(duplicate));
        }
        finally
        {
            if (duplicate is not null)
            {
                SDL_free((nint)duplicate);
            }
        }
    }

#if NET462
    private static ulong ToUInt64(UIntPtr value) => value.ToUInt64();
#else
    private static ulong ToUInt64(nuint value) => checked((ulong)value);
#endif

    private sealed record LengthResult(ulong ByteLength, ulong CharacterLength);

    private sealed record StrdupResult(bool PointerWasNotNull, string DecodedValue);
}
