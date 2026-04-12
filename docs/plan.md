# Project Plan — Janset.SDL2 / Janset.SDL3

> **This is the canonical status document.** When code and docs disagree, verify against the code. When phases and this file disagree, this file wins.

**Last updated**: 2026-04-11
**Maintainer**: Deniz Irgin (@denizirgin)

## Mission

Provide the .NET ecosystem with production-quality, modular SDL2 and SDL3 bindings that include **cross-platform native libraries built from source** — something no other project offers.

## Current Phase

**Phase 2: CI/CD & Packaging** (IN PROGRESS — resumed after ~10 month hiatus)

See [phases/README.md](phases/README.md) for the full phase breakdown.

## Phase Roll-Up

| Phase | Name | Status | Summary |
| --- | --- | --- | --- |
| 1 | SDL2 Core Bindings + Harvesting | **DONE** | C# bindings for 5 libraries, Cake Frosting build system, native binary harvesting pipeline, cross-platform CI workflows |
| 2 | CI/CD & Packaging | **IN PROGRESS** | Complete vcpkg.json for all libraries, NuGet package creation, release pipeline, local dev playbook |
| 3 | SDL2 Complete | PLANNED | All 6 satellites (+ SDL2_net), all 7 RIDs fully populated, tests, samples, NuGet publish |
| 4 | Binding Auto-Generation | PLANNED | CppAst-based generator, replace SDL2-CS imports with auto-generated bindings |
| 5 | SDL3 Support | PLANNED | SDL3 bindings + native packages in same monorepo, shared build infrastructure |

## What Works Today

### Bindings (C#)

| Library | Package ID | Source | Compiles | Targets |
| --- | --- | --- | --- | --- |
| SDL2 | `Janset.SDL2.Core` | `external/sdl2-cs/src/SDL2.cs` (8,966 lines) | Yes | net9.0, net8.0, netstandard2.0, net462 |
| SDL2_image | `Janset.SDL2.Image` | `external/sdl2-cs/src/SDL2_image.cs` (316 lines) | Yes | Same |
| SDL2_mixer | `Janset.SDL2.Mixer` | `external/sdl2-cs/src/SDL2_mixer.cs` (665 lines) | Yes | Same |
| SDL2_ttf | `Janset.SDL2.Ttf` | `external/sdl2-cs/src/SDL2_ttf.cs` (768 lines) | Yes | Same |
| SDL2_gfx | `Janset.SDL2.Gfx` | `external/sdl2-cs/src/SDL2_gfx.cs` (390 lines) | Yes | Same |
| SDL2_net | — | Not yet added | — | — |

### Native Library Build (vcpkg)

| Library | In vcpkg.json | Features Configured | Harvest Tested |
| --- | :---: | --- | :---: |
| SDL2 | Yes | vulkan, alsa, dbus, ibus, samplerate, wayland, x11 | Yes |
| SDL2_image | Yes | avif, libjpeg-turbo, libwebp, tiff | Yes |
| SDL2_mixer | **No** | Needs: mpg123, libflac, opusfile, libmodplug, wavpack, fluidsynth | No |
| SDL2_ttf | **No** | Needs: harfbuzz | No |
| SDL2_gfx | **No** | No features (simple library) | Yes (but not via vcpkg.json) |
| SDL2_net | **No** | No features | No |

### CI/CD

| Component | Status | Notes |
| --- | --- | --- |
| `prepare-native-assets-main.yml` | Working | Manual trigger, calls 3 platform workflows |
| `prepare-native-assets-windows.yml` | Working | Matrix: x64, x86, arm64; currently harvests `SDL2` + `SDL2_image` |
| `prepare-native-assets-linux.yml` | Working | Matrix: x64 (ubuntu:20.04), arm64 (ubuntu:24.04); currently harvests `SDL2` + `SDL2_image` |
| `prepare-native-assets-macos.yml` | Working | Matrix: x64 (macos-13), arm64 (macos-latest); currently harvests `SDL2` + `SDL2_image` |
| `release-candidate-pipeline.yml` | **Stub** | Placeholder logic, not functional |
| Pre-flight version check | Working | Validates manifest.json ↔ vcpkg.json consistency |
| Cake Harvest task | Working | Per-RID binary collection + status files |
| Cake Consolidate task | Working | Merges per-RID results into manifest |
| Cake Package task | **Missing** | Not implemented |
| NuGet publish | **Missing** | Neither internal nor public |

### Build System

