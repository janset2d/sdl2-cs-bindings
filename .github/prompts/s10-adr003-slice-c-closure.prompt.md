---
name: "S10 ADR-003 Slice C Closure"
description: "Priming prompt for the next agent closing the ADR-003 implementation pass after Slice C partial (C.1–C.8a) has landed on `feat/adr003-impl`. Remaining Slice C work (C.9 release.yml + C.10 expanded tests + C.11 smoke-witness rewire + C.12 plan-doc ledger + ADR-003 PD-13 closure + doc reference sweep) is next, followed by Slice E. Validate current repo state, continue per the implementation plan, no commits without explicit approval."
argument-hint: "Optional focus area, constraints, or whether implementation is already approved for a specific sub-step"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` mid-pass, after Slices A + B1 + D + DA + B2 + BA + CA + **Slice C partial (C.1–C.8a)** of the ADR-003 Cake refactor have landed. Your default job is to **continue** that pass from where it paused — closing the remaining Slice C sub-steps (C.9 → C.10 → C.11 → C.12), then opening Slice E.

## First Principle

Treat every claim in this prompt as **current-as-of-authoring** and **verify against live repo + git log + plan docs before acting**. Drift is expected between prompt authoring and session start. If something here contradicts what you observe, trust the observation and report the drift to Deniz before proceeding.

Do not force the repo to match this prompt. The partial-Slice-C commit cadence (see Session Lessons §7) is itself a deviation from the locked "one commit per slice" policy, authorised by Deniz mid-pass after C.8a health-check closed green. The next session may encounter similar judgement calls — surface them, don't assume.

## Where the Pass Is

- Active branch: `feat/adr003-impl`, ahead of `master` by **fifteen** commits as of prompt authoring (2026-04-22 evening).
  1. `282b707` — `chore(docs): ADR-003 implementation plan + review prompts`
  2. `36b1cd5` — `slice A: IPackageVersionProvider seam + ExplicitVersionProvider + Cake.CMake spike`
  3. `d484f57` — `slice B1: mapping contract migration + ManifestVersionProvider + ResolveVersions + Cake-native JSON`
  4. `dd54d8e` — `chore(docs): priming prompt for next agent (Slice D continuation)`
  5. `1ef3a82` — `slice D (WIP): NativeSmoke extraction + Cake-native infrastructure + diagnostics + CI first-draft`
  6. `5199d3c` — `slice DA: smoke playbooks Cake-first alignment`
  7. `d96b727` — `slice D witness fix: NativeSmokeTaskRunner CMake build invocation`
  8. `ef49553` — `docs(playbook): PWD env-leakage note in non-interactive cross-shell invocations`
  9. `00bc517` — `slice D closure (D.16): Unix witnesses green, ledger + playbook update`
  10. `45cfd08` — `chore(docs): priming prompt for next agent (Slice B2 continuation)`
  11. `06f9ea4` — `slice B2: graph flatten + SetupLocalDev composition split + PostFlight retire`
  12. `735ccb3` — `slice BA: smoke-witness file-based app (Spectre + local / ci-sim modes)`
  13. `b9b6848` — `docs(plan): record slice B2 + BA progress in §2.1/§6.4.1/§14`
  14. `b0d036e` — `slice CA: witness hardening — TUnit output + MSVC self-sourcing`
  15. `06585fa` — `chore(docs): priming prompt for next agent (Slice C continuation)` — **this is the prompt v1 that Deniz started the Slice C session from; this new file (s10) is the continuation for C.9–C.12**
  16. *new at session-start* — `slice C (partial, C.1–C.8a): per-stage request records + git-tag provider + G58 defense-in-depth + stateless ConsumerSmoke + Janset.Local.props rename` — **verify SHA via `git log --oneline master..HEAD` at session start**
- **Master has NOT been merged** — per Deniz's locked policy the `feat/adr003-impl` branch collects every slice commit and lands on master as **one `git merge --no-ff`** at end of pass (after Slice E). Do **not** merge or push to master without explicit end-of-pass approval.
- Build-host test suite: **418/418 green** on Windows at end of Slice C partial (up from 400 baseline pre-C). Breakdown: +8 record ctor tests (C.1), +5 GitTagVersionProvider integration tests (C.4), +1 git-tag multi-scope rejection test (C.5), +4 G58 scope-contains tests (C.6). 689ms, 0 warnings.
- Windows witness from plain PowerShell (post-C.8 state; known-degraded):
  - `./tests/scripts/smoke-witness.cs local` — 2/3 PASS (CleanArtifacts + SetupLocalDev green; `PackageConsumerSmoke` silent-skips because the task's `ShouldRun` returns false when no `--explicit-version` is supplied). **Fix is C.11 scope:** have `SetupLocalDev` emit `artifacts/resolve-versions/versions.json` (same shape as `ResolveVersions` emits today) as a side-effect; rewire smoke-witness local mode to read the JSON + pass `--explicit-version` on the PackageConsumerSmoke step.
  - `./tests/scripts/smoke-witness.cs ci-sim` — unchanged from pre-C.8: 8/8 PASS (PackageConsumerSmoke still deliberately skipped; C.11 flips this to 9/9).
- Cake task graph is **flat** (post-B2). `dotnet run --project build/_build -- --tree` lists every stage task standalone.
- WSL + macOS Intel witnesses are last-known-green at Slice D/DA close on 2026-04-21; not re-run post-B2/BA/CA/C-partial. Slice C remaining sub-steps are Windows-only per plan §6.5; Slice E re-witnesses all three platforms.

## Your Primary Mission

The ADR-003 pass proceeds as: **A ✓ → B1 ✓ → D ✓ → DA ✓ → B2 ✓ → BA ✓ → CA ✓ → C (partial ✓; remaining: C.9–C.12) → E**.

Default session task: **close Slice C by landing C.9 → C.10 → C.11 → C.12** in one final Slice-C commit on `feat/adr003-impl`, then open Slice E. The earlier "one commit per slice" policy was relaxed to a two-commit split for Slice C only (partial at C.8a, closure at C.12) by Deniz's direction on 2026-04-22 — the closure commit is the one that flips PD-13 to formally closed.

If Deniz explicitly switches you to plan-first mode (`plan first`, `önce planla`) or revises the sub-step order, obey. Default is continue.

## Mandatory Grounding (read these first, in order)

1. `AGENTS.md` — operating rules, approval gates, communication style.
2. `docs/onboarding.md` — strategic decisions + repo layout.
3. `docs/plan.md` — current phase + roadmap.
4. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` — **the execution plan for this pass**, specifically §2.1 Progress log (C partial DONE; C remaining + E not started), §6.5 Slice C scope (C.1–C.8a already landed; C.9–C.12 remain), §6.6 Slice E scope (E1–E3 CI absorption + GHCR container + retire `prepare-native-assets-*.yml`), §11 open questions (**Q17 is the tech-debt capture for the C.3 delegate-hook pattern — must be addressed during Slice E doc-alignment**), §14 change log v2.10 for the C-partial story.
5. `docs/decisions/2026-04-18-versioning-d3seg.md` (ADR-001) — D-3seg versioning + package-first consumer contract.
6. `docs/decisions/2026-04-19-ddd-layering-build-host.md` (ADR-002) — DDD layering + interface three-criteria rule + `LayerDependencyTests` catchnet.
7. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (ADR-003) — three axes, resolve-once, stage-owned validation, Option A SetupLocalDev. **Read the v1.6 Amendment block at the end of §3.3** — it documents the B2 design shift. **PD-13 closure lands in C.12** via the doc update.
8. `docs/playbook/cross-platform-smoke-validation.md` — Cake-first smoke matrix, Last-validated 2026-04-21.
9. `docs/playbook/unix-smoke-runbook.md` — manual Unix witness runbook.
10. `tests/scripts/README.md` + `tests/scripts/smoke-witness.cs` — the file-based witness app. **C.11 extends this** (read both: the script already has a skip-note on PackageConsumerSmoke for `ci-sim` that flips post-C, and `local` mode needs the new versions.json read path).
11. `.github/workflows/release.yml` — Slice-D first-draft skeleton. **C.9 extends it** with real `pack` + `consumer-smoke` matrix jobs.

