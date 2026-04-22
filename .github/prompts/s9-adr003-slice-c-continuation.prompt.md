---
name: "S9 ADR-003 Slice C Continuation"
description: "Priming prompt for the next agent picking up the ADR-003 implementation pass after Slices A + B1 + D + DA + B2 + BA + CA have landed on `feat/adr003-impl`. Slice C (per-stage request records + GitTagVersionProvider + G58 + stateless ConsumerSmoke) is next. Validate current repo state, answer the locked open questions, continue per the implementation plan, no commits without explicit approval."
argument-hint: "Optional focus area, constraints, or whether implementation is already approved for a specific slice"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` mid-pass, after Slices A + B1 + D + DA + B2 + BA + CA of the ADR-003 Cake refactor have landed. Your default job is to **continue** that pass from where it paused — not to re-plan, not to restart, not to merge to master.

## First Principle

Treat every claim in this prompt as **current-as-of-authoring** and **verify against live repo + git log + plan docs before acting**. Drift is expected between prompt authoring and session start. If something here contradicts what you observe, trust the observation and report the drift to Deniz before proceeding.

Do not force the repo to match this prompt. The Slice B2 review cycle (see Session Lessons §1) is a textbook reminder that a priming prompt can itself point at the wrong design — live judgment beats literal execution.

## Where the Pass Is

- Active branch: `feat/adr003-impl`, ahead of `master` by **fourteen** commits as of prompt authoring (2026-04-21 evening):
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
- **Master has NOT been merged** — per Deniz's locked policy the `feat/adr003-impl` branch collects every slice commit and lands on master as **one `git merge --no-ff`** at end of pass (after Slice E). Do **not** merge or push to master without explicit end-of-pass approval.
- Build-host test suite: **400/400 green** on Windows at end of Slice CA (up from 390 baseline pre-B2 — B2 added 8, BA added 0 directly, CA added 2).
- Windows witness from plain PowerShell (no Developer shell required post-CA):
  - `./tests/scripts/smoke-witness.cs local` — 3/3 PASS in 114.7s (`CleanArtifacts` → `SetupLocalDev` → `PackageConsumerSmoke`; TUnit per-TFM pass counts visible in the tee'd log: `net9.0`: 12 passed, `net8.0`: 12 passed, `net462`: 11 passed).
  - `./tests/scripts/smoke-witness.cs ci-sim` — 8/8 PASS in 81.3s (`CleanArtifacts` → `ResolveVersions` → `PreFlightCheck` → `EnsureVcpkgDependencies` → `Harvest` → `NativeSmoke` → `ConsolidateHarvest` → `Package`; NativeSmoke 13s via `IMsvcDevEnvironment` self-sourcing; `PackageConsumerSmoke` deliberately skipped — Slice C flips this).
- Cake task graph is **flat** (Slice B2 removed every `[IsDependentOn]` chain). `dotnet run --project build/_build -- --tree` lists every stage task standalone. No `PostFlight`.
- WSL + macOS Intel witnesses were green at Slice D/DA close on 2026-04-21 morning, not re-run post-B2/BA/CA. Slice C health check is Windows-only per plan §6.5; Slice E re-witnesses all three platforms.

## Your Primary Mission

The ADR-003 pass proceeds as: **A ✓ → B1 ✓ → D ✓ → DA ✓ → B2 ✓ → BA ✓ → CA ✓ → C → E**.

Default session task: **continue with Slice C (per-stage request records + GitTagVersionProvider + G58 + stateless ConsumerSmoke + `release.yml` pack+consumer-smoke matrix wiring)**, landing as one commit on `feat/adr003-impl`. Slice E follows.

Slice C is **large** (B1-scale, ~12 sub-steps). Start by answering Q1–Q5 below with Deniz before any code — those five answers determine the shape of records, provider API, and CLI surface. And add your recommendation for each if you have one. Do your recommendations based on best practices and consistency with the existing codebase, not on implementation effort. Once you have the green light on the design, implement per the proposed C.1–C.12 sequence in the plan doc §2.1, with the following rules:

If Deniz explicitly switches you to plan-first mode for C (`plan first`, `önce planla`) or revises the slice order, obey. Default is continue.

## Mandatory Grounding (read these first, in order)

1. `AGENTS.md` — operating rules, approval gates, communication style.
2. `docs/onboarding.md` — strategic decisions + repo layout.
3. `docs/plan.md` — current phase + roadmap.
4. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` — **the execution plan for this pass**, specifically §2.1 Progress log (B2/BA/CA DONE, C/E not started), §6.4 Slice B2 summary (reference for the composition-split design), §6.4.1 Slice BA (smoke-witness app), §6.4.2 Slice CA (MSVC self-sourcing), §6.5 Slice C scope (the target for this session), §6.6 Slice E scope (expanded 2026-04-21 with E1–E3 for `prepare-native-assets-*.yml` absorption + custom GHCR Ubuntu builder container), §7 inventory, §8 test methodology, §11 open questions including Q16 (custom Ubuntu builder container), §14 change log v2.7–v2.9 for the B2/BA/CA story.
5. `docs/decisions/2026-04-18-versioning-d3seg.md` (ADR-001) — D-3seg versioning + package-first consumer contract.
6. `docs/decisions/2026-04-19-ddd-layering-build-host.md` (ADR-002) — DDD layering + interface three-criteria rule + `LayerDependencyTests` catchnet.
7. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (ADR-003) — three axes, resolve-once, stage-owned validation, Option A SetupLocalDev. **Read the v1.6 Amendment block at the end of §3.3** — it documents the B2 design shift (composition out of resolver, into `SetupLocalDevTaskRunner`).
8. `docs/playbook/cross-platform-smoke-validation.md` — Cake-first smoke matrix, Last-validated 2026-04-21 (Windows plain PowerShell note post-CA).
9. `docs/playbook/unix-smoke-runbook.md` — manual Unix witness runbook; the smoke-witness.cs script is now the default path.
10. `tests/scripts/README.md` + `tests/scripts/smoke-witness.cs` — the file-based app that automates the witness flow. **Read the script** — you will extend it in C.11 (flip `ci-sim` PackageConsumerSmoke from skip → run once the stateless runner + `--feed-path` + `--explicit-version` CLI surface lands).

