# Project Plan — Janset.SDL2 / Janset.SDL3

> **This is the canonical status document.** When code and docs disagree, verify against the code. When phases and this file disagree, this file wins.

**Last updated**: 2026-04-14
**Maintainer**: Deniz Irgin (@denizirgin)

## Mission

Provide the .NET ecosystem with production-quality, modular SDL2 and SDL3 bindings that include **cross-platform native libraries built from source** — something no other project offers.

## Current Phase

**Phase 2: CI/CD & Packaging** (IN PROGRESS — resumed after ~10 month hiatus)

Phase 2 is now divided into two stages:

- **Phase 2a — Hybrid Packaging Foundation Spike** (ACTIVE): Prove the hybrid static + dynamic core packaging model end-to-end on win-x64 with SDL2.Core + SDL2.Image. Establish package-consumer smoke test spine, Cake strategy awareness, and local feed validation.
- **Phase 2b — Full Hybrid Pipeline** (NEXT): Generalize to all 7 RIDs, all 6 satellites, full PackageTask, CI guardrails, symbol visibility on Linux/macOS, release pipeline.

See [phases/README.md](phases/README.md) for the full phase breakdown.

## Strategic Decisions — April 2026

These decisions were made during the packaging strategy research cycle (April 2026) and are now locked. Supporting research is in [research/](research/).

