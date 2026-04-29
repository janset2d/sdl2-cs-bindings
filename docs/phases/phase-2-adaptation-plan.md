# Phase 2 Adaptation Plan — Active Execution Ledger

**Date:** 2026-04-25 (post-Slice E follow-up pass closure; historical body archived at [../_archive/phase-2-adaptation-plan-2026-04-15.md](../_archive/phase-2-adaptation-plan-2026-04-15.md))
**Status:** IN PROGRESS — ADR-003 implementation and CI/CD rewrite are landed; PA-2 behavioral validation closed 2026-04-26; PD-5 (RemoteInternal artifact source + PublishStagingTask real impl + all-host remote witness) fully closed 2026-04-29. Remaining work is the Phase 2b tail (`ResolveVersions` trigger-aware version routing before any publish-staging tag-push gate-lift, PD-7 / first prerelease publication to nuget.org).
**Prerequisites:**

- [ADR-001 — D-3seg Versioning, Package-First Local Dev, Artifact Source Profile](../decisions/2026-04-18-versioning-d3seg.md) (2026-04-18)
- [ADR-002 — DDD Layering for the Cake Build-Host](../decisions/2026-04-19-ddd-layering-build-host.md) (2026-04-19)
- [ADR-003 — Release Lifecycle Orchestration + Version Source Providers](../decisions/2026-04-20-release-lifecycle-orchestration.md) (2026-04-21, v1.5)
- [Release Lifecycle Direction](../knowledge-base/release-lifecycle-direction.md) (policy only, post-narrowing)

## Purpose

This document is the **active execution ledger** for Phase 2 (CI/CD & Packaging). It tracks current stream status, remaining gates, and open Pending Decisions. It does not re-derive policy (that lives in `release-lifecycle-direction.md` and the three ADRs) and it does not re-derive roadmap (that lives in `plan.md`).

Historical amendment layers (S1 adoption, retired streams A0 / A-risky partial revert, ADR-001 / ADR-003 rollout narratives, closed PDs) are preserved in the [archive](../_archive/phase-2-adaptation-plan-2026-04-15.md) for "why was this retired?" archaeology. This doc reads the post-ADR-003 baseline as settled.

## Current State (2026-04-25)

**Baseline established:**

