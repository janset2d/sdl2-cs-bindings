# Phase 5: SDL3 Support

**Status**: PLANNED
**Depends on**: Phase 4 (Binding Auto-Generation) — generator must exist first

## Objective

Add SDL3 bindings and native packages to the monorepo, sharing build infrastructure with SDL2 while keeping the two library sets independently versioned and releasable.

## Context

SDL3 was released as stable in January 2025 (SDL 3.2.0) and is now at **3.4.4** (April 2026). It is the actively developed version — SDL2 is in maintenance mode. All new game projects should prefer SDL3, but SDL2 will continue to receive bugfix releases.

### SDL3 API Differences

SDL3 is **not** API-compatible with SDL2. Key breaking changes:

- Functions that returned negative int error codes now return `bool`
- Device indices replaced with typed IDs (`SDL_AudioDeviceID`, etc.)
- `SDL_RWops` removed, replaced by `SDL_IOStream`
- Audio subsystem redesigned (callback → stream model)
- New subsystems: GPU API, Camera, Dialog, Filesystem/Storage, Async I/O
- All function names follow new naming conventions

This means SDL3 bindings must be generated from scratch — not adapted from SDL2. The auto-generator from Phase 4 is essential.

## vcpkg Availability

| Library | vcpkg Port | Version | Features |
|---------|-----------|---------|----------|
| SDL3 | `sdl3` | 3.4.4 | vulkan, alsa, dbus, ibus, wayland, x11 |
| SDL3_image | `sdl3-image` | 3.4.2 | jpeg, png, tiff, webp |
| SDL3_mixer | `sdl3-mixer` | 3.2.0#1 | fluidsynth, libflac, libvorbis, libxmp, mpg123, opusfile, wavpack |
| SDL3_ttf | `sdl3-ttf` | 3.2.2#1 | harfbuzz, svg |
| SDL3_net | **N/A** | — | Upstream WIP, no release yet |
| SDL3_shadercross | `sdl3-shadercross` | 3.0.0-preview2 | GPU shader translation (preview) |

**Blocker**: SDL3_net is not available in vcpkg. This library will be added when upstream releases.

## Scope

### 5.1 Monorepo Structure

Add SDL3 projects alongside SDL2:

```
src/
├── SDL2.Core/              ← Existing SDL2 bindings
├── SDL2.Image/             ← ...
├── SDL3.Core/              ← NEW: SDL3 bindings (auto-generated)
├── SDL3.Image/             ← NEW
├── SDL3.Mixer/             ← NEW
├── SDL3.Ttf/               ← NEW
├── SDL3.Gfx/               ← N/A (SDL2_gfx has no SDL3 equivalent)
└── native/
    ├── SDL2.Core.Native/   ← Existing
    ├── SDL3.Core.Native/   ← NEW
    ├── SDL3.Image.Native/  ← NEW
    ├── SDL3.Mixer.Native/  ← NEW
    └── SDL3.Ttf.Native/    ← NEW
```

### 5.2 Shared Build Infrastructure

The Cake Frosting build system should be parameterized to handle both SDL2 and SDL3:

- `manifest.json` (schema v2.1, single source of truth): Add SDL3 `library_manifests[]`, `package_families[]`, and — if SDL3 ever requires different runtime coverage than SDL2 — additional `runtimes[]` entries. Today's 7 RIDs apply unchanged (vcpkg triplets are library-agnostic).
- `vcpkg.json`: Add SDL3 dependencies (possibly separate `vcpkg-sdl3.json` or a merged file)
- HarvestTask: Should already work — just different library names and patterns
- CI workflows: Extend matrix or add parallel SDL3 workflows

### 5.3 Binding Generation

Use the Phase 4 generator to produce SDL3 bindings:

- Configure for SDL3 headers (from submodule or vendored)
- Map SDL3-specific types (SDL_bool → bool, new handle types, etc.)
- Generate into `src/SDL3.Core/Generated/`, etc.

### 5.4 SDL3 C# Binding Source

For SDL3 bindings, options:

- **Primary**: Use our own CppAst generator (Phase 4)
- **Alternative**: Import from [flibitijibibo/SDL3-CS](https://github.com/flibitijibibo/SDL3-CS) as submodule (similar to SDL2-CS approach) while generator is being built

### 5.5 NuGet Package Topology

```
Janset.SDL3                              ← Meta-package
├── Janset.SDL3.Core
│   └── Janset.SDL3.Core.Native
├── Janset.SDL3.Image
│   └── Janset.SDL3.Image.Native
├── Janset.SDL3.Mixer
│   └── Janset.SDL3.Mixer.Native
└── Janset.SDL3.Ttf
    └── Janset.SDL3.Ttf.Native
```

Note: No SDL3.Gfx (no SDL3 equivalent) and no SDL3.Net (upstream not ready).

### 5.6 Solution Organization

Options for organizing SDL2 + SDL3 in a single repo:

- **Option A**: Single `Janset.SDL.sln` with solution folders for SDL2/ and SDL3/
- **Option B**: Separate solution files: `Janset.SDL2.sln` + `Janset.SDL3.sln` + `Janset.SDL.sln` (combined)
- **Option C**: Solution filters: `.slnf` files to filter by SDL version

Recommendation: Option A with solution filters for developer convenience.

## Exit Criteria

- [ ] SDL3 binding projects compile for all target frameworks
- [ ] SDL3 native packages built via vcpkg for all 7 RIDs
- [ ] CI workflows handle both SDL2 and SDL3 builds
- [ ] Smoke tests pass for SDL3 libraries
- [ ] SDL3 pre-release packages published alongside SDL2 packages
- [ ] Documentation updated to cover both SDL2 and SDL3

## Known Blockers

1. **SDL3_net**: Not available in vcpkg. Monitor upstream `libsdl-org/SDL_net` for 3.x release.
2. **SDL3_shadercross**: Still in preview. Consider adding later when stable.
3. **SDL2_gfx**: No SDL3 equivalent exists. Third-party, frozen library. May not be relevant for SDL3.

## Competitive Landscape

| Project | SDL3 Bindings | Native Packages | vcpkg-Built |
|---------|:------------:|:---------------:|:----------:|
| ppy/SDL3-CS | Yes | Yes (committed) | No (CMake) |
| Alimer.Bindings.SDL | Yes | Yes (committed) | No (CMake) |
| flibitijibibo/SDL3-CS | Yes | No | No |
| bottlenoselabs/SDL3-cs | Yes | Yes (per-RID NuGet) | No (CMake) |
| **Janset.SDL3 (us)** | Yes (planned) | Yes (planned) | **Yes** |

Our differentiator remains: **reproducible vcpkg-based builds with proper NuGet packaging**.