Then branch to task-specific docs as needed: `release-guardrails.md`, `cake-build-architecture.md`, `phase-2-adaptation-plan.md` (ledger).

## Locked Policy Recap (do not re-debate without cause)

- **Commit policy (§3.3).** One local commit per slice on `feat/adr003-impl`, created only when the slice's worktree health check is green. No mid-slice pushes. End-of-pass `git merge --no-ff feat/adr003-impl` on master. Not squash — per-slice history stays queryable behind the merge parent. Slice C lands as one commit; internal sub-progress tracked in the plan doc §2.1 sub-checklist, not split commits.
- **Approval gate (AGENTS.md).** Never commit unless Deniz explicitly approves. Never push. Present diff summary + proposed commit message; wait for "commit" / "onayla" / "yap" / "başla".
- **No temporary bridges, no "retire later" debt (§3.3).** If an abstraction is obsolete by end of the current slice, it's removed inside that slice's commit. Slice C is the slice that lands `Janset.Smoke.local.props` retirement (if Q5 answer = mandatory `--explicit-version`) + its reference sweep — inside this slice, not deferred to E.
- **Cake-native, repo-native (§3.4).** New code uses `ICakeContext.FileSystem`, `IPathService`, Cake's `FilePath` / `DirectoryPath`. CLI wrappers follow the `Tool<TSettings>` / `Aliases` / `Settings` triad. **Zero raw `JsonSerializer.Serialize` / `Deserialize` calls** outside `Build.Context.CakeExtensions`. The Git command wrapper in C.3 adds a new `Infrastructure/Git/` surface — follow the dumpbin/vcpkg/cmake/tar precedent.
- **Cake target CLI: `--explicit-version <family>=<semver>` only.** `--family` / `--family-version` retired in B1. Any legacy reference in smoke / PA-2 / playbook docs outside `docs/_archive/` + change logs is stale.
- **Release tag push is blocked until Slice E closure.** The Slice D first-draft `release.yml` `harvest` / `native-smoke` / `consolidate-harvest` jobs lack the prereq + cache + submodule discipline of the `prepare-native-assets-*.yml` family (apt packages + `freepats` + autoconf 2.72 source build + `vcpkg-setup` composite + submodule recursion + NuGet workspace cache + Cake-compile-once + `--repo-root` + `harvested-assets-{os}-{rid}` naming). Slice E E1 absorbs the family via reusable-workflow dispatch; E2 moves the Linux apt preamble into a custom GHCR-hosted Ubuntu builder image; E3 retires-or-narrows the legacy per-platform workflows. Operator-facing CI harvest remains `prepare-native-assets-main.yml` (workflow_dispatch) until E1–E3 land.
- **NativeSmoke is symmetric 7-RID at the matrix layer.** No hard-coded RID allow-list in `NativeSmokeTaskRunner` (removed during Slice D). The four PA-2 RIDs (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) will fail naturally at `cmake --preset <rid>` until `tests/smoke-tests/native-smoke/CMakePresets.json` grows entries in Phase 2b. That expected-failure surface IS the PA-2 witness signal, not a code-level cap.
- **Caller gates platform, resolver asserts platform (CA pattern).** New platform-specific services follow the same shape as `IMsvcDevEnvironment`: caller guards on `OperatingSystem.IsWindows()` for intent visibility; resolver throws `PlatformNotSupportedException` on contract violation. Do not collapse the redundancy — the two-layer defence is intentional.
- **Merge-commit policy:** end-of-pass `--no-ff` only. Do not squash. Do not merge intermediate slices.

