# Binding Auto-Generation Workstream

**Status (2026-05-18):** active binding-generator workstream. Unified design spec + plan accepted and supersede the prior 2026-05-14 architecture spec + 2026-05-15 local-output-loop spec (now under `superpowers/specs/superseded/` and `superpowers/plans/superseded/`). CppAst remains the selected toolchain (ADR-004); the generator is hosted inside the Cake build host; generation runs Linux-canonical from a pinned Docker container; `SDL_syswm.h` typed-union layout is deferred to Stage 2; SDL3 binding generation is gated on PD-7. `build/manifest.json` is the per-family configuration center, including generated namespace/class identity; `GenerateBindingsTask` loops every family with `binding_generation.enabled=true` (`--family X` narrows). ClangSharp remains the documented migration path if CppAst's maintenance trade-off changes.

**Local loop (already shipped, commits `0db0e31` + `9a5f59e`):** `dotnet run --file tools.cs -- generate-bindings` runs the Cake `GenerateBindings` target inside the pinned `linux-builder` derived container, producing output under `artifacts/generated-bindings-preview/sdl2-core/` (gitignored). Production-location flag-flip to `src/SDL2.<Family>/Generated/` is a follow-up slice after the unified plan ships. Unified spec + plan: [`../superpowers/specs/2026-05-16-binding-generator-unified-design.md`](../superpowers/specs/2026-05-16-binding-generator-unified-design.md) + [`../superpowers/plans/2026-05-17-binding-generator-unified-plan.md`](../superpowers/plans/2026-05-17-binding-generator-unified-plan.md).

This folder owns the binding auto-generation knowledge base for Phase 4. The root keeps the workstream index and the active strategy brief; historical research and spike evidence live under `research/`. The unified design spec + plan live under `../superpowers/specs/2026-05-16-...` and `../superpowers/plans/2026-05-17-...`; their predecessors (the 2026-05-14 architecture spec, 2026-05-14 Stage 1 plan, 2026-05-15 local-output-loop spec + plan) moved to `../superpowers/specs/superseded/` and `../superpowers/plans/superseded/` once the unified docs absorbed them.

## Folder Layout

| Path | Purpose |
| --- | --- |
| [`README.md`](README.md) | Workstream index and reading order. |
| [`binding-autogen-strategy-brief.md`](binding-autogen-strategy-brief.md) | Active WHY/HOW/WHAT strategy brief for the CppAst binding-generator direction (revised through 2026-05-18). |
| [`binding-api-surface-strategy.md`](binding-api-surface-strategy.md) | Canonical API-surface decision: internal raw ABI, public typed low-level API, friendly overloads, peer matrix, string/span/handle/`SDL_bool` policy. |
| [`research/`](research/) | Research evidence, feasibility analysis, spike findings, and onboarding context that support the strategy brief. |
| [`../superpowers/specs/2026-05-16-binding-generator-unified-design.md`](../superpowers/specs/2026-05-16-binding-generator-unified-design.md) | **Active** unified design spec — manifest-driven per-family generator; supersedes the 2026-05-14 + 2026-05-15 specs. |
| [`../superpowers/plans/2026-05-17-binding-generator-unified-plan.md`](../superpowers/plans/2026-05-17-binding-generator-unified-plan.md) | **Active** unified implementation plan — Phases 1–3G; supersedes the Stage 1 plan + local-output-loop plan. |
| [`../superpowers/specs/superseded/`](../superpowers/specs/superseded/) | Historical specs preserved with banner notes: 2026-05-14 architecture design + 2026-05-15 local-output-loop design. |
| [`../superpowers/plans/superseded/`](../superpowers/plans/superseded/) | Historical plans preserved with banner notes: 2026-05-14 Stage 1 + 2026-05-15 local-output-loop. |
| [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) | In-progress maintenance playbook for platform macro catalogs, generated stamps, and overlay coupling. |

## Reading Order

