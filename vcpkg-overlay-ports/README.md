# vcpkg Overlay Ports

This directory contains local overrides for vcpkg ports that have upstream bugs or design mismatches affecting our build matrix or packaging strategy. Overlays are vcpkg's official mechanism for patching ports without forking the vcpkg repo.

**How it works:** The `vcpkg-setup` GitHub Action conditionally passes `--overlay-ports` pointing to this directory (only if the directory exists). When vcpkg encounters a port name that exists here, it uses our version instead of the upstream port.

## Active Overlays

### sdl2-mixer

- **Why:** The stock vcpkg portfile disables SDL2_mixer's bundled codec alternatives (minimp3, drflac, native-midi) and couples format support to external LGPL libraries. Our LGPL-free strategy needs the bundled alternatives enabled.
- **Tracking issue:** #84
- **Based on upstream version:** 2.8.1#2 (vcpkg baseline `0b88aacd`)
- **Changes from upstream:**
  - **vcpkg.json:** Removed LGPL features (`mpg123`, `fluidsynth`, `libflac`). Removed `libxmp` dependency from `libmodplug` feature. Added `timidity` to available features.
  - **portfile.cmake:** Force-enabled `SDL2MIXER_MP3=ON` and `SDL2MIXER_FLAC=ON` (bundled minimp3/drflac handle these). Set `SDL2MIXER_FLAC_DRFLAC=ON` (was hardcoded OFF). Set `SDL2MIXER_MIDI_NATIVE=ON` (was hardcoded OFF). Disabled LGPL backends: `SDL2MIXER_MP3_MPG123=OFF`, `SDL2MIXER_MIDI_FLUIDSYNTH=OFF`, `SDL2MIXER_MOD_XMP=OFF`.
- **Format coverage with this overlay:**

  | Format | Backend | Source | License |
  | --- | --- | --- | --- |
  | MP3 | minimp3 (bundled) | `src/codecs/minimp3/` | CC0 Public Domain |
  | FLAC | drflac (bundled) | `src/codecs/dr_libs/` | Unlicense/MIT-0 |
  | OGG Vorbis | libvorbis (vcpkg) | External (core dep) | BSD-3-Clause |
  | Opus | opusfile (vcpkg) | External (feature) | BSD-3-Clause |
  | WavPack | wavpack (vcpkg) | External (feature) | BSD-3-Clause |
  | WAV | built-in | SDL2_mixer core | Zlib |
  | MOD/Tracker | libmodplug (vcpkg) | External (feature) | Public Domain |
  | MIDI | Timidity (bundled) | `src/codecs/timidity/` | Artistic License 1.0 |
  | MIDI | Native MIDI | `src/codecs/native_midi/` | Zlib (Win: winmm, Mac: AudioToolbox) |

- **Runtime note:** Timidity MIDI requires `timidity.cfg` + GUS patch files at runtime. Without them, MIDI via Timidity produces silence. Native MIDI on Windows/macOS works without additional files.

### sdl2-gfx