Then branch to task-specific docs as needed: `release-guardrails.md`, `cake-build-architecture.md`, `phase-2-adaptation-plan.md` (ledger).

## Locked Policy Recap (do not re-debate without cause)

- **Commit policy (§3.3 + 2026-04-22 deviation).** The locked rule is "one local commit per slice on `feat/adr003-impl`, created only when the slice's worktree health check is green." Slice C deviated: a partial commit for C.1–C.8a landed at Deniz's direction, a closure commit for C.9–C.12 lands at session end. No further splits. Do **not** split any remaining sub-step into its own commit. End-of-pass `git merge --no-ff feat/adr003-impl` on master. Not squash — per-slice history stays queryable behind the merge parent.
- **Approval gate (AGENTS.md).** Never commit unless Deniz explicitly approves. Never push. Present diff summary + proposed commit message; wait for "commit" / "onayla" / "yap" / "başla".
- **No temporary bridges, no "retire later" debt (§3.3).** If an abstraction is obsolete by end of the current sub-step, it's removed inside the final Slice-C commit. In particular: the docs reference sweep for `Janset.Smoke.local.props` → `Janset.Local.props` lives in C.12 — don't leave stale references.
- **Cake-native, repo-native (§3.4).** New code uses `ICakeContext.FileSystem`, `IPathService`, Cake's `FilePath` / `DirectoryPath`. CLI wrappers follow the `Tool<TSettings>` / `Aliases` / `Settings` triad. **Zero raw `JsonSerializer.Serialize` / `Deserialize` calls** outside `Build.Context.CakeExtensions`. C.11's `SetupLocalDev`-emits-versions.json addition goes through `_cakeContext.WriteJsonAsync(...)` — same path `ResolveVersionsTaskRunner` already uses.
- **Runner-strict `--explicit-version` (Q5a decision, locked C.8).** `PackageConsumerSmokeRunner` throws on empty `request.Versions`. The `Janset.Local.props` file is a dev-ergonomics artefact for IDE direct-restore only — Cake runners never consume it. Don't revert this.
- **Release tag push is blocked until Slice E closure.** Same as before. Slice C (remaining) does NOT flip this gate — C.9 adds real pack + consumer-smoke job bodies to `release.yml`, but the harvest / native-smoke / consolidate-harvest prereq work still belongs to Slice E (E1–E3). Until Slice E closes, `prepare-native-assets-main.yml` (workflow_dispatch) remains the operator-facing CI harvest path.
- **NativeSmoke is symmetric 7-RID at the matrix layer.** Unchanged. The four PA-2 RIDs fail at `cmake --preset <rid>` naturally until `CMakePresets.json` grows entries in Phase 2b.
- **Caller gates platform, resolver asserts platform (CA pattern).** New platform-specific services follow the `IMsvcDevEnvironment` shape.
- **Merge-commit policy:** end-of-pass `--no-ff` only. Do not squash. Do not merge intermediate slices.