## Slice C Scope Ledger (proposed — reconfirm with Deniz after Q1–Q5)

| # | Title | What changes |
| --- | --- | --- |
| C.1 | **Per-stage request records** | 7 records under `Domain/<Module>/Models/`: `PreflightRequest`, `HarvestRequest`, `NativeSmokeRequest`, `ConsolidateHarvestRequest`, `PackRequest`, `PackageConsumerSmokeRequest`, `PublishRequest` (last is stub for Phase 2b consumption). Shapes per ADR-003 §3.2. |
| C.2 | **Runner `RunAsync(TRequest, CT)` cascade** | 7 runners accept Request record; old DI-state-driven overloads retire (Q3 = full cut-over) + cascade into `SetupLocalDevTaskRunner` / `LocalArtifactSourceResolver` / tests. |
| C.3 | **`Infrastructure/Git/IGitCommandRunner` + `GitCommandRunner`** | Cake-native wrapper for `git describe` / `git rev-parse` / `git tag -l`. Interface seam under ADR-002 criterion 2 (process-invocation vs pure-parse axis of change). |
| C.4 | **`GitTagVersionProvider`** | Single class with `GitTagScope.Single(FamilyId)` / `Multi(IReadOnlySet<FamilyId>)` param. Parses `sdl<N>-<role>-<semver>` tags at a given commit; meta-tag walks `manifest.package_families[]` + `depends_on` topo-sort (per-family tag discovery at meta-tag commit). G54 upstream-coherence check per entry. |
| C.5 | **ResolveVersions `git-tag` + `meta-tag` wiring** | `ResolveVersionsTaskRunner` branches on `--version-source=git-tag` / `meta-tag`. Tag-format validation + commit-resolution + emitted-JSON shape unchanged (flat dict). |
| C.6 | **`G58CrossFamilyDepResolvabilityValidator`** | `Domain/Packaging/` with scope-contains check (mandatory) + optional feed-probe (Q2). Satellite family's `depends_on` entries must either be in scope OR reachable on target feed. |
| C.7 | **G58 wiring into Pack stage** | `PackageOutputValidator` invokes G58 as part of post-pack suite (per ADR-003 §4 validation ownership). |
| C.8 | **`PackageConsumerSmokeRunner` stateless** | Accepts `PackageConsumerSmokeRequest(Rid, Versions, FeedPath)`. `IRuntimeProfile` becomes host-RID default with request override. `Janset.Smoke.local.props` retires (Q5 = mandatory `--explicit-version`). Smoke csproj flow unchanged for local-dev `SetupLocalDev` path — the runner stamps a transient props file OR passes `-p:LocalPackageFeed=... -p:JansetSdl<N><Role>PackageVersion=...` per MSBuild invocation (implementation detail, decide in C.8). |
| C.9 | **`release.yml` pack + consumer-smoke matrix jobs** | `pack` job single-runner `--target Package --explicit-version <mapping>` via `needs: resolve-versions`. `consumer-smoke` job 7-RID matrix re-entry, each runner downloads nupkg artifact + invokes `--target PackageConsumerSmoke --rid <rid> --feed-path ./nupkgs --explicit-version <mapping>`. Publish stays stub. |
| C.10 | **Tests** | Per-provider unit (git-tag happy path + G54 reject + meta-tag discovery + scope ordering); G58 scenarios (in-scope / missing-lower-bound / missing-family / feed-probe on+off); consumer-smoke request shape; Request record validation. Expected +25–35 tests. Target: 400 → ~425–435. |
| C.11 | **smoke-witness.cs `ci-sim` PackageConsumerSmoke flip** | Script's `ci-sim` mode stops skipping `PackageConsumerSmoke`. Invoke with `--rid <host-rid> --feed-path <repo>/artifacts/packages --explicit-version <resolved-mapping>` (mapping from the ResolveVersions JSON the script already parses). Target: 8/8 → 9/9 PASS on Windows. |
| C.12 | **Plan doc + ADR-003 PD-13 formal closure** | Plan §2.1 Slice C row flip to DONE + §14 v2.10 entry. ADR-003 §6 PD-13 "Direction selected" flips to formally closed once `Janset.Smoke.local.props` retirement lands. `release-lifecycle-direction.md` + `release-guardrails.md` cross-references updated. |

