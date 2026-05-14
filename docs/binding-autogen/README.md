# Binding Auto-Generation Workstream

**Status:** active research / spike-validation workstream. The final generator toolchain remains undecided until the WHY/HOW/WHAT design document is accepted.

This folder owns the binding auto-generation knowledge base for Phase 4. It intentionally sits outside `docs/research/` because the topic is now an active workstream with onboarding, feasibility analysis, spike findings, and future design notes rather than a one-off research artifact.

## Reading Order

| # | Document | Purpose |
| --- | --- | --- |
| 1 | [`binding-autogen-onboarding.md`](binding-autogen-onboarding.md) | Fast onboarding for a fresh contributor or agent picking up this workstream. |
| 2 | [`binding-autogen-approaches.md`](binding-autogen-approaches.md) | Toolchain survey and comparison across CppAst, ClangSharp, ppy/SDL3-CS, Alimer, SkiaSharp, and Silk.NET patterns. |
| 3 | [`binding-autogen-feasibility.md`](binding-autogen-feasibility.md) | Feasibility study: emit rules, platform-conditioned parsing, validation layers, open decisions. |
| 4 | [`binding-autogen-spike-findings.md`](binding-autogen-spike-findings.md) | Hands-on SDL2_gfx spike findings for ClangSharp and CppAst. |

## Current Decision Posture

- CppAst and ClangSharp are both spike-validated candidates.
- CppAst is the current maintainer lean, not a decision.
- Platform-conditioned header parsing is a tool-agnostic libclang/preprocessor concern. If a public SDL header exposes different declarations behind platform macros, the generator must model that with controlled platform passes regardless of whether the selected toolchain is CppAst or ClangSharp.
- ppy/SDL3-CS is the strongest SDL-specific reference for neutral + platform-specific passes. SkiaSharp is a strong CppAst discipline reference, but not a platform-split reference. Alimer is useful for C# shape ideas, but its single-pass union macro strategy is not sufficient for platform-conditioned headers/layout across our 7-RID correctness bar.

## Related Canonical Docs

- [`../phases/phase-4-binding-autogen.md`](../phases/phase-4-binding-autogen.md) — Phase 4 design brief.
- [`../release-strategy.md`](../release-strategy.md) — AST-first sequencing and v1.0 release strategy.
- [`../plan.md`](../plan.md) — roadmap and current phase status.
- [`../../AGENTS.md`](../../AGENTS.md) — operating rules and settled project decisions.
