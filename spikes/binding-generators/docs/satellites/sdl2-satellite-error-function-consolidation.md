# SDL2 Satellite Error Function Consolidation

**Date:** 2026-05-26
**Status:** Research artifact. Feeds companion-helper policy design when Layer 2/3 work begins (Roadmap M5/M6).
**Scope:** All SDL2 satellite families (Image, TTF, Mixer, GFX, Net) plus Core error macros.

---

## 1. Core Finding

SDL2_image, SDL2_ttf, and SDL2_mixer satellite error helper names (`IMG_SetError`, `TTF_GetError`, `Mix_ClearError`, etc.) are **C preprocessor `#define` macros** — not real `extern DECLSPEC` function declarations. They expand to Core SDL's error infrastructure (`SDL_SetError`, `SDL_GetError`, `SDL_ClearError`, `SDL_OutOfMemory`). ClangSharp cannot cross the `--methodClassName` / assembly boundary for these, so they must be excluded from Layer 1 generation and provided as C# redirect methods in a higher layer. **SDL_net is the exception** (see §2.6) — `SDLNet_SetError` and `SDLNet_GetError` are real exported functions.

---

## 2. Per-Family Header Analysis

### 2.1 SDL2 Core — `SDL_error.h`

| Macro | Expansion | Real function? | Generated? |
|---|---|---|---|
| `SDL_OutOfMemory()` | `SDL_Error(SDL_ENOMEM)` | No | No (correctly absent) |
| `SDL_Unsupported()` | `SDL_Error(SDL_UNSUPPORTED)` | No | No (correctly absent) |
| `SDL_InvalidParamError(param)` | `SDL_SetError("Parameter '%s' is invalid", (param))` | No | No (correctly absent) |

`SDL_SetError`, `SDL_GetError`, `SDL_ClearError`, `SDL_Error`, `SDL_GetErrorMsg` are **real exported functions** — correctly generated as `[DllImport]`/`[LibraryImport]` in both Compat and Modern output.

```c
// vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_error.h:139-141
#define SDL_OutOfMemory()   SDL_Error(SDL_ENOMEM)
#define SDL_Unsupported()   SDL_Error(SDL_UNSUPPORTED)
#define SDL_InvalidParamError(param)    SDL_SetError("Parameter '%s' is invalid", (param))
```

### 2.2 SDL_image

```c
// vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_image.h:2180, 2187
#define IMG_SetError    SDL_SetError
#define IMG_GetError    SDL_GetError
```

- **2 macros**, no `IMG_ClearError` or `IMG_OutOfMemory`.
- Already excluded in `rsp/sdl2-image.rsp:11-13`.

### 2.3 SDL_ttf

```c
// vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_ttf.h:2224, 2231
#define TTF_SetError    SDL_SetError
#define TTF_GetError    SDL_GetError
```

- **2 macros**, no `TTF_ClearError` or `TTF_OutOfMemory`.
- `.rsp` file to be created in Item 4 ([`satellite-expansion-roadmap.md`](../satellite-expansion-roadmap.md) §Item 4). Must include `--exclude TTF_SetError TTF_GetError`.

### 2.4 SDL_mixer

```c
// vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_mixer.h:2803-2822
/*
 * We'll use SDL for reporting errors
 */
#define Mix_SetError    SDL_SetError
#define Mix_GetError    SDL_GetError
#define Mix_ClearError  SDL_ClearError
#define Mix_OutOfMemory SDL_OutOfMemory
```

- **4 macros** — only satellite with all four error primitives.
- SDL2-CS surfaces only `Mix_SetError`, `Mix_GetError`, `Mix_ClearError` (NOT `Mix_OutOfMemory`) as C# redirect methods.
- `.rsp` file to be created in Item 5 ([`satellite-expansion-roadmap.md`](../satellite-expansion-roadmap.md) §Item 5). Must include `--exclude Mix_SetError Mix_GetError Mix_ClearError Mix_OutOfMemory`.

### 2.5 SDL2_gfx

No error macros. GFX uses return-code-based error reporting. No `.rsp` exclusions needed.

### 2.6 SDL_net — Exception

```c
// vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_net.h:948, 962
extern DECLSPEC void SDLCALL SDLNet_SetError(const char *fmt, ...);
extern DECLSPEC const char * SDLCALL SDLNet_GetError(void);
```

