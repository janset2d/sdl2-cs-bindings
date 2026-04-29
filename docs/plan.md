# Project Plan — Janset.SDL2 / Janset.SDL3

> **This is the canonical status document.** When code and docs disagree, verify against the code. When phases and this file disagree, this file wins.

**Last updated**: 2026-04-29 (post-PD-5 routing closure: `ResolveVersions` now routes workflow dispatch manifest/explicit modes plus family/train tags through the shared `versions.json` artifact; `publish-staging` tag-push gate lifted for internal-feed release tags; `publish-public` remains PD-7)
**Maintainer**: Deniz Irgin (@denizirgin)

## Mission

Provide the .NET ecosystem with production-quality, modular SDL2 and SDL3 bindings that include **cross-platform native libraries built from source** — something no other project offers.

## Current Phase

**Phase 2: CI/CD & Packaging** (IN PROGRESS — resumed after ~10 month hiatus; ADR-003 rewrite landed, PA-2 behavioral validation closed 2026-04-26, PD-5 RemoteInternal artifact source + PublishStaging real impl landed 2026-04-26, trigger-aware `ResolveVersions` routing + tag-push staging gate-lift landed 2026-04-29; current work is the Phase 2b public path tail: PD-7 and first prerelease publication to nuget.org)

Phase 2 is now divided into two stages:

- **Phase 2a — Hybrid Packaging Foundation Spike** (LANDED for the Phase 2a proof slice on 3 RIDs): Hybrid static + dynamic core packaging model proven end-to-end for `sdl2-core` + `sdl2-image` on `win-x64` / `linux-x64` / `osx-x64`. Windows host extended to 5 concrete families (`sdl2-core` / `-image` / `-mixer` / `-ttf` / `-gfx`). Package-consumer smoke test spine, Cake strategy awareness, and local feed validation operational.
- **Phase 2b — Full Hybrid Pipeline + Orchestration Rewrite** (ACTIVE): Canonical doc sweep (landed) → Cake refactor per ADR-003 (IPackageVersionProvider + per-stage request records + NativeSmoke extract + G58 + --explicit-version) (landed) → CI/CD workflow rewrite (`release.yml` with dynamic matrix + consumer smoke re-entry) (landed) → PA-2 behavioral validation on remaining 4 RIDs (closed 2026-04-26 via run 24938451364) → Remote artifact source profile + PublishStaging real impl (closed 2026-04-26 via run 24962876812) → trigger-aware version routing + internal-feed tag-push staging gate-lift (landed 2026-04-29) → public prerelease (PD-7 Trusted Publishing OIDC + #63 first nuget.org publish).

See [phases/README.md](phases/README.md) for the full phase breakdown and [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) for the active execution ledger.

## Strategic Decisions — April 2026

These decisions were made during the packaging strategy research cycle (April 2026) and are now locked. Supporting research is in [research/](research/).

| Decision | Detail | Rationale |
| --- | --- | --- |
| **Hybrid Static + Dynamic Core** | Transitive deps statically baked into satellite DLLs; SDL2 core stays dynamic; no separate transitive DLLs shipped | Industry standard (SkiaSharp, Magick.NET, ppy/SDL3-CS, LibGit2Sharp — 7/7 surveyed projects use this pattern). Eliminates the DLL collision class (#75). |
| **Custom vcpkg overlay triplets** | Per-RID hybrid triplets: default `VCPKG_LIBRARY_LINKAGE=static`, per-port override for SDL family to `dynamic` | Keeps all version management in vcpkg.json. No VENDORED builds, no wrapper DLLs. |
| **LGPL-free codec stack** | Drop mpg123, libxmp, fluidsynth from SDL2_mixer. Use bundled permissive alternatives (minimp3, drflac, stb_vorbis, libmodplug, Timidity/Native MIDI). | Eliminates all LGPL exposure. Mixer.Extras.Native package concept is dead. 100% permissive stack across all satellites. |
| **Pure Dynamic rejected** | The ~26-package Common.\* topology has zero precedent in the .NET ecosystem | High maintenance, NuGet graph complexity, no collision safety on Windows. |
| **Execution model: two feed-prep sources (ADR-001 2026-04-18)** | All consumer-facing validation paths use packages. Only the feed preparation varies: `--source=local` (repo pack produces the feed) or `--source=remote` (feed populated from internal/public feed download). **Supersedes the pre-ADR-001 three-mode framing** (Source Mode / Package Validation Mode / Release Mode). ProjectReference-based Source Mode retired. See [ADR-001](decisions/2026-04-18-versioning-d3seg.md) and [research/execution-model-strategy-2026-04-13.md](research/execution-model-strategy-2026-04-13.md) (amended by ADR-001). | Single consumer contract → smoke regressions reflect external-consumer regressions 1:1. No parallel tracks, no "worked in source mode, broke in package mode" class of bugs. |
| **Cake build host: strategy-driven evolution** | Three-interface split: IPackagingStrategy, INativeAcquisitionStrategy, IDependencyPolicyValidator. IPayloadLayoutPolicy deferred. **State as of 2026-04-21:** the strategy seam is partially landed as-designed, not a dispatcher. `IPackagingStrategy` = string-compare helper (`IsCoreLibrary`) only. `IDependencyPolicyValidator` has one real implementation (`HybridStaticValidator` consumes scanner output as a guardrail — real business logic) and one explicit pass-through (`PureDynamicValidator` — legacy-compat, returns Pass unconditionally by design per [cake-strategy-implementation-brief-2026-04-14.md](research/cake-strategy-implementation-brief-2026-04-14.md)). `INativeAcquisitionStrategy` was designed in the brief (VcpkgBuild / Overrides / CiArtifact) but has **not been implemented**; ADR-001's Artifact Source Profile abstraction (`Local` / `RemoteInternal` / `ReleasePublic`) now covers that problem space from the feed-preparation side. `IPayloadLayoutPolicy` was deferred in the brief until PackageTask landed; PackageTask has landed, the policy extraction has not — Packaging module hard-codes layout today. The Packaging module does not consume `IPackagingStrategy` at all; pack output shape is identical for hybrid and pure-dynamic RIDs. See [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) "Strategy & Tool Landing State" and [knowledge-base/cake-build-architecture.md](knowledge-base/cake-build-architecture.md) "Scanner Repurposing + Strategy-Aware Guardrails" for the preserved brief-vs-code model. | Keeps stable spine (scanners, closure, manifests), adds policy variation. Existing tools (dumpbin/ldd/otool) were not replaced by ADR-003; they remain stable producers whose `BinaryClosure` output now feeds both deployment planning and hybrid-static validation (`BinaryClosureWalker` → `HybridStaticValidator` second-consumer pattern). ADR-003 only classifies that behavior as Harvest-stage validation; it does not change the underlying strategy-pattern move. |
| **Triplet = strategy** | No `--strategy` CLI flag. Triplet name encodes the strategy. Manifest `runtimes[].strategy` field is the formal mapping, validated against triplet by PreFlightCheck. | Single authority, no two-headed configuration. |
| **Config merge to single manifest.json** | `runtimes.json` and `system_artefacts.json` merge into `manifest.json` (schema v2). Single source of truth for all build configuration. | Eliminates cross-file drift, atomic updates, CI reads one file. |
| **Validator uses vcpkg metadata** | HybridStaticValidator consumes BinaryClosureWalker output. No manually maintained expected-deps lists. | Transitive dep info changes per version; vcpkg metadata + runtime scan = ground truth. |
| **TUnit for build host tests** | TUnit 1.33.0, Microsoft.Testing.Platform runner, NSubstitute for mocking. Test-first approach: characterization tests before refactoring. | Fills the zero-test-coverage gap before any refactoring. See [research/tunit-testing-framework-2026-04-14.md](research/tunit-testing-framework-2026-04-14.md). |
| **Test naming convention** | `<MethodName>_Should_<Verb>_<When/If/Given>` with underscores between words, not in method name | Consistent, readable, project-wide standard. |
| **Remove external/sdl2-cs dependency** | The flibitijibibo/SDL2-CS git submodule will be removed. Current bindings are transitional — not trusted for production testing or long-term use. | SDL2-CS is unmaintained import-style bindings. Phase 4 CppAst generator replaces them entirely. |
| **C++ native smoke test project** | Cross-platform CMake/vcpkg C++ project for directly testing hybrid-built native libraries without P/Invoke layer. IDE-debuggable (Rider/VS/CLion). | Needed for format coverage testing (MP3/FLAC/MOD/MIDI), hybrid bake validation, and diagnosing native vs. P/Invoke issues. Research needed on best IDE integration approach. |
| **Package family as release unit** | Managed + .Native always share the same version (family version) and release together. Families version independently from each other. | Eliminates version matrix between managed and native. SkiaSharp/Magick.NET/Avalonia pattern. See [knowledge-base/release-lifecycle-direction.md](knowledge-base/release-lifecycle-direction.md). |
| **Tag-derived family versioning (D-3seg, ADR-001 2026-04-18)** | Family version = `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`. UpstreamMajor.UpstreamMinor anchored to `manifest.json library_manifests[].vcpkg_version` (G54 enforces). FamilyPatch is the repo's own release-iteration counter within a given Major.Minor line. Git tags with family-specific prefixes drive MinVer: `sdl2-core-2.32.0`, `sdl2-image-2.8.0`, `sdl2-mixer-2.8.0`, `sdl2-ttf-2.24.0`, `sdl2-gfx-1.0.0` (+ `sdl2-net-2.2.0` when Phase 3 lands). No build segment. No manual version edits in project files. Family identifier convention: `sdl<major>-<role>`. See [ADR-001](decisions/2026-04-18-versioning-d3seg.md) and [knowledge-base/release-lifecycle-direction.md §3](knowledge-base/release-lifecycle-direction.md). | Truth in labeling (consumer reads `Janset.SDL2.Core 2.32.0` and knows SDL2 2.32 minor line without README). vcpkg patch/port_version bumps roll into FamilyPatch on the maintainer's release cadence — not mandatory on every upstream touch. MinVer stays 3-part SemVer native. Exact upstream patch preserved in `janset-native-metadata.json` (G55) + README mapping table (G57). |
| **Hybrid release governance** | Targeted release per-family by default. Forced full-train release on cross-cutting changes (vcpkg baseline, triplet/strategy, shared toolchain, validation guardrails). | Fast iteration for isolated changes, coherence guarantee for infrastructure changes. |
| **Dependency contracts: minimum range everywhere (SkiaSharp pattern, revised S1 2026-04-17)** | Within-family AND cross-family: minimum version constraint (`>=`). Drift protection is orchestration-time (Cake `PackageTask` packs both family members at identical `--family-version` in one invocation; post-pack validator G23 asserts the emitted `<version>` elements match byte-for-byte). Previous design (exact pin within family via Mechanism 3) was proven mechanically but retired because it depended on MSBuild global-property propagation through NuGet's pack-time sub-eval, which `NuGet.Build.Tasks.Pack.targets` replaces rather than extends — unchanged for 8+ years, no upstream fix in .NET 10. See [knowledge-base/release-lifecycle-direction.md §4 Drift Protection Model](knowledge-base/release-lifecycle-direction.md) and [phases/phase-2-adaptation-plan.md "S1 Adoption Record"](phases/phase-2-adaptation-plan.md). | Industry-standard SkiaSharp / Avalonia / OpenTelemetry convention. Orchestration-time drift protection is sufficient because Cake owns both pack invocations per family; consumer-side exact pin was belt-and-suspenders against a scenario prevented by construction. |
| **CI matrix: 7 RID jobs, not library×RID** | One job per RID. vcpkg installs all libraries per-triplet, Cake harvests per-library within the job. Matrix generated dynamically from manifest.json. | vcpkg manifest mode is all-or-nothing. Dynamic matrix eliminates YAML↔manifest drift. |
| **Three-stage release promotion** | Local folder feed → internal feed (GitHub Packages) → public NuGet.org. Each stage is a deliberate, gated step. | Prevents accidental public releases. Matches ci-cd-packaging-and-release-plan.md design. |
| **Cake as single orchestration surface** | Change detection (dotnet-affected), versioning (NuGet.Versioning), harvesting, packaging, validation — all through Cake. CI workflows are execution triggers, not logic containers. | Centralized policy authority. CI YAML stays thin. |
| **Package-first consumer contract (ADR-001 2026-04-18)** | **All consumer-facing validation paths use packages; local vs remote changes only how the local feed is prepared.** Smoke, examples, sandbox, and future sample csprojs consume via `PackageReference` against a local folder feed. `buildTransitive/Janset.SDL2.Native.Common.targets` handles per-platform payload extraction (Windows DLL copy, Unix tar extract, .NETFramework AnyCPU fallback). **Supersedes** the pre-ADR-001 "source-graph vs shipping-graph separate mechanisms" design: Source Mode's `$(JansetSdl2SourceMode)` opt-in + `Directory.Build.targets` content-injection is retired. Binding-debug fast-loop is no longer mainline; if required, handled via separate opt-in throwaway harness outside the smoke system. | Single consumer model → smoke regressions reflect external-consumer regressions 1:1. Eliminates MSBuild `ProjectReference` transitive-native-asset edge cases. Symlink-preservation research from the retired Source Mode survives as reference for future remote-feed tar-extract caching. See [ADR-001 §2.6](decisions/2026-04-18-versioning-d3seg.md) and [`research/source-mode-native-visibility-2026-04-15.md`](research/source-mode-native-visibility-2026-04-15.md) (DEPRECATED — mechanism retired, evidence kept). |
| **Artifact Source Profile abstraction (ADR-001 2026-04-18)** | `dotnet cake --target=SetupLocalDev --source=local\|remote`. `--source=local` runs repo pack → populates `artifacts/packages` → writes `build/msbuild/Janset.Local.props` (gitignored, consumer override). `--source=remote` (landed 2026-04-26 PD-5 closure) fetches prebuilt nupkgs from the GitHub Packages internal feed → populates local cache → writes same override file. Consumer-side contract is identical either way (PackageReference from local folder feed). Cake abstraction: `IArtifactSourceResolver` + `ArtifactProfile { Local, RemoteInternal, ReleasePublic }`. Local + RemoteInternal resolvers landed; `ReleasePublic` stays stubbed pending Phase 2b PD-7 (nuget.org promotion). | Single orchestration surface principle (Cake-owned) with abstraction point at feed-prep, not consumer behaviour. CI and local dev converge on one mental model. Stream D-ci implementation plugs into existing resolver contract. See [ADR-001 §2.7–§2.8](decisions/2026-04-18-versioning-d3seg.md). |
| **DDD layering for build host (ADR-002 2026-04-19)** | Four-layer architecture: `Tasks/` (Cake-native presentation) + `Application/<Module>/` (use-case orchestrators: TaskRunners, Resolvers, SmokeRunner) + `Domain/<Module>/` (models, value objects, domain services, result types, `IPathService`) + `Infrastructure/<Module>/` (external-system adapters: filesystem, process, Cake Tool wrappers). Layer discipline enforced by `build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs` (three invariants). Interface discipline formalized: keep an interface only if (1) multiple implementations exist or (2) it formalizes an independent axis of change; test mocks are supporting evidence not standalone justification. | Shape readable by layer path. Cross-module infrastructure reuse (PathService, JSON helpers) is the default. Domain unit-testable with no Cake/filesystem/process. Phase 2b additions land in named slots. See [ADR-002](decisions/2026-04-19-ddd-layering-build-host.md). |
| **Release lifecycle orchestration (ADR-003 2026-04-20 draft)** | Three-layer mental model: **RID** (CI matrix axis) → **Family** (deploy unit) → **Version** (explicit input). Version source is the primary abstraction axis, not scenario. Three `IPackageVersionProvider` implementations: `ManifestVersionProvider` (manifest upstream + suffix), `GitTagVersionProvider` (family tag or meta-tag, single/multi-family modes), `ExplicitVersionProvider` (operator-supplied mapping). Version resolution is a **pre-stage concern** — exactly once per invocation; immutable downstream. PreFlight is **version-aware by contract**. **Stage-owned validation** (PreFlight / Harvest / NativeSmoke / Pack / ConsumerSmoke / Publish) — monolithic "PostFlight" validator suite retired; every guardrail belongs to exactly one stage. **Consumer smoke matrix re-entry**: same RID matrix re-enters after Pack, per-RID P/Invoke / dyld / arm64 paths validated. **Option A resolver-centric composition**: `SetupLocalDev` stays a thin Cake task over `IArtifactSourceResolver`; resolver internals may compose version providers + pack loops + stage-runner calls privately. **`--family-version` retires** (PD-13) in favor of `--explicit-version key=value,...`. Direction-selected closures: PD-7 (meta-tag + manifest-driven topological ordering), PD-8 (operator runs same pipeline via ExplicitVersionProvider), PD-13 (flag retirement). New guardrail: G58 (cross-family dep resolvability, Pack stage). **Status:** pseudocode/mental model; implementation lands in post-sweep Cake refactor + CI/CD rewrite passes. | Five scenarios (local / CI witness / targeted release / full-train / manual escape) reduce to three providers + one pipeline + one release workflow. CI stays thin (trigger semantics + job graph); build host stays thick (providers + stage policy + guardrails). Per-RID consumer paths get real coverage via matrix re-entry. See [ADR-003](decisions/2026-04-20-release-lifecycle-orchestration.md). |

## Phase Roll-Up

| Phase | Name | Status | Summary |
| --- | --- | --- | --- |
| 1 | SDL2 Core Bindings + Harvesting | **DONE** | C# bindings for 5 libraries, Cake Frosting build system, native binary harvesting pipeline, cross-platform CI workflows |
| 2a | Hybrid Packaging Foundation Spike | **DONE** | Proved hybrid model for the Phase 2a proof slice: `sdl2-core` + `sdl2-image` on `win-x64` / `linux-x64` / `osx-x64`, with package-consumer smoke spine and local-feed validation landed |
| 2b | Full Hybrid Pipeline + Orchestration Rewrite | **ACTIVE** | Slice A→C landed on master 2026-04-22 (`bfc6713`): Cake refactor complete, three `IPackageVersionProvider` impls, per-stage request records, stage-owned validation, flat task graph, G58 Pack-stage gate, D-3seg versioning, package-first consumer contract, three-platform smoke witness green, 426/426 tests. Slice E follow-up pass closed 2026-04-25 on `d190b5b` (CI absorption + GHCR image + lock-file discipline + PublishTask stubs + three-platform witness). PA-2 behavioral validation closed 2026-04-26 via `release.yml` run 24938451364 on `8ec85c5`. PD-5 (`RemoteArtifactSourceResolver` + `PublishStagingTask` real impl) fully closed 2026-04-29: CI run 24962876812 published the first internal-feed wave to GitHub Packages (5 family × 2 nupkg = 10 nupkgs at `<UpstreamMajor>.<UpstreamMinor>.0-ci.24962876812.1`); remote witnesses green on Windows `win-x64` 35/35, WSL Linux `linux-x64` 24/24, and macOS Intel `osx-x64` 24/24. Trigger-aware `ResolveVersions` routing and tag-push staging gate-lift landed 2026-04-29. Build-host suite at 500/500. Remaining tail: publish-public path (PD-7 / Trusted Publishing OIDC) + first prerelease publication to nuget.org (#63). |
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
| SDL2_net | — | Not yet added (Phase 3 — #58) | — | — |

### Native Library Build (vcpkg)

| Library | In vcpkg.json | Features Configured | Harvest Tested |
| --- | :---: | --- | :---: |
| SDL2 | Yes | vulkan, alsa, dbus, ibus, samplerate, wayland, x11 | Yes |
| SDL2_image | Yes | avif, libjpeg-turbo, libwebp, tiff | Yes |
| SDL2_mixer | Yes | libmodplug, opusfile, timidity, wavpack | Yes (win-x64 validation; matrix pending) |
| SDL2_ttf | Yes | harfbuzz | Yes (win-x64 validation; matrix pending) |
| SDL2_gfx | Yes | No features (simple library) | Yes (win-x64 validation; matrix pending) |
| SDL2_net | Yes | No features | Not yet (placeholder removed from manifest.json — re-add with full csproj + .Native structure in Phase 3, #58) |

### CI/CD

| Component | Status | Notes |
| --- | --- | --- |
| `release.yml` | Working | Live 10-job pipeline covering build-host test/publish, version resolution, PreFlight, dynamic matrix generation, harvest/native-smoke, consolidation, pack, consumer-smoke, and gated publish stages. Canonical job-by-job detail lives in `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`. |
| `build-linux-container.yml` | Working | Builds and publishes the multi-arch GHCR Linux builder image (`focal-latest` plus immutable dated tags) consumed by the Linux runtime rows in `build/manifest.json`. |
| Publish path | `publish-staging` live for explicit dispatch and release tags; `publish-public` still stubbed | `PublishStagingTask` real impl landed 2026-04-26 (commit `fdabcae`); pushes to GitHub Packages via `NuGet.Protocol.PackageUpdateResource`. Dispatch remains gated behind `workflow_dispatch.inputs.publish-staging=true`; tag pushes (`sdl2-*`, future `sdl3-*`, `train-*`) now publish to staging after trigger-aware `ResolveVersions` emits tag-derived `versions.json`. `PublishPublicTask` stays stubbed pending Phase 2b PD-7 (nuget.org / Trusted Publishing OIDC). First-wave witness: `release.yml` run 24962876812 (5 families × 2 nupkgs to `https://nuget.pkg.github.com/janset2d/index.json`). |
| PA-2 witness | Resolved 2026-04-26 | Closed via `workflow_dispatch` `release.yml` run 24938451364 on master `8ec85c5` (`mode=manifest-derived`, suffix `ci.24938451364.1`). All 7 RIDs Pack ✓ + ConsumerSmoke ✓; per-TFM TUnit zero-failure on each PA-2 row (win-arm64 35/35, win-x86 35/35, linux-arm64 24/24, osx-arm64 24/24, with net462 auto-skipped on Linux + macOS per `PackageConsumerSmokeRunner.ShouldSkipTfm`). |

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
| SDL2 | 2.32.10 | 2.32.10 | 2.32.10 | Current |
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
- [x] Update manifest.json runtime rows with the initial hybrid triplets and add `validation_mode`
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
> **Next:** Phase 2a strategy wiring is closed; this historical handoff is retained only for #85 archaeology. Current next work is the Phase 2b public release tail: PD-7 `PublishPublicTask` / nuget.org Trusted Publishing, first prerelease publication (#63), and PD-8 release-recovery runbook/operator escape hatch. HarvestPipeline extraction (#87) remains intentionally deferred.

**Release Lifecycle Adaptation** (see [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md)):

- [x] Stream A-safe: manifest.json `package_families` schema + NuGet.Versioning in Cake (manifest schema v2.1 + build host package reference landed)
- [x] Stream A0: Exact pin spike — mechanism proven 2026-04-16 (`PrivateAssets="all"` + explicit `PackageReference` with bracket notation), then **RETIRED 2026-04-17 (S1 adoption)** because production orchestration hit upstream NuGet limitations. Historical rationale preserved in the [archived adaptation plan](_archive/phase-2-adaptation-plan-2026-04-15.md) "S1 Adoption Record" section and [research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md](research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (SUPERSEDED).
- [x] Stream A-risky: MinVer 7.0.0 + exact-pin csproj rollout (Mechanism 3) + family identifier rename to canonical `sdl<major>-<role>` + MSBuild guard target + PreFlight `CsprojPackContractValidator` (G1-G8 + G17-G18) — landed 2026-04-16. PD-1 resolved, PD-9 opened. 256/256 build-host tests green at A-risky closure. **Partially reverted 2026-04-17 (S1 adoption):** Mechanism 3 csproj shape, sentinel PropertyGroup, `_GuardAgainstShippingRestoreSentinel` target, and G1/G2/G3/G5/G8 validator invariants removed in Phase 3. MinVer + family rename + G4/G6/G7/G17/G18 retained. See [archived adaptation plan](_archive/phase-2-adaptation-plan-2026-04-15.md) "Stream A-risky Historical Record" section.
- [x] Stream B: Strategy wiring (#85, closed) — DI + HarvestTask validator + PreFlight strategy coherence; HarvestPipeline extraction **split to follow-up issue #87**
- [x] **PA-1: Matrix strategy review** — closed 2026-04-18: keep Stream C RID-only; `strategy` stays runtime-row metadata, not a top-level matrix axis. See [research/ci-matrix-strategy-review-2026-04-17.md](research/ci-matrix-strategy-review-2026-04-17.md).
- [x] **PA-2: Hybrid overlay triplet expansion** — landed 2026-04-18: added overlay triplets for the remaining 4 RIDs, moved all 7 `manifest.json` runtime rows to `hybrid-static`, and wired CI's shared vcpkg setup plus the manual orchestrator workflow to use hybrid triplets. End-to-end pack/consumer validation for the four newly-covered rows closed 2026-04-26 via `release.yml` run 24938451364 on master `8ec85c5` (see ADR-003 Implementation Sequence step 4 below).
- [ ] Stream C: PreFlightCheck as CI gate, dynamic matrix generation from manifest, CI workflow migration
- [x] Stream D-local: PackageTask with family version (#54, #83 closed), package-consumer smoke test, local folder feed. D-local landed for the Phase 2a proof slice on 2026-04-16 under the pre-S1 guardrail set (G20–G27 post-pack exact-pin assertions). **S1 adoption (2026-04-17) retired G20 and G24; D-local post-pack guardrails narrow to G21 (minimum-range dependency shape), G22 (TFM consistency), G23 (within-family version match — now the primary coherence check), G25–G27 (symbols, repo, metadata), G46 (MSBuild payload guard), G47 (buildTransitive contract presence), G48 (per-RID native payload shape — DLLs on Windows, `$(PackageId).tar.gz` on Unix).** Phase 3 code changes (2026-04-17) collapsed the 4-step pack orchestration to 2-step, rewrote `PackageOutputValidator` onto the Result pattern with accumulating checks, added consumer-side `buildTransitive/Janset.SDL2.Native.Common.targets` for Unix `tar -xzf` extraction and .NETFramework AnyCPU DLL copy, and fixed cross-platform path-separator / filename-collision regressions exposed during 3-platform verification. **Verified 2026-04-17: PostFlight end-to-end green on all three original hybrid-static RIDs (`win-x64`, `linux-x64`, `osx-x64`) for the `sdl2-core` + `sdl2-image` proof slice; 273/273 build-host tests.** Scope note: PA-2 landed on 2026-04-18 and moved the four remaining runtime rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) onto hybrid overlay triplets, but those newly-covered rows are still not exercised end to end on the pack / consumer-smoke path. Broader consumer-smoke matrix expansion across all 7 RIDs stays in 2b.
- [ ] Stream D-ci: CI package-publish job + smoke gate + internal feed push
- [ ] Stream E: **2a scope = feasibility spike only** (full `DetectChangesTask` + CI filtering deferred to Phase 2b)
- [x] Stream F: Local-dev feed preparation + artifact source profiles — Cake two-source framework (`--source=local|remote`). `SetupLocalDev --source=local` landed for the host RID end to end (packages local feed + writes `build/msbuild/Janset.Local.props`). `SetupLocalDev --source=remote` landed 2026-04-26 (PD-5 closure, commits `bc7c677`, `fdabcae`, `549ad2f`) and was fully witnessed across all 3 maintainer hosts on 2026-04-29: pulls latest published nupkgs from GitHub Packages internal feed via `RemoteArtifactSourceResolver` + `INuGetFeedClient` / `NuGetProtocolFeedClient`; Windows `win-x64`, WSL Linux `linux-x64`, and macOS Intel `osx-x64` all passed `tests/scripts/smoke-witness.cs remote` against CI run 24962876812. `--source=release` (public NuGet.org) stays stubbed pending Phase 2b PD-7. Historical Source Mode mechanism is retired by ADR-001.

**ADR-003 Orchestration Rewrite — pass-1 closed 2026-04-22 (master `bfc6713`):**

ADR-003 locked the post-sweep direction (three `IPackageVersionProvider` impls, stage-owned validation, consumer smoke matrix re-entry, Option A resolver-centric `SetupLocalDev`, PD-7/PD-8/PD-13 direction-selected, G58 new). Pass-1 landed Slices A → C (core ADR-003 surface + three-platform smoke witness green) plus Slice E infrastructure (E1a-b cake-host artifact + E2 GHCR Dockerfile/workflow). Slice E remainder is a separate follow-up pass.

- [x] **Canonical documentation sweep** — landed across Slice A → C cadence; ADR-003 baseline reflected in `phase-2-adaptation-plan.md` rewrite, `release-lifecycle-direction.md` policy-only narrowing, `cake-build-architecture.md` + `release-guardrails.md` + `cross-platform-smoke-validation.md` + `unix-smoke-runbook.md` Cake-first updates, `Janset.Smoke.local.props → Janset.Local.props` reference sweep in C.12. Historical pre-sweep body of `phase-2-adaptation-plan.md` preserved in [_archive/phase-2-adaptation-plan-2026-04-15.md](_archive/phase-2-adaptation-plan-2026-04-15.md). Broader sweep tail (AGENTS.md / onboarding.md / knowledge-base surface drift cleanup + §11 Q17 resolution) carries into the Slice E follow-up pass.
- [x] **Cake refactor** — Slices A → C landed. `IPackageVersionProvider` + three impls (`ManifestVersionProvider`, `ExplicitVersionProvider`, `GitTagVersionProvider`) + per-stage request records (`HarvestRequest`, `ConsolidateHarvestRequest`, `PreflightRequest`, `PackRequest`, `PackageConsumerSmokeRequest`, `PublishRequest`, `NativeSmokeRequest`) + `NativeSmokeTask` extraction + flat task graph (all `[IsDependentOn]` removed) + `SetupLocalDevTaskRunner` (Option A resolver-centric composition) + `G58CrossFamilyDepResolvabilityValidator` Pack-stage gate (+ PreFlight mirror as defense-in-depth) + runner-strict `--explicit-version` (`PackageConsumerSmokeRunner` rejects empty `Versions`) + PD-13 `--family-version` retirement. Test suite 340 → 426 green.
- [x] **CI/CD workflow rewrite** — closed via Slice E follow-up pass (2026-04-25). Pass-1 landed `release.yml` skeleton + `build-cake-host` FDD pattern + GHCR builder image infrastructure. Follow-up pass closed: E1c platform-prereq composite + vcpkg-setup absorption (P3+P4), E1d lock-file discipline (P5: `<RestorePackagesWithLockFile>true</...>` everywhere committed; `<RestoreLockedMode>` strict-mode narrowed to build host only after CI surfaced cross-platform NU1004 + NU1009 patterns on SDK-implicit packages — see `phase-2-release-cycle-orchestration-implementation-plan.md` §2.2 P5 row), E3 retire `prepare-native-assets-*.yml` + `release-candidate-pipeline.yml` (P8.1), E4 PublishTask scaffolding stubs (P6: `PublishTaskRunner` + `PublishStagingTask` + `PublishPublicTask` throw `NotImplementedException` with Phase-2b pointer; `release.yml` jobs stay `if: false` gated until Phase 2b real implementation), E6 broader doc sweep + §11 Q17 (ADR-002 §2.3.1 delegate-hook amendment) + §11 Q18 (CMakePresets developer-experience refactor — drop `*-interactive` variants → `CMakeUserPresets.json.example` opt-in pattern), E7 three-platform witness (Windows + WSL Linux A-K green at master `d190b5b`; macOS deferred to CI per Deniz direction — no Mac hardware available locally). Two earlier post-P4 investigations also closed during the follow-up pass: `PackageConsumerSmoke` owns `win-x86` runtime bootstrap via Cake (`IDotNetRuntimeEnvironment` / `DotNetRuntimeEnvironment` installs x86 runtimes per executable TFM and injects `DOTNET_ROOT_X86` + `DOTNET_ROOT(x86)` into child `dotnet test` only); `vcpkg-setup` resolves mutable `container_image` tags such as `focal-latest` to immutable per-platform child manifest digests inline for the cache key while the actual job still executes on the convenience tag.
- [x] **PA-2 behavioral validation via new pipeline** — closed 2026-04-26 via `release.yml` run 24938451364 on master `8ec85c5`. `workflow_dispatch` `mode=manifest-derived` (default) cleared the full Cake target chain (PreFlight → Harvest + NativeSmoke → ConsolidateHarvest → Pack → ConsumerSmoke) on all 7 RIDs; the four PA-2 rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) all cleared every stage's owned validators with zero-failure per-TFM TUnit. Suffix shape was `ci.<run-id>.<attempt>` (the prior `pa2.<run-id>` wording was fictional — `release.yml` always emits `ci.<run-id>.<attempt>`; `ExplicitVersionProvider` is the operator's path to any custom suffix shape).

**Deferred to Phase 2b / Q3:**

- [ ] Add a Windows local prerequisites guide (VS tooling ecosystem + dumpbin/vswhere troubleshooting)
- [ ] Clean up native binaries from git history (#56)
- [ ] Correct and validate local development playbook (#57)
- [ ] Create custom Docker build image for Linux x64 (#79)
- [x] **CMakePresets developer-experience refactor** — closed 2026-04-25 in Slice E follow-up pass P8.4. `tests/smoke-tests/native-smoke/CMakePresets.json` reduced 21 → 14 buildPresets (Release + Debug per RID; `*-interactive` variants moved to `CMakeUserPresets.json` opt-in pattern); `CMakeUserPresets.json.example` template ships interactive entries for every RID; `README.md` "Two Modes" + "Interactive Mode" sections rewritten around the UserPresets opt-in flow; `.gitignore` excludes `tests/smoke-tests/native-smoke/CMakeUserPresets.json`. See [phase-2-release-cycle-orchestration-implementation-plan.md §11 Q18 closure](phases/phase-2-release-cycle-orchestration-implementation-plan.md) for the resolution trail.

### Q3 2026 — Phase 2b: Full Hybrid Pipeline + ADR-003 Finish

- [x] Execute PA-2 behavioral validation on the 4 newly-hybridized rows via the live `release.yml` pipeline (closed 2026-04-26 via run 24938451364 on master `8ec85c5`)
- [ ] Generalize Cake `Pack` stage to all 6 satellites × 7 RIDs under the new pipeline
- [ ] Harden evidence collection and operator playbooks around the existing 7-RID consumer-smoke matrix re-entry
- [x] Implement `RemoteArtifactSourceResolver` concrete (PD-5): internal feed URL convention (`https://nuget.pkg.github.com/janset2d/index.json`), auth pattern (`GH_TOKEN → GITHUB_TOKEN` env chain, Classic PAT with `read:packages`), cache strategy (`artifacts/packages/` with wipe-on-prepare). `SetupLocalDev --source=remote` operational and witnessed on Windows `win-x64`, WSL Linux `linux-x64`, and macOS Intel `osx-x64` against CI run 24962876812.
- [ ] Implement PD-7 public promotion + full-train completion: `PublishPublicTask` real impl, nuget.org Trusted Publishing OIDC, first prerelease publication (#63), meta-tag train witness with manifest-driven topological ordering, and partial-train recovery guidance.
- [ ] Implement PD-8 manual escape hatch: operator-driven pack/push via `ExplicitVersionProvider`; `playbook/release-recovery.md` drafted; Cake `Pack-Family` / `Smoke-Family` / `Push-Family` helpers
- [ ] Add Linux version scripts for symbol visibility (`.map` files per satellite) — lower priority
- [ ] Add SDL2_net bindings + native project (#58) — requires: managed csproj, .Native csproj, overlay port (if needed), manifest.json re-entry (package_families + library_manifests), vcpkg feature config, harvest validation
- [ ] Validate SDL2_mixer LGPL-free build across all RIDs
- [ ] Create sample projects (#60 — spec absorbed from retired phase-3-sdl2-complete.md §3.3)
- [ ] Publish first pre-release to NuGet.org (#63)
- [ ] Evaluate NoDependencies native package variant for minimal Linux environments (#80)
- [ ] Research and POC linux-musl-x64/arm64 native asset coverage (#82)
- [ ] Resolve PD-14 (Linux end-user MIDI packaging strategy) before first public Mixer release
- [ ] Resolve PD-15 (SDL2_gfx Unix symbol-export regression guard) during next CI hardening pass

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

Primary docs: [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) (active execution ledger), [decisions/2026-04-20-release-lifecycle-orchestration.md](decisions/2026-04-20-release-lifecycle-orchestration.md) (ADR-003 orchestration architecture), [research/packaging-strategy-hybrid-static-2026-04-13.md](research/packaging-strategy-hybrid-static-2026-04-13.md), [research/license-inventory-2026-04-13.md](research/license-inventory-2026-04-13.md), [research/execution-model-strategy-2026-04-13.md](research/execution-model-strategy-2026-04-13.md)

| Issue | Labels |
| --- | --- |
| `#83 Hybrid Packaging Foundation Spike — win-x64, SDL2.Core + SDL2.Image` | `type:enhancement`, `area:native`, `area:packaging`, `area:build-system` |
| `#87 Extract HarvestPipeline service from HarvestTask` | `type:cleanup`, `area:build-system`, `area:testing` |

### Phase 2b - Full Hybrid Pipeline

Primary docs: [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md), [decisions/2026-04-20-release-lifecycle-orchestration.md](decisions/2026-04-20-release-lifecycle-orchestration.md), [knowledge-base/ci-cd-packaging-and-release-plan.md](knowledge-base/ci-cd-packaging-and-release-plan.md), [knowledge-base/cake-build-architecture.md](knowledge-base/cake-build-architecture.md), [playbook/local-development.md](playbook/local-development.md)

| Issue | Labels |
| --- | --- |
| `#54 Implement PackageTask for native and managed package creation` | `type:enhancement`, `area:build-system`, `area:packaging` |
| `#55 Implement distributed harvest staging for the release-candidate pipeline` | `type:enhancement`, `area:build-system`, `area:ci-cd` |
| `#56 Clean native binaries from git and harden ignore rules` | `type:cleanup`, `area:docs`, `area:native` |
| `#57 Validate and correct the local development playbook` | `type:documentation`, `area:build-system`, `area:docs` |
| `#79 Create custom Docker build image for Linux x64` | `type:enhancement`, `area:ci-cd`, `platform:linux` |
| `#81 Add drift-prevention guardrail for harvest library lists` | `type:hardening`, `area:ci-cd`, `area:build-system` |

### Phase 3 - SDL2 Complete

Primary docs: this doc (`plan.md` Q3/Q4 2026 roadmap sections), [playbook/adding-new-library.md](playbook/adding-new-library.md), [playbook/local-development.md](playbook/local-development.md). Note: `phases/phase-3-sdl2-complete.md` retired 2026-04-21 (retire-to-stub, redirects here); sample projects spec absorbed into [issue #60](https://github.com/janset2d/sdl2-cs-bindings/issues/60).

| Issue | Labels |
| --- | --- |
| `#58 Add SDL2_net binding and native package skeleton` (manifest placeholder removed 2026-04-22; re-add with full structure when implementing) | `type:enhancement`, `area:bindings`, `area:native` |
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

## Known Issues And Status Notes

1. **Native binaries in git history (working-tree cleaned 2026-04-15)**: Stale payloads under `src/native/<Lib>/runtimes/` have been removed from tracking (74 files across 5 `.Native` packages) and `.gitignore` rule `src/native/*/runtimes/` added. **Pending:** history rewrite via `git-filter-repo` to drop ~30 MB of those stale payloads from past commits, followed by `git push --force-with-lease` on `master` and `nugetizer`. Local clones on WSL/Mac will need fresh `git clone` (or `git fetch origin && git reset --hard origin/<branch>`). 5 issues (#52, #53, #76, #77, #78) reference 4 repo commit SHAs that will be rewritten — post-rewrite remap via `.git/filter-repo/commit-map` is optional cleanup.
2. **Release publication path partially live (2026-04-29)**: `PublishStagingTask` real impl landed (commit `fdabcae`); pushes managed + native nupkg pairs to GitHub Packages via `NuGet.Protocol.PackageUpdateResource`. `release.yml` `publish-staging` is now live for explicit dispatch (`publish-staging=true`) and release tags after trigger-aware `ResolveVersions` routing emits the shared `versions.json`; first internal-feed witness via run 24962876812. `PublishPublicTask` stays stubbed (throws `CakeException` on invocation) pending Phase 2b PD-7 (nuget.org promotion via Trusted Publishing OIDC).
3. **HarvestPipeline extraction still pending after strategy wiring**: `IPackagingStrategy`, `IDependencyPolicyValidator`, and `StrategyResolver` are now wired into Program.cs DI and invoked from HarvestTask/PreFlightCheck. PreFlight follow-up cleanup also landed, and the build-host suite is 340 tests green (measured 2026-04-20). Remaining orchestration cleanup is tracked in #87.
4. **Local dev playbook needs correction**: A playbook exists, but parts of it were inaccurate and not yet validated end-to-end.
5. **Remote artifact-source selector landed and fully witnessed (2026-04-29, PD-5 closure)**: `SetupLocalDev --source=remote` operational via `RemoteArtifactSourceResolver` + `INuGetFeedClient` / `NuGetProtocolFeedClient`. Reads `GH_TOKEN`/`GITHUB_TOKEN` env (Classic PAT with `read:packages` scope), discovers latest published version per concrete family on `https://nuget.pkg.github.com/janset2d/index.json`, downloads managed + native nupkg pairs into `artifacts/packages/` (wipe-on-prepare), writes `Janset.Local.props` + `versions.json` for parity with the Local profile. End-to-end remote witnesses against CI run 24962876812 are green on all 3 maintainer platforms: Windows `win-x64` 3/3 PASS, ConsumerSmoke 35/35; WSL Linux `linux-x64` 3/3 PASS, ConsumerSmoke 24/24 (`net462` skipped); macOS Intel `osx-x64` 3/3 PASS, ConsumerSmoke 24/24 (`net462` skipped because Mono absent). `ReleasePublic` (`--source=release`) stays stubbed pending Phase 2b PD-7.
6. **Distributed CI output flow is not wired yet**: current harvest output is still local-first. The release pipeline will need a real staging-vs-consolidated path split so matrix jobs can upload per-RID artifacts before consolidation.
7. **Hybrid triplet expansion + behavioral coverage closed (2026-04-26)**: all 7 runtime rows map to hybrid overlay triplets in `build/manifest.json`. PA-2 behavioral validation on the four newly-covered rows (`win-x86`, `win-arm64`, `linux-arm64`, `osx-arm64`) closed via `release.yml` run 24938451364 on master `8ec85c5` — Pack ✓ + ConsumerSmoke ✓ on every PA-2 row's native runner; per-TFM TUnit zero-failure (35/35 on Windows ARM64 and x86 including net462; 24/24 on Linux and macOS arm64 with net462 auto-skipped per platform gate). Harvest + NativeSmoke green on every RID — the 29-test C harness built and executed under each PA-2 RID's native runtime. See PA-2 closure record in [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md#alignment-items) and [playbook/cross-platform-smoke-validation.md](playbook/cross-platform-smoke-validation.md#pa-2-per-triplet-witness-invocations).
8. **Symbol visibility analyzed, hardening deferred**: Symbol visibility analysis complete (`docs/research/symbol-visibility-analysis-2026-04-14.md`). zlib/libpng = 0 leaks on all platforms. FreeType/WebP have cosmetic leaks, accepted. Further hardening (version scripts, `-fvisibility=hidden`) deferred to Phase 2b.
9. **Windows local tooling guidance is not explicit enough**: contributors need a dedicated prerequisites guide for VS C++ tooling, Developer PowerShell usage, and dumpbin/vswhere troubleshooting.
10. **Build-host test topology drift**: test folders are not yet fully aligned to `_build` production boundaries (`Modules`/`Tasks`/`Tools`), which raises maintenance cost and weakens whitebox/blackbox separation.
11. **Release lifecycle direction locked, implementation mostly landed**: Package family model, D-3seg tag-derived versioning, hybrid release governance, dynamic CI matrix, trigger-aware version routing, internal-feed staging, and the package-first consumer contract are all locked in `docs/knowledge-base/release-lifecycle-direction.md`. Implementation plan split into Streams A-safe / A-risky, A0, B, C, D-local / D-ci, E (scope-reduced), F — see `docs/phases/phase-2-adaptation-plan.md`. ADR-001 (2026-04-18) retired the old Source Mode mechanism: Stream F covers feed preparation only. `SetupLocalDev --source=local` + `SetupLocalDev --source=remote` both landed and were witnessed on Windows, WSL Linux, and macOS Intel by 2026-04-29. `--source=release` (public NuGet.org) stays stubbed pending Phase 2b PD-7. PD-2 withdrawn under S1, PD-1 resolved, PD-6 closed as not applicable under the package-first contract, PD-5 resolved 2026-04-26, PD-13 closed — see the adaptation plan's alignment table for current open decisions.
12. **Public release promotion and full-train completion remain open (PD-7, opened 2026-04-16)**: The targeted-tag and train-tag version-resolution paths now exist, and internal-feed staging opens for release tags after smoke. The remaining PD-7 work is the public promotion tail: `PublishPublicTask` real implementation, nuget.org Trusted Publishing OIDC, first prerelease publication (#63), GitHub Release UX / release-notes aggregation, and an end-to-end train-tag witness with partial-train recovery guidance. The selected train mechanism is meta-tag + manifest-driven topological ordering; no separate `release-set.json` is planned. See PD-7 in [`docs/phases/phase-2-adaptation-plan.md`](phases/phase-2-adaptation-plan.md#alignment-items).
13. **Release recovery + manual escape hatch unresolved (PD-8, opened 2026-04-16)**: All happy-path release flow (individual + full-train) is GitHub-driven. The unhappy path (CI broken, partial-train failure, mid-train recovery, emergency hotfix) is unspecified. Manual escape hatch exists in principle (operator replicates CI step-for-step by hand using the documented `dotnet restore` + `dotnet pack --no-restore` + `dotnet nuget push` sequence) but lacks Cake helpers, API key provisioning policy, audit trail mechanism, and a `playbook/release-recovery.md` runbook. Research placeholder published at [`docs/research/release-recovery-and-manual-escape-hatch-2026-04-16.md`](research/release-recovery-and-manual-escape-hatch-2026-04-16.md) with two escape-hatch categories (individual + full-train), seven research questions, decision criteria, industry precedents to survey. Blocks Stream D-local Cake helper exposure + Stream D-ci publish pipeline (manual flow must mirror CI flow). See PD-8 in [`docs/phases/phase-2-adaptation-plan.md`](phases/phase-2-adaptation-plan.md#pending-decisions).
14. **Within-family exact-pin auto-derivation (PD-9) — CLOSED 2026-04-17 (not applicable post-S1)**: PD-9 opened 2026-04-16 tracked the frontier of making bracket-notation `PackageVersion` capture MinVer's `$(Version)` without explicit Cake orchestration. Investigation during D-local integration identified a structural limitation in NuGet's pack-time sub-evaluation: CLI globals don't propagate into the ProjectReference walk where `_GetProjectVersion` resolves, so the sentinel-fallback path triggers regardless of what Cake passes at the outer invocation. Our best-diagnosed mechanical explanation is the `<MSBuild Properties="BuildProjectReferences=false;">` invocation at `NuGet.Build.Tasks.Pack.targets:335`, which appears to replace the child eval's globals rather than extend them; the specific code path has shipped unchanged for 8+ years, is identical in .NET 10 SDK, and has no upstream fix in flight. **S1 adoption (2026-04-17) retired the exact-pin requirement itself**, so PD-9's frontier is no longer relevant to this project. Industry survey (LibGit2Sharp hardcodes literal, SkiaSharp uses minimum range, Avalonia minimum range, Magick.NET hardcodes, SDL3-CS bundles) retroactively confirmed the S1 path. See [`docs/phases/phase-2-adaptation-plan.md` "S1 Adoption Record"](phases/phase-2-adaptation-plan.md), PD-9 / PD-11 in the adaptation plan, and [`artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md`](../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md) (investigation evidence; specific mechanism treated as supporting evidence rather than sole definitive cause).
15. **Comprehensive guardrail roadmap published (2026-04-16; amended 2026-04-17)**: Originally 45 guardrails enumerated across 8 categories. **S1 adoption (2026-04-17) retired 9 guardrails** (G1/G2/G3/G5/G8/G9/G10/G20/G24 — all exact-pin-mechanism-specific); cumulative active count is now **34 across 8 categories**. G23 reframed as primary within-family coherence check (was derivative of G20). G11 marked REVISIT (NuGet built-in NU5016; dead-letter under minimum-range contract). Defense-in-depth principle preserved: critical invariants still checked at multiple layers (e.g., within-family version coherence checked at orchestration layer via Cake atomic `PackageTask` + post-pack layer via G23). Failure mode catalog updated — 3 exact-pin-specific failure modes removed as no-longer-possible under S1. New canonical doc: [`docs/knowledge-base/release-guardrails.md`](knowledge-base/release-guardrails.md). Used as the single map for "which guardrail lives where + when does it land."
16. **SDL2-CS submodule is transitional; two upstream wrapper defects pending generator-side retirement (logged 2026-04-18)**: `external/sdl2-cs` points at `flibitijibibo/SDL2-CS` and is retired long-term by the AST-driven binding generator planned for a later phase. Two confirmed-but-untracked upstream defects today: `src/SDL2_mixer.cs:148` declares `EntryPoint = "MIX_Linked_Version"` (wrong caps — native symbol is `Mix_Linked_Version`), and `src/SDL2_ttf.cs:77` declares `EntryPoint = "TTF_LinkedVersion"` (missing underscore — native symbol is `TTF_Linked_Version`). Both wrappers throw `EntryPointNotFoundException` when called. Repo-local impact contained: `PackageConsumer.Smoke/PackageSmokeTests.cs` asserts only the wrapper methods whose EntryPoint strings match native exports, and the native-smoke (C) harness exercises `Mix_Linked_Version` / `TTF_Linked_Version` directly. **Boundary rule**: do NOT patch the submodule worktree — fix the repo-local call site or wait for generator retirement. Canonical doc: [`docs/knowledge-base/cake-build-architecture.md` "SDL2-CS Submodule Boundary (Transitional)"](knowledge-base/cake-build-architecture.md). Optional future move: file the two defects upstream as a low-cost community PR before the AST generator retires the whole submodule; deferred until someone has cycles.
17. **Harvest license layout moved to per-RID + `_consolidated/` (2026-04-18; H1 closure)**: Prior harvest output wrote licenses flat at `licenses/{package}/{file}` — library-scoped, so sequential multi-RID runs on the same checkout overwrote earlier RIDs' license attribution (cross-platform feature deltas mean RID-specific transitive deps legitimately differ). Post-H1 layout: `licenses/{rid}/{package}/{file}` is raw per-RID evidence (written by ArtifactPlanner, cleaned per-RID by HarvestTask); `licenses/_consolidated/{package}/{file}` is the deduplicated union produced by ConsolidateHarvestTask and consumed by `src/native/Directory.Build.props` at pack time. Divergent license content across RIDs (same package+filename, different text) is emitted as per-RID suffixed variants (`copyright.win-x64`, `copyright.linux-x64`) and flagged with a warning so no attribution is silently chosen as the winner. Tests: 4 new consolidation cases in `ConsolidateHarvestTests` (union, dedup, divergence, no-success-rid skip). Full behavioral validation across a real multi-runner matrix pending Stream D-ci.
18. **G49 core-library identity guardrail landed (2026-04-18)**: Two manifest.json fields carry the "which vcpkg package is the core library" answer — `library_manifests[].core_lib=true` and `packaging_config.core_library` — and used to be read by two separate code paths (ArtifactPlanner + HybridStaticStrategy DI factory). Post-A1: ManifestConfig exposes a single `CoreLibrary` computed property (materializes the `IsCoreLib=true` library manifest; throws only on structural errors — zero or multiple cores); runtime consumers read via that property. PreFlight gains G49 (`CoreLibraryIdentityValidator`) which asserts the two manifest fields agree, failing the operator cleanly instead of letting a silent drift propagate.
19. **ADR-003 orchestration implementation is landed end to end (2026-04-25 status; PA-2 + PD-5 + CI routing closures 2026-04-29)**: the Slice E follow-up pass closed on master `d190b5b`, so the live surface is now `release.yml` + `build-linux-container.yml`, not the retired `prepare-native-assets-*` / `release-candidate-pipeline.yml` family. Three `IPackageVersionProvider` impls (Manifest / GitTag / Explicit), per-stage request records across all seven stages, stage-owned validation (PreFlight / Harvest / NativeSmoke / Pack / ConsumerSmoke), consumer-smoke matrix re-entry, Option A resolver-centric `SetupLocalDev`, PD-13 closure, G58 Pack-stage gate + PreFlight mirror, three-platform witness trail are all in place. PA-2 behavioral validation closed 2026-04-26 via run 24938451364 on master `8ec85c5`. PD-5 (RemoteInternal artifact source) fully closed 2026-04-29: `release.yml` run 24962876812 published the first internal-feed wave (5 families × 2 nupkgs) to `https://nuget.pkg.github.com/janset2d/index.json`, then remote witnesses passed on Windows, WSL Linux, and macOS Intel against that same wave. Trigger-aware `ResolveVersions` routing now covers `workflow_dispatch mode=manifest-derived`, `workflow_dispatch mode=explicit` through `--version-source=explicit`, family tags through git-tag, and train tags through meta-tag; tag-push staging is open for the internal feed. Remaining Phase 2b tail: PD-7 (`PublishPublicTask` real impl + nuget.org promotion via Trusted Publishing OIDC + first prerelease publication, #63).
20. **Slice E infrastructure + GHCR builder image are live (2026-04-25)**: the Cake-host-build-once pattern is active in `release.yml`, Linux jobs consume the multi-arch GHCR image published by `build-linux-container.yml`, and `build/manifest.json` points Linux runtime rows at `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest`. The open work has moved from image bring-up to operational hardening and publication flow.
21. **Slice E follow-up pass closed two operational gaps in the working tree (2026-04-23)**: `PackageConsumerSmoke` no longer relies on inline workflow PowerShell to make `win-x86` pass — new `IDotNetRuntimeEnvironment` / `DotNetRuntimeEnvironment` resolves required x86 runtime channels from executable TFMs, installs them via the official `dotnet-install.ps1` into a temp cache, and injects only `DOTNET_ROOT_X86` + `DOTNET_ROOT(x86)` into child `dotnet test` invocations so the parent Cake host stays on x64. Separately, `vcpkg-setup` now resolves `container_image` tags such as `ghcr.io/...:focal-latest` to immutable per-platform child manifest digests inline for cache keys (matched via `runner.os` + `runner.arch`, with top-level digest fallback) while the actual job still runs on the mutable tag; platform branching now keys on `runner.os` instead of string-comparing runner labels like `windows-latest`.
22. **Build-host test suite: 460 tests green (measured 2026-04-23)**. Trajectory across the pass: 340 (slice-A baseline) → 355 (B1) → 390 (D pre-witness) → 398 (B2) → 400 (CA) → 418 (C partial) → 426 (C closure) → 460 (Slice E follow-up runtime/cache hardening). Coverage ratchet policy active via `Coverage-Check` Cake task against `build/coverage-baseline.json`; CI gate wiring still deferred to Stream C PreflightGate.

## Cross-Reference

- **Detailed phase docs**: [phases/](phases/) — active execution ledger in [phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md); planned-phase design briefs kept only when they carry unique design content (see `phases/README.md` retention test)
- **Architecture Decision Records**: [decisions/](decisions/) — [ADR-001 D-3seg + package-first + artifact source profile](decisions/2026-04-18-versioning-d3seg.md), [ADR-002 DDD layering for build host](decisions/2026-04-19-ddd-layering-build-host.md), [ADR-003 release lifecycle orchestration + version source providers](decisions/2026-04-20-release-lifecycle-orchestration.md)
- **Technical deep-dives**: [knowledge-base/](knowledge-base/)
- **Design rationale & research**: [research/](research/)
- **How-to recipes**: [playbook/](playbook/)
- **Deep general references**: [reference/](reference/)
- **Historical snapshots**: [_archive/](_archive/) — pre-rewrite bodies of canonical documents preserved for historical rationale
