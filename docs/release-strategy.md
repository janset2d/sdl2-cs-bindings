# Release Strategy — Janset.SDL2 / Janset.SDL3

Date: 2026-11-06

> Strategic anchor for how this project ships. AGENTS.md, plan.md, phase docs, ADRs, and research notes all point here for shape-level decisions (what counts as "v1.0", when do we go public, how do we label prereleases). Tactical roadmap lives in [`ROADMAP.md`](ROADMAP.md); detailed phase scope lives in [`phases/`](phases/).

## Mission Recap

Provide the .NET ecosystem with production-quality, modular SDL2 + SDL3 bindings bundled with **cross-platform native libraries built from source via vcpkg** — distributed as NuGet packages with no manual native sourcing required from consumers. No other project in the ecosystem does all four.

For project framing, glossary, and non-goals, read [`onboarding.md`](onboarding.md). This document covers strategy: ambition, end state, sequencing, versioning, labeling, promotion, maintenance.

## Strategic Stance

- **Pride-driven, not deadline-driven.** Hobby project, but the v1.0 launch is a "fame and glory" milestone — quality bar over delivery date. Don't ship until the maintainer is willing to put their name on it publicly.
- **Full SDL coverage at v1.0.** Stable carries SDL2 + SDL3 families with all in-scope satellites. Partial coverage is preview territory.
- **AST-generated bindings.** Manually maintained P/Invoke imports do not scale to SDL3 (no upstream `SDL3-CS` covering the satellite set we ship) and are unsustainable across 11K+ lines of bindings. The binding auto-generation workstream (Phase 4 per [`phases/phase-4-binding-autogen.md`](phases/phase-4-binding-autogen.md), with [ABI policy](architecture/binding-abi-policy.md), [generation mechanism](architecture/binding-generation.md), [maintenance](engineering/binding-generator-maintenance.md), [testing](engineering/binding-testing.md) and [output review](engineering/binding-output-review.md)) is **critical path for v1.0** — not a post-launch nice-to-have. Active implementation: **ClangSharp + Roslyn postprocess** under [`../spikes/binding-generators/clangsharp/`](../spikes/binding-generators/clangsharp/). The sunset host generator is removed; tested salvage stays dormant at its existing paths pending future production integration. ADR-004 is Reopened pending formal amendment after Layer 2 closure. The AST-first sequencing rationale below is independent of the implementation toolchain.
- **Maintainer accepts the post-launch treadmill.** Once stable ships, SDL upstream version bumps drive regenerate-and-release-wave cycles. NativeSmoke + ConsumerSmoke matrices catch mechanical regressions; semantic / API-drift regressions need occasional manual play-test passes.

## End State at v1.0

Stable big-bang launch shape:

| Surface | At v1.0 |
| --- | --- |
| **SDL2 family** | Core + Image + Mixer + Ttf + Gfx + Net (binding pending per [#58](https://github.com/janset2d/sdl2-cs-bindings/issues/58)) — all AST-generated, all 7 RIDs |
| **SDL3 family** | Core + Image + Mixer + Ttf — all AST-generated, all 7 RIDs. SDL3_net deferred (upstream WIP); SDL3_shadercross deferred (preview-only upstream) |
| **Bindings source** | AST-generated from C headers; `external/sdl2-cs` submodule retired |
| **Native source** | vcpkg-built hybrid-static across all 7 RIDs (Windows x64/x86/ARM64, Linux x64/ARM64, macOS x64/ARM64) |
| **Distribution** | nuget.org stable (no internal-feed reliance for end consumers); sustained release cadence aligned with upstream SDL version bumps |

## Sequencing — AST-First Path to Big Bang

The roadmap **reorders the phase sequence in [`ROADMAP.md`](ROADMAP.md)**: AST landing (Phase 4) lands **before** first public prerelease (Phase 3 ship). Rationale below.

| Stage | Phase mapping | Focus | Public-ship state |
| --- | --- | --- | --- |
| **Stage 0 — Current** | Phase 2b tail | CI/CD hardening; PD-7 / PD-8 prerequisites; pipeline scope-assumption gaps (#2 / #3 / #4) | Internal feed only |
| **Stage 1 — SDL2.Core generated-core readiness** | Phase 4 (start) | SDL2.Core AST-generated in production shape; Linux-canonical parsing; manifest-driven `SDL2.SDL` identity; internal raw ABI + public typed low-level + friendly overloads; constants/enums/structs/fixed arrays/callbacks/critical functions sufficient to remove the Core dependency on `external/sdl2-cs/src/SDL2.cs`. `SDL_syswm.h` typed-union layout remains Stage 2. | Internal feed only |
| **Stage 2 — SDL2 family AST sweep** | Phase 4 (close) + Phase 3 | `SDL_syswm.h` typed-union layout with forward-declaration stub library; all SDL2 satellites AST-generated; remaining `external/sdl2-cs` production use retired; Pack-stage symbol-existence validator; samples + meta-package; **first `-preview.N` on nuget.org**. Closes PD-7 prerequisites. | nuget.org `-preview.N` |
| **Stage 3 — SDL3 extension** | Phase 5 | SDL3 family added; AST generator extended for SDL3 headers via a sibling SDL3 generation target; SDL3-specific ABI rules (1-byte bool wire types, `SDL_IOStream` replacing `SDL_RWops`); SDL3 prereleases ship alongside SDL2. **Gated on PD-7 completion** — SDL3 vcpkg port + overlay triplet + transitive dependency closure work is its own substantial scope and must not block SDL2 v1.0 stable. | nuget.org `-preview.N` for SDL2 + SDL3 |
| **Stage 4 — Stabilization** | 2027 Stabilization | Real-consumer feedback intake; deferred-decision revisits; v1.0 launch wave preparation | nuget.org `-rc.N` near launch |
| **Stage 5 — v1.0 Big Bang** | Stable launch | Stable cut of all in-scope families. Blog post, documentation site, sample apps. SDL2 + SDL3 brand launch event. | nuget.org stable |

### Why AST-First, Not Public-Prerelease-First

The current [`ROADMAP.md`](ROADMAP.md) phase ordering (Phase 3 first prerelease → Phase 4 AST replacement later) would publish preview packages built on `external/sdl2-cs` — a deprecated POC import marked in [`AGENTS.md`](../AGENTS.md) as "untrusted for production testing." That has two costs:

1. **API churn for preview consumers.** Public API surface is the most expensive thing to break. AST output is *the* API definition for the bindings package; rev-bumping the entire surface in v2 after preview consumers adopt v1-preview creates avoidable migration pain.
2. **SDL3 cannot ship without AST anyway.** No upstream `SDL3-CS` covers the satellite set we ship. If v1.0 includes SDL3, the AST generator is critical path; deferring it past Phase 3 ship only pushes the same critical-path work into a worse calendar position.

AST-first means: first public `-preview.N` wave carries AST-generated bindings, not sdl2-cs imports. Net cost: AST work happens earlier in the timeline. Net benefit: every public-shipped package targets the eventual stable API surface from day one.

### Effort Calibration

Estimates calibrated against [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL) as reference implementation (~1000 lines, 6 partial generator files, mature working result):

| Stage | Focused-work effort | Calendar (hobby cadence) |
| --- | --- | --- |
| Stage 1 — SDL2.Core generated-core readiness | 1–2 weeks | 1–2 months |
| Stage 2 — SDL2 sweep | 2–3 weeks | 2–3 months |
| Stage 3 — SDL3 extension | 2–3 weeks | 2–3 months |
| Stage 4 — Stabilization | 2–4 weeks spread + calendar wait for feedback | 3–6 months |
| Stage 5 — Big Bang | 1–2 weeks launch prep | 1 month |
| **Total focused work** | **~8–14 weeks** | **~10–17 months calendar** at typical hobby cadence |

Cadence variability dominates. Worst case ~2 years calendar; best case ~12 months with focused sprints.

## Versioning Policy

Versioning, labels, promotion gates and maintenance are owned by [packaging and release](architecture/packaging-and-release.md).

## Deferred Decisions

Strategic decisions intentionally not addressed before v1.0. Each carries explicit unpark triggers.

### Package Topology Refactor (3-tier role-meta + `.Bindings` + `.Native` split)

Research complete, parked. Evidence: internal research, 2026-05-12, "Package Topology Refactor Research - DEFERRED".

**Unpark trigger conditions** (any one suffices):

1. ≥3 GitHub issues request bindings-only / native-only consumption paths from real consumers.
2. learning-sdl2 or another active downstream surfaces friction with the current 2-package shape.
3. SDL3 launch planning concludes new topology should land at SDL3 introduction.
4. v1.0 launch planning concludes topology refactor should ship as part of the launch wave.

**Salvageable carve-outs already extracted from the research:**

- Cross-family lower-bound bug in `DependencyRangeNormalizer.cs:110` — to be fixed as a standalone Phase 2b hardening item, ~1–2h effort.
- `PackageFamilyVersionSet` (already lives at `build/_build/Data/Versions/`) is the canonical plumbing for that fix.

### Ultimate `Janset.SDL2` / `Janset.SDL3` Convenience Metapackages

Deferred past v1.0 launch wave. Implementing them requires a release-train versioning policy decision (core-anchored? train-anchored? prerelease-only?) better made after at least one full release-train cycle has been rehearsed in practice.

If v1.0 ships without ultimate metapackages, onboarding documentation must compensate with a clear "what should I install" quickstart guide.

### Public NuGet Dwell Times

Currently: ≥2 weeks per wave. Open to revision if real-consumer feedback signals different needs during Stage 4.

## Cross-Reference

- [`../AGENTS.md`](../AGENTS.md) — operating rules, reserved actions
- [`ROADMAP.md`](ROADMAP.md) — tactical roadmap, current status, hardening backlog
- [`onboarding.md`](onboarding.md) — project framing, settled strategic decisions, glossary, non-goals
- [`phases/phase-4-binding-autogen.md`](phases/phase-4-binding-autogen.md) — AST generator design brief
- [`phases/phase-5-sdl3-support.md`](phases/phase-5-sdl3-support.md) — SDL3 monorepo + bindings design brief
- [ABI policy](architecture/binding-abi-policy.md), [generation mechanism](architecture/binding-generation.md), [maintenance](engineering/binding-generator-maintenance.md), [testing](engineering/binding-testing.md) and [output review](engineering/binding-output-review.md) — durable binding canon
- [`../spikes/binding-generators/docs/canonical/`](../spikes/binding-generators/docs/canonical/) — spike roadmap, satellites and references
- [`adr/0001-d3seg-and-package-first.md`](adr/0001-d3seg-and-package-first.md) — ADR-001 D-3seg + package-first consumer contract
- [`adr/0004-binding-autogen-toolchain.md`](adr/0004-binding-autogen-toolchain.md) — ADR-004 binding-generator toolchain decision (Reopened 2026-05-23 — see spike)
- [`../spikes/binding-generators/`](../spikes/binding-generators/) — Active toolchain re-evaluation spike
- Deferred topology evidence: internal research, 2026-05-12, "Package Topology Refactor Research - DEFERRED".
- Deferred supply-chain work is tracked in issue #66; validator output shapes in issue #128; recovery procedures in issue #119.
