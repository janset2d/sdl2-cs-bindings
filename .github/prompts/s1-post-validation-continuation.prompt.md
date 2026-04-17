---
name: "S1 Post-Validation Continuation"
description: "Use when resuming forward work on janset2d/sdl2-cs-bindings after the 2026-04-17 3-platform S1 verification landed. Forces the agent to ground itself in the Cake build host, the Harvesting golden standard, and the honest strategy-layer state before touching anything."
argument-hint: "Optional focus area (e.g. PA-1, PA-2, experiment:#88) or version suffix override"
agent: "agent"
model: "GPT-5 (copilot)"
---

You are entering `janset2d/sdl2-cs-bindings` after the 2026-04-17 Stream D-local + buildTransitive + G47/G48 landing and the full 3-platform smoke validation that followed it. The repo is **not** in a mid-refactor state. Your job is forward work, scoped to one concrete engineering step at a time, and your first obligation is to ground yourself in the existing system before you propose changes.

The previous prompt in this slot was [`s1-staged-code-review-and-reverify.prompt.md`](s1-staged-code-review-and-reverify.prompt.md) — that covered the cleanup-and-verify pass right after S1 landed. It is kept in the repo for historical continuity; do not re-run it unless the user explicitly asks. Its work is done.

## Mandatory Onboarding Before You Touch Anything

These are non-negotiable. The rest of the prompt assumes you have completed them.

### 1. Become competent in the Cake build host

Spend real reading time — not a skim — on the build host under `build/_build/`. At minimum:

- [`build/_build/Program.cs`](../../build/_build/Program.cs) — DI composition, CLI surface, option wiring.
- [`build/_build/Modules/Harvesting/`](../../build/_build/Modules/Harvesting/) — the reference implementation for module shape (see §2 below).
- [`build/_build/Modules/Packaging/`](../../build/_build/Modules/Packaging/) — the newest module; verify you can reproduce in your head how `PackageTask → PackageTaskRunner` flows through `DotNetPackInvoker`, `ProjectMetadataReader`, `PackageOutputValidator`, and the consumer-side `buildTransitive/Janset.SDL2.Native.Common.targets`.
- [`build/_build/Modules/Strategy/`](../../build/_build/Modules/Strategy/) — **required** because the plan discusses strategy dispatch but the code does something narrower. See §3.
- [`build/_build.Tests/`](../../build/_build.Tests/) — mirror of production module shape. Production test invariants live here.

If you cannot, without opening docs, describe (a) how `HarvestTask` and `PackageTask` interact with the typed `Result<TError, TSuccess>` pattern, (b) why each `.Native` nupkg ships both a per-package wrapper and a shared `buildTransitive/Janset.SDL2.Native.Common.targets`, and (c) what G47 and G48 are, then you have not done the reading yet. Go back.

### 2. Internalize what "golden standard" means here

The repo uses the phrase "golden standard" (aka "reference pattern") for Harvesting specifically. That is not a vibes-based label. It refers to a concrete set of structural invariants documented in two places:

- [`docs/knowledge-base/cake-build-architecture.md` §"Reference Pattern: Harvesting First"](../../docs/knowledge-base/cake-build-architecture.md) — explains what to copy.
- [`docs/knowledge-base/harvesting-process.md` §2](../../docs/knowledge-base/harvesting-process.md) — reinforces the structural role of Harvesting for new modules.

Concrete Harvesting shape rules (before you claim another module follows the pattern, confirm each):

1. Task layer owns only `BuildContext`, user-facing failure policy, and orchestration. No business logic.
2. Services take narrow, explicit inputs — not `BuildContext` — and are DI-registered in `Program.cs`.
3. Typed domain Results (`ClosureResult`, `ArtifactPlannerResult`, `CopierResult`, etc.) with the full `OneOf.Monads` surface: implicit / explicit operators + `From*` / `To*` static factories + typed accessors.
4. Per-operation error hierarchy rooted in `BuildError` → module base → per-operation subclass.
5. Test shape mirrors production boundaries.
6. Scanner outputs (`dumpbin` / `ldd` / `otool`) are consumed both by `BinaryClosureWalker` (discovery) and by `HybridStaticValidator` (guardrail) — same data, two consumers, zero scanner code changes. This repurposing is documented in [`docs/research/cake-strategy-implementation-brief-2026-04-14.md` §"Scanner Repurposing"](../../docs/research/cake-strategy-implementation-brief-2026-04-14.md).

