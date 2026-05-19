#include <stddef.h>

typedef unsigned long SDL_threadID;

long SDL_lround(double x);
unsigned long SDL_strtoul(const char* text, char** endp, int radix);
SDL_threadID SDL_ThreadID(void);

typedef struct SDL_hid_device_info
{
    wchar_t* serial_number;
    wchar_t* manufacturer_string;
    wchar_t* product_string;
    struct SDL_hid_device_info* next;
} SDL_hid_device_info;

int SDL_hid_get_serial_number_string(void* device, wchar_t* string, size_t maxlen);
