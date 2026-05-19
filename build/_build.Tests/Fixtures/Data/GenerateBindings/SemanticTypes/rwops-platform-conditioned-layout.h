typedef long long Sint64;
typedef unsigned int Uint32;
typedef unsigned char Uint8;
typedef unsigned long size_t;
typedef int SDL_bool;

typedef struct SDL_RWops
{
    Sint64 (*size)(struct SDL_RWops* context);
    Sint64 (*seek)(struct SDL_RWops* context, Sint64 offset, int whence);
    size_t (*read)(struct SDL_RWops* context, void* ptr, size_t size, size_t maxnum);
    size_t (*write)(struct SDL_RWops* context, const void* ptr, size_t size, size_t num);
    int (*close)(struct SDL_RWops* context);
    Uint32 type;
    union
    {
        struct
        {
            SDL_bool append;
            void* h;
            struct
            {
                void* data;
                size_t size;
                size_t left;
            } buffer;
        } windowsio;
        struct
        {
            Uint8* base;
            Uint8* here;
            Uint8* stop;
        } mem;
    } hidden;
} SDL_RWops;

SDL_RWops* SDL_RWFromMem(void* mem, int size);
