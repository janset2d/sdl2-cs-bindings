# Item 5 - SDL2_mixer Layer 1 Raw ABI: Design Spec

**Date:** 2026-05-28
**Status:** Closed 2026-05-28 — Layer 1 raw ABI generation complete. Callback lifetime/rooting and runtime audio smoke remain deferred follow-ups.
**Branch:** `spike/binding-autogen-sdl2-gfx`
**Depends on:** Item 4 (SDL2_ttf Layer 1) - code landed in `1153b74`
**Unblocks:** Layer 2 public typed low-level API planning across Core, Image, GFX, TTF, and Mixer

## 1. Problem Statement

Add SDL2_mixer to the ClangSharp + Roslyn postprocess binding-generator spike as the fifth active SDL2 family after Core, Image, GFX, and TTF. Mixer is the highest-risk satellite in this expansion wave because it introduces callback typedefs and callback-accepting APIs that can outlive the call that registers them.

The target is Layer 1 raw ABI only. Item 5 must prove generated Mixer signatures are ABI-correct, compile across the spike TFM matrix, and do not regress existing active families. Public typed wrappers, friendly overloads, callback lifetime/rooting helpers, audio-device runtime smoke, package integration, and production source promotion remain out of scope.

## 2. Evidence Summary

### 2.1 Live Header Audit

Source: `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_mixer.h` from SDL2_mixer 2.8.1.

| Finding | Evidence |
|---------|----------|
| Version | `SDL_MIXER_MAJOR_VERSION 2`, `MINOR 8`, `PATCHLEVEL 1` at `SDL_mixer.h:47-49` |
| Public functions | 97 `extern DECLSPEC ... SDLCALL` declarations |
| All functions use SDLCALL | Every public function declaration found by `rg "extern DECLSPEC.*SDLCALL" SDL_mixer.h`; no no-SDLCALL anomaly like TTF |
| Opaque handle | `typedef struct Mix_Music Mix_Music;` at `SDL_mixer.h:269` |
| Transparent struct | `Mix_Chunk` is a 4-field POD struct at `SDL_mixer.h:231-236` |
| Enums | `MIX_InitFlags` bitmask at `SDL_mixer.h:109-118`; `Mix_Fading` at `:241-245`; `Mix_MusicType` at `:250-264` |
| Callback typedefs | 6 total: `Mix_MixCallback`, `Mix_MusicFinishedCallback`, `Mix_ChannelFinishedCallback`, `Mix_EffectFunc_t`, `Mix_EffectDone_t`, `Mix_EachSoundFontCallback` |
| Callback-typed functions | 7 functions / 8 callback parameters: `Mix_SetPostMix`, `Mix_HookMusic`, `Mix_HookMusicFinished`, `Mix_ChannelFinished`, `Mix_RegisterEffect` (two callback params), `Mix_UnregisterEffect`, `Mix_EachSoundFont` |
| Callback lifetime risk | Header documents callbacks firing inside SDL_mixer audio callbacks or arbitrary background thread contexts, e.g. `SDL_mixer.h:1127-1130`, `:1168-1171`, `:1210-1221`, `:1247-1257` |
| Error macros | `Mix_SetError`, `Mix_GetError`, `Mix_ClearError`, `Mix_OutOfMemory` expand to Core SDL error helpers at `SDL_mixer.h:2803-2822` |
| Legacy aliases | `MIX_MAJOR_VERSION`, `MIX_MINOR_VERSION`, `MIX_PATCHLEVEL`, `MIX_VERSION` at `SDL_mixer.h:63-66` |
| Object-like value macros | `MIX_CHANNELS`, `MIX_DEFAULT_FREQUENCY`, `MIX_DEFAULT_FORMAT`, `MIX_DEFAULT_CHANNELS`, `MIX_MAX_VOLUME`, `MIX_CHANNEL_POST`, `MIX_EFFECTSMAXSPEED` |
| Function-like version macros | `SDL_MIXER_VERSION(X)`, `MIX_VERSION(X)`, `SDL_MIXER_VERSION_ATLEAST(X,Y,Z)` are not Layer 1 raw ABI declarations |
| C `long` / `wchar_t` / variadics / unions / platform-conditioned API | None found in the pinned header |

