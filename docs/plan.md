# Project Plan — Janset.SDL2 / Janset.SDL3

> **This is the canonical status document.** When code and docs disagree, verify against the code. When phases and this file disagree, this file wins.

**Last updated**: 2026-04-12
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
| SDL2_mixer | Yes | fluidsynth, libflac, libmodplug, mpg123, opusfile, wavpack | Yes (win-x64 validation; matrix pending) |
| SDL2_ttf | Yes | harfbuzz | Yes (win-x64 validation; matrix pending) |
| SDL2_gfx | Yes | No features (simple library) | Yes (win-x64 validation; matrix pending) |
| SDL2_net | Yes | No features | Yes (win-x64 validation; matrix pending) |

### CI/CD

| Component | Status | Notes |
| --- | --- | --- |
| `prepare-native-assets-main.yml` | Working | Manual trigger, calls 3 platform workflows |
| `prepare-native-assets-windows.yml` | Working | Matrix: x64, x86, arm64; command set now includes all SDL2 satellites with explicit `--rid` (validation pending) |
| `prepare-native-assets-linux.yml` | Working | Matrix: x64 (ubuntu:20.04), arm64 (ubuntu:24.04); command set now includes all SDL2 satellites with explicit `--rid` (validation pending) |
| `prepare-native-assets-macos.yml` | Working | Matrix: x64 (macos-15-intel), arm64 (macos-latest); command set now includes all SDL2 satellites with explicit `--rid` (validation pending) |
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
| Dumpbin tool discovery (Windows) | Working (checks `VCToolsInstallDir` first, then `vswhere` + MSVC candidate probing) |
| BinaryClosureWalker (Linux/ldd) | Working |
| BinaryClosureWalker (macOS/otool) | Working (core implementation complete; further edge-case validation still valuable) |
| ArtifactPlanner + ArtifactDeployer | Working |
| Per-RID status file generation | Working |
| Consolidation to harvest-manifest.json | Working |

## Version Tracking

### Current Versions (vcpkg baseline in working tree: `0b88aacd...`)

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