**Estimated diff:** ~15–25 new files, ~15–20 modified files, ~25–35 new tests. Test count 400 → ~425–435.

## Open Questions — Blocker for C.1 (Deniz to answer first)

These five were surfaced at C kickoff. Script-cloned verbatim from the previous session so they reach the next agent intact. Do not proceed to C.1 without Deniz's answers.

**Q1. Meta-tag format.** Full-train multi-family release trigger.
- Option: `train-<YYYYMMDD>` (date-driven, readable in CI matrix) — **previous session's recommendation**
- Option: `release-<N>` (monotonic sequence)
- ADR-003 §6 PD-7 "Direction selected" but format unspecified.

**Q2. G58 feed-probe default.**
- Option (a): scope-contains always runs (mandatory); feed-probe **opt-in** when `--feed <URL>` is supplied. **Previous session's recommendation.** No network dependency in unit tests.
- Option (b): scope-contains + feed-probe against `nuget.org` default always. More protective but network-bound unit tests become flaky.

**Q3. Runner signature dual-period.** When adding Request-accepting overload:
- Option (a): old overload retires (breaking). Cleaner; cascading update into `SetupLocalDevTaskRunner` / `LocalArtifactSourceResolver` / tests within the same commit (B2 precedent). **Previous session's recommendation.**
- Option (b): both overloads live. Old one constructs Request from DI config + delegates to new. Easier transition but carries debt.

**Q4. `PackageConsumerSmoke --feed-path` default.** Flag required or optional?
- Option (a): default `artifacts/packages`, flag overrides. **Previous session's recommendation.** CI always passes explicit path; local dev enjoys the default.
- Option (b): flag required on every invocation. Stricter but uglier UX.

**Q5. `--explicit-version` mandatory for `PackageConsumerSmoke`?**
- Option (a): mandatory on every invocation. `Janset.Smoke.local.props` retires; single canonical flow; mini CI sim and `local` mode both drive through explicit mapping. **Previous session's recommendation.**
- Option (b): optional; `Janset.Smoke.local.props` fallback remains for backward-compat. Two code paths, drift risk.

## Execution Order (proposed)

1. **C.1** records → **C.2** runner signature cascade (biggest diff surface; do this first so every downstream coord happens against stable shapes).
2. **C.3** → **C.4** → **C.5** (git-tag axis, isolated from Request records except for `PackRequest` consumer).
3. **C.6** → **C.7** (G58 domain + wiring; isolated).
4. **C.8** (ConsumerSmoke stateless; depends on C.1 + C.2).
5. **C.9** (release.yml pack + smoke jobs).
6. **C.10** (tests — written alongside each sub-step, aggregated here as a bookkeeping marker).
7. **C.11** smoke-witness `ci-sim` flip (witness validates C.1 + C.2 + C.8 + C.9 wire correctly).
8. **C.12** plan doc + ADR-003 PD-13 formal closure.

## Session Lessons (critical context from B2 + BA + CA sessions — carry forward)

These are patterns and decisions that surfaced during the B2→CA arc. They bind Slice C because they define how to reason through decisions, not what to build.

