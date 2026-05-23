# Phase 4: Binding Auto-Generation

**Status:** Toolchain selection under re-evaluation in [`../../spikes/binding-generators/`](../../spikes/binding-generators/) (ADR-004 Reopened 2026-05-23). Earlier work produced an SDL2.Core internal ABI-shaped preview via the sunset Cake-hosted CppAst implementation. The spike's ClangSharp + Roslyn postprocess prototype currently runs in parallel and is near-ABI-compatible (raw visibility ✓, SDL.h required surface ✓, multi-TFM compile ✓, Priority C semantic-ABI gap in progress). Public typed wrappers, friendly overloads, production flip, and smoke gates remain regardless of toolchain selection. **Critical path for v1.0** per [`../release-strategy.md`](../release-strategy.md) — lands **before** the first public `-preview.N` wave.

> **Toolchain re-evaluation (2026-05-23):** This brief was written against the CppAst + Cake-hosted `build/_build/Targets/GenerateBindings/` implementation. That implementation is in sunset pending the spike. Stage 1/2/3 *scope* below remains valid; *toolchain and home references* (CppAst version trio, `tools.cs generate-bindings` invocation, Cake build host) describe the sunset implementation and will be rewritten when the spike concludes.

**Order:** Phase 4 ships before Phase 3. First public preview consumes AST-generated bindings, not the deprecated `external/sdl2-cs` imports. See [`../release-strategy.md`](../release-strategy.md) §Sequencing for the rationale.

## Canonical Docs

This page is a thin pointer to the canonical Phase 4 documents. Do not duplicate strategy, architecture, or implementation detail here — it drifts.

