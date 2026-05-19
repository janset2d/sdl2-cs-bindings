#define wchar_t int

typedef unsigned long size_t;

typedef struct SDL_hid_device_info
{
    wchar_t* serial_number;
    wchar_t* manufacturer_string;
    wchar_t* product_string;
    struct SDL_hid_device_info* next;
} SDL_hid_device_info;

void* SDL_hid_open(unsigned short vendor_id, unsigned short product_id, const wchar_t* serial_number);
int SDL_hid_get_manufacturer_string(void* dev, wchar_t* string, size_t maxlen);
int SDL_hid_get_product_string(void* dev, wchar_t* string, size_t maxlen);
int SDL_hid_get_serial_number_string(void* dev, wchar_t* string, size_t maxlen);
int SDL_hid_get_indexed_string(void* dev, int string_index, wchar_t* string, size_t maxlen);
