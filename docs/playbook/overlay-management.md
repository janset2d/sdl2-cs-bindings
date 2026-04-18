# Playbook: vcpkg Overlay Port & Triplet Management

**Last updated:** 2026-04-18

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
| `x86-windows-hybrid` | win-x86 | `x86-windows` | Default static, SDL family dynamic |
| `arm64-windows-hybrid` | win-arm64 | `arm64-windows` | Default static, SDL family dynamic |
| `x64-linux-hybrid` | linux-x64 | `x64-linux-dynamic` | Default static, SDL family dynamic, `-fvisibility=hidden` |
| `arm64-linux-hybrid` | linux-arm64 | `arm64-linux-dynamic` | Default static, SDL family dynamic, `-fvisibility=hidden` |
| `x64-osx-hybrid` | osx-x64 | `x64-osx-dynamic` | Default static, SDL family dynamic, `-fvisibility=hidden` |
| `arm64-osx-hybrid` | osx-arm64 | `arm64-osx-dynamic` | Default static, SDL family dynamic, `-fvisibility=hidden` |

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

```text
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

The `vcpkg-setup` composite action now passes both `--overlay-triplets` and `--overlay-ports` conditionally. Manual and workflow examples should treat both overlays as part of the standard vcpkg invocation surface.

### vcpkg Binary Cache (`.vcpkg-cache/`)

CI caches vcpkg's binary artifacts at `$GITHUB_WORKSPACE/.vcpkg-cache/` via `actions/cache@v5`, configured in [`.github/actions/vcpkg-setup/action.yml`](../../.github/actions/vcpkg-setup/action.yml). The repository `.gitignore` excludes that path so the directory is invisible to regular commits.

Key ingredients (as of 2026-04-18):

- **Path:** `${{ github.workspace }}/${{ inputs.vcpkg-cache-path }}` (input default: `.vcpkg-cache`).
- **Key:** `vcpkg-bin-<cache-key-base>-<triplet>-<hash(vcpkg.json, vcpkg-overlay-triplets/**, vcpkg-overlay-ports/**)>-<vcpkg-submodule-commit>`. The overlay-triplet and overlay-port trees participate in the hash so tweaking a hybrid triplet or a port feature flag busts the Actions cache without waiting for vcpkg's internal ABI layer to detect the change.
- **Restore keys:** hierarchical — most specific first (triplet + overlay hash), then less specific (triplet only) so tweaks to `vcpkg.json` can still pull a partial warm cache.

**Local dev.** Regular `./external/vcpkg/vcpkg install ...` invocations use the OS-default vcpkg binary cache (`%LOCALAPPDATA%\vcpkg\archives` on Windows, `~/.cache/vcpkg/archives` on Unix), not the repo-local path. Only set `VCPKG_DEFAULT_BINARY_CACHE=<repo>/.vcpkg-cache` locally if you explicitly want to reproduce CI's cache contents (e.g. when debugging a CI miss). The `.gitignore` entry is defensive — if you enable the repo-local cache path locally, it won't get accidentally committed.

**Drift rule.** If CI's cache path ever moves off `$GITHUB_WORKSPACE/.vcpkg-cache/` (for example to `~/.cache/vcpkg` for portability), update the `.gitignore` entry + this section simultaneously so the documentation is not silently stale.

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
11. Update `build/manifest.json` runtime rows with the new triplet name and strategy allocation

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
- [ ] `build/manifest.json` runtime rows point at the intended triplet names and strategies
- [ ] Version fields in overlay vcpkg.json match `build/manifest.json` and root `vcpkg.json` overrides
```

## Symbol Visibility

Full analysis: [research/symbol-visibility-analysis-2026-04-14.md](../research/symbol-visibility-analysis-2026-04-14.md)

### The Short Version

When transitive deps (zlib, FreeType, libwebp) are statically baked into satellite shared libraries, their symbols can "leak" as exports. Some libraries (FreeType, libwebp, opusfile) use `__attribute__((visibility("default")))` in their headers, which overrides our `-fvisibility=hidden` compiler flag.

**Whether this matters depends on the platform:**

| Platform | Symbol conflict risk | Why | Action |
| --- | --- | --- | --- |
| **Windows** | None | PE DLL isolation — only `__declspec(dllexport)` symbols export | None needed |
| **macOS** | None | Two-level namespaces — each dylib resolves its own symbols | None needed (cosmetic leaks OK) |
| **Linux** | Low (theoretical) | ELF flat global scope — first loaded symbol wins | Version scripts (Phase 2b) |

**SDL3 solves this upstream** with version scripts + macOS export lists. SDL2 does not. When we migrate to SDL3 (Phase 5), visibility comes free.

### Phase 2b Fix: Linux Version Scripts

One `.map` file per satellite, ~8 lines each:

```text
# vcpkg-overlay-triplets/version-scripts/libSDL2_image.map
libSDL2_image {
    global: IMG_*;
    local: *;
};
```

Applied via `VCPKG_LINKER_FLAGS="-Wl,--version-script=<path>"` scoped per-port in the Linux triplet. This is the same approach SkiaSharp uses.

## Hybrid Build Sanity Checks — Per Platform

Run these checks after every hybrid build to verify the model is working correctly. **All three checks must pass.**

### Check 1: No Transitive DLL/SO/DYLIB Leakage

The `bin/` or `lib/` directory must contain ONLY SDL family shared libraries. No `zlib1.dll`, `libpng16.so`, `libfreetype.dylib`, etc.

**Windows:**

```powershell
# List bin/ — should show only SDL2*.dll
dir vcpkg_installed\x64-windows-hybrid\bin\*.dll
# Expected: SDL2.dll, SDL2_image.dll, SDL2_mixer.dll, SDL2_ttf.dll, SDL2_gfx.dll, SDL2_net.dll
# FAIL if: zlib1.dll, libpng16.dll, jpeg62.dll, ogg.dll, etc.
```

**Linux:**

```bash
# List .so files — should show only libSDL2*.so*
ls vcpkg_installed/x64-linux-hybrid/lib/libSDL2*.so*
# Expected: libSDL2-2.0.so.*, libSDL2_image-2.0.so.*, etc.
# FAIL if: libz.so*, libpng16.so*, libfreetype.so*, etc.
```

**macOS:**

```bash
# List .dylib files — should show only libSDL2*.dylib
ls vcpkg_installed/x64-osx-hybrid/lib/libSDL2*.dylib
# Expected: libSDL2-2.0.*.dylib, libSDL2_image-2.0.*.dylib, etc.
# FAIL if: libz.*.dylib, libpng16.*.dylib, etc.
```

### Check 2: Dynamic Dependencies are Minimal

Each satellite should depend ONLY on SDL2 core + OS system libraries. No transitive native libs.

**Windows:**

```powershell
dumpbin /dependents vcpkg_installed\x64-windows-hybrid\bin\SDL2_image.dll
# Expected: SDL2.dll, KERNEL32.dll, VCRUNTIME*.dll, api-ms-win-crt-*.dll
# FAIL if: zlib1.dll, libpng16.dll, jpeg62.dll
```

**Linux:**

```bash
ldd vcpkg_installed/x64-linux-hybrid/lib/libSDL2_image.so
# Expected: libSDL2-2.0.so.0, libc.so.6, libm.so.6, libdl.so.2, ld-linux-x86-64.so.2
# FAIL if: libz.so.1, libpng16.so.16, libjpeg.so.62
```

**macOS:**

```bash
otool -L vcpkg_installed/x64-osx-hybrid/lib/libSDL2_image.dylib
# Expected: @rpath/libSDL2-2.0.*.dylib, /usr/lib/libSystem.B.dylib
# FAIL if: @rpath/libz.*.dylib, @rpath/libpng16.*.dylib
```

### Check 3: Symbol Visibility (Informational on macOS, Critical on Linux after Phase 2b)

**Windows:** Skip — PE is inherently safe.

**macOS (informational — leaks are harmless due to two-level namespaces):**

```bash
nm -gU libSDL2_image-*.dylib | grep -c deflate     # zlib: expect 0
nm -gU libSDL2_image-*.dylib | grep -c png_read     # libpng: expect 0
nm -gU libSDL2_ttf-*.dylib | grep -c FT_            # FreeType: expect >0 (known, harmless)
```

**Linux (critical after version scripts are added in Phase 2b):**

```bash
nm -D libSDL2_image.so | grep -c deflate            # expect 0 after version scripts
nm -D libSDL2_ttf.so | grep ' T ' | grep -v 'TTF_' | wc -l  # non-API exports: expect 0
```

## Platform-Specific Notes

### Windows

- **Symbol visibility:** Not a concern. PE format is export-opt-in.
- **MIDI:** Native MIDI via `winmm.dll` (Windows Multimedia API). Works on every Windows install. No config needed.

### Linux

- **Symbol visibility:** `-fvisibility=hidden` in triplet handles most symbols. Libraries with explicit `visibility("default")` annotations (FreeType, libwebp, opusfile) still leak until version scripts are added (Phase 2b). See [symbol-visibility-analysis-2026-04-14.md](../research/symbol-visibility-analysis-2026-04-14.md).
- **MIDI:** No OS-level MIDI synth. Two options:
  - **Timidity** (bundled in SDL2_mixer, Artistic License): requires `timidity.cfg` + GUS patch files at runtime. Install via `apt install timidity-daemon` or `freepats`. Without these, MIDI returns NULL/silence — no crash.
  - **Recommendation for consumers:** Use MP3/OGG for music on Linux. MIDI is niche and requires user-side setup.
- **SONAME/symlink chains:** Satellite `.so` files have versioned symlinks (e.g., `libSDL2_image-2.0.so.0 → libSDL2_image-2.0.so.0.800.8`). tar.gz packaging preserves these.
- **`-fPIC`:** Required for static libs linked into shared objects. vcpkg's Linux toolchain adds this by default.

### macOS

- **Symbol visibility:** `-fvisibility=hidden` in triplet + macOS two-level namespaces = **safe**. Cosmetic symbol leaks (FT_*, WebP*, op_*) exist but are harmless. No action needed.
- **MIDI:** Native MIDI via AudioToolbox framework. Works on every macOS install. No config needed.
- **Universal binaries:** Not used currently (we build per-arch). Future consideration for `osx` universal RID.
- **`@rpath` / `@loader_path`:** vcpkg handles install name fixup. Satellite `.dylib` files reference `@rpath/libSDL2-2.0.0.dylib` for the core dependency.

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
