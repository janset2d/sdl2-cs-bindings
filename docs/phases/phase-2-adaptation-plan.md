# Phase 2 Adaptation Plan — Active Execution Ledger

**Date:** 2026-04-21 (post-ADR-003 rewrite; historical body archived at [../_archive/phase-2-adaptation-plan-2026-04-15.md](../_archive/phase-2-adaptation-plan-2026-04-15.md))
**Status:** IN PROGRESS — canonical documentation sweep underway; Cake refactor + CI/CD rewrite + PA-2 behavioral validation queued.
**Prerequisites:**

- [ADR-001 — D-3seg Versioning, Package-First Local Dev, Artifact Source Profile](../decisions/2026-04-18-versioning-d3seg.md) (2026-04-18)
- [ADR-002 — DDD Layering for the Cake Build-Host](../decisions/2026-04-19-ddd-layering-build-host.md) (2026-04-19)
- [ADR-003 — Release Lifecycle Orchestration + Version Source Providers](../decisions/2026-04-20-release-lifecycle-orchestration.md) (2026-04-21, v1.5)
- [Release Lifecycle Direction](../knowledge-base/release-lifecycle-direction.md) (policy only, post-narrowing)

## Purpose

This document is the **active execution ledger** for Phase 2 (CI/CD & Packaging). It tracks current stream status, remaining gates, and open Pending Decisions. It does not re-derive policy (that lives in `release-lifecycle-direction.md` and the three ADRs) and it does not re-derive roadmap (that lives in `plan.md`).

Historical amendment layers (S1 adoption, retired streams A0 / A-risky partial revert, ADR-001 / ADR-003 rollout narratives, closed PDs) are preserved in the [archive](../_archive/phase-2-adaptation-plan-2026-04-15.md) for "why was this retired?" archaeology. This doc reads the post-ADR-003 baseline as settled.

## Current State (2026-04-21)

**Baseline established:**

