# Packaging Strategy: Hybrid Static + Dynamic Core

**Date:** 2026-04-13
**Status:** Option under evaluation
**Related:** [license-inventory-2026-04-13.md](license-inventory-2026-04-13.md), [packaging-strategy-pure-dynamic-2026-04-13.md](packaging-strategy-pure-dynamic-2026-04-13.md), [#75](https://github.com/janset2d/sdl2-cs-bindings/issues/75)

## Problem Statement

Our current pure-dynamic packaging model produces **unavoidable file collisions** when consumers reference multiple satellite packages:

```text
consumer app/bin/
├── SDL2.dll         (from SDL2.Core.Native)
├── SDL2_image.dll   (from SDL2.Image.Native)
├── zlib1.dll        ← from SDL2.Image.Native
├── libpng16.dll     ← from SDL2.Image.Native
├── SDL2_mixer.dll   (from SDL2.Mixer.Native)
├── zlib1.dll        ← from SDL2.Mixer.Native ⚠️ COLLISION
└── libpng16.dll     ← from SDL2.Mixer.Native ⚠️ COLLISION (via other paths)
```

When two satellite native packages ship the same DLL basename, MSBuild's file-copy step picks one winner and silently overwrites the other. If the two versions differ (different vcpkg port-versions, different codec features, different symbols) the consumer can load a zlib that's out of sync with what the library was built against → runtime symbol errors, corrupt behavior, or crashes.

Evidence from our own harvest (`artifacts/harvest_output/`):

- `zlib1.dll` appears in SDL2.Image, SDL2.Mixer, SDL2.Ttf
- `libpng16.dll` appears in SDL2.Image, SDL2.Ttf

This problem is the reason issue [#75](https://github.com/janset2d/sdl2-cs-bindings/issues/75) is a packaging blocker.

## The Two Available Strategies

| Strategy | Modularity | File collisions | Binary size | LGPL handling |
| --- | --- | --- | --- | --- |
| **Pure dynamic** (current) | ✅ | ❌ Present | Small | Natural (dlopen) |
| **Hybrid static + dynamic core** | ✅ | ✅ Eliminated | +~200KB/satellite | Via opt-in extras package |

Both keep the modular NuGet topology. The hybrid model trades a small size increase for the elimination of the entire collision class.

**This document covers the hybrid static option.** The other option — pure dynamic — is documented in [packaging-strategy-pure-dynamic-2026-04-13.md](packaging-strategy-pure-dynamic-2026-04-13.md). Neither document is a recommendation; they exist so a decision can be made with full visibility.

## Hybrid Static + Dynamic Core — The Model

### Principle

- **Shared runtime surface (SDL2 itself):** remains a single dynamic library, referenced by every satellite
- **Transitive dependencies (zlib, libpng, ogg, freetype, etc.):** statically linked into each satellite with hidden symbol visibility
- **LGPL dependencies (mpg123, libxmp, fluidsynth):** stay dynamic (runtime-loaded via SDL_mixer's upstream `dlopen` pattern), shipped separately as opt-in

### Why SDL2 Core must stay dynamic

If we static-linked SDL2 into every satellite, each satellite would carry its own copy of SDL state (window manager, event queue, audio subsystem). `SDL_Init` would run once per satellite, handles would not cross boundaries, singletons would not agree. Runtime chaos.

Keeping SDL2 dynamic means all satellites load **the same SDL2.dll** into the process. One SDL state, one event loop, everything consistent.

### Why transitive deps should be static

The "overlapping library" problem exists precisely because zlib, libpng, ogg, etc. are modular across satellites but have no unified ownership in our package graph. By baking them statically with hidden symbol visibility, each satellite becomes a sealed unit — it uses its own internal zlib, cannot export zlib symbols to other loaded libraries, and cannot be polluted by another satellite's zlib.

Each satellite grows by the size of its transitive deps (typically 200-400 KB), but the collision class disappears forever.

### Why LGPL codecs must stay dynamic

LGPL-2.1 allows use in any software (commercial, proprietary, open source) on the condition that the end user can **replace the LGPL library** with their own modified version. For NuGet-distributed binaries this translates to:

| Approach | LGPL compliant? |
| --- | :---: |
| Static-link LGPL codec into satellite binary (no object files) | ❌ |
| Ship LGPL codec as separate dynamic library, SDL_mixer loads it via `dlopen` | ✅ |

SDL_mixer's upstream has already designed around this: `mpg123`, `libxmp`, and `fluidsynth` are **runtime-loaded via `dlopen`/`LoadLibrary`** — never compile-time linked. The MP3/MIDI/tracker decode APIs are compiled into `SDL2_mixer.dll` but the LGPL library is only touched at runtime.

This means:

- `SDL2_mixer.dll` knows *how* to play MP3 (API compiled in)
- `SDL2_mixer.dll` does **not** carry mpg123 code
- At runtime, if `mpg123.dll` is next to the application, MP3 works
- If it is not, `Mix_LoadMUS()` on an MP3 returns null gracefully; no crash

We exploit this to separate commercial-safe (permissive-only) from full-featured (LGPL-enabled) delivery:

- Default `Janset.SDL2.Mixer.Native` ships permissive codecs only, omits LGPL DLLs
- Opt-in `Janset.SDL2.Mixer.Extras.Native` adds the LGPL DLLs as independent files

No recompilation, no rebuild. Adding the Extras NuGet reference drops the LGPL DLLs next to `SDL2_mixer.dll` and SDL_mixer finds them at runtime.

## Final Package Topology

| Package | Shipped Binaries | Build Mode | License Profile |
| --- | --- | --- | --- |
| `Janset.SDL2.Core.Native` | `SDL2.dll` | Dynamic | Zlib (permissive) |
| `Janset.SDL2.Image.Native` | `SDL2_image.dll` (all image codecs statically baked in) | Hybrid static | All permissive |
| `Janset.SDL2.Mixer.Native` | `SDL2_mixer.dll` (all permissive audio codecs statically baked in) | Hybrid static | All permissive |
| `Janset.SDL2.Mixer.Extras.Native` | `mpg123.dll`, `out123.dll`, `syn123.dll`, `libxmp.dll`, `libfluidsynth-3.dll` | Dynamic | **LGPL-2.1-or-later** |
| `Janset.SDL2.Ttf.Native` | `SDL2_ttf.dll` (freetype, harfbuzz, bzip2, brotli, zlib, libpng all statically baked in) | Hybrid static | All permissive |
| `Janset.SDL2.Gfx.Native` | `SDL2_gfx.dll` | Dynamic (no transitive deps) | Zlib |
| `Janset.SDL2.Net.Native` | `SDL2_net.dll` | Dynamic (no transitive deps) | Zlib |

All transitive dependency versions and SPDX license IDs are documented per DLL in [license-inventory-2026-04-13.md](license-inventory-2026-04-13.md).

## How It Solves the Collision Problem

### Before (pure dynamic)

```text
app/bin/
├── SDL2.dll
├── SDL2_image.dll     → looks for zlib1.dll in bin/
├── SDL2_mixer.dll     → looks for zlib1.dll in bin/
├── zlib1.dll          ← ONE wins, other gets overwritten
└── ... (10+ transitive DLLs at risk of collision)
```

### After (hybrid static)

```text
app/bin/
├── SDL2.dll                   (shared, dynamic, single copy)
├── SDL2_image.dll             (zlib/png/jpeg/tiff/webp/avif/lzma baked in, symbols hidden)
├── SDL2_mixer.dll             (ogg/vorbis/opus/flac/modplug/wavpack baked in, symbols hidden)
├── SDL2_ttf.dll               (freetype/harfbuzz/brotli/bzip2/zlib/png baked in, symbols hidden)
├── SDL2_gfx.dll
├── SDL2_net.dll
└── (optional) mpg123.dll, libxmp.dll, libfluidsynth-3.dll  ← only if Extras referenced
```

No shared transitive DLLs in the flat namespace. Each satellite is self-contained. The collision class does not exist.

## How It Solves the LGPL Problem

The default user never sees LGPL. They reference `Janset.SDL2.Mixer.Native`, get a single `SDL2_mixer.dll`, the satellite's permissive codecs light up (OGG/Opus/FLAC/WAV/ModPlug/WavPack), and MP3/MIDI/XM simply return "format unsupported" at runtime.

Users who want MP3/MIDI/tracker music add `Janset.SDL2.Mixer.Extras.Native` to their csproj. The LGPL DLLs drop into the bin folder. SDL_mixer's runtime `dlopen` finds them. Format support lights up. The user is responsible for LGPL compliance in their distribution (which is well understood in the game dev space).

Commercial game studios: default is safe, no legal review needed.
Open source or MIT/BSD hobby projects: opt-in to Extras, no hassle.
Framework author (us): one satellite with a variant, all others uniform.

## Build Implementation Sketch

### vcpkg Side

Create a hybrid triplet (e.g. `x64-windows-hybrid`, `x64-linux-hybrid-dynamic`):

- `VCPKG_LIBRARY_LINKAGE=static` as default
- Per-port override (via port customization) for SDL2: `VCPKG_LIBRARY_LINKAGE=dynamic`
- For LGPL ports (mpg123, libxmp, fluidsynth): only built when Extras build is explicitly requested; feature-gated out of default

### Build Side (satellite CMake wrapper)

Each satellite native package has a thin CMake build producing a single dynamic library:

```cmake
add_library(SDL2_image_bundle SHARED bridge.cpp)

target_link_libraries(SDL2_image_bundle PRIVATE
    SDL2::SDL2                              # dynamic, external
    SDL2_image::SDL2_image-static           # static
    PNG::PNG JPEG::JPEG TIFF::TIFF          # static
    ZLIB::ZLIB WebP::WebP avif              # static
)

# Force --whole-archive so the linker pulls in all exports from static deps
if(UNIX AND NOT APPLE)
    target_link_options(SDL2_image_bundle PRIVATE "-Wl,--whole-archive")
    # ... static libs above apply under whole-archive ...
    target_link_options(SDL2_image_bundle PRIVATE "-Wl,--no-whole-archive")
elseif(APPLE)
    target_link_options(SDL2_image_bundle PRIVATE "-Wl,-all_load")
endif()

# Hide transitive symbols — only SDL2_image's exports should leak out
set_target_properties(SDL2_image_bundle PROPERTIES
    C_VISIBILITY_PRESET hidden
    CXX_VISIBILITY_PRESET hidden
)
```

The output is a single `.dll`/`.so`/`.dylib` per satellite. No transitive DLLs ship.

### Harvest / Package Side

Our Cake Harvest task already walks binary closure. After the hybrid build, each satellite's closure is just its own binary plus SDL2 (which comes from the Core package via NuGet dependency). Manifest and packaging logic simplifies significantly — no per-satellite transitive deployment list needed.

## C# Binding Side — What Changes?

**Nothing in the binding surface.** Function names (`SDL_Init`, `Mix_OpenAudio`, `IMG_Load`, etc.) stay identical. The `[LibraryImport]` `EntryPoint` values stay the same.

What does change: the `LibraryName` stays as `SDL2_mixer` / `SDL2_image` etc., just as today. Since each satellite is still its own `.dll`, P/Invoke targets do not change. Our existing `Janset.SDL2.Mixer`, `Janset.SDL2.Image` C# projects require **zero code changes**.

The auto-generated bindings (Phase 4 / #69) will work unchanged — CppAst generates against headers, hybrid static vs pure dynamic makes no difference at the ABI level.

## License Redistribution

Every NuGet package must carry a `LICENSE-THIRD-PARTY.txt` listing the licenses of every library baked into its primary binary. Source data is already present under `artifacts/harvest_output/{library}/licenses/` — the Cake Harvest task collects all copyright files during binary closure walking. PackageTask (#54) needs to bundle these into the .nupkg.

For satellites with hybrid-static deps:

- `SDL2.Core.Native`: zlib (SDL2) + BSD-2-Clause (libsamplerate)
- `SDL2.Image.Native`: Zlib + 8 permissive licenses (all collected)
- `SDL2.Mixer.Native`: Zlib + 6 permissive licenses
- `SDL2.Mixer.Extras.Native`: LGPL-2.1-or-later (3 upstreams — prominently)
- `SDL2.Ttf.Native`: Zlib + 6 permissive licenses
- `SDL2.Gfx.Native` / `SDL2.Net.Native`: Zlib only

All SPDX IDs and per-DLL ownership already documented in [license-inventory-2026-04-13.md](license-inventory-2026-04-13.md).

## Trade-offs

### Pros

- **Collision class eliminated.** Overlapping DLL basenames cannot cross between satellites.
- **Modularity preserved.** Consumers still reference only the satellites they need.
- **Commercial-safe default.** No LGPL in the default path.
- **LGPL-friendly opt-in.** Extras package preserves `dlopen` pattern, legally clean.
- **No C# binding changes.** Zero churn in the managed side.
- **Simplified harvest/packaging.** Each satellite = one binary + license bundle.

### Cons

- **Small size increase.** Each satellite carries its own static copies (zlib ~80KB, libpng ~300KB, freetype ~700KB, etc.). Total binary footprint across all satellites grows by ~1-2 MB per RID.
- **Hybrid triplet maintenance.** Custom vcpkg triplets require validation on every baseline bump.
- **Build complexity.** Each satellite needs a CMake wrapper project, not just a vcpkg install + copy.
- **Harder live-patching.** With static linking, a security fix in zlib requires rebuilding every satellite that bakes zlib in. Pure dynamic only needs replacing zlib1.dll.

### The live-patching trade-off in practice

For a .NET NuGet-distributed library, users do not hand-patch individual DLLs. They update their package reference to a new version. So "zlib had a CVE" → we bump our zlib static dep → we publish new NuGet versions of the affected satellites → users update. The hybrid-static model adds one rebuild step on our side; the user experience is identical.

## Risks

1. **Symbol leakage.** If symbol visibility is not strict, two satellites baking zlib could accidentally cross-talk when loaded in the same process. Mitigated by `C_VISIBILITY_PRESET hidden` + `--whole-archive --no-whole-archive` paired correctly.
2. **Custom triplet fragility.** vcpkg baseline bumps could change port linkage defaults. Tested via our existing harvest matrix on every baseline.
3. **Extras package UX.** Users must understand they need to add `.Extras.Native` to get MP3. Mitigate via clear README + metapackage (`Janset.SDL2.Mixer.Full` that references Extras by default).

## Open Questions

1. Single Extras package vs. granular (`Mp3.Native`, `Midi.Native`, `Tracker.Native`)? Single is simpler; granular is finer but adds NuGet surface.
2. Do we provide a `Janset.SDL2` metapackage that pulls everything (including Extras)? Or keep users explicit?
3. Should we expose `IsExtrasAvailable()` helper in managed Mixer binding so consumer code can branch cleanly?
4. Per-RID licensing differences? macOS and Linux rarely have LGPL compliance issues in the same shape as Windows distribution, but the codec DLLs themselves are identical upstream.

## Open Decisions Before This Option Could Be Picked

1. Confirm whether the binary size delta (~1-2 MB per RID across all satellites) is acceptable for the project's distribution goals
2. Decide on Extras package granularity (single vs. per-codec split)
3. Decide whether a `Janset.SDL2.Mixer.Full` metapackage should pull Extras automatically
4. Validate hybrid triplet feasibility on all 7 RIDs before committing (currently unproven on macOS and Linux arm64)
5. Estimate ongoing maintenance cost of hybrid triplets vs. stock vcpkg triplets
