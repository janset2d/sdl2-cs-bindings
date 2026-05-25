# SDL2_gfx Binding Generation — Header Deep-Dive Analysis

**Date:** 2026-05-25
**Target:** SDL2_gfx (installed headers from `vcpkg_installed/x64-windows-hybrid/include/SDL2/`, 4 functional headers + 1 data-only header)
**Status:** Research artifact. No code written.

---

## 1. Export Macro Analysis — The GFX-Specific Risk

SDL2_gfx is the ONLY satellite library that uses **per-header custom export macros** instead of the standard `extern DECLSPEC` pattern. Each functional header defines its own scope macro:

| Header | Scope Macro | Functions |
|---|---|---|
| `SDL2_framerate.h` | `SDL2_FRAMERATE_SCOPE` | 5 |
| `SDL2_gfxPrimitives.h` | `SDL2_GFXPRIMITIVES_SCOPE` | ~56 |
| `SDL2_imageFilter.h` | `SDL2_IMAGEFILTER_SCOPE` | ~27 |
| `SDL2_rotozoom.h` | `SDL2_ROTOZOOM_SCOPE` | ~8 |

### Preprocessor Resolution

Each header has an identical preprocessor pattern (shown for `SDL2_gfxPrimitives.h`):

```c
#ifdef _MSC_VER
#  if defined(DLL_EXPORT) && !defined(LIBSDL2_GFX_DLL_IMPORT)
#    define SDL2_GFXPRIMITIVES_SCOPE __declspec(dllexport)
#  else
#    ifdef LIBSDL2_GFX_DLL_IMPORT
#      define SDL2_GFXPRIMITIVES_SCOPE __declspec(dllimport)
#    endif
#  endif
#elif defined(__GNUC__) && __GNUC__ >= 4
#  define SDL2_GFXPRIMITIVES_SCOPE extern __attribute__((visibility("default")))
#endif
#ifndef SDL2_GFXPRIMITIVES_SCOPE
#  define SDL2_GFXPRIMITIVES_SCOPE extern
#endif
```

### Critical Assessment: Will ClangSharp Recognize These?

**Yes, with high confidence.** On our parsing host (Windows, `_MSC_VER` defined, neither `DLL_EXPORT` nor `LIBSDL2_GFX_DLL_IMPORT` defined), the `#ifndef` fallback fires: `#define SDL2_GFXPRIMITIVES_SCOPE extern`. Result: `extern SDL2_GFXPRIMITIVES_SCOPE int pixelColor(...)` becomes `extern int pixelColor(...)` — a standard C function declaration.

### Insurance Strategy

Add explicit `--define-macro` entries to the family RSP as insurance against different preprocessor environments:

```rsp
--define-macro
SDL2_GFXPRIMITIVES_SCOPE=extern
SDL2_ROTOZOOM_SCOPE=extern
SDL2_FRAMERATE_SCOPE=extern
SDL2_IMAGEFILTER_SCOPE=extern
```

---

## 2. Function Inventory

### SDL2_framerate.h — 5 functions (ALL `SDL_` prefix)

| Function | Return | Parameters |
|---|---|---|
| `SDL_initFramerate` | void | `FPSmanager* manager` |
| `SDL_setFramerate` | int | `FPSmanager* manager, Uint32 rate` |
| `SDL_getFramerate` | int | `FPSmanager* manager` |
| `SDL_getFramecount` | int | `FPSmanager* manager` |
| `SDL_framerateDelay` | Uint32 | `FPSmanager* manager` |

### SDL2_gfxPrimitives.h — ~56 functions (ALL bare names, NO `SDL_` prefix)

**Drawing primitives (each has `*Color` and `*RGBA` variant, 28 pairs = 56 functions):**