### 2.2 SDL Wiki Cross-Check

The SDL wiki confirms the two Mixer-owned type shapes that matter most for Item 5:

- `Mix_Music` is declared as `typedef struct Mix_Music Mix_Music;` and described as the internal format for a music chunk interpreted via codecs.
- `Mix_Chunk` is a public struct with exactly `allocated`, `abuf`, `alen`, and `volume` fields.
- `Mix_RegisterEffect` takes `Mix_EffectFunc_t` and `Mix_EffectDone_t` callback parameters and documents audio-thread callback constraints.

References:

- `https://wiki.libsdl.org/SDL2_mixer/Mix_Music`
- `https://wiki.libsdl.org/SDL2_mixer/Mix_Chunk`
- `https://wiki.libsdl.org/SDL2_mixer/Mix_RegisterEffect`

### 2.3 Peer Binding Cross-Check

SDL2-CS (`external/sdl2-cs/src/SDL2_mixer.cs`) is useful as compatibility evidence but not as ABI authority:

- Uses `IntPtr` comments for `Mix_Music*` and `Mix_Chunk*` in function signatures instead of typed handles.
- Defines `MIX_Chunk` as a managed struct with `IntPtr abuf`, `uint alen`, and `byte volume`, matching the transparent POD concept.
- Uses `[Flags]` on `MIX_InitFlags`, but its constant roster is older than the pinned 2.8.1 header and omits newer flags such as `MIX_INIT_WAVPACK`.
- Uses `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` delegates for Mixer callbacks, with SDL2-CS-specific delegate names for some typedefs (`MixFuncDelegate`, `MusicFinishedDelegate`, `ChannelFinishedDelegate`, `SoundFontDelegate`).
- Implements `Mix_GetError`, `Mix_SetError`, and `Mix_ClearError` as C# redirects to Core SDL error methods. It does not expose `Mix_OutOfMemory`.
- Provides convenience string wrappers and manual `SDL_MIXER_VERSION(out SDL_version)` helper behavior that belongs to higher layers in Janset, not Layer 1 raw ABI.

ppy/SDL3-CS provides current ClangSharp callback evidence but not SDL2_mixer API compatibility evidence because SDL3_mixer has a substantially different API. Its generated SDL3_mixer output uses raw ClangSharp shapes such as `delegate* unmanaged[Cdecl]<...>` for callback-accepting functions, which supports the expectation that Modern Layer 1 callback parameters can be generated without a custom postprocess rewriter.

## 3. Design

### 3.1 Family Identity

| Field | Value |
|-------|-------|
| Config key | `"mixer"` |
| C# namespace | `SDL2.Mixer` |
| Internal raw ABI class | `SDL_mixerNative` |
| Native library | `SDL2_mixer` |
| Managed project | `Janset.SDL2.Mixer` |
| Owner mode | `true` (`Mix_Music` is satellite-owned) |

These identity fields already exist in `config/family-config.json`. Item 5 activates them by adding the Mixer header inventory and project/RSP files.

### 3.2 Header Inventory

Mixer has one functional header:

```json
"headers": [
  { "order": 10, "name": "SDL_mixer.h" }
]
```

No platform-sensitive headers are added. `SDL_mixer.h` has no platform-conditioned public API or layout.

### 3.3 Family RSP (`rsp/sdl2-mixer.rsp`)

Create a family RSP for raw ABI exclusions only. Do not include `--libraryPath` or `--methodClassName`; Iteration 2 moved family identity into `config/family-config.json`.

