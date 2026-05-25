# SDL2_ttf Binding Generation — Header Deep-Dive Analysis

**Date:** 2026-05-25
**Target:** SDL2_ttf v2.24.0 (installed header from `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_ttf.h`, 2343 lines)
**Status:** Research artifact. No code written.

---

## 1. Function Inventory

**Total: 87 public function declarations** (each `extern DECLSPEC ... SDLCALL` occurrence).

### Lifecycle (3)

| Function | Returns | Parameters |
|---|---|---|
| `TTF_Init` | `int` | `(void)` |
| `TTF_Quit` | `void` | `(void)` |
| `TTF_WasInit` | `int` | `(void)` |

### Version / Library Info (3)

| Function | Returns | Parameters |
|---|---|---|
| `TTF_Linked_Version` | `const SDL_version *` | `(void)` |
| `TTF_GetFreeTypeVersion` | `void` | `(int *major, int *minor, int *patch)` |
| `TTF_GetHarfBuzzVersion` | `void` | `(int *major, int *minor, int *patch)` |

### Font Opening (8) — 4 with C `long`

| Function | Returns | C `long` risk |
|---|---|---|
| `TTF_OpenFont` | `TTF_Font *` | No — `(const char *file, int ptsize)` |
| `TTF_OpenFontIndex` | `TTF_Font *` | **YES** — `long index` param |
| `TTF_OpenFontRW` | `TTF_Font *` | No |
| `TTF_OpenFontIndexRW` | `TTF_Font *` | **YES** — `long index` param |
| `TTF_OpenFontDPI` | `TTF_Font *` | No |
| `TTF_OpenFontIndexDPI` | `TTF_Font *` | **YES** — `long index` param |
| `TTF_OpenFontDPIRW` | `TTF_Font *` | No |
| `TTF_OpenFontIndexDPIRW` | `TTF_Font *` | **YES** — `long index` param |

### Font Property Getters / Setters (18)

All take `TTF_Font *` as the first parameter. Standard int/void returns, no ABI risks.

### Font Metrics (6) — 1 with C `long` return

| Function | Returns | C `long` risk |
|---|---|---|
| `TTF_FontHeight` | `int` | No |
| `TTF_FontAscent` | `int` | No |
| `TTF_FontDescent` | `int` | No |
| `TTF_FontLineSkip` | `int` | No |
| `TTF_SetFontLineSkip` | `void` | No |
| `TTF_FontFaces` | **`long`** | **YES — C `long` return type** |

### Glyph Inspection (4)

| Function | Returns |
|---|---|
| `TTF_GlyphIsProvided` | `int` |
| `TTF_GlyphIsProvided32` | `int` |
| `TTF_GlyphMetrics` | `int` |
| `TTF_GlyphMetrics32` | `int` |

### Text Measurement (6)

`TTF_SizeText`, `TTF_SizeUTF8`, `TTF_SizeUNICODE`, `TTF_MeasureText`, `TTF_MeasureUTF8`, `TTF_MeasureUNICODE`

### Rendering — Solid (8)

`TTF_RenderText_Solid`, `TTF_RenderUTF8_Solid`, `TTF_RenderUNICODE_Solid`, and `_Wrapped` variants, plus `TTF_RenderGlyph_Solid` / `TTF_RenderGlyph32_Solid`

### Rendering — Shaded (8)

Same surface as Solid but with `_Shaded` suffix and `SDL_Color bg` parameter.

### Rendering — Blended (8)

Same surface as Solid but with `_Blended` suffix (ARGB alpha blending).

### Rendering — LCD Subpixel (8)

Same surface as Solid but with `_LCD` suffix.

### Kerning (3)

Note: `TTF_GetFontKerningSize` is DEPRECATED — uses raw FreeType font indices, missing `SDLCALL`.

### Text Shaping / HarfBuzz (4)

`TTF_SetDirection` (DEPRECATED), `TTF_SetScript` (DEPRECATED), `TTF_SetFontDirection`, `TTF_SetFontScriptName`

### Miscellaneous (2)

`TTF_ByteSwappedUNICODE`, `TTF_CloseFont`

### Summary of flagged risks

