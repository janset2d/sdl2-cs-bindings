# Binding Auto-Generation Workstream

**Status (2026-05-15):** active binding-generator workstream. Strategy brief, architecture design spec, and Stage 1 implementation plan are all accepted and aligned. CppAst remains the selected toolchain (ADR-004); the generator is hosted inside the Cake build host (Cake-host fold, 2026-05-15 revision); generation runs Linux-canonical from a pinned Docker container; `SDL_syswm.h` typed-union layout is deferred to Stage 2; SDL3 binding generation is gated on PD-7. ClangSharp remains the documented migration path if CppAst's maintenance trade-off changes.

**Local loop (precursor, Stage 1 Task 3.5):** `dotnet run --file tools.cs -- generate-bindings` runs the Cake `GenerateBindings` target inside the pinned `linux-builder` derived container, producing spike-style preview output under `artifacts/generated-bindings-preview/sdl2-core/` (gitignored). Production-location flag-flip to `src/SDL2.<Family>/Generated/` lands at Stage 1 Task 7. Design + plan: [`../superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`](../superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md) + [`../superpowers/plans/2026-05-15-binding-generator-local-output-loop.md`](../superpowers/plans/2026-05-15-binding-generator-local-output-loop.md).

This folder owns the binding auto-generation knowledge base for Phase 4. The root keeps the workstream index and the active strategy brief; historical research and spike evidence live under `research/`. The companion design spec and Stage 1 implementation plan live under `../superpowers/specs/` and `../superpowers/plans/` per the project's plan-doc lifecycle convention — they retire when Stage 1 implementation ships.

## Folder Layout

| Path | Purpose |
| --- | --- |
| [`README.md`](README.md) | Workstream index and reading order. |
| [`binding-autogen-strategy-brief.md`](binding-autogen-strategy-brief.md) | Active WHY/HOW/WHAT strategy brief for the CppAst binding-generator direction (revised 2026-05-15). |
| [`research/`](research/) | Research evidence, feasibility analysis, spike findings, and onboarding context that support the strategy brief. |
| [`../superpowers/specs/2026-05-14-binding-generator-architecture-design.md`](../superpowers/specs/2026-05-14-binding-generator-architecture-design.md) | Architecture design spec (revised 2026-05-15) — temporary, retires when Stage 1 ships. |
| [`../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`](../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md) | Stage 1 implementation plan (revised 2026-05-15) — temporary, retires when Stage 1 ships. |
| [`../superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md`](../superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md) | Stage 1 Task 3.5 design spec for the local-output-loop precursor slice — temporary, retires when the slice ships. |
| [`../superpowers/plans/2026-05-15-binding-generator-local-output-loop.md`](../superpowers/plans/2026-05-15-binding-generator-local-output-loop.md) | Stage 1 Task 3.5 implementation plan for the local-output-loop slice — temporary, retires when the slice ships. |
| [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) | In-progress maintenance playbook for platform macro catalogs, generated stamps, and overlay coupling. |

## Reading Order

| # | Document | Purpose |
| --- | --- | --- |
| 1 | [`binding-autogen-strategy-brief.md`](binding-autogen-strategy-brief.md) | Current strategy brief, decision hypothesis, and 2026-05-15 revisions (Cake-host fold, Linux-canonical, stub speculation retraction, SysWM Stage 2 deferral, SDL3 PD-7 gating). |
| 2 | [`research/binding-autogen-onboarding.md`](research/binding-autogen-onboarding.md) | Fast onboarding for a fresh contributor or agent picking up this workstream. |
| 3 | [`../superpowers/specs/2026-05-14-binding-generator-architecture-design.md`](../superpowers/specs/2026-05-14-binding-generator-architecture-design.md) | Architecture design spec — Cake-host component layout, platform catalog tuple model, validation strategy. |
| 4 | [`../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`](../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md) | Stage 1 implementation plan — task-by-task TDD-style scaffolding under `build/_build/Targets/GenerateBindings/`. |
| 5 | [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) | Active maintenance procedure for macro catalogs, stamps, upstream bumps, and hybrid-static overlay coupling. |
| 6 | [`research/binding-autogen-approaches.md`](research/binding-autogen-approaches.md) | Toolchain survey and comparison across CppAst, ClangSharp, ppy/SDL3-CS, Alimer, SkiaSharp, and Silk.NET patterns. |
| 7 | [`research/binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) | Feasibility study: emit rules, platform-conditioned parsing, validation layers, open decisions. |
| 8 | [`research/binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) | Hands-on SDL2_gfx spike findings for ClangSharp and CppAst. |

