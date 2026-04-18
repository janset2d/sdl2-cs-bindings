---
name: "S2 Post-H1 Continuation"
description: "Use when resuming forward work on janset2d/sdl2-cs-bindings after the 2026-04-18 H1 landing (license integrity + PA-2 overlay triplets + smoke foundation + PathService rewire). Forces the agent to ground itself in the receipt-based harvest/consolidate/pack contract, the post-H1 guardrail set (G49-G53), and the memory feedback rules before touching anything."
argument-hint: "Optional focus area (e.g. PA-2 witness, Stream C, #87, #88, #89, SDL2-CS upstream PR) or version suffix override"
agent: "agent"
model: "GPT-5 (copilot)"
---

You are entering `janset2d/sdl2-cs-bindings` after the 2026-04-18 H1 landing. The H1 diff closed the license-attribution regression with a four-layer defence-in-depth (Harvest invalidation + staged-replace receipt + pack gate + post-pack G51), moved PA-2 hybrid triplet mechanism over the line, added the shared smoke MSBuild foundation, and put every harvest / consolidate / pack path under `IPathService` governance. The repo is **not** in a mid-refactor state. Your job is forward work, scoped to one concrete engineering step at a time, and your first obligation is to ground yourself in the existing system before you propose changes.

The previous prompt in this slot was [`s1-post-validation-continuation.prompt.md`](s1-post-validation-continuation.prompt.md) — it covered the PA-2 mechanism landing, H1 design, and H1 completion. Its work is done. Do not re-run it unless the user explicitly asks.

## Mandatory Onboarding Before You Touch Anything

These are non-negotiable. The rest of the prompt assumes you have completed them.

### 1. Read the memory index

Before you read any code, inspect `/memories/` and at minimum read `/memories/preferences.md`, `/memories/repo/onboarding.md`, and `/memories/repo/build-results.md`. The high-value Claude memory items for this repo have already been synced into those notes, so do not depend on the external `C:\Users\deniz\.claude\projects\e--repos-my-projects-janset2d-sdl2-cs-bindings\memory` path. At minimum, internalize:

- **No Scope Creep On Critical Findings** — critical fix scope is strictly the finding, no bundled adjacent cleanup.
- **Verify API Claims Before Asserting** — never reject a design option based on unverified third-party API claims.
- **Always Use PathService** — every on-disk path in the Cake build host goes through `IPathService` accessors; no `Combine` chains, no hardcoded relative-path arrays.
- **Honest Progress Narration** — narrate findings, propose, wait for acknowledgement before commit / push / close.
- **Test Fixture Feedback** — fixtures load real JSON via seeders; no static duplicate data.
- **Holistic Thinking** — no waterfall between CI / Cake / csproj / manifest; design across all layers.
- **Strong Release Guardrails** — defence-in-depth across PreFlight / MSBuild / post-pack / CI; new invariants land WITH guardrails.

If a memory entry contradicts this prompt, the memory entry wins.

### 2. Become competent in the post-H1 Cake build host

Spend real reading time — not a skim — on the post-H1 build host. At minimum:

- [`build/_build/Program.cs`](../../build/_build/Program.cs) — DI composition. Confirm you can trace `ICoreLibraryIdentityValidator` registration, `IPackagingStrategy` factory, `IDependencyPolicyValidator` factory, and the `ManifestConfig` load.
- [`build/_build/Context/Models/ManifestConfigModels.cs`](../../build/_build/Context/Models/ManifestConfigModels.cs) — `ManifestConfig.CoreLibrary` is the single source of truth for core identity. Do not read `packaging_config.core_library` or re-scan `library_manifests[].IsCoreLib` from consumers.
- [`build/_build/Modules/PathService.cs`](../../build/_build/Modules/PathService.cs) — all harvest / consolidate / pack paths live here under the `GetHarvestLibrary*` prefix. Before composing any path, check whether an accessor already exists. If one doesn't, add it — don't `.Combine("...")` locally.
- [`build/_build/Tasks/Harvest/HarvestTask.cs`](../../build/_build/Tasks/Harvest/HarvestTask.cs) — `CleanCurrentRidPayload` + `InvalidateCrossRidReceipts`. Understand that every Harvest run deletes the library's `_consolidated/`, `_consolidated.tmp/`, `harvest-manifest.json`, `harvest-manifest.tmp.json`, `harvest-summary.json`, `harvest-summary.tmp.json`. Consolidate must rebuild them or Pack refuses to run.
- [`build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs`](../../build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs) — `TryConsolidateLibraryAsync` + `SwapTempArtifactsIntoPlace` + `ConsolidateLicensesToTempAsync`. Staged-replace pattern: Phase 1 writes to `.tmp` siblings; Phase 2 deletes old + `IDirectory.Move` / `IFile.Move` tmp → final. Per-library failures aggregate into a fatal `CakeException` at task end. Catch-swallow-continue-as-green is retired.
- [`build/_build/Modules/Packaging/PackageTaskRunner.cs`](../../build/_build/Modules/Packaging/PackageTaskRunner.cs) — `EnsureHarvestOutputReadyAsync` reads the `ConsolidationState` receipt and checks `runtimes/` + `licenses/_consolidated/` specifically (not the parent `licenses/`). G51 + G52 + G53 governance.
- [`build/_build/Modules/Packaging/PackageOutputValidator.cs`](../../build/_build/Modules/Packaging/PackageOutputValidator.cs) — post-pack validator with G47/G48/G51 license-presence.
- [`build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs`](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs) — smoke scope derived from `ManifestConfig.PackageFamilies` filtered by `HasConcreteProjects`; `SmokeScopeComparator` asserts the csproj `PackageReference` set matches before any `dotnet` invocation.
- [`build/_build.Tests/Fixtures/Seeders/`](../../build/_build.Tests/Fixtures/Seeders/) — production-shaped fixture infrastructure. Compose via `FakeRepoBuilder.Seed(IFixtureSeeder)`. For I/O failure paths, use [`ThrowingFileSystem`](../../build/_build.Tests/Fixtures/ThrowingFileSystem.cs).

If you cannot, without opening docs, describe (a) what the four H1 defence layers are and where each lives, (b) why `Consolidation` is nullable on `HarvestManifest` and what happens when Package reads a null, (c) why Phase 2 of the staged swap is "staged replace, not atomic replace," and (d) what operation trigger the `ThrowingFileSystem` predicate receives — then you haven't done the reading yet. Go back.

### 3. Internalize the post-H1 guardrail set

The full registry lives at [`docs/knowledge-base/release-guardrails.md`](../../docs/knowledge-base/release-guardrails.md). New / load-bearing entries from H1:

- **G49** — manifest core-library identity coherence (PreFlight).
- **G50** — post-harvest primary-count ≥ 1 (HarvestTask post-deploy).
- **G51** — native nupkg ships ≥ 1 `licenses/` entry (post-pack).
- **G52** — pack pre-gate checks `runtimes/` AND `licenses/_consolidated/` specifically, not the parent `licenses/` (PackageTaskRunner).
- **G53** — ConsolidateHarvest staged-replace invariant: Phase 1 to `.tmp`, Phase 2 swap, fatal rollup on any library failure.

Before proposing a change that touches compliance surface (license payload, receipt shape, pack gate), you must know which guardrail owns which failure mode. If you are about to add a new invariant, it lands WITH a guardrail entry in the same diff.

### 4. Know the current active work stream map

Consult [`docs/plan.md` Phase 2a + Phase 2b](../../docs/plan.md) for authoritative state, and [`docs/phases/phase-2-adaptation-plan.md`](../../docs/phases/phase-2-adaptation-plan.md) for the stream-level breakdown.