- **These are real exported functions**, not `#define` macros. They live in `SDL2_net.dll`.
- Do **NOT** exclude from generation — they are legitimate P/Invoke targets.

---

## 3. Per-Peer Comparison

| Peer | IMG_SetError vs. | Method | Source |
|---|---|---|---|
| **SDL2-CS** | C# redirect: `IMG_GetError() => SDL.SDL_GetError()` | Manual friendly methods | [`external/sdl2-cs/src/SDL2_image.cs:256-264`](../../../../external/sdl2-cs/src/SDL2_image.cs) |
| **SDL2-CS** | C# redirect: `TTF_GetError() => SDL.SDL_GetError()` | Manual friendly methods | [`external/sdl2-cs/src/SDL2_ttf.cs:756-764`](../../../../external/sdl2-cs/src/SDL2_ttf.cs) |
| **SDL2-CS** | C# redirect: `Mix_GetError() => SDL.SDL_GetError()`, `Mix_SetError()`, `Mix_ClearError()` | Manual friendly methods | [`external/sdl2-cs/src/SDL2_mixer.cs:648-661`](../../../../external/sdl2-cs/src/SDL2_mixer.cs) |
| **ppy/SDL3-CS** | SDL3 satellite error aliases are not present upstream; tests use core `SDL_GetError` directly | Not emitted in `.g.cs` | [`references/ppy-SDL3-CS/`](../references/) |
| **Alimer** | N/A — no satellite bindings | Core SDL3 only | N/A |
| **Silk.NET 2.X** | No SDL_image/ttf/mixer satellite bindings found; core SDL only | N/A | N/A |

**Key observation:** No peer generates `[DllImport]`/`[LibraryImport]` stubs for satellite error macros. The universal pattern is either (a) exclude from raw ABI generation, or (b) provide C# redirect methods that forward to Core SDL error functions.

### SDL2 vs SDL3 Difference

In SDL3, some error macros were promoted to real exported functions:

- `SDL_OutOfMemory()` became a real function → ppy's `SDL_error.g.cs` generates it as `[DllImport]`.
- `SDL_Unsupported()` and `SDL_InvalidParamError()` remain macros → ppy hand-writes them as `[Macro]` methods in `SDL_error.cs`.
- This means SDL3 satellite bindings do NOT need error function companions at all (satellites call `SDL_SetError`/`SDL_GetError` directly, and those are already `[DllImport]` in the core assembly).

For SDL2, the situation is different — satellite error macros still redirect to Core via `#define`, so we need the exclude + C# redirect pattern.

---

## 4. `.rsp` Exclude Table

