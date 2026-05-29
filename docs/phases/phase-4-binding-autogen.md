# Phase 4: Binding Auto-Generation

**Status (2026-05-28):** SDL2 Layer 1 raw ABI closed on the active ClangSharp + Roslyn postprocess implementation under [`../../spikes/binding-generators/clangsharp/`](../../spikes/binding-generators/clangsharp/) across SDL2 Core / Image / GFX / TTF / Mixer. Layer 2 typed public API + Layer 3 friendly overloads + production flip remain. ADR-004 is **Reopened** with formal amendment deferred to post-Layer-2 closure. **Critical path for v1.0** per [`../release-strategy.md`](../release-strategy.md) — lands **before** the first public `-preview.N` wave.

**Order:** Phase 4 ships before Phase 3. First public preview consumes AST-generated bindings, not the deprecated `external/sdl2-cs` imports. See [`../release-strategy.md`](../release-strategy.md) §Sequencing for the rationale.

## Canonical Docs

This page is a thin pointer. Strategy, ABI/API policy, implementation mechanism, and forward milestones live in the canonical docs under [`../../spikes/binding-generators/docs/canonical/`](../../spikes/binding-generators/docs/canonical/). Do not duplicate them here — it drifts.

| Doc | Purpose |
| --- | --- |
| [`../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md`](../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md) | Canonical ABI/API policy. |
| [`../../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md`](../../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md) | Layer-based forward milestones. |
| [`../../spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md`](../../spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md) | Mechanism + classification labels. |
| [`../../spikes/binding-generators/docs/canonical/binding-generator-maintenance.md`](../../spikes/binding-generators/docs/canonical/binding-generator-maintenance.md) | ClangSharp + Roslyn maintenance playbook. |
| [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | ADR-004 (Reopened — amendment deferred). |
| [`../../spikes/binding-generators/`](../../spikes/binding-generators/) | Active spike. |

## Stage Sequencing

Scope-axis sequencing only. Implementation-axis sequencing (Layer 1 → Layer 2 → Layer 3 → Production Flip → SysWM Layout → Smoke Expansion → SDL3 Extension) lives in the canonical roadmap.

| Stage | Scope | Public-ship state |
| --- | --- | --- |
| **Stage 1** | SDL2.Core generated-core readiness; `SDL_GetWindowWMInfo` emitted with opaque `SDL_SysWMinfo*` (typed union deferred to Stage 2). | Internal feed only |
| **Stage 2** | SDL_syswm typed-union layout; SDL2 satellite sweep close (Image, Mixer, Ttf, Gfx — Layer 1 already closed — plus Net); `external/sdl2-cs` production-use retirement; Pack-stage symbol-existence validator. | First public `-preview.N` on nuget.org |
| **Stage 3** | SDL3 extension (gated on PD-7). Sibling SDL3 generation target; SDL3-specific ABI rules. | Captured under Phase 5. |

## Exit Criteria — Defer To Roadmap

Detailed exit criteria per stage live in [`../../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md`](../../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md).

## Cross-Reference

- [`../release-strategy.md`](../release-strategy.md) — strategic anchor (AST-first sequencing rationale)
- [`../plan.md`](../plan.md) — tactical roadmap (Phase 4 row)
- [`../../spikes/binding-generators/docs/canonical/`](../../spikes/binding-generators/docs/canonical/) — canonical binding-generator docs
- [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 toolchain decision (Reopened)
- [`../decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build-host pattern
- [`../decisions/2026-05-12-build-host-data-layer.md`](../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer
- [`../phases/phase-5-sdl3-support.md`](phase-5-sdl3-support.md) — Phase 5 SDL3 brief (gated on PD-7)
