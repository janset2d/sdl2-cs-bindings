# SDL2_mixer Binding Generation — Header Deep-Dive Analysis

**Date:** 2026-05-25
**Target:** SDL2_mixer v2.8.1 (installed header from `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_mixer.h`, 2831 lines)
**Status:** Research artifact. No code written.

---

## 1. Function Inventory

**Total: 97 public `extern DECLSPEC` function declarations.** All use `SDLCALL` (`__cdecl`).

### Functions flagged for special attention

| Risk | Count | Names |
|---|---|---|
| **Callback params** | 7 functions, 8 callback-typed parameters | `Mix_SetPostMix`, `Mix_HookMusic`, `Mix_HookMusicFinished`, `Mix_ChannelFinished`, `Mix_RegisterEffect` (two callback parameters at SDL_mixer.h:1367), `Mix_UnregisterEffect`, `Mix_EachSoundFont` |
| **`double` return** | 5 functions | `Mix_GetMusicPosition`, `Mix_MusicDuration`, `Mix_GetMusicLoopStartTime`, `Mix_GetMusicLoopEndTime`, `Mix_GetMusicLoopLengthTime` |
| **`double` param** | 2 functions | `Mix_FadeInMusicPos`, `Mix_SetMusicPosition` |
| **`const char*` params** | 8 functions | `Mix_OpenAudioDevice`, `Mix_LoadWAV`, `Mix_LoadMUS`, `Mix_HasChunkDecoder`, `Mix_HasMusicDecoder`, `Mix_SetMusicCMD`, `Mix_SetSoundFonts`, `Mix_SetTimidityCfg` — handled by base.rsp `char=byte` |
| **`const char*` returns** | 9 functions | `Mix_GetChunkDecoder`, `Mix_GetMusicDecoder`, `Mix_GetMusicTitle`, `Mix_GetMusicTitleTag`, `Mix_GetMusicArtistTag`, `Mix_GetMusicAlbumTag`, `Mix_GetMusicCopyrightTag`, `Mix_GetSoundFonts`, `Mix_GetTimidityCfg` — return `byte*` after remap |
| **`SDL_bool` return** | 2 functions | `Mix_HasChunkDecoder`, `Mix_HasMusicDecoder` — base.rsp `--with-type SDL_bool=int` |

**Zero instances of:** C `long`, `unsigned long`, `wchar_t`, variadic (`...`), `FILE*`.

### Full Function List (grouped by category)

**Lifecycle & Init:** `Mix_Linked_Version`, `Mix_Init`, `Mix_Quit` (3)

**Audio Device:** `Mix_OpenAudio`, `Mix_OpenAudioDevice`, `Mix_PauseAudio`, `Mix_QuerySpec`, `Mix_AllocateChannels`, `Mix_CloseAudio` (6)

**Loading:** `Mix_LoadWAV_RW`, `Mix_LoadWAV`, `Mix_LoadMUS`, `Mix_LoadMUS_RW`, `Mix_LoadMUSType_RW`, `Mix_QuickLoad_WAV`, `Mix_QuickLoad_RAW` (7)

**Cleanup:** `Mix_FreeChunk`, `Mix_FreeMusic` (2)

**Decoder Info:** `Mix_GetNumChunkDecoders`, `Mix_GetChunkDecoder`, `Mix_HasChunkDecoder`, `Mix_GetNumMusicDecoders`, `Mix_GetMusicDecoder`, `Mix_HasMusicDecoder` (6)

**Music Meta:** `Mix_GetMusicType`, `Mix_GetMusicTitle`, `Mix_GetMusicTitleTag`, `Mix_GetMusicArtistTag`, `Mix_GetMusicAlbumTag`, `Mix_GetMusicCopyrightTag` (6)

**Callbacks (the ABI risk area):** `Mix_SetPostMix`, `Mix_HookMusic`, `Mix_HookMusicFinished`, `Mix_GetMusicHookData`, `Mix_ChannelFinished`, `Mix_RegisterEffect`, `Mix_UnregisterEffect`, `Mix_UnregisterAllEffects` (8)

**Effect Control:** `Mix_SetPanning`, `Mix_SetPosition`, `Mix_SetDistance`, `Mix_SetReverseStereo` (4)

**Channel Management:** `Mix_ReserveChannels`, `Mix_GroupChannel`, `Mix_GroupChannels`, `Mix_GroupAvailable`, `Mix_GroupCount`, `Mix_GroupOldest`, `Mix_GroupNewer` (7)

**Playback:** `Mix_PlayChannel`, `Mix_PlayChannelTimed`, `Mix_PlayMusic`, `Mix_FadeInMusic`, `Mix_FadeInMusicPos`, `Mix_FadeInChannel`, `Mix_FadeInChannelTimed` (7)