| Risk | Count | Functions |
|---|---|---|
| C `long` parameter | 4 | `TTF_OpenFontIndex`, `TTF_OpenFontIndexRW`, `TTF_OpenFontIndexDPI`, `TTF_OpenFontIndexDPIRW` |
| C `long` return | 1 | `TTF_FontFaces` |
| Deprecated (exclude candidates) | 3 | `TTF_GetFontKerningSize`, `TTF_SetDirection`, `TTF_SetScript` |
| Macro-wrapped cross-family (exclude) | 2 | `TTF_SetError`, `TTF_GetError` |
| `wchar_t` / `wchar_t*` | **0** | None |
| Variadic (`...`) | **0** | None |
| `FILE*` | **0** | None |

### C `long` Detailed Breakdown

All 5 C `long` surface items use `long` (signed). They follow the same Slice C-A pattern designed for `SDL_threadID`:

| Function | Position | Strategy |
|---|---|---|
| `TTF_OpenFontIndex` | `long index` param | CLong param on Modern, dual-DllImport dispatch on Compat |
| `TTF_OpenFontIndexRW` | `long index` param | Same |
| `TTF_OpenFontIndexDPI` | `long index` param | Same |
| `TTF_OpenFontIndexDPIRW` | `long index` param | Same |
| `TTF_FontFaces` | `long` return | CLong return on Modern, dual-DllImport on Compat |

The Constitution L264 already calls this out explicitly.

---

## 2. Type Inventory

### Opaque Handles

| Type | Declaration | Pattern | Owner |
|---|---|---|---|
| `TTF_Font` | `typedef struct TTF_Font TTF_Font;` | **Pattern B** | `SDL2.Ttf` (this family is the owner) |

`TTF_Font` is satellite-owned — NOT in Core's `opaque-handle-roster.json`. The `uniform-opaque` postprocess needs owner mode for TTF. Core handles consumed via `ProjectReference`: `SDL_RWops` (4 functions use it), `SDL_Surface` (all 24 rendering functions return it).

### Transparent Structs

**None.** No structs with exposed fields.

### Enums

| Enum | Backing | Members |
|---|---|---|
| `TTF_Direction` | `int` (default) | `TTF_DIRECTION_LTR`, `RTL`, `TTB`, `BTT` |

Only one enum. Not `[Flags]`.

### Callback / Function Pointer Types

**None.** No callback typedefs.

### Foreign Types

**None.** No Vulkan, Win32, X11, or other foreign boundary types.

---

## 3. ABI Risk Assessment

### Risk 1: C `long` (HIGH — 5 functions)

**Impact:** All 7 RIDs. Reuses existing Slice C-A hybrid CLong/dual-dispatch pattern.
**Mitigation:** Extend `threadid-dispatch` postprocess to `clong-dispatch` covering both Core's `SDL_threadID` family and TTF's 5 `long` functions.

### Risk 2: Deprecated Functions (LOW)

3 functions deprecated in SDL_ttf 2.24.0. Two miss `SDLCALL` (potential ABI mismatch on Windows x86).
**Mitigation:** `--exclude` all 3 in family RSP.

### Risk 3: Cross-family Macro Aliases (LOW)

