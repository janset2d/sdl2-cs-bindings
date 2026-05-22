using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public enum SDL_bool
    {
        SDL_FALSE = 0,
        SDL_TRUE = 1,
    }

    public partial struct _SDL_iconv_t
    {
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_malloc([NativeTypeName("size_t")] nuint size);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_calloc([NativeTypeName("size_t")] nuint nmemb, [NativeTypeName("size_t")] nuint size);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_realloc([NativeTypeName("void*")] nint mem, [NativeTypeName("size_t")] nuint size);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_free([NativeTypeName("void*")] nint mem);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GetOriginalMemoryFunctions([NativeTypeName("SDL_malloc_func *")] delegate* unmanaged[Cdecl]<nuint, nint>* malloc_func, [NativeTypeName("SDL_calloc_func *")] delegate* unmanaged[Cdecl]<nuint, nuint, nint>* calloc_func, [NativeTypeName("SDL_realloc_func *")] delegate* unmanaged[Cdecl]<nint, nuint, nint>* realloc_func, [NativeTypeName("SDL_free_func *")] delegate* unmanaged[Cdecl]<nint, void>* free_func);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GetMemoryFunctions([NativeTypeName("SDL_malloc_func *")] delegate* unmanaged[Cdecl]<nuint, nint>* malloc_func, [NativeTypeName("SDL_calloc_func *")] delegate* unmanaged[Cdecl]<nuint, nuint, nint>* calloc_func, [NativeTypeName("SDL_realloc_func *")] delegate* unmanaged[Cdecl]<nint, nuint, nint>* realloc_func, [NativeTypeName("SDL_free_func *")] delegate* unmanaged[Cdecl]<nint, void>* free_func);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetMemoryFunctions([NativeTypeName("SDL_malloc_func")] delegate* unmanaged[Cdecl]<nuint, nint> malloc_func, [NativeTypeName("SDL_calloc_func")] delegate* unmanaged[Cdecl]<nuint, nuint, nint> calloc_func, [NativeTypeName("SDL_realloc_func")] delegate* unmanaged[Cdecl]<nint, nuint, nint> realloc_func, [NativeTypeName("SDL_free_func")] delegate* unmanaged[Cdecl]<nint, void> free_func);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetNumAllocations();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_getenv([NativeTypeName("const char *")] byte* name);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_setenv([NativeTypeName("const char *")] byte* name, [NativeTypeName("const char *")] byte* value, int overwrite);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_qsort([NativeTypeName("void*")] nint @base, [NativeTypeName("size_t")] nuint nmemb, [NativeTypeName("size_t")] nuint size, [NativeTypeName("SDL_CompareCallback")] delegate* unmanaged[Cdecl]<nint, nint, int> compare);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_bsearch([NativeTypeName("const void *")] nint key, [NativeTypeName("const void *")] nint @base, [NativeTypeName("size_t")] nuint nmemb, [NativeTypeName("size_t")] nuint size, [NativeTypeName("SDL_CompareCallback")] delegate* unmanaged[Cdecl]<nint, nint, int> compare);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_abs(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_isalpha(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_isalnum(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_isblank(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_iscntrl(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_isdigit(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_isxdigit(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_ispunct(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_isspace(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_isupper(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_islower(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_isprint(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_isgraph(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_toupper(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_tolower(int x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint16")]
        public static partial ushort SDL_crc16([NativeTypeName("Uint16")] ushort crc, [NativeTypeName("const void *")] nint data, [NativeTypeName("size_t")] nuint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_crc32([NativeTypeName("Uint32")] uint crc, [NativeTypeName("const void *")] nint data, [NativeTypeName("size_t")] nuint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_memset([NativeTypeName("void*")] nint dst, int c, [NativeTypeName("size_t")] nuint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_memcpy([NativeTypeName("void*")] nint dst, [NativeTypeName("const void *")] nint src, [NativeTypeName("size_t")] nuint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_memmove([NativeTypeName("void*")] nint dst, [NativeTypeName("const void *")] nint src, [NativeTypeName("size_t")] nuint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_memcmp([NativeTypeName("const void *")] nint s1, [NativeTypeName("const void *")] nint s2, [NativeTypeName("size_t")] nuint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_wcslen([NativeTypeName("const wchar_t *")] ushort* wstr);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_wcslcpy([NativeTypeName("wchar_t *")] ushort* dst, [NativeTypeName("const wchar_t *")] ushort* src, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_wcslcat([NativeTypeName("wchar_t *")] ushort* dst, [NativeTypeName("const wchar_t *")] ushort* src, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("wchar_t *")]
        public static partial ushort* SDL_wcsdup([NativeTypeName("const wchar_t *")] ushort* wstr);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("wchar_t *")]
        public static partial ushort* SDL_wcsstr([NativeTypeName("const wchar_t *")] ushort* haystack, [NativeTypeName("const wchar_t *")] ushort* needle);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_wcscmp([NativeTypeName("const wchar_t *")] ushort* str1, [NativeTypeName("const wchar_t *")] ushort* str2);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_wcsncmp([NativeTypeName("const wchar_t *")] ushort* str1, [NativeTypeName("const wchar_t *")] ushort* str2, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_wcscasecmp([NativeTypeName("const wchar_t *")] ushort* str1, [NativeTypeName("const wchar_t *")] ushort* str2);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_wcsncasecmp([NativeTypeName("const wchar_t *")] ushort* str1, [NativeTypeName("const wchar_t *")] ushort* str2, [NativeTypeName("size_t")] nuint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_strlen([NativeTypeName("const char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_strlcpy([NativeTypeName("char *")] byte* dst, [NativeTypeName("const char *")] byte* src, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_utf8strlcpy([NativeTypeName("char *")] byte* dst, [NativeTypeName("const char *")] byte* src, [NativeTypeName("size_t")] nuint dst_bytes);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_strlcat([NativeTypeName("char *")] byte* dst, [NativeTypeName("const char *")] byte* src, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_strdup([NativeTypeName("const char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_strrev([NativeTypeName("char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_strupr([NativeTypeName("char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_strlwr([NativeTypeName("char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_strchr([NativeTypeName("const char *")] byte* str, int c);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_strrchr([NativeTypeName("const char *")] byte* str, int c);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_strstr([NativeTypeName("const char *")] byte* haystack, [NativeTypeName("const char *")] byte* needle);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_strcasestr([NativeTypeName("const char *")] byte* haystack, [NativeTypeName("const char *")] byte* needle);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_strtokr([NativeTypeName("char *")] byte* s1, [NativeTypeName("const char *")] byte* s2, [NativeTypeName("char **")] byte** saveptr);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_utf8strlen([NativeTypeName("const char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_utf8strnlen([NativeTypeName("const char *")] byte* str, [NativeTypeName("size_t")] nuint bytes);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_itoa(int value, [NativeTypeName("char *")] byte* str, int radix);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_uitoa([NativeTypeName("unsigned int")] uint value, [NativeTypeName("char *")] byte* str, int radix);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_ltoa([NativeTypeName("long")] int value, [NativeTypeName("char *")] byte* str, int radix);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_ultoa([NativeTypeName("unsigned long")] uint value, [NativeTypeName("char *")] byte* str, int radix);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_lltoa([NativeTypeName("Sint64")] long value, [NativeTypeName("char *")] byte* str, int radix);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_ulltoa([NativeTypeName("Uint64")] ulong value, [NativeTypeName("char *")] byte* str, int radix);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_atoi([NativeTypeName("const char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_atof([NativeTypeName("const char *")] byte* str);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial int SDL_strtol([NativeTypeName("const char *")] byte* str, [NativeTypeName("char **")] byte** endp, int @base);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("unsigned long")]
        public static partial uint SDL_strtoul([NativeTypeName("const char *")] byte* str, [NativeTypeName("char **")] byte** endp, int @base);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Sint64")]
        public static partial long SDL_strtoll([NativeTypeName("const char *")] byte* str, [NativeTypeName("char **")] byte** endp, int @base);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint64")]
        public static partial ulong SDL_strtoull([NativeTypeName("const char *")] byte* str, [NativeTypeName("char **")] byte** endp, int @base);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_strtod([NativeTypeName("const char *")] byte* str, [NativeTypeName("char **")] byte** endp);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_strcmp([NativeTypeName("const char *")] byte* str1, [NativeTypeName("const char *")] byte* str2);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_strncmp([NativeTypeName("const char *")] byte* str1, [NativeTypeName("const char *")] byte* str2, [NativeTypeName("size_t")] nuint maxlen);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_strcasecmp([NativeTypeName("const char *")] byte* str1, [NativeTypeName("const char *")] byte* str2);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_strncasecmp([NativeTypeName("const char *")] byte* str1, [NativeTypeName("const char *")] byte* str2, [NativeTypeName("size_t")] nuint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_sscanf([NativeTypeName("const char *")] byte* text, [NativeTypeName("const char *")] byte* fmt);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_snprintf([NativeTypeName("char *")] byte* text, [NativeTypeName("size_t")] nuint maxlen, [NativeTypeName("const char *")] byte* fmt);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_asprintf([NativeTypeName("char **")] byte** strp, [NativeTypeName("const char *")] byte* fmt);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_acos(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_acosf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_asin(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_asinf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_atan(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_atanf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_atan2(double y, double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_atan2f(float y, float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_ceil(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_ceilf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_copysign(double x, double y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_copysignf(float x, float y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_cos(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_cosf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_exp(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_expf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_fabs(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_fabsf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_floor(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_floorf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_trunc(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_truncf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_fmod(double x, double y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_fmodf(float x, float y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_log(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_logf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_log10(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_log10f(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_pow(double x, double y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_powf(float x, float y);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_round(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_roundf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial int SDL_lround(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("long")]
        public static partial int SDL_lroundf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_scalbn(double x, int n);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_scalbnf(float x, int n);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_sin(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_sinf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_sqrt(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_sqrtf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double SDL_tan(double x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial float SDL_tanf(float x);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_iconv_t")]
        public static partial _SDL_iconv_t* SDL_iconv_open([NativeTypeName("const char *")] byte* tocode, [NativeTypeName("const char *")] byte* fromcode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_iconv_close([NativeTypeName("SDL_iconv_t")] _SDL_iconv_t* cd);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_iconv([NativeTypeName("SDL_iconv_t")] _SDL_iconv_t* cd, [NativeTypeName("const char **")] byte** inbuf, [NativeTypeName("size_t *")] nuint* inbytesleft, [NativeTypeName("char **")] byte** outbuf, [NativeTypeName("size_t *")] nuint* outbytesleft);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("char *")]
        public static partial byte* SDL_iconv_string([NativeTypeName("const char *")] byte* tocode, [NativeTypeName("const char *")] byte* fromcode, [NativeTypeName("const char *")] byte* inbuf, [NativeTypeName("size_t")] nuint inbytesleft);

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
        public static ReadOnlySpan<byte> SDL_PRIs32 => "d"u8;

        [NativeTypeName("#define SDL_PRIu32 \"u\"")]
        public static ReadOnlySpan<byte> SDL_PRIu32 => "u"u8;

        [NativeTypeName("#define SDL_PRIx32 \"x\"")]
        public static ReadOnlySpan<byte> SDL_PRIx32 => "x"u8;

        [NativeTypeName("#define SDL_PRIX32 \"X\"")]
        public static ReadOnlySpan<byte> SDL_PRIX32 => "X"u8;

        [NativeTypeName("#define SDL_ICONV_ERROR (size_t)-1")]
        public static readonly nuint SDL_ICONV_ERROR = unchecked((nuint)(-1));

        [NativeTypeName("#define SDL_ICONV_E2BIG (size_t)-2")]
        public static readonly nuint SDL_ICONV_E2BIG = unchecked((nuint)(-2));

        [NativeTypeName("#define SDL_ICONV_EILSEQ (size_t)-3")]
        public static readonly nuint SDL_ICONV_EILSEQ = unchecked((nuint)(-3));

        [NativeTypeName("#define SDL_ICONV_EINVAL (size_t)-4")]
        public static readonly nuint SDL_ICONV_EINVAL = unchecked((nuint)(-4));
    }
}
