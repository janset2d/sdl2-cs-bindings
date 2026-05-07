# Project Plan — Janset.SDL2 / Janset.SDL3

**Maintainer:** Deniz Irgin (@denizirgin)

> Canonical roadmap. Forward-looking only — historical narrative lives in code + git history. When code and this file disagree, verify against code.

## Mission

Provide the .NET ecosystem with production-quality, modular SDL2 and SDL3 bindings that include **cross-platform native libraries built from source** — something no other project offers.

## Current Phase

Two streams are active in parallel:

**Phase 2: CI/CD & Packaging — IN PROGRESS.** Core surface is landed (`release.yml` + Cake build host + `tools.cs`). Phase 2b tail = nuget.org promotion (PD-7), release-recovery playbook (PD-8), and the four scope-assumption gaps surfaced in 2026-05-01 rehearsals.

**Phase X / build-host refactor (ADR-002 target-centric architecture) — ACTIVE.** P0 (docs/guardrails), P1 (baseline), P2a (V2 test infra), P2b (foundation primitives), P3 (repositories + BuildContext + InfoTask migration), and the ResolveVersions part of P4 are complete. `versions.json` path contract resolved: `--versions-file` is universal for both ResolveVersions writers and stage-task readers, `PathService` no longer hardcodes the output directory. `CleanArtifacts` and `CompileSolution` retired from Cake (CleanArtifacts: local hygiene owned by `tools.cs`; CompileSolution: zero callers, bare `dotnet build` covers the use case). `testing-guidelines.md` extracted as canonical test reference. 573 tests, 0 failures. Remaining P4 candidates: diagnostic targets. Canonical docs: [`decisions/2026-05-05-target-centric-build-host.md`](decisions/2026-05-05-target-centric-build-host.md), [`refactoring/target-centric-build-host-refactor-plan.md`](refactoring/target-centric-build-host-refactor-plan.md), [`refactoring/target-centric-build-host-review-checklist.md`](refactoring/target-centric-build-host-review-checklist.md), [`refactoring/testing-guidelines.md`](refactoring/testing-guidelines.md).

Active execution ledgers: [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) (Phase 2b) and [refactoring/target-centric-build-host-refactor-plan.md](refactoring/target-centric-build-host-refactor-plan.md) (build-host refactor).

## Roadmap

### Phase 2b — Public release tail

Detail in [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) (PDs, gaps, candidate directions).