## Slice C Remaining Scope Ledger (reconfirm at session start)

| # | Title | What changes |
| --- | --- | --- |
| **C.9** | **`release.yml` pack + consumer-smoke matrix jobs** | `pack` job: single-runner Ubuntu job, `needs: resolve-versions`; body downloads every harvest artifact, runs `--target ConsolidateHarvest` then `--target Package --explicit-version <mapping>` (mapping threaded from `resolve-versions` via `fromJson(needs.resolve-versions.outputs.versions-json)`); uploads nupkg artifact. `consumer-smoke` job: 7-RID matrix re-entry fanning out on `manifest.runtimes[]` via `generate-matrix` output; each matrix entry downloads the nupkg artifact + invokes `--target PackageConsumerSmoke --rid <rid> --feed-path ./nupkgs --explicit-version <mapping>`. `publish-staging` + `publish-public` stay as the existing stubs — Slice E scope. |
| **C.10** | **Tests** | Expand coverage beyond the C.1–C.7 landing. Focus areas: (a) additional `GitTagVersionProvider` integration cases (detached HEAD behaviour, annotated-tag handling, multiple tags at same commit for same family negative case, missing-tag meta-tag walk on a satellite that was never tagged); (b) G58 additional scenarios (chain depends_on — sdl2-ttf → sdl2-core only; concurrent missing — multiple satellites missing a single core); (c) `PackageConsumerSmokeRunner` strict-mapping guard test (new integration test: empty-mapping request → runner throws with the Slice-C.8 error message); (d) `PackageConsumerSmokeTask.ShouldRun` skip test (new unit test mirroring the existing `PackageTask.ShouldRun` skip test). Target: +12–18 tests; net 418 → ~430–436. |
| **C.11** | **smoke-witness.cs rewire — versions.json from SetupLocalDev** | Two-part change. **(a) `SetupLocalDevTaskRunner`** gains a side-effect: after resolving the local mapping, emit `artifacts/resolve-versions/versions.json` in the same flat-JSON shape `ResolveVersionsTaskRunner` emits today. Reuse the existing `IPathService.GetResolveVersionsOutputFile()` + `CakeExtensions.WriteJsonAsync` helpers (no new paths). **(b) `tests/scripts/smoke-witness.cs`**: rewire both `local` and `ci-sim` modes so each reads the versions.json (both flows now produce it — `local` via SetupLocalDev, `ci-sim` via ResolveVersions) + threads `--explicit-version` into the `PackageConsumerSmoke` step. `local` mode: 3/3 PASS with real smoke coverage. `ci-sim` mode: 9/9 PASS (the post-C.8 skip-note in the script goes away). **Keep** the concrete-families filter the script already applies to the mapping before passing to downstream stages — `sdl2-net` and any other placeholder family still has no managed_project/native_project and must stay filtered. |
| **C.12** | **Plan doc + ADR-003 PD-13 formal closure + reference sweep** | Plan §2.1 Progress log flip "Slice C (remaining: C.9–C.12)" row to DONE + §14 change log v2.11 entry. ADR-003 §6 PD-13 "Direction selected" flips to formally closed (the `--family-version` flag already retired in B1 but the direction-closure gate was the `--explicit-version` mandatory landing in C.8 — document the closure in the ADR). `release-lifecycle-direction.md` + `release-guardrails.md` cross-references updated. **`Janset.Smoke.local.props` → `Janset.Local.props` documentation reference sweep** (plan §11 Q17 addressing + all stale references across `docs/playbook/*.md`, `docs/phases/*.md`, `docs/decisions/*.md`, `docs/knowledge-base/*.md`, `tests/smoke-tests/README.md`, `tests/scripts/README.md`, `README.md`). The partial-commit already updated the code + one docstring comment in `LocalArtifactSourceResolverTests.cs`; C.12 covers the rest. |

