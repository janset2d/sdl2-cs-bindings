# Symbol Visibility in Hybrid Static Packaging — Analysis & Strategy

**Date:** 2026-04-14
**Status:** Research complete, decisions locked
**Related:** [packaging-strategy-hybrid-static-2026-04-13.md](packaging-strategy-hybrid-static-2026-04-13.md), [#83](https://github.com/janset2d/sdl2-cs-bindings/issues/83)

## The Problem

When transitive dependencies (zlib, libpng, FreeType, etc.) are statically baked into satellite shared libraries (SDL2_image.dll/.so/.dylib), their symbols may "leak" — becoming visible exports from the satellite. If two satellites both export the same symbol (e.g., `deflate` from zlib), a conflict can occur at runtime.

## Measured Symbol Leak by Platform (win-x64, osx-x64 — 2026-04-14)

### Windows (win-x64) — dumpbin /dependents

| Satellite | External deps | Transitive symbol leak |
| --- | --- | --- |
| SDL2_image.dll | SDL2.dll + system | **Zero** — PE format is export-opt-in |
| SDL2_mixer.dll | SDL2.dll + WINMM.dll + system | **Zero** |
| SDL2_ttf.dll | SDL2.dll + system | **Zero** |

**Windows is inherently safe.** DLL symbols are only exported via explicit `__declspec(dllexport)`. Static archive code baked into a DLL does not export unless explicitly decorated.

### macOS (osx-x64) — nm -gU + otool -L

| Satellite | otool deps | Leaked symbol count | Leaked prefixes |
| --- | --- | --- | --- |
| SDL2_image.dylib | SDL2 + libSystem | 46 | `WebP*`, `VP8*` (libwebp) |
| SDL2_mixer.dylib | SDL2 + AudioToolbox + system | 91 | `op_*` (opusfile) |
| SDL2_ttf.dylib | SDL2 + libSystem | 177 | `FT_*` (175, FreeType), `TT_*` (2, FreeType internals) |

**macOS leaks are present but HARMLESS.** macOS uses **two-level namespaces** (default since macOS 10.1): each dylib's internal symbol references are bound at link time to the specific dylib that provides them. Even if both SDL2_image.dylib and SDL2_ttf.dylib export `deflate`, each one calls its own copy. There is no flat symbol namespace conflict.

### Linux (linux-x64) — To be validated

Expected behavior: similar leaks to macOS. Linux uses ELF flat global scope by default, making leaks a theoretical conflict risk. Version scripts (`--version-script`) are the industry-standard fix.

## Why -fvisibility=hidden Doesn't Fully Work

Our hybrid triplets set `-fvisibility=hidden` in `VCPKG_C_FLAGS`. This is a **compiler flag** that sets the default visibility for new symbols being compiled. However:

1. Libraries like FreeType explicitly override this in their headers:
   ```c
   // freetype/config/public-macros.h
   #define FT_EXPORT(x)  extern __attribute__((visibility("default"))) x
   ```
2. `-fvisibility=hidden` only affects code compiled with that flag. When a pre-compiled `.a` archive (containing `.o` objects compiled with default visibility) is linked into a `.so`, the visibility markings in the `.o` files are preserved.
3. **Compiler flags cannot override source-level visibility annotations.** Only the linker can.

`-fvisibility=hidden` is still valuable — it hides symbols from libraries that DON'T use explicit visibility attributes (like zlib, bzip2, lzma). It reduced our leak from potentially thousands of symbols to hundreds.

## How SDL3 Solved This (SDL2 Did Not)

**SDL2 satellites never had this problem** because they were designed for a world of system-installed shared libraries. There was no static baking, so no symbol leakage concern.

**SDL3 redesigned the build system** specifically for vendored/static-baked builds:

| Mechanism | SDL3 | SDL2 |
| --- | --- | --- |
| `C_VISIBILITY_PRESET hidden` | Yes (all satellites) | Yes (but insufficient alone) |
| Version scripts (Linux `.sym`) | Yes — `local: *;` catches everything | **No** |
| Export lists (macOS `.exports`) | Yes — explicit whitelist | **No** |
| `VENDORED=ON` + `DEPS_SHARED=OFF` | First-class supported | Partially supported |

ppy/SDL3-CS benefits from this — they pass `VENDORED=ON` and SDL3's own build system handles all visibility. **Zero extra work.**

For SDL2, we're on our own. The upstream doesn't provide version scripts.

## Platform Safety Analysis

| Platform | Symbol isolation mechanism | Leak risk | Action needed |
| --- | --- | --- | --- |
| **Windows** | PE export-opt-in (DLL isolation) | **None** | None — inherently safe |
| **macOS** | Two-level namespaces (since 10.1) | **None** | None — OS handles it |
| **Linux** | ELF global scope (flat namespace) | **Low** (same vcpkg baseline = identical copies) | Version scripts recommended (Phase 2b) |

### Why Linux is "low risk" not "no risk"

ELF's default behavior: when `dlopen` loads a `.so` without `RTLD_LOCAL`, its exported symbols enter the global scope. If two `.so` files export the same symbol, the **first one loaded wins** (POSIX binding order).

**In our case:** both SDL2_image.so and SDL2_ttf.so bake zlib from the same vcpkg baseline. The zlib code is bit-identical. Even if the linker resolves `deflate` from the "wrong" copy, the code is the same. No corruption.

**When it could become a real problem:**
- Consumer loads another native library that also exports `deflate` (different zlib version)
- We accidentally build satellites from different vcpkg baselines with different dep versions
- A dep is compiled with different `#define` options across satellites

All of these are unlikely in our controlled build environment.

## The Fix: Linux Version Scripts (Phase 2b)

The industry-standard solution is a linker version script per satellite:

```
# vcpkg-overlay-triplets/version-scripts/libSDL2_image.map
libSDL2_image {
    global: IMG_*;
    local: *;
};
```

Applied via `VCPKG_LINKER_FLAGS="-Wl,--version-script=<path>/libSDL2_image.map"` in the Linux hybrid triplet, scoped per-port.

**SkiaSharp uses this exact approach.** Their `libSkiaSharp.map` exports only `sk_*`, `gr_*`, `skottie_*` patterns. Everything else is `local: *`.

**Cost:** ~8 lines per satellite, written once, maintained only if API prefixes change (never for SDL).

**Not needed for macOS:** two-level namespaces handle it. Not needed for Windows: PE handles it.

## Decision

| Decision | Detail |
| --- | --- |
| Windows | No action. PE isolation. |
| macOS | No action. Two-level namespaces. Accept cosmetic leaks. |
| Linux | Add version scripts in Phase 2b. Accept leaks in Phase 2a spike. |
| SDL3 migration | Version scripts come free from upstream. Zero work on our side. |

## Validation Commands — Per Platform

### Windows
```powershell
# Check satellite has no transitive DLL deps:
dumpbin /dependents SDL2_image.dll
# Expected: only SDL2.dll + system DLLs
```

### macOS
```bash
# Check satellite has no transitive dylib deps:
otool -L libSDL2_image.dylib
# Expected: only @rpath/libSDL2-*.dylib + /usr/lib/libSystem.B.dylib + frameworks

# Check symbol visibility (informational — leaks are harmless on macOS):
nm -gU libSDL2_image-*.dylib | grep -c deflate  # should be 0 (zlib hidden)
nm -gU libSDL2_ttf-*.dylib | grep -c FT_         # will be >0 (FreeType leaks, harmless)
```

### Linux
```bash
# Check satellite has no transitive .so deps:
ldd libSDL2_image.so
# Expected: only libSDL2-*.so + libc/libm/libdl/libpthread/ld-linux

# Check symbol visibility:
nm -D libSDL2_image.so | grep -c deflate  # should be 0 after version scripts
nm -D libSDL2_image.so | grep ' T ' | grep -v 'IMG_' | wc -l  # non-API leaks

# Validate version script is applied:
readelf -d libSDL2_image.so | grep VERNEED  # should show version reference
```

## References

- [SkiaSharp libSkiaSharp.map](https://github.com/mono/SkiaSharp/blob/main/native/linux/libSkiaSharp/libSkiaSharp.map) — glob-pattern version script
- [SDL3_image SDL_image.sym](https://github.com/libsdl-org/SDL_image/blob/main/src/SDL_image.sym) — per-function version script
- [SDL3_image SDL_image.exports](https://github.com/libsdl-org/SDL_image/blob/main/src/SDL_image.exports) — macOS export list
- [FreeType FT_EXPORT macro](https://github.com/freetype/freetype/blob/master/include/freetype/config/public-macros.h) — explicit `visibility("default")`
- [ELF Symbol Visibility](https://gcc.gnu.org/wiki/Visibility) — GCC documentation on visibility attributes
- [Apple Two-Level Namespaces](https://developer.apple.com/library/archive/documentation/DeveloperTools/Conceptual/MachOTopics/1-Articles/executing_files.html) — why macOS is safe
