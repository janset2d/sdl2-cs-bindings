# Item 4 - SDL2_ttf Layer 1 Raw ABI: Design Spec

**Date:** 2026-05-27
**Status:** Draft
**Branch:** `spike/binding-autogen-sdl2-gfx`
**Depends on:** Item 3 (SDL2_gfx Layer 1) - code landed in `3d085ab`
**Unblocks:** Item 5 (SDL2_mixer Layer 1)

## 1. Problem Statement

Add SDL2_ttf to the ClangSharp + Roslyn postprocess binding-generator spike as the fourth active SDL2 family after Core, Image, and GFX. TTF is the first satellite that exercises both major cross-family mechanisms introduced during Item 1: a satellite-owned opaque handle (`TTF_Font`) and retained C `long` ABI surface (`TTF_OpenFontIndex*`, `TTF_FontFaces`).

The target is Layer 1 raw ABI only. Public typed wrappers, friendly overloads, error redirect helpers, font asset policy, and package integration remain out of scope.

## 2. Evidence Summary

### 2.1 Live Header Audit

Source: `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_ttf.h` from SDL2_ttf 2.24.0.

| Finding | Evidence |
|---------|----------|
| Version | `SDL_TTF_MAJOR_VERSION 2`, `MINOR 24`, `PATCHLEVEL 0` at `SDL_ttf.h:50-52` |
| Public functions | 88 `extern ... DECLSPEC` declarations |
| Opaque handle | `typedef struct TTF_Font TTF_Font;` at `SDL_ttf.h:165` |
| C `long` parameters | `TTF_OpenFontIndex*` at `SDL_ttf.h:231`, `:286`, `:337`, `:400` |
| C `long` return | `TTF_FontFaces` at `SDL_ttf.h:689` |
| Non-deprecated no-`SDLCALL` functions | `TTF_GetFontKerningSizeGlyphs` `:2168`, `TTF_GetFontKerningSizeGlyphs32` `:2185`, `TTF_SetFontSDF` `:2204`, `TTF_GetFontSDF` `:2217` |
| Deprecated functions | `TTF_GetFontKerningSize` `:2145`, `TTF_SetDirection` `:2268`, `TTF_SetScript` `:2291` |
| Error macros | `TTF_SetError -> SDL_SetError` at `:2224`, `TTF_GetError -> SDL_GetError` at `:2231` |
| Core type dependencies | `SDL_RWops*` in 4 open functions; `SDL_Surface*` returns in 32 render functions; also `SDL_Color`, `SDL_bool`, `SDL_version`, `Uint16`, `Uint32` |
| Platform-conditioned API/layout | None found; only header guards, C++ guards, version gates, and fallback macro guards |

### 2.2 SDL Wiki Cross-Check

The SDL wiki confirms `TTF_Font` is opaque data and that `TTF_OpenFontIndex` / `TTF_FontFaces` use C `long` in their public signatures.

References:

- `https://wiki.libsdl.org/SDL2_ttf/TTF_Font`
- `https://wiki.libsdl.org/SDL2_ttf/TTF_OpenFontIndex`
- `https://wiki.libsdl.org/SDL2_ttf/TTF_FontFaces`

### 2.3 Peer Binding Cross-Check

SDL2-CS (`external/sdl2-cs/src/SDL2_ttf.cs`) is useful as compatibility evidence but not as ABI authority:

- Uses raw `IntPtr` for `TTF_Font` everywhere.
- Binds `TTF_OpenFontIndex*` using C# `long`, which is ABI-wrong on Windows because C `long` is 32-bit on LLP64.
- Binds `TTF_FontFaces` as `IntPtr` with an inline comment: `IntPtr is actually a C long! This ignores Win64!`.
- Includes `TTF_GetFontKerningSizeGlyphs` and `TTF_GetFontKerningSizeGlyphs32` with `CallingConvention.Cdecl`.
- Omits `TTF_SetFontSDF` and `TTF_GetFontSDF`.
- Implements `TTF_GetError` / `TTF_SetError` as C# redirects to Core SDL error APIs, not as native imports.

ppy/SDL3-CS confirms the general ClangSharp pattern but SDL3_ttf has a different API shape. It is useful for two points only: opaque `TTF_Font` remains a generated opaque type, and generated imports use explicit Cdecl.

