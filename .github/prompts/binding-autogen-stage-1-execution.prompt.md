---
name: "Binding auto-generation Stage 1 execution"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings on branch spike/binding-autogen-sdl2-gfx after the binding-autogen doc wave landed (HEAD 1c0b6e6 on 2026-05-15). Strategy brief + architecture design spec + Stage 1 implementation plan + downstream docs are all committed and aligned. No code changes yet. Recommended next: execute Stage 1 Task 1 (scaffold CppAst packages into the Cake build host)."
argument-hint: "Optional override: skip directly to a specific Stage 1 task (Task 1-10), or pick a different area entirely. Defaults to starting at Task 1."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You're an engineer entering `janset2d/sdl2-cs-bindings` on branch `spike/binding-autogen-sdl2-gfx`. The previous two sessions revised the binding-autogen strategy brief, authored the architecture design spec + Stage 1 implementation plan, and then synced every downstream doc (README + onboarding + plan.md + phase-4 brief + release-strategy + research-doc retraction banners). Two commits landed: `c921e44` (brief + spec + plan revision) and `1c0b6e6` (downstream doc sync). **No code changes yet** — the generator-folded-into-Cake-host architecture is fully designed and documented but has not been built. Your job in this session is to begin executing the Stage 1 plan, starting at Task 1, under the approval gate.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-15 — post-doc-wave, no code work yet)** and verify against the live repo, `git log`, and canonical docs before acting. The strategy brief at `docs/binding-autogen/binding-autogen-strategy-brief.md` and the Stage 1 plan at `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` are the source of truth for what to build; the research docs in `docs/binding-autogen/research/` are frozen artifacts (see the retraction banners at their tops).

## What Just Happened

Two doc-only commits landed across two waves. No source code or build-host code was touched.

### Wave 1 — Strategy brief + design spec + Stage 1 plan revision (commit `c921e44`)

Folded the generator into the Cake build host (was a standalone `src/`-tree console app in the 2026-05-14 draft), locked Linux-canonical determinism contract, retracted mingw-w64 / Apple SDK stub speculation against external evidence, deferred `SDL_syswm.h` typed-union layout to Stage 2, gated SDL3 binding generation on PD-7.

| File | What changed |
|---|---|
| `docs/binding-autogen/binding-autogen-strategy-brief.md` | Decision Hypothesis rewritten for Cake-host + Linux-canonical; HOW §"Toolchain commitment" pinned to linux-x64 trio only; HOW §"Generator architecture" rewritten for `build/_build/Targets/GenerateBindings/` layout with pure-vs-Cake split; HOW §"Multi-pass parsing strategy" rewritten to preprocessor-macro switching only with ppy/SDL3-CS + spike evidence; new HOW §"Generation environment" hardens Docker prereq; new WHY §"Why hosted in the Cake build host — not a standalone CLI tool" (5 reasons); Plan Shape Stage 1/2/3 rescoped (SysWM → Stage 2); WHAT impact inventory rows updated; Decision Audit gains Error 4 + Reframe rows. |
| `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md` | Component Architecture rewritten as `build/_build/Targets/GenerateBindings/` layout with pure (no Cake) subfolders + Cake-aware shell; cross-cutting validators under `build/_build/Validation/BindingGeneration/`; `SDL_syswm.h` Stage 1 scope shifted to Stage 2 deferral. |
| `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` | 3700+ line plan rewritten: revision header documenting four retractions; File Structure Map shifted to Cake host; Task 1 rewritten to add CppAst packages to `build/_build/Build.csproj`; PlatformCatalog reduced from 18 entries to 8-entry `(OsCondition, BackendCondition[])` tuple model; SysWM emission deferred; BindingGeneratorCli renamed to `BindingGenerationPipeline` (pure orchestrator); new Task 8 step for `tools.cs generate-bindings` Docker orchestration; runtime guard added; bulk path/namespace rewrites. |

### Wave 2 — Downstream doc sync + research retraction banners (commit `1c0b6e6`)

Eight downstream docs updated to reflect the 2026-05-15 architectural shifts. Frozen-artifact banners added at the top of three research docs pointing at the strategy brief's Decision Audit for retractions.