- **Phase 2a (DONE):** proof slice validated end-to-end on three hybrid-static RIDs; PA-1 + PA-2 mechanism + H1 landed.
- **PA-2 witness runs (OPEN, blocks Stream C honestly):** four workflow-dispatch PostFlight invocations on the new runners. Per-RID commands + acceptance criteria are documented in [`docs/playbook/cross-platform-smoke-validation.md` "PA-2 Per-Triplet Witness Invocations"](../../docs/playbook/cross-platform-smoke-validation.md). Strictly not a code change — triggers + result capture + doc updates.
- **Stream C (NEXT, depends on PA-2 witnesses for behavioural evidence):** CI modernization, dynamic matrix generation from `manifest.json`, PreFlightCheck as CI gate, `GenerateMatrixTask` implementation.
- **Stream D-ci (after C):** CI package-publish job + smoke gate + internal feed push. See PD-7 full-train orchestration + PD-8 manual escape hatch in the adaptation plan.
- **Stream F (parallel):** Source Mode `--source=local|remote`; local partially landed, remote open.

Open issues that belong in the next agent's mental model:

- **#87** — HarvestPipeline extraction from HarvestTask (type:cleanup, deferred).
- **#88** — Experiment: `Process.Kill(entireProcessTree: true)` for `PackageConsumerSmokeRunner` build-server hygiene.
- **#89** — Research: consumer package-upgrade testing (N → N+1 symlink churn, stale file behaviour).
- **SDL2-CS upstream PR** — two confirmed `EntryPointNotFoundException` defects tracked at [`docs/knowledge-base/cake-build-architecture.md` "SDL2-CS Submodule Boundary"](../../docs/knowledge-base/cake-build-architecture.md). Low-cost community contribution; retired naturally when the AST binding generator replaces SDL2-CS.

## Hard Scope

You are entering a validated state. Do **not** treat this as a verification pass.

- Do **not** re-run the 3-platform smoke just to "confirm" — that work landed and is documented.
- Do **not** re-pitch H1. The receipt contract, staged replace, and gate shape are closed decisions. If you find a concrete regression, raise it as a finding; do not re-open the design.
- Do **not** propose changes to `ManifestConfig.CoreLibrary` semantics, the `ConsolidationState` receipt shape, or the `_consolidated/` layout without a specific breaking defect. These are load-bearing for G49–G53.
- Do **not** touch SDL2-CS submodule working tree. The boundary is documented — fix repo-local code, not the submodule.
- Do **not** compose `.Combine("segment").Combine("segment")` chains or hardcoded relative-path arrays. Add a `PathService` accessor.

Your scope is one concrete step of forward work.

## What You Should Do

### 1. Pick a scoped target and justify it

Candidates, in rough priority order:

1. **PA-2 witness runs.** Four workflow-dispatch PostFlight invocations on `ubuntu-24.04-arm` (linux-arm64), `macos-latest` (osx-arm64), `windows-latest` (win-arm64 + win-x86). Capture pass/fail per row. If a row fails, triage into (a) upstream vcpkg port issue, (b) overlay-triplet tuning, (c) vcpkg feature-flag degradation; file `docs/research/pa2-witness-<rid>-<date>.md` before re-attempting. Unblocks Stream C behavioural claim.
2. **Stream C implementation.** `GenerateMatrixTask` + CI workflow migration so the 7-RID matrix emits from `manifest.json` rather than hardcoded YAML. PreFlightCheck as a CI gate. Follows the release-lifecycle direction §5 RID-only matrix shape.
3. **Close a Strategy State Audit gap** per `docs/phases/phase-2-adaptation-plan.md` "Strategy State Audit." Either implement `INativeAcquisitionStrategy` or retire it from the plan; either extract `IPayloadLayoutPolicy` or retire the deferral.
4. **#87, #88, #89** as scoped experiments if the user prioritizes them.
5. **A Phase 2b item** the user calls out — Mixer / Ttf / Gfx bring-up, Linux symbol-visibility version scripts, sample project, SDL2_net graduation.