Alimer references in this repo cover Core/Image only and provide no useful TTF-specific precedent.

## 3. Design

### 3.1 Family Identity

| Field | Value |
|-------|-------|
| Config key | `"ttf"` |
| C# namespace | `SDL2.Ttf` |
| Internal raw ABI class | `SDL_ttfNative` |
| Native library | `SDL2_ttf` |
| Managed project | `Janset.SDL2.Ttf` |
| Owner mode | `true` (`TTF_Font` is satellite-owned) |

These identity fields already exist in `config/family-config.json`. Item 4 activates them by adding the header inventory and project/RSP files.

### 3.2 Header Inventory

TTF has one functional header:

```json
"headers": [
  { "order": 10, "name": "SDL_ttf.h" }
]
```

No platform-sensitive headers are added. `SDL_ttf.h` has no platform-conditioned API or layout.

### 3.3 Family RSP (`rsp/sdl2-ttf.rsp`)

Create a family RSP for raw ABI exclusions only. Do not include `--libraryPath` or `--methodClassName`; Iteration 2 moved family identity into `config/family-config.json`.

```rsp
# TTF_SetError / TTF_GetError are macro shortcuts that point at SDL_SetError
# / SDL_GetError in the core class. ClangSharp can't cross the family
# methodClassName boundary, so emit them as part of the SDL2_ttf friendly
# wrapper layer later rather than as raw bindings.
#
# The three deprecated functions are excluded from Layer 1 rather than emitted
# as raw imports. TTF_GetFontKerningSize also lacks SDLCALL, while
# TTF_SetDirection / TTF_SetScript are superseded by per-font APIs.
--exclude
TTF_SetError
TTF_GetError
TTF_GetFontKerningSize
TTF_SetDirection
TTF_SetScript
```

### 3.4 No-SDLCALL Calling Convention Decision

Do not add `rsp/per-header/SDL_ttf.rsp` initially.

Rationale:

1. ClangSharp's own tests show normal C functions emit `[DllImport(..., CallingConvention = CallingConvention.Cdecl)]` by default in compatible Windows codegen.
2. GFX already proved declarations without SDL's normal `extern DECLSPEC ... SDLCALL` shape still emit Cdecl in this spike.
3. The postprocess `libraryimport` pass converts Modern output to `[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]` based on the existing `[DllImport]` calling convention.

Verification remains mandatory: generated Compat output for `TTF_GetFontKerningSizeGlyphs`, `TTF_GetFontKerningSizeGlyphs32`, `TTF_SetFontSDF`, and `TTF_GetFontSDF` must contain `CallingConvention = CallingConvention.Cdecl`; generated Modern output must contain `CallConvCdecl` after `libraryimport`. If this gate fails, add a per-header RSP using ClangSharp `--with-callconv` for those four symbols as a follow-up correction.

### 3.5 Opaque Handle Policy

`TTF_Font` is a TTF-owned Pattern B opaque handle. Owner mode must emit `Generated/{Compat,Modern}/Handles.g.cs` in namespace `SDL2.Ttf` with `public readonly partial struct TTF_Font`.

Core-owned handle and data types remain consumed through `ProjectReference` to `Janset.SDL2.Core`:

- `SDL_RWops` in RWops open functions
- `SDL_Surface` returns from render functions
- `SDL_Color`, `SDL_version`, `SDL_bool`, and scalar typedefs

Do not add `TTF_Font` to Core. Do not emit `TTF_Font` in consumer projects.

### 3.6 C `long` Policy

The existing `clong-dispatch` postprocess applies to TTF because `families.ttf.clong_methods[]` is already populated with:

- `TTF_OpenFontIndex`
- `TTF_OpenFontIndexRW`
- `TTF_OpenFontIndexDPI`
- `TTF_OpenFontIndexDPIRW`
- `TTF_FontFaces`

Expected output shape:

- Modern: `CLong` parameters/return where native type is C `long`, with `[LibraryImport]` and `CallConvCdecl`.
- Compat: managed dispatcher plus private Win32/Unix64 `[DllImport]` helpers, matching the existing C `long` strategy from `ClongDualDispatchRewriter`.

