# Phase 4: Binding Auto-Generation

**Status:** Strategy brief + architecture design spec + Stage 1 implementation plan all accepted 2026-05-15. **Critical path for v1.0** per [`../release-strategy.md`](../release-strategy.md) — lands **before** the first public `-preview.N` wave.

**Order:** Phase 4 ships before Phase 3. First public preview consumes AST-generated bindings, not the deprecated `external/sdl2-cs` imports. See [`../release-strategy.md`](../release-strategy.md) §Sequencing for the rationale.

## Canonical Docs

This page is a thin pointer to the canonical Phase 4 documents. Do not duplicate strategy, architecture, or implementation detail here — it drifts.

| Doc | Purpose |
| --- | --- |
| [`../binding-autogen/binding-autogen-strategy-brief.md`](../binding-autogen/binding-autogen-strategy-brief.md) | Accepted WHY/HOW/WHAT strategy brief (revised 2026-05-15). Authoritative for toolchain, generator host, parsing strategy, plan shape, open decisions. |
| [`../superpowers/specs/2026-05-14-binding-generator-architecture-design.md`](../superpowers/specs/2026-05-14-binding-generator-architecture-design.md) | Architecture design spec. Component layout under `build/_build/Targets/GenerateBindings/`, platform catalog tuple model, validation strategy. |
| [`../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`](../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md) | Stage 1 implementation plan. Task-by-task TDD-style scaffolding. |
| [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | ADR-004 toolchain decision (CppAst). |
| [`../binding-autogen/research/binding-autogen-onboarding.md`](../binding-autogen/research/binding-autogen-onboarding.md) | LLM/contributor onboarding (revised 2026-05-15). |

## What Phase 4 Delivers

A CppAst-based auto-generated binding surface for SDL2 (core + all in-scope satellites), hosted inside the Cake build host, producing committed `src/SDL2.<Family>/Generated/*.g.cs` source consumed by the existing managed-family csprojs. SDL3 work follows in Phase 5, gated on PD-7 (SDL2 real-public-release).

The big shape:

- **Generator home:** `build/_build/Targets/GenerateBindings/` (Cake target) + `build/_build/Validation/BindingGeneration/` (PreFlight + Pack-stage validators). Pure emitter code stays Cake-free; the Cake-aware shell owns orchestration.
- **Toolchain:** CppAst 0.24.0 + libclang.runtime.linux-x64 + libClangSharp.runtime.linux-x64 (Linux-canonical version trio). Non-Linux runtime variants intentionally absent; generator fails closed on non-`linux-x64` hosts.
- **Local invocation:** `tools.cs generate-bindings` orchestrates the pinned `linux-builder` Docker container; Docker is a hard prerequisite, no host-OS fallback.
- **Parse strategy:** preprocessor-macro switching only across an ~8-entry `(OsCondition, BackendCondition[])` `PlatformCatalog` (Neutral + Windows desktop + WinRT + GDK + Linux + macOS + iOS + Android). No `--target` cross-compile flag, no mingw-w64, no Apple SDK.
- **Output shape:** dual `[LibraryImport]` (net7+) / `[DllImport]` (legacy) emit per function, typed `readonly partial struct` opaque handles, friendly overloads (`string` / `ReadOnlySpan<byte>` / `Span<T>` / `out` / `ref`), `[SupportedOSPlatform]` attribution.
- **Coherence guardrails:** `.generated-stamp` per family with PreFlight drift validator; Pack-stage symbol-existence validator at Stage 2.

## Stage Sequencing

Per the strategy brief §Plan Shape (and [`../release-strategy.md`](../release-strategy.md) §Sequencing — note the strategy brief subdivides the release-strategy "AST-first" stages further into per-execution-stage scope):

| Stage | Scope | Public-ship state |
| --- | --- | --- |
| **Stage 1** | SDL2.Core proof-of-life with full platform-conditioned function attribution. `SDL_GetWindowWMInfo` emitted as function with opaque `SDL_SysWMinfo*` (typed union deferred to Stage 2). | Internal feed only |
| **Stage 2** | `SDL_SysWMinfo`/`SDL_SysWMmsg` typed-union layout with ~15–20-type forward-declaration stub library; SDL2 satellite sweep (Image, Mixer, Ttf, Gfx, Net); `external/sdl2-cs` retirement; Pack-stage symbol-existence validator. | First public `-preview.N` on nuget.org |
| **Stage 3** | SDL3 extension (gated on PD-7). Sibling `GenerateSdl3Bindings` Cake target; SDL3-specific ABI rules (1-byte bool wire types, `SDL_IOStream` replacing `SDL_RWops`). | Captured under Phase 5. |

## Exit Criteria — Defer to Strategy Brief

Detailed exit criteria per stage live in the strategy brief §Plan Shape. The Phase 4 brief intentionally does not duplicate them — the strategy brief is canonical and revising criteria in two places creates drift. Read [`../binding-autogen/binding-autogen-strategy-brief.md`](../binding-autogen/binding-autogen-strategy-brief.md) §Plan Shape for the per-stage criteria.

## Cross-Reference

- [`../release-strategy.md`](../release-strategy.md) — strategic anchor (AST-first sequencing rationale)
- [`../plan.md`](../plan.md) — tactical roadmap (Phase 4 row)
- [`../binding-autogen/README.md`](../binding-autogen/README.md) — workstream index and reading order
- [`../binding-autogen/binding-autogen-strategy-brief.md`](../binding-autogen/binding-autogen-strategy-brief.md) — accepted strategy brief
- [`../binding-autogen/research/binding-autogen-onboarding.md`](../binding-autogen/research/binding-autogen-onboarding.md) — onboarding for contributors picking up this workstream
- [`../binding-autogen/research/binding-autogen-feasibility.md`](../binding-autogen/research/binding-autogen-feasibility.md) — feasibility study (pre-2026-05-15; see strategy brief Decision Audit for retractions)
- [`../binding-autogen/research/binding-autogen-spike-findings.md`](../binding-autogen/research/binding-autogen-spike-findings.md) — hands-on spike validation
- [`../binding-autogen/research/binding-autogen-approaches.md`](../binding-autogen/research/binding-autogen-approaches.md) — toolchain survey + ppy/Alimer/Silk.NET comparison
- [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 CppAst toolchain decision
- [`../decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build-host pattern (the Cake-host fold inherits this)
- [`../decisions/2026-05-12-build-host-data-layer.md`](../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer (the binding validators extend this)
- [`../phases/phase-5-sdl3-support.md`](phase-5-sdl3-support.md) — Phase 5 SDL3 brief (gated on PD-7)
