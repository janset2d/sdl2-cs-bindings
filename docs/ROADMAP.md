# Project Plan — Janset.SDL2 / Janset.SDL3

Date: 2026-11-06

**Maintainer:** Deniz Irgin (@denizirgin)

> Canonical roadmap. Forward-looking only — historical narrative lives in code + git history. When code and this file disagree, verify against code.

## Mission

Provide the .NET ecosystem with production-quality, modular SDL2 and SDL3 bindings that include **cross-platform native libraries built from source** — something no other project offers.

## Current Phase

Current status:

**Phase 2: CI/CD & Packaging — IN PROGRESS.** Core surface is landed (`release.yml` + Cake build host + `tools.cs`). Phase 2b tail = nuget.org promotion (PD-7), release-recovery playbook (PD-8), and the four scope-assumption gaps surfaced in 2026-05-01 rehearsals.

**Phase X / build-host refactor — CLOSED 2026-05-10.** The settled build-host architecture now lives in [`ADR-002`](adr/0002-target-centric-build-host.md) and [`ADR-003`](adr/0003-build-host-data-layer.md). Durable day-to-day rules live in [`engineering/testing-style.md`](engineering/testing-style.md) and [`engineering/coding-style.md`](engineering/coding-style.md). Migration-era notes were retired after test-infrastructure consolidation.

Active execution ledger: [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) (Phase 2b).

## Known gaps

Public NuGet.org promotion (`PublishPublic`) is a stub; staging publishes to GitHub Packages only.

## Roadmap

> **Phase sequencing reordered per [`release-strategy.md`](release-strategy.md):** Phase 4 binding auto-generation is **critical path for v1.0** and lands **before** the first public `-preview.N` wave (Phase 2b PD-7). The active implementation is ClangSharp + Roslyn postprocess in [`../spikes/binding-generators/`](../spikes/binding-generators/); the sunset host generator and alternative scaffold are removed, with tested salvage dormant at its existing paths pending production integration. Strategic rationale + stages + promotion gates live in [`release-strategy.md`](release-strategy.md); this file remains the tactical roadmap.

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
Binding generator constitution: [architecture/binding-abi-policy.md](architecture/binding-abi-policy.md) (canonical ABI/API/translation contract and manifest config-vs-policy rules).
Binding generator roadmap: [../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md](../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md) (layer-based forward milestones: Layer 2 typed public API, Layer 3 friendly overloads, Production Flip, SysWM Layout + Satellite Sweep Close, Smoke Expansion, SDL3 Extension).
Binding generator implementation notes: [architecture/binding-generation.md](architecture/binding-generation.md) (mechanism + classification labels + hardcoding rules).
Binding generator maintenance: [engineering/binding-generator-maintenance.md](engineering/binding-generator-maintenance.md) (ClangSharp + Roslyn postprocess maintenance playbook).

**Status (2026-05-28):** SDL2 Layer 1 raw ABI closed across Core / Image / GFX / TTF / Mixer on the active ClangSharp + Roslyn postprocess implementation at `spikes/binding-generators/clangsharp/`. Layer 2 typed public API is next. Multi-TFM compile clean (5 TFMs); five-family oracle report clean across Priority C categories. Toolchain selection effectively decided pending formal ADR-004 amendment after Layer 2 closure. `SDL_syswm.h` typed-union layout remains under the SysWM Layout milestone. SDL3 binding generation gated on PD-7.

