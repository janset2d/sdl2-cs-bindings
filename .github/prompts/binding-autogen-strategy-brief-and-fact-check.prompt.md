---
name: "Binding auto-generation strategy brief + research fact-check"
description: "Priming prompt for the next agent picking up the binding-autogen workstream on branch spike/binding-autogen-sdl2-gfx after the strategy brief was drafted §1-§5 and the existing research docs were fact-check corrected on 2026-05-14. All work is uncommitted; brief is structurally incomplete (§6-§9 scaffolded). Recommended next: finish §6-§9, commit the wave, then author the companion ADR before transitioning to Phase 4 implementation plan."
argument-hint: "Optional override: jump directly to Phase 4 plan authoring (skips brief §6-§9), or to companion ADR first (skips brief completion)"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You're an engineer entering `janset2d/sdl2-cs-bindings` on branch `spike/binding-autogen-sdl2-gfx`. The previous session ran a deep fact-check of the binding-autogen research base (corrected three substantive errors + cosmetic drift), then opened the brainstorming pass for the WHY/HOW/WHAT strategy brief. The brief is **drafted but incomplete** — sections 1-5 are full prose (~693 lines), sections 6-9 are HTML-comment scaffolds with structured "Contents to cover" lists. **None of the work has been committed.** Your job in this session is to finish what's open, commit cleanly, then either author the companion ADR or transition to Phase 4 implementation plan authoring — Deniz signals which.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-14 — pre-commit, working-tree state)** and verify against the live repo, `git log`, and canonical docs before acting. The strategy brief at `docs/binding-autogen/binding-autogen-strategy-brief.md` is the source of truth for the locked decisions; the existing 4 research docs were corrected during this session and may differ from what your training data shows.

## What Just Happened

Four waves, all on the working tree, none committed yet.

### Wave 1 — Research fact-check (verification only)

Verified 5 documents in `docs/binding-autogen/` against live external sources + actual spike artifacts. Methodology: `Explore` subagent for spike artifacts, `Bash`/`Grep` for repo claims, `WebFetch` for external NuGet / GitHub / Microsoft Learn evidence, math recalc for the decision matrix.

| Claim verified | Result |
|---|---|
| `external/sdl2-cs` totals 11,105 lines across 5 files | **EXACT** match (SDL2.cs 8966 / gfx 390 / image 316 / mixer 665 / ttf 768) |
| Directory.Packages.props version-trio pin (CppAst 0.24.0 + libclang.runtime 20.1.2 + libClangSharp.runtime 20.1.2) | **EXACT** match |
| CppAst v0.24.0 published 2025-11-20, BSD-2-Clause, targets net8.0 | NuGet confirms |
| Alimer.Bindings.SDL 3.9.8 (2025-04-04), MIT, SDL3 core only | NuGet confirms |
| ppy/SDL3-CS uses ClangSharpPInvokeGenerator 17.0.1 + Dockerfile + Python `generate_bindings.py` + `FriendlyOverloadGenerator.cs` | All confirmed live |
| Silk.NET 3.0 proposal "delegates parsing to ClangSharp" | Quote verified verbatim |
| Spike artifacts (102 P/Invoke, 361/345 lines, FPSmanager, shared Compile Include link) | All structural claims pass |

### Wave 2 — Corrections applied to existing docs

Three substantive errors fixed in-place, plus cosmetic drift refresh, plus an effort-estimate cross-reference fix. All as `Edit` operations on the corresponding files:

| Error / drift | File(s) edited | Fix |
|---|---|---|
| **Error 1** — "ClangSharp captured only 2 SDL2_gfx constants vs CppAst's 8" was the macro-capture-advantage spike narrative | `binding-autogen-spike-findings.md` §2 + §6.G + §7.4 (renamed "Revised 2026-05-14") + §7.5 trade-off matrix + §7.7 point 2 + §9 Q6; plus `binding-autogen-feasibility.md` §9 D6 row; plus `binding-autogen-onboarding.md` tree diagram | Both toolchains emit identical 8 constants (verified against current `bindings/Generated/SDL2_gfx.g.cs`). Retracted CppAst's macro-capture win narrative. CppAst pick now rests on scope-trajectory argument only. |
| **Error 2** — `FriendlyOverloadGenerator` claimed to use `IIncrementalGenerator` | `binding-autogen-spike-findings.md` §5.5 | Actual code uses `ISourceGenerator` (older API). D10 modernization is a real cost factor if migrating to ClangSharp. |
| **Error 3** — Original decision matrix arithmetic (CppAst 23 vs ClangSharp 22) | `binding-autogen-approaches.md` matrix table + re-validation paragraph | Both columns actually sum to 23 (tied). Recalculated weighted matrix (43 vs 63) still favors ClangSharp on raw-binding economics; scope-trajectory argument overrides. |
| Cosmetic drift | Multiple §7.5 / §2 / §7 trade-off matrix entries + onboarding.md tree | RSP 44→43 lines, CppAst Program.cs 225→261, NativeTypeNameAttribute 20→25, Constants.cs 10→11; added "verified 2026-05-14" date markers so future drift is traceable. |
| Effort-estimate cross-ref drift | `binding-autogen-feasibility.md` §8 | Replaced "release-strategy.md §Effort Calibration estimated 6-9 weeks / 5-9 months calendar" (numbers don't exist anywhere) with correct ranges (8-14 weeks total / 10-17 months total; Phase 4 portion = 5-8 weeks / 3.5-7 months, which matches feasibility.md §8 exactly). |
| Orphan "D12" reference | `binding-autogen-spike-findings.md` §5.4 | Converted to point at §9 Q2. Feasibility doc's D1-D11 list stays bounded. |

### Wave 3 — Brainstorming pass, foundational locks

Brainstorming skill flow walked through doc-shape clarifying questions one at a time. Locked decisions (in order asked):

| Question | Locked |
|---|---|
| Doc role | **Strategy brief + companion ADR**. Brief in `docs/binding-autogen/`, ADR in `docs/decisions/`. |
| Toolchain | **CppAst**. WHY framed around scope-trajectory bet (spike-findings §7.8 + §9 Q8). ClangSharp documented as migration target. |
| TFM scope | **Full matrix** — `net10` + `net9` + `net8` + `netstandard2.0` + `net462`. Dual-emit `[LibraryImport]` (net7+) / `[DllImport]` (legacy) in one emitter loop. |
| Handle baseline | **Typed `readonly struct Name(nint value)`** (Option C — Alimer / Vortice / Silk.NET pattern). Verified live against Alimer Handles.cs + Vortice.Vulkan Handles.cs. NOT IntPtr (sdl2-cs), NOT raw `T*` (ppy), NOT SafeHandle (Microsoft generic guidance — explicitly evaluated and rejected for SDL hot-path / app-scoped reasons). |
| Doc shape | **Topology-mirror + Decision Audit**. Sections: Decision Hypothesis / WHY / HOW / WHAT / Plan Shape / Open Decisions / Decision Audit / Cross-Reference. ~400-500 lines target. |
| Generation pipeline | **Separate workflow** `regenerate-bindings.yml`, manual trigger, **auto-PR via `peter-evans/create-pull-request@v7`** (Silk.NET reference pattern). |
| Generation environment | **Single Linux container for all OS parse views** (ppy pattern). Reuse `docker/linux-builder.Dockerfile` as-is. Local-dev `tools.cs generate-bindings` via Docker invocation. |
| New guardrails | **Two locked**: (1) vcpkg-state coherence at PreFlight (catches "natives rebuilt without binding regen"), (2) symbol-existence at Pack (catches "binding declares unexported function"). |

### Wave 4 — Strategy brief drafted (§1-§5)

Created `docs/binding-autogen/binding-autogen-strategy-brief.md` (~693 lines). Sections complete:

| Section | Status | Notes |
|---|---|---|
| Title + status header | ✅ | "Working draft" / retire conditions / current status marker |
| Decision Hypothesis | ✅ | Tight executive summary of all locks |
| WHY | ✅ | 3 sub-arguments with full citation density (internal + external) |
| HOW | ✅ | 11 sub-sections covering toolchain pin, generator architecture, emit rules (11-rule mapping table), multi-pass parsing (Linux container + topology tree), satellite/shared-types, multi-TFM dual emit, friendly overloads, generation pipeline (workflow YAML sketch), generation environment (Docker reuse + tools.cs sketch), vcpkg-state coherence guardrail (stamp file schema + validation rule), symbol-existence guardrail |
| WHAT | ✅ | 25-row impact inventory table + 7-layer test strategy mapped to canonical infrastructure + 4 decoupled work items |
| Plan Shape | 🚧 scaffold | HTML comment with "Contents to cover" list (Stage 1/2/3 outline + exit criteria + plan-authoring methodology reference) |
| Current Open Decisions | 🚧 scaffold | HTML comment with topics list |
| Decision Audit | 🚧 scaffold | HTML comment with all Wave-2 findings cataloged |
| Cross-Reference | 🚧 scaffold | HTML comment with consolidated link list |

Self-review checks on §1-§5 passed: no stray TBD/TODO outside intentional scaffolds, internal consistency holds (WHY argues scope-trajectory → HOW commits scope-trajectory features), every relative link verified to resolve, behavior-first guardrail naming throughout per AGENTS.md §"Guardrail IDs are not code names."

## Current State You Should Assume Until Verified

- **Master HEAD**: `c196ee1` — `docs: release strategy + AST research wave + topology parking`
- **Branch**: `spike/binding-autogen-sdl2-gfx` (ahead of master with the spike commits already documented in onboarding.md §"Current state")
- **Worktree expectation**: **dirty** — see below. Nothing from the four waves above has been committed.
- **New file (untracked)**: `docs/binding-autogen/binding-autogen-strategy-brief.md` (~693 lines, §1-§5 prose, §6-§9 HTML-comment scaffolds)
- **Edited files inside `docs/binding-autogen/`** (untracked folder from master's perspective): `binding-autogen-approaches.md`, `binding-autogen-feasibility.md`, `binding-autogen-spike-findings.md`, `binding-autogen-onboarding.md` — all carry the Wave-2 corrections. Note: `git status` shows the entire folder as `??` rather than these files as `M` because the folder itself isn't yet in master's tree.
- **Other tracked files modified**: `AGENTS.md`, `Directory.Packages.props`, `docs/README.md`, `docs/phases/phase-4-binding-autogen.md`, `docs/phases/phase-5-sdl3-support.md`, `docs/plan.md`, `docs/release-strategy.md` — these are pre-session-1 changes that were already in the working tree when this session began (carry-forward).
- **Deletions tracked**: `docs/research/binding-autogen-approaches.md` and `docs/research/binding-autogen-feasibility.md` — file moves into `docs/binding-autogen/` (rename, not delete).
- **Untracked spike tree**: `tools/` — the SDL2_gfx spike artifacts (ClangSharp + CppAst sides + platform-pass + image variants), pre-session-1.
- **Build-host tests**: not run this session. Last known green per AGENTS.md context.
- **Behavior signal**: spike runtime smoke is green per `binding-autogen-spike-findings.md` §8 (both toolchains, 180 frames @ 30 FPS, identical render).
- **Phase 4 status**: PLANNED. Strategy brief authoring is the active sub-task; companion ADR + Phase 4 implementation plan are downstream.

## Recommended Next Step

Three options. Talk to Deniz before committing to which one. Master-direct commits are the default per AGENTS.md.

### Option A — Finish §6-§9 of the strategy brief (Recommended)

**Classification:** Lightweight, well-scoped. ~1-2 hour focused work.

**Pre-flight:**
1. Read `docs/binding-autogen/binding-autogen-strategy-brief.md` end-to-end (the in-progress brief).
2. Skim the HTML-comment scaffolds in §6-§9 for the structured "Contents to cover" lists left by the previous session — those are your section briefs.
3. Skim [`docs/release-strategy.md`](../../docs/release-strategy.md) §Sequencing (Stage 1-3 mapping anchor for §6 Plan Shape).
4. Skim [`docs/parking-lot/package-topology/phase-planning-methodology.md`](../../docs/parking-lot/package-topology/phase-planning-methodology.md) — reference for plan-authoring discipline that §6 should explicitly cite.

**Specific work:**

- `## Plan Shape` — Stage 1 (SDL2.Core proof-of-life) / Stage 2 (SDL2 satellite sweep + sdl2-cs retirement) / Stage 3 (SDL3 extension). Per-stage exit criteria. Cite release-strategy.md §Sequencing. Match topology brief's "Plan Shape" sub-section shape (`### Phase 0: finish strategy ...` / etc.).
- `## Current Open Decisions` — table of sub-decisions not locked here with recommended defaults. Topics already enumerated in the scaffold comment.
- `## Decision Audit` — record Wave-2 findings (Error 1/2/3 + cosmetic drift + outstanding verification items) as historical traceability. Catalog already captured in `What Just Happened` above.
- `## Cross-Reference` — consolidated link list. Internal (AGENTS.md, ADR-001/002/003, knowledge-base, parking-lot, research, phases, playbook, release-strategy, plan, onboarding) + binding-autogen siblings + external research (CppAst, Alimer, Vortice, ppy, Silk.NET, Microsoft Learn, peter-evans/create-pull-request, bottlenoselabs).

**Acceptance criteria:**

- All 9 sections are full prose; no remaining HTML-comment scaffolds.
- Line count likely 800-900 total (target was ~400-500; reality has been denser).
- `grep "Pending section" <file>` returns 0.
- All relative paths resolve.
- Self-review pass: placeholder scan / internal consistency / scope / ambiguity (see brainstorming skill checklist step 7).
- **Then** ask Deniz to review the full brief on disk. After approval, propose a commit message and ask for "go / proceed / yap / başla" before committing.

### Option B — Companion ADR first

**Classification:** Short, well-scoped. ~30 min focused work.

**Pre-flight:**
1. Read [`docs/decisions/2026-05-05-d3seg-and-package-first.md`](../../docs/decisions/2026-05-05-d3seg-and-package-first.md) (ADR-001) for shape reference.
2. Read [`docs/decisions/2026-05-12-build-host-data-layer.md`](../../docs/decisions/2026-05-12-build-host-data-layer.md) (ADR-003) — most recent ADR shape.
3. Re-read the strategy brief's Decision Hypothesis + WHY § "Why CppAst" sub-section.

**Specific work:**

Create `docs/decisions/2026-MM-DD-binding-autogen-toolchain.md` (use the session date) recording:

- Toolchain: CppAst 0.24.0 + version-trio (per Strategy Brief HOW).
- Rationale: scope-trajectory bet (cite spike-findings §7.8).
- Locked emit-discipline points: typed handle struct baseline, full TFM matrix, multi-pass via single Linux container, generation pipeline shape.
- Migration door: ClangSharp + ppy pattern documented as fallback path.
- Cross-references back to the strategy brief.

**Acceptance criteria:** ADR is ~80-120 lines, follows ADR-001 / ADR-003 shape (Status / Date / numbered sections / References), passes `markdownlint` if applicable. Then commit (after Deniz approval) and return to Option A to finish the brief.

**Why this option exists:** the ADR captures the irreversible decision in canonical record form before §6-§9 are done. Risk: skipping ahead in the brief feels structurally odd. Mitigation: finish §6-§9 immediately after the ADR commits.

### Option C — Jump to Phase 4 implementation plan (writing-plans skill)

**Classification:** Multi-session arc. Aggressive jump-ahead.

**Pre-flight:**
1. Read the strategy brief end-to-end including §6-§9 scaffolds.
2. Read [`docs/parking-lot/package-topology/phase-planning-methodology.md`](../../docs/parking-lot/package-topology/phase-planning-methodology.md) — the canonical plan-authoring convention to apply.
3. Read [`docs/parking-lot/package-topology/implementation-plan-phase-1.md`](../../docs/parking-lot/package-topology/implementation-plan-phase-1.md) for plan-doc shape reference.

**Specific work:** Invoke `superpowers:writing-plans` skill against Phase 4 Stage 1 (SDL2.Core proof-of-life). Plan slices the work, exit criteria per slice, references this brief + AGENTS.md + ADRs.

**Acceptance criteria:** Phase 4 Stage 1 plan document on disk; ready for `superpowers:executing-plans` or `superpowers:subagent-driven-development` in a downstream session.

**Caveat:** Stage 1/2/3 outline lives in the brief's §6 Plan Shape, which is currently scaffolded. Authoring Phase 4 plan without §6 means deriving slices ad-hoc. Only choose this option if Deniz explicitly accepts that the brief's §6-§9 will land in a separate housekeeping commit later. Not the natural shape.

## Mandatory Grounding (read in this order)

1. [`docs/onboarding.md`](../../docs/onboarding.md) — project framing + glossary + non-goals
2. [`AGENTS.md`](../../AGENTS.md) — operating rules, approval gate, settled strategic decisions
3. [`CLAUDE.md`](../../CLAUDE.md) — relay to AGENTS.md
4. [`docs/binding-autogen/binding-autogen-onboarding.md`](../../docs/binding-autogen/binding-autogen-onboarding.md) — workstream entry point
5. [`docs/binding-autogen/binding-autogen-strategy-brief.md`](../../docs/binding-autogen/binding-autogen-strategy-brief.md) — the in-progress brief (your active artifact)
6. [`docs/binding-autogen/binding-autogen-feasibility.md`](../../docs/binding-autogen/binding-autogen-feasibility.md) — corrected this session; emit rules + 7-layer test strategy + open decisions
7. [`docs/binding-autogen/binding-autogen-spike-findings.md`](../../docs/binding-autogen/binding-autogen-spike-findings.md) — corrected this session; scope-trajectory analysis + Q1-Q8 open questions
8. [`docs/binding-autogen/binding-autogen-approaches.md`](../../docs/binding-autogen/binding-autogen-approaches.md) — corrected this session; decision matrix + source-level comparison
9. [`docs/release-strategy.md`](../../docs/release-strategy.md) — Stage 1-5 sequencing; AST-first anchor
10. [`docs/phases/phase-4-binding-autogen.md`](../../docs/phases/phase-4-binding-autogen.md) — Phase 4 brief; superseded by strategy brief at Phase 6
11. [`docs/decisions/2026-05-05-d3seg-and-package-first.md`](../../docs/decisions/2026-05-05-d3seg-and-package-first.md) — ADR-001 (D-3seg + family-lock)
12. [`docs/knowledge-base/release-guardrails.md`](../../docs/knowledge-base/release-guardrails.md) — guardrail catalog; two new guardrails (vcpkg-state coherence + symbol existence) queued for Phase 6 promotion
13. [`docs/parking-lot/package-topology/phase-planning-methodology.md`](../../docs/parking-lot/package-topology/phase-planning-methodology.md) — plan-authoring convention applicable to Phase 4 plans

## Locked Policy Recap

These are the rules most-likely-to-be-tempted-to-violate during this work. Full strategic anchor lives in AGENTS.md §Settled Strategic Decisions.

- **Approval gate is a hard rule.** Documentation-only edits are exempt; commits, code changes, build-system changes, deployment all require explicit "go / proceed / yap / apply / başla". Strategy brief edits = OK without per-edit approval; commit of the wave = requires approval.
- **CppAst toolchain locked.** Brief's WHY argues this on scope-trajectory grounds. Do not reopen unless Deniz explicitly does. ClangSharp stays documented as migration target.
- **Version-trio pin is non-negotiable.** CppAst 0.24.0 + libclang.runtime 20.1.2 + libClangSharp.runtime 20.1.2 — coordinated bumps only. Mismatched majors crash AST visitor (spike §7.6).
- **Full TFM matrix locked.** net10/net9/net8/netstandard2.0/net462. Dual-emit in single emitter loop. Brief WHAT impact inventory anchors this.
- **Typed `readonly struct` handle baseline locked.** Option C — Alimer/Vortice pattern. NOT IntPtr, NOT raw `T*`, NOT SafeHandle. Microsoft generic guidance ("DO use SafeHandle") explicitly evaluated and rejected for SDL hot-path / app-scoped reasons (brief WHY + handle-baseline discussion).
- **Single Linux container for all OS parse views.** Reuse `docker/linux-builder.Dockerfile` as-is; `setup-dotnet` adds .NET SDK at job start.
- **Two pipelines, two artifact lineages.** `release.yml` for native + managed nupkg packaging; `regenerate-bindings.yml` (new) for binding regen + auto-PR. They share `vcpkg-setup` + `nuget-cache` composite actions.
- **Generated source committed to git.** D8 locked. CI drift check (`git diff --exit-code src/**/Generated/`) is a release guard.
- **AGENTS.md "Guardrail IDs are not code names."** Behavior-first names everywhere (`BindingVcpkgCoherenceValidator`, `BindingSymbolExistenceValidator`). G-IDs assigned at Phase 6 catalog refresh.
- **Brief retires at Phase 6.** Promotes decisions into ADR + AGENTS.md + knowledge-base + onboarding + release-strategy. Lifecycle matches the topology refactor's strategy-brief / impact-map / per-phase-plan model (parking-lot/package-topology/phase-planning-methodology.md).
- **Memory file `feedback_follow_explicit_paths`.** When Deniz names a path, use it exactly. Strategy brief lives at `docs/binding-autogen/binding-autogen-strategy-brief.md` and nowhere else.
- **Memory file `feedback_no_timing_pressure_or_motive_assumptions`.** Don't push timing ("ready to commit?"). Don't presume decision criteria. Deniz sets pace and signals when a topic is done.

## Final Steering Note

The hard work for this brief is done. §1-§5 carry the load-bearing decisions; §6-§9 are largely synthesis and cataloging against material already produced. Plan Shape (§6) is the only one with real authoring weight — Stage 1/2/3 with exit criteria and Phase 4 plan-authoring methodology reference. The others (Open Decisions / Decision Audit / Cross-Reference) are list-shaped: you have the inputs in the existing conversation outputs + the scaffold notes left in the file.

Default rhythm: finish §6-§9 → self-review → present commit message to Deniz → commit wave → write companion ADR → commit ADR → enter Phase 4 plan authoring via `superpowers:writing-plans`. The Phase 4 plan is the natural next stop after the brief lands; topology refactor's `phase-planning-methodology.md` is the convention to apply verbatim.

Hold the line on scope. The brief deliberately defers a lot to "Phase 4 plan owns this" — that's correct. Don't slip implementation details into §6.
