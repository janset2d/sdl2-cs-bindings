# SDL2_ttf Binding Generation — Header Deep-Dive Analysis

**Date:** 2026-05-25
**Target:** SDL2_ttf v2.24.0 (installed header from `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_ttf.h`, 2343 lines)
**Status:** Research artifact. No code written.

---

## 1. Function Inventory

**Total: 88 public function declarations** (all `extern DECLSPEC` occurrences, including `extern SDL_DEPRECATED DECLSPEC` variants).

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

Note: `TTF_GetFontKerningSize` is DEPRECATED — uses raw FreeType font indices, and is the only deprecated function that lacks `SDLCALL` (`extern SDL_DEPRECATED DECLSPEC int` without `SDLCALL` at SDL_ttf.h:2145).

### Text Shaping / HarfBuzz (4)

`TTF_SetDirection` (DEPRECATED, HAS `SDLCALL` at SDL_ttf.h:2268), `TTF_SetScript` (DEPRECATED, HAS `SDLCALL` at SDL_ttf.h:2291), `TTF_SetFontDirection`, `TTF_SetFontScriptName`

### Non-SDLCALL Functions (4 non-deprecated)

These four functions lack the `SDLCALL` calling-convention macro (`extern DECLSPEC` without `SDLCALL` at SDL_ttf.h:2168, 2185, 2204, 2217). They require an explicit bind/exclude decision — do not silently lose them because they don't match the usual `extern DECLSPEC ... SDLCALL` regex:
- `TTF_GetFontKerningSizeGlyphs` (line 2168)
- `TTF_GetFontKerningSizeGlyphs32` (line 2185)
- `TTF_SetFontSDF` (line 2204)
- `TTF_GetFontSDF` (line 2217)

### Miscellaneous (2)

`TTF_ByteSwappedUNICODE`, `TTF_CloseFont`

### Summary of flagged risks

| Risk | Count | Functions |
|---|---|---|
| C `long` parameter | 4 | `TTF_OpenFontIndex`, `TTF_OpenFontIndexRW`, `TTF_OpenFontIndexDPI`, `TTF_OpenFontIndexDPIRW` |
| C `long` return | 1 | `TTF_FontFaces` |
| Deprecated (exclude candidates) | 3 | `TTF_GetFontKerningSize`, `TTF_SetDirection`, `TTF_SetScript` |
| No SDLCALL (non-deprecated — needs bind/exclude decision) | 4 | `TTF_GetFontKerningSizeGlyphs`, `TTF_GetFontKerningSizeGlyphs32`, `TTF_SetFontSDF`, `TTF_GetFontSDF` |
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

`TTF_Font` is satellite-owned — NOT in Core's `opaque-handle-roster.json`. The `uniform-opaque` postprocess needs owner mode for TTF. Core handles consumed via `ProjectReference`: `SDL_RWops` (4 functions use it), `SDL_Surface` (all 32 rendering functions return it — 4 render groups × 8 functions each).

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

3 functions deprecated in SDL_ttf 2.24.0. Only `TTF_GetFontKerningSize` lacks `SDLCALL` (ABI risk on Windows x86 — at SDL_ttf.h:2145 it uses `extern SDL_DEPRECATED DECLSPEC int` without `SDLCALL`). `TTF_SetDirection` (line 2268) and `TTF_SetScript` (line 2291) both include `SDLCALL` despite being deprecated.
**Mitigation:** `--exclude` all 3 in family RSP.

### Risk 3: Cross-family Macro Aliases (LOW)