Packaging was migrated onto this shape on 2026-04-17. If you add or modify a module, verify the 6 invariants hold before you claim you are "following the pattern."

### 3. Read the Strategy State Audit before making any strategy-related claim

This is the single most load-bearing onboarding item. The plan and the #85 handoff describe the strategy layer as "landed." That is technically correct but easy to misread as "strategy actively dispatches behavior per RID."

Read [`docs/phases/phase-2-adaptation-plan.md` §"Strategy State Audit"](../../docs/phases/phase-2-adaptation-plan.md). That section is the source of truth for what the strategy layer actually does today. Summary you must hold in your head before proposing anything strategy-adjacent:

- `IPackagingStrategy` is a string-compare helper (`IsCoreLibrary`). Consumed in exactly one place — `HybridStaticValidator`. The Packaging module does not touch it.
- `IDependencyPolicyValidator` has **one** real implementation (`HybridStaticValidator` — closure-leak guardrail). `PureDynamicValidator` is a one-line pass-through by design (brief-specified, not a stub).
- `INativeAcquisitionStrategy` was designed in the brief but **never implemented**. Its role may have been implicitly subsumed by Source Mode (Stream F) — this is undocumented and deserves a decision.
- `IPayloadLayoutPolicy` was deferred in the brief "until PackageTask lands." PackageTask has been shipping for two weeks. The deferral is stale.
- Behavioral dispatch today amounts to two things: (a) which validator DI resolves during harvest; (b) declarative PreFlight coherence. Everything downstream — Packaging, consumer smoke, deployer — is strategy-agnostic.

If your proposed next step assumes strategy is a dispatcher, go re-read the audit. Most likely the right next step is either to make the strategy seam real (PA-1 decision + PA-2 overlay triplets) or to retire the unrealized interfaces from the plan, not to ride on top of wiring that does not exist yet.

### 4. Know the current active work stream map

Consult [`docs/plan.md` §Phase 2a + §Phase 2b](../../docs/plan.md) for the authoritative state, and [`docs/phases/phase-2-adaptation-plan.md`](../../docs/phases/phase-2-adaptation-plan.md) for the stream-level breakdown.

- **Phase 2a (ACTIVE):** proof slice validated end-to-end on three hybrid-static RIDs. Exit bar met on 2026-04-17.
- **PA-1 (OPEN, blocks Stream C):** matrix strategy review — RID-only vs `strategy × RID` vs parity-job shape.
- **PA-2 (OPEN, blocks Stream C):** hybrid overlay triplet expansion for `x86-windows-hybrid`, `arm64-windows-hybrid`, `arm64-linux-hybrid`, `arm64-osx-hybrid`.
- **Stream C (NEXT, blocked on PA-1 + PA-2):** CI modernization, dynamic matrix, PreFlight as gate.
- **Stream D-ci (after C):** CI package-publish + smoke gate + internal feed push.
- **Stream F (parallel, different axis):** Source Mode `--source=local|remote`. Local path is partially landed; remote path has open producer/archive contracts.

Open issues that belong in the next agent's mental model:

- **#87** — HarvestPipeline extraction from HarvestTask (type:cleanup, deferred).
- **#88** — Experiment: `Process.Kill(entireProcessTree: true)` refactor for `PackageConsumerSmokeRunner` to replace the per-user `dotnet build-server shutdown` side-effect.
- **#89** — Research: consumer package-upgrade testing (N → N+1 symlink churn, stale file behavior, cross-family version drift).

## Hard Scope

You are entering a validated state. Do not treat this as a verification pass.

- Do **not** re-run the 3-platform smoke just to "confirm" — that was the previous prompt's job and it passed.
- Do **not** rehash the S1 adoption decision. It is closed.
- Do **not** propose changes to the strategy layer without first reading the audit and explicitly stating which audit gap your change closes.

