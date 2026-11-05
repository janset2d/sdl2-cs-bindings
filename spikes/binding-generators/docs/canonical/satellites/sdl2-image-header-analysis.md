# SDL2_image Binding Generation — Cross-Check Analysis

**Date:** 2026-05-25
**Target:** SDL2_image v2.8.8 (installed header from `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_image.h`, 2195 lines)
**Status:** Cross-check of existing generated bindings at `src/Janset.SDL2.Image/Generated/`. One bug found.

---

## 1. Overall Verdict

**The existing SDL2_image bindings are 99% correct.** Complete function coverage (59/59), correct Pattern B handle usage, correct namespace and ProjectReference, clean RSP configuration. One metadata bug found: `IMG_InitFlags` is missing `[Flags]` attribute.

---

## 2. Function Inventory — From Header

59 `extern DECLSPEC` declarations total. Categorized:

| Category | Count | Examples |
|---|---|---|
| Lifecycle | 3 | `IMG_Linked_Version`, `IMG_Init`, `IMG_Quit` |
| Generic load | 3 | `IMG_Load`, `IMG_Load_RW`, `IMG_LoadTyped_RW` |
| Texture load | 3 | `IMG_LoadTexture`, `IMG_LoadTexture_RW`, `IMG_LoadTextureTyped_RW` |
| Format detection (is*) | 18 | `IMG_isBMP` through `IMG_isWEBP` |
| Format-specific load | 20 | `IMG_LoadAVIF_RW` through `IMG_LoadWEBP_RW` (19 format-specific) + `IMG_LoadSizedSVG_RW` |
| Save | 4 | `IMG_SavePNG`, `IMG_SavePNG_RW`, `IMG_SaveJPG`, `IMG_SaveJPG_RW` |
| XPM from array | 2 | `IMG_ReadXPMFromArray`, `IMG_ReadXPMFromArrayToRGB888` |
| Animation | 6 | `IMG_LoadAnimation`, `IMG_LoadAnimation_RW`, `IMG_LoadAnimationTyped_RW`, `IMG_FreeAnimation`, `IMG_LoadGIFAnimation_RW`, `IMG_LoadWEBPAnimation_RW` |

**Risk flags: ALL CLEAN.** Zero C `long`, zero `wchar_t`, zero variadics, zero `FILE*`, zero platform-conditioned code (the `#if SDL_VERSION_ATLEAST(2,0,0)` guard on texture functions is always true for SDL2).

---

## 3. Type Inventory

### Opaque Handle Types — NONE owned by Image
Image exclusively consumes handles from SDL2.Core:
- `SDL_RWops` — Pattern B force-opaque, used by-value after uniform-opaque
- `SDL_Renderer` — Pattern B auto-detected, used by-value
- `SDL_Texture` — Pattern B auto-detected, used by-value

### Transparent Structs
- `IMG_Animation` — 5 fields (`w`, `h`, `count`, `frames`, `delays`), fully blittable

### Enums
- `IMG_InitFlags` — bitmask enum, 6 values (JPG=0x01, PNG=0x02, TIF=0x04, WEBP=0x08, JXL=0x10, AVIF=0x20)

### Callbacks — None

---

## 4. Cross-Check Results

### 4A. Missing Functions: **NONE**
All 59 header functions appear in generated output. Oracle count matches (59 functions).

### 4B. Signature Errors: **NONE**
All function signatures verified correct after applying `base.rsp` remaps and `uniform-opaque` postprocess.

### 4C. Pattern B Handle Usage: **CORRECT**
- `SDL_RWops*` → `SDL_RWops` (by-value) ✓
- `SDL_Renderer*` → `SDL_Renderer` (by-value) ✓
- `SDL_Texture*` → `SDL_Texture` (by-value) ✓
- `SDL_Surface*` stays `SDL_Surface*` (transparent struct, not a handle) ✓
- `SDL_version*` stays `SDL_version*` (transparent struct) ✓
- `IMG_Animation*` stays `IMG_Animation*` (own struct) ✓

### 4D. Constants: **CORRECT**
All 4 version constants generated with correct values. Backwards-compat `IMAGE_*` aliases not generated (correct — they reference `SDL_IMAGE_*` tokens, not literals).

