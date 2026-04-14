# Playbook: vcpkg Overlay Port & Triplet Management

**Last updated:** 2026-04-14

This playbook covers the complete lifecycle of custom vcpkg overlay ports and overlay triplets in this project.

## Why We Have Overlays

### Overlay Triplets — Hybrid Static + Dynamic Core

Stock vcpkg triplets build everything as either all-static or all-dynamic. Our hybrid packaging model needs **transitive deps static** + **SDL family dynamic**. Custom triplets encode this policy.

Location: `vcpkg-overlay-triplets/`

### Overlay Ports — LGPL-Free Codecs

The stock `sdl2-mixer` portfile disables SDL2_mixer's bundled permissive codec alternatives (minimp3, drflac, native-midi) and couples format support to external LGPL libraries. Our overlay port re-enables the bundled alternatives.

Location: `vcpkg-overlay-ports/`

## Active Overlays Registry

### Triplets

| Triplet | RID | Base | Key Change |
| --- | --- | --- | --- |
| `x64-windows-hybrid` | win-x64 | `x64-windows-release` | Default static, SDL family dynamic |
| `x64-linux-hybrid` | linux-x64 | `x64-linux-dynamic` | Default static, SDL family dynamic, `-fvisibility=hidden` |
| `x64-osx-hybrid` | osx-x64 | `x64-osx-dynamic` | Default static, SDL family dynamic, `-fvisibility=hidden` |

### Ports

| Port | Why | Upstream Issue | Tracking |
| --- | --- | --- | --- |
| `sdl2-mixer` | Enable bundled LGPL-free codec alternatives | N/A (design choice) | #84 |
| `mpg123` | arm64 Linux FPU detection bug | microsoft/vcpkg#40709 | #78 (deprecated — pending removal) |

## Overlay Triplet Anatomy

A triplet is a small CMake config file (~10 lines). It sets variables that vcpkg reads during build:

```cmake
# vcpkg-overlay-triplets/x64-windows-hybrid.cmake
set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CRT_LINKAGE dynamic)
set(VCPKG_LIBRARY_LINKAGE static)     # Default: all deps → .lib/.a
set(VCPKG_BUILD_TYPE release)

# SDL family: stays dynamic — these are the DLLs we ship
if(PORT MATCHES "^(sdl2|sdl2-image|sdl2-mixer|sdl2-ttf|sdl2-gfx|sdl2-net)$")
    set(VCPKG_LIBRARY_LINKAGE dynamic)
endif()
```

**No CMake build logic. No toolchain files. Just variable assignments.**

### Linux/macOS Extra: Symbol Visibility

On Linux and macOS, shared library symbols are exported by default (unlike Windows PE which is opt-in). To prevent transitive dep symbols from leaking through satellite `.so`/`.dylib` files, Linux/macOS triplets add:

```cmake
set(VCPKG_C_FLAGS "-fvisibility=hidden")
set(VCPKG_CXX_FLAGS "-fvisibility=hidden -fvisibility-inlines-hidden")
```

This ensures that when zlib's `deflate` is statically linked into `libSDL2_image.so`, the `deflate` symbol is NOT exported from `libSDL2_image.so`. Windows doesn't need this — PE format is export-opt-in by default.

## Overlay Port Anatomy

An overlay port is a copy of the upstream port with targeted modifications. It contains:

```
vcpkg-overlay-ports/sdl2-mixer/
├── vcpkg.json        ← Modified: LGPL features removed, libxmp dep removed
├── portfile.cmake    ← Modified: bundled alternatives enabled, LGPL backends disabled
├── fix-pkg-prefix.patch  ← Unchanged: byte-identical copy from upstream
└── usage             ← Unchanged: byte-identical copy from upstream
```

**Rule: unchanged files must be byte-identical to upstream.** This makes diff-based maintenance easy.

## Day-to-Day: How to Use Overlays

### Local Development

```bash
# vcpkg install automatically picks up overlays via --overlay-* flags:
./external/vcpkg/vcpkg install \
    --triplet x64-windows-hybrid \
    --overlay-triplets=./vcpkg-overlay-triplets \
    --overlay-ports=./vcpkg-overlay-ports
```

### CI (GitHub Actions)

The `vcpkg-setup` composite action already passes `--overlay-ports` conditionally. `--overlay-triplets` needs to be added (Phase 2b CI update).

## Procedure: Adding a New Overlay Triplet