`TTF_SetError`/`TTF_GetError` are macros expanding to `SDL_SetError`/`SDL_GetError`.
**Mitigation:** `--exclude` (same as Image's `IMG_SetError`/`IMG_GetError`).

### Risk 4: Non-Deprecated No-SDLCALL Functions (MEDIUM)

4 non-deprecated functions lack `SDLCALL`: `TTF_GetFontKerningSizeGlyphs`, `TTF_GetFontKerningSizeGlyphs32`, `TTF_SetFontSDF`, `TTF_GetFontSDF` (SDL_ttf.h:2168, 2185, 2204, 2217). SDL's `SDLCALL` explicitly maps to `__cdecl` on Windows at `begin_code.h:77-80`; without it, the native declaration has no explicit calling-convention annotation. The real risk is ClangSharp/.NET P/Invoke import emission defaulting incorrectly — not that the native default is `__stdcall`. Generated `[DllImport]`/`[LibraryImport]` must force or verify `CallingConvention.Cdecl`.
**Mitigation:** Explicit bind/exclude decision needed. Cannot silently include them without verifying the calling convention matches. These are modern, recommended API functions (replacements for deprecated variants) — they should be bound, but the calling convention must be explicitly handled.

### Risk 5: No Platform-Conditioned Code

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

### Base RSP — Covers generic scalar remaps only

Existing `base.rsp` remaps (`char=byte`, `void*=nint`, `wchar_t *=nint`, `SDL_bool=int`) cover generic scalar translation. TTF-specific concerns (C `long` dispatch, no-SDLCALL calling convention, and satellite-owned `TTF_Font`) require postprocess and family-policy work beyond base RSP coverage.

---

## 7. Family Config

```python
"ttf": {
    "namespace": "SDL2.Ttf",
    "raw_class": "SDL_ttfNative",
    "rsp": "sdl2-ttf.rsp",
    "headers": "sdl2-ttf.headers.txt",
    "library_dir": "Janset.SDL2.Ttf",
},
```

Production header list: single entry `SDL_ttf.h`.
Owner mode for `uniform-opaque`: `owner_mode = "owner" if family in ("core", "ttf") else "consumer"`. **This alone is insufficient.** The `OpaqueHandleEmitRewriter` auto-detects empty structs by name prefix `StartsWith("SDL_")` at `OpaqueHandleEmitRewriter.cs:200`. `TTF_Font` won't match — it starts with `TTF_`. The rewriter also emits handles into `namespace SDL2` at `OpaqueHandleEmitRewriter.cs:336-337`, which is wrong for satellite-owned handles that should go in `SDL2.Ttf`. A family-aware handle discovery and emit path is needed. See §8 postprocess notes for the required changes.

---

## 8. Postprocess Notes

### Existing Rewriters Coverage

| Rewriter | Needed? | Notes |
|---|---|---|
| `strip-varargs` | No | No variadic functions |
| `libraryimport` | Yes | Standard Modern pass |
| `platform-delta` | No | No platform-sensitive headers |
| `guid-substitute` | No | No `SDL_GUID` types |
| `threadid-dispatch` | Yes — significant extension needed | The current rewriter is hardcoded to `SDL_ThreadID`/`SDL_GetThreadID` at `ThreadIdDualDispatchRewriter.cs:76-80`, and only inspects return-type native type names at L128-132. TTF needs 4 `long` parameter positions plus 1 `long` return. The rewriter must handle C `long` at parameter positions (not just return type) and accept an expanded name set. |
| `uniform-opaque` | Yes — family-aware handle path needed | The current rewriter discovers opaque handles by `StartsWith("SDL_")` at `OpaqueHandleEmitRewriter.cs:200` and hardcodes emit namespace to `SDL2` at L336-337. Needs a family-aware path: discover handles per-family, emit into the correct namespace (`SDL2.Ttf`), and handle non-`SDL_`-prefixed names (`TTF_Font`). |

### Extended Rewriter: C `long` dispatch

The existing `ThreadIdDualDispatchRewriter` is hardcoded to 2 method names (`SDL_ThreadID`, `SDL_GetThreadID`) at `ThreadIdDualDispatchRewriter.cs:76-80` and only checks return-type native type names at L128-132. Extending it for TTF requires:

1. Expanding the `AffectedMethodNames` set to include the 5 TTF functions
2. Adding parameter-position rewrite logic (the current rewriter only rewrites the return type): `TTF_OpenFontIndex*` functions need `long index` → `CLong index` on Modern, dual-DllImport dispatch on Compat
3. The rewriter should be renamed to `ClongDualDispatchRewriter` to reflect its generalized role

This is more than a "same logic" extension — the parameter-position handling is new code.

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

1. **TTF_Font ownership in uniform-opaque:** TTF is an owner family. However, `owner_mode = "owner"` on the CLI is insufficient — the `OpaqueHandleEmitRewriter` hardcodes `StartsWith("SDL_")` for auto-discovery at `OpaqueHandleEmitRewriter.cs:200` and emits handles into hardcoded `namespace SDL2` at L336-337. Required changes: (a) accept a family-aware handle discovery path that doesn't depend on `SDL_` prefix, (b) emit handles into the correct per-family namespace (`SDL2.Ttf` for `TTF_Font`), (c) handle dual ownership: a satellite needs its own local handle (`TTF_Font`) while also consuming Core handles (`SDL_RWops`, `SDL_Surface`) by value via ProjectReference.

2. **Should deprecated functions emit `[Obsolete]` instead of being excluded?** Recommendation: Exclude. Only `TTF_GetFontKerningSize` lacks `SDLCALL` (ABI risk). `TTF_SetDirection` and `TTF_SetScript` have `SDLCALL` but take raw HarfBuzz types cast to `int` — not a typed API. SDL2-CS never emitted these.

3. **No-SDLCALL non-deprecated functions:** `TTF_GetFontKerningSizeGlyphs`, `TTF_GetFontKerningSizeGlyphs32`, `TTF_SetFontSDF`, `TTF_GetFontSDF` — these are modern recommended API but lack `SDLCALL`. SDL's `SDLCALL` maps to `__cdecl` on Windows at `begin_code.h:77-80`; without it, the declaration carries no explicit calling-convention annotation. Generated P/Invoke imports must force `CallingConvention.Cdecl` rather than relying on ClangSharp's default emission.

4. **Extend `threadid-dispatch` or create new rewriter?** Extend (rename to `ClongDualDispatchRewriter`). However, this is more than adding names — the current rewriter only rewrites return types (`ThreadIdDualDispatchRewriter.cs:128-132`). TTF's `long index` parameters need parameter-position dispatch logic, which is new code.