| Decision | Detail | Rationale |
| --- | --- | --- |
| **Hybrid Static + Dynamic Core** | Transitive deps statically baked into satellite DLLs; SDL2 core stays dynamic; no separate transitive DLLs shipped | Industry standard (SkiaSharp, Magick.NET, ppy/SDL3-CS, LibGit2Sharp — 7/7 surveyed projects use this pattern). Eliminates the DLL collision class (#75). |
| **Custom vcpkg overlay triplets** | Per-RID hybrid triplets: default `VCPKG_LIBRARY_LINKAGE=static`, per-port override for SDL family to `dynamic` | Keeps all version management in vcpkg.json. No VENDORED builds, no wrapper DLLs. |
| **LGPL-free codec stack** | Drop mpg123, libxmp, fluidsynth from SDL2_mixer. Use bundled permissive alternatives (minimp3, drflac, stb_vorbis, libmodplug, Timidity/Native MIDI). | Eliminates all LGPL exposure. Mixer.Extras.Native package concept is dead. 100% permissive stack across all satellites. |
| **Pure Dynamic rejected** | The ~26-package Common.\* topology has zero precedent in the .NET ecosystem | High maintenance, NuGet graph complexity, no collision safety on Windows. |
| **Execution model: three modes** | Source Mode (fast inner loop), Package Validation Mode (local feed consumer test), Release Mode (published packages) | Avoids forcing one build mode to solve all problems. See [research/execution-model-strategy-2026-04-13.md](research/execution-model-strategy-2026-04-13.md). |
| **Cake build host: strategy-driven evolution** | Three-interface split: IPackagingStrategy, INativeAcquisitionStrategy, IDependencyPolicyValidator. IPayloadLayoutPolicy deferred. | Keeps stable spine (scanners, closure, manifests), adds policy variation. Existing tools (dumpbin/ldd/otool) repurposed as guardrails. |
| **Triplet = strategy** | No `--strategy` CLI flag. Triplet name encodes the strategy. Manifest `runtimes[].strategy` field is the formal mapping, validated against triplet by PreFlightCheck. | Single authority, no two-headed configuration. |
| **Config merge to single manifest.json** | `runtimes.json` and `system_artefacts.json` merge into `manifest.json` (schema v2). Single source of truth for all build configuration. | Eliminates cross-file drift, atomic updates, CI reads one file. |
| **Validator uses vcpkg metadata** | HybridStaticValidator consumes BinaryClosureWalker output. No manually maintained expected-deps lists. | Transitive dep info changes per version; vcpkg metadata + runtime scan = ground truth. |
| **TUnit for build host tests** | TUnit 1.33.0, Microsoft.Testing.Platform runner, NSubstitute for mocking. Test-first approach: characterization tests before refactoring. | Fills the zero-test-coverage gap before any refactoring. See [research/tunit-testing-framework-2026-04-14.md](research/tunit-testing-framework-2026-04-14.md). |
| **Test naming convention** | `<MethodName>_Should_<Verb>_<When/If/Given>` with underscores between words, not in method name | Consistent, readable, project-wide standard. |
| **Remove external/sdl2-cs dependency** | The flibitijibibo/SDL2-CS git submodule will be removed. Current bindings are transitional — not trusted for production testing or long-term use. | SDL2-CS is unmaintained import-style bindings. Phase 4 CppAst generator replaces them entirely. |
| **C++ native smoke test project** | Cross-platform CMake/vcpkg C++ project for directly testing hybrid-built native libraries without P/Invoke layer. IDE-debuggable (Rider/VS/CLion). | Needed for format coverage testing (MP3/FLAC/MOD/MIDI), hybrid bake validation, and diagnosing native vs. P/Invoke issues. Research needed on best IDE integration approach. |

## Phase Roll-Up

| Phase | Name | Status | Summary |
| --- | --- | --- | --- |
| 1 | SDL2 Core Bindings + Harvesting | **DONE** | C# bindings for 5 libraries, Cake Frosting build system, native binary harvesting pipeline, cross-platform CI workflows |
| 2a | Hybrid Packaging Foundation Spike | **ACTIVE** | Prove hybrid model on win-x64 (SDL2.Core + SDL2.Image), package-consumer smoke test, Cake strategy seam, local feed validation |
| 2b | Full Hybrid Pipeline | NEXT | All 7 RIDs, all 6 satellites, full PackageTask, CI guardrails, symbol visibility, release pipeline |
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

### Q2 2026 (Current) — Phase 2a: Foundation Spike

**Completed (infrastructure baseline):**

- [x] Resume development, understand status quo
- [x] Reorganize documentation
- [x] Rewrite AGENTS.md for this repo
- [x] Realign GitHub issues, labels, and milestones to the canonical roadmap
- [x] Validate and stabilize full vcpkg.json coverage (#52)
- [x] Validate the bumped vcpkg baseline — SDL2 2.32.10, all 7 RIDs green (#53)
- [x] Update prepare-native-assets workflows for full SDL2 satellite harvest parity (#76)
- [x] Lock packaging strategy direction: Hybrid Static + Dynamic Core (#75)
- [x] Lock LGPL-free decision: drop mpg123, libxmp, fluidsynth; use bundled permissive alternatives

**Completed — Hybrid Build Proof (3-platform):**

- [x] Create custom vcpkg overlay triplets for win-x64, linux-x64, osx-x64 (#75, closed)
- [x] Validate hybrid build: 6/6 satellites × 3 platforms, zero transitive DLLs/SOs/dylibs (#75, closed)
- [x] Update vcpkg.json: remove LGPL features, create sdl2-mixer overlay port with bundled alternatives (#84, closed)
- [x] Update runtimes.json with hybrid triplets, manifest.json with validation_mode
- [x] Symbol visibility validation: zlib/libpng = 0 leaks on all platforms, FreeType/WebP cosmetic leaks accepted
- [x] Cross-platform line ending normalization (.gitattributes eol=lf)

**Completed — Runtime Validation (3-platform):**

- [x] Create C++ native validation sandbox: headless + interactive smoke test, 6 satellites × codec coverage, 13/13 PASS on win-x64, linux-x64, osx-x64. CMake/vcpkg, IDE-debuggable (CLion/VS/VS Code)

**Active — Packaging Infrastructure (test-first, docs-first):**

- [ ] Update canonical docs for config merge, triplet=strategy, TUnit adoption decisions (#85)
- [x] Create TUnit test project for Cake build host with characterization tests on current code (#85) — baseline established (129 tests passing)
- [ ] Merge 3 config files into single manifest.json (schema v2) — runtimes + system_exclusions + library_manifests (#85)
- [ ] Introduce Cake build host strategy awareness: IPackagingStrategy, IDependencyPolicyValidator, INativeAcquisitionStrategy (#85)
- [ ] Repurpose BinaryClosureWalker + runtime scanners as guardrails: transitive dep leak in hybrid mode = build failure (#85)
- [ ] Extract HarvestPipeline service from HarvestTask (#85)
- [ ] Harden build host testing architecture for refactor readiness: mirror `_build` boundaries, remove mirrored test logic, add missing task/scanner/provider/tool coverage (#85)
- [ ] Establish deterministic build-host coverage workflow and ratchet policy (`dotnet test ... -- --coverage`) with no-regression gate and branch tracking (#85)
- [ ] Implement minimal PackageTask for win-x64 (SDL2.Core + SDL2.Image → .nupkg → local folder feed) (#83)
- [ ] Create package-consumer smoke test project: PackageReference → local feed restore → SDL_Init + IMG_Load("test.png") (#83)

**Deferred to Phase 2b / Q3:**

- [ ] Add a Windows local prerequisites guide (VS tooling ecosystem + dumpbin/vswhere troubleshooting)
- [ ] Clean up native binaries from git history (#56)
- [ ] Correct and validate local development playbook (#57)
- [ ] Create custom Docker build image for Linux x64 (#79)

### Q3 2026 — Phase 2b: Full Hybrid Pipeline

- [ ] Create hybrid overlay triplets for remaining 4 RIDs (win-x86, win-arm64, linux-arm64, osx-arm64)
- [ ] Update manifest.json runtimes section with all hybrid triplet mappings
- [ ] Generalize PackageTask to all 6 satellites × 7 RIDs
- [ ] Generalize Cake strategy services beyond spike scope
- [ ] Add Linux version scripts for symbol visibility (`.map` files per satellite) — lower priority
- [ ] Implement full package-consumer smoke test matrix (win-x64, linux-x64, osx-arm64 minimum)
- [ ] Implement release-candidate-pipeline.yml end-to-end
- [ ] Add SDL2_net bindings + native project
- [ ] Validate SDL2_mixer LGPL-free build across all RIDs
- [ ] Create sample projects
- [ ] Publish first pre-release to NuGet.org
- [ ] Evaluate NoDependencies native package variant for minimal Linux environments (#80)
- [ ] Research and POC linux-musl-x64/arm64 native asset coverage (#82)

### Q4 2026 — Phases 3–4

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

### Phase 2a - Hybrid Packaging Foundation Spike (ACTIVE)

Primary docs: [phases/phase-2-cicd-packaging.md](phases/phase-2-cicd-packaging.md), [research/packaging-strategy-hybrid-static-2026-04-13.md](research/packaging-strategy-hybrid-static-2026-04-13.md), [research/license-inventory-2026-04-13.md](research/license-inventory-2026-04-13.md), [research/execution-model-strategy-2026-04-13.md](research/execution-model-strategy-2026-04-13.md)

| Issue | Labels |
| --- | --- |
| `#75 Implement hybrid static packaging foundation (was: collision policy)` | `type:enhancement`, `area:native`, `area:packaging`, `area:build-system`, `area:testing` |
| `#83 Hybrid Packaging Foundation Spike — win-x64, SDL2.Core + SDL2.Image` | `type:enhancement`, `area:native`, `area:packaging`, `area:build-system` |
| `#84 Transition to LGPL-free SDL2_mixer codec stack` | `type:enhancement`, `area:native`, `area:packaging` |
| `#85 Introduce packaging strategy awareness in Cake build host` | `type:enhancement`, `area:build-system`, `area:testing` |

### Phase 2b - Full Hybrid Pipeline

Primary docs: [phases/phase-2-cicd-packaging.md](phases/phase-2-cicd-packaging.md), [knowledge-base/ci-cd-packaging-and-release-plan.md](knowledge-base/ci-cd-packaging-and-release-plan.md), [knowledge-base/cake-build-architecture.md](knowledge-base/cake-build-architecture.md), [playbook/local-development.md](playbook/local-development.md)

| Issue | Labels |
| --- | --- |
| `#54 Implement PackageTask for native and managed package creation` | `type:enhancement`, `area:build-system`, `area:packaging` |
| `#55 Implement distributed harvest staging for the release-candidate pipeline` | `type:enhancement`, `area:build-system`, `area:ci-cd` |
| `#56 Clean native binaries from git and harden ignore rules` | `type:cleanup`, `area:docs`, `area:native` |
| `#57 Validate and correct the local development playbook` | `type:documentation`, `area:build-system`, `area:docs` |
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
| `#82 Add linux-musl-x64 and linux-musl-arm64 native asset coverage` | `type:research`, `area:native`, `area:ci-cd`, `platform:linux` |

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
2. **Release pipeline is a stub**: `release-candidate-pipeline.yml` has placeholder logic.
3. **Build host tests exist, but depth is uneven**: `build/_build.Tests` has a healthy baseline count, but refactor-sensitive orchestration/scanner/provider areas remain under-covered and coverage percentages are still low relative to refactor risk.
4. **Local dev playbook needs correction**: A playbook exists, but parts of it were inaccurate and not yet validated end-to-end.
5. **`--use-overrides` is parsed but not wired**: Legacy flag, to be reframed as `--native-source overrides` during Cake strategy refactor.
6. **Distributed CI output flow is not wired yet**: current harvest output is still local-first. The release pipeline will need a real staging-vs-consolidated path split so matrix jobs can upload per-RID artifacts before consolidation.
7. **Hybrid triplets not yet created**: Custom overlay triplets for the hybrid static model are designed but not yet implemented. Spike will prove the model on win-x64.
8. **Symbol visibility unaddressed on Linux/macOS**: vcpkg does not set `-fvisibility=hidden` by default. Windows is safe (PE export-opt-in). Linux/macOS need custom triplet flags or linker version scripts. Deferred to Phase 2b.
9. **Windows local tooling guidance is not explicit enough**: contributors need a dedicated prerequisites guide for VS C++ tooling, Developer PowerShell usage, and dumpbin/vswhere troubleshooting.
10. **Build-host test topology drift**: test folders are not yet fully aligned to `_build` production boundaries (`Modules`/`Tasks`/`Tools`), which raises maintenance cost and weakens whitebox/blackbox separation.

## Cross-Reference

- **Detailed phase docs**: [phases/](phases/)
- **Technical deep-dives**: [knowledge-base/](knowledge-base/)
- **Design rationale & research**: [research/](research/)
- **How-to recipes**: [playbook/](playbook/)
- **Deep general references**: [reference/](reference/)