1. Identify the stock triplet to base on (check `external/vcpkg/triplets/` and `triplets/community/`)
2. Create `vcpkg-overlay-triplets/<name>.cmake`
3. Copy the stock triplet's variables
4. Change `VCPKG_LIBRARY_LINKAGE` to `static` (default)
5. Add `PORT MATCHES` override for SDL family → `dynamic`
6. For Linux/macOS: add `-fvisibility=hidden` flags
7. Test: `vcpkg install --triplet <name> --overlay-triplets=./vcpkg-overlay-triplets`
8. Verify: `bin/` contains only SDL DLLs, no transitive dep DLLs
9. Update this playbook's Active Overlays Registry table
10. Update `vcpkg-overlay-triplets/README.md`
11. Update `build/runtimes.json` with the new triplet name

## Procedure: Adding a New Overlay Port

1. Copy the **entire** upstream port directory:
   ```bash
   cp -r external/vcpkg/ports/<port-name>/ vcpkg-overlay-ports/<port-name>/
   ```
2. Make your changes to `portfile.cmake` and/or `vcpkg.json`
3. **Document every change** with comments in the modified files
4. Verify unchanged files match upstream:
   ```bash
   diff vcpkg-overlay-ports/<port>/<file> external/vcpkg/ports/<port>/<file>
   ```
5. Test: `vcpkg install --overlay-ports=./vcpkg-overlay-ports`
6. Update `vcpkg-overlay-ports/README.md` with: why, tracking issue, changes from upstream
7. Update this playbook's Active Overlays Registry table

## Procedure: vcpkg Baseline Bump

This is the most common maintenance event. When bumping the vcpkg baseline (updating `external/vcpkg` submodule):

### Step 1: Check What Changed Upstream

```bash
# For each overlay port:
diff -r vcpkg-overlay-ports/sdl2-mixer/ external/vcpkg/ports/sdl2-mixer/

# For triplets (rarely change, but check):
diff vcpkg-overlay-triplets/x64-windows-hybrid.cmake external/vcpkg/triplets/x64-windows-release.cmake
```

### Step 2: Sync Unchanged Files

If upstream changed files we didn't modify (e.g., `usage`, patches):

```bash
cp external/vcpkg/ports/sdl2-mixer/fix-pkg-prefix.patch vcpkg-overlay-ports/sdl2-mixer/
cp external/vcpkg/ports/sdl2-mixer/usage vcpkg-overlay-ports/sdl2-mixer/
```

### Step 3: Check Port Version Changes

If the upstream port version changed (e.g., sdl2-mixer 2.8.1 → 2.8.2):

1. Update `vcpkg-overlay-ports/sdl2-mixer/vcpkg.json` version field
2. Update `vcpkg-overlay-ports/sdl2-mixer/portfile.cmake` SHA512 hash
3. Check if CMake options changed (new flags, renamed flags, removed flags)
4. Re-apply our custom changes on top of the new portfile
5. Update `build/manifest.json` version
6. Update `vcpkg.json` overrides version

### Step 4: Rebuild and Validate

```bash
# Remove cached build for the port:
rm -rf external/vcpkg/buildtrees/sdl2-mixer
rm -rf external/vcpkg/packages/sdl2-mixer_*
rm -rf vcpkg_installed/x64-windows-hybrid

# Rebuild:
./external/vcpkg/vcpkg install --triplet x64-windows-hybrid \
    --overlay-triplets=./vcpkg-overlay-triplets \
    --overlay-ports=./vcpkg-overlay-ports

# Verify:
# 1. Build succeeds
# 2. bin/ contains only SDL DLLs
# 3. dumpbin/ldd/otool shows no transitive dep dependencies
```

### Step 5: Update Documentation

- `vcpkg-overlay-ports/README.md` — update "Based on upstream version" field
- `docs/research/lgpl-free-codec-migration-2026-04-14.md` — if format coverage changed
- This playbook — if procedures changed

## Procedure: Removing an Overlay

When an upstream fix makes our overlay unnecessary:

1. Delete the overlay directory
2. Test: `vcpkg install` works without the overlay
3. Update `vcpkg-overlay-ports/README.md`
4. Update this playbook's Active Overlays Registry
5. If no overlays remain for a type, the `--overlay-*` flag can be removed from CI

## Maintenance Checklist (Copy for PR Review)

