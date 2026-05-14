# Binding Auto-Generation Workstream

**Status:** active binding-generator workstream. The strategy brief selects the CppAst path for Phase 4 planning, with ClangSharp preserved as the documented migration path if the maintenance trade-off changes.

This folder owns the binding auto-generation knowledge base for Phase 4. The root keeps the workstream index and the active strategy brief; historical research and spike evidence live under `research/`.

## Folder Layout

| Path | Purpose |
| --- | --- |
| [`README.md`](README.md) | Workstream index and reading order. |
| [`binding-autogen-strategy-brief.md`](binding-autogen-strategy-brief.md) | Active WHY/HOW/WHAT strategy brief for the CppAst binding-generator direction. |
| [`research/`](research/) | Research evidence, feasibility analysis, spike findings, and onboarding context that support the strategy brief. |

## Reading Order

| # | Document | Purpose |
| --- | --- | --- |
| 1 | [`binding-autogen-strategy-brief.md`](binding-autogen-strategy-brief.md) | Current strategy brief and decision hypothesis for Phase 4 planning. |
| 2 | [`research/binding-autogen-onboarding.md`](research/binding-autogen-onboarding.md) | Fast onboarding for a fresh contributor or agent picking up this workstream. |
| 3 | [`research/binding-autogen-approaches.md`](research/binding-autogen-approaches.md) | Toolchain survey and comparison across CppAst, ClangSharp, ppy/SDL3-CS, Alimer, SkiaSharp, and Silk.NET patterns. |
| 4 | [`research/binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) | Feasibility study: emit rules, platform-conditioned parsing, validation layers, open decisions. |
| 5 | [`research/binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) | Hands-on SDL2_gfx spike findings for ClangSharp and CppAst. |

## Current Decision Posture

- CppAst is the selected Phase 4 planning direction, recorded in the strategy brief and [ADR-004](../decisions/2026-05-14-binding-autogen-toolchain.md).
- ClangSharp remains the documented migration path if CppAst's maintenance burden outweighs the single-emitter benefits.
- Platform-conditioned header parsing is a tool-agnostic libclang/preprocessor concern. If a public SDL header exposes different declarations behind platform macros, the generator must model that with controlled platform passes regardless of whether the selected toolchain is CppAst or ClangSharp.
- ppy/SDL3-CS is the strongest SDL-specific reference for neutral + platform-specific passes. SkiaSharp is a strong CppAst discipline reference, but not a platform-split reference. Alimer is useful for C# shape ideas, but its single-pass union macro strategy is not sufficient for platform-conditioned headers/layout across our 7-RID correctness bar.

## Related Canonical Docs

- [`../phases/phase-4-binding-autogen.md`](../phases/phase-4-binding-autogen.md) — Phase 4 design brief.
- [`../release-strategy.md`](../release-strategy.md) — AST-first sequencing and v1.0 release strategy.
- [`../plan.md`](../plan.md) — roadmap and current phase status.
- [`../../AGENTS.md`](../../AGENTS.md) — operating rules and settled project decisions.