**Volume:** `Mix_Volume`, `Mix_VolumeChunk`, `Mix_VolumeMusic`, `Mix_GetMusicVolume`, `Mix_MasterVolume` (5)

**Halt/Fade:** `Mix_HaltChannel`, `Mix_HaltGroup`, `Mix_HaltMusic`, `Mix_ExpireChannel`, `Mix_FadeOutChannel`, `Mix_FadeOutGroup`, `Mix_FadeOutMusic`, `Mix_FadingMusic`, `Mix_FadingChannel` (9)

**Pause/Resume:** `Mix_Pause`, `Mix_Resume`, `Mix_Paused`, `Mix_PauseMusic`, `Mix_ResumeMusic`, `Mix_RewindMusic`, `Mix_PausedMusic` (7)

**Music Positioning:** `Mix_ModMusicJumpToOrder`, `Mix_StartTrack`, `Mix_GetNumTracks`, `Mix_SetMusicPosition`, `Mix_GetMusicPosition`, `Mix_MusicDuration`, `Mix_GetMusicLoopStartTime`, `Mix_GetMusicLoopEndTime`, `Mix_GetMusicLoopLengthTime` (9)

**State:** `Mix_Playing`, `Mix_PlayingMusic`, `Mix_GetChunk` (3)

**Config:** `Mix_SetMusicCMD`, `Mix_SetSynchroValue`, `Mix_GetSynchroValue`, `Mix_SetSoundFonts`, `Mix_GetSoundFonts`, `Mix_EachSoundFont`, `Mix_SetTimidityCfg`, `Mix_GetTimidityCfg` (8)

---

## 2. Type Inventory

### 2A. Opaque Handle: `Mix_Music`

```c
typedef struct Mix_Music Mix_Music;
```

Forward-declared only — no struct body. Pattern B opaque handle. **Satellite-owned** and listed under the `mixer` family in `opaque-handle-roster.json` schema 2.0, not under Core's family entry. Item 1 provides the owner-mode infrastructure; Item 5 validates it against real Mixer generation.

### 2B. Transparent Struct: `Mix_Chunk`

```c
typedef struct Mix_Chunk {
    int allocated;
    Uint8 *abuf;
    Uint32 alen;
    Uint8 volume;
} Mix_Chunk;
```

**CRITICAL FINDING: There is NO union.** Earlier speculation about a union field was wrong. Four simple fields:

| Field | C Type | Managed Type | Offset (x86) | Offset (x64) | Size |
|---|---|---|---|---|---|
| `allocated` | `int` | `int` | 0 | 0 | 4 |
| `abuf` | `Uint8 *` | `byte*` | 4 | 8 | 4/8 |
| `alen` | `Uint32` | `uint` | 8 | 16 | 4 |
| `volume` | `Uint8` | `byte` | 12 | 20 | 1 |

Total: 16 bytes (x86), 24 bytes (x64) with trailing padding. `[StructLayout(LayoutKind.Sequential)]` — no explicit layout needed. Standard POD struct.

### 2C. Enums

**`MIX_InitFlags`** — `[Flags]` enum, bitmask values (`MIX_INIT_FLAC=0x01, MOD=0x02, MP3=0x08, OGG=0x10, MID=0x20, OPUS=0x40, WAVPACK=0x80`). Int backing.

**`Mix_Fading`** — 3 values (`MIX_NO_FADING, MIX_FADING_OUT, MIX_FADING_IN`). Not `[Flags]`. Int backing.

**`Mix_MusicType`** — 13 values (`MUS_NONE, MUS_CMD, MUS_WAV, MUS_MOD, MUS_MID, MUS_OGG, MUS_MP3, MUS_MP3_MAD_UNUSED, MUS_FLAC, MUS_MODPLUG_UNUSED, MUS_OPUS, MUS_WAVPACK, MUS_GME`). Not `[Flags]`. Int backing.

### 2D. Callback Typedefs (All SDLCALL / __cdecl)

| Typedef | Signature |
|---|---|
| `Mix_MixCallback` | `void (SDLCALL *)(void *udata, Uint8 *stream, int len)` |
| `Mix_MusicFinishedCallback` | `void (SDLCALL *)(void)` |
| `Mix_ChannelFinishedCallback` | `void (SDLCALL *)(int channel)` |
| `Mix_EffectFunc_t` | `void (SDLCALL *)(int chan, void *stream, int len, void *udata)` |
| `Mix_EffectDone_t` | `void (SDLCALL *)(int chan, void *udata)` |
| `Mix_EachSoundFontCallback` | `int (SDLCALL *)(const char*, void*)` |

All 6 use `SDLCALL` which resolves to `__cdecl` on Windows. Modern codegen emits `delegate* unmanaged[Cdecl]<...>`, Compat emits delegate types with `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`.

### 2E. No Structs with Function-Pointer Fields

