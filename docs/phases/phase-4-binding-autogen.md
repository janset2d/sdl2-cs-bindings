# Phase 4: Binding Auto-Generation

**Status:** Generated SDL2.Core internal ABI surface stabilized; public typed wrappers, friendly overloads, production flip, and smoke gates remain. **Critical path for v1.0** per [`../release-strategy.md`](../release-strategy.md) — lands **before** the first public `-preview.N` wave.

**Order:** Phase 4 ships before Phase 3. First public preview consumes AST-generated bindings, not the deprecated `external/sdl2-cs` imports. See [`../release-strategy.md`](../release-strategy.md) §Sequencing for the rationale.

## Canonical Docs

This page is a thin pointer to the canonical Phase 4 documents. Do not duplicate strategy, architecture, or implementation detail here — it drifts.

| Doc | Purpose |
| --- | --- |
| [`../binding-autogen/binding-generator-constitution.md`](../binding-autogen/binding-generator-constitution.md) | Canonical ABI/API/translation constitution: internal raw ABI, public typed low-level API, friendly overloads, manifest config vs code-owned policy, evidence gates. |
| [`../binding-autogen/binding-generator-roadmap.md`](../binding-autogen/binding-generator-roadmap.md) | Canonical future roadmap: SDL2.Core public-surface readiness, SDL2 satellite sweep, SDL3 extension. |
| [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | ADR-004 toolchain decision (CppAst). |

## What Phase 4 Delivers

A CppAst-based auto-generated binding surface for SDL2 (core + all in-scope satellites), hosted inside the Cake build host, producing committed `src/SDL2.<Family>/Generated/*.g.cs` source consumed by the existing managed-family csprojs. SDL3 work follows in Phase 5, gated on PD-7 (SDL2 real-public-release).

The big shape:

- **Generator home:** `build/_build/Targets/GenerateBindings/` (Cake target) + `build/_build/Validation/BindingGeneration/` (PreFlight + Pack-stage validators). Pure emitter code stays Cake-free; the Cake-aware shell owns orchestration.
- **Toolchain:** CppAst 0.24.0 + libclang.runtime.linux-x64 + libClangSharp.runtime.linux-x64 (Linux-canonical version trio). Non-Linux runtime variants intentionally absent; generator fails closed on non-`linux-x64` hosts.
- **Local invocation:** `tools.cs generate-bindings` orchestrates the pinned `linux-builder` Docker container; Docker is a hard prerequisite, no host-OS fallback.
- **Parse strategy:** preprocessor-macro switching only across an ~8-entry `(OsCondition, BackendCondition[])` `PlatformCatalog` (Neutral + Windows desktop + WinRT + GDK + Linux + macOS + iOS + Android). No `--target` cross-compile flag, no mingw-w64, no Apple SDK.
- **Output shape:** internal raw ABI externs with dual `[LibraryImport]` (net7+) / `[DllImport]` (legacy), manifest-driven namespace/class identity, public typed low-level handles/enums/structs/callbacks, canonical UTF-8 span string-like macro constants, public friendly overloads (`string` / `ReadOnlySpan<byte>` / `Span<T>` / `out` / `ref`), `[SupportedOSPlatform]` attribution. Public raw `IntPtr` externs are not part of v1 preview; SDL2-CS compatibility is best-effort.
- **Coherence guardrails:** `.generated-stamp` per family with PreFlight drift validator; Pack-stage symbol-existence validator at Stage 2.

## Stage Sequencing

Per the binding-generator roadmap and [`../release-strategy.md`](../release-strategy.md) §Sequencing:

| Stage | Scope | Public-ship state |
| --- | --- | --- |
| **Stage 1** | SDL2.Core generated-core readiness: production-shaped core output with full platform-conditioned function attribution plus constants, enums, structs/unions where verified, fixed arrays, callbacks, and critical function coverage. `SDL_GetWindowWMInfo` emitted as function with opaque `SDL_SysWMinfo*` (typed union deferred to Stage 2). | Internal feed only |
| **Stage 2** | `SDL_SysWMinfo`/`SDL_SysWMmsg` typed-union layout with ~15–20-type forward-declaration stub library; SDL2 satellite sweep (Image, Mixer, Ttf, Gfx, Net); remaining `external/sdl2-cs` production-use retirement; Pack-stage symbol-existence validator. | First public `-preview.N` on nuget.org |
| **Stage 3** | SDL3 extension (gated on PD-7). Sibling `GenerateSdl3Bindings` Cake target; SDL3-specific ABI rules (1-byte bool wire types, `SDL_IOStream` replacing `SDL_RWops`). | Captured under Phase 5. |

## Exit Criteria — Defer To Roadmap

Detailed exit criteria per stage live in [`../binding-autogen/binding-generator-roadmap.md`](../binding-autogen/binding-generator-roadmap.md). The Phase 4 brief intentionally does not duplicate them.

## Cross-Reference

- [`../release-strategy.md`](../release-strategy.md) — strategic anchor (AST-first sequencing rationale)
- [`../plan.md`](../plan.md) — tactical roadmap (Phase 4 row)
- [`../binding-autogen/README.md`](../binding-autogen/README.md) — workstream index and reading order
- [`../binding-autogen/binding-generator-constitution.md`](../binding-autogen/binding-generator-constitution.md) — binding-generator constitution
- [`../binding-autogen/binding-generator-roadmap.md`](../binding-autogen/binding-generator-roadmap.md) — active roadmap
- [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 CppAst toolchain decision
- [`../decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build-host pattern (the Cake-host fold inherits this)
- [`../decisions/2026-05-12-build-host-data-layer.md`](../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer (the binding validators extend this)
- [`../phases/phase-5-sdl3-support.md`](phase-5-sdl3-support.md) — Phase 5 SDL3 brief (gated on PD-7)
