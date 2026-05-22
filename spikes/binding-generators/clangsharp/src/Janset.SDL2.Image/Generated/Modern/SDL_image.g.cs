using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public enum IMG_InitFlags
    {
        IMG_INIT_JPG = 0x00000001,
        IMG_INIT_PNG = 0x00000002,
        IMG_INIT_TIF = 0x00000004,
        IMG_INIT_WEBP = 0x00000008,
        IMG_INIT_JXL = 0x00000010,
        IMG_INIT_AVIF = 0x00000020,
    }

    public unsafe partial struct IMG_Animation
    {
        public int w;

        public int h;

        public int count;

        public SDL_Surface** frames;

        public int* delays;
    }

    public static unsafe partial class SDL_imageNative
    {
        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const SDL_version *")]
        public static partial SDL_version* IMG_Linked_Version();

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_Init(int flags);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void IMG_Quit();

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadTyped_RW(SDL_RWops* src, int freesrc, [NativeTypeName("const char *")] byte* type);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_Load([NativeTypeName("const char *")] byte* file);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_Load_RW(SDL_RWops* src, int freesrc);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Texture* IMG_LoadTexture(SDL_Renderer* renderer, [NativeTypeName("const char *")] byte* file);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Texture* IMG_LoadTexture_RW(SDL_Renderer* renderer, SDL_RWops* src, int freesrc);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Texture* IMG_LoadTextureTyped_RW(SDL_Renderer* renderer, SDL_RWops* src, int freesrc, [NativeTypeName("const char *")] byte* type);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isAVIF(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isICO(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isCUR(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isBMP(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isGIF(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isJPG(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isJXL(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isLBM(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isPCX(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isPNG(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isPNM(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isSVG(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isQOI(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isTIF(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isXCF(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isXPM(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isXV(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_isWEBP(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadAVIF_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadICO_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadCUR_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadBMP_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadGIF_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadJPG_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadJXL_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadLBM_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadPCX_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadPNG_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadPNM_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadSVG_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadQOI_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadTGA_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadTIF_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadXCF_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadXPM_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadXV_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadWEBP_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_LoadSizedSVG_RW(SDL_RWops* src, int width, int height);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_ReadXPMFromArray([NativeTypeName("char **")] byte** xpm);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_Surface* IMG_ReadXPMFromArrayToRGB888([NativeTypeName("char **")] byte** xpm);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_SavePNG(SDL_Surface* surface, [NativeTypeName("const char *")] byte* file);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_SavePNG_RW(SDL_Surface* surface, SDL_RWops* dst, int freedst);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_SaveJPG(SDL_Surface* surface, [NativeTypeName("const char *")] byte* file, int quality);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_SaveJPG_RW(SDL_Surface* surface, SDL_RWops* dst, int freedst, int quality);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial IMG_Animation* IMG_LoadAnimation([NativeTypeName("const char *")] byte* file);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial IMG_Animation* IMG_LoadAnimation_RW(SDL_RWops* src, int freesrc);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial IMG_Animation* IMG_LoadAnimationTyped_RW(SDL_RWops* src, int freesrc, [NativeTypeName("const char *")] byte* type);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void IMG_FreeAnimation(IMG_Animation* anim);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial IMG_Animation* IMG_LoadGIFAnimation_RW(SDL_RWops* src);

        [LibraryImport("SDL2_image")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial IMG_Animation* IMG_LoadWEBPAnimation_RW(SDL_RWops* src);

        [NativeTypeName("#define SDL_IMAGE_MAJOR_VERSION 2")]
        public const int SDL_IMAGE_MAJOR_VERSION = 2;

        [NativeTypeName("#define SDL_IMAGE_MINOR_VERSION 8")]
        public const int SDL_IMAGE_MINOR_VERSION = 8;

        [NativeTypeName("#define SDL_IMAGE_PATCHLEVEL 8")]
        public const int SDL_IMAGE_PATCHLEVEL = 8;

        [NativeTypeName("#define SDL_IMAGE_COMPILEDVERSION SDL_VERSIONNUM(SDL_IMAGE_MAJOR_VERSION, SDL_IMAGE_MINOR_VERSION, SDL_IMAGE_PATCHLEVEL)")]
        public const int SDL_IMAGE_COMPILEDVERSION = ((2) * 1000 + (8) * 100 + (8));
    }
}
