# Packaging Strategy: Pure Dynamic (Alternative)

**Date:** 2026-04-13
**Status:** Option under evaluation
**Related:** [license-inventory-2026-04-13.md](license-inventory-2026-04-13.md), [packaging-strategy-hybrid-static-2026-04-13.md](packaging-strategy-hybrid-static-2026-04-13.md), [#75](https://github.com/janset2d/sdl2-cs-bindings/issues/75)

## Purpose of This Document

This document captures the pure dynamic packaging option in full — the path where every library keeps shipping as an independent shared library, matching the current harvest output. It is a companion to [packaging-strategy-hybrid-static-2026-04-13.md](packaging-strategy-hybrid-static-2026-04-13.md); the two sit side-by-side so the project can choose between them based on real trade-offs rather than assumptions. Neither document is a recommendation.

## Model Recap

In the pure dynamic model:

- Every library (SDL2, SDL2_image, SDL2_mixer, zlib, libpng, freetype, etc.) ships as a separate shared library (`.dll` / `.so` / `.dylib`)
- Each satellite native NuGet package carries the satellite's primary binary **plus** all of its runtime transitive dependencies as loose files
- MSBuild copies every runtime file into the consumer's `bin/` folder

This is exactly what our current harvest output produces.

## The Collision Problem

When two or more satellite native packages ship the same transitive DLL, MSBuild's copy step picks one winner:

```text
consumer/bin/
├── SDL2.dll
├── SDL2_image.dll
├── SDL2_mixer.dll
├── SDL2_ttf.dll
├── zlib1.dll          ← comes from Image, Mixer, Ttf. Last write wins.
├── libpng16.dll       ← comes from Image, Ttf. Last write wins.
├── ogg.dll            ← Mixer
├── vorbis.dll         ← Mixer
├── FLAC.dll           ← Mixer
├── opus.dll           ← Mixer
└── ...                ← ~15 more transitive DLLs
```

From our actual harvest output (`artifacts/harvest_output/`):

| DLL | Appears in |
| --- | --- |
| `zlib1.dll` | SDL2.Image, SDL2.Mixer (indirect), SDL2.Ttf |
| `libpng16.dll` | SDL2.Image, SDL2.Ttf |

If all three satellite packages ship compatible versions, the collision is benign. If they drift (different vcpkg port-versions, different codec feature sets resulting in slightly different symbol tables, ABI-compatible but not bit-identical), we get silent corruption, missing exports, or crashes.

This is the root cause of [#75](https://github.com/janset2d/sdl2-cs-bindings/issues/75).

## How to Keep Pure Dynamic and Still Ship Responsibly

Three concrete mechanisms exist. Any serious pure-dynamic strategy needs at least one of them.

### Mechanism A — Shared Native Dependency Packages

Extract every DLL that appears in multiple satellites into its own NuGet package.

```text
Janset.SDL2.Common.ZLib.Native/      → zlib1.dll
Janset.SDL2.Common.LibPng.Native/    → libpng16.dll
Janset.SDL2.Common.LibOgg.Native/    → ogg.dll       (only used by Mixer, but pattern-consistent)
Janset.SDL2.Common.FreeType.Native/  → freetype.dll  (only Ttf)
...
```

Each satellite then declares **NuGet dependencies** on only the shared packages it needs:

```xml
<!-- SDL2.Image.Native.csproj -->
<PackageReference Include="Janset.SDL2.Common.ZLib.Native" Version="1.3.1" />
<PackageReference Include="Janset.SDL2.Common.LibPng.Native" Version="1.6.57" />
<!-- ... -->

<!-- SDL2.Ttf.Native.csproj -->
<PackageReference Include="Janset.SDL2.Common.ZLib.Native" Version="1.3.1" />
<PackageReference Include="Janset.SDL2.Common.LibPng.Native" Version="1.6.57" />
<PackageReference Include="Janset.SDL2.Common.FreeType.Native" Version="2.14.3" />
```

NuGet's transitive resolution guarantees a single version of each shared package in the final graph. One `zlib1.dll` in `bin/`, not three.

**Pros:**

- Solves the collision class cleanly
- Natural fit for NuGet's graph resolution
- Easy security patch model (bump `Common.ZLib.Native` to patch every consumer)

**Cons:**

- Large proliferation of tiny NuGet packages (10-15 `Common.*.Native` packages for our graph)
- Versioning coordination — every common-dep bump cascades through every satellite rebuild
- First-time setup cost (need to classify every transitive dep and draft separate packages)
- Adds complexity to PackageTask (#54) significantly

### Mechanism B — RPATH / `$ORIGIN` and SONAME discipline (Linux/macOS)

For Linux and macOS, the loader has well-defined search orders. We can exploit this so satellites find their own transitive deps rather than fighting over a flat `bin/`:

```bash
# On Linux/macOS, after build and before packaging:
patchelf --set-rpath '$ORIGIN' libSDL2_image.so

# On macOS:
install_name_tool -add_rpath '@loader_path' libSDL2_image.dylib
```

This tells `libSDL2_image.so` to look for its own transitive deps **in its own directory first**, falling back to system paths only after. If each satellite's native package ships to `runtimes/{rid}/native/{satellite}/` (a per-satellite subdirectory), collisions cannot happen by construction.

**Pros:**

- No new NuGet packages
- Elegant on Unix platforms

**Cons:**

- **Does not work on Windows.** Windows PE does not honour RPATH; its DLL search order is CWD, System32, PATH — no per-directory-relative search. Custom DLL-search-path logic via `AddDllDirectory` or `SetDllDirectory` adds managed-side startup code, fragile to get right.
- **Even on Unix, NuGet does not preserve directory structure cleanly.** MSBuild's default native-copy behaviour flattens `runtimes/{rid}/native/` into `bin/`. Per-satellite subdirectories require custom MSBuild targets that override the flattening.
- **SONAME versioning.** We already deal with Linux SONAME chains (see our existing `tar.gz` symlink handling). Per-satellite paths multiply that complexity.

Usable, but not a complete cross-platform answer on its own.

### Mechanism C — Selective static bake within a dynamic triplet

Keep the overall dynamic build, but statically bake only the small, problem-causing transitive deps (zlib, libpng) into each satellite that uses them. Every other library still ships as a standalone dynamic `.dll`/`.so`.

#### Is this actually supported by vcpkg?

Yes, via **per-port linkage overrides in a custom triplet file**. vcpkg exposes the `PORT` variable to the triplet script, so a triplet can branch on it:

```cmake
# custom triplet: x64-linux-dynamic-selective.cmake
set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CRT_LINKAGE dynamic)
set(VCPKG_LIBRARY_LINKAGE dynamic)          # default for this triplet
set(VCPKG_CMAKE_SYSTEM_NAME Linux)

# Selective overrides — these few ports build as static archives
if(PORT MATCHES "^(zlib|libpng|libjpeg-turbo)$")
    set(VCPKG_LIBRARY_LINKAGE static)
endif()
```

This is the only officially supported selective-linkage pattern in vcpkg; there is no manifest-level `dependencies.linkage` override. The public `Intelight/vcpkg` fork has a real `x86-windows-mixed.cmake` that uses this exact pattern to mix static and dynamic ports by name. Microsoft's own docs on overlay triplets describe this as a supported use case. See [microsoft/vcpkg#15067](https://github.com/microsoft/vcpkg/issues/15067) for the design discussion and [microsoft/vcpkg#33256](https://github.com/microsoft/vcpkg/discussions/33256) for official guidance.

The triplet itself is dropped into an overlay directory and referenced at install time:

```bash
vcpkg install --triplet=x64-linux-dynamic-selective \
              --overlay-triplets=./vcpkg-overlay-triplets
```

We already pass `--overlay-ports` from our `vcpkg-setup` action (conditionally). Adding `--overlay-triplets` is a one-line change.

#### What it actually does

When vcpkg resolves dependencies for a port (e.g. sdl2-image), it uses the triplet once per dependency. Under selective linkage:

- `sdl2-image` resolves as dynamic → produces `SDL2_image.dll`
- Its dependency `zlib` matches the override → produces `zlib.lib` (Windows) / `libz.a` (Linux), not a DLL
- Linker building `SDL2_image.dll` pulls `zlib.lib` into the DLL as static code
- Final output: `SDL2_image.dll` has zlib code inside it, no `zlib1.dll` shipped

Repeat for sdl2-mixer, sdl2-ttf, etc. — each satellite carries its own static zlib. The `zlib1.dll` that was colliding in the pure-dynamic model no longer exists.

#### Known pitfalls (documented in vcpkg issues)

1. **Symbol visibility is not automatic.** When zlib static-links into `SDL2_image.dll`, zlib's exported symbols (`deflate`, `inflate`, …) are by default re-exported from `SDL2_image.dll` on Windows (PE auto-export if not marked `__declspec(dllimport)`) and on Linux (default `-fvisibility=default`). If two satellites both export zlib symbols into the same process, the loader can pick one and silently use it for both → the exact collision we tried to avoid. Mitigation requires symbol-hiding at the satellite CMake level (`C_VISIBILITY_PRESET hidden` + explicit export of only SDL2_image's API). This is the same mitigation the hybrid-static option needs.

2. **CRT linkage must match.** Mixing `VCPKG_LIBRARY_LINKAGE=dynamic` with `VCPKG_CRT_LINKAGE=static` is unsafe on Windows — each DLL gets its own CRT heap, breaking `malloc`/`free` across module boundaries. Our selective triplet must keep CRT dynamic everywhere.

3. **Full rebuild on triplet change.** vcpkg caches builds per-triplet. Switching from `x64-linux-dynamic` to `x64-linux-dynamic-selective` means rebuilding all ports under the new triplet from scratch. No incremental reuse.

4. **Port-list drift is a maintenance burden.** The `PORT MATCHES` list has to track every transitive dep we want baked. Adding a new library (e.g. adding `freetype` to the bake list because Ttf now has collisions) means updating the triplet and rebuilding everything. In practice the list grows over time.

5. **.NET native binding prior art is thin.** No major .NET native binding projects ship packages built with this pattern (confirmed via research). SkiaSharp and LibGit2Sharp either go full hybrid (static bake + dynamic wrapper) or stay fully dynamic with `dlopen` discipline. This doesn't mean the pattern is wrong — just that we'd be somewhat on our own if we hit edge cases.

#### How Mechanism C differs from hybrid static

The difference is a matter of scope:

| Aspect | Mechanism C (selective bake) | Hybrid static |
| --- | --- | --- |
| Default linkage | Dynamic | Static |
| Exceptions | Specific collision-prone deps forced static | SDL2 core forced dynamic |
| Typical bake list size | 2-5 ports | 20+ ports |
| SDL2 core handling | Already dynamic (default) | Explicit override |
| Symbol visibility discipline | Required for baked ports | Required for baked ports |
| Maintenance burden | Tracking bake list as collisions appear | Tracking exceptions as they appear |

Conceptually Mechanism C **starts at dynamic and adds static where needed**; hybrid static **starts at static and adds dynamic where needed**. They meet in the middle. For our dependency graph (where only zlib + libpng + libjpeg are shared across satellites) Mechanism C is the lighter-touch version; if the collision list grows over time it converges on hybrid static.

#### When Mechanism C is the right call

- The collision list is small and stable (zlib + libpng + maybe libjpeg)
- We want to keep most of our current "one transitive DLL per port" shape
- We accept that symbol visibility discipline must be added to the satellite wrappers anyway
- We're prepared to maintain the custom triplet alongside vcpkg baseline bumps

#### When it's not the right call

- The collision list keeps growing (new satellites, new codecs) — eventually it's the same complexity as hybrid static without the clarity
- We want zero transitive DLL files shipping alongside satellites — Mechanism C still has many (ogg, vorbis, freetype, harfbuzz, etc.)
- We need each satellite to be fully sealed (no outside dependencies except SDL2 core) — Mechanism C doesn't achieve this; Mechanism A or hybrid static do

## LGPL in the Pure Dynamic World

Pure dynamic actually has a **natural fit** with SDL_mixer's LGPL codecs, because nothing is statically linked to begin with. `mpg123.dll`, `libxmp.dll`, `libfluidsynth-3.dll` just ship alongside `SDL2_mixer.dll`, and SDL_mixer finds them at runtime.

However, this means the commercial-safe / full-featured separation is **less clean than in the hybrid model**:

- In pure dynamic, if `Janset.SDL2.Mixer.Native` includes mpg123.dll by default, *every* consumer pulls in LGPL. Commercial consumers would then have to manually exclude files or use a separate SKU.
- We could still do the Extras split (default package omits LGPL DLLs; opt-in package adds them), so the LGPL strategy from the hybrid model is portable to pure dynamic.

The LGPL story is therefore not a decider between the two models — both can handle it.

## Full Package Topology Under Pure Dynamic + Mechanism A

```text
Janset.SDL2.Common.ZLib.Native                  → zlib1.dll
Janset.SDL2.Common.LibPng.Native                → libpng16.dll
Janset.SDL2.Common.LibOgg.Native                → ogg.dll
Janset.SDL2.Common.LibVorbis.Native             → vorbis.dll, vorbisfile.dll, vorbisenc.dll
Janset.SDL2.Common.LibJpeg.Native               → jpeg62.dll, turbojpeg.dll
Janset.SDL2.Common.LibTiff.Native               → tiff.dll
Janset.SDL2.Common.LibWebp.Native               → libwebp.dll, libwebpdecoder.dll, libwebpdemux.dll, libwebpmux.dll, libsharpyuv.dll
Janset.SDL2.Common.LibAvif.Native               → avif.dll
Janset.SDL2.Common.LibYuv.Native                → libyuv.dll
Janset.SDL2.Common.LibLzma.Native               → liblzma.dll
Janset.SDL2.Common.Brotli.Native                → brotlicommon.dll, brotlidec.dll, brotlienc.dll
Janset.SDL2.Common.Bzip2.Native                 → bz2.dll
Janset.SDL2.Common.FreeType.Native              → freetype.dll
Janset.SDL2.Common.HarfBuzz.Native              → harfbuzz*.dll (4 variants)
Janset.SDL2.Common.Opus.Native                  → opus.dll
Janset.SDL2.Common.LibFlac.Native               → FLAC.dll, FLAC++.dll
Janset.SDL2.Common.LibModplug.Native            → modplug.dll
Janset.SDL2.Common.WavPack.Native               → wavpackdll.dll
Janset.SDL2.Common.LibSampleRate.Native         → samplerate.dll

Janset.SDL2.Core.Native            → SDL2.dll
                                     deps: Common.LibSampleRate

Janset.SDL2.Image.Native           → SDL2_image.dll
                                     deps: Core, Common.ZLib, Common.LibPng, Common.LibJpeg,
                                           Common.LibTiff, Common.LibWebp, Common.LibAvif,
                                           Common.LibYuv, Common.LibLzma

Janset.SDL2.Mixer.Native           → SDL2_mixer.dll
                                     deps: Core, Common.LibOgg, Common.LibVorbis, Common.Opus,
                                           Common.LibFlac, Common.LibModplug, Common.WavPack

Janset.SDL2.Mixer.Extras.Native    → mpg123.dll, out123.dll, syn123.dll, libxmp.dll, libfluidsynth-3.dll
                                     (LGPL — opt-in)

Janset.SDL2.Ttf.Native             → SDL2_ttf.dll
                                     deps: Core, Common.FreeType, Common.HarfBuzz, Common.ZLib,
                                           Common.Bzip2, Common.Brotli, Common.LibPng

Janset.SDL2.Gfx.Native             → SDL2_gfx.dll
                                     deps: Core

Janset.SDL2.Net.Native             → SDL2_net.dll
                                     deps: Core
```

**Total: ~26 NuGet packages** (19 Common + 7 satellite primaries).

## Trade-offs

### Pros

- **Live-patchable.** Bumping `Common.ZLib.Native` for a CVE automatically flows to all consumers without rebuilding our satellite packages.
- **Smaller individual binaries.** No static duplication; each transitive dep ships exactly once.
- **Matches the original harvest output shape.** Less deviation from what our current Cake Harvest pipeline already produces.
- **LGPL codecs fit naturally** (dlopen is the native mode).

### Cons

- **Package count explosion.** 26 packages vs. 7 in the hybrid model. Every package is a maintenance burden (versioning, publishing, README, license file).
- **Transitive version coordination.** If zlib bumps its ABI, every Common + satellite needs coordinated re-release. Bumping the zlib Common package alone is not enough — every satellite built against it must also republish with matching version bounds.
- **NuGet graph complexity.** Consumers see dozens of indirect package references, which surfaces in tooling (dependency trees, security scanners, SBOMs). The hybrid model shows 2-3 packages in a typical consumer graph; pure dynamic + Mechanism A shows 10-15.
- **Still no Windows RPATH.** Mechanism A solves collision via NuGet graph but not via filesystem isolation, so if a consumer bundles additional native libs (via their own NuGet, via app deploy, etc.) collisions can still happen. Mechanism B (RPATH) helps on Linux/macOS but not Windows.
- **Harder to reason about.** "Which version of zlib does my app actually have?" is harder when 5 packages declare zlib dependencies.

## When Pure Dynamic Is A Strong Fit

A project might reasonably pick pure dynamic when:

- Security patching velocity is a core requirement (e.g. enterprise audited distribution, SBOM drift minimization)
- Consumers frequently override individual transitive deps with their own versions
- Binary size is a hard constraint across many RIDs
- The product is distributed outside NuGet as well (native SDK model, system packages)
- The project prefers maintaining many thin packages over a few thick ones

## When Pure Dynamic Is A Weak Fit

- Collision safety is non-negotiable and Windows is a first-class target (no RPATH equivalent)
- Low package count is valued over granular versioning
- The team does not want to coordinate version bumps across a large Common.* package tree
- The consumer-facing dependency graph is expected to stay shallow

## Open Decisions Before This Option Could Be Picked

1. Commit to at least one collision-resolution mechanism (A, B, or combination) — not choosing one is not a valid state
2. If Mechanism A: approve the ~26-package topology and the versioning discipline required to keep it sane
3. If Mechanism B: accept that Windows still needs a separate solution (custom DLL-search-path code in every consumer, or per-satellite subdir loading)
4. Define LGPL split approach (Extras package is portable here too)
5. Plan PackageTask ([#54](https://github.com/janset2d/sdl2-cs-bindings/issues/54)) to produce and maintain the common-package tree

## Reference

- Full binary-level evidence for every collision and license claim: [license-inventory-2026-04-13.md](license-inventory-2026-04-13.md)
- Counterpart strategy: [packaging-strategy-hybrid-static-2026-04-13.md](packaging-strategy-hybrid-static-2026-04-13.md)
- Related issue: [#75](https://github.com/janset2d/sdl2-cs-bindings/issues/75)