Use this checklist when reviewing overlay-related changes:

```markdown
- [ ] All unchanged files are byte-identical to upstream (`diff` confirmed)
- [ ] Patches generated via `git diff` from pristine source (never hand-written)
- [ ] `vcpkg install` succeeds with overlay
- [ ] `bin/` contains only SDL DLL family (no transitive DLLs leaked)
- [ ] dumpbin/ldd/otool confirms no unexpected dynamic dependencies
- [ ] `vcpkg-overlay-ports/README.md` updated
- [ ] `vcpkg-overlay-triplets/README.md` updated (if triplet changed)
- [ ] This playbook's Active Overlays Registry updated
- [ ] `build/runtimes.json` triplet names correct
- [ ] Version fields in overlay vcpkg.json match `build/manifest.json` and root `vcpkg.json` overrides
```

## Platform-Specific Notes

### Windows

- **Symbol visibility:** Not a concern. PE format is export-opt-in. Static deps baked into DLL do not leak symbols.
- **MIDI:** Native MIDI via `winmm.dll` (Windows Multimedia API). Works on every Windows install. No config needed.

### Linux

- **Symbol visibility:** Critical. Add `-fvisibility=hidden` to triplet `VCPKG_C_FLAGS` and `VCPKG_CXX_FLAGS`. Validate with `nm -D libSDL2_image.so | grep deflate` — should return 0 results.
- **MIDI:** No OS-level MIDI synth. Timidity bundled in SDL2_mixer but requires **runtime config files**:
  - `timidity.cfg` — instrument mapping config
  - GUS patch files — actual instrument sound samples
  - Typically installed via `apt install timidity-daemon` or `freepats` package
  - **Without these files, `Mix_LoadMUS("file.mid")` returns NULL or produces silence — no crash.**
  - For game framework consumers: document that Linux MIDI requires user-side setup, or recommend MP3/OGG for music instead.
- **SONAME/symlink chains:** Satellite `.so` files still have versioned symlinks (e.g., `libSDL2_image-2.0.so.0 → libSDL2_image-2.0.so.0.800.8`). tar.gz packaging preserves these.
- **`-fPIC`:** Required for static libs that get linked into shared objects. vcpkg's Linux toolchain adds this by default.

### macOS

- **Symbol visibility:** Same concern as Linux. Add `-fvisibility=hidden` to triplet. Validate with `nm -gU libSDL2_image.dylib | grep deflate`.
- **MIDI:** Native MIDI via AudioToolbox framework. Works on every macOS install. No config needed.
- **Universal binaries:** Not used currently (we build per-arch). Future consideration for `osx` universal RID.
- **`@rpath` / `@loader_path`:** vcpkg handles install name fixup. Satellite `.dylib` files reference `@rpath/libSDL2-2.0.0.dylib` for the core dependency.
- **Minimum deployment target:** Stock triplet doesn't set `CMAKE_OSX_DEPLOYMENT_TARGET`. Consider adding if compatibility issues arise.

## FAQ

### Q: Why can't we just use vcpkg's stock triplets?

Stock triplets are all-dynamic or all-static. We need mixed: transitive deps static (baked into satellites), SDL family dynamic (shipped as DLLs). Custom triplets are vcpkg's official mechanism for this.

### Q: Why can't we avoid the overlay port for sdl2-mixer?

The stock portfile hardcodes `-DSDL2MIXER_FLAC_DRFLAC=OFF` and `-DSDL2MIXER_MIDI_NATIVE=OFF`, explicitly disabling SDL2_mixer's bundled alternatives. It also bundles `libxmp` (LGPL) into the `libmodplug` feature. There's no way to override this without modifying the portfile.

### Q: How often do overlay ports need updating?

SDL2_mixer is mature (last release: 2.8.1). Expect 0-1 version bumps per year. Baseline bumps that don't change sdl2-mixer's version require zero overlay changes.

### Q: What happens if I forget to update the overlay after a baseline bump?

The build fails loudly — SHA512 hash mismatch if the source changed, or CMake configure error if options changed. There is no silent drift.

### Q: Can we upstream our changes?

Potentially. The stock portfile's design choice to disable bundled alternatives was intentional (vcpkg prefers external packages over bundled ones). Our use case (LGPL-free with bundled alternatives) is valid but may not align with vcpkg's philosophy. Worth discussing with vcpkg maintainers if the maintenance burden becomes significant.