Research docs (6–8) carry pre-2026-05-15 context and have not been retro-edited — the strategy brief's Decision Audit records the corrections that supersede claims in those docs (notably the mingw-w64 / Apple SDK stub speculation, retracted Error 4 row).

## Current Decision Posture

- CppAst is the selected Phase 4 planning direction, recorded in the strategy brief and [ADR-004](../decisions/2026-05-14-binding-autogen-toolchain.md).
- **Generator lives inside the Cake build host** under `build/_build/Targets/GenerateBindings/` with cross-cutting validators under `build/_build/Validation/BindingGeneration/`. No standalone `src/`-tree console app. Pure emitter code stays Cake-free; the Cake-aware shell owns orchestration.
- **Linux-canonical** generation. Only `libclang.runtime.linux-x64` + `libClangSharp.runtime.linux-x64` are pinned. The `GenerateBindings` Cake target fails closed on non-`linux-x64` hosts. Local invocation routes through `tools.cs generate-bindings`, which orchestrates the pinned `linux-builder` Docker container — Docker is a hard prerequisite, no host-OS fallback.
- **Preprocessor-macro switching only** for platform passes. No `--target` cross-compile flag, no mingw-w64, no Apple SDK headers. SDL's public headers carry the cross-platform opaque-type forward declarations the parser needs. Verified against ppy/SDL3-CS Dockerfile + `generate_bindings.py` (WebFetch 2026-05-15) and the local CppAst spike.
- **`SDL_syswm.h` typed-union layout is Stage 2.** Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `SDL_SysWMinfo*` parameter. The typed union with `[StructLayout(LayoutKind.Explicit, Size = 64)]` plus the small forward-declaration stub library (~15–20 types) lands in Stage 2.
- **SDL3 binding generation is gated on PD-7** (SDL2 real-public-release). The SDL3 vcpkg port + overlay triplet work is its own substantial scope and must not block SDL2 v1.0 stable.
- ClangSharp remains the documented migration path if CppAst's version-trio coupling or owned-emitter cost becomes painful in practice.
- ppy/SDL3-CS is the strongest SDL-specific reference for neutral + platform-specific passes. SkiaSharp is a strong CppAst discipline reference, but not a platform-split reference. Alimer is useful for C# shape ideas, but its single-pass union macro strategy is not sufficient for platform-conditioned headers/layout across our 7-RID correctness bar.

## Related Canonical Docs

- [`../phases/phase-4-binding-autogen.md`](../phases/phase-4-binding-autogen.md) — Phase 4 design brief.
- [`../release-strategy.md`](../release-strategy.md) — AST-first sequencing and v1.0 release strategy.
- [`../plan.md`](../plan.md) — roadmap and current phase status.
- [`../decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build-host pattern the Cake-host fold inherits.
- [`../decisions/2026-05-12-build-host-data-layer.md`](../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer pattern the binding validators extend.
- [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 binding-autogen toolchain.
- [`../knowledge-base/testing-guidelines.md`](../knowledge-base/testing-guidelines.md) — canonical TUnit/MTP test infrastructure for generator tests.
- [`../knowledge-base/extraction-guidelines.md`](../knowledge-base/extraction-guidelines.md) — collaborator extraction discipline.
- [`../../AGENTS.md`](../../AGENTS.md) — operating rules and settled project decisions.