No `ClongDualDispatchRewriter` code change is expected. The spec gate is generated-output verification.

### 3.7 C# Project (`src/Janset.SDL2.Ttf/`)

Create the project from the GFX/Image satellite pattern:

- Multi-target `net10.0;net9.0;net8.0;netstandard2.0;net462`.
- `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`.
- `<RootNamespace>SDL2</RootNamespace>`.
- `ProjectReference` to `../Janset.SDL2.Core/Janset.SDL2.Core.csproj`.
- TFM split: legacy TFMs compile `Generated/Compat/**/*.cs`; modern TFMs compile `Generated/Modern/**/*.cs`.
- Include `Support/**/*.cs` for every TFM.
- Add `System.Memory` for legacy TFMs.

Support files required:

- `Support/DisableRuntimeMarshalling.cs` - same Pattern B assembly contract as Core/Image/GFX.
- `Support/NativeTypeNameAttribute.cs` - required because the attribute is internal per assembly; GFX proved every satellite needs a local copy.

No package/project integration outside the spike is included.

### 3.8 Orchestrator Activation

After explicit TTF generation is verified, update `selected_families("all")` from:

```python
return ["core", "image", "gfx"]
```

to:

```python
return ["core", "image", "gfx", "ttf"]
```

Update the Python self-test expectation accordingly. Do not activate Mixer.

### 3.9 Solution Wiring

Add `src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj` to `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx` under `/src/`, after GFX.

### 3.10 Oracle

`oracle.cs` already carries `FamilyConfigs.Sdl2Ttf` and `KnownFamilies` includes the TTF family. Item 4 still treats oracle verification as an explicit implementation concern:

- Confirm `FamilyConfigs.Sdl2Ttf` paths match the TTF project/generation paths after the project is created.
- Confirm `oracle.cs --family sdl2-ttf --write-report` produces a real TTF row instead of a missing generated-output row.
- Do not refactor `oracle.cs`; any required change should be a minimal data/path correction only.

### 3.11 Peer Visual Checks

After generation/build/oracle passes, perform manual peer cross-checks as the final review items:

- SDL2-CS: compare generated TTF function presence and notable divergences against `external/sdl2-cs/src/SDL2_ttf.cs`. Expected divergences include typed `TTF_Font` Pattern B instead of `IntPtr`, ABI-correct C `long` handling instead of SDL2-CS's `long`/`IntPtr` shortcuts, no raw imports for error macros, and inclusion of modern SDF functions if ClangSharp emits them.
- Silk.NET: if a local reference or reachable source exists, do a visual check for SDL_ttf coverage, handle representation, flags/macro treatment, and C `long` handling. If no SDL_ttf peer surface is found, record that explicitly in the implementation notes instead of treating it as a blocker.

These checks are last because the generated output needs to exist first. They are evidence/compatibility review, not binding authority.

### 3.12 Tests and Runtime Smoke

Item 4's first-level gates are generation, build, oracle, and self-tests. Runtime ABI smoke for real TTF calls is desirable but needs a redistributable font asset and native SDL2_ttf dependency copy strategy. Do not block Layer 1 spec implementation on adding a font asset unless Deniz explicitly expands the scope.

Minimum runtime smoke candidates for a follow-up test slice:

1. `TTF_Init` / `TTF_WasInit` / `TTF_Quit` lifecycle.
2. `TTF_OpenFont` + `TTF_CloseFont` using a small redistributable `.ttf` test asset.
3. `TTF_OpenFontIndex(..., 0)` parameter-position C `long` smoke.
4. `TTF_FontFaces` return-position C `long` smoke.

## 4. Non-Goals

- No production `src/Janset.SDL2.Ttf` package integration.
- No `build/manifest.json`, `vcpkg.json`, Cake, CI, or package project changes.
- No generator schema change.
- No postprocess rewriter changes unless generated output proves a correctness gap.
- No Layer 2 typed public API or Layer 3 friendly overloads.
- No C# redirects for `TTF_GetError` / `TTF_SetError`; those belong to a later companion-helper policy.
- No function-like macro helpers for `SDL_TTF_VERSION`, `TTF_VERSION`, `SDL_TTF_VERSION_ATLEAST`, or legacy render aliases.
- No Mixer activation.

