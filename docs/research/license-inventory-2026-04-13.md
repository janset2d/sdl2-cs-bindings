# SDL2 Native Dependency License Inventory

**Date:** 2026-04-13
**Sources:**

- Binary closure: `artifacts/harvest_output/{library}/runtimes/win-x64/native/` (Cake Harvest output)
- License identifiers: `vcpkg_installed/x64-windows-release/share/{package}/vcpkg.spdx.json` (vcpkg SPDX metadata)
- DLL ownership: `vcpkg_installed/vcpkg/info/*.list` (vcpkg package manifests)

**Scope:** Every DLL that lands in a native NuGet package, mapped to its owning vcpkg package and SPDX license. Data is authoritative — SPDX IDs come from vcpkg's own metadata, cross-checked against actual copyright files.

## Per-DLL Inventory

All 42 unique binaries from the win-x64 harvest, grouped by satellite package.

### SDL2.Core.Native

| DLL | Package | Version | SPDX License | Family |
| --- | --- | --- | --- | --- |
| `SDL2.dll` | sdl2 | 2.32.10 | `Zlib` | Permissive |
| `samplerate.dll` | libsamplerate | 0.2.2 | `BSD-2-Clause` | Permissive |

### SDL2.Image.Native

| DLL | Package | Version | SPDX License | Family |
| --- | --- | --- | --- | --- |
| `SDL2_image.dll` | sdl2-image | 2.8.8 | `Zlib` | Permissive |
| `zlib1.dll` | zlib | 1.3.1 | `Zlib` | Permissive |
| `libpng16.dll` | libpng | 1.6.57 | `libpng-2.0` | Permissive |
| `jpeg62.dll` | libjpeg-turbo | 3.1.4.1 | `BSD-3-Clause` | Permissive |
| `turbojpeg.dll` | libjpeg-turbo | 3.1.4.1 | `BSD-3-Clause` | Permissive |
| `tiff.dll` | tiff | 4.7.1 | `libtiff` (BSD-like) | Permissive |
| `avif.dll` | libavif | 1.4.1 | `Apache-2.0 AND BSD-2-Clause` | Permissive |
| `libwebp.dll` | libwebp | 1.6.0 | `BSD-3-Clause` | Permissive |
| `libwebpdecoder.dll` | libwebp | 1.6.0 | `BSD-3-Clause` | Permissive |
| `libwebpdemux.dll` | libwebp | 1.6.0 | `BSD-3-Clause` | Permissive |
| `libwebpmux.dll` | libwebp | 1.6.0 | `BSD-3-Clause` | Permissive |
| `libsharpyuv.dll` | libwebp | 1.6.0 | `BSD-3-Clause` | Permissive |
| `libyuv.dll` | libyuv | 1916 | `BSD-3-Clause` (from copyright) | Permissive |
| `liblzma.dll` | liblzma | 5.8.3 | `0BSD` (for liblzma itself) ⚠️ | Permissive |

**Note on liblzma:** The XZ Utils project is a mix — `liblzma` (the `.dll` we ship) is `0BSD`. Command-line tools in the same project (`xz`, `xzdec`) have partial LGPL components but we do not ship those.

### SDL2.Mixer.Native

| DLL | Package | Version | SPDX License | Family |
| --- | --- | --- | --- | --- |
| `SDL2_mixer.dll` | sdl2-mixer | 2.8.1 | `Zlib` | Permissive |
| `ogg.dll` | libogg | 1.3.6 | `BSD-3-Clause` | Permissive |
| `vorbis.dll` | libvorbis | 1.3.7 | `BSD-3-Clause` | Permissive |
| `vorbisfile.dll` | libvorbis | 1.3.7 | `BSD-3-Clause` | Permissive |
| `vorbisenc.dll` | libvorbis | 1.3.7 | `BSD-3-Clause` | Permissive |
| `opus.dll` | opus | 1.5.2 | `BSD-3-Clause` | Permissive |
| `FLAC.dll` | libflac | 1.5.0 | `BSD-3-Clause` | Permissive |
| `FLAC++.dll` | libflac | 1.5.0 | `BSD-3-Clause` | Permissive |
| `modplug.dll` | libmodplug | 0.8.9.0 | Public Domain | Permissive |
| `wavpackdll.dll` | wavpack | 5.9.0 | `BSD-3-Clause` | Permissive |
| **`mpg123.dll`** | **mpg123** | **1.33.4** | **`LGPL-2.1-or-later`** | **Weak copyleft** |
| **`out123.dll`** | **mpg123** | **1.33.4** | **`LGPL-2.1-or-later`** | **Weak copyleft** |
| **`syn123.dll`** | **mpg123** | **1.33.4** | **`LGPL-2.1-or-later`** | **Weak copyleft** |
| **`libxmp.dll`** | **libxmp** | **4.6.0** | **`LGPL-2.1-or-later`** | **Weak copyleft** |
| **`libfluidsynth-3.dll`** | **fluidsynth** | **2.5.2** | **`LGPL-2.1-or-later`** | **Weak copyleft** |