### 1. Priming prompts can point at the wrong design — live review wins

The Slice B2 priming prompt instructed a resolver-centric composition: `LocalArtifactSourceResolver.PrepareFeedAsync` becomes the orchestrator, calling Preflight/EnsureVcpkg/Harvest/NativeSmoke/Consolidate/Pack in sequence. The previous agent executed this literally — 11-param ctor, all runners injected, tests exploded in scaffolding cost. Deniz's review rejected it on two grounds:
- "Resolver" is a semantic lie when the class *produces* artifacts from scratch (a resolver consumes an existing feed).
- Forcing `NativeSmokeTaskRunner` onto the local-dev path makes CMake + MSVC toolchain a prereq for managed-only iteration.

The refactor that ran extracted a new `SetupLocalDevTaskRunner` (`Application/Packaging/`) owning the composition; the resolver narrowed to verify-feed + stamp-props; `NativeSmoke` left the local-dev chain entirely. ADR-003 §3.3 gained a **v1.6 Amendment** documenting the shift.

**Lesson:** when the prompt and your own reading disagree, narrate the disagreement to Deniz before committing. He'd rather amend the ADR than ship an honest-but-wrong composition. The commit message pattern that emerged — "initial reading → review → amended design → code + ADR amendment in same commit" — is worth preserving.

### 2. Ergonomic gaps surface organically — fold them in, don't scope-creep

Two micro-slices (BA + CA) appeared post-B2 because the witness ergonomics failed operator use:
- **BA:** After B2 the full-smoke path was `CleanArtifacts → SetupLocalDev → PackageConsumerSmoke`, three Cake invocations. Deniz asked for a single-command witness. Result: `tests/scripts/smoke-witness.cs` file-based .NET 10 app with `local` + `ci-sim` subcommands.
- **CA:** BA surfaced two friction points — `PackageConsumerSmoke`'s TUnit pass/fail summary was hidden at `_log.Verbose`, and `NativeSmoke` required a Developer PowerShell on Windows. Result: `echoStdout` plumbing + `IMsvcDevEnvironment` self-sourcing (same pattern as `DumpbinTool.TryResolveFromDeveloperPrompt` — VSWhere + `vcvarsall.bat` parse + env-delta merge into `ToolSettings.EnvironmentVariables`).

**Lesson:** the commit-per-slice rule isn't strict when an adjacent concern genuinely blocks the next slice. BA was justified because Slice C would otherwise inherit the three-invocation witness pattern; CA was justified because NativeSmoke on CI Windows matrix would fail the same way. Each landed as its own commit with its own witness + doc update. Don't jam these into C just because the plan doc enumerates C in the sequence — ask Deniz if a pre-C micro-slice is warranted when you hit operational friction.

### 3. Deniz's decision patterns (observed, not prescribed)