**Estimated diff:** ~2–4 new files (C.10 tests mainly), ~6–10 modified files (C.11 witness + SetupLocalDev + various docs in C.12). Test count 418 → ~430–436.

## Execution Order (proposed)

1. **C.9** release.yml pack + consumer-smoke — the CI shape confirms the Slice C work is consumable from CI before we witness locally.
2. **C.11** smoke-witness rewire + SetupLocalDev versions.json — unblocks local witness for end-to-end green.
3. **C.10** test expansion — ride the new infrastructure (temp-git-repo fixture, strict-mapping runner) for the additional coverage.
4. **C.12** plan doc ledger + ADR-003 PD-13 closure + reference sweep — last, because the plan doc needs to reflect every sub-step's final state.

Alternative: interleave C.10 with each of C.9/C.11 so tests land with the code that needs them — this is the usual shape. Either order is acceptable; Deniz will call which he prefers.

## Session Lessons (critical context — carry forward)

These bind the closure session because they define how the partial landing decided things you must maintain.

### 1. Partial-slice commit was a deliberate deviation, not a template

The locked policy (§3.3) is one commit per slice. Slice C partial (C.1–C.8a) landed on `2026-04-22` at Deniz's direction after C.8a health check closed green, because (a) C.9–C.12 are naturally separable CI + test + doc + ledger work, and (b) the eight landed sub-steps form an internally consistent feature (full version resolution + G58 defense-in-depth + runner-strict ConsumerSmoke + props file renamed — a self-contained checkpoint).

**Lesson:** don't propose further splits within the C.9–C.12 closure. Land them as one closure commit. The merge-commit to master at end-of-pass will still show Slice C as two commits behind the merge parent — that's fine, bisectability across the partial / closure boundary is a clean surface.

### 2. Delegate-hook pattern is bounded to `PackageTaskRunner.ResolveHeadCommitSha`

Cake.Frosting.Git bypasses `ICakeContext.FileSystem` (hits `System.IO` via LibGit2Sharp native binary). Unit tests riding `FakeFileSystem` cannot exercise the production resolver; the C.3 session introduced a test-only delegate hook (`Func<ICakeContext, DirectoryPath, string>? resolveHeadCommitSha = null`) on `PackageTaskRunner` ctor with default `GitLogTip` wrapper. Plan §11 Q17 captures this as conscious tech-debt.

`GitTagVersionProvider` took the **integration-test path** instead (real `Repository.Init()` in `Path.GetTempPath()` via `TempGitRepo` fixture in `Fixtures/TempGitRepo.cs`) — no delegate hook there.

**Lesson:** do not propagate the delegate hook to `GitTagVersionProvider` or any other Cake.Git call site in C.10 test expansion. Use the integration-test fixture for any new Cake.Git-touching tests. Q17's resolution happens during Slice E doc-alignment — evaluate then whether to retire the C.3 hook in favour of a uniform temp-repo fixture, or codify the hook as an acceptable pattern for non-mockable third-party boundaries.