- [ ] **PD-7 — Public NuGet promotion**: `PublishPublicTask` real implementation + nuget.org Trusted Publishing OIDC + first prerelease publication ([#63](https://github.com/janset2d/sdl2-cs-bindings/issues/63))
- [ ] **PD-8 — Release recovery playbook**: `playbook/release-recovery.md` operator runbook + `Pack-Family` / `Smoke-Family` / `Push-Family` Cake helpers (or explicit deferral)
- [ ] **PD-3 — `dotnet-affected` integration**: NuGet library vs CLI wrapper ADR after the spike
- [ ] **Pipeline scope-assumption gaps** (rehearsal 2026-05-01; full surface in adaptation plan):
  - [ ] Gap #2 — G58 feed-probe: cross-family resolvability against the publish target feed
  - [ ] Gap #3 — ConsumerSmoke partial-scope: per-family smoke csprojs candidate direction
  - [ ] Gap #4 — Trigger mechanism rework: manual `workflow_dispatch` or GitHub Releases as canonical trigger
- [ ] Generalize Cake `Pack` stage to all 6 satellites × 7 RIDs under the new pipeline
- [ ] Harden evidence collection and operator playbooks around the 7-RID consumer-smoke matrix re-entry
- [ ] Add SDL2_net bindings + native project ([#58](https://github.com/janset2d/sdl2-cs-bindings/issues/58)) — managed csproj + `.Native` csproj + overlay port (if needed) + manifest.json re-entry + vcpkg feature config + harvest validation
- [ ] Validate SDL2_mixer LGPL-free build across all RIDs (per-RID codec parity audit)
- [ ] Linux version scripts for symbol visibility (`.map` files per satellite) — lower priority
- [ ] Resolve **PD-14** Linux end-user MIDI packaging strategy (pre-first-public-Mixer-release)
- [ ] Resolve **PD-15** SDL2_gfx Unix symbol-export regression guard
- [ ] Windows local prerequisites guide (VS tooling + dumpbin/vswhere troubleshooting)
- [ ] Native binaries cleanup from git history ([#56](https://github.com/janset2d/sdl2-cs-bindings/issues/56))
- [ ] Validate and correct local development playbook ([#57](https://github.com/janset2d/sdl2-cs-bindings/issues/57))

### Phase 3 — SDL2 Complete

No separate phase doc per [phases/README.md](phases/README.md) retention test — items track here.

- [ ] Sample projects under `samples/` ([#60](https://github.com/janset2d/sdl2-cs-bindings/issues/60))
- [ ] `Janset.SDL2` meta-package ([#61](https://github.com/janset2d/sdl2-cs-bindings/issues/61))
- [ ] `CONTRIBUTING.md` and contributor workflow guidance ([#62](https://github.com/janset2d/sdl2-cs-bindings/issues/62))
- [ ] SDL2 smoke test suite + CI coverage ([#59](https://github.com/janset2d/sdl2-cs-bindings/issues/59))
- [ ] Linux runtime compatibility + minimum glibc docs ([#64](https://github.com/janset2d/sdl2-cs-bindings/issues/64))
- [ ] NoDependencies native package variant for minimal Linux ([#80](https://github.com/janset2d/sdl2-cs-bindings/issues/80))
- [ ] linux-musl-x64 + linux-musl-arm64 native asset coverage ([#82](https://github.com/janset2d/sdl2-cs-bindings/issues/82))

### Phase 4 — Binding Auto-Generation

Design brief: [phases/phase-4-binding-autogen.md](phases/phase-4-binding-autogen.md).

- [ ] Implement CppAst-based binding generator ([#69](https://github.com/janset2d/sdl2-cs-bindings/issues/69))
- [ ] Migrate SDL2 bindings from imported SDL2-CS files to generated code ([#70](https://github.com/janset2d/sdl2-cs-bindings/issues/70))

### Phase 5 — SDL3 Support

Design brief: [phases/phase-5-sdl3-support.md](phases/phase-5-sdl3-support.md).

- [ ] Add SDL3 bindings and native packages to monorepo ([#71](https://github.com/janset2d/sdl2-cs-bindings/issues/71))
- [ ] Extend CI and packaging flow for SDL3 prereleases ([#72](https://github.com/janset2d/sdl2-cs-bindings/issues/72))

### Phase X — Build-Host Modernization

ADR-004 migration closed (P0 → P4-A on `master`). [`ADR-002 target-centric refactor`](decisions/2026-05-05-target-centric-build-host.md) is the active continuation and absorbs residual ADR-004 cleanup — former P4-C pipeline decomposition and P5 atomic naming items are subsumed by ADR-002's full target-by-target refactor.

**Active plan:** [`refactoring/target-centric-build-host-refactor-plan.md`](refactoring/target-centric-build-host-refactor-plan.md). ADR-002 P0-P3 closed; P4 in progress. `Info`, `ResolveVersionsFromManifest`, `ResolveVersionsFromExplicit` migrated; `versions.json` path contract resolved; `CleanArtifacts` and `CompileSolution` retired from Cake; `testing-guidelines.md` extracted.

**Post-refactor tasks (after P10):**
- [ ] Redesign `FakeCakeWorldV2` fluent API — method names like `WithVersionsFile` / `WithSuffix` / `WithRid` are confusing (they set CLI option values, not file contents). A clearer separation between "CLI option seeding" and "fake filesystem seeding" is warranted once all targets are migrated and the full test surface is on V2.

### 2027 — Stabilization

- [ ] Stabilize SDL2 + SDL3 packages (v1.0)
- [ ] Community feedback incorporation
- [ ] Begin Janset2D development on top of these bindings

## Hardening Backlog

Open issues that are not phase-bound:

- [ ] [#65 Harden package validation and local feed workflows](https://github.com/janset2d/sdl2-cs-bindings/issues/65)
- [ ] [#66 Harden supply-chain and release integrity workflows](https://github.com/janset2d/sdl2-cs-bindings/issues/66)
- [ ] [#67 Implement external native override support](https://github.com/janset2d/sdl2-cs-bindings/issues/67) (parking lot)
- [ ] [#81 Add drift-prevention guardrail for harvest library lists](https://github.com/janset2d/sdl2-cs-bindings/issues/81)
- [ ] [#87 Extract HarvestPipeline service from HarvestTask](https://github.com/janset2d/sdl2-cs-bindings/issues/87)

For non-issue-tracked deferred ideas, see [parking-lot.md](parking-lot.md).

## Versioning

Per-family version = `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>` (D-3seg). UpstreamMajor.Minor anchored to `manifest.library_manifests[].vcpkg_version` and enforced by guardrail G54.

Current upstream pins:

| Library | vcpkg Pin | Upstream | Notes |
| --- | --- | --- | --- |
| SDL2 | 2.32.10 | 2.32.10 | Current |
| SDL2_image | 2.8.8#1 | 2.8.10 | Upstream ahead of vcpkg |
| SDL2_mixer | 2.8.1#1 | 2.8.1 | Current |
| SDL2_ttf | 2.24.0 | 2.24.0 | Current |
| SDL2_gfx | 1.0.4#11 | 1.0.4 | Frozen (3rd party) |
| SDL2_net | 2.2.0#3 | 2.2.0 | Bind pending ([#58](https://github.com/janset2d/sdl2-cs-bindings/issues/58)) |

SDL3 vcpkg availability: SDL3 3.4.4, SDL3_image 3.4.2, SDL3_mixer 3.2.0#1, SDL3_ttf 3.2.2#1. SDL3_net not yet in vcpkg (upstream WIP). SDL3_shadercross 3.0.0-preview2 (preview).

## Cross-Reference

- **Operating rules**: [`AGENTS.md`](../AGENTS.md), [`docs/onboarding.md`](onboarding.md)
- **Phase docs**: [phases/](phases/) — Phase 2 active ledger + Phase 4/5 design briefs
- **Architecture decisions**: [decisions/](decisions/) — ADR-001 (D-3seg + package-first) accepted; ADR-002 (target-centric build-host) accepted
- **Guardrails**: [knowledge-base/release-guardrails.md](knowledge-base/release-guardrails.md)
- **How-to recipes**: [playbook/](playbook/)
- **Design rationale**: [research/](research/)
- **Deferred ideas**: [parking-lot.md](parking-lot.md)