| File | What changed |
|---|---|
| `docs/binding-autogen/README.md` | Status banner + reading order + decision posture rewritten; spec + plan added to reading order; ADR-002/003 + knowledge-base cross-refs. |
| `docs/binding-autogen/research/binding-autogen-onboarding.md` | Date 2026-05-15; new "2026-05-15 Architectural Shifts" section enumerates four retractions; Required Reading list adds spec + plan rows; Current State reframes to Cake host; Open Decisions table converts D1–D11 + Q1–Q8 to closed/Stage-2/deferred; Document Map adds spec + plan + bottlenoselabs rows. |
| `docs/plan.md` | Phase 4 row expanded with Stage 1/2 sub-bullets, references to `docs/superpowers/specs/` and `docs/superpowers/plans/`, Cake-host status note; Phase 5 row gains PD-7 gating note. |
| `docs/phases/phase-4-binding-autogen.md` | Rewritten as a thin canonical-pointer page; defers strategy/architecture/implementation detail to the strategy brief + spec + plan. |
| `docs/release-strategy.md` | Stage 1/2/3 rows in the Sequencing table gain explicit context (Cake-host fold, Linux-canonical, Stage 1 vs Stage 2 scope split, SDL3 PD-7 gating). |
| `docs/binding-autogen/research/binding-autogen-feasibility.md` | Frozen-artifact banner added; explicit pointers for the three retractions (mingw-w64/Apple-SDK stub speculation, standalone `src/`-tree project, SDL_syswm first-class framing). |
| `docs/binding-autogen/research/binding-autogen-spike-findings.md` | Frozen-artifact banner notes that the Windows-host spike scaffolding does NOT carry into Stage 1's Linux-canonical production; preprocessor-macro switching at `Program.cs:42-72` is what survives. |
| `docs/binding-autogen/research/binding-autogen-approaches.md` | Frozen-artifact banner supersedes the toolchain-comparison framing on generator host + multi-platform parsing strategy. |

## Onboarding Snapshot

**Project context.** Janset.SDL2 / Janset.SDL3 — .NET 10 / C# 14 modular SDL bindings + vcpkg-built hybrid-static native libraries, distributed as NuGet packages across 7 RIDs (`win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`). Cake Frosting 6.1 build host, `tools.cs` file-based .NET 10 app for local-dev orchestration. Hobby project; pride-driven, not deadline-driven; v1.0 big-bang launch covers SDL2 + SDL3 with all in-scope satellites.

**Phase 4 context.** Binding auto-generation. CppAst 0.24.0 + libclang.runtime.linux-x64 + libClangSharp.runtime.linux-x64 20.1.2 — Linux-canonical version trio. The generator lives inside the Cake build host under `build/_build/Targets/GenerateBindings/`, target-local per ADR-002 §2.4 until SDL3 creates a real second consumer. Pure emitter code stays Cake-free; the Cake-aware shell owns orchestration. Local invocation flows through `tools.cs generate-bindings` → Docker against the pinned `linux-builder` image.

**Locked strategic decisions** (full anchor: AGENTS.md §Settled Strategic Decisions):

- AST-first sequencing — Phase 4 lands before first public `-preview.N` wave.
- CppAst toolchain selected per ADR-004; ClangSharp documented migration path.
- Generator hosted in Cake build host (Cake-host fold, 2026-05-15 revision).
- Linux-canonical generation; Docker hard prereq; fail-closed on non-`linux-x64` hosts.
- Preprocessor-macro switching only — no `--target`, no mingw-w64, no Apple SDK.
- `SDL_syswm.h` typed-union layout deferred to Stage 2.
- SDL3 binding generation gated on PD-7 (SDL2 real-public-release).
- 8-entry `(OsCondition, BackendCondition[])` `PlatformCatalog`: Neutral + Windows desktop + WinRT + GDK + Linux + macOS + iOS + Android. DirectFB/Vivante/MIR/OS-2 are documented exclusions.

## Current State You Should Assume Until Verified