| Component | Status |
| --- | --- |
| Cake Frosting host | Working (.NET 9.0, Cake.Frosting 5.0.0) |
| DI-based service architecture | Working (IPathService, IRuntimeProfile, etc.) |
| BinaryClosureWalker (Windows/dumpbin) | Working |
| BinaryClosureWalker (Linux/ldd) | Working |
| BinaryClosureWalker (macOS/otool) | Working (core implementation complete; further edge-case validation still valuable) |
| ArtifactPlanner + ArtifactDeployer | Working |
| Per-RID status file generation | Working |
| Consolidation to harvest-manifest.json | Working |

## Version Tracking

### Current Versions (vcpkg baseline: `41c447cc...`)

| Library | Our Version | vcpkg Latest | Upstream Latest | Action Needed |
| --- | --- | --- | --- | --- |
| SDL2 | 2.32.4 | **2.32.10** | 2.32.10 | Update vcpkg baseline |
| SDL2_image | 2.8.8 | 2.8.8#1 | **2.8.10** | Minor — upstream ahead of vcpkg |
| SDL2_mixer | 2.8.1 | 2.8.1#1 | 2.8.1 | Current |
| SDL2_ttf | 2.24.0 | 2.24.0 | 2.24.0 | Current |
| SDL2_gfx | 1.0.4 | 1.0.4#11 | 1.0.4 | Frozen (3rd party, never updating) |
| SDL2_net | 2.2.0 | 2.2.0#3 | 2.2.0 | Current |

### SDL3 Availability (for Phase 5 planning)

| Library | vcpkg Version | Status |
| --- | --- | --- |
| SDL3 | 3.4.4 | Stable, full feature set |
| SDL3_image | 3.4.2 | Stable |
| SDL3_mixer | 3.2.0#1 | Stable |
| SDL3_ttf | 3.2.2#1 | Stable (+ harfbuzz, svg features) |
| SDL3_net | N/A | **Not in vcpkg** — upstream WIP |
| SDL3_shadercross | 3.0.0-preview2 | Preview (GPU shader translation) |

## Roadmap

### Q2 2026 (Current)

- [ ] Resume development, understand status quo
- [ ] Reorganize documentation
- [ ] Rewrite AGENTS.md for this repo
- [ ] Complete vcpkg.json (add mixer, ttf, gfx, net with features)
- [ ] Update vcpkg baseline to get SDL2 2.32.10
- [ ] Clean up native binaries from git history
- [ ] Correct and validate local development playbook

### Q3 2026

- [ ] Complete Cake PackageTask (harvest → .nupkg)
- [ ] Implement release-candidate-pipeline.yml end-to-end
- [ ] Add SDL2_net bindings + native project
- [ ] Populate all 7 RIDs for all 6 libraries
- [ ] Create smoke tests (basic SDL_Init → SDL_Quit per library)
- [ ] Create sample projects
- [ ] Publish first pre-release to NuGet.org

### Q4 2026

- [ ] Implement CppAst-based binding auto-generation
- [ ] Replace SDL2-CS imports with auto-generated bindings
- [ ] Add SDL3 bindings + native packages to monorepo
- [ ] Publish SDL3 pre-release packages

### 2027

- [ ] Stabilize SDL2 + SDL3 packages (v1.0)
- [ ] Community feedback incorporation
- [ ] Begin Janset2D development on top of these bindings

## Known Issues

1. **Native binaries committed to git**: Some test binaries were committed to `src/native/*/runtimes/`. Need cleanup + .gitignore rules.
2. **vcpkg.json incomplete**: Only SDL2 and SDL2_image declared. Mixer, TTF, Gfx, Net missing.
3. **Release pipeline is a stub**: `release-candidate-pipeline.yml` has placeholder logic.
4. **No tests**: The only test project (`test/Sandboc/`) is a development utility, not a test suite.
5. **Local dev playbook needs correction**: A playbook exists, but parts of it were inaccurate and not yet validated end-to-end.
6. **Partial CI plumbing exists in code only**: `PathService` exposes harvest-staging helpers and the build host exposes `--use-overrides`, but neither is integrated into the active task/workflow path.
7. **Distributed CI output flow is not wired yet**: current harvest output is still local-first. The release pipeline will need a real staging-vs-consolidated path split so matrix jobs can upload per-RID artifacts before consolidation.

## Cross-Reference

- **Detailed phase docs**: [phases/](phases/)
- **Technical deep-dives**: [knowledge-base/](knowledge-base/)
- **Design rationale & research**: [research/](research/)
- **How-to recipes**: [playbook/](playbook/)
- **Deep general references**: [reference/](reference/)
