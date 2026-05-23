using System;
using System.Runtime.InteropServices;

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
        public ushort* serial_number;

        [NativeTypeName("unsigned short")]
        public ushort release_number;

        [NativeTypeName("wchar_t *")]
        public ushort* manufacturer_string;

        [NativeTypeName("wchar_t *")]
        public ushort* product_string;

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
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_init();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_exit();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_hid_device_change_count();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_hid_device_info* SDL_hid_enumerate([NativeTypeName("unsigned short")] ushort vendor_id, [NativeTypeName("unsigned short")] ushort product_id);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_hid_free_enumeration(SDL_hid_device_info* devs);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_hid_device* SDL_hid_open([NativeTypeName("unsigned short")] ushort vendor_id, [NativeTypeName("unsigned short")] ushort product_id, [NativeTypeName("const wchar_t *")] ushort* serial_number);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_hid_device* SDL_hid_open_path([NativeTypeName("const char *")] byte* path, int bExclusive);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_write(SDL_hid_device* dev, [NativeTypeName("const unsigned char *")] byte* data, [NativeTypeName("size_t")] UIntPtr length);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_read_timeout(SDL_hid_device* dev, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("size_t")] UIntPtr length, int milliseconds);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_read(SDL_hid_device* dev, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("size_t")] UIntPtr length);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_set_nonblocking(SDL_hid_device* dev, int nonblock);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_send_feature_report(SDL_hid_device* dev, [NativeTypeName("const unsigned char *")] byte* data, [NativeTypeName("size_t")] UIntPtr length);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_get_feature_report(SDL_hid_device* dev, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("size_t")] UIntPtr length);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_hid_close(SDL_hid_device* dev);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_get_manufacturer_string(SDL_hid_device* dev, [NativeTypeName("wchar_t *")] ushort* @string, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_get_product_string(SDL_hid_device* dev, [NativeTypeName("wchar_t *")] ushort* @string, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_get_serial_number_string(SDL_hid_device* dev, [NativeTypeName("wchar_t *")] ushort* @string, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_hid_get_indexed_string(SDL_hid_device* dev, int string_index, [NativeTypeName("wchar_t *")] ushort* @string, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_hid_ble_scan(SDL_bool active);
    }
}
