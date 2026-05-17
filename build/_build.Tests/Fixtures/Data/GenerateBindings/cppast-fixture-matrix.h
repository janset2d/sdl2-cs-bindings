typedef enum SDL_bool
{
    SDL_FALSE = 0,
    SDL_TRUE = 1
} SDL_bool;

typedef struct SDL_Window SDL_Window;

typedef struct SDL_GUID
{
    unsigned char data[16];
} SDL_GUID;

typedef union SDL_GameControllerButtonBind
{
    int button;
    int axis;
} SDL_GameControllerButtonBind;

typedef struct __va_list_tag __va_list_tag;
typedef __va_list_tag* va_list;
typedef struct _IO_FILE FILE;
typedef struct VkInstance_T* VkInstance;
typedef unsigned long long VkSurfaceKHR;
typedef void* XUserHandle;
typedef void* XTaskQueueHandle;

int SDL_ReportAssertion(void*, const char*, const char*, int);
void SDL_Log(const char* fmt, ...);
void SDL_LogMessageV(int category, int priority, const char* fmt, va_list ap);
void* SDL_RWFromFP(FILE* fp, int autoclose);
SDL_Window* SDL_CreateWindow(void);
SDL_GUID SDL_GUIDFromString(const char* pchGUID);
SDL_GameControllerButtonBind SDL_GameControllerGetBindForAxis(void* gamecontroller, int axis);
int SDL_Vulkan_CreateSurface(SDL_Window* window, VkInstance instance, VkSurfaceKHR* surface);
int SDL_GDKGetDefaultUser(XUserHandle* outUserHandle);
int SDL_GDKGetTaskQueue(XTaskQueueHandle* outTaskQueue);