- **HEAD**: `1c0b6e6` — `docs(binding-autogen): sync downstream docs + research retraction banners for 2026-05-15 revisions`
- **Previous commit**: `c921e44` — `docs(binding-autogen): fold generator into Cake host, lock Linux-canonical, retract stub speculation`
- **Branch**: `spike/binding-autogen-sdl2-gfx` (ahead of `master` with the doc revisions + the SDL2_gfx spike commits already on this branch).
- **Worktree expectation**: **clean** — both commits landed. Untracked files: `tools/` (spike artifacts, pre-session), `external/` if present.
- **Build-host tests**: not run during the doc wave. Last known green per AGENTS.md baseline + `c196ee1`/`13e182f` history. Verify before any code work with `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0`.
- **Spike runtime behavior signal**: green per `binding-autogen-spike-findings.md` §8 (180 frames @ 30 FPS bouncing-circle render, both toolchains).
- **Phase 4 status**: Stage 1 plan accepted, awaiting execution. No `build/_build/Targets/GenerateBindings/` folder exists yet — Task 1 creates it.
- **vcpkg state**: as of last `tools.cs setup` run. The Cake host's existing PreFlight handles vcpkg coherence; nothing to verify here for Stage 1 Task 1 specifically (Task 1 is package-add + scaffolding only).

## Recommended Next Step

Three options. Talk to Deniz before committing to which one. Master-direct commits are the default. Approval gate is a hard rule — every code-touching commit needs explicit "go / proceed / yap / apply / başla".

### Option A — Execute Stage 1 Task 1 (Recommended)

**Classification:** Multi-session arc; lightweight first task. Task 1 alone is ~30–60 min of focused work.

**Pre-flight:**

1. Run baseline checks per the plan's Task 1 Step 1: `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0` and `dotnet build src/SDL2.Core/SDL2.Core.csproj -c Release`. Both must pass before any Task 1 changes.
2. Read [`docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`](../../docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md) revision header + Scope Decisions Locked section + Cross-References block + Task 1 in full.
3. Invoke the `superpowers:subagent-driven-development` skill (recommended) or `superpowers:executing-plans` skill to work the plan task-by-task.

**Specific work (Task 1):**

- Add packages via `dotnet add build/_build/Build.csproj package CppAst --version 0.24.0` then `libclang.runtime.linux-x64 --version 20.1.2` then `libClangSharp.runtime.linux-x64 --version 20.1.2`. **No Windows or macOS runtime variants.**
- Wrap the three new entries in `Directory.Packages.props` with the documented Linux-canonical comment block per Task 1 Step 2.
- Create `build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs` placeholder + `Sdl2CoreGenerationConfig.cs` + `GenerateBindingsRequest.cs` per Task 1 Steps 3 + 5.
- Wire `AddGenerateBindings()` into `build/_build/Program.cs` composition root next to other `Add*()` calls.
- Write the failing TUnit test in `build/_build.Tests/Unit/Targets/GenerateBindings/Sdl2CoreGenerationConfigTests.cs` per Task 1 Step 4; make it pass per Step 5; verify build host still compiles cleanly per Step 6.
- Commit checkpoint per Task 1's commit step. **Present commit message to Deniz and wait for explicit approval before committing.**

**Acceptance criteria:**

- Three `dotnet add` commands ran successfully; `Directory.Packages.props` has three new `PackageVersion` entries; `build/_build/Build.csproj` has three new `PackageReference` entries.
- `Sdl2CoreGenerationConfigTests` passes both tests.
- Full build-host test suite still green.
- Commit lands on `spike/binding-autogen-sdl2-gfx` with maintainer-approved message.

### Option B — Re-read Stage 1 plan end-to-end first

**Classification:** Lightweight verification pass. ~30–45 min of focused reading.

**Why:** The Stage 1 plan is 3700+ lines after the bulk path/namespace replacements in Wave 1. Bulk replacements can introduce subtle inconsistencies (orphan references, mismatched arity in code snippets, stale invocation examples). A clean read before execution catches issues that would otherwise surface mid-Task-N as "this doesn't compile" friction.

**Pre-flight:** Read the plan front-to-back with a focus on:

- Inter-task references (e.g., Task 3 mentions Task 1 outputs — do paths align?)
- Code-snippet arity (`PlatformParseView` constructor went from 6 args to 5; any caller still passing 6 args?)
- Class-name consistency (`BindingGeneratorCli` → `BindingGenerationPipeline`; any stale references?)
- Invocation examples (`--triplet x64-linux-hybrid` everywhere it appears; `--target GenerateBindings --family sdl2-core` for Cake invocations)

