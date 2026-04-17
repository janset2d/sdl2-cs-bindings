# Project Plan — Janset.SDL2 / Janset.SDL3

> **This is the canonical status document.** When code and docs disagree, verify against the code. When phases and this file disagree, this file wins.

**Last updated**: 2026-04-17
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
| **Cake build host: strategy-driven evolution** | Three-interface split: IPackagingStrategy, INativeAcquisitionStrategy, IDependencyPolicyValidator. IPayloadLayoutPolicy deferred. **State as of 2026-04-17:** the strategy seam is partially landed as-designed, not a dispatcher. `IPackagingStrategy` = string-compare helper (`IsCoreLibrary`) only. `IDependencyPolicyValidator` has one real implementation (`HybridStaticValidator` consumes scanner output as a guardrail — real business logic) and one explicit pass-through (`PureDynamicValidator` — legacy-compat, returns Pass unconditionally by design per [cake-strategy-implementation-brief-2026-04-14.md](research/cake-strategy-implementation-brief-2026-04-14.md)). `INativeAcquisitionStrategy` was designed in the brief (VcpkgBuild / Overrides / CiArtifact) but has **not been implemented**; Source Mode via Stream F implicitly covers the same problem space along a different axis. `IPayloadLayoutPolicy` was deferred in the brief until PackageTask landed; PackageTask has landed, the policy extraction has not — Packaging module hard-codes layout today. The Packaging module does not consume `IPackagingStrategy` at all; pack output shape is identical for hybrid and pure-dynamic RIDs. See [phases/phase-2-adaptation-plan.md "Strategy State Audit"](phases/phase-2-adaptation-plan.md) for the brief-vs-code gap table. | Keeps stable spine (scanners, closure, manifests), adds policy variation. Existing tools (dumpbin/ldd/otool) repurposed as guardrails — this specific scanner-as-validator role change is fully implemented (`BinaryClosureWalker` → `HybridStaticValidator` second-consumer pattern, see brief §"Scanner Repurposing"). |
| **Triplet = strategy** | No `--strategy` CLI flag. Triplet name encodes the strategy. Manifest `runtimes[].strategy` field is the formal mapping, validated against triplet by PreFlightCheck. | Single authority, no two-headed configuration. |
| **Config merge to single manifest.json** | `runtimes.json` and `system_artefacts.json` merge into `manifest.json` (schema v2). Single source of truth for all build configuration. | Eliminates cross-file drift, atomic updates, CI reads one file. |
| **Validator uses vcpkg metadata** | HybridStaticValidator consumes BinaryClosureWalker output. No manually maintained expected-deps lists. | Transitive dep info changes per version; vcpkg metadata + runtime scan = ground truth. |
| **TUnit for build host tests** | TUnit 1.33.0, Microsoft.Testing.Platform runner, NSubstitute for mocking. Test-first approach: characterization tests before refactoring. | Fills the zero-test-coverage gap before any refactoring. See [research/tunit-testing-framework-2026-04-14.md](research/tunit-testing-framework-2026-04-14.md). |
| **Test naming convention** | `<MethodName>_Should_<Verb>_<When/If/Given>` with underscores between words, not in method name | Consistent, readable, project-wide standard. |
| **Remove external/sdl2-cs dependency** | The flibitijibibo/SDL2-CS git submodule will be removed. Current bindings are transitional — not trusted for production testing or long-term use. | SDL2-CS is unmaintained import-style bindings. Phase 4 CppAst generator replaces them entirely. |
| **C++ native smoke test project** | Cross-platform CMake/vcpkg C++ project for directly testing hybrid-built native libraries without P/Invoke layer. IDE-debuggable (Rider/VS/CLion). | Needed for format coverage testing (MP3/FLAC/MOD/MIDI), hybrid bake validation, and diagnosing native vs. P/Invoke issues. Research needed on best IDE integration approach. |
| **Package family as release unit** | Managed + .Native always share the same version (family version) and release together. Families version independently from each other. | Eliminates version matrix between managed and native. SkiaSharp/Magick.NET/Avalonia pattern. See [knowledge-base/release-lifecycle-direction.md](knowledge-base/release-lifecycle-direction.md). |
| **Tag-derived family versioning** | Family version derived from git tags with family-specific prefixes (`sdl2-core-1.0.0`, `sdl2-image-1.0.3`). No manual version edits in project files. Family identifier convention: `sdl<major>-<role>` (canonical, see [knowledge-base/release-lifecycle-direction.md §1](knowledge-base/release-lifecycle-direction.md#1-ubiquitous-language)). | Zero-config, tag-based, monorepo-native versioning. The mandatory `sdl<major>-` prefix mirrors the `Janset.SDL2.*` PackageId convention and disambiguates SDL2 from SDL3 families in advance. Current tooling: MinVer 7.0.0. |
| **Hybrid release governance** | Targeted release per-family by default. Forced full-train release on cross-cutting changes (vcpkg baseline, triplet/strategy, shared toolchain, validation guardrails). | Fast iteration for isolated changes, coherence guarantee for infrastructure changes. |
| **Dependency contracts: minimum range everywhere (SkiaSharp pattern, revised S1 2026-04-17)** | Within-family AND cross-family: minimum version constraint (`>=`). Drift protection is orchestration-time (Cake `PackageTask` packs both family members at identical `--family-version` in one invocation; post-pack validator G23 asserts the emitted `<version>` elements match byte-for-byte). Previous design (exact pin within family via Mechanism 3) was proven mechanically but retired because it depended on MSBuild global-property propagation through NuGet's pack-time sub-eval, which `NuGet.Build.Tasks.Pack.targets` replaces rather than extends — unchanged for 8+ years, no upstream fix in .NET 10. See [knowledge-base/release-lifecycle-direction.md §4 Drift Protection Model](knowledge-base/release-lifecycle-direction.md) and [phases/phase-2-adaptation-plan.md "S1 Adoption Record"](phases/phase-2-adaptation-plan.md). | Industry-standard SkiaSharp / Avalonia / OpenTelemetry convention. Orchestration-time drift protection is sufficient because Cake owns both pack invocations per family; consumer-side exact pin was belt-and-suspenders against a scenario prevented by construction. |
| **CI matrix: 7 RID jobs, not library×RID** | One job per RID. vcpkg installs all libraries per-triplet, Cake harvests per-library within the job. Matrix generated dynamically from manifest.json. | vcpkg manifest mode is all-or-nothing. Dynamic matrix eliminates YAML↔manifest drift. |
| **Three-stage release promotion** | Local folder feed → internal feed (GitHub Packages) → public NuGet.org. Each stage is a deliberate, gated step. | Prevents accidental public releases. Matches ci-cd-packaging-and-release-plan.md design. |
| **Cake as single orchestration surface** | Change detection (dotnet-affected), versioning (NuGet.Versioning), harvesting, packaging, validation — all through Cake. CI workflows are execution triggers, not logic containers. | Centralized policy authority. CI YAML stays thin. |
| **Source-graph and shipping-graph native payload mechanisms are separate** | **Policy (Source Mode):** solution-root `Directory.Build.targets` + `$(JansetSdl2SourceMode)` opt-in + `artifacts/native-staging/` (gitignored). Platform-branched copy: Windows `<Content>` + `CopyToOutputDirectory`, Linux/macOS `<Target>` + `<Exec cp -a>` (preserves symlink chains at 1× size, zero duplication). Tar.gz is NOT used in Source Mode. **Policy (shipping graph):** tar.gz packaging + `buildTransitive/*.targets` extraction for NuGet consumers, to work around NuGet ZIP's symlink limitation. **Current state (shipping graph):** tar.gz harvest in Cake is implemented; per-satellite `buildTransitive/*.targets` and the Unix untar consumer-side target are **not yet implemented repo-wide** (only `Janset.SDL2.Core.Native.targets` exists today, and it is .NET Framework + Windows-only). Bringing the shipping-graph policy to parity is part of Stream D-local execution. | Empirical evidence 2026-04-15: Windows PoC (worktree) verified `<Content>` propagation through `ProjectReference` chains and platform gating. Linux PoC (WSL Ubuntu, 2-level chain) and macOS PoC (SSH Intel Mac, Darwin 24.6, 3-level dylib chain) each measured `<Content>`-only 3× duplication vs `<Exec cp -a>` 1× preservation. Symlink-specific measurements apply to Linux and macOS only — Windows native layouts have no symlinks to measure. Unification of source-graph and shipping-graph mechanisms would require a concrete rationale; default is separation. See [`research/source-mode-native-visibility-2026-04-15.md`](research/source-mode-native-visibility-2026-04-15.md). |
| **Cake two-source framework for Source Mode** | `dotnet cake --target=Source-Mode-Prepare --rid=<rid> --source=local\|remote` — contributors never touch vcpkg directly. `--source=local` wraps existing vcpkg + harvest (host-RID only, 2a scope). `--source=remote --url=<url>` is intended to download an archive from the URL and extract it into staging (any RID, incl. non-host). Both paths are designed to produce an identical staging layout. **Open contracts (2b):** the **producer contract** (which workflow publishes artifacts, artifact granularity, auth) and the **archive contract** (format, internal layout, symlink preservation guarantee) are both unresolved. Treat the remote summary as a direction, not a settled implementation. | Single orchestration surface principle. Option 1 (remote) is the **directional** answer to PD-5 (non-host RID acquisition); full mechanism — producer + archive contracts — is still open. |

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
| Cake Frosting host | Working (.NET 9.0, Cake.Frosting 6.1.0) |
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

- [x] Update canonical docs for config merge, triplet=strategy, TUnit adoption decisions (#85)
- [x] Create TUnit test project for Cake build host with characterization tests on current code (#85) — 241 tests passing, 62.62% line, 50.96% branch at last measured coverage baseline
- [x] Merge 3 config files into single manifest.json (schema v2) — runtimes + system_exclusions + library_manifests (#85)
- [x] Introduce Cake build host strategy awareness: IPackagingStrategy, IDependencyPolicyValidator — contracts + implementations + runtime wiring landed (DI + Harvest validation + PreFlight coherence) (#85)
- [x] Wire strategy layer into runtime: Program.cs DI registration, HarvestTask validation step, PreFlightCheck coherence (#85)
- [x] Repurpose BinaryClosureWalker + runtime scanners as guardrails: transitive dep leak in hybrid mode = build failure (#85)
- [ ] Extract HarvestPipeline service from HarvestTask (#87)
- [x] Harden build host testing architecture for refactor readiness: all layers covered (247 tests passing; coverage ratchet policy active via #86) (#85, #68)
- [x] Establish coverage ratchet policy: static floor in `build/coverage-baseline.json` (60.0% line / 49.0% branch, measured 62.62% / 50.96%), Cake `Coverage-Check` task enforces the floor via cobertura parse (#86). CI gate wiring deferred to Stream C PreflightGate.
- [x] Align Coverage and PreFlight service boundaries closer to the Harvesting reference pattern: injectable coverage threshold validator, typed PreFlight validator results, and dedicated `vcpkg.json` reader-backed task orchestration cleanup.

> **#85 Handoff (2026-04-14, updated 2026-04-16)**
> **Landed:** Strategy primitives (IPackagingStrategy, IDependencyPolicyValidator, StrategyResolver), OneOf result pattern (ValidationResult/ValidationError/ValidationSuccess), config merge (schema v2), runtime wiring (Program.cs DI + HarvestTask validation invocation + PreFlightCheck coherence invocation), composition-root DI resolution tests via `ConfigureBuildServices` seam (Hybrid + PureDynamic), concurrency fix in async extensions, coverage ratchet (#86), shared build-error/result cleanup, harvest error tightening, the follow-up PreFlight cleanup that aligned validators/reporting closer to Harvesting-style service boundaries (DI-loaded `ManifestConfig`, dedicated `vcpkg.json` reader, typed validator results, `IStrategyResolver`, reporter owning Cake context), and the Coverage alignment pass that moved the threshold rule behind an injectable validator boundary. Build-host suite is now 247 tests green.
> **Tracker state:** #85 is complete and closes on delivered strategy wiring. Cleanup follow-up lives in #87.
> **Remaining:** HarvestPipeline extraction only, tracked by #87.
> **Next:** Stream D-local's Phase 2a scope is now landed for the `sdl2-core` + `sdl2-image` proof slice: `Package` + post-pack nuspec assertion + `PackageConsumerSmoke` local-feed validation all pass on win-x64 at `1.3.0-dlocal.1`. Immediate next work shifts to PA-1 / PA-2 alignment before Stream C CI modernization. HarvestPipeline extraction (#87) is still intentionally deferred. Broader package-consumer matrix coverage remains 2b work.

**Release Lifecycle Adaptation** (see [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md)):

- [x] Stream A-safe: manifest.json `package_families` schema + NuGet.Versioning in Cake (manifest schema v2.1 + build host package reference landed)
- [x] Stream A0: Exact pin spike — mechanism proven 2026-04-16 (`PrivateAssets="all"` + explicit `PackageReference` with bracket notation), then **RETIRED 2026-04-17 (S1 adoption)** because production orchestration hit upstream NuGet limitations. See [phases/phase-2-adaptation-plan.md "S1 Adoption Record"](phases/phase-2-adaptation-plan.md) and [research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md](research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (SUPERSEDED).
- [x] Stream A-risky: MinVer 7.0.0 + exact-pin csproj rollout (Mechanism 3) + family identifier rename to canonical `sdl<major>-<role>` + MSBuild guard target + PreFlight `CsprojPackContractValidator` (G1-G8 + G17-G18) — landed 2026-04-16. PD-1 resolved, PD-9 opened. 256/256 build-host tests green at A-risky closure. **Partially reverted 2026-04-17 (S1 adoption):** Mechanism 3 csproj shape, sentinel PropertyGroup, `_GuardAgainstShippingRestoreSentinel` target, and G1/G2/G3/G5/G8 validator invariants removed in Phase 3. MinVer + family rename + G4/G6/G7/G17/G18 retained. See [phases/phase-2-adaptation-plan.md Stream A-risky](phases/phase-2-adaptation-plan.md).
- [x] Stream B: Strategy wiring (#85, closed) — DI + HarvestTask validator + PreFlight strategy coherence; HarvestPipeline extraction **split to follow-up issue #87**
- [ ] **PA-1: Matrix strategy review** — alignment on RID-only vs `strategy × RID` vs parity-job before Stream C CI workflow migration (see [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md#open-alignment-items-pre-stream-c))
- [ ] **PA-2: Hybrid overlay triplet expansion** — add overlay triplets for remaining 4 RIDs (`x86-windows-hybrid`, `arm64-windows-hybrid`, `arm64-linux-hybrid`, `arm64-osx-hybrid`); Stream C prerequisite (see [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md#open-alignment-items-pre-stream-c))
- [ ] Stream C: PreFlightCheck as CI gate, dynamic matrix generation from manifest, CI workflow migration
- [x] Stream D-local: PackageTask with family version (#54, #83 closed), package-consumer smoke test, local folder feed. D-local landed for the Phase 2a proof slice on 2026-04-16 under the pre-S1 guardrail set (G20–G27 post-pack exact-pin assertions). **S1 adoption (2026-04-17) retired G20 and G24; D-local post-pack guardrails narrow to G21 (minimum-range dependency shape), G22 (TFM consistency), G23 (within-family version match — now the primary coherence check), G25–G27 (symbols, repo, metadata), G46 (MSBuild payload guard), G47 (buildTransitive contract presence), G48 (per-RID native payload shape — DLLs on Windows, `$(PackageId).tar.gz` on Unix).** Phase 3 code changes (2026-04-17) collapsed the 4-step pack orchestration to 2-step, rewrote `PackageOutputValidator` onto the Result pattern with accumulating checks, added consumer-side `buildTransitive/Janset.SDL2.Native.Common.targets` for Unix `tar -xzf` extraction and .NETFramework AnyCPU DLL copy, and fixed cross-platform path-separator / filename-collision regressions exposed during 3-platform verification. **Verified 2026-04-17: PostFlight end-to-end green on all three hybrid-static RIDs (`win-x64`, `linux-x64`, `osx-x64`) for the `sdl2-core` + `sdl2-image` proof slice; 273/273 build-host tests.** Scope note: the four pure-dynamic RIDs (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) are not covered end-to-end — no overlay hybrid triplet exists for them yet (PA-2) and pure-dynamic behavioral validation is a no-op by design (see strategy-driven evolution state note above). Broader consumer-smoke matrix expansion plus pure-dynamic behavior stays in 2b.
- [ ] Stream D-ci: CI package-publish job + smoke gate + internal feed push
- [ ] Stream E: **2a scope = feasibility spike only** (full `DetectChangesTask` + CI filtering deferred to Phase 2b)
- [ ] Stream F: Local-dev native acquisition + Source Mode payload visibility — Cake two-source framework (`--source=local|remote`), platform-branched `Directory.Build.targets` (Windows `<Content>` / Unix `<Exec cp -a>`). Mechanism empirically verified on Windows (worktree), Linux (WSL, 2-level chain), and macOS (SSH Intel Mac, 3-level dylib chain). 2a delivers `--source=local` (host-RID) end-to-end; `--source=remote` interface wired, producer/URL/auth mechanism open, full impl 2b. PD-4 resolved + PD-5 **direction locked** via [research/source-mode-native-visibility-2026-04-15.md](research/source-mode-native-visibility-2026-04-15.md)
- [ ] PD-6: `.NET Framework` (`net462`) source-mode visibility — **must land before any `net462` in-tree test is added**

**Deferred to Phase 2b / Q3:**

- [ ] Add a Windows local prerequisites guide (VS tooling ecosystem + dumpbin/vswhere troubleshooting)
- [ ] Clean up native binaries from git history (#56)
- [ ] Correct and validate local development playbook (#57)
- [ ] Create custom Docker build image for Linux x64 (#79)

### Q3 2026 — Phase 2b: Full Hybrid Pipeline

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
| `#83 Hybrid Packaging Foundation Spike — win-x64, SDL2.Core + SDL2.Image` | `type:enhancement`, `area:native`, `area:packaging`, `area:build-system` |
| `#87 Extract HarvestPipeline service from HarvestTask` | `type:cleanup`, `area:build-system`, `area:testing` |

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

1. **Native binaries in git history (working-tree cleaned 2026-04-15)**: Stale payloads under `src/native/<Lib>/runtimes/` have been removed from tracking (74 files across 5 `.Native` packages) and `.gitignore` rule `src/native/*/runtimes/` added. **Pending:** history rewrite via `git-filter-repo` to drop ~30 MB of those stale payloads from past commits, followed by `git push --force-with-lease` on `master` and `nugetizer`. Local clones on WSL/Mac will need fresh `git clone` (or `git fetch origin && git reset --hard origin/<branch>`). 5 issues (#52, #53, #76, #77, #78) reference 4 repo commit SHAs that will be rewritten — post-rewrite remap via `.git/filter-repo/commit-map` is optional cleanup.
2. **Release pipeline is a stub**: `release-candidate-pipeline.yml` has placeholder logic.
3. **HarvestPipeline extraction still pending after strategy wiring**: `IPackagingStrategy`, `IDependencyPolicyValidator`, and `StrategyResolver` are now wired into Program.cs DI and invoked from HarvestTask/PreFlightCheck. PreFlight follow-up cleanup also landed, and the build-host suite is 241 tests green. Remaining orchestration cleanup is tracked in #87.
4. **Local dev playbook needs correction**: A playbook exists, but parts of it were inaccurate and not yet validated end-to-end.
5. **Native source-mode selector is not implemented yet**: active build-host flow assumes vcpkg-built natives; multi-source acquisition remains deferred to the strategy-refactor backlog.
6. **Distributed CI output flow is not wired yet**: current harvest output is still local-first. The release pipeline will need a real staging-vs-consolidated path split so matrix jobs can upload per-RID artifacts before consolidation.
7. **Hybrid triplets created for 3 primary RIDs**: `x64-windows-hybrid`, `x64-linux-hybrid`, `x64-osx-hybrid` overlay triplets exist in `vcpkg-overlay-triplets/`. Remaining 4 pure-dynamic RIDs (win-x86, win-arm64, linux-arm64, osx-arm64) use stock triplets. **Pulled into Phase 2a as a Stream C prerequisite** — tracked as PA-2 in [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md#open-alignment-items-pre-stream-c). Without hybrid expansion, the dynamic CI matrix exercises the hybrid validator only in 3/7 jobs.
8. **Symbol visibility analyzed, hardening deferred**: Symbol visibility analysis complete (`docs/research/symbol-visibility-analysis-2026-04-14.md`). zlib/libpng = 0 leaks on all platforms. FreeType/WebP have cosmetic leaks, accepted. Further hardening (version scripts, `-fvisibility=hidden`) deferred to Phase 2b.
9. **Windows local tooling guidance is not explicit enough**: contributors need a dedicated prerequisites guide for VS C++ tooling, Developer PowerShell usage, and dumpbin/vswhere troubleshooting.
10. **Build-host test topology drift**: test folders are not yet fully aligned to `_build` production boundaries (`Modules`/`Tasks`/`Tools`), which raises maintenance cost and weakens whitebox/blackbox separation.
11. **Release lifecycle direction locked, implementation partially designed**: Package family model, tag-derived versioning, hybrid release governance, dynamic CI matrix, three-stage promotion — all locked in `docs/knowledge-base/release-lifecycle-direction.md` (including the "Two Version Planes" section clarifying upstream library version vs family version, and the canonical `sdl<major>-<role>` family identifier convention adopted 2026-04-16). Implementation plan split into Streams A-safe / A-risky, A0, B, C, D-local / D-ci, E (scope-reduced), F — see `docs/phases/phase-2-adaptation-plan.md`. **PD-4 mechanism locked 2026-04-15** — verified on Windows (worktree), Linux (WSL Ubuntu, 2-level chain), and macOS (SSH Intel Mac, Darwin 24.6, 3-level dylib chain); platform-branched (`<Content>` on Windows, `<Exec cp -a>` on Unix, symlink chains preserved at 1× size); end-to-end validation with real SDL2 natives pending Stream F execution — see [`docs/research/source-mode-native-visibility-2026-04-15.md`](research/source-mode-native-visibility-2026-04-15.md). **PD-5 direction locked 2026-04-15** via the same doc's two-source framework (`--source=remote --url=<url>`); concrete mechanism (URL convention, producer workflow, auth, caching) unresolved — 2b scope. **PD-2 WITHDRAWN 2026-04-17 (S1 adoption)** — resolved 2026-04-16 as Mechanism 3 (bracket-notation CPM + `PrivateAssets="all"`), retired 2026-04-17 because the mechanism depended on MSBuild global-property propagation through NuGet's pack-time `_GetProjectVersion` sub-eval, which `NuGet.Build.Tasks.Pack.targets:335` implements with `Properties=` (globals-replace, not globals-extend); unchanged for 8+ years and identical in .NET 10 SDK; no in-flight upstream fix. S1 adopted SkiaSharp-style minimum range for within-family deps and moved drift protection to Cake orchestration time. See [`docs/phases/phase-2-adaptation-plan.md` "S1 Adoption Record"](phases/phase-2-adaptation-plan.md), PD-11 in the same doc's Pending Decisions table, and [`docs/research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (SUPERSEDED). **PD-1 resolved 2026-04-16** — MinVer 7.0.0 sets `$(Version)` correctly on native csprojs even with `<IncludeBuildOutput>false</IncludeBuildOutput>`; empirically verified during A-risky implementation; pack of `Janset.SDL2.Image.Native` produces correctly-versioned nuspec from MinVer-derived version. **PD-3, PD-6 still open; PD-5 direction locked with mechanism open** — see adaptation plan's Pending Decisions table.
12. **Full-train release orchestration is unresolved (PD-7, opened 2026-04-16)**: Canonical direction defines *what* a full-train release is and *when* one is required, but the *how* (tag invocation, release ordering enforcement, GitHub Release UX, failure recovery, release-notes aggregation) is open. Interim operational mechanism is manual multi-tag push (Path A). Research placeholder published at [`docs/research/full-train-release-orchestration-2026-04-16.md`](research/full-train-release-orchestration-2026-04-16.md) with scope, three candidate paths (manual multi-tag / meta-tag + `release-set.json` workflow / pack-time override / hybrid), decision criteria, and peer-project precedents to survey. Blocks Stream D-ci CI release pipeline. Does **not** block A-risky, A-safe, A0, B, C, D-local, or F — csproj-level shape is orthogonal to orchestration-layer mechanism. See PD-7 in [`docs/phases/phase-2-adaptation-plan.md`](phases/phase-2-adaptation-plan.md#pending-decisions).
13. **Release recovery + manual escape hatch unresolved (PD-8, opened 2026-04-16)**: All happy-path release flow (individual + full-train) is GitHub-driven. The unhappy path (CI broken, partial-train failure, mid-train recovery, emergency hotfix) is unspecified. Manual escape hatch exists in principle (operator replicates CI step-for-step by hand using the documented `dotnet restore` + `dotnet pack --no-restore` + `dotnet nuget push` sequence) but lacks Cake helpers, API key provisioning policy, audit trail mechanism, and a `playbook/release-recovery.md` runbook. Research placeholder published at [`docs/research/release-recovery-and-manual-escape-hatch-2026-04-16.md`](research/release-recovery-and-manual-escape-hatch-2026-04-16.md) with two escape-hatch categories (individual + full-train), seven research questions, decision criteria, industry precedents to survey. Blocks Stream D-local Cake helper exposure + Stream D-ci publish pipeline (manual flow must mirror CI flow). See PD-8 in [`docs/phases/phase-2-adaptation-plan.md`](phases/phase-2-adaptation-plan.md#pending-decisions).
14. **Within-family exact-pin auto-derivation (PD-9) — CLOSED 2026-04-17 (not applicable post-S1)**: PD-9 opened 2026-04-16 tracked the frontier of making bracket-notation `PackageVersion` capture MinVer's `$(Version)` without explicit Cake orchestration. Investigation during D-local integration identified a structural limitation in NuGet's pack-time sub-evaluation: CLI globals don't propagate into the ProjectReference walk where `_GetProjectVersion` resolves, so the sentinel-fallback path triggers regardless of what Cake passes at the outer invocation. Our best-diagnosed mechanical explanation is the `<MSBuild Properties="BuildProjectReferences=false;">` invocation at `NuGet.Build.Tasks.Pack.targets:335`, which appears to replace the child eval's globals rather than extend them; the specific code path has shipped unchanged for 8+ years, is identical in .NET 10 SDK, and has no upstream fix in flight. **S1 adoption (2026-04-17) retired the exact-pin requirement itself**, so PD-9's frontier is no longer relevant to this project. Industry survey (LibGit2Sharp hardcodes literal, SkiaSharp uses minimum range, Avalonia minimum range, Magick.NET hardcodes, SDL3-CS bundles) retroactively confirmed the S1 path. See [`docs/phases/phase-2-adaptation-plan.md` "S1 Adoption Record"](phases/phase-2-adaptation-plan.md), PD-9 / PD-11 in the adaptation plan, and [`artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md`](../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md) (investigation evidence; specific mechanism treated as supporting evidence rather than sole definitive cause).
15. **Comprehensive guardrail roadmap published (2026-04-16; amended 2026-04-17)**: Originally 45 guardrails enumerated across 8 categories. **S1 adoption (2026-04-17) retired 9 guardrails** (G1/G2/G3/G5/G8/G9/G10/G20/G24 — all exact-pin-mechanism-specific); cumulative active count is now **34 across 8 categories**. G23 reframed as primary within-family coherence check (was derivative of G20). G11 marked REVISIT (NuGet built-in NU5016; dead-letter under minimum-range contract). Defense-in-depth principle preserved: critical invariants still checked at multiple layers (e.g., within-family version coherence checked at orchestration layer via Cake atomic `PackageTask` + post-pack layer via G23). Failure mode catalog updated — 3 exact-pin-specific failure modes removed as no-longer-possible under S1. New canonical doc: [`docs/knowledge-base/release-guardrails.md`](knowledge-base/release-guardrails.md). Used as the single map for "which guardrail lives where + when does it land."

## Cross-Reference

- **Detailed phase docs**: [phases/](phases/)
- **Technical deep-dives**: [knowledge-base/](knowledge-base/)
- **Design rationale & research**: [research/](research/)
- **How-to recipes**: [playbook/](playbook/)
- **Deep general references**: [reference/](reference/)
