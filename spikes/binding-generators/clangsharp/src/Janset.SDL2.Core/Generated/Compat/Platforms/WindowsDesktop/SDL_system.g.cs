using System;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SDL_WindowsMessageHook([NativeTypeName("void*")] nint userdata, [NativeTypeName("void*")] nint hWnd, [NativeTypeName("unsigned int")] uint message, [NativeTypeName("Uint64")] ulong wParam, [NativeTypeName("Sint64")] long lParam);

    public partial struct IDirect3DDevice9
    {
    }

    public partial struct ID3D11Device
    {
    }

    public partial struct ID3D12Device
    {
    }

    public static unsafe partial class SDLNative
    {
        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_SetWindowsMessageHook([NativeTypeName("SDL_WindowsMessageHook")] IntPtr callback, [NativeTypeName("void*")] nint userdata);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_Direct3D9GetAdapterIndex(int displayIndex);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern IDirect3DDevice9* SDL_RenderGetD3D9Device(SDL_Renderer* renderer);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern ID3D11Device* SDL_RenderGetD3D11Device(SDL_Renderer* renderer);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern ID3D12Device* SDL_RenderGetD3D12Device(SDL_Renderer* renderer);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_DXGIGetOutputInfo(int displayIndex, int* adapterIndex, int* outputIndex);
    }
}