```rsp
# Mix_SetError / Mix_GetError / Mix_ClearError / Mix_OutOfMemory are
# macro shortcuts that point at SDL core error helpers. ClangSharp can't
# cross the family methodClassName boundary, so emit these as part of a
# later SDL2_mixer public helper layer rather than as raw bindings.
--exclude
Mix_SetError
Mix_GetError
Mix_ClearError
Mix_OutOfMemory
```

Do not create `rsp/per-header/SDL_mixer.rsp` initially. The header has no foreign types, platform-conditioned declarations, C `long`, `wchar_t`, variadics, or callconv anomaly. Add a per-header RSP only if generated output proves a concrete correctness gap.

### 3.4 Opaque Handle Policy

`Mix_Music` is a Mixer-owned Pattern B opaque handle. Owner mode must emit `Generated/{Compat,Modern}/Handles.g.cs` in namespace `SDL2.Mixer` with `public readonly partial struct Mix_Music`.

Core-owned handle and data types remain consumed through `ProjectReference` to `Janset.SDL2.Core`:

- `SDL_RWops` in RWops loading APIs.
- `SDL_version` in `Mix_Linked_Version`.
- `SDL_bool` in decoder query APIs.
- `Uint8`, `Uint16`, `Uint32`, `Sint16`, and audio constants through Core typedef/macro output.

Do not add `Mix_Music` to Core. Do not force-opaque `Mix_Chunk`; it is a public transparent Mixer-owned data struct, not an opaque handle.

### 3.5 `Mix_Chunk` Struct Policy

`Mix_Chunk` must remain a transparent POD struct with four fields:

| Field | Native type | Expected managed raw type |
|-------|-------------|---------------------------|
| `allocated` | `int` | `int` |
| `abuf` | `Uint8*` | `byte*` |
| `alen` | `Uint32` | `uint` |
| `volume` | `Uint8` | `byte` |

Expected layout is sequential with pointer-size-dependent padding: 12 bytes on x86, 24 bytes on Windows/Linux/macOS x64 when represented with a pointer field and natural alignment. Item 5 does not need a runtime layout test gate, but generated output must not treat `Mix_Chunk` as opaque and must not emit a union or explicit-layout lie.

### 3.6 Callback Policy

Mixer has six callback typedefs, all declared with `SDLCALL`:

| Typedef | Native signature intent |
|---------|-------------------------|
| `Mix_MixCallback` | `void(void* udata, Uint8* stream, int len)` |
| `Mix_MusicFinishedCallback` | `void(void)` |
| `Mix_ChannelFinishedCallback` | `void(int channel)` |
| `Mix_EffectFunc_t` | `void(int chan, void* stream, int len, void* udata)` |
| `Mix_EffectDone_t` | `void(int chan, void* udata)` |
| `Mix_EachSoundFontCallback` | `int(const char*, void*)` |

Expected Layer 1 output:

- Compat: delegate declarations with `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`.
- Modern: callback-typed parameters expressed as `delegate* unmanaged[Cdecl]<...>`.
- `void*` userdata remains pointer-shaped (`void*`, `nint`, or `IntPtr` depending on ClangSharp backend shape already accepted by this spike).
- No lifetime/rooting helper is generated in Layer 1.

Callback-consuming functions requiring manual verification:

- `Mix_SetPostMix(Mix_MixCallback mix_func, void* arg)`
- `Mix_HookMusic(Mix_MixCallback mix_func, void* arg)`
- `Mix_HookMusicFinished(Mix_MusicFinishedCallback music_finished)`
- `Mix_ChannelFinished(Mix_ChannelFinishedCallback channel_finished)`
- `Mix_RegisterEffect(int chan, Mix_EffectFunc_t f, Mix_EffectDone_t d, void* arg)`
- `Mix_UnregisterEffect(int channel, Mix_EffectFunc_t f)`
- `Mix_EachSoundFont(Mix_EachSoundFontCallback function, void* data)`

`Mix_UnregisterAllEffects` has no callback-typed parameter and is not part of the callback signature gate.

### 3.7 Callback Lifetime Deferral