- [x] Resume development, understand status quo
- [x] Reorganize documentation
- [x] Rewrite AGENTS.md for this repo
- [x] Realign GitHub issues, labels, and milestones to the canonical roadmap
- [x] Validate and stabilize full vcpkg.json coverage (#52)
- [x] Validate the bumped vcpkg baseline — SDL2 2.32.10, all 7 RIDs green (#53)
- [x] Update prepare-native-assets workflows for full SDL2 satellite harvest parity (#76)
- [ ] Document and approve shared native dependency policy (#75)
- [ ] Add a Windows local prerequisites guide (VS tooling ecosystem + dumpbin/vswhere troubleshooting)
- [ ] Clean up native binaries from git history (#56)
- [ ] Correct and validate local development playbook (#57)
- [ ] Create custom Docker build image for Linux x64 (#79)

### Q3 2026

- [ ] Complete Cake PackageTask (harvest → .nupkg)
- [ ] Implement release-candidate-pipeline.yml end-to-end
- [ ] Add SDL2_net bindings + native project
- [ ] Populate all 7 RIDs for all 6 libraries
- [ ] Create smoke tests (basic SDL_Init → SDL_Quit per library)
- [ ] Create sample projects
- [ ] Publish first pre-release to NuGet.org
- [ ] Evaluate NoDependencies native package variant for minimal Linux environments (#80)

### Q4 2026

- [ ] Implement CppAst-based binding auto-generation
- [ ] Replace SDL2-CS imports with auto-generated bindings
- [ ] Add SDL3 bindings + native packages to monorepo
- [ ] Publish SDL3 pre-release packages

### 2027

- [ ] Stabilize SDL2 + SDL3 packages (v1.0)
- [ ] Community feedback incorporation
- [ ] Begin Janset2D development on top of these bindings

## Roadmap Tracking Policy

- GitHub issues are part of the execution model, not optional bookkeeping.
- Work that meaningfully advances the roadmap should be represented in issues with current labels, milestones, and doc references.
- The canonical issue table below is the docs-facing source for current open roadmap issues; GitHub tracker changes should be managed against this table, not a separate tracker-definition script.
- Issue taxonomy should mirror the active canonical phase model, not retired planning structures.
- The active label families are `type:*` for work kind, `area:*` for repo scope, and optional `platform:*` labels when the work is OS-specific.
- Retired label families such as `phase:*`, `component:*`, `topic:*`, `meta:*`, and `process:*` should not be reused.
- Issue bodies should describe current reality, intended outcome, dependencies, and exit criteria.
- PRs are not mandatory for this repo, but commits should reference issues when practical so work can still be traced without a PR layer.
- If an item is deliberately deferred, keep it visible through backlog issues and the parking-lot docs instead of letting it disappear into commit history.

## Canonical Issue Table

This table is the human-readable roadmap map for the current open issue tracker. If the table and GitHub drift, update both in the same change.

Tracker cleanup note: issue `#51` completed the one-time tracker realignment. The rows below cover the active open roadmap issues.

### Phase 2 - CI/CD & Packaging

Primary docs: [phases/phase-2-cicd-packaging.md](phases/phase-2-cicd-packaging.md), [knowledge-base/ci-cd-packaging-and-release-plan.md](knowledge-base/ci-cd-packaging-and-release-plan.md), [knowledge-base/cake-build-architecture.md](knowledge-base/cake-build-architecture.md), [playbook/local-development.md](playbook/local-development.md)

| Issue | Labels |
| --- | --- |
| `#54 Implement PackageTask for native and managed package creation` | `type:enhancement`, `area:build-system`, `area:packaging` |
| `#55 Implement distributed harvest staging for the release-candidate pipeline` | `type:enhancement`, `area:build-system`, `area:ci-cd` |
| `#56 Clean native binaries from git and harden ignore rules` | `type:cleanup`, `area:docs`, `area:native` |
| `#57 Validate and correct the local development playbook` | `type:documentation`, `area:build-system`, `area:docs` |
| `#75 Define shared native dependency collision policy before packaging changes` | `type:research`, `area:native`, `area:packaging`, `area:ci-cd`, `area:testing` |
| `#79 Create custom Docker build image for Linux x64` | `type:enhancement`, `area:ci-cd`, `platform:linux` |
| `#81 Add drift-prevention guardrail for harvest library lists` | `type:hardening`, `area:ci-cd`, `area:build-system` |

### Phase 3 - SDL2 Complete

Primary docs: [phases/phase-3-sdl2-complete.md](phases/phase-3-sdl2-complete.md), [playbook/adding-new-library.md](playbook/adding-new-library.md), [playbook/local-development.md](playbook/local-development.md)

| Issue | Labels |
| --- | --- |
| `#58 Add SDL2_net binding and native package skeleton` | `type:enhancement`, `area:bindings`, `area:native` |
| `#59 Create the SDL2 smoke test suite and CI coverage` | `type:enhancement`, `area:ci-cd`, `area:testing` |
| `#60 Create sample projects under samples/` | `type:enhancement`, `area:docs`, `area:samples` |
| `#61 Add the Janset.SDL2 meta-package` | `type:enhancement`, `area:bindings`, `area:packaging` |
| `#62 Write CONTRIBUTING.md and contributor workflow guidance` | `type:documentation`, `area:docs` |
| `#63 Publish the first SDL2 prerelease packages and release metadata` | `type:enhancement`, `area:packaging`, `area:release` |
| `#64 Document Linux runtime compatibility and minimum glibc support` | `type:documentation`, `area:release`, `area:testing`, `platform:linux` |
| `#80 Evaluate NoDependencies native package variant for minimal Linux environments` | `type:research`, `area:packaging`, `area:native`, `platform:linux` |

### Backlog - Hardening

Primary docs: [plan.md](plan.md), [knowledge-base/ci-cd-packaging-and-release-plan.md](knowledge-base/ci-cd-packaging-and-release-plan.md), [knowledge-base/cake-build-architecture.md](knowledge-base/cake-build-architecture.md)

| Issue | Labels |
| --- | --- |
| `#65 Harden package validation and local feed workflows` | `type:hardening`, `area:build-system`, `area:packaging`, `area:testing` |
| `#66 Harden supply-chain and release integrity workflows` | `type:hardening`, `area:release`, `area:testing` |
| `#68 Add unit tests for the build host` | `type:hardening`, `area:build-system`, `area:testing` |

### Backlog - Parking Lot

Primary docs: [parking-lot.md](parking-lot.md), [knowledge-base/cake-build-architecture.md](knowledge-base/cake-build-architecture.md)

| Issue | Labels |
| --- | --- |
| `#67 Implement external native override support` | `type:enhancement`, `area:build-system`, `area:native` |

### Phase 4 - Binding Auto-Generation

Primary docs: [phases/phase-4-binding-autogen.md](phases/phase-4-binding-autogen.md), [research/binding-autogen-approaches.md](research/binding-autogen-approaches.md)

| Issue | Labels |
| --- | --- |
| `#69 Implement the CppAst binding generator` | `type:enhancement`, `area:bindings`, `area:build-system` |
| `#70 Migrate SDL2 bindings from imported SDL2-CS files to generated code` | `type:enhancement`, `area:bindings` |

### Phase 5 - SDL3 Support

Primary docs: [phases/phase-5-sdl3-support.md](phases/phase-5-sdl3-support.md), [research/sdl3-ecosystem-analysis.md](research/sdl3-ecosystem-analysis.md)

| Issue | Labels |
| --- | --- |
| `#71 Add SDL3 bindings and native packages to the monorepo` | `type:enhancement`, `area:bindings`, `area:native` |
| `#72 Extend CI and packaging flow for SDL3 prereleases` | `type:enhancement`, `area:ci-cd`, `area:packaging`, `area:release` |

## Known Issues

1. **Native binaries committed to git**: Some test binaries were committed to `src/native/*/runtimes/`. Need cleanup + .gitignore rules.
2. **vcpkg/manifest updates need full validation**: full SDL2 coverage and baseline bump are in the working tree, but matrix-level validation is still pending.
3. **Release pipeline is a stub**: `release-candidate-pipeline.yml` has placeholder logic.
4. **No tests**: The only test project (`test/Sandboc/`) is a development utility, not a test suite.
5. **Local dev playbook needs correction**: A playbook exists, but parts of it were inaccurate and not yet validated end-to-end.
6. **Partial CI plumbing exists in code only**: `PathService` exposes harvest-staging helpers and the build host exposes `--use-overrides`, but neither is integrated into the active task/workflow path.
7. **Distributed CI output flow is not wired yet**: current harvest output is still local-first. The release pipeline will need a real staging-vs-consolidated path split so matrix jobs can upload per-RID artifacts before consolidation.
8. **Expanded workflow command set needs matrix validation**: Windows/Linux/macOS workflows now use full satellite harvest commands with explicit RID, but cross-platform matrix stability for this expanded set is not yet validated end-to-end.
9. **Shared native dependency collision policy not formalized**: duplicate basenames across satellite native packages (for example zlib-family binaries) need a documented pre-coding decision and CI guardrail strategy.
10. **Windows local tooling guidance is not explicit enough**: contributors need a dedicated prerequisites guide for VS C++ tooling, Developer PowerShell usage, and dumpbin/vswhere troubleshooting.

## Cross-Reference

- **Detailed phase docs**: [phases/](phases/)
- **Technical deep-dives**: [knowledge-base/](knowledge-base/)
- **Design rationale & research**: [research/](research/)
- **How-to recipes**: [playbook/](playbook/)
- **Deep general references**: [reference/](reference/)