| Doc | Purpose |
| --- | --- |
| [`../binding-autogen/binding-generator-constitution.md`](../binding-autogen/binding-generator-constitution.md) | Canonical ABI/API/translation constitution: internal raw ABI, public typed low-level API, friendly overloads, manifest config vs code-owned policy, evidence gates. |
| [`../binding-autogen/binding-generator-roadmap.md`](../binding-autogen/binding-generator-roadmap.md) | Canonical future roadmap: SDL2.Core public-surface readiness, SDL2 satellite sweep, SDL3 extension. |
| [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | ADR-004 toolchain decision (Reopened 2026-05-23 — see spike). |
| [`../../spikes/binding-generators/`](../../spikes/binding-generators/) | Active toolchain re-evaluation spike. Comparison evidence under `output/reports/`. |

## What Phase 4 Delivers

An auto-generated binding surface for SDL2 (core + all in-scope satellites), produced by a build-host-integrated generator, committing `src/SDL2.<Family>/Generated/*.g.cs` source consumed by the existing managed-family csprojs. SDL3 work follows in Phase 5, gated on PD-7 (SDL2 real-public-release). The specific toolchain (ClangSharp + Roslyn postprocess vs single-pass CppAst emitter) is being selected in [`../../spikes/binding-generators/`](../../spikes/binding-generators/).

The big shape (toolchain-neutral contract — implementation details below describe the sunset Cake-hosted CppAst implementation):

- **Generator home:** Build-host-integrated, not a standalone product project. Cross-cutting validators reachable from PreFlight and Pack stages. The sunset implementation lived at `build/_build/Targets/GenerateBindings/` + `build/_build/Validation/BindingGeneration/`; the successor implementation will inherit equivalent integration responsibilities. Pure emitter code stays Cake-free; the Cake-aware shell owns orchestration.
- **Toolchain (sunset Cake impl):** CppAst 0.24.0 + libclang.runtime.linux-x64 + libClangSharp.runtime.linux-x64. Successor toolchain TBD by the spike (ClangSharp 20.x + Microsoft.CodeAnalysis postprocess is one candidate; an Alimer-style single-pass CppAst is the other).
- **Local invocation (sunset Cake impl):** `tools.cs generate-bindings` orchestrates a pinned `linux-builder` Docker container. Successor implementation must remain reachable through `tools.cs` so day-to-day developers do not handle generator orchestration directly; Linux-canonical parsing is required unless the spike proves an equivalent alternative.
- **Parse strategy:** preprocessor-macro switching across an ~8-entry platform catalog (Neutral + Windows desktop + WinRT + GDK + Linux + macOS + iOS + Android). No `--target` cross-compile flag, no mingw-w64, no Apple SDK on Linux. The ClangSharp spike currently uses minimal Windows-local platform shims for spike-only iteration (`endian.h`, `AvailabilityMacros.h`, `TargetConditionals.h`) — production evidence still requires native Linux/macOS generation.
- **Output shape:** internal raw ABI externs with dual `[LibraryImport]` (net7+) / `[DllImport]` (legacy), manifest-driven namespace/class identity, public typed low-level handles/enums/structs/callbacks, canonical UTF-8 span string-like macro constants, public friendly overloads (`string` / `ReadOnlySpan<byte>` / `Span<T>` / `out` / `ref`), `[SupportedOSPlatform]` attribution. Public raw `IntPtr` externs are not part of v1 preview; SDL2-CS compatibility is best-effort.
- **Coherence guardrails:** `.generated-stamp` per family with PreFlight drift validator; Pack-stage symbol-existence validator at Stage 2.

## Stage Sequencing

Per the binding-generator roadmap and [`../release-strategy.md`](../release-strategy.md) §Sequencing:

| Stage | Scope | Public-ship state |
| --- | --- | --- |
| **Stage 1** | SDL2.Core generated-core readiness on the selected toolchain: production-shaped core output with full platform-conditioned function attribution plus constants, enums, structs/unions where verified, fixed arrays, callbacks, and critical function coverage. `SDL_GetWindowWMInfo` emitted as function with opaque `SDL_SysWMinfo*` (typed union deferred to Stage 2). | Internal feed only |
| **Stage 2** | `SDL_SysWMinfo`/`SDL_SysWMmsg` typed-union layout with ~15–20-type forward-declaration stub library; SDL2 satellite sweep (Image, Mixer, Ttf, Gfx, Net); remaining `external/sdl2-cs` production-use retirement; Pack-stage symbol-existence validator. | First public `-preview.N` on nuget.org |
| **Stage 3** | SDL3 extension (gated on PD-7). Sibling SDL3 generator target on the same selected toolchain; SDL3-specific ABI rules (1-byte bool wire types, `SDL_IOStream` replacing `SDL_RWops`). | Captured under Phase 5. |

## Exit Criteria — Defer To Roadmap

Detailed exit criteria per stage live in [`../binding-autogen/binding-generator-roadmap.md`](../binding-autogen/binding-generator-roadmap.md). The Phase 4 brief intentionally does not duplicate them.

## Cross-Reference

- [`../release-strategy.md`](../release-strategy.md) — strategic anchor (AST-first sequencing rationale)
- [`../plan.md`](../plan.md) — tactical roadmap (Phase 4 row)
- [`../binding-autogen/README.md`](../binding-autogen/README.md) — workstream index and reading order
- [`../binding-autogen/binding-generator-constitution.md`](../binding-autogen/binding-generator-constitution.md) — binding-generator constitution
- [`../binding-autogen/binding-generator-roadmap.md`](../binding-autogen/binding-generator-roadmap.md) — active roadmap
- [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 toolchain decision (Reopened 2026-05-23)
- [`../../spikes/binding-generators/`](../../spikes/binding-generators/) — Active toolchain re-evaluation spike
- [`../decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build-host pattern (the Cake-host fold inherits this)
- [`../decisions/2026-05-12-build-host-data-layer.md`](../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer (the binding validators extend this)
- [`../phases/phase-5-sdl3-support.md`](phase-5-sdl3-support.md) — Phase 5 SDL3 brief (gated on PD-7)
