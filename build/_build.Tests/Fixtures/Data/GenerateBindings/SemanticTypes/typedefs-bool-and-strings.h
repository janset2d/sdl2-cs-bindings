typedef enum SDL_bool
{
    SDL_FALSE = 0,
    SDL_TRUE = 1
} SDL_bool;

typedef unsigned short Uint16;
typedef Uint16 SDL_AudioFormat;

extern const char* SDL_GetError(void);
extern int SDL_SetHint(const char* name, const char* value);
extern void SDL_ClearError(void);