Pixel (2): `pixelColor`, `pixelRGBA`
Line (6): `hlineColor/RGBA`, `vlineColor/RGBA`, `lineColor/RGBA`, `aalineColor/RGBA`, `thickLineColor/RGBA`
Rectangle (6): `rectangleColor/RGBA`, `roundedRectangleColor/RGBA`, `boxColor/RGBA`, `roundedBoxColor/RGBA`
Circle (6): `circleColor/RGBA`, `aacircleColor/RGBA`, `filledCircleColor/RGBA`, `arcColor/RGBA`
Ellipse (6): `ellipseColor/RGBA`, `aaellipseColor/RGBA`, `filledEllipseColor/RGBA`
Pie (4): `pieColor/RGBA`, `filledPieColor/RGBA`
Trigon (6): `trigonColor/RGBA`, `aatrigonColor/RGBA`, `filledTrigonColor/RGBA`
Polygon (8): `polygonColor/RGBA`, `aapolygonColor/RGBA`, `filledPolygonColor/RGBA`, `texturedPolygon`
Bezier (2): `bezierColor/RGBA`
Font/String (4): `gfxPrimitivesSetFont`, `gfxPrimitivesSetFontRotation`, `characterColor/RGBA`, `stringColor/RGBA`

Parameter pattern: `*Color` functions take `SDL_Renderer*` + coordinates (`Sint16`) + `Uint32 color`. `*RGBA` functions take `SDL_Renderer*` + coordinates + `Uint8 r, g, b, a`.

### SDL2_imageFilter.h — ~27 functions (ALL `SDL_` prefix)

Two-input filters (11): `SDL_imageFilterAdd/Mean/Sub/AbsDiff/Mult/MultNor/MultDivby2/MultDivby4/BitAnd/BitOr/Div`
Single-input + constant (13): `SDL_imageFilterBitNegation/AddByte/AddUint/AddByteToHalf/SubByte/SubUint/ShiftRight/ShiftRightUint/MultByByte/ShiftRightAndMultByByte/ShiftLeftByte/ShiftLeftUint/ShiftLeft`
Advanced (3): `SDL_imageFilterBinarizeUsingThreshold/ClipToRange/NormalizeLinear`
MMX control (3): `SDL_imageFilterMMXdetect/MMXoff/MMXon`

All take `unsigned char*` buffers, return `int` (0 = success, -1 = error).

### SDL2_rotozoom.h — 8 functions (ALL bare names, NO `SDL_` prefix)

| Function | Return | Parameters |
|---|---|---|
| `rotozoomSurface` | `SDL_Surface*` | `SDL_Surface* src, double angle, double zoom, int smooth` |
| `rotozoomSurfaceXY` | `SDL_Surface*` | `SDL_Surface* src, double angle, double zoomx, double zoomy, int smooth` |
| `rotozoomSurfaceSize` | void | `int w, int h, double angle, double zoom, int* dstw, int* dsth` |
| `rotozoomSurfaceSizeXY` | void | `int w, int h, double angle, double zoomx, double zoomy, int* dstw, int* dsth` |
| `zoomSurface` | `SDL_Surface*` | `SDL_Surface* src, double zoomx, double zoomy, int smooth` |
| `zoomSurfaceSize` | void | `int w, int h, double zoomx, double zoomy, int* dstw, int* dsth` |
| `shrinkSurface` | `SDL_Surface*` | `SDL_Surface* src, int factorx, int factory` |
| `rotateSurface90Degrees` | `SDL_Surface*` | `SDL_Surface* src, int numClockwiseTurns` |

### SDL2_gfxPrimitives_font.h — EXCLUDED from scope
Data-only header containing `static unsigned char gfxPrimitivesFontdata[8*256]`. No public API surface. Not included in scope files.

---

## 3. Type Inventory

### FPSmanager Struct (Transparent, fully blittable)

```c
typedef struct {
    Uint32 framecount;  // offset 0,  4 bytes
    float rateticks;    // offset 4,  4 bytes
    Uint32 baseticks;   // offset 8,  4 bytes
    Uint32 lastticks;   // offset 12, 4 bytes
    Uint32 rate;        // offset 16, 4 bytes
} FPSmanager;           // Total: 20 bytes
```

