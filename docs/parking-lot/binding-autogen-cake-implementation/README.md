# Parking Lot — Sunset Cake-Hosted CppAst Binding Generator Implementation

**Archived:** 2026-05-23
**Reason:** Toolchain re-evaluation under [`../../../spikes/binding-generators/`](../../../spikes/binding-generators/) ([ADR-004 Reopened](../../decisions/2026-05-14-binding-autogen-toolchain.md)). The binding-autogen toolchain decision is being re-evaluated; either successor toolchain (ClangSharp + Roslyn postprocess or Alimer-style single-pass CppAst) replaces the implementation these milestone plans were written against.

## What lived under `build/_build/Targets/GenerateBindings/`

The sunset implementation was a Cake-hosted CppAst pipeline with a ~30-file `ModelBuilding/` structure: parser/model/emitter separation, semantic type classification, source-first macro pipeline, multi-platform parse-view merge, dynapi name validation, and fixture-backed tests for SDL2.Core ABI blockers. It produced an SDL2.Core internal ABI-shaped preview at `artifacts/generated-bindings-preview/sdl2-core/` that compiled across the 5-TFM matrix.

Milestones M0 (doc consolidation) and M2 (topology refactor) were completed against that implementation. Milestone M3 (profile boundary) was planned but paused before implementation when the toolchain re-evaluation opened. Milestone M1 (safety harness) was largely absorbed by the spike's own evidence (per-header generation report, oracle comparison, multi-TFM compile-check, dynapi coherence).

## Archived plans

| Plan | Status when archived | Use |
| --- | --- | --- |
| [`milestone-1-safety-harness-baseline.md`](milestone-1-safety-harness-baseline.md) | Completed against sunset Cake impl | Architectural-intent reference for the safety-harness pattern any successor implementation needs (snapshot tests, fixture coverage, generated-preview checkpointing). The path/test-tree specifics describe the sunset Cake-impl test layout. |
| [`milestone-2-behavior-preserving-topology-refactor.md`](milestone-2-behavior-preserving-topology-refactor.md) | Completed against sunset Cake impl | Architectural-intent reference for separating parser/model/emitter concerns in a CppAst-shaped pipeline. The folder layout and named collaborators describe the sunset Cake-impl topology. |
| [`milestone-3-profile-boundary.md`](milestone-3-profile-boundary.md) | Paused before implementation | Architectural-intent reference for the manifest-config-vs-code-owned-policy boundary, family profile shape (`sdl2-core`, `sdl2-satellite`, `sdl2-gfx`, `sdl3-core`, `sdl3-satellite`), and the engine/policy separation. The CppAst-specific implementation steps are no longer canonical. |

## Why preserved rather than deleted

- `git log --follow` and `git blame -C` continuity for downstream auditing.
- The toolchain-neutral *intent* in each plan (safety harness pattern, parser/model/emitter separation, profile/manifest boundary) survives and informs the successor implementation; only the toolchain mechanics change.
- The roadmap's M4–M10 stages reference these plans for context; preserving them avoids breaking those cross-references.

## What is *not* archived here

- The constitution (`docs/binding-autogen/binding-generator-constitution.md`) — remains canonical policy regardless of toolchain.
- The roadmap (`docs/binding-autogen/binding-generator-roadmap.md`) — remains canonical at the milestone-summary level; detailed CppAst+Cake references inside are sunset and will be rewritten when the spike concludes.
- The testing strategy (`docs/binding-autogen/testing-strategy.md`) — toolchain-neutral testing layer model remains canonical.
- The playbooks (`docs/playbook/binding-generator-maintenance.md`, `docs/playbook/binding-output-oracle-validation.md`) — operational procedures for the sunset Cake impl, kept under `docs/playbook/` with sunset banners until successor playbooks land.

## Unparking

If the spike concludes by selecting the CppAst path *and* preserves substantial elements of the sunset Cake implementation, individual plans here may be unparked back to `docs/binding-autogen/milestones/` with updated implementation specifics. More likely outcome: successor implementation lands with fresh milestone plans, and these stay archived as historical reference.
