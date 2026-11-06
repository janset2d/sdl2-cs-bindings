using System.Text;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl;

internal static unsafe class SdlUtf8
{
    public static string FromNullTerminated(byte* value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        int length = 0;

        while (value[length] != 0)
        {
            length++;
        }

        return Encoding.UTF8.GetString(value, length);
    }

    public static PinnedUtf8 Pin(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
        return new PinnedUtf8(bytes);
    }
}