### 4E. Namespace and ProjectReference: **CORRECT**
- Namespace: `SDL2.Image` ✓
- ProjectReference to Core ✓
- Internal raw ABI class: `SDL_imageNative` ✓
- Modern: `[LibraryImport]` + `[UnmanagedCallConv]` ✓
- Compat: `[DllImport]` + `extern` ✓
- TFM split correct ✓
- `DisableRuntimeMarshalling.cs` present ✓

### 4F. RSP Correctness: **CORRECT**
- `--libraryPath SDL2_image` ✓
- `--methodClassName SDL_imageNative` ✓
- `--exclude IMG_SetError IMG_GetError` ✓ (see also [sdl2-satellite-error-function-consolidation.md](sdl2-satellite-error-function-consolidation.md) for cross-family analysis)
- No missing excludes ✓
- Three-tier RSP wiring correct ✓

---

## 5. Bug Found and Fixed: `IMG_InitFlags` missing `[Flags]`

### Evidence
**Header** (SDL_image.h L95-103):
```c
typedef enum IMG_InitFlags {
    IMG_INIT_JPG    = 0x00000001,
    IMG_INIT_PNG    = 0x00000002,
    ...
} IMG_InitFlags;
```

**Documentation** (L112): "Flags should be one or more flags from IMG_InitFlags OR'd together."

**Pre-Item 1 generated output** (both Modern and Compat):
```csharp
public enum IMG_InitFlags  // ← MISSING [Flags]
{
    IMG_INIT_JPG = 0x00000001,
    ...
}
```

### Impact
Without `[Flags]`, .NET consumers calling `IMG_Init(IMG_INIT_JPG | IMG_INIT_PNG).ToString()` get the numeric value `3` instead of `"IMG_INIT_JPG, IMG_INIT_PNG"`. Debugging display and enum formatting are affected. **Note:** `Enum.HasFlag()` does NOT require `[Flags]` — it works correctly on any enum regardless of the attribute. The concrete impact is formatting and debuggability only.

### Constitution Authority
[`binding-generator-constitution.md`](../binding-generator-constitution.md) §"Enums": "Add `[Flags]` when header comments, composed aliases, bit values, or API docs prove bitmask semantics."

### Fix
Item 1 S1-6 adds the systematic `flags-detect` postprocess step. `IMG_InitFlags` is decorated by the `Flags` suffix rule in both Modern and Compat output; Item 2 remains as an explicit Image-targeted regression/closure check.

### Severity
Low — metadata annotation only. ABI and values are correct. Fix pre-NuGet-ship.

---

## 6. Gaps (Not Bugs — Intentional or Deferred)

1. **Function-like macros not emitted** — `SDL_IMAGE_VERSION(X)` and `SDL_IMAGE_VERSION_ATLEAST(X,Y,Z)` are Layer 2/3 helper candidates, correctly not in Layer 1.
2. **No friendly overloads** — Raw ABI uses `byte*` for strings. Layer 3 ergonomics deferred.
3. **Backward-compat `IMAGE_*` aliases skipped** — Correct. They token-reference `SDL_IMAGE_*` macros.

---

## 7. Cross-Family `[Flags]` Risk

The same `[Flags]` gap is **confirmed** in:
- Mixer: `MIX_InitFlags` — power-of-two bitmask values at SDL_mixer.h:109-118, documentation at L131 says "one or more flags OR'd together". Same pattern as `IMG_InitFlags`.
- GFX: No enums (uses `#define` constants only). Not affected.
- TTF: `TTF_Direction` is NOT a bitmask (exclusive values LTR/RTL/TTB/BTT). Not affected.

Item 1's systematic `[Flags]` detection postprocess now covers `IMG_InitFlags` and should also cover `MIX_InitFlags` when Mixer generation activates.

---

## 8. Summary

| Check | Result |
|---|---|
| Function coverage | ✓ 59/59 |
| Signature correctness | ✓ |
| Pattern B handle usage | ✓ |
| Constants | ✓ |
| Namespace | ✓ |
| ProjectReference | ✓ |
| TFM split | ✓ |
| Import style (Modern/Compat) | ✓ |
| RSP correctness | ✓ |
| Cross-assembly contract | ✓ |
| **`[Flags]` on `IMG_InitFlags`** | **✓ fixed by Item 1 S1-6 `flags-detect`** |
