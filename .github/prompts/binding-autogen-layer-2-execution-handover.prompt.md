---
name: "Binding Autogen Layer 2 — Execution Handover"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the Layer 2 design cycle (peer research → cross-family analysis → brainstorm/spec → implementation plan → dual expert review) closed on 2026-05-30. SDL2 Layer 1 raw ABI is closed; the Layer 2 typed-public-API DESIGN + a corrected Core-foundation implementation PLAN are ready and ecosystem-validated; implementation is PARKED (not started). All design docs are UNCOMMITTED working files on branch spike/binding-autogen-sdl2-gfx (HEAD still 1b32464). Recommended next: execute the Core L2 foundation plan via superpowers:subagent-driven-development."
argument-hint: "Optional focus area or reason to override the recommended next step (e.g. 'start executing Task 1', 're-validate review finding #N', 'commit the design docs first', 'begin satellite activation')"
agent: "agent"
model: "Claude Opus 4.8 (1M context)"
---

You are an engineer entering `janset2d/sdl2-cs-bindings` to **execute** the SDL2.Core Layer 2 typed-public-API foundation. The Layer 2 *design cycle* just closed (2026-05-30): peer research → cross-family analysis → brainstorm → design spec → implementation plan → two independent expert reviews, all integrated. **Most important state observation:** the design, the corrected foundation plan, and the dual-review outcomes are all written and recorded, but **no implementation has started and nothing is committed** — every artifact below is an uncommitted working file. Your job is to *execute the plan*, not redesign. The single resume anchor is the forward-scope ledger (Grounding #1).

## First Principle

> Treat every claim in this prompt as **current-as-of-authoring (`2026-05-30` — Layer 2 design cycle close)** and verify against the live repo, `git log`, and the canonical docs before acting. These artifacts are **uncommitted working files** — confirm they are still present and unchanged (`git status`, read the ledger) before relying on them; a later session may have committed, revised, or discarded them.

## What Just Happened

The Layer 2 design cycle (no commits — all working-tree docs):

| Artifact | Path | What it is |
| --- | --- | --- |
| Phase A peer mapping | `spikes/binding-generators/docs/canonical/references/2026-05-29-layer-2-peer-mapping.md` | ppy / Alimer / Silk / SDL2-CS mapped to our L1/L2/L3 under Litmus A. |
| Cross-family matrix | `spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-cross-family-requirements.md` | per-pass × per-family + the satellite-only exceptions a Core-first design forgets (library-agnostic invariants). |
| **Design spec (approved)** | `docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md` | D1–D10, Litmus A, Safe-Alternative Guarantee, `T?`-collapse, string axis, `SDL_bool`=typed-enum, function-like-macro #3, §10B family-blind invariants, §12 eight Constitution amendments. |
| **Core foundation plan** | `docs/superpowers/plans/2026-05-30-binding-autogen-layer-2-core-foundation.md` | the implementation plan you execute. TDD, family-blind, must-fixes integrated. |
| Forward-scope ledger | `spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-design-and-deferred-scope.md` | everything beyond the foundation plan (deferred slices, L3, amendments), each pointing to its authoritative source. **Read first.** |

**Brainstorm decisions ratified** (full text in the spec): three-layer contract (internal raw `SDLNative` / public typed `SDL` forwarders / friendly overloads); Litmus A seam (allocates-or-changes-shape → L3); `SDL_bool` stays the int-backed typed enum (no implicit bool); explicit-only handle conversions; `T?=null` collapse for optional struct pointers; separate `string` + `ReadOnlySpan<byte>` (no `Utf8String`); clong normalized to `long`/`ulong` at L2; function-like macros generated via roster + per-kind emitters (#3); committed postprocess, **not** a source generator.

**Dual expert review (2026-05-30)** — one unbiased (no design rationale, pure P/Invoke + modern-C# merit), one context-full (onboarded + peer-benchmarked vs CsWin32 / ClangSharp `PInvokeGenerator` / TerraFX / Vortice / csbindgen / rust-bindgen). Verdict **proceed fixes-first**; architecture + ratified decisions ecosystem-validated. The dual lens caught **two legacy-TFM compile blockers a single review missed** — `delegate*`-param Modern/Compat divergence (~25 methods) and `[SupportedOSPlatform]` unconditional emit — plus a clong `(int)`→Unix-truncation bug and a dropped-`public const` hole. **All must-fixes are integrated into the plan.**

## Onboarding Snapshot

Modular C# SDL2/SDL3 bindings + vcpkg-built natives across 7 RIDs, NuGet-distributed. The active workstream (Phase 4) auto-generates the bindings via **ClangSharp + Roslyn postprocess** under `spikes/binding-generators/clangsharp/` (ADR-004 Reopened). SDL2 **Layer 1 raw ABI is closed** across Core/Image/GFX/TTF/Mixer (multi-TFM clean: net10/9/8/netstandard2.0/net462). Layer 2 = the typed public API on top.

**Doc topology** (so you don't get lost): `spikes/binding-generators/docs/canonical/` is the durable policy home (constitution, roadmap, implementation-notes, maintenance, testing-strategy; `references/` holds the dated L2 design artifacts; `satellites/` holds per-family analyses). `docs/superpowers/specs/` + `plans/` hold this cycle's spec + plan (workflow artifacts — may be cleaned post-slice; the canonical ledger is the durable record). Settled project-wide decisions live in `AGENTS.md` — read it; don't re-derive.

## Current State You Should Assume Until Verified

- **HEAD**: `1b32464` — `docs(binding-autogen): pre-Layer-2 doc consolidation`. **No new commits from the design cycle.**
- **Branch**: `spike/binding-autogen-sdl2-gfx`, 1 commit ahead of origin (the consolidation; push pending Deniz approval).
- **Worktree**: uncommitted doc sweep — 4 new docs under `docs/superpowers/{specs,plans}/` + `canonical/references/`, the new ledger, and `spikes/binding-generators/docs/next-iteration-plan.md` modified. No code changed.
- **Layer 1**: closed; raw ABI committed under `spikes/binding-generators/clangsharp/src/Janset.SDL2.*/Generated/{Compat,Modern}/`.
- **Layer 2**: design + plan + dual-review **done and recorded**; implementation **NOT started (PARKED by decision 2026-05-30)**.
- **Postprocess pipeline**: `spikes/binding-generators/clangsharp/postprocess/` (Roslyn CLI, modes dispatched in `Program.cs`); orchestrated by `generate_bindings.py`; per-family facts in `config/family-config.json`.
- **ADR-004**: Reopened — formal toolchain amendment deferred to Layer 2 closure. The cycle's evidence reinforces the ClangSharp choice; do not amend ADR-004 yet.

## Recommended Next Step

1. **Execute the Core L2 foundation plan** *(multi-task arc; recommended)*. Pre-flight: read Grounding #1–#4, then verify the postprocess code + a sample of generated output match the plan's assumptions (`Program.cs` mode dispatch, `OpaqueHandleEmitRewriter` discover/build/emit, `Generated/Modern` vs `Compat` divergence on `SDL_events.g.cs`). Files: create `postprocess/PublicTypedMethodProjection.cs` + `StructLayoutSequentialRewriter.cs`; modify `Config/FamilyConfig.cs`, `config/family-config.json`, `Program.cs`, `PostProcessSelfTests.cs`, `generate_bindings.py`, `Janset.SDL2.Core.csproj`; create `tests/api-snapshot/`. Use `superpowers:subagent-driven-development` (fresh subagent per task, review between). Acceptance: `--self-test` PASS incl. the Modern+Compat **compile-check** fixture; multi-TFM build clean; public-API snapshot reviewed + no-raw-leak; twice-regen diff empty; slopwatch clean. The plan's must-fixes are integrated and the two compile blockers are caught by the compile-check fixture.
2. **Re-validate a specific review finding first** *(lightweight)* — if you doubt one of the integrated must-fixes (e.g. the `delegate*` uniform-`nint` shape, or the clong `(nint)` cast), confirm against the actual `Generated/{Modern,Compat}` output before executing.
3. **Commit the design docs as a checkpoint** *(optional)* — per the single-changeset discipline, with Deniz's explicit go, before starting implementation.

Talk to Deniz before committing to which one. Direct commits are acceptable in this repo; the Layer 2 implementation lands as **one revertable squash commit** (work branch + WIP checkpoints + `git reset --soft`), no worktrees.

## Mandatory Grounding (read in this order)

1. **`spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-design-and-deferred-scope.md`** — the forward-scope ledger; your map to everything.
2. `docs/superpowers/plans/2026-05-30-binding-autogen-layer-2-core-foundation.md` — the plan you execute (incl. its "Deferred review findings").
3. `docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md` — the design (Litmus A §3, §10B family-blind, §12 amendments).
4. `spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-cross-family-requirements.md` — per-family exceptions (why the rewriters are family-blind).
5. `AGENTS.md` (operating rules, approval gate, settled decisions) + `CLAUDE.md` (relay).
6. `spikes/binding-generators/docs/canonical/binding-generator-constitution.md` — §"Layer Contract", §"C long" (L262 range-check), §"Generator Home", §"Manifest Configuration Vs Code-Owned Policy", §"Generation Determinism Contract".
7. The postprocess code the plan extends: `postprocess/{Program.cs, OpaqueHandleEmitRewriter.cs, FlagsAttributeRewriter.cs, ClongDualDispatchRewriter.cs, PostProcessSelfTests.cs, Config/FamilyConfig.cs}` + `generate_bindings.py` (`run_postprocess` ≈ L796-833; postprocess loop ≈ L2024-2052).
8. `spikes/binding-generators/docs/next-iteration-plan.md` — Active Iteration (resume pointer) + Forward Backlog.
9. (When doing satellite activation / L3) the peer mapping + `canonical/satellites/` per-family analyses.

Skip anything not load-bearing for your focus; do not pad.

## Insights & Experience From the Design Cycle

The hard-won lessons — internalize these before touching code:

- **Library-agnostic before Core-first.** The single biggest lesson. Analyze *all* families up front and make the rewriters **family-blind + config-driven + no-op-tolerant**; never design for Core then retrofit satellites. The canonical trap is clong: Core's clong methods are RETURN-only, so a Core-first design forgets TTF's clong **INPUT-param** Windows range-check. The cross-family matrix exists precisely to surface these; the foundation self-tests **fixture the satellite-only cases before satellites are wired** (clong-INPUT, empty-config no-op, Discover-filter). Keep this discipline for every pass.
- **Dual-agent review earns its keep — run both lenses.** An *unbiased* reviewer (given the plan + code but NOT the design rationale) and a *context-full* reviewer (onboarded + peer-benchmarked) catch different things: the unbiased lens found the `delegate*`/Compat compile blocker + the clong `(int)`-truncation bug; the context-full lens found the `[SupportedOSPlatform]` legacy break and grounded it in the roadmap exit criterion. For any non-trivial spec/plan, dispatch both, and benchmark against **non-SDL** binding libs (CsWin32, ClangSharp `PInvokeGenerator`, TerraFX, Vortice, csbindgen, rust-bindgen), not just the SDL peers.
- **The committed-postprocess approach is locked — do not relitigate.** Unbiased/greenfield reviewers *will* suggest a Roslyn source generator (it is more idiomatic and deletes the determinism/idempotency/snapshot overhead). The answer is Constitution §"Generator Home": output must be committed `.g.cs` with a reproducibility stamp, and a consumer-build SG cannot be committed. Cite it and move on.
- **TFM divergence is broader than clong.** The Modern vs Compat raw trees diverge on three axes: clong (`CLong`/`CULong` vs normalized `long`/`ulong`), `delegate*`-params (`delegate* unmanaged[Cdecl]<…>` vs `IntPtr`, ~25 methods), and `[SupportedOSPlatform]` (a .NET 5+ attribute, `#if NET5_0_OR_GREATER`-guarded, absent on netstandard2.0/net462). Deniz's decision: **one TFM-shared public surface** with uniform signatures — handle divergences via `#if` bodies (clong, delegate*→uniform-`nint`-cast) and **verbatim copy of the raw member's attribute trivia** (which already carries the `#if` guard + all platform values + `[Obsolete]`). NOT a TFM-split. The **compile-check fixture** (compile the emitted public class against BOTH a Modern-shaped and a Compat-shaped stub raw class) is what catches these — `string.Contains` self-tests do not prove compilability.
- **clong correctness gotcha:** forward `new CLong((nint)p)` / `new CULong((nuint)p)`, **never `(int)p`** (the `(int)` narrowing truncates a valid 64-bit value on Unix x64, where `CLong.Value` is `nint`/64-bit). The Windows range-check (`int.MinValue..int.MaxValue`) belongs only on the Windows branch.
- **Mechanism patterns to mirror:** `PublicTypedMethodProjection` follows `OpaqueHandleEmitRewriter`'s **discover → build → emit** (static `Discover`/`BuildXContent` + a Program.cs early-return that writes one file), NOT an in-place `CSharpSyntaxRewriter`. The TDD harness is `PostProcessSelfTests.Run()` → add a `CheckXxx(failures)` (fixture string → run → assert), executed via `dotnet run --project postprocess -- --self-test`. Family facts come from `FamilyConfig` (config = *which*; code = *how* — never put ABI shape in JSON).
- **Work discipline (observed + enforced):** approval gate (no commit/feature-code without explicit `go / yap / apply / proceed / başla`; docs-only edits exempt for *editing* but committing still needs a presented summary + message); single-changeset (WIP commits → squash; no worktrees); docs-first; deferred work goes to canonical docs / backlog, never implied. The canonical ledger is the durable record; the `docs/superpowers/` spec+plan are workflow artifacts.

## Locked Policy Recap

Most-likely-to-be-tempting-to-violate in execution:

- **Approval gate** — present summary + proposed commit message; no commit without explicit go.
- **Single changeset via branch+squash** — Layer 2 lands as ONE revertable squash commit; no worktrees.
- **Internal raw ABI stays internal** (`SDLNative`); L2 = public `SDL` forwarders; **no public raw extern leak** (the snapshot no-leak gate enforces it; `[InternalsVisibleTo]` is for tests only).
- **Committed postprocess, NOT a Roslyn source generator** (Constitution §"Generator Home").
- **Single public surface + `#if`/trivia** (Deniz's call), not a TFM-split — revisit only if divergent method count balloons.
- **Generated, not magic companion** — macros generated / parser-marked; `[NativeTypeName]` dropped on the public surface, kept on raw.
- **No JSON ABI knobs** — `family-config.json` holds facts; code holds policy.
- **clong → `long`/`ulong` at L2** (never public `CLong`/`CULong` — absent on legacy TFMs); `(nint)` not `(int)`.
- **Satellites call Core's public L2** (no IVT to satellites); Core L2 lands before any satellite L2.
- **Family-blind rewriters + no-op tolerance + universal `[StructLayout(Sequential)]`.**
- **Multi-agent review at gates** (spec §13) — dual lens, peer-benchmarked.

## Final Steering Note

The hard thinking is done: the design is ratified, ecosystem-validated against the strongest non-SDL precedents, and the plan's two latent compile blockers are now caught by a self-test rather than discovered at the all-TFM build. Your task is execution discipline, not redesign — run the plan task-by-task with a fresh subagent each, let the compile-check fixture be your early-warning, and hold the family-blind line (every pass config-driven, no-op-tolerant, satellite cases fixtured before satellites exist). If a reviewer or your own instinct pulls toward a source generator or a TFM-split, re-read §"Generator Home" and Deniz's single-surface call before acting. The Layer 1 surface is stable, the seams are clean, and the path is short. Build it correctly the first time.