- ADR-001 D-3seg versioning locked (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`); within-family minimum range; cross-family ranged `>= x.y.z, < (UpstreamMajor+1).0.0`; G54–G57 guardrails active.
- ADR-002 DDD layering landed: `Tasks / Application / Domain / Infrastructure` + `Context` + Cake-native `Tasks/` exception; `LayerDependencyTests` enforces three invariants; Harvest fat-task runner extraction (Wave 6) landed.
- ADR-003 orchestration direction selected (v1.5): three version-source providers (Manifest / GitTag / Explicit), stage-owned validation, consumer smoke matrix re-entry, Option A resolver-centric composition for `SetupLocalDev`. **Pseudocode level — implementation pending.**
- PA-1 closed 2026-04-18: Stream C keeps RID-only CI matrix model; strategy stays runtime-row metadata, not a matrix axis.
- PA-2 mechanism landed 2026-04-18: all 7 `manifest.runtimes[]` rows now use hybrid overlay triplets. Behavioral validation on the four newly-covered rows pending (see Alignment Items).
- H1 landed 2026-04-18: harvest license layout moved to per-RID + `_consolidated/`; consolidation receipt asserted by `PackageTaskRunner`.
- Build-host test suite: 340 tests green (2026-04-20 measurement).
- D-local end-to-end validated on three hybrid-static RIDs (`win-x64`, `linux-x64`, `osx-x64`) for the `sdl2-core` + `sdl2-image` proof slice; Windows host expanded to all five concrete families (`sdl2-core` / `-image` / `-mixer` / `-ttf` / `-gfx`) on `win-x64`.
- `SetupLocalDev --source=local` operational end to end on Windows host; smoke csprojs restore + build + execute in IDE via `build/msbuild/Janset.Smoke.local.props`.

**Not yet green:**

- Canonical documentation sweep (active; this pass).
- Cake refactor per ADR-003 §3: `IPackageVersionProvider` + 3 impls, per-stage request records, `NativeSmokeTask` extraction, `--family-version` → `--explicit-version` retirement, G58 validator.
- CI/CD workflow rewrite per ADR-003 §3.4: new `release.yml` with dynamic matrix from `manifest.runtimes[]` and consumer smoke matrix re-entry; deprecation or harvest-only reuse of existing `prepare-native-assets-*.yml`.
- PA-2 behavioral witness runs on `win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64` via the new pipeline.
- Remote artifact source profile implementation (`RemoteArtifactSourceResolver` + `SetupLocalDev --source=remote`) — Phase 2b Stream D-ci.
- Stream D-ci: CI package-publish job + smoke gate + internal feed push.
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
| **C** | PreFlightCheck as CI gate + `GenerateMatrixTask` + CI workflow migration | **Open.** Target shape recomposed under ADR-003 §3.4 (new `release.yml`, dynamic matrix from `manifest.runtimes[]`, PreFlight version-aware by contract). |
| **D-local** | Cake `Package` task + `PackageConsumerSmoke` + local folder feed validation | **Landed for Phase 2a proof slice.** Three hybrid-static RIDs × `sdl2-core` + `sdl2-image` green end to end; Windows host expanded to 5 families. Remaining: 7-RID × 5-family matrix coverage (needs PA-2 witness + Stream C dynamic matrix). |
| **D-ci** | CI package-publish + smoke gate + internal feed push | **Open.** Depends on Stream C + PD-5 (Remote profile) + PD-7 (full-train orchestration) + PD-8 (manual escape hatch). |
| **E** | Change detection (dotnet-affected in Cake) | **Scope-reduced to 2a feasibility spike.** Full impl + CI filtering deferred to 2b. |
| **F** | Local-dev feed preparation + remote feed acquisition | **`--source=local` landed.** `LocalArtifactSourceResolver` packs families at manifest-derived upstream-aligned prerelease versions + writes `Janset.Smoke.local.props`. `--source=remote` interface stubbed (`UnsupportedArtifactSourceResolver`); concrete `RemoteArtifactSourceResolver` is Phase 2b Stream D-ci scope (PD-5). |

## ADR-003 Implementation Sequence

The post-ADR-003 implementation proceeds as separately-revertable commit groups. Each step references ADR-003 sections.

1. **Canonical documentation sweep** *(active, 2026-04-21)* — 14-step alignment pass across `release-lifecycle-direction.md`, this doc, `cake-build-architecture.md`, `release-guardrails.md`, `ci-cd-packaging-and-release-plan.md`, `cross-platform-smoke-validation.md`, `plan.md`, plus retire-to-stub for `phase-2-cicd-packaging.md` and `phase-3-sdl2-complete.md`.
2. **Cake refactor** (2-3 sessions, iterative) — `IPackageVersionProvider` + 3 impls (Manifest / GitTag / Explicit); per-stage request records (`PreflightRequest`, `HarvestRequest`, `NativeSmokeRequest`, `ConsolidateHarvestRequest`, `PackRequest`, `PackageConsumerSmokeRequest`, `PublishRequest`); extract `NativeSmokeTask` from Harvest; `PackageTask` input moves to per-family version mapping; `G58` validator in Pack stage; `--family-version` retire + `--explicit-version` introduce; `SetupLocalDev` composition stays Option A (resolver-centric).
3. **CI/CD workflow rewrite** (1-2 sessions) — new `release.yml` with tag + dispatch triggers, dynamic matrix from `GenerateMatrixTask`, consumer smoke matrix re-entry; deprecation or harvest-only reuse of existing `prepare-native-assets-*.yml`.
4. **PA-2 behavioral validation via the new pipeline** — `workflow_dispatch` on `win-arm64` / `win-x86` / `linux-arm64` / `osx-arm64`; `mode=manifest-derived`, `suffix=pa2.<run-id>`; failure triage per playbook PA-2 section (updated during sweep).

## Alignment Items

| # | Item | Status |
| --- | --- | --- |
| **PA-1** | Matrix strategy review (RID-only vs `strategy × RID` vs parity-job) | **Resolved 2026-04-18** — keep RID-only (7 jobs); `strategy` stays runtime-row metadata. Supporting analysis: [ci-matrix-strategy-review-2026-04-17.md](../research/ci-matrix-strategy-review-2026-04-17.md). |
| **PA-2 (mechanism)** | Hybrid overlay triplet expansion to remaining 4 RIDs | **Landed 2026-04-18** — overlay triplets exist; all 7 runtime rows on `hybrid-static`; CI shared vcpkg setup + orchestrator workflow aligned. |
| **PA-2 (behavioral)** | End-to-end pack + consumer smoke on the four newly-hybridized rows | **Pending.** Runs through the new `release.yml` once ADR-003 Cake refactor + workflow rewrite land. |

## Pending Decisions

Closed / withdrawn / recorded PDs (PD-1, PD-2, PD-4, PD-6, PD-9, PD-11, PD-12) live in the archive. Only Open and Direction-Selected PDs are tracked here.

| # | Decision | Status | Exit Criterion | Blocks |
| --- | --- | --- | --- | --- |
| **PD-3** | dotnet-affected integration: NuGet library vs CLI wrapper via `Cake.Process` | Open — 2a feasibility spike only; full decision in 2b | ADR-style note committing to one path after spike | Stream E full implementation (2b) |
| **PD-5** | `RemoteInternal` Artifact Source Profile concrete implementation (reframed under ADR-001) | Open — Phase 2b deliverable | Internal feed URL convention chosen, auth pattern documented, cache strategy validated, `SetupLocalDev --source=remote` operational on all 3 host platforms | `--source=remote` full implementation (Phase 2b, Stream D-ci) |
| **PD-7** | Full-train release orchestration mechanism | **Direction selected** (ADR-003) — meta-tag + manifest-driven topological ordering via `GitTagVersionProvider` multi-mode | Formal closure: `full-train-release.md` playbook drafted; meta-tag trigger wired in `release.yml`; industry precedent survey completed in [full-train-release-orchestration-2026-04-16.md](../research/full-train-release-orchestration-2026-04-16.md) closed | Stream D-ci CI release pipeline |
| **PD-8** | Release recovery + manual escape hatch | **Direction selected** (ADR-003) — operator runs same pipeline via `ExplicitVersionProvider` (`--explicit-version family=version,...`) | Formal closure: `playbook/release-recovery.md` exists with operator-executable step lists; Cake `Pack-Family` / `Smoke-Family` / `Push-Family` helpers implemented (or explicitly deferred); API key provisioning policy documented | Stream D-local Cake helper exposure, Stream D-ci CI publish pipeline (manual flow must mirror CI flow step-for-step) |
| **PD-10** | D-local package-consumer smoke scope: `-r <rid>` vs framework-dependent resolver path coverage | Open — recommended resolution moment: when K checkpoint promoted to active on all 3 platforms (Phase 2b) | Decision recorded: either (a) keep `-r <rid>`-only with documentation note, or (b) add second invocation without `-r <rid>` to cover default resolver path | Not blocking; current smoke still catches any real regression in the file-copy path |
| **PD-13** | `--family-version` CLI flag retirement | **Direction selected** (ADR-003) — flag retires in favor of `--explicit-version key=value,...` | Formal retirement: `--explicit-version` wired into `ExplicitVersionProvider`; legacy `--family-version` removed across all call sites during Cake refactor pass | Not blocking; current smoke runner trusts `Janset.Smoke.local.props` as version source |
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
