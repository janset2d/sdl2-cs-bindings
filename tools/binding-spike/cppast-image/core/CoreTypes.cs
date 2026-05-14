using System.Runtime.InteropServices;

namespace Janset.Spike.SDL2.Core;

public readonly partial struct SdlSurface;

public readonly partial struct SdlTexture;

public readonly partial struct SdlRenderer;

public readonly partial struct SdlRWops;

[StructLayout(LayoutKind.Sequential)]
public partial struct SdlVersion
{
    public byte major;
    public byte minor;
    public byte patch;
}
