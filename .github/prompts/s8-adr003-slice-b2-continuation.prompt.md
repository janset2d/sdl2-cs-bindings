---
name: "S8 ADR-003 Slice B2 Continuation"
description: "Priming prompt for the next agent picking up the ADR-003 implementation pass after Slices A + B1 + D + DA have landed on `feat/adr003-impl`. Slice B2 (graph flattening + LocalArtifactSourceResolver internal-composition rewrite + PostFlight retire) is next. Validate current repo state, continue per the implementation plan, no commits without explicit approval."
argument-hint: "Optional focus area, constraints, or whether implementation is already approved for a specific slice"
agent: "agent"
model: "GPT-5 (copilot)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` mid-pass, after Slices A + B1 + D + DA of the ADR-003 Cake refactor have landed. Your default job is to **continue** that pass from where it paused — not to re-plan, not to restart, not to merge to master.

## First Principle

Treat every claim in this prompt as **current-as-of-authoring** and **verify against live repo + git log + plan docs before acting**. Drift is expected between prompt authoring and session start. If something here contradicts what you observe, trust the observation and report the drift to Deniz before proceeding.

Do not force the repo to match this prompt.

## Where the Pass Is

- Active branch: `feat/adr003-impl`, ahead of `master` by **seven** commits as of prompt authoring:
  1. `chore(docs): ADR-003 implementation plan + review prompts`
  2. `slice A: IPackageVersionProvider seam + ExplicitVersionProvider + Cake.CMake spike`
  3. `slice B1: mapping contract migration + ManifestVersionProvider + ResolveVersions + Cake-native JSON`
  4. `chore(docs): priming prompt for next agent (Slice D continuation)`
  5. `slice D (WIP): NativeSmoke extraction + Cake-native infrastructure + diagnostics + CI first-draft` (`1ef3a82`)
  6. `slice DA: smoke playbooks Cake-first alignment` (`5199d3c`)
  7. `slice D witness fix: NativeSmokeTaskRunner CMake build invocation` (`d96b727`) — one bug witnesses surfaced
  8. `docs(playbook): PWD env-leakage note in non-interactive cross-shell invocations` (`ef49553`)
  9. `slice D closure (D.16): Unix witnesses green, ledger + playbook update` (`00bc517`)
- **Master has NOT been merged** — per Deniz's locked policy the `feat/adr003-impl` branch collects every slice commit and lands on master as **one `git merge --no-ff`** at end of pass (after Slice E). Do **not** merge or push to master without explicit end-of-pass approval.
- Build-host test suite: **390/390 green** at end of Slice D on Windows; same green on WSL linux-x64 and macOS Intel osx-x64 during the Unix witnesses.
- **Unix witnesses landed 2026-04-21**: WSL linux-x64 + macOS Intel osx-x64 both green end-to-end against `docs/playbook/unix-smoke-runbook.md` (Cake-first, 95% shared script between the two platforms). NativeSmoke `29/29 PASS` on both platforms. macOS `otool -L` direct-dep evidence for hybrid-static bake-in is in the `00bc517` closure notes + `cross-platform-smoke-validation.md` Last-validated header.
- End-to-end `SetupLocalDev --source=local --rid <rid>` validated on Windows (win-x64, 47s, 15 nupkgs) + WSL (linux-x64, ~2:24) + macOS Intel (osx-x64, ~1:22) with per-family D-3seg versions and `build/msbuild/Janset.Smoke.local.props` written each time.
- Cake task graph still **chain-based** — graph flatten **is Slice B2 scope**. Current shape: `PostFlight → PackageConsumerSmoke → Package → PreFlightCheck` + `SetupLocalDev → ConsolidateHarvest → NativeSmoke → Harvest → EnsureVcpkgDependencies → Info`. Plus standalone: `CleanArtifacts`, `CompileSolution`, `Inspect-HarvestedDependencies`, `GenerateMatrix`, `ResolveVersions`, `Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`, `Coverage-Check`.

## Your Primary Mission

The ADR-003 pass proceeds as: **A ✓ → B1 ✓ → D ✓ → DA ✓ → B2 → C → E**.

Default session task: **continue with Slice B2 (graph flattening + `LocalArtifactSourceResolver.PrepareFeedAsync` internal-composition rewrite + `PostFlight` retire + reference sweep)**, landing as one commit on `feat/adr003-impl`. Slices C and E follow in sequence.