### 3. G58 is dual-wired; one implementation, two call sites

`IG58CrossFamilyDepResolvabilityValidator` is injected as a DI singleton into both `PreflightTaskRunner` (mirror check, pre-harvest fail-fast) and `PackageTaskRunner` (pre-pack gate). Same implementation; different failure surfaces (`PreflightReporter.ReportG58CrossFamilyResolvability` renders the per-check UI for PreFlight; PackageTaskRunner logs errors directly + throws a single aggregated CakeException). The feed-probe surface (enum states `OnFeed` / `FeedProbeFailed`) is reserved but not wired.

**Lesson:** don't re-factor G58 into PackageOutputValidator (post-pack). It is a pre-pack gate by design — failing the invocation before producing garbage nupkgs. If C.10 adds more G58 tests, keep the tests scoped to the scope-contains behaviour; feed-probe remains Phase 2b.

### 4. `Janset.Local.props` is an IDE artefact, not a runner input

Post-C.8a, `build/msbuild/Janset.Local.props` (renamed from `Janset.Smoke.local.props`) is broadened from smoke-only to repo-wide local-feed override. `LocalArtifactSourceResolver` stamps it during `SetupLocalDev --source=local`; `Janset.Smoke.props` still auto-imports it conditionally for smoke csprojs; future samples / AST tests / sandboxes can opt in via the same import pattern without inventing parallel mechanisms.

**Cake runners do NOT consume this file.** The `--explicit-version` CLI contract is the single source of truth for stage invocations. Runner-strict via `PackageConsumerSmokeRunner`'s empty-mapping guard (C.8) + `PackageConsumerSmokeTask.ShouldRun` skip-with-log (C.8) enforces this.

**Lesson:** C.11's `SetupLocalDev`-emits-versions.json side-effect does NOT replace or retire `Janset.Local.props` — the two coexist. versions.json is for scripts (smoke-witness, future CI helpers that need to parse the mapping); Janset.Local.props is for IDE direct-restore of consumer csprojs. Don't collapse them.

### 5. `Targeted`/`Train` naming locked in `GitTagScope`

Sum-type cases use `GitTagScope.Targeted(familyId)` / `GitTagScope.Train` — matches the release-vocabulary in `docs/knowledge-base/release-lifecycle-direction.md` ("targeted release per-family" / "full-train release"). Initial draft used `Single`/`Multi` — rejected by analyzer CA1720 (`Single` conflicts with `System.Single`) and by semantic clarity.

**Lesson:** preserve this naming in any future CLI surface / doc mentions. If C.10 adds `GitTagVersionProvider` tests, use `new GitTagScope.Targeted("sdl2-image")` / `new GitTagScope.Train()` consistently.

### 6. `BuildContext.Runtime` landed; no new Task→Domain-service seams in C.9+

Slice C.2 added `IRuntimeProfile Runtime` to `BuildContext` so Task classes can construct per-stage request records from context without injecting `IRuntimeProfile` directly (which would violate ADR-002 §2.8 invariant 3). Any new Task class in C.9–C.12 should follow the same pattern: read from `context.Runtime` / `context.Paths` / `context.Vcpkg`, construct the request inline, pass to the runner.

**Lesson:** if C.10 test fixtures need runtime info, they go through `FakeRepoBuilder.BuildContextWithHandles()` → `context.Runtime` (fixture stubs IRuntimeProfile with `Rid`=configured or default `win-x64`). Do not add new IRuntimeProfile injections to Task classes.

### 7. `ShouldRun` skip-with-log is the canonical empty-mapping shape

`PackageTask.ShouldRun` and `PackageConsumerSmokeTask.ShouldRun` both return `false` with an informational log message when `PackageBuildConfiguration.ExplicitVersions` is empty. This is the standard "operator forgot --explicit-version" surface — do not replace with runner-throws or silent passes.

**Lesson:** if C.9/C.11 touches other Task classes whose behaviour depends on the resolved mapping, apply the same `ShouldRun` pattern with a log line that points at the SetupLocalDev / resolve-versions flow.

### 8. Smoke-witness local mode is temporarily degraded

Post-C.8, `./tests/scripts/smoke-witness.cs local` silent-skips the `PackageConsumerSmoke` step because the Task's `ShouldRun` returns false without `--explicit-version`. C.11 is the fix.

