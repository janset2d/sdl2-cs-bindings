using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Pure;

public sealed class GuidAbiTests
{
    [Test]
    [Arguments("0000000000000000ffffffffffffffff")]
    [Arguments("00112233445566778091a2b3c4d5e6f0")]
    [Arguments("a0112233445566778091a2b3c4d5e6f0")]
    [Arguments("a0112233445566778091a2b3c4d5e6f1")]
    [Arguments("a0112233445566778191a2b3c4d5e6f0")]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlGuid)]
    [UpstreamSdlTest("test/testautomation_guid.c", "TestGuidFromString")]
    [UpstreamSdlTest("test/testautomation_guid.c", "TestGuidToString")]
    public async Task SDLGuidRoundTrip_Should_Preserve_Sdl_Raw_Hex_String(string guidString)
    {
        GuidConversionSnapshot snapshot = ConvertGuidFromString(guidString);

        await Assert.That(snapshot.RoundTrippedString).IsEqualTo(guidString);
        await Assert.That(snapshot.RawBytes.SequenceEqual(ParseSdlGuidBytes(guidString))).IsTrue();
    }

    [Test]
    [Arguments("0000000000000000ffffffffffffffff")]
    [Arguments("00112233445566778091a2b3c4d5e6f0")]
    [Arguments("a0112233445566778091a2b3c4d5e6f0")]
    [Arguments("a0112233445566778091a2b3c4d5e6f1")]
    [Arguments("a0112233445566778191a2b3c4d5e6f0")]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlGuid)]
    [UpstreamSdlTest("test/testautomation_guid.c", "TestGuidToString")]
    public async Task SDLGuidToString_Should_Write_Null_Terminated_ThirtyTwo_Character_String(string guidString)
    {
        string roundTripped = ConvertRawGuidToString(guidString);

        await Assert.That(roundTripped.Length).IsEqualTo(32);
        await Assert.That(roundTripped).IsEqualTo(guidString);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(32)]
    [Arguments(33)]
    [Arguments(36)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlGuid)]
    [UpstreamSdlTest("test/testautomation_guid.c", "TestGuidToString")]
    public async Task SDLGuidToString_Should_Not_Write_Before_Buffer_And_Should_Respect_Buffer_Size(int size)
    {
        const string guidStrings = "00112233445566778091a2b3c4d5e6f0";
        GuidWriteSnapshot snapshot = WriteGuidWithOffset(guidStrings, size);

        await Assert.That(snapshot.PrefixWasUntouched).IsTrue();
        await Assert.That(snapshot.WrittenSize).IsLessThanOrEqualTo(size);
        if (size >= 33)
        {
            await Assert.That(snapshot.Output).IsEqualTo(guidStrings);
        }
    }

    private static unsafe GuidConversionSnapshot ConvertGuidFromString(string guidString)
    {
        using PinnedUtf8 input = SdlUtf8.Pin(guidString);
        Guid guid = SDL_GUIDFromString(input.Pointer);

        byte* buffer = stackalloc byte[33];
        SDL_GUIDToString(guid, buffer, 33);

        return new GuidConversionSnapshot(SdlUtf8.FromNullTerminated(buffer), ReadRawGuidBytes(guid));
    }

    private static unsafe string ConvertRawGuidToString(string guidString)
    {
        Guid guid = CreateGuidFromSdlBytes(ParseSdlGuidBytes(guidString));

        byte* buffer = stackalloc byte[33];
        SDL_GUIDToString(guid, buffer, 33);

        return SdlUtf8.FromNullTerminated(buffer);
    }

    private static unsafe GuidWriteSnapshot WriteGuidWithOffset(string guidString, int size)
    {
        const int guidStringOffset = 4;
        const int bufferSize = 64;

        Guid guid = CreateGuidFromSdlBytes(ParseSdlGuidBytes(guidString));

        byte* buffer = stackalloc byte[bufferSize];
        byte fillChar = (byte)(size + 0xA0);
        for (int i = 0; i < bufferSize; i++)
        {
            buffer[i] = fillChar;
        }

        byte* output = buffer + guidStringOffset;
        SDL_GUIDToString(guid, output, size);

        bool prefixWasUntouched = buffer[0] == fillChar
            && buffer[1] == fillChar
            && buffer[2] == fillChar
            && buffer[3] == fillChar;
        int writtenSize = 0;
        while (writtenSize < bufferSize - guidStringOffset && output[writtenSize] != fillChar)
        {
            writtenSize++;
        }

        string outputString = size >= 33 ? SdlUtf8.FromNullTerminated(output) : string.Empty;
        return new GuidWriteSnapshot(prefixWasUntouched, writtenSize, outputString);
    }

    private static unsafe byte[] ReadRawGuidBytes(Guid guid)
    {
        byte[] bytes = new byte[16];
        byte* guidBytes = (byte*)&guid;
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = guidBytes[i];
        }

        return bytes;
    }

    private static unsafe Guid CreateGuidFromSdlBytes(byte[] bytes)
    {
        fixed (byte* bytesPointer = bytes)
        {
            return *(Guid*)bytesPointer;
        }
    }

    private static byte[] ParseSdlGuidBytes(string guidString)
    {
        if (guidString.Length != 32)
        {
            throw new ArgumentException("SDL GUID strings must contain exactly 32 hex characters.", nameof(guidString));
        }

        byte[] bytes = new byte[16];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((HexValue(guidString[i * 2]) << 4) | HexValue(guidString[(i * 2) + 1]));
        }

        return bytes;
    }

    private static int HexValue(char value)
    {
        return value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Expected a hexadecimal character."),
        };
    }

    private sealed record GuidConversionSnapshot(string RoundTrippedString, byte[] RawBytes);

    private readonly record struct GuidWriteSnapshot(bool PrefixWasUntouched, int WrittenSize, string Output);
}
