using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct VkInstance_T
    {
    }

    public partial struct VkSurfaceKHR_T
    {
    }

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_Vulkan_LoadLibrary([NativeTypeName("const char *")] byte* path);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_Vulkan_GetVkGetInstanceProcAddr();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_Vulkan_UnloadLibrary();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_Vulkan_GetInstanceExtensions(SDL_Window* window, [NativeTypeName("unsigned int *")] uint* pCount, [NativeTypeName("const char **")] byte** pNames);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_Vulkan_CreateSurface(SDL_Window* window, [NativeTypeName("VkInstance")] VkInstance_T* instance, [NativeTypeName("VkSurfaceKHR *")] VkSurfaceKHR_T** surface);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_Vulkan_GetDrawableSize(SDL_Window* window, int* w, int* h);
    }
}