**Lesson:** do not proceed to C.10 / C.12 without closing C.11 first if Deniz wants a green witness on every sub-step boundary. The natural order is C.9 → C.11 → C.10 → C.12.

## What NOT to Do (Failure Modes To Avoid)

- **Don't merge to master.** End-of-pass only.
- **Don't squash.** `git merge --no-ff feat/adr003-impl` preserves slice commits.
- **Don't split C.9–C.12 into multiple commits.** One closure commit for the remaining sub-steps.
- **Don't introduce raw `JsonSerializer.Serialize` / `Deserialize` anywhere outside `CakeExtensions`.** C.11's `SetupLocalDev` versions.json emission uses `_cakeContext.WriteJsonAsync(...)`.
- **Don't break `LayerDependencyTests`.** New Task classes read from `BuildContext.*` properties, not Domain-service DI.
- **Don't re-introduce the `Janset.Smoke.local.props`-as-runner-fallback branch in ConsumerSmoke.** Runner-strict `--explicit-version` is locked.
- **Don't propagate the delegate-hook pattern beyond `PackageTaskRunner.ResolveHeadCommitSha`.** `GitTagVersionProvider` + any new Cake.Git test call sites use the `TempGitRepo` integration fixture.
- **Don't commit plan-doc drift silently.** C.12 closes the ledger — every code change must have a corresponding plan-doc line by commit time.
- **Don't touch `cross-platform-smoke-validation.md` or `unix-smoke-runbook.md` unless C.9/C.11 changes operator-visible behaviour.** The C.12 sweep covers references; operator-visible command-line changes in C.9 (release.yml Cake invocations) surface via the CI workflow, not the playbooks.
- **Don't expand Slice E scope into Slice C.** Slice E owns the `prepare-native-assets-*.yml` absorption + GHCR container + publish-stub + cross-platform witness. If C.9's release.yml pack/smoke work surfaces a prereq gap (apt packages, vcpkg bootstrap, etc.), log it for Slice E and move on — don't fix it in C.9.

## Communication Preferences

- Deniz communicates in Turkish + English interchangeably. Answer in whichever language he used, or a mix.
- Narrate findings + propose + wait for ack before commit/push/close. No chaining from discovery to action.
- Be direct. Challenge decisions when needed; don't be yes-man.
- When in doubt, stop and ask. "No scope creep on critical findings" per memory — if a C.9–C.12 diff uncovers a cross-slice concern, isolate the current delta and file the concern as a Slice E backlog item.

## Definition of Done for Your Session

### Default (close Slice C, open Slice E kickoff)

Progress Slice C forward from C.9 to C.12, closing with:

- Each remaining sub-step (C.9 → C.11 → C.10 → C.12, or Deniz-preferred order) lands in the working tree; build + test green after each material group.
- Worktree health check green at close:
  - `dotnet build build/_build` green.
  - `dotnet test build/_build.Tests` green; test count 418 → ~430–436.
  - `LayerDependencyTests` green.
  - `--tree` flat-graph check.
  - `./tests/scripts/smoke-witness.cs local` **3/3 PASS** on Windows (PackageConsumerSmoke actually runs with versions threaded from SetupLocalDev).
  - `./tests/scripts/smoke-witness.cs ci-sim` **9/9 PASS** on Windows (PackageConsumerSmoke flips from skip to run).
- Plan doc §2.1 Progress log + §14 Change log v2.11 + Slice C closure ledger updated in the same commit.
- ADR-003 §6 PD-13 formally closed.
- `Janset.Smoke.local.props` → `Janset.Local.props` reference sweep complete across all canonical docs.
- One commit on `feat/adr003-impl` after Deniz approves with the proposed message shape (multi-paragraph, subsection-grouped, Co-Authored-By).
- Slice E kickoff checklist primed in-conversation (E1 reusable-workflow dispatch, E2 GHCR Ubuntu container, E3 retire `prepare-native-assets-*.yml`, E4–E5 Publish stubs, broader doc sweep including Q17 addressing).

### Optional (pass close)

If you reach Slice E and close it green on all three platforms (Windows + WSL + macOS Intel SSH) per `cross-platform-smoke-validation.md` A–K checkpoints, the pass is ready to merge. **Still wait for Deniz's explicit merge approval.** Then `git checkout master && git merge --no-ff feat/adr003-impl` (not squash), and report the merge commit SHA.

Do not leave `feat/adr003-impl` in a half-closure state without a visible handoff note.
