#pragma once
// Stub <Inspectable.h>: SDL_syswm.h:58 `#include <Inspectable.h>` under
// SDL_VIDEO_DRIVER_WINRT, then declares `IInspectable * window;` in the WinRT
// branch of its platform union. The real header is part of the Windows SDK
// (WinRT C++/CX) and isn't available on Linux; SDL_syswm.h doesn't forward-declare
// IInspectable itself (unlike NSWindow / UIWindow / ANativeWindow which SDL
// declares inline). So we provide an opaque forward declaration here. SDL only
// uses it as a pointer member, never sizeof / dereferenced.
//
// SDL_SysWMinfo as a whole is on Sdl2CoreGenerationConfig.DeferredDeclarations
// for Stage 2 — this typedef just keeps the parser happy.
typedef struct _IInspectable IInspectable;