All fields have known width. `float` matches C# `float`. Passed by pointer only — never by value. `[StructLayout(LayoutKind.Sequential)]`, no explicit offset needed.

Note: `FPSmanager` does NOT use the `SDL_` prefix — follows GFX's mixed naming convention.

### No Opaque Handles
GFX defines no `typedef struct X* X` patterns. The `uniform-opaque` postprocess in consumer mode is a no-op for GFX.

### No Enums, No Unions, No Callbacks
GFX uses `#define` constants for smoothing modes, not enums. No union types. No function pointer typedefs.

### No Satellite-Owned Opaque Handles
Unlike TTF (`TTF_Font`) and Mixer (`Mix_Music`), GFX has zero opaque handle types. Consumer mode works as-is.

---

## 4. ABI Risk Assessment

| Risk | Severity | Analysis |
|---|---|---|
| **Export macro recognition** | **MEDIUM** | The big one. Clean resolution via insurance `--define-macro`. |
| **Mixed naming surface** | LOW | ~56 bare-name + ~32 `SDL_`-prefixed functions. Layer 1 raw ABI: no issue. Layer 3 ergonomic concern. |
| **C `long` / `unsigned long`** | **NONE** | Exclusively fixed-width SDL typedefs or `int`. |
| **`wchar_t`** | **NONE** | No wide-character surface. |
| **Variadics** | **NONE** | No `...` functions. |
| **FILE* / va_list** | **NONE** | No stdio/stdarg types. |
| **Struct layout** | LOW | `FPSmanager` — transparent, 20 bytes, all blittable. Pointer-only passing. |
| **Callbacks** | **NONE** | No function pointer parameters. |
| **`double`** | LOW | `rotozoomSurface*` uses `double`. C# `double` matches exactly (IEEE 754). |
| **Platform-conditioned** | **NONE** | No `#ifdef` platform guards for public API shapes. |

### Verdict
**GFX is the lowest-risk satellite to add.** The export macro quirk has a clean insurance strategy. No C `long`, no callbacks, no opaque handles, no unions, no platform-conditioned code. Simpler than Image in terms of ABI risk.

---

## 5. Constants and Macros

### SDL2_framerate.h
`FPS_UPPER_LIMIT` (200), `FPS_LOWER_LIMIT` (1), `FPS_DEFAULT` (30) — simple integer literals.

### SDL2_gfxPrimitives.h
`SDL2_GFXPRIMITIVES_MAJOR` (1), `SDL2_GFXPRIMITIVES_MINOR` (0), `SDL2_GFXPRIMITIVES_MICRO` (4), `M_PI` (conditional).

### SDL2_rotozoom.h
`SMOOTHING_OFF` (0), `SMOOTHING_ON` (1), `M_PI` (conditional).

All simple integer literals — handled by ClangSharp's `--generate-macro-bindings`.

---

## 6. Include Dependencies

All GFX headers include only SDL2 core headers (`SDL.h` or specific headers like `SDL_video.h`) plus `begin_code.h`/`close_code.h`. All via `include/SDL2/`. No new include directories needed — headers are flat in the same `include/SDL2/` directory alongside core headers.

---

## 7. RSP Recommendations

### Family RSP: `rsp/sdl2-gfx.rsp`
```
--libraryPath
SDL2_gfx

--methodClassName
SDL2_gfxNative

# SDL2_gfx uses per-header export macros instead of extern DECLSPEC.
# Insurance: explicitly define each scope macro to extern.
--define-macro
SDL2_GFXPRIMITIVES_SCOPE=extern
SDL2_ROTOZOOM_SCOPE=extern
SDL2_FRAMERATE_SCOPE=extern
SDL2_IMAGEFILTER_SCOPE=extern
```

### Per-Header RSP: None needed initially
No foreign types, no platform-conditioned declarations, no excludes in the first pass.

### Scope Files