- **Caller-side platform gate + resolver defensive throw.** When `IMsvcDevEnvironment` first returned `Empty` on non-Windows, Deniz said: make it explicit. Caller asserts `OperatingSystem.IsWindows()` and short-circuits before even awaiting; resolver throws `PlatformNotSupportedException` on contract violation. The two-layer redundancy is intentional — call-site intent visibility + defence in depth. Apply the same shape to any new platform-specific resolver in C (e.g., if `IGitCommandRunner` ends up with a platform branch).
- **"Dumpbin'in path'ini bulduğumuza benzer bir mekanizma"** — Deniz intuits patterns from existing code. Before proposing a new approach, check whether a neighbour (DumpbinTool, VcpkgBootstrapTool, etc.) already solved a structurally similar problem. `MsvcDevEnvironment` is literally `DumpbinTool`'s shape extended from "locate one .exe" to "locate + source a whole env".
- **Authenticity over convenience in ci-sim.** When the resolver-centric composition rewrite broke `PackageConsumerSmoke` in ci-sim mode (because it relied on `Janset.Smoke.local.props` which only `SetupLocalDev` stamps), the tempting fix was "script stamps the props itself". Deniz rejected that: "script'e custom şeyler yazdığımız anda kardeşim artık simulasyon yapmıyoruz". The honest move is `ci-sim` skips `PackageConsumerSmoke` with a clear "post-Slice-C flip" note until the runner is parameterized. Slice C is where that flip happens.
- **CPM-native over scope isolation.** When `#:package Spectre.Console@*` conflicted with repo-root CPM, the first instinct was `tests/scripts/Directory.Packages.props` with CPM disabled. Deniz shut it down: "Olm tabi Spectre ı root Directory.Packages a ekleyeceğiz neden olmadık işler yapıyorsun saçma". CPM is repo-wide policy; new packages land in root `Directory.Packages.props` with a `PackageVersion` entry, script uses `#:package <Name>` sans version.
- **Directory.Build.props inheritance stays.** "Directory.Build.props'dan alsın herşeyi zararı yok" — only specific compat edges get `NoError` overrides (mirroring `Build.csproj`'s `CA1050;CA2007;...` pattern). For Slice C's new records, don't invent isolation; inherit and add targeted suppressions only when the analyzer surface is genuinely misapplied (e.g., `CA1502`/`CA1505` on file-based app `<Main>$`).
- **File-based apps use `System.IO` directly.** BA's first cut used shell `tee` for log capture; Deniz reminded that `File.WriteAllTextAsync` is cleaner and cross-platform. Apply to any script-side tooling in C.

### 4. Analyzer suite patterns

- `VSTHRD011` (Microsoft.VisualStudio.Threading.Analyzers) flags `Lazy<Task<T>>` as deadlock-risk. Replace with a simple nullable field + reference-type atomic write (Cake host is single-threaded in practice; pathological concurrent resolve does one extra unit of work).
- `MA0045` (Meziantou.Analyzer) forces async over sync I/O. Convert `Process.WaitForExit` → `WaitForExitAsync`, `StreamReader.ReadToEnd` → `ReadToEndAsync`. No sync-over-async cheats (`.GetAwaiter().GetResult()` still triggers warning in many contexts).
- `CA1502` + `CA1505` on file-based apps: top-level statements + static local functions all roll up into `<Main>$` complexity. Suppress via `#:property NoError=$(NoError);CA1502;CA1505` + `#:property NoWarn=...` rather than forcing type refactor into a non-idiomatic shape.
- TUnit `Assert.That(() => asyncFn()).Throws<T>()` has nullability quirks with `Task<IReadOnlyDictionary<,>>`. Use `Assert.That(async () => await asyncFn()).Throws<T>()` for explicit awaiting to satisfy the `T?` inference.
- `IDisposable` complaint on `SemaphoreSlim` owner: the simpler fix is usually to remove the semaphore (single-threaded use case) rather than make the owner disposable. Apply to concurrency in general — measure before locking.

### 5. Commit message style

Multi-paragraph, grouped by subsection (scope / tests / witness / docs / CI impact). Co-Authored-By trailer for Claude. Example from CA:

```text
slice CA: witness hardening — TUnit output + MSVC self-sourcing

Two operational gaps Slice BA surfaced closed before Slice C begins:

(1) PackageConsumerSmoke TUnit output was hidden ...
(2) NativeSmoke on Windows halted ...

Tests: ...
Witness (Windows, plain PowerShell — Developer shell no longer needed): ...
Docs: ...
CI impact (future): ...

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

Witness numbers in commit messages (`3/3 PASS in 114.7s`, `net9.0: 12 passed`) are load-bearing evidence — they're what Deniz checks against when re-reading the history.

### 6. Plan doc ledger discipline

Every slice commit touches `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md`:
- §2.1 Progress log row (new entry with SHA + one-paragraph scope recap + witness numbers).
- §6.x authored section (scope + health check + close-line) if the slice introduces new shape; reference existing §6.x for continuations.
- §14 Change log v-N.M entry (narrative commentary on the slice's design decisions, including what was rejected and why).

Doc drift in other playbooks (cross-platform-smoke-validation, unix-smoke-runbook, local-development) gets surgical drift fixes in the same slice commit when the slice changes operator-visible behaviour. Broader playbook restructures defer to Slice E per plan §6.6.

## What NOT to Do (Failure Modes To Avoid)

- **Don't merge to master.** End-of-pass only. Even if everything looks green after C, `git push` / `git merge master` without explicit Deniz approval is a hard boundary.
- **Don't squash.** `git merge --no-ff feat/adr003-impl` preserves slice commits behind the merge parent — that's the deliberate bisect surface.
- **Don't start C.1 without Q1–Q5 answers.** Five questions shape the records, provider API, and CLI surface. Guessing wrong means refactor cost. Ask first.
- **Don't pre-empt Slice E's CI rewrite or container pivot.** `release.yml` body changes in C are the pack job + consumer-smoke matrix job — **not** the per-platform reusable-workflow dispatch or the GHCR container. E1–E3 absorb that. If you think a broader YAML change is needed, flag it and check with Deniz before editing.
- **Don't introduce raw `JsonSerializer.Serialize` / `Deserialize` anywhere outside `CakeExtensions`.** B1 closed this surface; BA + CA kept it closed (smoke-witness.cs uses `JsonDocument.ParseAsync` which is allowed at the script-boundary layer, not inside the build host). Add new helpers to `CakeExtensions` if the existing surface does not cover your need.
- **Don't break `LayerDependencyTests`.** Post-B2 the target shape is enforced (no Task → Domain/Infrastructure interface refs). C.1 records live under `Domain/<Module>/Models/` — allowed by `Tasks → Domain.<Module>.Models.*` canonical permanent permission (ADR-002 §2.8 invariant 3). If a violation surfaces, it is real drift — fix the layering, don't suppress the test.
- **Don't touch `unix-smoke-runbook.md` or `cross-platform-smoke-validation.md` unless C actually changes operator-visible behaviour.** The B2 + CA drift fixes are fresh. If C retires `Janset.Smoke.local.props` (Q5 = mandatory `--explicit-version`), the playbook notes mentioning it need a single-line past-tense update, not a full re-sweep. Broader playbook restructure is Slice E scope.
- **Don't commit plan-doc drift silently.** Plan doc is part of the slice ledger. When C lands, update §2.1 Progress log + §14 Change log v2.10 + the Slice C sub-checklist in the same commit as the C code.
- **Don't re-introduce the resolver-as-orchestrator pattern B2 rolled back.** If Slice C's request records make `IArtifactSourceResolver.PrepareFeedAsync(context, versions, ct)` feel like a natural spot to add pipeline orchestration: **don't**. Composition stays in `SetupLocalDevTaskRunner`. The resolver verifies and stamps. ADR-003 §3.3 v1.6 Amendment is binding.

## Communication Preferences

- Deniz communicates in Turkish + English interchangeably. Answer in whichever language he used, or a mix.
- Narrate findings + propose + wait for ack before commit/push/close. No chaining from discovery to action.
- Be direct. Challenge decisions when needed; don't be yes-man.
- When in doubt, stop and ask. "No scope creep on critical findings" per memory — if a C diff uncovers a cross-slice concern, isolate the C delta and file the concern as a Slice E backlog item OR propose a pre-C micro-slice (BA/CA precedent).

## Definition of Done for Your Session

### Default (continue Slice C → Slice E)

Progress Slice C forward per the plan, closing with:

- Q1–Q5 answered by Deniz, scope reconfirmed, execution order confirmed.
- Each sub-step (C.1–C.12) lands in the working tree incrementally; build + test green after each material group.
- Worktree health check green at close: `dotnet build build/_build` + `dotnet test build/_build.Tests` + `LayerDependencyTests` + `--tree` flat-graph check + `./tests/scripts/smoke-witness.cs ci-sim` **9/9 PASS** on Windows (PackageConsumerSmoke flips from skip to run).
- Plan doc §2.1 Progress log + §14 Change log v2.10 + Slice C sub-checklist updated in the same commit.
- One commit on `feat/adr003-impl` after Deniz approves with the proposed message shape (multi-paragraph, subsection-grouped, Co-Authored-By).
- Slice E kickoff checklist primed in-conversation (E1 reusable-workflow dispatch, E2 GHCR Ubuntu container, E3 retire `prepare-native-assets-*.yml`, E4–E5 Publish stubs, broader doc sweep).

### Optional (pass close)

If you reach Slice E and close it green on all three platforms (Windows + WSL + macOS Intel SSH) per `cross-platform-smoke-validation.md` A–K checkpoints, the pass is ready to merge. **Still wait for Deniz's explicit merge approval.** Then `git checkout master && git merge --no-ff feat/adr003-impl` (not squash), and report the merge commit SHA.

Do not leave `feat/adr003-impl` in a half-slice state without a visible handoff note.
