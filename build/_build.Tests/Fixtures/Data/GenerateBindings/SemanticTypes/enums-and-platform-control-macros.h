#define SDL_WINAPI_FAMILY_PHONE 2

typedef enum SDL_bool
{
    SDL_FALSE = 0,
    SDL_TRUE = 1
} SDL_bool;

typedef enum SDL_Keymod
{
    KMOD_NONE = 0x0000,
    KMOD_LSHIFT = 0x0001,
    KMOD_RSHIFT = 0x0002,
    KMOD_SHIFT = (KMOD_LSHIFT | KMOD_RSHIFT)
} SDL_Keymod;

typedef enum SDL_GLcontextFlag
{
    SDL_GL_CONTEXT_DEBUG_FLAG = 0x0001,
    SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG = 0x0002
} SDL_GLcontextFlag;

typedef enum SDL_RendererFlip
{
    SDL_FLIP_NONE = 0x00000000,
    SDL_FLIP_HORIZONTAL = 0x00000001,
    SDL_FLIP_VERTICAL = 0x00000002
} SDL_RendererFlip;