**Bootstrap:** `SDL2_framerate.h` (simplest header, 5 functions, 1 struct)
**Full:** `SDL2_framerate.h`, `SDL2_gfxPrimitives.h`, `SDL2_imageFilter.h`, `SDL2_rotozoom.h` (4 functional headers; font data header excluded)

---

## 8. Family Config

```python
"gfx": {
    "namespace": "SDL2.Gfx",
    "raw_class": "SDL2_gfxNative",
    "rsp": "sdl2-gfx.rsp",
    "bootstrap_scope": "bootstrap-sdl2-gfx.headers.txt",
    "full_scope": "sdl2-gfx.headers.txt",
    "library_dir": "Janset.SDL2.Gfx",
},
```

Consumer mode for `uniform-opaque` (automatic — no code change needed).

---

## 9. Postprocess Notes

All six postprocess steps are GFX-safe with zero changes needed:

| Step | Effect on GFX | Risk |
|---|---|---|
| `platform-delta` | No-op (no platform-sensitive headers) | None |
| `strip-varargs` | No-op (no variadic functions) | None |
| `libraryimport` | Standard Modern pass | None |
| `guid-substitute` | No-op (no `SDL_GUID` types) | None |
| `threadid-dispatch` | No-op (no `SDL_threadID` types) | None |
| `uniform-opaque` (consumer) | Removes partial struct stubs; no opaque handles to rewrite | Low |

**No new postprocess steps needed for GFX.** The existing pipeline covers it completely. GFX has no opaque handles of its own and no satellite-owned handle gap to solve.

---

## 10. `generate_bindings.py` Changes

Minimal — ~6 locations need a `"gfx"` entry added:
- `FAMILY_CONFIG` dict
- `selected_families()` return value
- `--family` choices
- `stats` dict initialization
- `write_report()` loop
- `PLATFORM_SENSITIVE_HEADERS` dict (`"gfx": []`)

No logic changes. No new postprocess modes. No include directory changes. No new rewriters.

---

## 11. Test Priorities

1. **`FPSmanager` struct layout** — verify sizeof == 20 and field offsets
2. **`SDL_initFramerate` + `SDL_setFramerate` + `SDL_framerateDelay`** — simplest GFX functions, pointer-struct interaction
3. **`pixelColor` / `pixelRGBA`** — simplest gfxPrimitives functions, `SDL_Renderer*` + Sint16 coords
4. **`SDL_imageFilterAdd`** — `unsigned char*` buffer filtering, tests `byte*` remapping
5. **`rotozoomSurface`** — `double` params + returns `SDL_Surface*` (Core type boundary)
6. **`stringColor`** — `const char*` text rendering, narrow-string marshalling
7. **`filledPolygonColor`** — `const Sint16*` pointer arrays
8. **`texturedPolygon`** — Core type + GFX primitive cross-interop

---

## 12. Open Questions

1. **Will ClangSharp definitely recognize `extern int pixelColor(...)` after scope macro is resolved?** Based on ClangSharp docs: yes. Verify with bootstrap dry-run.

2. **Does `--define-macro SDL2_GFXPRIMITIVES_SCOPE=extern` in the RSP preempt the header's own `#ifndef` correctly?** The header's `#ifndef` check will be false (already defined), so our `--define-macro` applies. Should work.

3. **Does ClangSharp correctly emit `const char*` as `byte*`?** `base.rsp` line 19 has `char=byte`. Should produce `byte*` in generated output.

4. **Does ClangSharp correctly emit `unsigned char*` as `byte*`?** In C, `unsigned char` is a single byte. C# `byte` is unsigned 8-bit. ClangSharp's built-in type mapping handles this.

5. **Should GFX be prioritized ahead of TTF and Mixer?** **Yes.** GFX is objectively the lowest-risk satellite: no C `long`, no callbacks, no opaque handles, no platform-conditioned code. The export macro quirk has a clean resolution. Do GFX first as a quick win to validate multi-satellite pipeline infrastructure before tackling TTF (C `long` risk) and Mixer (callback risk).