If Deniz explicitly switches you to plan-first mode for B2 (`plan first`, `önce planla`) or revises the slice order, obey. Default is continue.

## Mandatory Grounding (read these first, in order)

1. `AGENTS.md` — operating rules, approval gates, communication style.
2. `docs/onboarding.md` — strategic decisions + repo layout.
3. `docs/plan.md` — current phase + roadmap.
4. `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` — **the execution plan for this pass**, specifically §2.1 Progress log (Slice D + DA DONE, B2/C/E not started), §6.4 Slice B2 scope, §6.5 Slice C scope, §6.6 Slice E scope (expanded 2026-04-21 with E1–E3 for `prepare-native-assets-*.yml` absorption + custom GHCR Ubuntu builder container), §7 inventory, §8 test methodology, §11 open questions including Q16 (custom Ubuntu builder container), §14 change log v2.4 / v2.5 / v2.6 for the Slice D+DA closure story.
5. `docs/decisions/2026-04-18-versioning-d3seg.md` (ADR-001) — D-3seg versioning + package-first consumer contract.
6. `docs/decisions/2026-04-19-ddd-layering-build-host.md` (ADR-002) — DDD layering + interface three-criteria rule + `LayerDependencyTests` catchnet.
7. `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (ADR-003) — three axes, resolve-once, stage-owned validation, Option A SetupLocalDev.
8. `docs/playbook/cross-platform-smoke-validation.md` — Cake-first smoke matrix (swept in Slice DA), Last-validated 2026-04-21.
9. `docs/playbook/unix-smoke-runbook.md` — unified Linux + macOS operational witness script (new in Slice DA).

Then branch to task-specific docs as needed: `release-guardrails.md`, `cake-build-architecture.md`, `phase-2-adaptation-plan.md` (ledger).

## Locked Policy Recap (do not re-debate without cause)

- **Commit policy (§3.3).** One local commit per slice on `feat/adr003-impl`, created only when the slice's worktree health check is green. No mid-slice pushes. End-of-pass `git merge --no-ff feat/adr003-impl` on master. Not squash — per-slice history stays queryable behind the merge parent. Slice D + Slice DA landed as separate commits (DA is a doc-only micro-slice sequenced between D code and D witness). Slice B2 returns to the single-commit-per-slice norm.
- **Approval gate (AGENTS.md).** Never commit unless Deniz explicitly approves. Never push. Present diff summary + proposed commit message; wait for "commit" / "onayla" / "yap" / "başla".
- **No temporary bridges, no "retire later" debt (§3.3).** If an abstraction is obsolete by end of the current slice, it's removed inside that slice's commit. Slice B2 is the slice that lands `PostFlight` retirement + its reference sweep — inside this slice, not deferred to C or E.
- **Cake-native, repo-native (§3.4).** New code uses `ICakeContext.FileSystem`, `IPathService`, Cake's `FilePath` / `DirectoryPath`. CLI wrappers follow the `Tool<TSettings>` / `Aliases` / `Settings` triad. **Zero raw `JsonSerializer.Serialize` / `Deserialize` calls** outside `Build.Context.CakeExtensions`. Slice B1 closed the JSON surface; Slice D added the native-tar wrapper; both set the pattern. If B2's resolver-composition rewrite calls for a new helper, add it to `CakeExtensions` with XML doc.
- **Cake target CLI: `--explicit-version <family>=<semver>` only.** `--family` / `--family-version` retired in B1. Any legacy reference in smoke / PA-2 / playbook docs outside `docs/_archive/` + change logs is stale.
- **Release tag push is blocked until Slice E closure.** The Slice D first-draft `release.yml` `harvest` / `native-smoke` / `consolidate-harvest` jobs lack the prereq + cache + submodule discipline of the `prepare-native-assets-*.yml` family (apt packages + `freepats` + autoconf 2.72 source build + `vcpkg-setup` composite + submodule recursion + NuGet workspace cache + Cake-compile-once + `--repo-root` + `harvested-assets-{os}-{rid}` naming). Slice E E1 absorbs the family via reusable-workflow dispatch; E2 moves the Linux apt preamble into a custom `ghcr.io/janset2d/sdl2-bindings-linux-builder:<tag>` image built by a dedicated companion workflow; E3 retires-or-narrows the legacy per-platform workflows. Operator-facing CI harvest remains `prepare-native-assets-main.yml` (workflow_dispatch) until E1–E3 land.
- **NativeSmoke is symmetric 7-RID at the matrix layer.** No hard-coded RID allow-list in `NativeSmokeTaskRunner` (removed during Slice D). The four PA-2 RIDs (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) will fail naturally at `cmake --preset <rid>` until `tests/smoke-tests/native-smoke/CMakePresets.json` grows entries in Phase 2b. That expected-failure surface IS the PA-2 witness signal, not a code-level cap.
- **Merge-commit policy:** end-of-pass `--no-ff` only. Do not squash. Do not merge intermediate slices.

## Open Cleanup Items (B2 candidate audit)

- **`PackageFamilySelector` inlining into `PackageTaskRunner`** (§11). After B1, the selector has exactly one production consumer (`PackageTaskRunner.RunAsync`) + one docstring reference (`PackageConsumerSmokeRunner`). B2 is the right re-audit window: after graph flatten + resolver rewrite, if the selector still has exactly one caller, inline the topological-sort helper into `PackageTaskRunner` (or into a static `Domain/Packaging/FamilyTopologyHelpers.cs`) and retire the `IPackageFamilySelector` interface + impl + `PackageFamilySelectionResult` + `PackageFamilySelectionError` + `PackageFamilySelection` model. Criterion-2 check before retiring: re-scan after flatten; if the rewrite surfaces a new consumer, keep the interface.
- **`NativePackageMetadataValidator` `ICakeContext` injection** — after B1's Deserialize migration, the validator still uses `IFileSystem`-only ctor and calls the static `CakeExtensions.DeserializeJson`. That's acceptable per the "static helper for Infrastructure-layer readers" direction; no further change needed unless B2 discovers a context-bound use case.
- **Linux `Inspect-HarvestedDependencies` direct-deps output** (follow-up identified during Slice D witness): `ldd` emits transitive closure by design (51 entries for libSDL2_net.so in the WSL witness), which is correct `ldd` behaviour but less informative than macOS's `otool -L` direct-only output. A `readelf -d` NEEDED-filter supplement for Linux would surface direct `DT_NEEDED` entries only, aligning the two platforms' diagnostic signal. Not a regression (Harvest's G19 guardrail gates the direct-dep invariant already); minor doc-and-tool improvement for the broader Slice E sweep. Not blocking for B2.

## Slice B2 Kickoff Checklist

Per plan §6.4:

1. **Baseline** — `dotnet test build/_build.Tests -c Release` green at 390/390 (or current baseline). If not, stop and triage — something drifted.
2. **Graph flattening:** remove every `[IsDependentOn(...)]` between stage tasks:
   - `HarvestTask`: remove `[IsDependentOn(EnsureVcpkgDependenciesTask)]`.
   - `NativeSmokeTask`: remove `[IsDependentOn(HarvestTask)]` (added in Slice D, now retired).
   - `ConsolidateHarvestTask`: remove `[IsDependentOn(NativeSmokeTask)]` (added in Slice D, now retired).
   - `SetupLocalDevTask`: remove `[IsDependentOn(ConsolidateHarvestTask)]`.
   - `PackageTask`: remove `[IsDependentOn(PreFlightCheckTask)]`.
   - `PackageConsumerSmokeTask`: remove `[IsDependentOn(PackageTask)]`.
3. **Resolver internal-composition rewrite:** `LocalArtifactSourceResolver.PrepareFeedAsync` body becomes:
   1. Resolve mapping via `ManifestVersionProvider` (local suffix).
   2. Invoke `PreflightTaskRunner.Run(new PreflightRequest(manifest, mapping))` — fail-fast on G54 / G49 / etc.
   3. For each RID in scope (host-RID default): `EnsureVcpkgDependenciesTaskRunner.RunAsync(rid)` → `HarvestTaskRunner.RunAsync(harvestReq(rid, libraries))` → `NativeSmokeTaskRunner.RunAsync(nsReq(rid))`.
   4. `ConsolidateHarvestTaskRunner.RunAsync(...)`.
   5. `PackageTaskRunner.RunAsync(packRequest(mapping, ...))`.
   6. `WriteConsumerOverrideAsync(...)`.

   Note: request-record shapes arrive in Slice C; in B2 the runners accept their existing-shape inputs plus the mapping.
4. **Precondition-check pattern rollout:** each runner's `RunAsync` verifies input-artifact presence at top of body and raises a precondition-specific `CakeException` with remediation hint. `PackageTaskRunner.EnsureHarvestOutputReadyAsync` is the existing template; extend to `PreflightTaskRunner`, `HarvestTaskRunner`, `NativeSmokeTaskRunner` (partial already — extend), `ConsolidateHarvestTaskRunner`, `PackageConsumerSmokeRunner`.
5. **PostFlight retirement sweep:**
   - Delete `build/_build/Tasks/PostFlight/PostFlightTask.cs` (and the folder).
   - Remove `PostFlight` reference from `PackageTask.cs` line 23 XML doc comment.
   - Remove `PostFlight` reference from `PackageTask.cs` line 34 XML doc comment.
   - Rewrite `PackageTask.cs` line 46 user-facing log message (replace "PostFlight release chain always supplies the flag" with wording reflecting the new mapping-driven invocation surface).
   - Rewrite `PackageConsumerSmokeRunner.cs` line 433 XML doc comment (replace "a single PostFlight run" with platform-neutral wording).
   - Rewrite `PackageConsumerSmokeRunner.cs` line 458 XML doc comment (same pattern).
6. **`PackageFamilySelector` inlining audit** (per §11 open cleanup). After graph flatten + resolver rewrite, re-scan for production consumers. If exactly one remains (`PackageTaskRunner.RunAsync`), inline + retire. If a new consumer surfaces, keep and log the rationale.
7. **Update tests** that mocked the `[IsDependentOn]` chain behavior to inject runners directly. Tests that exercised `SetupLocalDev` end-to-end via chain invocation should now exercise it via `IArtifactSourceResolver.PrepareFeedAsync` with the composed runners.
8. **Windows health-check run** at slice close: `--target CleanArtifacts` → `--target SetupLocalDev --source=local --rid win-x64` → (optional) `--target PackageConsumerSmoke --rid win-x64` — still green post-flatten. Witnesses on WSL + macOS Intel are **not required for B2** (Slice D already took them); re-witness is Slice E scope.
9. **`release.yml` adjustment** — existing `harvest` / `native-smoke` / `consolidate-harvest` jobs are already first-draft with no inherited task chain (each job invokes a single Cake target). After flatten, no YAML change should be needed; verify with a second reading. Slice E does the full rewire to reusable-workflow dispatch — do **not** pre-empt that here.
10. **Commit** after Deniz's approval: `slice B2: graph flatten + resolver internal-composition rewrite + PostFlight retire`.

## Slices Beyond B2 (preview)

- **Slice C — per-stage request records + GitTagVersionProvider + G58 + ConsumerSmoke stateless.** `Domain/<Module>/Models/*Request.cs` (seven records per ADR-003 §3.2: `PreflightRequest`, `HarvestRequest`, `NativeSmokeRequest`, `ConsolidateHarvestRequest`, `PackRequest`, `PackageConsumerSmokeRequest`, `PublishRequest`). `GitTagVersionProvider` + `Infrastructure/Git/IGitCommandRunner.cs`. `ResolveVersionsTask --version-source=git-tag` + `meta-tag` support. `G58CrossFamilyDepResolvabilityValidator` wired into `PackageOutputValidator` (Pack stage). `PackageConsumerSmokeRunner` stateless-callable via `PackageConsumerSmokeRequest`.
- **Slice E — cross-platform closure + `prepare-*` absorption + custom GHCR Ubuntu container + playbook cake-ification + `release.yml` finalization + `Publish` stubs.** Full three-platform matrix (Windows + WSL + macOS Intel SSH) re-witness. E1: `release.yml` harvest / native-smoke / consolidate-harvest jobs dispatch per-platform reusable workflows via `uses:` + matrix-derived inputs from `generate-matrix`. E2: custom Ubuntu builder image hosted on `ghcr.io/janset2d/sdl2-bindings-linux-builder:<tag>`, built by new `.github/workflows/build-linux-container.yml` companion workflow (see plan §11 Q16 for rebuild-cadence / tag-scheme / multi-arch / registry-auth / prerequisite-baking open sub-questions — resolve at E2 kickoff). E3: retire (or narrow) `prepare-native-assets-*.yml`. E4-E5: `PublishTask` + `Application/Publishing/PublishTaskRunner.cs` stubs throwing Phase-2b-pending errors; `publish-staging` / `publish-public` guarded `release.yml` jobs. Broader doc sweep (`AGENTS.md`, `onboarding.md`, `cake-build-architecture.md`, `release-guardrails.md`, `release-lifecycle-direction.md`, `phase-2-adaptation-plan.md`, `plan.md`) — this is the catch-all for everything Slice DA explicitly deferred. `TEMP-wsl-smoke-commands.md` / `TEMP-macos-smoke-commands.md` were already retired in DA; nothing to do there. `docs/playbook/local-development.md` still has two live `--family-version` usages that DA intentionally left out of scope — E sweeps those.

## What NOT to Do (Failure Modes To Avoid)

- **Don't merge to master.** End-of-pass only. Even if everything looks green after B2, `git push` / `git merge master` without explicit Deniz approval is a hard boundary.
- **Don't squash.** `git merge --no-ff feat/adr003-impl` preserves slice commits behind the merge parent — that's the deliberate bisect surface.
- **Don't pre-empt Slice C request records inside B2.** Runners accept their existing input shapes plus the mapping. Slice C rewrites every runner signature to take a record; doing half of it in B2 leaves the codebase in a split-personality state. The `Preflight`Request hint in §6.4 step 3.2 is illustrative — if the existing `PreflightTaskRunner.Run` signature does not yet take a mapping, pass the mapping via whatever surface already exists (service field, method arg on an overload, etc.).
- **Don't pre-empt Slice E's CI rewrite or container pivot.** `release.yml` body changes in B2 are **zero** — the first-draft job bodies already don't assume a chain. If you think a YAML change is needed, flag it and check with Deniz before editing.
- **Don't introduce raw `JsonSerializer.Serialize` / `Deserialize` anywhere outside `CakeExtensions`.** Slice B1 closed this surface; keep it closed. Add new helpers to `CakeExtensions` if the existing surface does not cover your need.
- **Don't break `LayerDependencyTests`.** Post-B2 the target shape (no Task → Domain/Infrastructure interface refs) should be fully reachable — the temporary catchnet allowance for Task-held Domain/Infrastructure interfaces existed while Harvest/PreFlight fat tasks still held them. After flatten + runner rewrite, every Task is a thin adapter with at most an Application runner reference + Domain/Infrastructure DTOs + Cake tool wrappers. If a `LayerDependencyTests` violation surfaces, it is real drift — fix the layering, don't suppress the test.
- **Don't touch `unix-smoke-runbook.md` or `cross-platform-smoke-validation.md` unless B2 actually changes operator-visible behaviour.** The Slice DA sweep is fresh. If B2 retires `PostFlight`, the playbook's K checkpoint note ("legacy PostFlight umbrella slated for retirement in Slice B2") can be updated to past-tense, but that is a line edit, not a full re-sweep. Record every playbook delta in the B2 commit's summary.
- **Don't commit plan-doc drift silently.** Plan doc `phase-2-release-cycle-orchestration-implementation-plan.md` is part of the slice ledger. When B2 lands, update §2.1 Progress log + §14 Change log v2.7 + the Slice B2 sub-checklist in the same commit as the B2 code.

## Communication Preferences

- Deniz communicates in Turkish + English interchangeably. Answer in whichever language he used, or a mix.
- Narrate findings + propose + wait for ack before commit/push/close. No chaining from discovery to action.
- Be direct. Challenge decisions when needed; don't be yes-man.
- When in doubt, stop and ask. "No scope creep on critical findings" per memory — if a B2 diff uncovers a cross-slice concern, isolate the B2 delta and file the concern as a Slice C or Slice E backlog item.

## Definition of Done for Your Session

### Default (continue Slice B2 → Slice C → Slice E)

Progress one or more slices forward per the plan, each closing with:

- worktree health check green (build + test + `LayerDependencyTests` + `--tree` flat-graph check + slice-specific end-to-end invocation per §6 per-slice health check),
- plan doc §2.1 Progress log + §14 Change log + sub-checklist updated,
- one commit on `feat/adr003-impl` after Deniz approves,
- next slice's kickoff checklist primed in-conversation.

### Optional (pass close)

If you reach Slice E and close it green on all three platforms (Windows + WSL + macOS Intel SSH) per `cross-platform-smoke-validation.md` A–K checkpoints, the pass is ready to merge. **Still wait for Deniz's explicit merge approval.** Then `git checkout master && git merge --no-ff feat/adr003-impl` (not squash), and report the merge commit SHA.

Do not leave `feat/adr003-impl` in a half-slice state without a visible handoff note.
