# Release Strategy — Janset.SDL2 / Janset.SDL3

> Strategic anchor for how this project ships. AGENTS.md, plan.md, phase docs, ADRs, and research notes all point here for shape-level decisions (what counts as "v1.0", when do we go public, how do we label prereleases). Tactical roadmap lives in [`plan.md`](plan.md); detailed phase scope lives in [`phases/`](phases/).

## Mission Recap

Provide the .NET ecosystem with production-quality, modular SDL2 + SDL3 bindings bundled with **cross-platform native libraries built from source via vcpkg** — distributed as NuGet packages with no manual native sourcing required from consumers. No other project in the ecosystem does all four.

For project framing, glossary, and non-goals, read [`onboarding.md`](onboarding.md). This document covers strategy: ambition, end state, sequencing, versioning, labeling, promotion, maintenance.

## Strategic Stance

- **Pride-driven, not deadline-driven.** Hobby project, but the v1.0 launch is a "fame and glory" milestone — quality bar over delivery date. Don't ship until the maintainer is willing to put their name on it publicly.
- **Full SDL coverage at v1.0.** Stable carries SDL2 + SDL3 families with all in-scope satellites. Partial coverage is preview territory.
- **AST-generated bindings.** Manually maintained P/Invoke imports do not scale to SDL3 (no upstream `SDL3-CS` covering the satellite set we ship) and are unsustainable across 11K+ lines of bindings. The binding auto-generation workstream (Phase 4 per [`phases/phase-4-binding-autogen.md`](phases/phase-4-binding-autogen.md) and [`binding-autogen/`](binding-autogen/)) is **critical path for v1.0** — not a post-launch nice-to-have. **Toolchain selection is under re-evaluation** in [`../spikes/binding-generators/`](../spikes/binding-generators/) (ADR-004 Reopened 2026-05-23): ClangSharp + Roslyn postprocess vs Alimer-style single-pass CppAst. Either path replaces the sunset Cake-hosted CppAst implementation under `build/_build/Targets/GenerateBindings/`. The AST-first sequencing rationale below is independent of which toolchain ships.
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

The roadmap **reorders the phase sequence in [`plan.md`](plan.md)**: AST landing (Phase 4) lands **before** first public prerelease (Phase 3 ship). Rationale below.

