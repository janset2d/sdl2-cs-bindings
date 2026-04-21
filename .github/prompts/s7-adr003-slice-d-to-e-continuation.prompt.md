---
name: "S7 ADR-003 Slice D→E Continuation"
description: "Priming prompt for the next agent picking up the ADR-003 implementation pass mid-stream: Slices A + B1 already committed on `feat/adr003-impl`; Slice D (NativeSmoke + Cake-native polish) is next. Validate current repo state, continue per the implementation plan, no commits without explicit approval."
argument-hint: "Optional focus area, constraints, or whether implementation is already approved for a specific slice"
agent: "agent"
model: "GPT-5 (copilot)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` mid-pass, after Slices A + B1 of the ADR-003 Cake refactor have landed. Your default job is to **continue** that pass from where it paused — not to re-plan, not to restart, not to merge to master.

## First Principle

Treat every claim in this prompt as **current-as-of-authoring** and **verify against live repo + git log + plan docs before acting**. Drift is expected between prompt authoring and session start. If something here contradicts what you observe, trust the observation and report the drift to Deniz before proceeding.

Do not force the repo to match this prompt.

## Where the Pass Is

- Active branch: `feat/adr003-impl`, ahead of `master` by three commits as of prompt authoring:
  1. `chore(docs): ADR-003 implementation plan + review prompts`
  2. `slice A: IPackageVersionProvider seam + ExplicitVersionProvider + Cake.CMake spike`
  3. `slice B1: mapping contract migration + ManifestVersionProvider + ResolveVersions + Cake-native JSON` *(long body; the single B1-closure commit)*
- **Master has NOT been merged** — per Deniz's locked policy the `feat/adr003-impl` branch collects all slice commits and lands on master as **one `git merge --no-ff`** at end of pass (after Slice E). Do **not** merge or push to master without explicit end-of-pass approval.
- Build-host test suite: 355/355 green at end of B1.
- End-to-end `SetupLocalDev --source=local --rid win-x64` validated on Windows host in 47s — 15 nupkgs at per-family D-3seg versions (sdl2-core `2.32.0-local...`, sdl2-gfx `1.0.0-local...`, sdl2-image/mixer `2.8.0-local...`, sdl2-ttf `2.24.0-local...`) + `build/msbuild/Janset.Smoke.local.props` written. This is the first time the codebase runs end-to-end with true per-family D-3seg versions.
- Cake task graph intact (chain-based) — `PostFlight → PackageConsumerSmoke → Package → PreFlightCheck` + `SetupLocalDev → ConsolidateHarvest → Harvest → EnsureVcpkgDependencies → Info`. Graph flatten is **Slice B2 scope**, not B1.

## Your Primary Mission

The ADR-003 pass proceeds as: **A ✓ → B1 ✓ → D → B2 → C → E**.

Default session task: **continue with Slice D (NativeSmoke extraction + Cake-native polish + diagnostics + early Linux witness)**, landing as one commit on `feat/adr003-impl`. Slices B2, C, E follow in sequence.

If Deniz explicitly switches you to plan-first mode for D (`plan first`, `önce planla`) or revises the slice order, obey. Default is continue.

## Mandatory Grounding (read these first, in order)