- ADR-001 D-3seg versioning locked (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`); within-family minimum range; cross-family ranged `>= x.y.z, < (UpstreamMajor+1).0.0`; G54–G57 guardrails active.
- ADR-002 DDD layering landed: `Tasks / Application / Domain / Infrastructure` + `Context` + Cake-native `Tasks/` exception; `LayerDependencyTests` enforces three invariants; Harvest fat-task runner extraction (Wave 6) landed.
- ADR-003 orchestration implementation **is landed end to end on master 2026-04-25 (`d190b5b`)**: three version-source providers (Manifest / GitTag / Explicit), seven per-stage request records, stage-owned validation across all stages, consumer-smoke matrix re-entry in `release.yml`, Option A resolver-centric `SetupLocalDevTaskRunner`, `G58CrossFamilyDepResolvabilityValidator` Pack-stage gate + PreFlight mirror, flat task graph (all `[IsDependentOn]` removed), runner-strict `--explicit-version` (PackageConsumerSmokeRunner rejects empty `Versions`), PD-13 closed, and the Slice E follow-up pass is closed.
- PA-1 closed 2026-04-18: Stream C keeps RID-only CI matrix model; strategy stays runtime-row metadata, not a matrix axis.
- PA-2 mechanism landed 2026-04-18; PA-2 behavioral validation closed 2026-04-26 via `release.yml` run 24938451364 on master `8ec85c5`. All 7 `manifest.runtimes[]` rows pack + consumer-smoke green end-to-end on their native runners; the four newly-hybridized rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) all cleared per-TFM TUnit with zero failures (35/35 on Windows ARM64 and x86 including net462; 24/24 on Linux and macOS arm64 with net462 platform-gated).
- H1 landed 2026-04-18: harvest license layout moved to per-RID + `_consolidated/`; consolidation receipt asserted by `PackageTaskRunner`.
- Build-host test suite: **493 tests green** (2026-04-29 measurement; trajectory 340 → 355 → 390 → 398 → 400 → 418 → 426 → 460 → 493 across Slices A → E + PD-5 closure).
- D-local end-to-end validated on three hybrid-static RIDs (`win-x64`, `linux-x64`, `osx-x64`) across all concrete families; Slice B2 `SetupLocalDevTaskRunner` composition and Slice C runner-strict `--explicit-version` landed without regression.
- `SetupLocalDev --source=local` operational end to end on Windows + WSL + macOS Intel; smoke csprojs restore + build + execute in IDE via `build/msbuild/Janset.Local.props` (renamed from `Janset.Smoke.local.props` in Slice C.8a).
- Slice E follow-up pass closed 2026-04-25: `release.yml` absorbed the old workflow discipline via composite actions, lock-file discipline landed, the retired `prepare-native-assets-*` / `release-candidate-pipeline.yml` family is gone, `PublishStagingTask` / `PublishPublicTask` stubs are wired and gated, the GHCR Linux builder image is live, and Linux `manifest.runtimes[]` rows point at `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest`.
- PD-5 fully closed 2026-04-29 (commits `bc7c677` + `fdabcae` + `549ad2f`, witness completion on `4afdd1d`): `RemoteArtifactSourceResolver` + `INuGetFeedClient` / `NuGetProtocolFeedClient` make `SetupLocalDev --source=remote` operational; `PublishStagingTask` real impl pushes managed + native nupkg pairs to GitHub Packages via `PackageUpdateResource.Push`; `VersionsFileWriter` shared helper extracted so Local + Remote profiles emit identical `versions.json`; `smoke-witness.cs` gains `remote` mode. `release.yml` run 24962876812 (workflow_dispatch on `fdabcae` with `publish-staging=true`) published the first internal-feed wave: 5 families × 2 nupkgs = 10 nupkgs at `<UpstreamMajor>.<UpstreamMinor>.0-ci.24962876812.1`. Remote host witnesses against that wave are green on all 3 maintainer platforms: Windows `win-x64` 3/3 PASS in 78.6s, ConsumerSmoke 35/35; WSL Linux `linux-x64` 3/3 PASS in 71.0s, ConsumerSmoke 24/24 with `net462` skipped per Mono/TUnit incompatibility; macOS Intel `osx-x64` 3/3 PASS in 119.5s, ConsumerSmoke 24/24 with `net462` skipped because Mono is not installed.

**Remaining Phase 2b tail:**

- `ResolveVersions` trigger-aware version routing — current `release.yml` hard-codes manifest-derived CI suffixes in the `resolve-versions` job. Before any tag-push `publish-staging` gate-lift, route `workflow_dispatch mode=explicit` through `ResolveVersions --version-source=explicit` with repeated `--explicit-version family=semver`, route family tags through `GitTagVersionProvider`, route train tags through meta-tag mode, and keep downstream stages uniformly consuming `--versions-file`.
- `publish-staging` tag-push gate-lift — currently `if: github.event_name == 'workflow_dispatch' && inputs.publish-staging == true`; lift only after version-source routing lands, otherwise a tag push would publish CI-suffixed packages instead of tag-derived family versions.
- PD-7 + `PublishPublicTask` real impl: nuget.org promotion via Trusted Publishing (OIDC); separate `Promote-To-Public.yml` workflow OR a new stage on `release.yml`; first prerelease publication (#63).
- PD-8: `playbook/release-recovery.md` operator-driven escape hatch via `ExplicitVersionProvider`.
- Stream E: change-detection feasibility spike (2a scope only; full impl deferred to 2b).

## Strategy & Tool Landing State

Post-Stream B closure (#85) + ADR-001 + PA-2. This section is **stable fact listing**, not a rolling audit.

### Strategy seam shape

| Component | Role | State |
| --- | --- | --- |
| `IPackagingStrategy` | Named accessor: `Model` + `IsCoreLibrary(string vcpkgName)` string-compare helper | **Landed as designed.** Consumed by `HybridStaticValidator` only; Packaging module does not consume it. Pack output shape is identical for hybrid and pure-dynamic RIDs. |
| `IDependencyPolicyValidator` | Strategy-aware harvest-closure policy | **Landed as designed.** `HybridStaticValidator` has real behavioral logic (transitive-dep leak detection via scanner output; **G19**). `PureDynamicValidator` is an intentional pass-through per the design brief. Wired per-RID via DI; invoked from `HarvestTaskRunner`. |
| `StrategyResolver` + `IStrategyCoherenceValidator` | Manifest↔triplet coherence check | **Landed.** Invoked from `PreflightTaskRunner` as **G16**. |
| `INativeAcquisitionStrategy` | Native binary acquisition routes (brief draft: VcpkgBuild / Overrides / CiArtifact) | **Retired by ADR-001.** The Artifact Source Profile abstraction (`IArtifactSourceResolver` + `ArtifactProfile { Local / RemoteInternal / ReleasePublic }`) covers the feed-prep concern from the consumer-facing side. The original interface is not re-added. |
| `IPayloadLayoutPolicy` | Windows direct-copy vs Unix archive policy abstraction | **Still deferred.** The brief-cited trigger condition ("when PackageTask is implemented") has been met. The decision between extracting the policy now or formally retiring the deferral belongs to the Cake refactor pass (ADR-003 impl). |

### Scanner-as-guardrail (second-consumer pattern)

`WindowsDumpbinScanner`, `LinuxLddScanner`, `MacOtoolScanner` were originally built only for dependency discovery by `BinaryClosureWalker`. Post-strategy wiring they carry a second role as **packaging guardrail input** with zero scanner-code changes:

- `BinaryClosureWalker` still calls the platform-specific scanner → produces `BinaryClosure` for `ArtifactPlanner` (original role, preserved).
- `HybridStaticValidator` consumes the same `BinaryClosure` as a second consumer, filters by system-file / primary / core-library rules, fails on transitive-dep leak (**G19**).
- `HarvestTask` post-deployment assertion fails when `DeploymentStatistics.PrimaryFiles.Count == 0` (**G50** — strategy-agnostic, catches silent feature-flag degradation).

This architectural move is the thesis of [cake-strategy-implementation-brief-2026-04-14.md](../research/cake-strategy-implementation-brief-2026-04-14.md) §"Scanner Repurposing" and is fully realized end to end. Anchored canonically in [cake-build-architecture.md](../knowledge-base/cake-build-architecture.md) §Strategy awareness.

ADR-003 does **not** retire or weaken this move. It only classifies the same `HybridStaticValidator` / `G19` / `G50` behavior under the Harvest stage's owned validation surface. The scanner-as-stable-producer + validator-as-second-consumer pattern remains the architectural explanation for how the project moved from the old pure-dynamic assumption set to hybrid-static guardrails without rewriting `dumpbin` / `ldd` / `otool` integrations.

### Known gaps (named so they don't rediscover under deadline pressure)

- **Pure-dynamic path has no behavioral closure check.** Post-PA-2, no `manifest.runtimes[]` row uses `pure-dynamic`. The pass-through validator is a dormant fallback. Before any future reintroduction, decide whether `PureDynamicValidator` gains an actual contract or whether pure-dynamic retires permanently.
- **Packaging module does not consume `IPackagingStrategy`.** If pack-time behavior ever needs to vary by strategy, the seam needs to be added (e.g., pure-dynamic nupkgs shipping differently-shaped `runtimes/` subtrees). Not present today.
- **`IPayloadLayoutPolicy` deferral is stale.** The trigger condition fired weeks ago. Up-or-down decision belongs to the Cake refactor pass.

## Implementation Streams

Stream status summary. Detailed historical narrative (retired A0, partially-reverted A-risky pre-S1 shape, pre-ADR-001 Stream F) lives in the archive. Only current status + remaining gates are listed here.

| Stream | Scope | Status |
| --- | --- | --- |
| **A-safe** | manifest.json `package_families` schema + NuGet.Versioning in Cake build host | **Landed.** Schema v2.1 live; NuGet.Versioning consumed by D-local pack + upper-bound resolver. |
| **A-risky** | MinVer 7.0.0 + family identifier rename (`sdl<major>-<role>`) + csproj `<MinVerTagPrefix>` per family + PreFlight csproj pack contract (G4/G6/G7/G17/G18) | **Landed (post-S1 subset).** Exact-pin subset (G1/G2/G3/G5/G8) retired by S1; MinVer + family rename + retained guardrails live. |
| **B** | Strategy seam wiring (DI + Harvest validator + PreFlight coherence) | **Landed.** #85 closed. `HarvestPipeline` orchestration extraction remains in #87. |
| **C** | PreFlightCheck as CI gate + `GenerateMatrixTask` + CI workflow migration | **Landed.** `release.yml` is live with version-aware PreFlight and matrix generation from `manifest.runtimes[]`; remaining work is witness coverage and publish hardening, not workflow migration. |
| **D-local** | Cake `Package` task + `PackageConsumerSmoke` + local folder feed validation | **Landed for Phase 2a proof slice.** Three hybrid-static RIDs × `sdl2-core` + `sdl2-image` green end to end; Windows host expanded to 5 families. Remaining: 7-RID × 5-family matrix coverage (needs PA-2 witness + Stream C dynamic matrix). |
| **D-ci** | CI package-publish + smoke gate + internal feed push | **Internal-feed push landed 2026-04-26** (PD-5 / commit `fdabcae`): `release.yml` `publish-staging` job pushes managed + native nupkg pairs to GitHub Packages via `PackageUpdateResource.Push`; gated behind `workflow_dispatch.inputs.publish-staging=true`. **Remaining**: trigger-aware `ResolveVersions` routing first, then tag-push gate-lift; PD-7 nuget.org promotion (`Promote-To-Public.yml` or equivalent) + Trusted Publishing OIDC; PD-8 manual escape hatch. |
| **E** | Change detection (dotnet-affected in Cake) | **Scope-reduced to 2a feasibility spike.** Full impl + CI filtering deferred to 2b. |
| **F** | Local-dev feed preparation + remote feed acquisition | **Both profiles landed.** `LocalArtifactSourceResolver` packs families at manifest-derived upstream-aligned prerelease versions + writes `Janset.Local.props`. `RemoteArtifactSourceResolver` (PD-5 closure 2026-04-26) discovers latest published nupkgs from GitHub Packages internal feed, downloads managed + native pairs, writes the same `Janset.Local.props` + `versions.json` shape. `--source=release` (public NuGet.org) stays stubbed pending PD-7. |

## ADR-003 Implementation Sequence

The post-ADR-003 implementation proceeded as separately-revertable commit groups against the `feat/adr003-impl` branch, merged to master 2026-04-22 at `bfc6713`. Each step references ADR-003 sections.

1. **Canonical documentation sweep** *(landed across Slices A→E)* — alignment pass across `release-lifecycle-direction.md`, this doc, `cake-build-architecture.md`, `release-guardrails.md`, `ci-cd-packaging-and-release-plan.md`, `cross-platform-smoke-validation.md` (Cake-first rewrite via Slice DA), `unix-smoke-runbook.md` (new, supersedes TEMP-wsl / TEMP-macos), `plan.md`, plus retire-to-stub for `phase-2-cicd-packaging.md` and `phase-3-sdl2-complete.md`. `Janset.Smoke.local.props` → `Janset.Local.props` reference sweep landed in Slice C.12; Slice E follow-up closed the broader doc tail (including §11 Q17 / Q18 closures) and moved any remaining legacy-surface cleanup into normal documentation hygiene rather than a blocked phase gate.
2. **Cake refactor** *(landed, Slices A → C)* — `IPackageVersionProvider` + 3 impls (`ManifestVersionProvider`, `ExplicitVersionProvider`, `GitTagVersionProvider`) landed. All seven per-stage request records materialized under `Domain/<Module>/Models/`. `NativeSmokeTask` extracted from Harvest into its own task. `PackageTask` input moved to per-family version mapping. `G58CrossFamilyDepResolvabilityValidator` wired as Pack-stage gate plus PreFlight mirror (defense-in-depth, single DI singleton shared). `--family-version` retired (PD-13 closed); `--explicit-version` is runner-strict — `PackageConsumerSmokeRunner` rejects empty `Versions`. `SetupLocalDev` composition stays Option A via `SetupLocalDevTaskRunner` under `Application/Packaging/`. Three-platform smoke witness (Windows + WSL + macOS Intel) green on `./tests/scripts/smoke-witness.cs local` + `ci-sim`. Test suite 340 → 426.
3. **CI/CD workflow rewrite** *(landed through Slice E follow-up pass; PublishStaging real impl 2026-04-26)* — `release.yml` grew through every slice: stub `resolve-versions-stub` (A) → real `resolve-versions` + `preflight` (B1) → `generate-matrix` + first-draft `harvest` / `native-smoke` / `consolidate-harvest` (D) → real `pack` + `consumer-smoke` matrix re-entry + `publish-staging` / `publish-public` stubs (C) → `build-cake-host` single-runner FDD publish + all consumers consume `dotnet ./cake-host/Build.dll` instead of per-job restore+build (E1a-b) → composite-action absorption, lock-file discipline, workflow retirement, three-platform witness closure in the Slice E follow-up pass → `publish-staging` real steps (PD-5 commit `fdabcae`): gated `workflow_dispatch.inputs.publish-staging=true`, per-job `permissions: packages: write` override, downloads cake-host + nupkg-output + versions.json, maps `secrets.GITHUB_TOKEN → env: GH_TOKEN`, invokes `PublishStagingTask`. `docker/linux-builder.Dockerfile` and `.github/workflows/build-linux-container.yml` back the live multi-arch GHCR Linux builder image. **Course correction 2026-04-29:** tag-push staging must not be enabled until `resolve-versions` routes by trigger and mode; `workflow_dispatch mode=explicit` flows through `ResolveVersions --version-source=explicit`, family tags flow through `GitTagVersionProvider`, train tags flow through meta-tag mode, and downstream jobs keep using the single `versions.json` artifact. The remaining work is operational: version-routing first, then gate-lift on `publish-staging`, PD-7 nuget.org promotion path, real `publish-public` transfer.
4. **PA-2 behavioral validation via the live pipeline** *(closed 2026-04-26)* — `workflow_dispatch` `release.yml` run 24938451364 on master `8ec85c5` (`mode=manifest-derived`, suffix `ci.24938451364.1`) cleared the full Cake target chain on all 7 RIDs. The four PA-2 rows packed + consumer-smoked end-to-end on their native runners with zero-failure per-TFM TUnit. The playbook's prior `pa2.<run-id>` suffix wording was fictional (`release.yml` always emits `ci.<run-id>.<attempt>`); operator-supplied custom suffix shapes ride through `ExplicitVersionProvider` instead of a manifest-derived knob.

## Alignment Items

| # | Item | Status |
| --- | --- | --- |
| **PA-1** | Matrix strategy review (RID-only vs `strategy × RID` vs parity-job) | **Resolved 2026-04-18** — keep RID-only (7 jobs); `strategy` stays runtime-row metadata. Supporting analysis: [ci-matrix-strategy-review-2026-04-17.md](../research/ci-matrix-strategy-review-2026-04-17.md). |
| **PA-2 (mechanism)** | Hybrid overlay triplet expansion to remaining 4 RIDs | **Landed 2026-04-18** — overlay triplets exist; all 7 runtime rows on `hybrid-static`; CI shared vcpkg setup + orchestrator workflow aligned. |
| **PA-2 (behavioral)** | End-to-end pack + consumer smoke on the four newly-hybridized rows | **Resolved 2026-04-26** via `release.yml` run 24938451364 on master `8ec85c5`. All 7 RIDs cleared Pack + ConsumerSmoke; per-TFM TUnit zero-failure on each PA-2 row (win-arm64 35/35, win-x86 35/35, linux-arm64 24/24, osx-arm64 24/24, with net462 auto-skipped on Linux + macOS per platform gate). |

## Pending Decisions

Closed / withdrawn / recorded PDs (PD-1, PD-2, PD-4, PD-6, PD-9, PD-11, PD-12) live in the archive. Only Open and Direction-Selected PDs are tracked here.

| # | Decision | Status | Exit Criterion | Blocks |
| --- | --- | --- | --- | --- |
| **PD-3** | dotnet-affected integration: NuGet library vs CLI wrapper via `Cake.Process` | Open — 2a feasibility spike only; full decision in 2b | ADR-style note committing to one path after spike | Stream E full implementation (2b) |
| **PD-5** | `RemoteInternal` Artifact Source Profile concrete implementation (reframed under ADR-001) | **Resolved 2026-04-29** (impl commits `bc7c677`, `fdabcae`, `549ad2f`; all-host witness on `4afdd1d`). | Internal feed URL `https://nuget.pkg.github.com/janset2d/index.json` (hard-coded constant in `RemoteArtifactSourceResolver` + `PublishStagingTask`); auth via `GH_TOKEN → GITHUB_TOKEN` env chain (Classic PAT with `read:packages`; CI maps `secrets.GITHUB_TOKEN → env: GH_TOKEN`); cache strategy = `artifacts/packages/` reused across profiles with wipe-on-prepare; `SetupLocalDev --source=remote` operational on all 3 maintainer host platforms against CI run 24962876812: Windows `win-x64` 3/3 PASS, ConsumerSmoke 35/35; WSL Linux `linux-x64` 3/3 PASS, ConsumerSmoke 24/24 (`net462` skipped); macOS Intel `osx-x64` 3/3 PASS, ConsumerSmoke 24/24 (`net462` skipped because Mono absent). | — |
| **PD-7** | Full-train release orchestration mechanism | **Direction selected** (ADR-003) — meta-tag + manifest-driven topological ordering via `GitTagVersionProvider` multi-mode | Formal closure: `full-train-release.md` playbook drafted; meta-tag trigger wired in `release.yml`; industry precedent survey completed in [full-train-release-orchestration-2026-04-16.md](../research/full-train-release-orchestration-2026-04-16.md) closed | Stream D-ci CI release pipeline |
| **PD-8** | Release recovery + manual escape hatch | **Direction selected** (ADR-003) — operator runs same pipeline via `ExplicitVersionProvider` (`--explicit-version family=version,...`) | Formal closure: `playbook/release-recovery.md` exists with operator-executable step lists; Cake `Pack-Family` / `Smoke-Family` / `Push-Family` helpers implemented (or explicitly deferred); API key provisioning policy documented | Stream D-local Cake helper exposure, Stream D-ci CI publish pipeline (manual flow must mirror CI flow step-for-step) |
| **PD-10** | D-local package-consumer smoke scope: `-r <rid>` vs framework-dependent resolver path coverage | Open — recommended resolution moment: when K checkpoint promoted to active on all 3 platforms (Phase 2b) | Decision recorded: either (a) keep `-r <rid>`-only with documentation note, or (b) add second invocation without `-r <rid>` to cover default resolver path | Not blocking; current smoke still catches any real regression in the file-copy path |
| **PD-13** | `--family-version` CLI flag retirement | **Closed (2026-04-22, ADR-003).** `--explicit-version` mandatory; runner-strict (`PackageConsumerSmokeRunner` throws on empty mapping, C.8); all legacy `--family-version` call sites removed. | See ADR-003 §6 PD-13 closure entry. | — |
| **PD-14** | Linux end-user MIDI packaging strategy: how SDL2_mixer consumers get MIDI decoder registration on Linux under LGPL-free policy | Open — resolution moment: pre-first-public-release-of-Mixer-family, coordinated with licensing review | ADR recorded with path chosen: (a) documentation only (`apt install freepats`), (b) opt-in `Janset.SDL2.Mixer.Native.Soundfonts` meta-package with permissive SF2, (c) opt-in GUS patches (likely non-starter under LGPL-free policy) | Not blocking for Phase 2; surfaces pre-first-public-Mixer-release |
| **PD-15** | SDL2_gfx Unix symbol-export regression guard (patch survival under vcpkg baseline bumps) | Open — resolution moment: next CI hardening pass or first vcpkg baseline bump touching sdl2-gfx ports | Decision recorded with chosen path: (a) smoke-time `readelf` / `nm` symbol assertion, (b) post-pack guardrail extending `PackageOutputValidator` to binary inspection, (c) both (defense in depth) | Not blocking; current patch working on all 3 platforms |
| **PD-16** | Shared native dependency duplicate policy: cross-package same-name binary collisions (e.g., `zlib1.dll` in `Image` + `Mixer` on Windows under pure-dynamic) | **Dormant under hybrid-static** — opened 2026-04-21 (absorbed from retired `phase-2-cicd-packaging.md` §2.8). Transitive deps statically baked into satellite primaries post-PA-2; `zlib1.dll` does not ship as a standalone file. G19 catches static bake leakage. Concern re-activates if pure-dynamic ever reintroduced OR if defense-in-depth audit requested. | If pursued: (1) per-RID duplicate basename inventory across satellite native payloads, (2) hash equality report (initial focus: zlib family), (3) decision record for same-name handling, (4) CI guardrail design (fail build when duplicate basenames have non-identical hashes for same RID), (5) migration notes if shared-dependency strategy adopted later. Acceptance gate: do not modify packaging / CI behavior on this topic until deliverables reviewed and approved. | Not blocking under hybrid-static. Blocks any future pure-dynamic reintroduction. |

## Cross-Reference

- **Policy:** [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md)
- **Orchestration architecture:** [ADR-003](../decisions/2026-04-20-release-lifecycle-orchestration.md)
- **Build-host layering:** [ADR-002](../decisions/2026-04-19-ddd-layering-build-host.md)
- **Versioning + consumer contract:** [ADR-001](../decisions/2026-04-18-versioning-d3seg.md)
- **Pipeline + guardrails:** [cake-build-architecture.md](../knowledge-base/cake-build-architecture.md), [release-guardrails.md](../knowledge-base/release-guardrails.md), [ci-cd-packaging-and-release-plan.md](../knowledge-base/ci-cd-packaging-and-release-plan.md)
- **Smoke validation:** [cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md)
- **Roadmap + status:** [plan.md](../plan.md)
- **Historical rationale:** [archive](../_archive/phase-2-adaptation-plan-2026-04-15.md)