Your scope is one concrete step of forward work.

## What You Should Do

### 1. Pick a scoped target and justify it

Candidates, in rough priority order:

1. **PA-1 — Matrix strategy review.** Deliverable: a design decision memo (markdown in `docs/research/`) comparing RID-only vs `strategy × RID` vs parity-job shapes for Stream C's CI matrix. Must land before PA-2 or Stream C.
2. **PA-2 — Hybrid overlay triplet expansion.** Deliverable: four new overlay triplets (`x86-windows-hybrid`, `arm64-windows-hybrid`, `arm64-linux-hybrid`, `arm64-osx-hybrid`), manifest update, harvest + pack + consumer-smoke validation on at least one newly-covered RID per OS family. Blocks on PA-1 decision being explicit about whether pure-dynamic is kept.
3. **Close a Strategy State Audit gap.** Either implement `INativeAcquisitionStrategy` or retire it from the plan with a rationale pointing at Source Mode. Either extract `IPayloadLayoutPolicy` or retire the deferral.
4. **#87, #88, or #89** as scoped experiments if the user explicitly prioritizes them.
5. **A Phase 2b item** the user calls out — e.g., Mixer / Ttf / Gfx bring-up, Linux symbol-visibility version scripts, sample project, SDL2_net addition.

State your target up front, justify why it is the right next step given the open blockers, and get user confirmation before executing.

### 2. Follow repository discipline

- Every new module follows the Harvesting shape (see §2 onboarding).
- Every new service returns typed Results with the full surface (see §2 rule 3).
- Every behavioral claim is backed by a test in `build/_build.Tests/` or a reproducible Cake invocation.
- Docs update with the code. If you land G-whatever, it enters [`docs/knowledge-base/release-guardrails.md`](../../docs/knowledge-base/release-guardrails.md) in the same commit.
- If you touch native payload or consumer-side .targets logic, you also update [`docs/playbook/cross-platform-smoke-validation.md`](../../docs/playbook/cross-platform-smoke-validation.md) and re-run the matrix on all three platforms.
- Commits are conventional; destructive git operations require explicit user confirmation.

### 3. Verify on the three platforms when it matters

Platform access is documented in [`docs/playbook/cross-platform-smoke-validation.md` §"Platform Access"](../../docs/playbook/cross-platform-smoke-validation.md). Use it. Do not claim cross-platform success from Windows-only runs.

### 4. Keep the memory honest

If you discover a gap between docs and code that was not caught by the Strategy State Audit, either fix the docs or open an issue. Do not let the misalignment rot silently.

## Output Contract

When you start working, respond in this order:

### 1. Onboarding confirmation

Four short statements proving you completed the §1–§4 onboarding:

- One sentence summarizing the current state of the Packaging module shape and how it relates to Harvesting.
- One sentence summarizing what strategy actually dispatches today per the audit.
- One sentence naming the current active blocker (PA-1 or PA-2 depending on ordering you propose).
- One sentence naming the scoped target you are picking for this session.

### 2. Target justification

Two or three sentences on why the target you picked is the right next step given the open blockers and user priorities. If the user already steered you to a target via the prompt argument, acknowledge it and confirm alignment instead.

### 3. Plan

A short, concrete step list for executing the target. Include verification gates (tests, platform runs, doc updates).

### 4. Execution

Execute the plan. Pause for user confirmation before:

- Anything destructive (git reset, force push, deleting tracked files).
- Any action visible outside the repo (PR creation, issue close, public feed push).
- Any decision that reverses a prior Strategic Decision in [`plan.md`](../../docs/plan.md).

## Style Requirements

- Be direct. Do not pad with meta-commentary about what you're about to do.
- Prefer evidence over vibes. When you say "the code does X," cite the file and line or quote the symbol.
- Match response length to task complexity. Onboarding confirmation is four sentences, not four paragraphs.
- Do not repeat content from this prompt back at the user unless they ask you to.
- If a plan assumption turns out to be wrong mid-execution, stop, surface the mismatch, and propose a re-scope. Do not paper over it.
