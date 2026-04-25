---
name: "S16 Phase 2b pickup (post-green release.yml confirmation)"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the 2026-04-26 post-push all-green `release.yml` run 24938451364 on master `8ec85c5`. Slice E remains closed, the smoke docs were refreshed in `8ec85c5`, and the recommended next step is to turn the new green run into a clean Phase 2b witness/roadmap decision instead of reopening closed CI-polish work. This supersedes s15 for sessions that start after the post-push green run."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` after the **post-push green `release.yml` confirmation on 2026-04-26**. The repo is no longer at the Slice E closure checkpoint described by `s15`; it is one step later:

- **Current master HEAD:** `8ec85c5` — `docs: align live CI and smoke validation docs with current behavior`
- **Previous baseline:** `8634b2d` — Slice E follow-up pass closure + broader Plan A doc-sweep audit
- **Latest full CI confirmation:** `release.yml` run `24938451364`, green across all 7 RIDs after `8ec85c5` was pushed

This prompt supersedes `.github/prompts/s15-phase-2b-tail-kickoff.prompt.md` for sessions that start **after** that green run. Read `s15` only if you need the detailed archaeology of what the Slice E follow-up pass closed.

## First Principle

Treat every claim here as **current-as-of-authoring (2026-04-26)** and verify against the live repo, git log, and canonical docs before acting.

The codebase is in a relatively clean post-closure state:

- Slice E is closed.
- The post-closure doc alignment commit is already on master.
- CI has now gone green again on that updated master.

That means the highest-value next move is **not** reopening closed CI-polish work. The highest-value move is to turn the new evidence into a clean Phase 2b pickup decision: either close/narrow the PA-2 witness gap in docs, or prove exactly what evidence is still missing.

If docs and code disagree, trust the code, then either fix the doc or note the drift visibly. Follow the same archive rule used in the Plan A doc-sweep audit: if a doc is more than roughly half amendment surface, archive to `docs/_archive/<name>-<YYYY-MM-DD>.md` and rewrite in place; otherwise prefer surgical edits.

This repo still runs **master-direct commits** per Deniz direction. Do not create a branch unless there is a concrete reason.

## What Just Happened

Since the `s15` handoff, the repo picked up one additional, meaningful checkpoint:

1. A strict Windows clean-slate validation run was executed locally.
   - `CleanArtifacts` confirmed to preserve `vcpkg_installed/` while leaving `artifacts/temp` and `artifacts/test-results`, which were then manually removed to satisfy the stricter empty-artifacts expectation.
   - `dotnet restore build/build.sln --locked-mode`
   - `dotnet build build/_build/Build.csproj -c Release --no-restore`
   - `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --no-restore -v minimal`
   - Result: `460/460` green.
2. `tests/scripts/smoke-witness.cs` was rerun from inside `tests/scripts` on Windows.
   - `local --verbose`: `3/3 PASS`, `99.5s`
   - `ci-sim --verbose`: `9/9 PASS`, `129.0s`
   - `NativeSmoke`: `29/29`
   - `PackageConsumerSmoke`: `12 + 12 + 11 = 35/35`
3. That exposed two live-doc drifts, both fixed in `8ec85c5`.
   - `docs/playbook/cross-platform-smoke-validation.md` now says `NativeSmoke` is `29/29`, not `28/28`.
   - `tests/scripts/README.md` now says `ci-sim` includes `PackageConsumerSmoke`, which matches current behavior.
4. After the commit was pushed, the user ran `release.yml` again.
   - `release.yml` run `24938451364` is green.
   - This is the new top-of-stack CI evidence and should be treated as the current baseline unless contradicted by the repo or GitHub state.

## Onboarding Snapshot

This repo is still the packaging/build/distribution layer for **modular C# SDL2 bindings first, SDL3 later**, with **native libraries built from source via vcpkg** and shipped through **NuGet**.

The build backbone remains:

- `.NET 9 / C# 13`
- `Cake Frosting` build host under `build/_build/` (DDD-layered per ADR-002)
- `vcpkg` for native builds (custom `*-hybrid` overlay triplets)
- `GitHub Actions` for the RID matrix + release pipeline + builder image
- `build/manifest.json` schema v2.1 as the single source of truth

Locked decisions that are still not open for casual re-debate:

- **Hybrid static + dynamic core** packaging model
- **Triplet = strategy** (no standalone `--strategy` CLI flag)
- **Package-first consumer contract** (ADR-001)
- **Cake owns orchestration policy; YAML stays thin**
- **7 RID coverage remains in scope**
- **LGPL-free codec stack** for SDL2_mixer

Target RIDs (canonical in `build/manifest.json runtimes[]`):

- `win-x64` / `win-x86` / `win-arm64`
- `linux-x64` / `linux-arm64`
- `osx-x64` / `osx-arm64`

## Current State You Should Assume Until Verified

- **Master HEAD:** `8ec85c5`
- **Worktree expectation:** likely clean, since the user said commit + push were already done
- **Build-host tests:** `460/460` green
- **Latest CI evidence:** `release.yml` run `24938451364` green on `8ec85c5`
- **Latest local witness evidence:** still the 2026-04-25 Windows + WSL walkthrough captured in the playbook; no new WSL rerun was performed after the green CI because the user explicitly deprioritized it
- **Publish path:** still stubbed (`PublishStaging` / `PublishPublic` gated `if: false`)
- **Remote artifact source:** still stubbed (`SetupLocalDev --source=remote` not implemented)