- **Why:** SDL2_gfx's public headers only define `__declspec(dllexport)` on MSVC and fall back to bare `extern` on every other compiler. Combined with our hybrid overlay triplets' `-fvisibility=hidden` (set in `vcpkg-overlay-triplets/_hybrid-common.cmake` for both Linux and Darwin), every SDL2_gfx public API symbol is emitted as hidden on `libSDL2_gfx*.so` / `*.dylib`. This breaks the C++ native-smoke link step (`filledCircleRGBA` unresolved) and, more importantly, breaks C# P/Invoke at runtime on Unix — `dlsym` cannot resolve hidden symbols.
- **Upstream issue:** None. SDL2_gfx 1.0.4 shipped in 2018 and has had no releases since; upstream is effectively abandoned.
- **Based on upstream version:** 1.0.4#11 (vcpkg baseline `0b88aacd`)
- **Files changed from upstream:** Only `003-fix-unix-visibility.patch` (new) and `portfile.cmake` (adds the new patch to the `PATCHES` list + Janset overlay comment header). All other files (`vcpkg.json`, `CMakeLists.txt`, `001-lrint-arm64.patch`, `002-use-the-lrintf-intrinsic.patch`) are byte-identical copies of the upstream port.
- **What the patch does:** Adds a single `#elif defined(__GNUC__) && __GNUC__ >= 4` branch to each of the four public headers (`SDL2_gfxPrimitives.h`, `SDL2_framerate.h`, `SDL2_rotozoom.h`, `SDL2_imageFilter.h`) so the `SDL2_<MODULE>_SCOPE` macro resolves to `extern __attribute__((visibility("default")))` on GCC/Clang. The MSVC branch and the final `#ifndef ... extern` fallback are untouched, so Windows behavior is unchanged and older compilers still get the bare `extern` path. 8 insertions total across 4 files.
- **Why the patch rather than a per-port triplet exception:** Disabling `-fvisibility=hidden` for sdl2-gfx at the triplet level would also expose every transitive statically-linked symbol on the dynamic satellite's export table, which is the exact failure mode the hybrid strategy's visibility rule is designed to prevent (see `vcpkg-overlay-triplets/_hybrid-common.cmake:17`). The patch is port-local, annotates only the real public API, and matches how the rest of the SDL family handles exports (SDL_image/mixer/ttf/net all use SDL's own `DECLSPEC` which already emits `visibility("default")` on GCC/Clang).
- **Regression guard — open (PD-15):** No automated CI check asserts the patched symbols remain `GLOBAL DEFAULT` / `T` after a vcpkg baseline bump. Today the patch is exercised by every CI job that runs `Harvest` on Linux / macOS, but a future baseline bump could rewrite the upstream source layout such that the patch applies with partial offsets or becomes a silent no-op, and the gap would only surface later at runtime (`EntryPointNotFoundException` on downstream C# consumers). Tracked as [phase-2-adaptation-plan.md PD-15](../docs/phases/phase-2-adaptation-plan.md#pending-decisions). Resolution candidates: smoke-time `readelf`/`nm` assertion on the harvested lib, post-pack `G`-series guardrail, or both.

### mpg123 (DEPRECATED — pending removal)

- **Why:** arm64 Linux FPU detection bug — container environments incorrectly report no FPU, causing `REAL_IS_FIXED` + `OPT_NEON64` compile conflict.
- **Upstream issue:** microsoft/vcpkg#40709
- **Tracking issue:** #78
- **Dependency chain:** `sdl2-mixer` (feature: mpg123) → `mpg123`
- **Based on upstream version:** 1.33.4 (vcpkg baseline `0b88aacd`)
- **Files changed from upstream:** Only `have-fpu.diff` (FPU detection patch). All other files (`vcpkg.json`, `portfile.cmake`, `pkgconfig.diff`) are identical copies of the upstream port.
- **Deprecation note:** The `mpg123` feature has been removed from our sdl2-mixer overlay (LGPL-free transition, #84). This overlay port is no longer needed for our build. It will be removed once confirmed that no other port depends on mpg123 in our dependency graph.

## How Patches Work in vcpkg

vcpkg extracts source tarballs and applies patches using `git apply`. This means:

- Patch files **must** be in unified diff format (standard `git diff` output)
- **Never write patch files by hand** — they will almost certainly have encoding, whitespace, or line count issues that cause `error: corrupt patch`
- On Windows, `git diff` can sometimes output UTF-16 — patches must be UTF-8

### Creating or Updating a Patch

```bash
# 1. Find the pristine source tarball (vcpkg caches downloads)
ls external/vcpkg/downloads/{library}-{version}.tar.*

# 2. Extract to a temp directory
mkdir -p /tmp/{library}-patch
cp external/vcpkg/downloads/{library}-{version}.tar.bz2 /tmp/{library}-patch/
cd /tmp/{library}-patch
tar xjf {library}-{version}.tar.bz2
cd {library}-{version}

# 3. Initialize a git repo on the pristine source
git init && git add -A && git commit -m "pristine"

# 4. Make your changes (edit files directly)

# 5. Generate the patch
git diff > /path/to/vcpkg-overlay-ports/{library}/your-patch.diff

# 6. Validate: reset and test apply
git checkout -- .
git apply --check /path/to/vcpkg-overlay-ports/{library}/your-patch.diff
```

**Important:** If the upstream port already has patches (like `have-fpu.diff` or `pkgconfig.diff`), your overlay patch replaces the upstream one entirely. Generate your patch against the **unpatched** source, including both the upstream fix and your additional changes.

## Maintenance Rules

### On vcpkg baseline bump

1. Compare each overlay against the new upstream port: `diff -r vcpkg-overlay-ports/{port}/ external/vcpkg/ports/{port}/`
2. If `vcpkg.json` or `portfile.cmake` changed upstream, re-sync the overlay copies
3. If the source version changed, re-generate patches against the new pristine source using the workflow above
4. Run `git apply --check` against the new source tarball before pushing

### On upstream fix

When the upstream issue is resolved in a vcpkg commit included in our baseline:

1. Delete the overlay directory
2. Test that `vcpkg install` works without the overlay
3. If no overlays remain, the `--overlay-ports` flag is automatically skipped (conditional in `vcpkg-setup` action)

### On dependency version change

If the parent library (e.g. `sdl2-mixer`) bumps its dependency version (e.g. mpg123 1.33.4 → 1.34.x), the overlay's SHA512 in `portfile.cmake` and all patches may need updating against the new source.

## Overlay Port Checklist

Use this when adding or updating an overlay:

- [ ] All unchanged files are byte-identical to upstream (`diff` against `external/vcpkg/ports/{port}/`)
- [ ] Patches generated via `git diff` from pristine source (never hand-written)
- [ ] `git apply --check` passes against the pristine source tarball
- [ ] This README updated with: why, upstream issue link, tracking issue, dependency chain, base version
- [ ] CI passes on all affected platforms

Keep this README current — it is the canonical registry of why each overlay exists.
