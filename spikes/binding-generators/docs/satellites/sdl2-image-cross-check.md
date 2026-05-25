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
| Format-specific load | 19 | `IMG_LoadBMP_RW` through `IMG_LoadWEBP_RW` (includes TGA, SVG, SizedSVG) |
| Save | 4 | `IMG_SavePNG`, `IMG_SavePNG_RW`, `IMG_SaveJPG`, `IMG_SaveJPG_RW` |
| XPM from array | 2 | `IMG_ReadXPMFromArray`, `IMG_ReadXPMFromArrayToRGB888` |
| Animation | 5 | `IMG_LoadAnimation`, `IMG_LoadAnimation_RW`, `IMG_LoadAnimationTyped_RW`, `IMG_FreeAnimation`, `IMG_LoadGIFAnimation_RW`, `IMG_LoadWEBPAnimation_RW` |

**Risk flags: ALL CLEAN.** Zero C `long`, zero `wchar_t`, zero variadics, zero `FILE*`, zero platform-conditioned code (the `#if SDL_VERSION_ATLEAST(2,0,0)` guard on texture functions is always true for SDL2).

---

## 3. Type Inventory

### Opaque Handle Types — NONE owned by Image
Image exclusively consumes handles from SDL2.Core:
- `SDL_RWops` — Pattern B force-opaque, used by-value after uniform-opaque
- `SDL_Renderer` — Pattern B auto-detected, used by-value
- `SDL_Texture` — Pattern B auto-detected, used by-value

### Transparent Structs
- `IMG_Animation` — 4 fields (`w`, `h`, `count`, `frames`, `delays`), fully blittable

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
- `--exclude IMG_SetError IMG_GetError` ✓
- No missing excludes ✓
- Three-tier RSP wiring correct ✓

---

## 5. Bug Found: `IMG_InitFlags` missing `[Flags]`

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

**Generated output** (both Modern and Compat):
```csharp
public enum IMG_InitFlags  // ← MISSING [Flags]
{
    IMG_INIT_JPG = 0x00000001,
    ...
}
```

### Impact
Without `[Flags]`, .NET consumers calling `IMG_Init(IMG_INIT_JPG | IMG_INIT_PNG).ToString()` get the numeric value `3` instead of `"IMG_INIT_JPG, IMG_INIT_PNG"`. Also affects `Enum.HasFlag()` behavior in some edge cases.

### Constitution Authority
§"Enums" L446-448: "Add `[Flags]` when header comments, composed aliases, bit values, or API docs prove bitmask semantics."

### Fix
Add `[Flags]` to the enum declaration. Could be a targeted manual fix (2 lines — one per codegen tree) or a systematic `[Flags]` detection postprocess.

### Severity
Low — metadata annotation only. ABI and values are correct. Fix pre-NuGet-ship.

---

## 6. Gaps (Not Bugs — Intentional or Deferred)

1. **Function-like macros not emitted** — `SDL_IMAGE_VERSION(X)` and `SDL_IMAGE_VERSION_ATLEAST(X,Y,Z)` are Layer 2/3 helper candidates, correctly not in Layer 1.
2. **No friendly overloads** — Raw ABI uses `byte*` for strings. Layer 3 ergonomics deferred.
3. **Backward-compat `IMAGE_*` aliases skipped** — Correct. They token-reference `SDL_IMAGE_*` macros.

---

## 7. Cross-Family `[Flags]` Risk

The same `[Flags]` gap likely exists in:
- Mixer: `MIX_InitFlags` (also a power-of-two bitmask enum)
- Potentially GFX and TTF

A systematic audit of all satellite enum declarations is recommended before the next generation run. This could be a targeted follow-up rather than blocking satellite expansion.

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
| **`[Flags]` on `IMG_InitFlags`** | **✗ BUG** |
