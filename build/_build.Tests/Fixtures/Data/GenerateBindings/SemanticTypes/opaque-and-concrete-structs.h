typedef struct SDL_Window SDL_Window;

typedef struct SDL_Texture
{
    unsigned int format;
    int w;
    int h;
    int refcount;
} SDL_Texture;

typedef struct SDL_Rect
{
    int x;
    int y;
    int w;
    int h;
} SDL_Rect;