Mixer callbacks can persist beyond the registration call and may fire from SDL_mixer's audio callback or a background thread. This creates a real managed lifetime/rooting problem, but it is not a Layer 1 raw ABI generation problem.

Item 5 records this as deferred follow-up work already tracked in:

- `spikes/binding-generators/docs/satellite-expansion-roadmap.md` §`Deferred Follow-up: Mixer Callback Lifetime Smoke`.
- `spikes/binding-generators/docs/next-iteration-plan.md` §`Layer 2 / Layer 3 Follow-ups`.

Deferred policy must later define:

- Compat delegate rooting for the full native registration lifetime.
- Modern `[UnmanagedCallersOnly]` / `delegate* unmanaged[Cdecl]` authoring rules.
- Dummy-audio deterministic runtime smoke setup.
- Cleanup/unregistration rules for persistent callbacks.

Layer 1 closure must not add callback-rooting abstractions, helper classes, or audio runtime smoke unless Deniz explicitly expands the scope.

### 3.8 Constants, Enums, and Macros

`MIX_InitFlags` must receive `[Flags]` through the existing `flags-detect` suffix rule. `Mix_Fading` and `Mix_MusicType` must not receive `[Flags]`.

Object-like value macros should be kept when ClangSharp resolves them safely:

- `SDL_MIXER_MAJOR_VERSION`, `SDL_MIXER_MINOR_VERSION`, `SDL_MIXER_PATCHLEVEL`.
- `MIX_MAJOR_VERSION`, `MIX_MINOR_VERSION`, `MIX_PATCHLEVEL`, matching the TTF legacy alias precedent.
- `SDL_MIXER_COMPILEDVERSION` if the evaluator resolves `SDL_VERSIONNUM(...)`, matching Image's compiled-version precedent.
- `MIX_CHANNELS`, `MIX_DEFAULT_FREQUENCY`, `MIX_DEFAULT_FORMAT`, `MIX_DEFAULT_CHANNELS`, `MIX_MAX_VOLUME`, `MIX_CHANNEL_POST`, `MIX_EFFECTSMAXSPEED`.

Function-like macros are not Layer 1 raw ABI declarations:

- `SDL_MIXER_VERSION(X)`.
- `MIX_VERSION(X)`.
- `SDL_MIXER_VERSION_ATLEAST(X,Y,Z)`.

If these are absent from generated output, that is expected. If the generation report can classify skipped function-like macros, it should record them as helper candidates rather than silently treating them as generated API.

### 3.9 C# Project (`src/Janset.SDL2.Mixer/`)

Create the project from the TTF/GFX/Image satellite pattern:

- Multi-target `net10.0;net9.0;net8.0;netstandard2.0;net462`.
- `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`.
- `<RootNamespace>SDL2</RootNamespace>`.
- `ProjectReference` to `../Janset.SDL2.Core/Janset.SDL2.Core.csproj`.
- TFM split: legacy TFMs compile `Generated/Compat/**/*.cs`; modern TFMs compile `Generated/Modern/**/*.cs`.
- Include `Support/**/*.cs` for every TFM.
- Add `System.Memory` for legacy TFMs.

Support files required:

- `Support/DisableRuntimeMarshalling.cs` - same Pattern B assembly contract as Core/Image/GFX/TTF.
- `Support/NativeTypeNameAttribute.cs` - required because the attribute is internal per assembly.

No package/project integration outside the spike is included.

### 3.10 Orchestrator Activation

After explicit Mixer generation is verified, update `selected_families("all")` from:

```python
return ["core", "image", "gfx", "ttf"]
```

to:

```python
return ["core", "image", "gfx", "ttf", "mixer"]
```

Update the Python self-test expectation accordingly. Do not activate SDL2_net or SDL3.

### 3.11 Solution Wiring

Add `src/Janset.SDL2.Mixer/Janset.SDL2.Mixer.csproj` to `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx` under `/src/`, after TTF.

### 3.12 Oracle

`oracle.cs` already accepts `sdl2-mixer` as a family in the five-family command path. Item 5 treats oracle verification as an explicit implementation concern:

- Confirm Mixer paths match the project/generation paths after the project is created.
- Confirm `oracle.cs --family sdl2-mixer --write-report` produces a real Mixer row instead of a missing generated-output row.
- Expect possible evidence-tool gaps around callback typedef inspection before assuming generated ABI is wrong.
- Do not refactor `oracle.cs`; any required change should be a minimal data/path/evidence correction only.

### 3.13 Peer Visual Checks

After generation/build/oracle passes, perform manual peer cross-checks as final review items:

- SDL2-CS: compare generated Mixer function presence and notable divergences against `external/sdl2-cs/src/SDL2_mixer.cs`. Expected divergences include typed `Mix_Music` Pattern B instead of `IntPtr`, generated `Mix_Chunk` raw pointer field shape instead of SDL2-CS `IntPtr`, canonical callback typedef names where ClangSharp emits them, no raw imports for error macros, and newer constants/enums from SDL2_mixer 2.8.1.
- ppy/SDL3-CS: use only as ClangSharp callback-shape evidence. Do not treat SDL3_mixer as an SDL2_mixer API oracle because the APIs differ substantially.
- Silk.NET: Item 4 found Silk.NET.SDL 2.23.0 effectively core-only for this purpose. Do not block Item 5 on finding a Silk.NET SDL2_mixer peer surface; record if no useful peer surface is available.

These checks are evidence/compatibility review, not binding authority.

### 3.14 Tests and Runtime Smoke

Item 5's first-level gates are generation, ABI-shape inspection, build, oracle, self-tests, determinism, and slopwatch. Runtime callback/audio smoke is valuable but deferred until callback lifetime/rooting policy, dummy-audio setup, and audio fixture policy are designed.

Deferred runtime smoke candidates:

1. `Mix_OpenAudio` / `Mix_CloseAudio` lifecycle under dummy audio.
2. `Mix_LoadWAV` / `Mix_FreeChunk` with a tiny deterministic WAV fixture.
3. `Mix_SetPostMix` or `Mix_RegisterEffect` callback roundtrip after rooting policy exists.
4. `Mix_EachSoundFont` callback shape smoke if deterministic test data can be provided without host MIDI assumptions.

## 4. Non-Goals

- No production `src/Janset.SDL2.Mixer` package integration.
- No `build/manifest.json`, `vcpkg.json`, Cake, CI, or package project changes.
- No generator schema change.
- No postprocess rewriter changes unless generated output proves a correctness gap.
- No Layer 2 typed public API or Layer 3 friendly overloads.
- No callback-rooting abstractions, callback registries, managed lifetime helpers, or audio runtime smoke.
- No C# redirects for `Mix_GetError`, `Mix_SetError`, `Mix_ClearError`, or `Mix_OutOfMemory`; those belong to a later companion-helper policy.
- No function-like macro helpers for `SDL_MIXER_VERSION`, `MIX_VERSION`, or `SDL_MIXER_VERSION_ATLEAST`.
- No SDL2_net or SDL3 activation.

## 5. Verification Gates

### 5.1 Header and RSP Activation

1. `families.mixer.headers[]` contains exactly `SDL_mixer.h` with order `10`.
2. `rsp/sdl2-mixer.rsp` exists and excludes only the four error macros listed in §3.3.
3. No `rsp/per-header/SDL_mixer.rsp` exists unless generated output proves a concrete ABI gap.

### 5.2 Generation

1. `python spikes/binding-generators/clangsharp/generate_bindings.py --family mixer --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` exits 0.
2. `Generated/Compat/SDL_mixer.g.cs` and `Generated/Modern/SDL_mixer.g.cs` are non-empty.
3. `Generated/Compat/Handles.g.cs` and `Generated/Modern/Handles.g.cs` are emitted and contain `Mix_Music` in namespace `SDL2.Mixer`.
4. Excluded symbols are absent: `Mix_SetError`, `Mix_GetError`, `Mix_ClearError`, `Mix_OutOfMemory`.
5. All 97 public `extern DECLSPEC ... SDLCALL` function declarations are represented unless a generated report records an accepted explicit exclusion.