**Note on mpg123 DLL triplet:** The mpg123 package ships three DLLs (`mpg123.dll` for decode, `out123.dll` for audio output, `syn123.dll` for synthesis). All three share the same LGPL-2.1-or-later license. If we drop mpg123, all three are dropped together.

### SDL2.Ttf.Native

| DLL | Package | Version | SPDX License | Family |
| --- | --- | --- | --- | --- |
| `SDL2_ttf.dll` | sdl2-ttf | 2.24.0 | `Zlib` | Permissive |
| `freetype.dll` | freetype | 2.14.3 | `(FTL OR GPL-2.0-or-later)` dual | Permissive (we pick FTL) |
| `harfbuzz.dll` | harfbuzz | 13.0.1 | `MIT-Modern-Variant` | Permissive |
| `harfbuzz-subset.dll` | harfbuzz | 13.0.1 | `MIT-Modern-Variant` | Permissive |
| `harfbuzz-raster.dll` | harfbuzz | 13.0.1 | `MIT-Modern-Variant` | Permissive |
| `harfbuzz-vector.dll` | harfbuzz | 13.0.1 | `MIT-Modern-Variant` | Permissive |
| `zlib1.dll` | zlib | 1.3.1 | `Zlib` | Permissive |
| `bz2.dll` | bzip2 | 1.0.8 | `bzip2-1.0.6` (BSD-like) | Permissive |
| `brotlicommon.dll` | brotli | 1.2.0 | `MIT` | Permissive |
| `brotlidec.dll` | brotli | 1.2.0 | `MIT` | Permissive |
| `brotlienc.dll` | brotli | 1.2.0 | `MIT` | Permissive |
| `libpng16.dll` | libpng | 1.6.57 | `libpng-2.0` | Permissive |

**Note on freetype:** FreeType ships with a dual license — we can use it under FTL (BSD-like) or GPL-2.0. For commercial compatibility we consume under FTL, which is permissive.

### SDL2.Gfx.Native

| DLL | Package | Version | SPDX License | Family |
| --- | --- | --- | --- | --- |
| `SDL2_gfx.dll` | sdl2-gfx | 1.0.4 | `Zlib` (from copyright; SPDX NOASSERTION) | Permissive |

### SDL2.Net.Native

| DLL | Package | Version | SPDX License | Family |
| --- | --- | --- | --- | --- |
| `SDL2_net.dll` | sdl2-net | 2.2.0 | `Zlib` | Permissive |

## The LGPL Pocket

All LGPL-licensed binaries — 5 DLLs from 3 upstream projects — land exclusively in `SDL2.Mixer.Native`:

| Upstream | DLLs | What it provides |
| --- | --- | --- |
| mpg123 | `mpg123.dll`, `out123.dll`, `syn123.dll` | MP3 decode + audio out + signal synthesis |
| libxmp | `libxmp.dll` | Tracker module playback (MOD, XM, IT, S3M, etc.) |
| fluidsynth | `libfluidsynth-3.dll` | SoundFont-based MIDI synthesis |

**Every other satellite (SDL2.Core, SDL2.Image, SDL2.Ttf, SDL2.Gfx, SDL2.Net) is 100% permissive.** The LGPL problem is surgically isolated.

## Why LGPL Matters (in one paragraph)

LGPL-2.1 lets anyone use the library in any software — commercial, proprietary, open source — on **one** condition: the end user must retain the ability to replace the LGPL'd library with a modified version. In practice this means **dynamic loading is fine**, but **static linking requires also shipping `.o` object files** so a user can relink with their own mpg123. For a NuGet-distributed C# binding, only dynamic-load delivery is practical. SDL_mixer's upstream `dlopen` pattern for mp3/midi/tracker codecs exists for exactly this reason.

## Delivery Mode Matrix

| Approach | Compatible with permissive deps? | Compatible with LGPL deps? |
| --- | :---: | :---: |
| Ship as loose dynamic `.dll` / `.so` | ✅ | ✅ |
| Static-link into satellite `.dll` with hidden symbols | ✅ | ❌ (relink obligation) |
| Static-link into a single mega-binary | ✅ | ❌ (relink obligation + license bundling) |

## LGPL Handling Options

Four independent options exist for dealing with the three LGPL dependencies. They are orthogonal to the packaging strategy choice (hybrid-static vs. pure-dynamic) — any packaging strategy can pair with any of these.

