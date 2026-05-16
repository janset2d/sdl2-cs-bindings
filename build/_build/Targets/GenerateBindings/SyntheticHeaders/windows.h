#pragma once
// Stub <windows.h>: SDL_syswm.h, SDL_egl.h, and SDL_opengl.h pull this under
// SDL_VIDEO_DRIVER_WINDOWS / __WIN32__ for HWND, HINSTANCE, HDC, HGLRC, etc.
// We never bind raw Win32 types; SDL_SysWMinfo's platform union is the only
// place those types surface and it is on Sdl2CoreGenerationConfig.DeferredDeclarations
// for Stage 2. An empty stub lets the Windows-view parse proceed.

// stddef.h is needed for wchar_t (the spike's synthetic stddef stub typedefs
// it via the libclang builtin __WCHAR_TYPE__).
#include <stddef.h>

// Forward-declare opaque handle aliases used in SDL_syswm.h / SDL_egl.h /
// SDL_opengl.h signatures so libclang doesn't fail on unknown type names. These
// resolve to void* on the binding side and never make it past the
// DeferredDeclarations filter.
typedef void* HWND;
typedef void* HINSTANCE;
typedef void* HDC;
typedef void* HGLRC;
typedef void* HMODULE;
typedef void* HMENU;
typedef void* HMONITOR;
typedef void* HICON;
typedef void* HCURSOR;
typedef void* HBITMAP;
typedef void* HBRUSH;
typedef void* HPEN;
typedef void* HFONT;
typedef void* HGDIOBJ;
typedef void* HRGN;
typedef void* HPALETTE;
typedef void* HWINEVENTHOOK;
typedef void* HRAWINPUT;
typedef unsigned int UINT;
typedef int INT;
typedef int BOOL;
typedef unsigned long DWORD;
typedef unsigned short WORD;
typedef unsigned char BYTE;
typedef long LONG;
typedef long long LONGLONG;
typedef unsigned long long ULONGLONG;
typedef long LRESULT;
typedef unsigned long long WPARAM;
typedef long long LPARAM;
typedef long HRESULT;
typedef const char* LPCSTR;
typedef const wchar_t* LPCWSTR;
typedef char* LPSTR;
typedef wchar_t* LPWSTR;