## What Changed In Canonical Docs After s15

The following surfaces now point at the newer post-push state and should be treated as the freshest top-level entry points:

- `docs/plan.md` — last-updated line now reflects the 2026-04-26 green run
- `docs/onboarding.md` — What Works Today now points at master `8ec85c5`
- `docs/playbook/cross-platform-smoke-validation.md` — latest validation header now distinguishes the 2026-04-26 CI confirmation from the 2026-04-25 local walkthrough

The deeper Slice E closure narrative remains in the same places as before:

- `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`
- `docs/phases/phase-2-adaptation-plan.md`
- `s15-phase-2b-tail-kickoff.prompt.md`

## Recommended Next Step

### Recommended pickup: formalize or narrow the PA-2 witness gap using the new green CI run

This is the most sensible next move from the docs, and it is the one you should default to unless Deniz redirects you.

Why this is the right pickup now:

- The docs still describe **PA-2 behavioural validation** as pending.
- The repo now has stronger evidence than `s15` had: a newer all-green 7-RID run on the current post-doc-alignment master.
- That makes the next job a **decision-and-evidence** task, not a blind implementation task.

Your job is to determine which of these is true:

1. The new run `24938451364` is enough to treat PA-2 behavioural validation as satisfied, so the canonical docs should be updated.
2. The new run is helpful but still does **not** satisfy the playbook's “formal witness” bar, in which case the docs should say exactly what evidence is still missing.

Do **not** hand-wave this. Verify what the docs currently require, compare that to the actual evidence trail, and then either close the gap or describe the residual gap precisely.

## Recommended Workflow For The Next Agent

1. Read the mandatory grounding below in order.
2. Run `git log --oneline -20` and confirm `8ec85c5` is still HEAD.
3. Inspect the canonical docs that still frame PA-2 behavioural validation as pending.
   - `docs/plan.md`
   - `docs/phases/phase-2-adaptation-plan.md`
   - `docs/playbook/cross-platform-smoke-validation.md`
   - `docs/onboarding.md`
4. Compare those claims against the current evidence set.
   - local Windows witness
   - local WSL witness from the prior session
   - CI run `24938451364`
5. Make one of two moves:
   - **If the evidence is sufficient:** update the canonical docs to say so, with explicit wording about what was validated and on which run.
   - **If the evidence is still insufficient:** update the docs to state the exact remaining witness requirement, not the old vague “pending” wording.
6. Only after that, pick the next code-facing Phase 2b item.

## If The PA-2 Witness Gap Closes Cleanly

If you confirm that the new run is enough and the docs are updated cleanly, the next likely work item is:

### Next code-facing candidate: `RemoteArtifactSourceResolver`

Why this is the best likely follow-on:

- `PublishTaskRunner` depends on real feed/auth decisions and has more operational coordination overhead.
- `RemoteArtifactSourceResolver` is self-contained, clearly stubbed, and already called out as active Phase 2b tail work.
- It advances the repo without reopening the publish/auth can of worms too early.

So the default sequence should be:

1. settle PA-2 witness docs
2. then move toward `SetupLocalDev --source=remote`

If Deniz explicitly wants release publication instead, switch to `PublishTaskRunner` as the primary pickup.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `docs/plan.md`
4. `docs/phases/phase-2-adaptation-plan.md`
5. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`
6. `docs/decisions/2026-04-18-versioning-d3seg.md`
7. `docs/decisions/2026-04-19-ddd-layering-build-host.md`
8. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md`
9. `.github/workflows/release.yml`
10. `.github/workflows/build-linux-container.yml`
11. `.github/actions/{vcpkg-setup,nuget-cache,platform-build-prereqs}/action.yml`
12. `build/manifest.json`
13. `src/native/_shared/Janset.SDL2.Native.Common.targets`
14. `build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs`
15. `build/_build/Infrastructure/DotNet/DotNetRuntimeEnvironment.cs`

Live-state snapshots:

- `docs/playbook/cross-platform-smoke-validation.md`
- `docs/playbook/unix-smoke-runbook.md`
- `docs/knowledge-base/cake-build-architecture.md`
- `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`

Historical archaeology only when needed:

- `.github/prompts/s15-phase-2b-tail-kickoff.prompt.md`
- `docs/_archive/`

## Locked Policy Recap

These still do not change without explicit Deniz override:

- **Master-direct commits**
- **No commit without approval**
- **Cake remains the policy owner; YAML stays thin**
- **Native asset layout contract lives in `Janset.SDL2.Native.Common.targets` comments**
- **Lock-file strict mode stays scoped to the build host**
- **Do not reintroduce workflow-side win-x86 runtime bootstrap hacks**
- **Do not resurrect stale `runner-os` shape in `vcpkg-setup`**
- **If docs and code disagree, fix the canonical doc in the same change**
- **Commit message style**: short subject + 4-8 short bullets, not prose paragraphs

## Final Steering Note

Do not open the next session by proposing five parallel futures. This repo already has a documented tail. Start with the smallest high-confidence move that converts the new green run into durable project state.

Right now, that means:

- verify whether the 2026-04-26 green run closes the PA-2 witness gap
- update the docs accordingly
- then move to `RemoteArtifactSourceResolver` unless Deniz chooses a different Phase 2b item