### Option A — Drop LGPL codecs entirely

Remove `mpg123`, `libxmp`, `fluidsynth` from the SDL2_mixer feature set. Ship only permissive codecs.

- Lost formats: MP3, MOD/XM/IT (tracker music via libxmp), MIDI (via fluidsynth)
- Retained formats: OGG Vorbis, Opus, FLAC, WAV, MODPlug (simpler tracker support)
- Pro: 100% permissive stack, zero legal friction for consumers
- Con: MP3 is commonly expected in game audio pipelines

### Option B — Keep LGPL codecs in the default package (dynamic loading)

Ship `mpg123.dll`/`libxmp.dll`/`libfluidsynth-3.dll` alongside `SDL2_mixer.dll` in the default `Janset.SDL2.Mixer.Native` package. SDL_mixer's `dlopen` pattern keeps static-linking out of the picture, so LGPL compliance is satisfied.

- Pro: Full format support out of the box, single package
- Con: Every consumer pulls in LGPL; commercial consumers need to understand their redistribution obligation even if we stay compliant on our end

### Option C — Split into default (permissive) and opt-in extras (LGPL)

- **`Janset.SDL2.Mixer.Native`** (default, permissive-only): sdl2-mixer + ogg/vorbis/opus/flac/modplug/wavpack
- **`Janset.SDL2.Mixer.Extras.Native`** (opt-in): `mpg123.dll`/`out123.dll`/`syn123.dll`, `libxmp.dll`, `libfluidsynth-3.dll` as separate dynamic libraries
- Pro: Clean commercial default, full support on opt-in; preserves upstream `dlopen` pattern
- Con: Two packages to maintain for mixer; users need to learn the split

### Option D — Drop MP3 from mixer, use managed alternative

Drop mpg123 from mixer. Users who need MP3 can use managed MP3 decoders (NLayer, NAudio, etc.) feeding PCM into mixer via `Mix_QuickLoad_RAW`.

- Pro: No LGPL anywhere, common solution in the .NET game dev space
- Con: Requires consumer code changes; less ergonomic

## LGPL Split Option — Default vs. Extras (Option C in detail)

One pattern that works across any packaging strategy (hybrid-static or pure-dynamic) is splitting the mixer into a permissive-only default package and an opt-in LGPL extras package:

- **`Janset.SDL2.Mixer.Native`** (default, permissive-only)
  - Ships: sdl2-mixer, libogg, libvorbis, opus, libflac, libmodplug, wavpack
  - Zero LGPL exposure → no friction for commercial consumers
- **`Janset.SDL2.Mixer.Extras.Native`** (opt-in, adds LGPL codecs)
  - Ships: `mpg123.dll`/`out123.dll`/`syn123.dll`, `libxmp.dll`, `libfluidsynth-3.dll` as separate dynamic libraries
  - Preserves SDL_mixer's upstream `dlopen` pattern → LGPL compliant
  - Users who need MP3/MIDI/tracker music reference this package explicitly
- **All other satellites** (`SDL2.Core.Native`, `SDL2.Image.Native`, `SDL2.Ttf.Native`, `SDL2.Gfx.Native`, `SDL2.Net.Native`)
  - 100% permissive — any packaging model works

This split is portable: it can be applied on top of either the hybrid-static or the pure-dynamic strategy. It gives commercial developers a clean default while still exposing full format support to consumers who opt in.

Alternative LGPL stances (A, B, D) are documented in the [LGPL Handling Options](#lgpl-handling-options) section above; all four are viable and the choice is independent of the packaging strategy decision.

## Open Questions

1. Should the LGPL extras be one package (`Mixer.Extras.Native`) or three (`Mixer.Mp3.Native`, `Mixer.Midi.Native`, `Mixer.Tracker.Native`)? One package is simpler; three gives finest granularity but adds NuGet bloat.
2. Should the managed `Janset.SDL2.Mixer` wrapper expose separate API surface for LGPL-backed functions (so they only light up when `Extras.Native` is referenced)?
3. FreeType ships dual (FTL/GPL). Are we explicit about choosing FTL in our own copyright/NOTICE files? (Required for clean commercial compatibility.)

## Next Steps

1. **Decide on Option C** (or revisit alternatives) — single direction call
2. Adjust vcpkg.json features: remove `mpg123`, `libxmp-compat`, `fluidsynth` from the default `sdl2-mixer` feature set; reinstate them behind a separate overlay/feature flag for the Extras package
3. Draft the hybrid-static triplet for permissive-only deps (feeds into #75 decision)
4. If approved, scaffold `Janset.SDL2.Mixer.Extras.Native` package topology
