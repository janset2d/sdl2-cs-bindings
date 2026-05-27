# Item 3 — SDL2_gfx Layer 1 Raw ABI: Design Spec

**Date:** 2026-05-27
**Status:** Approved
**Branch:** `spike/binding-autogen-sdl2-gfx`
**Depends on:** Item 2 (SDL_image verification) — closed
**Unblocks:** Item 4 (SDL2_ttf Layer 1)

## 1. Problem Statement

Add SDL2_gfx to the ClangSharp + Roslyn postprocess binding generator as the third active family (after Core and Image). GFX is the lowest-risk satellite: 102 functions across 4 headers, one transparent blittable struct (`FPSmanager`), no opaque handles, no C `long`, no callbacks, no unions, no platform-conditioned code. The only generation risk is per-header export macros — resolved with 4 `--define-macro` insurance entries.

## 2. Design

### 2.1 Family Identity

| Field | Value |
|-------|-------|
| Config key | `"gfx"` |
| C# namespace | `SDL2.Gfx` |
| Internal raw ABI class | `SDL2_gfxNative` |
| Native library | `SDL2_gfx` |
| Managed project | `Janset.SDL2.Gfx` |
| Owner mode | `false` (consumer — no satellite-owned opaque handles) |

### 2.2 Header Inventory

4 functional headers, ordered, no cross-header dependencies (all include only SDL2 core headers):

1. `SDL2_framerate.h` — 5 functions (all `SDL_`-prefixed), `FPSmanager` struct
2. `SDL2_gfxPrimitives.h` — 59 functions (bare names), export macro `SDL2_GFXPRIMITIVES_SCOPE`
3. `SDL2_imageFilter.h` — 30 functions (all `SDL_`-prefixed), export macro `SDL2_IMAGEFILTER_SCOPE`
4. `SDL2_rotozoom.h` — 8 functions (bare names), export macro `SDL2_ROTOZOOM_SCOPE`

Excluded: `SDL2_gfxPrimitives_font.h` (data-only, no public API surface).

### 2.3 Family RSP (`rsp/sdl2-gfx.rsp`)

```
# SDL2_gfx uses per-header export macros instead of extern DECLSPEC.
# Insurance: explicitly define each scope macro to extern so ClangSharp
# resolves function declarations regardless of host preprocessor state.
--define-macro
SDL2_GFXPRIMITIVES_SCOPE=extern
SDL2_ROTOZOOM_SCOPE=extern
SDL2_FRAMERATE_SCOPE=extern
SDL2_IMAGEFILTER_SCOPE=extern

# M_PI appears conditionally in SDL2_gfxPrimitives.h and SDL2_rotozoom.h
# (both guarded by #ifndef M_PI). Host compiler (MSVC/libclang) pre-defines
# it on Windows so ClangSharp never emits it there; cross-platform determinism
# requires an explicit exclude. BCL provides System.Math.PI.
--exclude
M_PI
```

No `--libraryPath` or `--methodClassName` — those are derived from config (`library_name` / `raw_class`) per the Iteration 2 RSP-identity retirement rule.

### 2.4 Config Changes (`config/family-config.json`)

The `families.gfx` section already exists with identity fields. Populate `headers[]`:

```json
"headers": [
  { "order": 10, "name": "SDL2_framerate.h" },
  { "order": 20, "name": "SDL2_gfxPrimitives.h" },
  { "order": 30, "name": "SDL2_imageFilter.h" },
  { "order": 40, "name": "SDL2_rotozoom.h" }
]
```

No other config fields change — all pre-populated values (identity, `owner_mode: false`, empty `clong_methods[]`, empty `platform_sensitive_headers[]`, `required_surface: null`, opaque/flags rosters) are already correct for GFX.

### 2.5 Orchestrator Change (`generate_bindings.py`)

Add `"gfx"` to `selected_families("all")` return value. All other per-family routing (header iteration, postprocess invocation, `owner_mode` derivation) is already family-parameterized — GFX activates automatically through the existing infrastructure.

