internal static class Fixtures
{
    public const string DynapiExports = "++'_SDL_Init'.'SDL2.dll'.'SDL_Init'";

    public const string ManifestRequiredSurface = """
{
  "library_manifests": [
    {
      "id": "sdl2",
      "role": "core",
      "binding_generation": {
        "required_functions": {
          "SDL.h": ["SDL_Init"]
        },
        "required_constants": {
          "SDL.h": ["SDL_INIT_VIDEO"]
        }
      }
    }
  ]
}
""";

    public const string Sdl2CsSource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    public static class SDL
    {
        private const string nativeLibName = "SDL2";

        [DllImport(nativeLibName)]
        public static extern int SDL_Init(uint flags);
    }
}
""";

    public const string EntryPointImportSource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    public static class SDL
    {
        private const string nativeLibName = "SDL2";

        [DllImport(nativeLibName, EntryPoint = "SDL_Init")]
        private static extern int INTERNAL_SDL_Init(uint flags);
    }
}
""";

    public const string MixedGeneratedSource = """
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_bool
    {
        SDL_FALSE = 0,
        SDL_TRUE = 1,
    }

    public unsafe partial struct SDL_RWops
    {
        public delegate* unmanaged[Cdecl]<SDL_RWops*, long> size;
    }

    public static unsafe partial class SDLNative
    {
        [NativeTypeName("#define SDL_INIT_TIMER 0x00000001u")]
        public const uint SDL_INIT_TIMER = 0x00000001u;

        [NativeTypeName("#define SDL_INIT_AUDIO 0x00000010u")]
        public const uint SDL_INIT_AUDIO = 0x00000010u;

        public const uint SDL_INIT_VIDEO = 0x00000020u;

        [NativeTypeName("#define SDL_INIT_EVERYTHING SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO")]
        public const uint SDL_INIT_EVERYTHING = SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO;

        public static ReadOnlySpan<byte> SDL_HINT_RENDER_DRIVER => "SDL_RENDER_DRIVER"u8;

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_Init(uint flags);

        public static int ManagedHelper() => 42;
    }
}

namespace SDL2.Image
{
    public static unsafe partial class SDL_imageNative
    {
        [LibraryImport("SDL2_image", EntryPoint = "IMG_Init")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_Init(int flags);
    }
}

namespace SDL2
{
    namespace Image
    {
        public static unsafe partial class NestedNamespaceNative
        {
            private const string LibName = "SDL2";

            [DllImport(LibName)]
            public static extern void SDL_Quit();
        }

        public static unsafe partial class NestedImageNative
        {
            private const string LibName = "SDL2_image";

            [DllImport(LibName)]
            public static extern void IMG_Quit();
        }
    }
}
""";

    public const string RawAbiEdgeCaseSource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    public unsafe partial struct SDL_SysWMinfo
    {
        public int version;
    }

    public unsafe partial struct SDL_SysWMmsg
    {
        public int version;
    }

    public static unsafe partial class SDLNative
    {
        [DllImport(LibName)]
        public static extern void SDL_UnresolvedImport();

        [DllImport("SDL2")]
        [return: NativeTypeName("long")]
        public static extern int SDL_LongReturn([NativeTypeName("unsigned long")] uint value);

        [DllImport("SDL2")]
        public static extern void SDL_WcharParameter([NativeTypeName("const wchar_t *")] ushort* value);
    }
}
""";

    public const string InternalRawAbiVisibilitySource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2")]
        public static extern int SDL_Init(uint flags);
    }

    public static class SDL
    {
        public static int Init(uint flags) => SDLNative.SDL_Init(flags);
    }
}
""";

    public const string NestedInternalRawAbiVisibilitySource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    internal static class Outer
    {
        public static unsafe partial class SDLNative
        {
            [DllImport("SDL2")]
            public static extern int SDL_Init(uint flags);
        }
    }
}
""";

    public static readonly IReadOnlyList<RawAbiCheck> DuplicateFirstRawAbiChecks =
    [
        new("raw-abi-public-class", "error", "Hard Bug", "A", "duplicate", "a.cs"),
        new("raw-abi-public-class", "error", "Hard Bug", "B", "duplicate", "b.cs"),
        new("raw-abi-public-class", "error", "Hard Bug", "C", "duplicate", "c.cs"),
        new("raw-abi-public-class", "error", "Hard Bug", "D", "duplicate", "d.cs"),
        new("raw-abi-public-import", "error", "Hard Bug", "E", "varied", "e.cs"),
        new("platform-sensitive-long", "warning", "Compatibility Risk", "F", "varied", "f.cs")
    ];

    public const string ImageNamespaceDriftSource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    public static partial class SDL_imageNative
    {
        [DllImport("SDL2_image")]
        public static extern int IMG_Init(int flags);
    }
}
""";

    // Mirrors the pre-remap ClangSharp output for SDL_hidapi.h: the C tag `SDL_hid_device_`
    // is emitted as an empty partial struct, and functions reference it via the typedef
    // `SDL_hid_device` through [NativeTypeName] annotations. The adjacent `SDL_hid_device_info`
    // declaration exercises the false-positive guard (legitimately different public type).
    public const string DuplicateTagTypedefSource = """
using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_hid_device_
    {
    }

    public unsafe partial struct SDL_hid_device_info
    {
        public int interface_number;
    }

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2")]
        [return: NativeTypeName("SDL_hid_device *")]
        public static extern SDL_hid_device_* SDL_hid_open([NativeTypeName("unsigned short")] ushort vendor_id);

        [DllImport("SDL2")]
        public static extern void SDL_hid_close([NativeTypeName("SDL_hid_device *")] SDL_hid_device_* dev);

        [DllImport("SDL2")]
        public static extern SDL_hid_device_info* SDL_hid_enumerate([NativeTypeName("unsigned short")] ushort vendor_id);
    }
}
""";

    // Mirrors the post-remap canonical state: empty struct named after the public typedef,
    // every reference uses the same name, no [NativeTypeName] mismatch. Includes the
    // adjacent `SDL_hid_device_info` type to confirm the guard suppresses unrelated names.
    public const string CanonicalTagTypedefSource = """
using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_hid_device
    {
    }

    public unsafe partial struct SDL_hid_device_info
    {
        public int interface_number;
    }

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2")]
        public static extern SDL_hid_device* SDL_hid_open([NativeTypeName("unsigned short")] ushort vendor_id);

        [DllImport("SDL2")]
        public static extern void SDL_hid_close(SDL_hid_device* dev);

        [DllImport("SDL2")]
        public static extern SDL_hid_device_info* SDL_hid_enumerate([NativeTypeName("unsigned short")] ushort vendor_id);
    }
}
""";
}
