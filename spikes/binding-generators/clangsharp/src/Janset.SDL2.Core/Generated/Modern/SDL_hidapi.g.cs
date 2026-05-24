using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public partial struct SDL_hid_device
    {
    }

    public unsafe partial struct SDL_hid_device_info
    {
        [NativeTypeName("char *")]
        public byte* path;

        [NativeTypeName("unsigned short")]
        public ushort vendor_id;

        [NativeTypeName("unsigned short")]
        public ushort product_id;

        [NativeTypeName("wchar_t *")]
        public nint serial_number;

        [NativeTypeName("unsigned short")]
        public ushort release_number;

        [NativeTypeName("wchar_t *")]
        public nint manufacturer_string;

        [NativeTypeName("wchar_t *")]
        public nint product_string;

        [NativeTypeName("unsigned short")]
        public ushort usage_page;

        [NativeTypeName("unsigned short")]
        public ushort usage;

        public int interface_number;

        public int interface_class;

        public int interface_subclass;

        public int interface_protocol;

        [NativeTypeName("struct SDL_hid_device_info *")]
        public SDL_hid_device_info* next;
    }

    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_init();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_exit();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_hid_device_change_count();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_hid_device_info* SDL_hid_enumerate([NativeTypeName("unsigned short")] ushort vendor_id, [NativeTypeName("unsigned short")] ushort product_id);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_hid_free_enumeration(SDL_hid_device_info* devs);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_hid_device* SDL_hid_open([NativeTypeName("unsigned short")] ushort vendor_id, [NativeTypeName("unsigned short")] ushort product_id, [NativeTypeName("const wchar_t *")] nint serial_number);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_hid_device* SDL_hid_open_path([NativeTypeName("const char *")] byte* path, int bExclusive);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_write(SDL_hid_device* dev, [NativeTypeName("const unsigned char *")] byte* data, [NativeTypeName("size_t")] nuint length);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_read_timeout(SDL_hid_device* dev, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("size_t")] nuint length, int milliseconds);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_read(SDL_hid_device* dev, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("size_t")] nuint length);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_set_nonblocking(SDL_hid_device* dev, int nonblock);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_send_feature_report(SDL_hid_device* dev, [NativeTypeName("const unsigned char *")] byte* data, [NativeTypeName("size_t")] nuint length);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_get_feature_report(SDL_hid_device* dev, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("size_t")] nuint length);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_hid_close(SDL_hid_device* dev);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_get_manufacturer_string(SDL_hid_device* dev, [NativeTypeName("wchar_t *")] nint @string, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_get_product_string(SDL_hid_device* dev, [NativeTypeName("wchar_t *")] nint @string, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_get_serial_number_string(SDL_hid_device* dev, [NativeTypeName("wchar_t *")] nint @string, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_hid_get_indexed_string(SDL_hid_device* dev, int string_index, [NativeTypeName("wchar_t *")] nint @string, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_hid_ble_scan(SDL_bool active);
    }
}
