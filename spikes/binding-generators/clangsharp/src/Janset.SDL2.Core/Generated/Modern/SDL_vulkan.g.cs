using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_Vulkan_LoadLibrary([NativeTypeName("const char *")] byte* path);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_Vulkan_GetVkGetInstanceProcAddr();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_Vulkan_UnloadLibrary();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_Vulkan_GetInstanceExtensions(SDL_Window window, [NativeTypeName("unsigned int *")] uint* pCount, [NativeTypeName("const char **")] byte** pNames);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_Vulkan_CreateSurface(SDL_Window window, [NativeTypeName("VkInstance")] nint instance, [NativeTypeName("VkSurfaceKHR *")] nint* surface);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_Vulkan_GetDrawableSize(SDL_Window window, int* w, int* h);
    }
}