**Specific work:** Flag any inconsistencies in a short follow-up message. Either fix them in a small doc-fixup commit (approval gate applies) or accept them as known-edge-cases that the next agent will fix during execution.

**When to choose this:** If risk-aversion is the priority over momentum. Stage 1 is a multi-week effort; spending 30 min de-risking the plan is cheap.

### Option C — Different area entirely

If Deniz redirects to a non-binding-autogen area (Phase 2b PD-7 work, the SDL2_gfx Unix symbol-export regression PD-15, the SDL2_net binding scaffolding, or something else), respect the redirect. Read the relevant phase doc + recent commits to that area before acting. Don't push the binding-autogen workstream forward unilaterally — Deniz sets the pace.

## Mandatory Grounding (read in this order)

1. [`docs/onboarding.md`](../../docs/onboarding.md) — project framing + glossary + non-goals.
2. [`AGENTS.md`](../../AGENTS.md) — operating rules, approval gate, settled strategic decisions, build-host reference pattern.
3. [`CLAUDE.md`](../../CLAUDE.md) — relay to AGENTS.md.
4. [`docs/plan.md`](../../docs/plan.md) — tactical roadmap, Phase 4 status with Stage 1/2 sub-bullets, Phase 5 PD-7 gating.
5. [`docs/binding-autogen/binding-autogen-strategy-brief.md`](../../docs/binding-autogen/binding-autogen-strategy-brief.md) — accepted strategy brief; canonical source for all Phase 4 decisions.
6. [`docs/binding-autogen/research/binding-autogen-onboarding.md`](../../docs/binding-autogen/research/binding-autogen-onboarding.md) — workstream onboarding; read the new "2026-05-15 Architectural Shifts" section first.
7. [`docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md`](../../docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md) — architecture design spec; Cake-host component layout.
8. [`docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`](../../docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md) — Stage 1 implementation plan; your active artifact.
9. [`docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 toolchain decision.
10. [`docs/decisions/2026-05-05-target-centric-build-host.md`](../../docs/decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric pattern the Cake-host fold inherits.
11. [`docs/decisions/2026-05-12-build-host-data-layer.md`](../../docs/decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer the binding validators extend.
12. [`docs/knowledge-base/extraction-guidelines.md`](../../docs/knowledge-base/extraction-guidelines.md) — collaborator extraction discipline; relevant when Stage 1 Tasks 2–7 add the pure emitter classes.
13. [`docs/knowledge-base/testing-guidelines.md`](../../docs/knowledge-base/testing-guidelines.md) — canonical TUnit/MTP test infrastructure; `FakeCakeWorld`, `TargetTestHost`, `FixtureLoader` patterns.
14. [`docs/knowledge-base/release-guardrails.md`](../../docs/knowledge-base/release-guardrails.md) — guardrail catalog; binding vcpkg coherence + symbol existence final G-IDs assigned at Phase 6.
15. [`docs/release-strategy.md`](../../docs/release-strategy.md) — strategic anchor, Stage 1/2/3 sequencing.

Skip the spike-findings / approaches / feasibility research docs unless you specifically need to verify a corrected claim — their frozen-artifact banners point you back to the strategy brief Decision Audit.

## Locked Policy Recap

Most-likely-to-be-tempted-to-violate rules during Stage 1 execution. Full anchor: AGENTS.md §Settled Strategic Decisions + the strategy brief.

- **Approval gate is a hard rule.** Every code-touching commit needs explicit "go / proceed / yap / apply / başla". Documentation-only edits are exempt. The Stage 1 plan's "Commit checkpoint" steps mean: present a summary + proposed commit message to Deniz, wait for approval, then commit.
- **No new `src/`-tree generator project.** The generator lives at `build/_build/Targets/GenerateBindings/`, full stop. Do not create `src/Janset.SDL2.Bindings.Generator/` even as a "temporary" host. The 2026-05-14 draft proposed it and it was retracted on 2026-05-15.
- **No new test project.** Generator tests live in `build/_build.Tests/Unit/Targets/GenerateBindings/` and `build/_build.Tests/Scenarios/GenerateBindings/`. Do not create `tests/Janset.SDL2.Bindings.Generator.Tests/`.
- **Linux-canonical runtime trio only.** `libclang.runtime.linux-x64` + `libClangSharp.runtime.linux-x64` 20.1.2. No `win-x64`, `osx-x64`, `osx-arm64`, or `linux-arm64` variants. The version trio bumps together — never independently.
- **`GenerateBindings` Cake target is Linux-canonical.** The runtime guard in `GenerateBindingsTask` fails closed on any non-`linux-x64` host with an actionable diagnostic. Local invocation flows through `tools.cs generate-bindings` (Docker orchestration).
- **Preprocessor-macro switching only for platform passes.** No `--target` cross-compile flag, no mingw-w64, no Apple SDK headers, no platform stubs in Stage 1. SDL's public headers carry their own forward declarations.
- **`SDL_syswm.h` typed-union layout is Stage 2.** Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `nint`-shaped `SDL_SysWMinfo*` parameter; `SDL_SysWMinfo` + `SDL_SysWMmsg` go into `UnsupportedDeclarations.g.json` with category `deferred-to-stage-2`.
- **Pure-vs-Cake split is non-negotiable.** Subfolders `HeaderSet/`, `Parsing/`, `Model/`, `Emitting/`, `Stamps/` under the GenerateBindings target are PURE — no `ICakeContext`, no Cake aliases, no `Tool<TSettings>` dependencies. Cake-aware shell (`GenerateBindingsTask`, `BindingGenerationRunner`, `ServiceCollectionExtensions`) is the only Cake-touching surface.
- **`.generated-stamp` is deterministic.** Never include wall-clock timestamps. The reproducibility gate (Layer 5 test) requires byte-identical regenerated diffs.
- **`tools.cs` stays standalone.** Per ADR-002 §2.3. The new `generate-bindings` subcommand lives in `tools.cs` as a Spectre.Console.Cli command; it must not reference `build/_build` internals.
- **`dotnet add` only.** Never hand-edit `Directory.Packages.props` or csproj `PackageReference` entries directly. Use `dotnet add <project> package <name> --version <version>`.
- **Guardrail IDs are not code names.** Behavior-first names (`BindingGenerationCoherenceValidator`, `BindingSymbolExistenceValidator`). Final G-IDs assigned at Phase 6 guardrail-catalog refresh.
- **Memory file `feedback_session_pacing`.** After a slice commits, default to "what's next?" not "let me wrap up." Don't write priming prompts unilaterally.
- **Memory file `feedback_skip_micro_checkpoints`.** Batch routine task completions silently during execution. Stop only at significant boundaries (Task-level commits).
- **Memory file `feedback_follow_explicit_paths`.** When Deniz names a path, use it exactly. The Stage 1 plan's paths are authoritative.
- **Memory file `feedback_no_timing_pressure_or_motive_assumptions`.** Don't push timing ("ready to commit?"). Don't presume decision criteria. Deniz sets pace and signals when a topic is done.

## Final Steering Note

The hard design work is done. The Stage 1 plan is task-by-task TDD-style with concrete code snippets and acceptance criteria for every step. Your job is to execute it carefully, batch routine completions silently, and stop at Task-level commit boundaries to present approval-gate summaries.

Default rhythm: Task 1 (scaffold packages + placeholders) → commit → Task 2 (HeaderSet) → commit → … → Task 10 (documentation + final validation + anti-slop gate) → commit. Ten tasks, ten commits. Each task is its own focused unit; resist the temptation to merge tasks "because they touch related files." The plan's task boundaries match natural review points and keep diffs reviewable.

Hold the line on the Cake-host fold. If you find yourself wanting to create a `src/`-tree project or a separate test csproj "just for cleanliness", that's the 2026-05-14 draft trying to come back — the 2026-05-15 revision is canonical. The strategy brief's WHY §"Why hosted in the Cake build host — not a standalone CLI tool" has the full argument if you ever need to remind yourself or push back on a suggestion.

The build host is in good shape. Stage 1 makes it stronger.
