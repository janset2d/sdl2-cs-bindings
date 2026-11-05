using System.Runtime.InteropServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_RecordGesture([NativeTypeName("SDL_TouchID")] long touchId);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SaveAllDollarTemplates(SDL_RWops dst);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SaveDollarTemplate([NativeTypeName("SDL_GestureID")] long gestureId, SDL_RWops dst);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_LoadDollarTemplates([NativeTypeName("SDL_TouchID")] long touchId, SDL_RWops src);
    }
}