| # | Document | Purpose |
| --- | --- | --- |
| 1 | [`binding-autogen-strategy-brief.md`](binding-autogen-strategy-brief.md) | Current strategy brief, decision hypothesis, and 2026-05-17/18 revisions (Cake-host fold, Linux-canonical, API-surface decision, manifest identity, constants/macros policy, unsafe/compile-check stabilization, SDL_bool correction, SysWM Stage 2 deferral, SDL3 PD-7 gating). |
| 2 | [`binding-api-surface-strategy.md`](binding-api-surface-strategy.md) | Canonical API surface: what is internal, what is public, why typed handles beat IntPtr, when string/span overloads apply, and which peer patterns are borrowed/rejected. |
| 3 | [`research/binding-autogen-onboarding.md`](research/binding-autogen-onboarding.md) | Fast onboarding for a fresh contributor or agent picking up this workstream. |
| 4 | [`../superpowers/specs/2026-05-16-binding-generator-unified-design.md`](../superpowers/specs/2026-05-16-binding-generator-unified-design.md) | Unified design spec — manifest-driven per-family generator, BindingModel + 6 categories, per-category emitters, validator wiring, cross-family type refs, output topology, API-surface anchors. |
| 5 | [`../superpowers/plans/2026-05-17-binding-generator-unified-plan.md`](../superpowers/plans/2026-05-17-binding-generator-unified-plan.md) | Unified implementation plan — Phases 1 (docs) / 2A–2D (manifest + infrastructure) / 3A–3G (Preview → real shape + emitters + dual-emit + smoke), with the 2026-05-17/18 stabilization queue inserted before structural emission. |
| 6 | [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) | Active maintenance procedure for macro catalogs, stamps, upstream bumps, and hybrid-static overlay coupling. |
| 7 | [`research/binding-autogen-approaches.md`](research/binding-autogen-approaches.md) | Toolchain survey and comparison across CppAst, ClangSharp, ppy/SDL3-CS, Alimer, SkiaSharp, and Silk.NET patterns. |
| 8 | [`research/binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) | Feasibility study: emit rules, platform-conditioned parsing, validation layers, open decisions. |
| 9 | [`research/binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) | Hands-on SDL2_gfx spike findings for ClangSharp and CppAst. |

Research docs (6–8) carry pre-2026-05-15 context and have not been retro-edited — the strategy brief's Decision Audit records the corrections that supersede claims in those docs (notably the mingw-w64 / Apple SDK stub speculation, retracted Error 4 row).

## Current Decision Posture

- CppAst is the selected Phase 4 planning direction, recorded in the strategy brief and [ADR-004](../decisions/2026-05-14-binding-autogen-toolchain.md).
- Public API shape is **internal raw ABI externs + public typed low-level API + friendly overloads** per [`binding-api-surface-strategy.md`](binding-api-surface-strategy.md). Public raw `IntPtr` externs are not part of v1 preview; typed handles expose native values as the escape hatch. SDL2-CS compatibility is best-effort: useful as an oracle, not the shape to freeze.
- Output class identity is family-based, manifest-driven, and not parse-view-based. SDL2 core currently uses namespace `SDL2`, public class `SDL`, and internal raw ABI class `SDLNative`; parse views produce files/attributes, not `Sdl2_Neutral` or `Sdl2_MacOS` classes.
- String-like SDL macro constants such as `SDL_HINT_*` use canonical `ReadOnlySpan<byte>` UTF-8 literal properties. String ergonomics is provided by method overloads; do not duplicate every macro as both `const string` and `ReadOnlySpan<byte>`.
- **Generator lives inside the Cake build host** under `build/_build/Targets/GenerateBindings/` with cross-cutting validators under `build/_build/Validation/BindingGeneration/`. No standalone `src/`-tree console app. Pure emitter code stays Cake-free; the Cake-aware shell owns orchestration.
- **Linux-canonical** generation. Only `libclang.runtime.linux-x64` + `libClangSharp.runtime.linux-x64` are pinned. The `GenerateBindings` Cake target fails closed on non-`linux-x64` hosts. Local invocation routes through `tools.cs generate-bindings`, which orchestrates the pinned `linux-builder` Docker container — Docker is a hard prerequisite, no host-OS fallback.
- **Preprocessor-macro switching only** for platform passes. No `--target` cross-compile flag, no mingw-w64, no Apple SDK headers. SDL's public headers carry the cross-platform opaque-type forward declarations the parser needs. Verified against ppy/SDL3-CS Dockerfile + `generate_bindings.py` (WebFetch 2026-05-15) and the local CppAst spike.
- **`SDL_syswm.h` typed-union layout is Stage 2.** Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `SDL_SysWMinfo*` parameter. The typed union with `[StructLayout(LayoutKind.Explicit, Size = 64)]` plus the small forward-declaration stub library (~15–20 types) lands in Stage 2.
- **SDL3 binding generation is gated on PD-7** (SDL2 real-public-release). The SDL3 vcpkg port + overlay triplet work is its own substantial scope and must not block SDL2 v1.0 stable.
- ClangSharp remains the documented migration path if CppAst's version-trio coupling or owned-emitter cost becomes painful in practice.
- ppy/SDL3-CS is the strongest SDL-specific reference for neutral + platform-specific passes. SkiaSharp is a strong CppAst discipline reference, but not a platform-split reference. Alimer is useful for C# shape ideas, but its single-pass union macro strategy is not sufficient for platform-conditioned headers/layout across our 7-RID correctness bar.
- Structural emission is generic AST-driven work. Name allowlists such as `Stage1StructNames` and `SDL_GameControllerButtonBind`-specific flattening are not canonical design; anonymous unions are translated from AST shape. `SDL_GUID` is explicitly mapped to `System.Guid` through substitution policy and is not emitted as a generated struct.

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