### 2.6 C# Project (`src/Janset.SDL2.Gfx/`)

Pattern: copy `Janset.SDL2.Image.csproj`, change names:
- `<RootNamespace>`: `Janset.SDL2.Gfx`
- `<AssemblyName>`: `Janset.SDL2.Gfx`
- `<ProjectReference>` → `../Janset.SDL2.Core/Janset.SDL2.Core.csproj`
- Compile Include globs: `Generated/**/*.cs` (recursive, covers both Compat and Modern)

Copy `Support/DisableRuntimeMarshalling.cs` from Image (cross-assembly Pattern B contract — identical content, no family-specific values).

Add project to `Janset.SDL2.ClangSharpSpike.slnx`.

### 2.7 Postprocess

Zero changes. All 7 steps are GFX-safe:

| Step | Effect on GFX |
|------|--------------|
| `platform-delta` | No-op (no platform-sensitive headers) |
| `strip-varargs` | No-op (no variadic functions) |
| `libraryimport` | Standard Modern pass |
| `flags-detect` | No-op (no `Flags`-suffixed enums in GFX) |
| `guid-substitute` | No-op (no `SDL_GUID` types) |
| `clong-dispatch` | No-op (no C `long` surface) |
| `uniform-opaque` (consumer) | Removes empty partial struct stubs; no opaque handles to rewrite |

### 2.8 Oracle (`oracle.cs`)

Add GFX entry to the existing hardcoded `FamilyConfigs` and `KnownFamilies`, matching the pattern used for Core and Image. This is a minimal data-only addition — the full oracle.cs refactor remains deferred per the 2026-05-27 Deniz decision.

## 3. Non-Goals

- No new postprocess steps, no new rewriters, no new CLI modes.
- No include directory changes — GFX headers are flat in the same `include/SDL2/` directory.
- No per-header RSP files for GFX (none needed — no foreign types, no BCL-replaceable helpers, no tag canonicalization).
- No `build/manifest.json` changes.
- No CI/CD or production build changes.

## 4. Verification Gates

### 4.1 Determinism

1. `--family all --execute` twice with identical inputs → second `git diff --ignore-cr-at-eol` empty across all three active families.
2. Core and Image `Generated/` roots byte-unchanged by GFX activation.

### 4.2 Build

1. `dotnet build Janset.SDL2.ClangSharpSpike.slnx -c Release` — 0 warnings, 0 errors across all TFMs (postprocess + Core + Image + GFX).
2. GFX csproj builds cleanly on all 5 TFMs.

### 4.3 Oracle

1. `oracle.cs --family sdl2-core --family sdl2-image --family sdl2-gfx --write-report` — GFX evidence report produced, Core + Image 0 new findings.

### 4.4 AbiTests

1. `AbiTests.csproj` `792/792` across `net462`, `net8.0`, `net9.0`, `net10.0`.

### 4.5 Slopwatch

1. `slopwatch analyze --fail-on warning` — 0 issues.

### 4.6 Self-tests

1. Python `--self-test` passes.
2. C# postprocess `--self-test` passes.

## 5. Exit Criteria

Item 3 is closed when:

1. Config `families.gfx.headers[]` populated with 4 headers.
2. `selected_families("all")` returns `["core", "image", "gfx"]`.
3. `rsp/sdl2-gfx.rsp` created with 4 `--define-macro` + `--exclude M_PI`.
4. `src/Janset.SDL2.Gfx/` project created with csproj + `Support/DisableRuntimeMarshalling.cs`.
5. GFX added to `Janset.SDL2.ClangSharpSpike.slnx`.
6. `oracle.cs` extended with GFX hardcoded entry.
7. All verification gates pass (determinism, build, oracle, AbiTests, slopwatch, self-tests).
8. Core and Image `Generated/` regress-free relative to pre-Item 3 baseline (byte-identical output).
