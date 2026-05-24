using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_SetWindowsMessageHook([NativeTypeName("SDL_WindowsMessageHook")] delegate* unmanaged[Cdecl]<nint, nint, uint, ulong, long, void> callback, [NativeTypeName("void*")] nint userdata);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_Direct3D9GetAdapterIndex(int displayIndex);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("IDirect3DDevice9*")]
        public static partial nint SDL_RenderGetD3D9Device(SDL_Renderer* renderer);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("ID3D11Device*")]
        public static partial nint SDL_RenderGetD3D11Device(SDL_Renderer* renderer);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("ID3D12Device*")]
        public static partial nint SDL_RenderGetD3D12Device(SDL_Renderer* renderer);

        #if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
        #endif

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_DXGIGetOutputInfo(int displayIndex, int* adapterIndex, int* outputIndex);
    }
}
