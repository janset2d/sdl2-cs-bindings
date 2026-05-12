# Project Plan — Janset.SDL2 / Janset.SDL3

**Maintainer:** Deniz Irgin (@denizirgin)

> Canonical roadmap. Forward-looking only — historical narrative lives in code + git history. When code and this file disagree, verify against code.

## Mission

Provide the .NET ecosystem with production-quality, modular SDL2 and SDL3 bindings that include **cross-platform native libraries built from source** — something no other project offers.

## Current Phase

Current status:

**Phase 2: CI/CD & Packaging — IN PROGRESS.** Core surface is landed (`release.yml` + Cake build host + `tools.cs`). Phase 2b tail = nuget.org promotion (PD-7), release-recovery playbook (PD-8), and the four scope-assumption gaps surfaced in 2026-05-01 rehearsals.

**Phase X / build-host refactor — CLOSED 2026-05-10.** The settled build-host architecture now lives in [`ADR-002`](decisions/2026-05-05-target-centric-build-host.md) and [`ADR-003`](decisions/2026-05-12-build-host-data-layer.md). Durable day-to-day rules live in [`knowledge-base/testing-guidelines.md`](knowledge-base/testing-guidelines.md) and [`knowledge-base/extraction-guidelines.md`](knowledge-base/extraction-guidelines.md). Migration-era notes were retired after test-infrastructure consolidation.

Active execution ledger: [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) (Phase 2b).

## Roadmap

> **Phase sequencing reordered per [`release-strategy.md`](release-strategy.md):** Phase 4 (CppAst binding generator) is **critical path for v1.0** and lands **before** the first public `-preview.N` wave (Phase 2b PD-7). Strategic rationale + stages + promotion gates live in [`release-strategy.md`](release-strategy.md); this file remains the tactical roadmap.

### Phase 2b — Public release tail

Detail in [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) (PDs, gaps, candidate directions).

- [ ] **PD-7 — Public NuGet promotion**: `PublishPublicTask` real implementation + nuget.org Trusted Publishing OIDC + first prerelease publication ([#63](https://github.com/janset2d/sdl2-cs-bindings/issues/63))
- [ ] **PD-8 — Release recovery playbook**: `playbook/release-recovery.md` operator runbook + `Pack-Family` / `Smoke-Family` / `Push-Family` Cake helpers (or explicit deferral)
- [ ] **PD-3 — `dotnet-affected` integration**: NuGet library vs CLI wrapper ADR after the spike
- [ ] **Pipeline scope-assumption gaps** (rehearsal 2026-05-01; full surface in adaptation plan):
  - [ ] Gap #2 — G58 feed-probe: cross-family resolvability against the publish target feed
  - [ ] Gap #3 — ConsumerSmoke partial-scope: per-family smoke csprojs candidate direction
  - [ ] Gap #4 — Trigger mechanism rework: manual `workflow_dispatch` or GitHub Releases as canonical trigger
- [ ] Generalize Cake `Pack` stage to all 6 satellites × 7 RIDs (post-ADR-002 target-centric layout)
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

Closed 2026-05-10. The build host now follows [`ADR-002`](decisions/2026-05-05-target-centric-build-host.md) and [`ADR-003`](decisions/2026-05-12-build-host-data-layer.md); the remaining work is post-refactor hardening, not architecture migration.

**Canonical docs:** [`decisions/2026-05-05-target-centric-build-host.md`](decisions/2026-05-05-target-centric-build-host.md), [`decisions/2026-05-12-build-host-data-layer.md`](decisions/2026-05-12-build-host-data-layer.md), [`knowledge-base/testing-guidelines.md`](knowledge-base/testing-guidelines.md), [`knowledge-base/extraction-guidelines.md`](knowledge-base/extraction-guidelines.md).

**Post-refactor hardening:** tracked in [`post-refactor-cleanup-plan.md`](post-refactor-cleanup-plan.md) — correctness gaps, style sweep, deferred extractions, test debt, UX polish. When all checkboxes resolve there, that file retires and any survivors fold back here.

### 2027 — Stabilization

Big-bang v1.0 launch shape + entry criteria per [`release-strategy.md`](release-strategy.md) §End State at v1.0 + §Promotion Gates.

- [ ] Stabilize SDL2 + SDL3 packages (v1.0) — full satellite coverage per release-strategy end-state table
- [ ] Community feedback incorporation
- [ ] Begin Janset2D development on top of these bindings

## Hardening Backlog

Open issues that are not phase-bound:

- [ ] [#65 Harden package validation and local feed workflows](https://github.com/janset2d/sdl2-cs-bindings/issues/65)
- [ ] [#66 Harden supply-chain and release integrity workflows](https://github.com/janset2d/sdl2-cs-bindings/issues/66)
- [ ] [#67 Implement external native override support](https://github.com/janset2d/sdl2-cs-bindings/issues/67) (parking lot)
- [ ] [#81 Add drift-prevention guardrail for harvest library lists](https://github.com/janset2d/sdl2-cs-bindings/issues/81)
- [ ] [#87 Reassess HarvestTask extraction boundaries after the pipeline retirement](https://github.com/janset2d/sdl2-cs-bindings/issues/87)

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

- **Release strategy**: [`release-strategy.md`](release-strategy.md) — strategic anchor (end state, sequencing, labeling, promotion, maintenance)
- **Operating rules**: [`AGENTS.md`](../AGENTS.md), [`docs/onboarding.md`](onboarding.md)
- **Phase docs**: [phases/](phases/) — Phase 2 active ledger + Phase 4/5 design briefs
- **Architecture decisions**: [decisions/](decisions/) — ADR-001 (D-3seg + package-first), ADR-002 (target-centric build-host), ADR-003 (contract-centric data layer)
- **Guardrails**: [knowledge-base/release-guardrails.md](knowledge-base/release-guardrails.md)
- **How-to recipes**: [playbook/](playbook/)
- **Design rationale**: [research/](research/)
- **Deferred ideas**: [parking-lot.md](parking-lot.md), [parking-lot/](parking-lot/)
