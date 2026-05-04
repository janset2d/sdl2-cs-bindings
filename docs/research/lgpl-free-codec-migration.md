# LGPL-Free Codec Migration — SDL2_mixer

**Date:** 2026-04-14
**Status:** Implemented and validated on win-x64
**Related:** [license-inventory-2026-04-13.md](license-inventory-2026-04-13.md), [#84](https://github.com/janset2d/sdl2-cs-bindings/issues/84)

## What Changed

SDL2_mixer's vcpkg features were reconfigured and a custom overlay port was created to eliminate all LGPL dependencies while preserving format coverage using SDL2_mixer 2.8.1's bundled permissive codec alternatives.

### Removed vcpkg Features (LGPL)

| Feature | Library | License | Why removed |
| --- | --- | --- | --- |
| `mpg123` | mpg123 1.33.4 | **LGPL-2.1-or-later** | minimp3 (bundled, CC0) covers MP3 |
| `fluidsynth` | fluidsynth 2.5.2 | **LGPL-2.1-or-later** | Timidity (bundled, Artistic) + Native MIDI cover MIDI |
| `libflac` | libflac 1.5.0 | BSD-3-Clause (not LGPL, but redundant) | drflac (bundled, Unlicense) covers FLAC |

### Removed Transitive Dependency

| Dependency | Feature that pulled it | License | Why removed |
| --- | --- | --- | --- |
| `libxmp` | `libmodplug` (vcpkg bundled both together) | **LGPL-2.1-or-later** | Overlay port separates libmodplug from libxmp |

### DLLs Eliminated from Harvest

These DLLs previously appeared in SDL2.Mixer.Native and are now gone:

| DLL | Package | No longer shipped |
| --- | --- | --- |
| `mpg123.dll` | mpg123 | Replaced by minimp3 (bundled) |
| `out123.dll` | mpg123 | Not needed without mpg123 |
| `syn123.dll` | mpg123 | Not needed without mpg123 |
| `libfluidsynth-3.dll` | fluidsynth | Replaced by Timidity + Native MIDI |
| `libxmp.dll` | libxmp | Replaced by libmodplug only |
| `FLAC.dll` | libflac | Replaced by drflac (bundled) |
| `FLAC++.dll` | libflac | Not needed without libflac |

**Net reduction: 7 DLLs removed from SDL2_mixer's closure.**

## What Replaced Them — Bundled Alternatives

All alternatives are embedded in SDL2_mixer's own source tree. They are header-only (minimp3, drflac) or compiled-in (Timidity, Native MIDI) — no external library needed.

| Format | Old Backend | New Backend | Source Location | License | External Deps |
| --- | --- | --- | --- | --- | --- |
| **MP3** | mpg123 (LGPL) | **minimp3** | `src/codecs/minimp3/minimp3.h` | CC0 Public Domain | None (header-only) |
| **FLAC** | libflac (BSD) | **drflac** | `src/codecs/dr_libs/dr_flac.h` | Unlicense / MIT-0 | None (header-only) |
| **MOD/Tracker** | libmodplug + libxmp (LGPL) | **libmodplug only** | External (vcpkg port, public domain) | Public Domain | libmodplug.lib (static) |
| **MIDI** | fluidsynth (LGPL) | **Native MIDI** (Win/Mac) + **Timidity** (bundled) | `src/codecs/native_midi/` + `src/codecs/timidity/` | Zlib / Artistic License 1.0 | OS APIs only (winmm, AudioToolbox) |

### CMake Flags Changed in Overlay Portfile

| Flag | Stock portfile | Overlay portfile | Effect |
| --- | --- | --- | --- |
| `SDL2MIXER_MP3` | Set by feature (OFF when mpg123 absent) | **ON** (unconditional) | MP3 subsystem always enabled |
| `SDL2MIXER_MP3_MPG123` | Set by feature | **OFF** (unconditional) | mpg123 never used |
| `SDL2MIXER_FLAC` | Set by feature (OFF when libflac absent) | **ON** (unconditional) | FLAC subsystem always enabled |
| `SDL2MIXER_FLAC_DRFLAC` | **OFF** (hardcoded) | **ON** | Bundled drflac enabled |
| `SDL2MIXER_MIDI_NATIVE` | **OFF** (hardcoded) | **ON** | Windows/macOS native MIDI enabled |
| `SDL2MIXER_MIDI_FLUIDSYNTH` | Set by feature | **OFF** (unconditional) | FluidSynth never used |
| `SDL2MIXER_MOD_XMP` | Implicitly enabled | **OFF** (unconditional) | libxmp never used |

## Format Coverage — Before vs After

| Format | Before (with LGPL) | After (LGPL-free) | Quality Delta |
| --- | --- | --- | --- |
| **MP3** | mpg123 (mature, battle-tested) | minimp3 (lightweight, header-only) | Minor — minimp3 handles standard MP3 well, edge cases in exotic VBR modes possible |
| **FLAC** | libflac (reference implementation) | drflac (header-only) | Negligible — drflac is well-tested, used by many projects |
| **OGG Vorbis** | libvorbis | libvorbis (unchanged) | None — same backend |
| **Opus** | opusfile | opusfile (unchanged) | None — same backend |
| **WavPack** | wavpack | wavpack (unchanged) | None — same backend |
| **WAV** | built-in | built-in (unchanged) | None |
| **MOD/S3M/XM/IT** | libmodplug + libxmp | libmodplug only | Minor — libxmp handles more exotic tracker formats, libmodplug covers standard MOD/S3M/XM/IT |
| **MIDI (Win/Mac)** | fluidsynth (SoundFont) | Native MIDI (OS API) | Different character — Native MIDI uses OS synth (General MIDI), fluidsynth uses custom SoundFonts |
| **MIDI (Linux)** | fluidsynth (SoundFont) | Timidity (GUS patches) | Requires runtime config — Timidity needs `timidity.cfg` + patch files to produce sound |

## Trade-offs

### What We Gained

1. **100% permissive license stack** — zero LGPL in any satellite package
2. **7 fewer DLLs** in SDL2_mixer's closure
3. **No LGPL split needed** — Mixer.Extras.Native concept eliminated, package topology simplified
4. **Smaller binary footprint** — header-only codecs vs. external shared libraries
5. **Simpler dependency graph** — fewer vcpkg packages to build, cache, and maintain

### What We Traded Away

1. **MP3 edge cases** — minimp3 may handle exotic VBR/LAME-encoded files slightly differently than mpg123. For game audio (short clips, standard encoding), this is negligible.
2. **SoundFont MIDI** — FluidSynth's SoundFont rendering is higher quality than Native MIDI or Timidity. Game projects using custom SoundFonts will miss this. Native MIDI on Windows/macOS uses the OS's General MIDI synth which is adequate for most game use cases.
3. **Timidity runtime requirement** — On Linux, MIDI via Timidity needs external config files at runtime. This is a deployment consideration, not a build issue.
4. **Extended tracker format coverage** — libxmp supports more exotic tracker formats (Amiga MOD variants, rare IT effects). libmodplug covers the standard formats (MOD, S3M, XM, IT) which are 99% of game-dev tracker usage.

### Risk Assessment

| Risk | Severity | Mitigation |
| --- | --- | --- |
| minimp3 MP3 decode quality | Low | minimp3 is widely used (Unreal Engine, many game engines). Report issues to lieff/minimp3. |
| drflac FLAC decode edge case | Very Low | drflac is the default in SDL2_mixer upstream. Well-tested. |
| Timidity MIDI on Linux | Medium | Document runtime requirements. Consider bundling a minimal GUS patch set, or default to "MIDI not available on Linux" with clear docs. |
| libmodplug tracker coverage | Low | Standard formats covered. Exotic formats are extremely rare in game dev. |

## Validation Strategy

### Compile-time validation (already done)

- [x] `vcpkg install --triplet x64-windows-hybrid` succeeds with overlay port
- [x] `SDL2_mixer.dll` has no LGPL DLL dependencies (dumpbin confirmed)
- [x] `WINMM.dll` dependency present (proves Native MIDI compiled in)
- [x] No transitive DLL leakage (only SDL2.dll + system DLLs)

### Runtime validation (needed — Phase 2a spike scope)

Each format needs a runtime test to confirm the codec is actually functional:

| Format | Test Method | Test Asset | Success Criteria |
| --- | --- | --- | --- |
| MP3 | `Mix_LoadMUS("test.mp3")` | Short MP3 clip (CBR 128kbps) | Returns non-null, `Mix_PlayMusic` does not error |
| FLAC | `Mix_LoadMUS("test.flac")` | Short FLAC clip | Returns non-null |
| OGG | `Mix_LoadMUS("test.ogg")` | Short OGG Vorbis clip | Returns non-null |
| Opus | `Mix_LoadMUS("test.opus")` | Short Opus clip | Returns non-null |
| WAV | `Mix_LoadWAV("test.wav")` | Short WAV clip (16-bit PCM) | Returns non-null |
| MOD | `Mix_LoadMUS("test.mod")` | Short ProTracker MOD | Returns non-null |
| MIDI (Win) | `Mix_LoadMUS("test.mid")` | Short MIDI file | Returns non-null on Windows |

These can be headless tests — no audio output device needed. `Mix_LoadMUS` returning non-null proves the codec can parse the format. `Mix_PlayMusic` with `Mix_OpenAudio` in a null-output configuration confirms the decode pipeline works.

### Cross-platform validation (Phase 2b)

- Linux: Same tests, verify Timidity MIDI behavior (needs config files)
- macOS: Same tests, verify AudioToolbox native MIDI
- All RIDs: dumpbin/ldd/otool dependency check — no LGPL .dll/.so/.dylib

## Overlay Port Maintenance

The sdl2-mixer overlay port needs updating when:

1. **vcpkg baseline bump changes sdl2-mixer version** — re-sync SHA512, check for new CMake options
2. **SDL2_mixer adds/removes bundled codecs** — adjust CMake flags accordingly
3. **Upstream vcpkg port changes feature model** — re-sync vcpkg.json feature structure

See `vcpkg-overlay-ports/README.md` for the full maintenance workflow.