## 5. Verification Gates

### 5.1 Header and RSP Activation

1. `families.ttf.headers[]` contains exactly `SDL_ttf.h` with order `10`.
2. `rsp/sdl2-ttf.rsp` exists and excludes only the two error macros plus three deprecated functions.
3. No `rsp/per-header/SDL_ttf.rsp` exists unless generated callconv output fails the Cdecl gate.

### 5.2 Generation

1. `python spikes/binding-generators/clangsharp/generate_bindings.py --family ttf --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` exits 0.
2. `Generated/Compat/SDL_ttf.g.cs` and `Generated/Modern/SDL_ttf.g.cs` are non-empty.
3. `Generated/Compat/Handles.g.cs` and `Generated/Modern/Handles.g.cs` are emitted and contain `TTF_Font`.
4. Excluded symbols are absent: `TTF_SetError`, `TTF_GetError`, `TTF_GetFontKerningSize`, `TTF_SetDirection`, `TTF_SetScript`.
5. Non-deprecated no-`SDLCALL` symbols are present: `TTF_GetFontKerningSizeGlyphs`, `TTF_GetFontKerningSizeGlyphs32`, `TTF_SetFontSDF`, `TTF_GetFontSDF`.

### 5.3 ABI Shape

1. Compat no-`SDLCALL` functions include `CallingConvention = CallingConvention.Cdecl`.
2. Modern no-`SDLCALL` functions include `CallConvCdecl` after `libraryimport`.
3. Modern C `long` signatures use `CLong` for `TTF_OpenFontIndex*` parameters and `TTF_FontFaces` return.
4. Compat C `long` signatures use dual-dispatch helpers rather than raw shared `long`/`nint` imports.
5. `TTF_Font*` parameters and returns are rewritten to by-value `TTF_Font` Pattern B handles where appropriate; double pointers remain pointer-shaped.
6. Core-owned handles/types resolve through the Core project, with no duplicate `SDL_Surface`, `SDL_RWops`, or other Core type declarations in TTF output.

### 5.4 Determinism and Regression

1. After activating TTF in `selected_families("all")`, `--family all --execute` regenerates Core/Image/GFX/TTF.
2. Core, Image, and GFX generated outputs remain byte-identical modulo CRLF noise: `git diff --ignore-cr-at-eol` must be empty for their `Generated/` roots.
3. `selected_families("all")` returns `['core', 'image', 'gfx', 'ttf']` and not Mixer.

### 5.5 Build and Evidence

1. `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj -c Release` succeeds with 0 warnings / 0 errors across 5 TFMs.
2. `dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release` succeeds with 0 warnings / 0 errors.
3. `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-gfx --family sdl2-ttf --family sdl2-mixer --write-report` writes a TTF evidence row; Mixer remains missing/dormant.
4. `python spikes/binding-generators/clangsharp/generate_bindings.py --self-test` passes.
5. `dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test` passes.
6. `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"` reports 0 issues after code/project/test changes.

### 5.6 Final Peer Visual Checks

1. SDL2-CS visual check is recorded with specific notes for `TTF_Font`, C `long`, no-`SDLCALL`, deprecated functions, and error macros.
2. Silk.NET visual check is recorded if a SDL_ttf peer surface is available; otherwise the implementation notes state that no useful Silk.NET SDL_ttf precedent was found.

## 6. Exit Criteria

Item 4 is closed when:

1. `families.ttf.headers[]` is populated with `SDL_ttf.h`.
2. `rsp/sdl2-ttf.rsp` exists with the approved exclusions.
3. `src/Janset.SDL2.Ttf/` exists with project, support files, and generated Compat/Modern output.
4. `Handles.g.cs` is emitted for TTF owner mode and contains `TTF_Font` in namespace `SDL2.Ttf`.
5. C `long` and no-`SDLCALL` ABI gates pass in generated output.
6. TTF is added to the spike solution and `selected_families("all")`.
7. Core/Image/GFX generated outputs are regress-free after aggregate generation.
8. Build, oracle, self-tests, and slopwatch gates pass.
9. Final SDL2-CS and Silk.NET visual-check notes are recorded.