1. `AGENTS.md` — operating rules, approval gates, communication style.
2. `docs/onboarding.md` — strategic decisions + repo layout.
3. `docs/plan.md` — current phase + roadmap.
4. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` — **the execution plan for this pass**, specifically §2.1 Progress log, §6.3 Slice D scope, §6.4 Slice B2 scope, §6.5 Slice C scope, §6.6 Slice E scope, §7 inventory, §8 test methodology, §11 open questions + locked cleanup items.
5. `docs/decisions/2026-04-18-versioning-d3seg.md` (ADR-001) — D-3seg versioning + package-first consumer contract.
6. `docs/decisions/2026-04-19-ddd-layering-build-host.md` (ADR-002) — DDD layering + interface three-criteria rule + `LayerDependencyTests` catchnet.
7. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (ADR-003) — three axes, resolve-once, stage-owned validation, Option A SetupLocalDev.
8. `docs/playbook/cross-platform-smoke-validation.md` — the cross-platform spec Slice E validates against.

Then branch to task-specific docs as needed: `release-guardrails.md`, `cake-build-architecture.md`, `phase-2-adaptation-plan.md` (ledger).

## Locked Policy Recap (do not re-debate without cause)

- **Commit policy (§3.3).** One local commit per slice on `feat/adr003-impl`, created only when the slice's worktree health check is green. No mid-slice pushes. End-of-pass `git merge --no-ff feat/adr003-impl` on master. Not squash — per-slice history stays queryable behind the merge parent.
- **Approval gate (AGENTS.md).** Never commit unless Deniz explicitly approves. Never push. Present diff summary + proposed commit message; wait for "commit" / "onayla" / "yap" / "başla".
- **No temporary bridges, no "retire later" debt (§3.3).** If an abstraction is obsolete by end of the current slice, it's removed inside that slice's commit.
- **Cake-native, repo-native (§3.4, Deniz direction locked during B1).** New code uses `ICakeContext.FileSystem`, `IPathService`, Cake's `FilePath` / `DirectoryPath`. CLI wrappers follow the `Tool<TSettings>` / `Aliases` / `Settings` triad. **Zero raw `JsonSerializer.Serialize` / `Deserialize` calls** outside `Build.Context.CakeExtensions`. New file I/O routes through the Cake extensions (`WriteJsonAsync`, `ToJsonAsync`, `WriteAllTextAsync`, `ReadAllTextAsync`, `SerializeJson`, `DeserializeJson`). If you need a new Cake helper, add it to `CakeExtensions` with XML doc; do not sprinkle raw BCL JSON calls.
- **Stage independence is Slice B2 scope.** In Slices D + B1 the task graph stays chain-based (`[IsDependentOn]` links kept). Slice B2 flattens them and rewrites `SetupLocalDev` resolver composition to include every runner internally. Do not pre-empt B2's flatten inside D.
- **NativeSmoke 3-RID cap this pass.** `win-x64` / `linux-x64` / `osx-x64` only — `tests/smoke-tests/native-smoke/CMakePresets.json` has presets for exactly these three. The four PA-2 RIDs (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) are Phase 2b work and do not land in this pass.
- **`Cake.CMake` 1.4.0 binding is PROVEN** (Slice A spike). Use it directly from `NativeSmokeTaskRunner`; no repo-local `CMakeTool` fallback needed.
- **`SharpCompress` is the tar-extraction choice for `Inspect-HarvestedDependencies`** (§11 open question closed 2026-04-21).
- **Merge-commit policy:** end-of-pass `--no-ff` only. Do not squash. Do not merge intermediate slices.

## Open Cleanup Items Carried into Later Slices

- **`PackageFamilySelector` inlining into `PackageTaskRunner`** (§11). After B1, the selector has exactly one production consumer (`PackageTaskRunner.RunAsync`) + one docstring reference. Candidate for inline-and-retire at Slice B2 open (once the resolver composition rewrite is the only remaining selector caller; if graph-flatten plus resolver rewrite leaves zero external consumers, inline the topological-sort helper into `PackageTaskRunner` or a static `FamilyTopologyHelpers`, and retire the interface + impl + `PackageFamilySelectionResult` + `PackageFamilySelection` model). Criterion-2 check before retiring: re-scan after flatten; if a new consumer appears, keep.
- **`NativePackageMetadataValidator` `ICakeContext` injection** (not yet logged in plan). After B1's Deserialize migration, the validator still uses `IFileSystem`-only ctor and calls the static `CakeExtensions.DeserializeJson`. That's acceptable per the "static helper for Infrastructure-layer readers" direction; no further change needed unless the validator grows to need other context-bound extensions.
- **`ConsolidateHarvestTask` vestigial default-ctor** (§7.1 B2 retire list) — kept for B1 closure because the DI path is already primary; removal lands in Slice D per plan.
- **`DependentsTask` vestigial `ToolPath = context.Tools.Resolve(...)` line** (§7.1 B2 retire list) — also Slice D.

## Slice D Kickoff Checklist

Per plan §6.3 (with Deniz's 2026-04-21 direction on early WSL witness):

1. **Baseline** — `dotnet test build/_build.Tests -c Release` green at 355/355 (or current baseline). If not, stop and triage — something drifted.
2. **`Cake.CMake` PackageReference** already in `Directory.Packages.props` + `Build.csproj` since Slice A. Remove `build/_build/Infrastructure/Tools/CMake/_CakeCmakeSpike.cs` (spike placeholder; retire when real CMake tooling lands).
3. **`SharpCompress` PackageReference** — add via `dotnet add package SharpCompress` (CPM-aware).
4. **NativeSmoke primary surface:**
   - `Build.Application.Harvesting.NativeSmokeTaskRunner` — CMake configure/build via Cake.CMake; native-smoke executable invocation via a lean process wrapper (preferably a `NativeSmokeRunnerTool : Tool<NativeSmokeRunnerSettings>` under `Infrastructure/Tools/NativeSmoke/`, following the Vcpkg/Dumpbin/Ldd/Otool pattern). Precondition check: harvest output for current RID + preset exists for current RID.
   - `Build.Tasks.Harvest.NativeSmokeTask` — thin adapter (golden: `PackageTask`). Graph: `[IsDependentOn(HarvestTask)]` *within the B1-era chain*; `ConsolidateHarvestTask [IsDependentOn(NativeSmokeTask)]` replaces its existing `[IsDependentOn(HarvestTask)]` edge.
5. **Early WSL witness (Deniz direction 2026-04-21).** Before adding the diagnostic utility tasks, run the Linux witness: on WSL, `--target CleanArtifacts` (if `CleanArtifacts` hasn't landed yet, `rm -rf` the artifact subtrees you need) → `--target SetupLocalDev --source=local --rid linux-x64` → standalone `--target NativeSmoke --rid linux-x64`. If Cake.CMake / `NativeSmokeRunnerTool` / preset invocation breaks on Linux, catch it with the smallest-possible moving-parts set. Triage before layering more code.
6. **Diagnostic + utility tasks (after the witness passes):**
   - `CleanArtifacts` task — wipes `artifacts/{harvest_output,packages,package-consumer-smoke,test-results/smoke}` + native-smoke build dirs. Cake-native via `ICakeContext.DeleteDirectory`.
   - `Inspect-HarvestedDependencies --rid <rid>` task — SharpCompress extraction of harvest tarballs into `artifacts/temp/inspect/<rid>/<lib>/`, per-library primary binary resolution, invoke platform scanner (`Dumpbin-Dependents` / `Ldd-Dependents` / `Otool-Analyze` aliases) on extracted binary, log dep set. Replaces `docs/playbook/TEMP-wsl-smoke-commands.md` §5 bash `inspect_ldd` loop.
   - `CompileSolution` task — thin `DotNetBuild` alias wrapper on `Janset.SDL2.sln`. Replaces WSL playbook §8 solution-build step.
7. **Cleanup in the same slice:**
   - `ConsolidateHarvestTask` default-ctor fallback deleted.
   - `DependentsTask` vestigial `ToolPath` line deleted.
8. **Unit tests** for every new runner (mock process-runner for NativeSmokeTaskRunner's CMake invocation shape; FakeFileSystem + SharpCompress in-memory for Inspect-HarvestedDependencies; FakeFileSystem for CleanArtifacts).
9. **CI deliverable (`release.yml` grows):** `generate-matrix` job (emits JSON matrix from `manifest.runtimes[]` via a new `GenerateMatrixTask` — decision: D scope or B2 scope per plan §11; default D since it's self-contained), `harvest` matrix job, `native-smoke` matrix job capped to 3 RIDs with explicit YAML comment about the PA-2-row deferral, `consolidate-harvest` aggregation job. Pack / ConsumerSmoke jobs remain stubbed.
10. **Second WSL witness at slice close** — the full green pass: `CleanArtifacts` → `SetupLocalDev --source=local --rid linux-x64` → standalone `NativeSmoke --rid linux-x64` → `Inspect-HarvestedDependencies --rid linux-x64`.
11. **Commit** after Deniz's approval: `slice D: NativeSmoke extraction + Cake-native polish + Linux witness`.

## Slices Beyond D (preview)

- **Slice B2 — graph flatten + resolver rewrite + PostFlight retire.** Delete every `[IsDependentOn]` between stage tasks. Rewrite `LocalArtifactSourceResolver.PrepareFeedAsync` to internally compose PreFlight → (per-RID) EnsureVcpkg → Harvest → NativeSmoke → Consolidate → Pack → write local.props. Retire `PostFlightTask` + its reference sweep (PackageTask.cs comment + log msg; PackageConsumerSmokeRunner.cs XML docs). Audit-scan `PackageFamilySelector` consumers; if only in `PackageTaskRunner`, inline + retire. Health check: `--tree` shows flat graph; end-to-end `SetupLocalDev --source=local --rid win-x64` green again (now with PreFlight in composition).
- **Slice C — per-stage request records + GitTagVersionProvider + G58 + ConsumerSmoke stateless.** `Domain/<Module>/Models/*Request.cs` (seven records). `GitTagVersionProvider` + `Infrastructure/Git/IGitCommandRunner.cs`. `ResolveVersionsTask --version-source=git-tag` + `meta-tag` support. `G58CrossFamilyDepResolvabilityValidator` wired into `PackageOutputValidator` (Pack stage). `PackageConsumerSmokeRunner` stateless-callable via `PackageConsumerSmokeRequest`.
- **Slice E — cross-platform closure + playbook cake-ification + release.yml finalization + Publish stubs.** Full three-platform matrix (Windows + WSL + macOS Intel SSH) per playbook A–K. `PublishTask` + `Application/Publishing/PublishTaskRunner.cs` stubs throwing Phase-2b-pending errors. `release.yml` `publish-staging` / `publish-public` jobs as guarded, never-execute-in-this-pass placeholders. WSL `TEMP-wsl-smoke-commands.md` rewrite to invoke Cake targets exclusively.

## What NOT to Do (Failure Modes To Avoid)

- **Don't merge to master.** End-of-pass only. Even if everything looks green, `git push` / `git merge master` without explicit Deniz approval is a hard boundary.
- **Don't squash.** `git merge --no-ff feat/adr003-impl` preserves slice commits behind the merge parent — that's the deliberate bisect surface.
- **Don't pre-flatten the graph in Slice D.** Slice D keeps chain-based `[IsDependentOn]` — just inserts `NativeSmokeTask` between Harvest and Consolidate. Full flatten is B2.
- **Don't fold `PostFlight` retirement into Slice D.** It's B2 scope. Slice D leaves PostFlight + its references alone.
- **Don't introduce raw `JsonSerializer.Serialize` / `Deserialize` anywhere outside `CakeExtensions`.** If you need a new JSON shape (e.g., new request-record serialization in Slice C), route through `WriteJsonAsync` / `SerializeJson` / `ToJsonAsync` / `DeserializeJson`. Add new helpers to `CakeExtensions` if the existing surface doesn't cover your need.
- **Don't expand `NativeSmoke` to 7 RIDs.** Plan §12 caps this pass at 3 RIDs (local-validatable). The four PA-2 rows are Phase 2b (require CMake preset additions + GitHub-Actions-only execution; outside this pass).
- **Don't break `LayerDependencyTests`.** New request records under `Domain/<Module>/Models/` are allowed (canonical per ADR-002 §2.8 invariant #3). New Application services injecting Domain types are also fine. If you hit a violation, it's usually real drift — fix the layering, don't suppress the test.
- **Don't commit plan-doc drift silently.** Plan doc `phase-2-release-cycle-orchestration-implementation-plan.md` is part of the slice ledger. When a slice lands, update §2.1 Progress log + §14 Change log + the slice's sub-checklist in the same commit as the slice code.

## Communication Preferences

- Deniz communicates in Turkish + English interchangeably. Answer in whichever language he used, or a mix.
- Narrate findings + propose + wait for ack before commit/push/close. No chaining from discovery to action.
- Be direct. Challenge decisions when needed; don't be yes-man.
- When in doubt, stop and ask. "No scope creep on critical findings" per memory.

## Definition of Done for Your Session

### Default (continue Slice D → Slice E)

Progress one or more slices forward per the plan, each closing with:

- worktree health check green (build + test + LayerDependencyTests + `--tree` where relevant + slice-specific end-to-end invocation per §6 per-slice health check),
- plan doc §2.1 Progress log + §14 Change log + sub-checklist updated,
- one commit on `feat/adr003-impl` after Deniz approves,
- next slice's kickoff checklist primed in-conversation.

### Optional (pass close)

If you reach Slice E and close it green on all three platforms (Windows + WSL + macOS Intel SSH) per `cross-platform-smoke-validation.md` A–K checkpoints, the pass is ready to merge. **Still wait for Deniz's explicit merge approval.** Then `git checkout master && git merge --no-ff feat/adr003-impl` (not squash), and report the merge commit SHA.

Do not leave `feat/adr003-impl` in a half-slice state without a visible handoff note.
