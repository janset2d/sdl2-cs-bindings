using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_bool
    {
        SDL_FALSE = 0,
        SDL_TRUE = 1,
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("void*")]
    public delegate nint SDL_malloc_func([NativeTypeName("size_t")] UIntPtr size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("void*")]
    public delegate nint SDL_calloc_func([NativeTypeName("size_t")] UIntPtr nmemb, [NativeTypeName("size_t")] UIntPtr size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("void*")]
    public delegate nint SDL_realloc_func([NativeTypeName("void*")] nint mem, [NativeTypeName("size_t")] UIntPtr size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SDL_free_func([NativeTypeName("void*")] nint mem);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int SDL_CompareCallback([NativeTypeName("const void *")] nint param0, [NativeTypeName("const void *")] nint param1);

    public partial struct SDL_iconv_t
    {
    }

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_malloc([NativeTypeName("size_t")] UIntPtr size);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_calloc([NativeTypeName("size_t")] UIntPtr nmemb, [NativeTypeName("size_t")] UIntPtr size);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_realloc([NativeTypeName("void*")] nint mem, [NativeTypeName("size_t")] UIntPtr size);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_free([NativeTypeName("void*")] nint mem);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_GetOriginalMemoryFunctions([NativeTypeName("SDL_malloc_func *")] IntPtr* malloc_func, [NativeTypeName("SDL_calloc_func *")] IntPtr* calloc_func, [NativeTypeName("SDL_realloc_func *")] IntPtr* realloc_func, [NativeTypeName("SDL_free_func *")] IntPtr* free_func);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_GetMemoryFunctions([NativeTypeName("SDL_malloc_func *")] IntPtr* malloc_func, [NativeTypeName("SDL_calloc_func *")] IntPtr* calloc_func, [NativeTypeName("SDL_realloc_func *")] IntPtr* realloc_func, [NativeTypeName("SDL_free_func *")] IntPtr* free_func);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SetMemoryFunctions([NativeTypeName("SDL_malloc_func")] IntPtr malloc_func, [NativeTypeName("SDL_calloc_func")] IntPtr calloc_func, [NativeTypeName("SDL_realloc_func")] IntPtr realloc_func, [NativeTypeName("SDL_free_func")] IntPtr free_func);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GetNumAllocations();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_getenv([NativeTypeName("const char *")] byte* name);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_setenv([NativeTypeName("const char *")] byte* name, [NativeTypeName("const char *")] byte* value, int overwrite);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_qsort([NativeTypeName("void*")] nint @base, [NativeTypeName("size_t")] UIntPtr nmemb, [NativeTypeName("size_t")] UIntPtr size, [NativeTypeName("SDL_CompareCallback")] IntPtr compare);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_bsearch([NativeTypeName("const void *")] nint key, [NativeTypeName("const void *")] nint @base, [NativeTypeName("size_t")] UIntPtr nmemb, [NativeTypeName("size_t")] UIntPtr size, [NativeTypeName("SDL_CompareCallback")] IntPtr compare);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_abs(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_isalpha(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_isalnum(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_isblank(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_iscntrl(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_isdigit(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_isxdigit(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_ispunct(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_isspace(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_isupper(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_islower(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_isprint(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_isgraph(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_toupper(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_tolower(int x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_crc16([NativeTypeName("Uint16")] ushort crc, [NativeTypeName("const void *")] nint data, [NativeTypeName("size_t")] UIntPtr len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_crc32([NativeTypeName("Uint32")] uint crc, [NativeTypeName("const void *")] nint data, [NativeTypeName("size_t")] UIntPtr len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_memset([NativeTypeName("void*")] nint dst, int c, [NativeTypeName("size_t")] UIntPtr len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_memcpy([NativeTypeName("void*")] nint dst, [NativeTypeName("const void *")] nint src, [NativeTypeName("size_t")] UIntPtr len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_memmove([NativeTypeName("void*")] nint dst, [NativeTypeName("const void *")] nint src, [NativeTypeName("size_t")] UIntPtr len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_memcmp([NativeTypeName("const void *")] nint s1, [NativeTypeName("const void *")] nint s2, [NativeTypeName("size_t")] UIntPtr len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_wcslen([NativeTypeName("const wchar_t *")] nint wstr);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_wcslcpy([NativeTypeName("wchar_t *")] nint dst, [NativeTypeName("const wchar_t *")] nint src, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_wcslcat([NativeTypeName("wchar_t *")] nint dst, [NativeTypeName("const wchar_t *")] nint src, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("wchar_t *")]
        public static extern nint SDL_wcsdup([NativeTypeName("const wchar_t *")] nint wstr);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("wchar_t *")]
        public static extern nint SDL_wcsstr([NativeTypeName("const wchar_t *")] nint haystack, [NativeTypeName("const wchar_t *")] nint needle);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_wcscmp([NativeTypeName("const wchar_t *")] nint str1, [NativeTypeName("const wchar_t *")] nint str2);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_wcsncmp([NativeTypeName("const wchar_t *")] nint str1, [NativeTypeName("const wchar_t *")] nint str2, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_wcscasecmp([NativeTypeName("const wchar_t *")] nint str1, [NativeTypeName("const wchar_t *")] nint str2);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_wcsncasecmp([NativeTypeName("const wchar_t *")] nint str1, [NativeTypeName("const wchar_t *")] nint str2, [NativeTypeName("size_t")] UIntPtr len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_strlen([NativeTypeName("const char *")] byte* str);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_strlcpy([NativeTypeName("char *")] byte* dst, [NativeTypeName("const char *")] byte* src, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_utf8strlcpy([NativeTypeName("char *")] byte* dst, [NativeTypeName("const char *")] byte* src, [NativeTypeName("size_t")] UIntPtr dst_bytes);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_strlcat([NativeTypeName("char *")] byte* dst, [NativeTypeName("const char *")] byte* src, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_strdup([NativeTypeName("const char *")] byte* str);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_strrev([NativeTypeName("char *")] byte* str);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_strupr([NativeTypeName("char *")] byte* str);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_strlwr([NativeTypeName("char *")] byte* str);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_strchr([NativeTypeName("const char *")] byte* str, int c);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_strrchr([NativeTypeName("const char *")] byte* str, int c);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_strstr([NativeTypeName("const char *")] byte* haystack, [NativeTypeName("const char *")] byte* needle);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_strcasestr([NativeTypeName("const char *")] byte* haystack, [NativeTypeName("const char *")] byte* needle);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_strtokr([NativeTypeName("char *")] byte* s1, [NativeTypeName("const char *")] byte* s2, [NativeTypeName("char **")] byte** saveptr);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_utf8strlen([NativeTypeName("const char *")] byte* str);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_utf8strnlen([NativeTypeName("const char *")] byte* str, [NativeTypeName("size_t")] UIntPtr bytes);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_itoa(int value, [NativeTypeName("char *")] byte* str, int radix);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_uitoa([NativeTypeName("unsigned int")] uint value, [NativeTypeName("char *")] byte* str, int radix);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_lltoa([NativeTypeName("Sint64")] long value, [NativeTypeName("char *")] byte* str, int radix);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* SDL_ulltoa([NativeTypeName("Uint64")] ulong value, [NativeTypeName("char *")] byte* str, int radix);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_atoi([NativeTypeName("const char *")] byte* str);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_atof([NativeTypeName("const char *")] byte* str);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Sint64")]
        public static extern long SDL_strtoll([NativeTypeName("const char *")] byte* str, [NativeTypeName("char **")] byte** endp, int @base);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint64")]
        public static extern ulong SDL_strtoull([NativeTypeName("const char *")] byte* str, [NativeTypeName("char **")] byte** endp, int @base);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_strtod([NativeTypeName("const char *")] byte* str, [NativeTypeName("char **")] byte** endp);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_strcmp([NativeTypeName("const char *")] byte* str1, [NativeTypeName("const char *")] byte* str2);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_strncmp([NativeTypeName("const char *")] byte* str1, [NativeTypeName("const char *")] byte* str2, [NativeTypeName("size_t")] UIntPtr maxlen);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_strcasecmp([NativeTypeName("const char *")] byte* str1, [NativeTypeName("const char *")] byte* str2);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_strncasecmp([NativeTypeName("const char *")] byte* str1, [NativeTypeName("const char *")] byte* str2, [NativeTypeName("size_t")] UIntPtr len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_sscanf([NativeTypeName("const char *")] byte* text, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_snprintf([NativeTypeName("char *")] byte* text, [NativeTypeName("size_t")] UIntPtr maxlen, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_asprintf([NativeTypeName("char **")] byte** strp, [NativeTypeName("const char *")] byte* fmt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_acos(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_acosf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_asin(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_asinf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_atan(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_atanf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_atan2(double y, double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_atan2f(float y, float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_ceil(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_ceilf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_copysign(double x, double y);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_copysignf(float x, float y);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_cos(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_cosf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_exp(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_expf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_fabs(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_fabsf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_floor(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_floorf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_trunc(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_truncf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_fmod(double x, double y);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_fmodf(float x, float y);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_log(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_logf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_log10(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_log10f(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_pow(double x, double y);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_powf(float x, float y);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_round(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_roundf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_scalbn(double x, int n);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_scalbnf(float x, int n);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_sin(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_sinf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_sqrt(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_sqrtf(float x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double SDL_tan(double x);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern float SDL_tanf(float x);

        [NativeTypeName("#define SDL_MAX_SINT8 ((Sint8)0x7F)")]
        public const sbyte SDL_MAX_SINT8 = ((sbyte)(0x7F));

        [NativeTypeName("#define SDL_MIN_SINT8 ((Sint8)(~0x7F))")]
        public const sbyte SDL_MIN_SINT8 = ((sbyte)(~0x7F));

        [NativeTypeName("#define SDL_MAX_UINT8 ((Uint8)0xFF)")]
        public const byte SDL_MAX_UINT8 = ((byte)(0xFF));

        [NativeTypeName("#define SDL_MIN_UINT8 ((Uint8)0x00)")]
        public const byte SDL_MIN_UINT8 = ((byte)(0x00));

        [NativeTypeName("#define SDL_MAX_SINT16 ((Sint16)0x7FFF)")]
        public const short SDL_MAX_SINT16 = ((short)(0x7FFF));

        [NativeTypeName("#define SDL_MIN_SINT16 ((Sint16)(~0x7FFF))")]
        public const short SDL_MIN_SINT16 = ((short)(~0x7FFF));

        [NativeTypeName("#define SDL_MAX_UINT16 ((Uint16)0xFFFF)")]
        public const ushort SDL_MAX_UINT16 = ((ushort)(0xFFFF));

        [NativeTypeName("#define SDL_MIN_UINT16 ((Uint16)0x0000)")]
        public const ushort SDL_MIN_UINT16 = ((ushort)(0x0000));

        [NativeTypeName("#define SDL_MAX_SINT32 ((Sint32)0x7FFFFFFF)")]
        public const int SDL_MAX_SINT32 = ((int)(0x7FFFFFFF));

        [NativeTypeName("#define SDL_MIN_SINT32 ((Sint32)(~0x7FFFFFFF))")]
        public const int SDL_MIN_SINT32 = ((int)(~0x7FFFFFFF));

        [NativeTypeName("#define SDL_MAX_UINT32 ((Uint32)0xFFFFFFFFu)")]
        public const uint SDL_MAX_UINT32 = ((uint)(0xFFFFFFFFU));

        [NativeTypeName("#define SDL_MIN_UINT32 ((Uint32)0x00000000)")]
        public const uint SDL_MIN_UINT32 = ((uint)(0x00000000));

        [NativeTypeName("#define SDL_MAX_SINT64 ((Sint64)0x7FFFFFFFFFFFFFFFll)")]
        public const long SDL_MAX_SINT64 = ((long)(0x7FFFFFFFFFFFFFFFL));

        [NativeTypeName("#define SDL_MIN_SINT64 ((Sint64)(~0x7FFFFFFFFFFFFFFFll))")]
        public const long SDL_MIN_SINT64 = ((long)(~0x7FFFFFFFFFFFFFFFL));

        [NativeTypeName("#define SDL_MAX_UINT64 ((Uint64)0xFFFFFFFFFFFFFFFFull)")]
        public const ulong SDL_MAX_UINT64 = ((ulong)(0xFFFFFFFFFFFFFFFFUL));

        [NativeTypeName("#define SDL_MIN_UINT64 ((Uint64)(0x0000000000000000ull))")]
        public const ulong SDL_MIN_UINT64 = ((ulong)(0x0000000000000000UL));

        [NativeTypeName("#define SDL_FLT_EPSILON 1.1920928955078125e-07F")]
        public const float SDL_FLT_EPSILON = 1.1920928955078125e-07F;

        [NativeTypeName("#define SDL_PRIs32 \"d\"")]
        public static ReadOnlySpan<byte> SDL_PRIs32 => new byte[] { 0x64, 0x00 };

        [NativeTypeName("#define SDL_PRIu32 \"u\"")]
        public static ReadOnlySpan<byte> SDL_PRIu32 => new byte[] { 0x75, 0x00 };

        [NativeTypeName("#define SDL_PRIx32 \"x\"")]
        public static ReadOnlySpan<byte> SDL_PRIx32 => new byte[] { 0x78, 0x00 };

        [NativeTypeName("#define SDL_PRIX32 \"X\"")]
        public static ReadOnlySpan<byte> SDL_PRIX32 => new byte[] { 0x58, 0x00 };

        [NativeTypeName("#define SDL_ICONV_ERROR (size_t)-1")]
        public static readonly UIntPtr SDL_ICONV_ERROR = unchecked((nuint)(-1));

        [NativeTypeName("#define SDL_ICONV_E2BIG (size_t)-2")]
        public static readonly UIntPtr SDL_ICONV_E2BIG = unchecked((nuint)(-2));

        [NativeTypeName("#define SDL_ICONV_EILSEQ (size_t)-3")]
        public static readonly UIntPtr SDL_ICONV_EILSEQ = unchecked((nuint)(-3));

        [NativeTypeName("#define SDL_ICONV_EINVAL (size_t)-4")]
        public static readonly UIntPtr SDL_ICONV_EINVAL = unchecked((nuint)(-4));
    }
}