### 5.3 ABI Shape

1. Compat callback typedef declarations include `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` for all six callback typedefs.
2. Modern callback-typed parameters use `delegate* unmanaged[Cdecl]<...>` for all callback-consuming functions.
3. `Mix_RegisterEffect` carries both callback-typed parameters (`Mix_EffectFunc_t f`, `Mix_EffectDone_t d`) in Compat and Modern output.
4. `Mix_Music*` parameters and returns are rewritten to by-value `Mix_Music` Pattern B handles where appropriate; double pointers, if any appear later, remain pointer-shaped.
5. `Mix_Chunk` remains a transparent struct with `allocated`, `abuf`, `alen`, and `volume` fields; it is not emitted as a Pattern B handle.
6. `MIX_InitFlags` has `[Flags]`; `Mix_Fading` and `Mix_MusicType` do not.
7. Core-owned handles/types resolve through the Core project, with no duplicate `SDL_RWops`, `SDL_version`, `SDL_bool`, or other Core type declarations in Mixer output.

### 5.4 Determinism and Regression

1. After activating Mixer in `selected_families("all")`, `--family all --execute` regenerates Core/Image/GFX/TTF/Mixer.
2. Core, Image, GFX, and TTF generated outputs remain byte-identical modulo CRLF noise: `git diff --ignore-cr-at-eol` must be empty for their `Generated/` roots.
3. `selected_families("all")` returns `['core', 'image', 'gfx', 'ttf', 'mixer']`.
4. No SDL2_net or SDL3 family is activated.

### 5.5 Build and Evidence

1. `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Janset.SDL2.Mixer.csproj -c Release` succeeds with 0 warnings / 0 errors across 5 TFMs.
2. `dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release` succeeds with 0 warnings / 0 errors.
3. `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-gfx --family sdl2-ttf --family sdl2-mixer --write-report` writes a real Mixer evidence row.
4. `python spikes/binding-generators/clangsharp/generate_bindings.py --self-test` passes.
5. `dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test` passes.
6. `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"` reports 0 issues after code/project/test changes.

### 5.6 Final Peer Visual Checks

1. SDL2-CS visual check is recorded with notes for `Mix_Music`, `Mix_Chunk`, callback typedefs/functions, `[Flags]`, error macros, and stale peer constants.
2. ppy/SDL3-CS visual check is recorded only for callback generation shape, with an explicit note that SDL3_mixer is not an SDL2 API oracle.
3. Silk.NET visual check is recorded if a SDL2_mixer peer surface is available; otherwise implementation notes state that no useful Silk.NET SDL2_mixer precedent was found.

## 6. Exit Criteria

Item 5 is closed when:

1. `families.mixer.headers[]` is populated with `SDL_mixer.h`.
2. `rsp/sdl2-mixer.rsp` exists with approved error-macro exclusions.
3. `src/Janset.SDL2.Mixer/` exists with project, support files, and generated Compat/Modern output.
4. `Handles.g.cs` is emitted for Mixer owner mode and contains `Mix_Music` in namespace `SDL2.Mixer`.
5. Callback typedef/function-parameter ABI gates pass in generated Compat and Modern output.
6. `Mix_Chunk`, `MIX_InitFlags`, `Mix_Fading`, and `Mix_MusicType` shape gates pass.
7. Mixer is added to the spike solution and `selected_families("all")`.
8. Core/Image/GFX/TTF generated outputs are regress-free after aggregate generation.
9. Build, oracle, self-tests, and slopwatch gates pass.
10. Final SDL2-CS, ppy/SDL3-CS, and Silk.NET visual-check notes are recorded.
11. Callback lifetime/rooting and deterministic callback runtime smoke remain tracked as deferred follow-up work, not Layer 1 closure blockers.
