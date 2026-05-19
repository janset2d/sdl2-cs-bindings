typedef unsigned char Uint8;
typedef unsigned short Uint16;
typedef unsigned int Uint32;

typedef void (*SDL_AudioCallback)(void* userdata, Uint8* stream, int len);

typedef struct SDL_AudioSpec
{
    int freq;
    Uint16 format;
    Uint8 channels;
    Uint8 silence;
    Uint16 samples;
    Uint32 size;
    SDL_AudioCallback callback;
    void* userdata;
} SDL_AudioSpec;

typedef struct SDL_Event
{
    Uint32 type;
    Uint8 padding[56];
} SDL_Event;

typedef struct SDL_GameControllerButtonBind
{
    int bindType;
    union
    {
        int button;
        int axis;
    } value;
} SDL_GameControllerButtonBind;