`TTF_SetError`/`TTF_GetError` are macros expanding to `SDL_SetError`/`SDL_GetError`.
**Mitigation:** `--exclude` (same as Image's `IMG_SetError`/`IMG_GetError`).

### Risk 4: No Platform-Conditioned Code

**Verified:** Only `#if` blocks are version-gated `SDL_TTF_COMPILEDVERSION` and `SDL_DEPRECATED` fallback definition. No platform-conditioned function availability or struct layout.

---

## 4. Constants and Macros

### Simple Numeric Constants (auto-emit via generate-macro-bindings)

`SDL_TTF_MAJOR_VERSION` (2), `SDL_TTF_MINOR_VERSION` (24), `SDL_TTF_PATCHLEVEL` (0), `UNICODE_BOM_NATIVE`, `UNICODE_BOM_SWAPPED`, `TTF_STYLE_NORMAL` through `TTF_STYLE_STRIKETHROUGH`, `TTF_HINTING_NORMAL` through `TTF_HINTING_LIGHT_SUBPIXEL`, `TTF_WRAPPED_ALIGN_LEFT/CENTER/RIGHT`.

### Function-like Macros

`SDL_TTF_VERSION(X)`, `SDL_TTF_VERSION_ATLEAST(X, Y, Z)`, backward-compat rendering aliases (`TTF_RenderText` → `TTF_RenderText_Shaded`, etc.).

### Cross-family Error Macros (exclude)

| Macro | Expands to | Action |
|---|---|---|
| `TTF_SetError` | `SDL_SetError` | `--exclude` |
| `TTF_GetError` | `SDL_GetError` | `--exclude` |

---

## 5. Include Dependencies

SDL_ttf.h includes only: `SDL.h` (umbrella), `begin_code.h`, `close_code.h`. All through `include/SDL2/` — no new include directories needed.

---

## 6. RSP Recommendations

### Family RSP: `rsp/sdl2-ttf.rsp`

```
--libraryPath
SDL2_ttf

--methodClassName
SDL_ttfNative

--exclude
TTF_SetError
TTF_GetError
TTF_GetFontKerningSize
TTF_SetDirection
TTF_SetScript
```

### Per-Header RSP: `rsp/per-header/SDL_ttf.rsp`

**Not needed initially.** Single-header library with no foreign-type boundaries and no platform-conditioned API.

### Base RSP — No changes needed

Existing `base.rsp` remaps (`char=byte`, `void*=nint`, `wchar_t *=nint`, `SDL_bool=int`) cover all TTF needs.

---

## 7. Family Config

```python
"ttf": {
    "namespace": "SDL2.Ttf",
    "raw_class": "SDL_ttfNative",
    "rsp": "sdl2-ttf.rsp",
    "bootstrap_scope": "bootstrap-sdl2-ttf.headers.txt",
    "full_scope": "sdl2-ttf.headers.txt",
    "library_dir": "Janset.SDL2.Ttf",
},
```

Scope file: single entry `SDL_ttf.h`.
Owner mode for `uniform-opaque`: `owner_mode = "owner" if family in ("core", "ttf") else "consumer"`.

---

## 8. Postprocess Notes

### Existing Rewriters Coverage

| Rewriter | Needed? | Notes |
|---|---|---|
| `strip-varargs` | No | No variadic functions |
| `libraryimport` | Yes | Standard Modern pass |
| `platform-delta` | No | No platform-sensitive headers |
| `guid-substitute` | No | No `SDL_GUID` types |
| `threadid-dispatch` | Yes — extended | Rename to `clong-dispatch`, add 5 TTF function names |
| `uniform-opaque` | Yes — owner mode | Emits `TTF_Font` Pattern B handle in `Handles.g.cs` |

### New/Extended Rewriter: `clong-dispatch`

Rename `threadid-dispatch` to `clong-dispatch`, extend name match set to include 5 TTF C `long` functions. Same structural transform — no new rewriter logic needed.

---

## 9. Test Priorities

1. **`TTF_Init` / `TTF_Quit` / `TTF_WasInit`** — Library lifecycle
2. **`TTF_OpenFont` + `TTF_CloseFont`** — Font open/close, proves `TTF_Font` handle roundtrip
3. **`TTF_OpenFontIndex`** — Top-priority C `long` test (index=0, verify identical to `TTF_OpenFont`)
4. **`TTF_FontFaces`** — C `long` return test (assert return == 1 on single-face font)
5. **`TTF_FontHeight` / `TTF_FontAscent` / `TTF_FontDescent`** — Font metrics
6. **`TTF_RenderUTF8_Solid`** — Most-used rendering function, returns `SDL_Surface *` (Core type)
7. **`TTF_SizeUTF8`** — Text measurement with out parameters
8. **`TTF_GlyphIsProvided32`** — 32-bit codepoint glyph inspection

### Test Font

Need a small, redistributable `.ttf` font file committed to `tests/`. DejaVu Sans recommended (freely redistributable).

---

## 10. Open Questions

1. **TTF_Font ownership in uniform-opaque:** TTF is an owner family — needs `owner_mode = "owner" if family in ("core", "ttf") else "consumer"` (one-line change in generate_bindings.py L1268).

2. **Should deprecated functions emit `[Obsolete]` instead of being excluded?** Recommendation: Exclude. Two of three miss `SDLCALL` (ABI risk). SDL2-CS never emitted them.

3. **`const TTF_Font *` in getter functions:** Does `uniform-opaque` handle `const` pointer-to-by-value? Yes — ClangSharp strips C `const`, rewriter sees `TTF_Font*` pattern.

4. **Extend `threadid-dispatch` or create new rewriter?** Recommend extending (rename to `clong-dispatch`). Same structural transform pattern, avoid code duplication.