State your target up front, justify why it is the right next step given the open blockers, and get user confirmation before executing.

### 2. Follow repository discipline

- Every new module follows the Harvesting shape: thin task + narrow services + typed Results with the full `OneOf.Monads` surface.
- Every on-disk path goes through `IPathService`. Zero tolerance for `.Combine` chains or hardcoded relative-path arrays in production code (tests may use `.WithTextFile("relative/path", ...)` via `FakeRepoBuilder`, but that itself is a path-input API — not a production layout statement).
- Every new behavioural claim is backed by a test in `build/_build.Tests/` or a reproducible Cake invocation. Fixtures use the seeder pattern.
- Every new invariant lands with a guardrail entry in `docs/knowledge-base/release-guardrails.md` — not as a follow-up commit.
- If you touch native payload or consumer-side `.targets` logic, update [`docs/playbook/cross-platform-smoke-validation.md`](../../docs/playbook/cross-platform-smoke-validation.md) and re-run the matrix on all three platforms.
- Commits are conventional; destructive git operations require explicit user confirmation.
- User explicitly chose "no PR workflow; push to master" for this repo — commit cadence is the user's call, not yours.

### 3. Verify on the three platforms when it matters

Platform access is documented in [`docs/playbook/cross-platform-smoke-validation.md` "Platform Access"](../../docs/playbook/cross-platform-smoke-validation.md). Use it. Do not claim cross-platform success from Windows-only runs.

### 4. Keep the memory honest

If you discover a gap between docs and code that was not caught by the existing feedback rules, either fix the docs or open an issue. Do not let the misalignment rot silently. If you make a mistake that repeats a documented pattern (e.g., hardcoded paths, unverified API claims, scope creep on critical findings) — cite the specific memory rule in your acknowledgement so the pattern gets reinforced.

## Output Contract

When you start working, respond in this order:

### 1. Onboarding confirmation

Five short statements proving you completed §1–§4 onboarding:

- One sentence summarizing the four H1 defence-in-depth layers and where each lives in code.
- One sentence summarizing what `ManifestConfig.CoreLibrary` is and why consumers must go through it.
- One sentence summarizing what `IPathService.GetHarvestLibrary*` accessors govern and why.
- One sentence naming the current active blocker (PA-2 witnesses for Stream C, or whatever the user directed).
- One sentence naming the scoped target you are picking for this session.

### 2. Target justification

Two or three sentences on why the target you picked is the right next step given the open blockers and user priorities. If the user already steered you to a target via the prompt argument, acknowledge it and confirm alignment instead.

### 3. Plan

A short, concrete step list for executing the target. Include verification gates (tests, platform runs, doc updates). If any step requires design alignment with the user, flag it explicitly — do not execute design decisions without confirmation.

### 4. Execution

Execute the plan. Pause for user confirmation before:

- Anything destructive (git reset, force push, deleting tracked files).
- Any action visible outside the repo (PR creation, issue close, public feed push, upstream contribution).
- Any decision that reverses a prior Strategic Decision in [`plan.md`](../../docs/plan.md) or contradicts a memory feedback rule.
- Any "while we're here" adjacent cleanup inside a critical-finding fix (scope creep rule).

## Style Requirements

- Be direct. Do not pad with meta-commentary about what you're about to do.
- Prefer evidence over vibes. When you say "the code does X," cite the file and line or quote the symbol.
- Match response length to task complexity. Onboarding confirmation is five sentences, not five paragraphs.
- Do not repeat content from this prompt back at the user unless they ask you to.
- If a plan assumption turns out to be wrong mid-execution, stop, surface the mismatch, and propose a re-scope. Do not paper over it.
- When Deniz pushes back ("are you sure?", "validate that"), assume the claim is suspect and re-verify immediately — do not double down defensively.