| Family | Functions to exclude | `.rsp` file | Status |
|---|---|---|---|
| Core | `SDL_OutOfMemory`, `SDL_Unsupported`, `SDL_InvalidParamError` (defensive — already not generated) | `rsp/sdl2-core.rsp` | Missing from exclude list (doesn't appear in output anyway) |
| Image | `IMG_SetError`, `IMG_GetError` | `rsp/sdl2-image.rsp:11-13` | Done |
| TTF | `TTF_SetError`, `TTF_GetError` | `rsp/sdl2-ttf.rsp` (to be created — Item 4) | Pending |
| Mixer | `Mix_SetError`, `Mix_GetError`, `Mix_ClearError`, `Mix_OutOfMemory` | `rsp/sdl2-mixer.rsp` (to be created — Item 5) | Pending |
| GFX | None | `rsp/sdl2-gfx.rsp` (to be created — Item 3) | N/A |
| Net | **None** (real functions, do NOT exclude) | N/A | N/A |

---

## 5. Preferred Redirection Pattern (Target for Layer 2/3)

Based on the SDL2-CS reference and peer consensus:

```csharp
// In Janset.SDL2.Image (satellite assembly, friendly layer):
namespace SDL2.Image
{
    public static unsafe partial class SDL_image
    {
        // Redirect to Core's public Layer 2/3 error API (not internal raw ABI).
        // The satellite assembly references Core via ProjectReference,
        // so Core's public typed/friendly SDL_GetError/SDL_SetError resolve normally.
        public static string IMG_GetError() => SDL.SDL_GetError();
        public static void IMG_SetError(string fmtAndArglist) => SDL.SDL_SetError(fmtAndArglist);
    }
}

// Same pattern for TTF (namespace SDL2.Ttf) and Mixer (namespace SDL2.Mixer).
// Mixer also needs: Mix_ClearError() => SDL.SDL_ClearError().
```

These are **plain C# methods** with no `[DllImport]`/`[LibraryImport]` — they delegate to Core's **public** Layer 2/3 error API. Satellite assemblies cannot call Core's `internal SDLNative` raw ABI directly; the redirect targets Core's future typed/friendly public surface, which itself wraps the internal raw ABI. The satellite assembly references Core via `ProjectReference`, so Core public API resolves normally.

For `Mix_OutOfMemory`: SDL2-CS does NOT surface this even in the redirect layer, likely because `SDL_OutOfMemory` itself is a macro in SDL2. We should follow the same conservative approach — defer `Mix_OutOfMemory` until we have a concrete consumer scenario.

---

## 6. Path to Companion-Helper Policy

This research feeds an eventual **companion-helper policy** document that must be written before Layer 2/3 error redirects land. The Constitution (L547-548) says:

> *"Approved function-like macro helpers are manually authored or generated from an explicit companion-helper policy. ClangSharp does not emit them."*

The following are open decisions that companion-helper policy should resolve:

| Decision | Options |
|---|---|
| Which layer? | Layer 2 (typed public) vs Layer 3 (friendly overloads) |
| Generation vs manual? | Auto-generate from macro→function mapping table, or hand-write |
| Which error macros to surface? | All documented, or only those with known consumer demand |
| `Mix_OutOfMemory`? | Surface as redirect now, or defer (SDL2-CS defers) |
| `SDL_OutOfMemory`/`SDL_Unsupported`/`SDL_InvalidParamError`? | Add to Core companion, or leave as non-surfaced macros |
| Auto-generation mechanism? | Postprocess `MacroHelperRewriter`, or `FAMILY_CONFIG`-adjacent JSON mapping |

---

## 7. References

- **Header analysis docs (per-family):**
  - [`sdl2-image-cross-check.md`](sdl2-image-cross-check.md) §5 — `IMG_InitFlags` bug; §7 — cross-family `[Flags]` risk
  - [`sdl2-ttf-header-analysis.md`](sdl2-ttf-header-analysis.md) — C `long` surface, satellite-owned `TTF_Font`, excluded error macros
  - [`sdl2-mixer-header-analysis.md`](sdl2-mixer-header-analysis.md) — callback typedefs, satellite-owned `Mix_Music`, 4 error macros
  - [`sdl2-gfx-header-analysis.md`](sdl2-gfx-header-analysis.md) — no error macros
- **Roadmap:**
  - [`satellite-expansion-roadmap.md`](../satellite-expansion-roadmap.md) §Items 3/4/5 — per-family `.rsp` creation
- **Constitution (policy authority):**
  - [`binding-generator-constitution.md`](../../../../docs/binding-autogen/binding-generator-constitution.md) L547-548 — companion-helper policy requirement
  - Layer Contract L35-59 — three-layer architecture (internal raw ABI, public typed low-level, friendly overloads)
- **Peer reference:**
  - [`ppy-reference-analysis-2026-05-25.md`](../ppy-reference-analysis-2026-05-25.md) §2 — companion class analysis, `[Macro]` pattern
  - `external/sdl2-cs/src/SDL2_image.cs:256-264` — `IMG_GetError`/`IMG_SetError` redirect
  - `external/sdl2-cs/src/SDL2_ttf.cs:756-764` — `TTF_GetError`/`TTF_SetError` redirect
  - `external/sdl2-cs/src/SDL2_mixer.cs:648-661` — `Mix_GetError`/`Mix_SetError`/`Mix_ClearError` redirect
- **Working implementations:**
  - `rsp/sdl2-image.rsp:7-13` — current exclude pattern with inline rationale
  - `src/Janset.SDL2.Core/Generated/{Compat,Modern}/SDL_error.g.cs` — generated `SDL_SetError`/`SDL_GetError`/`SDL_ClearError`/`SDL_Error`/`SDL_GetErrorMsg`

## See Also

- [sdl2-function-like-macro-consolidation.md](sdl2-function-like-macro-consolidation.md) — broader function-like macro taxonomy (error macros are a sub-topic within Type B)