- [ ] **Stage 1 — SDL2.Core generated-core readiness**: Layer 1 raw ABI closed (current state); Layer 2 typed public API + production flip retiring `external/sdl2-cs/src/SDL2.cs` remain. ([#69](https://github.com/janset2d/sdl2-cs-bindings/issues/69))
- [ ] **Stage 2 — SDL_syswm full union + SDL2 family sweep close + sdl2-cs retire**: SysWM Layout typed-union layout + remaining `external/sdl2-cs` production-use retirement + Pack-stage symbol-existence validator + SDL2_net addition. Satellite Layer 1 already closed for Image / GFX / TTF / Mixer. ([#70](https://github.com/janset2d/sdl2-cs-bindings/issues/70))

Non-blocking polish candidate after the correctness gates: improve enum source-fidelity/readability where it is compile-safe, especially `SDL_KeyCode` members that SDL expresses as `SDL_Scancode | SDLK_SCANCODE_MASK` and small character-code literals such as `SDLK_ESCAPE`. Numeric enum values remain ABI-correct and acceptable as the fallback; this is not a committed Stage 1/2 task.

### Phase 5 — SDL3 Support

Design brief: [phases/phase-5-sdl3-support.md](phases/phase-5-sdl3-support.md).

**Gated on PD-7** (SDL2 real-public-release) per the binding-generator roadmap's SDL3 extension stage. The SDL3 vcpkg port + overlay triplet + transitive dependency closure work is its own substantial scope and must not block SDL2 v1.0 stable. Phase 5 plan starts after Phase 4 Stage 2 ships and PD-7 lands.

- [ ] Add SDL3 bindings and native packages to monorepo ([#71](https://github.com/janset2d/sdl2-cs-bindings/issues/71))
- [ ] Extend CI and packaging flow for SDL3 prereleases ([#72](https://github.com/janset2d/sdl2-cs-bindings/issues/72))

### Phase X — Build-Host Modernization

Closed 2026-05-10. The build host now follows [`ADR-002`](adr/0002-target-centric-build-host.md) and [`ADR-003`](adr/0003-build-host-data-layer.md); the remaining work is post-refactor hardening, not architecture migration.

**Canonical docs:** [`adr/0002-target-centric-build-host.md`](adr/0002-target-centric-build-host.md), [`adr/0003-build-host-data-layer.md`](adr/0003-build-host-data-layer.md), [`engineering/testing-style.md`](engineering/testing-style.md), [`engineering/coding-style.md`](engineering/coding-style.md).

**Post-refactor hardening:** tracked by the issue groups below. Completed cancellation plumbing needs no new issue.

| Cleanup group | Tracking |
|---|---|
| Correctness gaps | issue #{{11-cleanup-correctness}} |
| UX and CLI clarity | issue #{{12-cleanup-cli}} |
| Style consistency | issue #{{13-cleanup-style}} |
| API and performance refinements | issue #{{14-cleanup-api-performance}} |
| Extractions and model boundaries | issue #{{15-cleanup-extractions}} |
| Reporter consolidation | issue #{{16-cleanup-reporters}} |
| Test discipline | issue #{{17-cleanup-tests}} |
| Conditional watch list | issue #{{18-cleanup-watch-list}} |

Detailed follow-up groups: consumer-smoke extraction — issue #{{01-extraction-package-smoke}};
harvest extraction — issue #87; harvest data and paths — issue #{{03-cross-target-relayering}};
readiness semantics — issue #{{04-harvest-readiness-gate-decision}};
dependency ranges — issue #{{05-dependency-range-semantics}};
reporter design — issue #{{06-reporter-cohort}}; fixture API — issue #{{07-fakecakeworld-fluent-api}};
diagnostic UX — issue #{{08-diagnostic-target-ux}}; test oracles — issue #{{09-test-discipline-cluster}};
deferred polish and triggers — issue #{{10-deferred-cleanup}}.
These groups overlap the cleanup table; cross-link them rather than implementing the same work twice.

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

Other deferred work is tracked in issues:

- Known-bad native combinations and expiry: issue #{{19-known-issues-skip-list}}.
- Release rollback and recovery: issue #{{20-release-recovery}}.
- Maintenance and feed health checks: issue #{{21-maintenance-health-checks}}.
- Invariant-culture logging: issue #{{22-invariant-culture-logging}}.
- Optional workflow-owned coverage signal: issue #{{23-optional-coverage-signal}}.
- Package-info and dependency-analysis caching: issue #{{24-performance-caching}}.
- Tool reproducibility: issue #{{25-tool-reproducibility}}.
- Release log hygiene: issue #{{26-release-log-hygiene}}.
- Native CI cache audit: issue #{{27-ci-cache-audit}}.
- Windows runner architecture and cache identity: issue #{{28-windows-runner-architecture}}.
- SBOM and native symbols: issue #66.
- Validator output-shape decision: issue #{{31-validator-shape-standardization}}.

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
- **Architecture decisions**: [adr/](adr/) — ADR-001 (D-3seg + package-first), ADR-002 (target-centric build-host), ADR-003 (contract-centric data layer)
- **Guardrails**: [architecture/packaging-and-release.md](architecture/packaging-and-release.md)
- **How-to recipes**: [playbook/](playbook/)
- **Design evidence** (non-normative):
  - internal research, 2026-04-11, "Research: Native Library Packaging Patterns in .NET".
  - internal research, 2026-04-13, "SDL2 Native Dependency License Inventory".
  - internal research, 2026-04-14, "LGPL-Free Codec Migration - SDL2_mixer".
  - internal research, 2026-04-14, "Symbol Visibility in Hybrid Static Packaging - Analysis & Strategy".
  - internal research, 2026-04-16, "Research: Full-Train Release Orchestration".
  - internal research, 2026-04-16, "Research: Release Recovery + Manual Escape Hatch".
  - internal research, 2026-04-11, "Research: SDL3 Ecosystem Analysis".
  - internal research, 2026-04-22, "Unix Native Identity Strategy - vcpkg, SDL2 Family, Overlay Ports, and the Single-File Question".
  - internal research, 2026-05-22, "Research: Semantic ABI Type Classification For SDL Bindings".
- **Deferred topology evidence**: internal research, 2026-05-12, "Package Topology Refactor Research - DEFERRED"; unpark triggers remain in [release strategy](release-strategy.md#deferred-decisions).
- **Deferred ideas**: the issue groups in Hardening Backlog above.