| Stage | Phase mapping | Focus | Public-ship state |
| --- | --- | --- | --- |
| **Stage 0 — Current** | Phase 2b tail | CI/CD hardening; PD-7 / PD-8 prerequisites; pipeline scope-assumption gaps (#2 / #3 / #4) | Internal feed only |
| **Stage 1 — SDL2.Core generated-core readiness** | Phase 4 (start) | SDL2.Core AST-generated in production shape via the toolchain selected by the spike; Linux-canonical parsing; manifest-driven `SDL2.SDL` identity; internal raw ABI + public typed low-level + friendly overloads; constants/enums/structs/fixed arrays/callbacks/critical functions sufficient to remove the Core dependency on `external/sdl2-cs/src/SDL2.cs`. `SDL_syswm.h` typed-union layout remains Stage 2. | Internal feed only |
| **Stage 2 — SDL2 family AST sweep** | Phase 4 (close) + Phase 3 | `SDL_syswm.h` typed-union layout with forward-declaration stub library; all SDL2 satellites AST-generated; remaining `external/sdl2-cs` production use retired; Pack-stage symbol-existence validator; samples + meta-package; **first `-preview.N` on nuget.org**. Closes PD-7 prerequisites. | nuget.org `-preview.N` |
| **Stage 3 — SDL3 extension** | Phase 5 | SDL3 family added; AST generator extended for SDL3 headers via a sibling SDL3 generation target on the selected toolchain; SDL3-specific ABI rules (1-byte bool wire types, `SDL_IOStream` replacing `SDL_RWops`); SDL3 prereleases ship alongside SDL2. **Gated on PD-7 completion** — SDL3 vcpkg port + overlay triplet + transitive dependency closure work is its own substantial scope and must not block SDL2 v1.0 stable. | nuget.org `-preview.N` for SDL2 + SDL3 |
| **Stage 4 — Stabilization** | 2027 Stabilization | Real-consumer feedback intake; deferred-decision revisits; v1.0 launch wave preparation | nuget.org `-rc.N` near launch |
| **Stage 5 — v1.0 Big Bang** | Stable launch | Stable cut of all in-scope families. Blog post, documentation site, sample apps. SDL2 + SDL3 brand launch event. | nuget.org stable |

### Why AST-First, Not Public-Prerelease-First

The current [`plan.md`](plan.md) phase ordering (Phase 3 first prerelease → Phase 4 AST replacement later) would publish preview packages built on `external/sdl2-cs` — a deprecated POC import marked in [`AGENTS.md`](../AGENTS.md) as "untrusted for production testing." That has two costs:

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

Package versions follow **D-3seg** ([`decisions/2026-05-05-d3seg-and-package-first.md`](decisions/2026-05-05-d3seg-and-package-first.md), ADR-001): `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`, with V1 family-lock (managed + native of the same family share a single version per release wave).

**Brand-level "v1.0" is a launch moment, not a package version string.** SkiaSharp pattern: every SkiaSharp package is `2.88.x`-style D-3seg-style; the "v1.0 launch" was a project-identity announcement, not a package version reset. Same applies here. At v1.0 launch:

- `Janset.SDL2.Core 2.32.0` (or whatever D-3seg resolves to at the launch commit) is the stable package version.
- "Janset SDL Bindings v1.0" is the **brand launch event** — captured in blog post + documentation site + GitHub release notes, not in any package version string.

## NuGet Labeling Convention

| Stage | NuGet labels | Feed |
| --- | --- | --- |
| Stage 0–1 (internal-only, AST not yet stable) | No public label; internal-feed builds use `-local.<timestamp>` / `-ci.<run-id>.<attempt>` | GitHub Packages internal feed |
| Stage 2–3 (AST shipped, public preview) | `-preview.N` (incrementing N per public wave) | nuget.org |
| Stage 4 (stabilization, near launch) | `-rc.N` (incrementing N) | nuget.org |
| Stage 5 (v1.0 launch and onward) | Stable (no prerelease suffix) | nuget.org |

**Skip `-alpha`.** The internal feed already carries the "we don't trust this for outside use" semantic. Once anything ships publicly, the message should be "preview, expected to work, edges may still move" — not "alpha, expect breakage." Aligns with the pride-driven framing.

## Promotion Gates

Each feed-to-feed promotion has explicit entry criteria. No silent promotion based on time-elapsed alone.

| Promotion | Entry criteria |
| --- | --- |
| **Internal feed → nuget.org `-preview.N`** | (a) AST-generated bindings cover the SDL family being promoted; (b) learning-sdl2 (or equivalent real downstream) runs against the wave without breakage; (c) full 7-RID matrix green; (d) NativeSmoke + ConsumerSmoke all-green for ≥1 internal-feed wave |
| **`-preview.N` → `-rc.N`** | (a) All in-scope SDL families have published at least one `-preview.N` wave; (b) no `-preview` consumer-reported defect open for ≥2 weeks; (c) AST regeneration against an upstream version bump validated at least once |
| **`-rc.N` → stable v1.0** | (a) Brand launch artifacts ready (blog post, docs site, sample projects); (b) `-rc` wave validated against learning-sdl2's production scenario; (c) maintainer explicit commit to post-launch maintenance cadence |

**Minimum dwell times.** Each public feed surface stays alive ≥2 weeks per wave so real-consumer drift can surface. Stable doesn't "rush past" any wave count without consumer-side evidence.

## Maintenance Commitment Post-v1.0

Best-effort hobby cadence. Specifically:

- **AST regeneration on upstream bumps.** When SDL2.32.x → 2.33.0 or SDL3.4.x → 3.5.0 lands in vcpkg, regenerate AST output + release wave (`<NewUpstreamMinor>.0` D-3seg). Aim: ≤4 weeks from upstream stable release to Janset release. No formal SLA.
- **Bug response.** GitHub issues triaged best-effort. No formal SLA. CRITICAL P/Invoke / runtime payload bugs prioritized; cosmetic / nice-to-have at maintainer discretion.
- **Smoke matrix as the primary confidence layer.** NativeSmoke + ConsumerSmoke catch mechanical regressions (P/Invoke signature drift, missing runtime payload, `DllNotFoundException` paths). Semantic regressions (API still compiles, behavior changed) need occasional manual play-test passes — frequency at maintainer discretion.
- **No commitment to backports.** Latest stable is the supported version. If a consumer pins to `2.32.x` and SDL upstream ships `2.34.x`, the Janset package follows upstream — no `2.32.x` backport branch.

## Deferred Decisions

Strategic decisions intentionally not addressed before v1.0. Each carries explicit unpark triggers.

### Package Topology Refactor (3-tier role-meta + `.Bindings` + `.Native` split)

Research complete, parked. Full analysis lives in [`parking-lot/package-topology/`](parking-lot/package-topology/).

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

- [`../AGENTS.md`](../AGENTS.md) — operating rules, approval gate, settled strategic decisions
- [`plan.md`](plan.md) — tactical roadmap, current status, hardening backlog
- [`onboarding.md`](onboarding.md) — project framing, glossary, non-goals
- [`phases/phase-4-binding-autogen.md`](phases/phase-4-binding-autogen.md) — AST generator design brief
- [`phases/phase-5-sdl3-support.md`](phases/phase-5-sdl3-support.md) — SDL3 monorepo + bindings design brief
- [`binding-autogen/`](binding-autogen/) - binding generator workstream index, constitution, and roadmap
- [`decisions/2026-05-05-d3seg-and-package-first.md`](decisions/2026-05-05-d3seg-and-package-first.md) — ADR-001 D-3seg + package-first consumer contract
- [`decisions/2026-05-14-binding-autogen-toolchain.md`](decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 binding-generator toolchain decision (Reopened 2026-05-23 — see spike)
- [`../spikes/binding-generators/`](../spikes/binding-generators/) — Active toolchain re-evaluation spike
- [`parking-lot/package-topology/`](parking-lot/package-topology/) — deferred topology research
- [`parking-lot.md`](parking-lot.md) — other deferred ideas
