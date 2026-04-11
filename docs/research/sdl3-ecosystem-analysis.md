# Research: SDL3 Ecosystem Analysis

**Date**: 2026-04-11
**Context**: Planning SDL3 support for this project (Phase 5).

## SDL3 Status

SDL3 is **stable and production-ready** as of January 2025.

| Milestone | Date | Version |
|-----------|------|---------|
| First stable release | January 2025 | 3.2.0 |
| Current latest | April 2026 | **3.4.4** |

SDL2 is now in **maintenance mode** — bugfix-only releases, no new features.

## SDL3 New Features (Not in SDL2)

- **GPU API**: Cross-platform modern 3D rendering and GPU compute
- **Camera API**: Webcam/camera access
- **Dialog API**: Native file/folder dialogs
- **Filesystem/Storage APIs**: Platform-abstract file access
- **Async I/O**: Asynchronous file and network operations
- **Process API**: Process management
- **Pen API**: Stylus/pen input
- **Properties API**: Key-value property system
- **PipeWire audio**: Preferred over PulseAudio on modern Linux
- **HDR/Colorspace**: High dynamic range groundwork
- **Improved documentation**: Dramatically better than SDL2

## SDL3 vs SDL2 API Breaking Changes

SDL3 is **not backwards compatible** with SDL2. Key changes:

| Area | SDL2 | SDL3 |
|------|------|------|
| Error returns | Negative int | `bool` |
| Device enumeration | Index-based (`SDL_NumJoysticks()` + loop) | Array-based (`SDL_GetJoysticks()`) |
| File I/O | `SDL_RWops` | `SDL_IOStream` |
| Surface format | `SDL_Surface.format` is pointer | Is enum value |
| Pixel format | Single type | Split into enum + details struct |
| Audio | Callback model | Stream model (`SDL_AudioStream`) |
| Function naming | Inconsistent | Consistent conventions |
| Headers | `#include <SDL.h>` | `#include <SDL3/SDL.h>` |

**P/Invoke still works** — SDL3 is still a C library. But every binding declaration needs rewriting.

## vcpkg Support

| Port | Version | Features | Status |
|------|---------|----------|--------|
| `sdl3` | 3.4.4 | vulkan, alsa, dbus, ibus, wayland, x11 | Stable |
| `sdl3-image` | 3.4.2 | jpeg, png, tiff, webp | Stable |
| `sdl3-mixer` | 3.2.0#1 | fluidsynth, libflac, libvorbis, libxmp, mpg123, opusfile, wavpack | Stable |
| `sdl3-ttf` | 3.2.2#1 | harfbuzz, svg (PlutoSVG for color emoji!) | Stable |
| `sdl3-net` | **N/A** | — | Upstream WIP, no release |
| `sdl3-shadercross` | 3.0.0-preview2 | SPIRV/HLSL shader translation | Preview |
| `sdl2-compat` | **N/A** | SDL2 API on top of SDL3 | vcpkg PR closed (header conflicts) |

### Comparison with SDL2 Feature Availability

| Feature | SDL2 vcpkg | SDL3 vcpkg | Notes |
|---------|-----------|-----------|-------|
| vulkan | Yes | Yes | |
| alsa | Yes | Yes | |
| dbus | Yes | Yes | |
| ibus | Yes | Yes | |
| wayland | Yes | Yes | |
| x11 | Yes | Yes | |
| samplerate | Yes | — | Not a feature in SDL3 |
| harfbuzz (ttf) | Yes | Yes | |
| svg/emoji (ttf) | — | Yes | New in SDL3_ttf |
| libxmp (mixer) | — | Yes | New in SDL3_mixer |
| AVIF (image) | Yes | Disabled | Explicitly disabled in SDL3 portfile |

## C# SDL3 Binding Ecosystem

### Existing Projects

| Project | Auto-Gen Tool | NuGet | Native Binaries | Downloads |
|---------|:------------:|:-----:|:---------------:|:---------:|
| **ppy/SDL3-CS** | ClangSharp | Yes (`ppy.SDL3-CS`) | Yes (committed) | 405K |
| **edwardgushchin/SDL3-CS** | Manual | Yes (`SDL3-CS`) | Unknown | 38K |
| **flibitijibibo/SDL3-CS** | c2ffi | No | No | — |
| **Alimer.Bindings.SDL** | CppAst | Yes | Yes (committed) | 20K |
| **bottlenoselabs/SDL3-cs** | c2ffi+c2cs | Unknown | Yes (per-RID NuGet) | — |

### Key Observation

**None of these projects build natives from source via vcpkg.** They either:
- Commit pre-built binaries from SDL release tarballs
- Build via CMake in CI
- Don't ship natives at all (BYO)

This means our value proposition (vcpkg-built, reproducible, feature-flagged native NuGet packages) is unique for SDL3 as well.

## SDL2-compat

`sdl2-compat` provides SDL2 API compatibility on top of SDL3. However:
- Not in vcpkg (PR #44023 was closed due to header conflicts)
- Intended for existing SDL2 applications, not for binding projects
- We don't need it — we'll have separate SDL2 and SDL3 bindings

## Implications for Our Project

1. **SDL3 vcpkg support is solid** — all major satellites available with feature flags
2. **Our build infra reuses well** — vcpkg triplets, RID mapping, harvesting are library-agnostic
3. **Binding generation is mandatory** — SDL3 API changes make manual port infeasible
4. **SDL3_net is blocked** — no vcpkg port, no upstream release. Monitor and add when available
5. **SDL3_shadercross is interesting** — GPU shader translation could be valuable for Janset2D, but it's preview
6. **SDL2_gfx has no SDL3 equivalent** — third-party library, not maintained by libsdl-org

## Sources

- [SDL3 Release Notes](https://wiki.libsdl.org/SDL3/README-migration)
- [SDL3 on vcpkg](https://vcpkg.roundtrip.dev/ports/sdl3)
- [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS)
- [flibitijibibo/SDL3-CS](https://github.com/flibitijibibo/SDL3-CS)
- [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL)
- [sdl2-compat vcpkg PR #44023](https://github.com/microsoft/vcpkg/pull/44023)