Unlike `SDL_RWops`, Mixer has no structs containing function pointer fields. All callbacks pass as parameters.

### 2F. No Unions

**Verified:** Zero unions in SDL_mixer.h.

---

## 3. ABI Risk Assessment

### Risk 1: Callback Delegate Contracts (HIGH but well-understood)

All 6 callback typedefs use `SDLCALL` → `__cdecl`. Required interop attributes well-documented. ClangSharp handles callback typedefs natively — `compatible-codegen` emits delegate types, `latest-codegen` emits `delegate*` function pointers. No postprocess step needed.

**Callback-lifetime-risk evidence anchors** (header-documented audio-thread / background-thread callback constraints — durable evidence for the four callback-typedef declaration sites; lifetime/rooting policy itself is deferred Layer 2 / Layer 3 work):

- `SDL_mixer.h:1127-1130` — `Mix_SetPostMix` callback-lifetime documentation.
- `SDL_mixer.h:1168-1171` — `Mix_HookMusic` callback-lifetime documentation.
- `SDL_mixer.h:1210-1221` — `Mix_HookMusicFinished` callback-lifetime documentation.
- `SDL_mixer.h:1247-1257` — `Mix_ChannelFinished` callback-lifetime documentation.

Layer 1 raw ABI generation only requires the `__cdecl` calling-convention shape; the lifetime/rooting policy (Compat delegate rooting + Modern `[UnmanagedCallersOnly]` / `delegate* unmanaged[Cdecl]` authoring rules + dummy-audio runtime smoke + cleanup/unregistration rules) is durable forward-work tracked in [`next-iteration-plan.md`](../../next-iteration-plan.md) §"Layer 2 / Layer 3 Follow-ups".

### Risk 2: Mix_Chunk Layout (NEGLIGIBLE)

No union. Clean POD struct. ClangSharp handles directly.

### Risk 3: `double` Parameters/Returns (NEGLIGIBLE)

`double` is blittable and IEEE 754 identical across platforms.

### Risk 4: C `long` (ZERO)

None found in any function signature.

### Risk 5: Platform-Conditioned Code (ZERO)

Only include guards, C++ extern wrappers, and version-gated defines. No platform-macro `#if` blocks.

---

## 4. Constants and Macros

### Error Macros (exclude from raw ABI)

`Mix_SetError`, `Mix_GetError`, `Mix_ClearError`, `Mix_OutOfMemory` — `#define` shortcuts to SDL core functions. Cannot cross `methodClassName` boundary. Exclude. See [sdl2-satellite-error-function-consolidation.md](sdl2-satellite-error-function-consolidation.md) for full cross-family analysis, peer comparison, and companion-helper policy path.

### Legacy Compatibility Aliases (keep where value-like)

`MIX_MAJOR_VERSION`, `MIX_MINOR_VERSION`, `MIX_PATCHLEVEL` — backward-compat value aliases over `SDL_MIXER_*` version macros. Keep them when ClangSharp resolves them, matching the SDL2_ttf alias precedent.

`MIX_VERSION(X)` is function-like and should be skipped/reported with the other version helper macros rather than excluded in the family RSP.

### Version Macros (keep)

`SDL_MIXER_MAJOR_VERSION` (2), `SDL_MIXER_MINOR_VERSION` (8), `SDL_MIXER_PATCHLEVEL` (1).

### Other Constants (auto-emit)

`MIX_CHANNELS` (8), `MIX_DEFAULT_FREQUENCY` (44100), `MIX_DEFAULT_FORMAT`, `MIX_DEFAULT_CHANNELS` (2), `MIX_MAX_VOLUME` (128), `MIX_CHANNEL_POST` (-2), `MIX_EFFECTSMAXSPEED` (string literal).

---

## 5. Include Dependencies

Includes only SDL2 core headers: `SDL_stdinc.h`, `SDL_rwops.h`, `SDL_audio.h`, `SDL_endian.h`, `SDL_version.h`, `begin_code.h`, `close_code.h`. All via `include/SDL2/` — no new include directories needed.

---

## 6. RSP Recommendations

### Family RSP: `rsp/sdl2-mixer.rsp`

```
--libraryPath
SDL2_mixer

--methodClassName
SDL_mixerNative

--exclude
Mix_SetError
Mix_GetError
Mix_ClearError
Mix_OutOfMemory
```

### Per-Header RSP: None needed

No foreign types, no platform-conditioned declarations.

### Version Helper Macros (Layer 2/3 — not excluded, but must be skipped/reported)

The following function-like and computed macros should be explicitly handled in the report rather than left to accidental emission:

- `SDL_MIXER_VERSION(X)` (SDL_mixer.h:55-60) — function-like, fills SDL_version struct. Should be skipped/reported.
- `MIX_VERSION(X)` (SDL_mixer.h:66) — backward-compat function-like alias over `SDL_MIXER_VERSION(X)`. Should be skipped/reported.
- `SDL_MIXER_COMPILEDVERSION` (SDL_mixer.h:79-80) — computed value macro via `SDL_VERSIONNUM`. Image already emits the matching `SDL_IMAGE_COMPILEDVERSION` as `const int` at `SDL_image.g.cs:277-278`. Should be kept/auto-emitted if ClangSharp resolves it (same pattern as Image).
- `SDL_MIXER_VERSION_ATLEAST(X,Y,Z)` (SDL_mixer.h:86-89) — function-like comparison. Should be skipped/reported.

ClangSharp's `--generate-macro-bindings` handles value macros only, so the function-like macros should be naturally skipped. However, they should appear in the generation report under a "version helpers" category rather than being silently absent.

---

## 7. Family Config

```python
"mixer": {
    "namespace": "SDL2.Mixer",
    "raw_class": "SDL_mixerNative",
    "rsp": "sdl2-mixer.rsp",
    "headers": "sdl2-mixer.headers.txt",
    "library_dir": "Janset.SDL2.Mixer",
},
```

Production header list: single entry `SDL_mixer.h`.
Mixer uses owner mode for `uniform-opaque` because `Mix_Music` at SDL_mixer.h:269 is a Mixer-owned opaque handle. Item 1 provides the family-keyed roster and namespace-aware owner-mode path; Item 5 validates the generated output.

---

## 8. Postprocess Notes

### Existing Rewriters Coverage

| Rewriter | Needed? | Notes |
|---|---|---|
| `strip-varargs` | No | No variadic functions |
| `libraryimport` | Yes | Standard Modern pass |
| `platform-delta` | No | No platform-sensitive headers |
| `guid-substitute` | No | No `SDL_GUID` types |
| `flags-detect` | Yes | `MIX_InitFlags` is caught by suffix rule. |
| `clong-dispatch` | No | No C `long` surface. |
| `uniform-opaque` | Yes — owner mode | Emits `Mix_Music` locally and consumes Core-owned handles by value when present. |

### Satellite-Owned Opaque Handles

`Mix_Music` is a satellite-owned opaque handle, so the correct owner is `SDL2.Mixer`. Do not add it to Core's family entry. Item 1's family-keyed roster + owner-mode path is the intended mechanism.

### `MIX_InitFlags` — `[Flags]` Annotation

Handled by Item 1's `flags-detect` postprocess step via the `Flags` suffix rule; Item 5 should verify the generated enum stays decorated.

### No New Postprocess Steps Needed

Callback function pointers are handled natively by ClangSharp codegen. No postprocess rewriter needed for delegate types.

---

## 9. Test Priorities

1. **`Mix_Chunk` struct size/blittability** — Characterization test (20 bytes x64)
2. **Callback round-trip smoke** (HIGHEST priority) — `Mix_SetPostMix`, `Mix_HookMusicFinished`, `Mix_ChannelFinished`, `Mix_RegisterEffect`/`Mix_UnregisterEffect`, `Mix_EachSoundFont`
3. **`Mix_OpenAudio` / `Mix_CloseAudio` lifecycle**
4. **`Mix_LoadWAV` / `Mix_PlayChannel` / `Mix_FreeChunk`** — Chunk playback
5. **`Mix_Init` / `Mix_Quit`**
6. **`double` return functions** — `Mix_GetMusicPosition`, `Mix_MusicDuration` — verify double precision across P/Invoke

### Callback Smoke Design

For Compat TFMs: delegate + `Marshal.GetFunctionPointerForDelegate()` + `GC.KeepAlive()` (per Microsoft P/Invoke best practices). For Modern TFMs: `[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]` static methods.

---

## 10. Open Questions

1. **`Mix_Music` opaque handle ownership:** Owner is `SDL2.Mixer`. Item 1 provides the Pattern B infrastructure; Item 5 must validate the real generated `Handles.g.cs` and runtime handle roundtrip before NuGet ship.

2. **`MIX_EFFECTSMAXSPEED` string macro:** ClangSharp emits it as the canonical UTF-8 span shape in both Compat and Modern output.

3. **`MIX_DEFAULT_FORMAT = AUDIO_S16SYS`:** ClangSharp resolves the cross-header token to the generation-host value (`0x8010` on little-endian Windows-local generation). This is broader endian/platform-computed macro policy work; see [sdl2-endian-platform-macro-consolidation.md](sdl2-endian-platform-macro-consolidation.md).

4. **`MIX_MAX_VOLUME = SDL_MIX_MAXVOLUME`:** ClangSharp resolves this cross-header value to `128` in both Compat and Modern output.

5. **`Mix_Linked_Version` returns `const SDL_version *`:** No special handling needed — consumer reads through pointer without freeing.
