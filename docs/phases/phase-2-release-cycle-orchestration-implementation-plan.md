# Phase 2 — ADR-003 Release Lifecycle Orchestration: Implementation Plan

**Date:** 2026-04-21 (v2.2 — Slices A + B1 committed; Slice D is next; master merge deferred to end-of-pass).
**Status:** IN PROGRESS — Slices A + B1 landed on `feat/adr003-impl` (uncommitted intermediate sub-steps ran through 355/355 tests; end-of-B1 end-to-end `SetupLocalDev` green in 47s). Slice D (NativeSmoke extraction + Cake-native polish) queued next.
**Owner:** Deniz İrgin (@denizirgin).
**Authorship:** Drafted during plan-first session 2026-04-21; revised after three-reviewer critical pass; updated as slices land.

**Prerequisites (read these first):**

- [ADR-001 — D-3seg Versioning, Package-First Local Dev, Artifact Source Profile](../decisions/2026-04-18-versioning-d3seg.md)
- [ADR-002 — DDD Layering for the Cake Build-Host](../decisions/2026-04-19-ddd-layering-build-host.md)
- [ADR-003 — Release Lifecycle Orchestration + Version Source Providers](../decisions/2026-04-20-release-lifecycle-orchestration.md)
- [Phase 2 Adaptation Plan (ledger)](phase-2-adaptation-plan.md)
- [Cross-Platform Smoke Validation (playbook)](../playbook/cross-platform-smoke-validation.md)
- [Release Guardrails (canonical)](../knowledge-base/release-guardrails.md)
- [Release Lifecycle Direction (policy only, post-narrowing)](../knowledge-base/release-lifecycle-direction.md)

---

## 1. Purpose and Scope

This document is the **execution-level implementation plan** for the ADR-003 Cake refactor pass and the parallel CI/CD skeleton it produces. It sits between the ADR (which locks intent) and the commit diff (which changes code).

It answers four questions:

1. **What is being changed?** — the Cake task surface, CLI, version-resolution path, stage ownership boundaries, and `release.yml` skeleton.
2. **How is it being changed?** — sliced into six development waves (A → B1 → D → B2 → C → E), with CI skeleton growing in parallel.
3. **Why this particular shape?** — thought-process trail from ADR re-reads, code inspection, and two substantive corrections during the plan-first session.
4. **How do we know it worked?** — per-slice worktree health checks, one Linux witness at end of slice D, and full cross-platform validation on WSL + macOS at end of slice E.

It does **not** re-derive:

- External contracts (version shape, consumer contract, artifact source profile) — those are ADR-001.
- Internal layering (Tasks / Application / Domain / Infrastructure) — that is ADR-002.
- Orchestration ownership graph (RID → Family → Version, stage-owned validation, resolve-once invariant) — that is ADR-003.

---

## 2. Status Headline

- **ADRs locked.** ADR-001 (v1), ADR-002 (v1), ADR-003 (v1.5 draft, direction-selected for PD-7 / PD-8 / PD-13).
- **Canonical doc sweep complete** as of 2026-04-21 (per `plan.md` and `phase-2-adaptation-plan.md`).
- **Build-host baseline pre-slice-A:** 340/340 tests green (TUnit 1.33.0, .NET 9.0.12, win-x64, Release). **Post-Slice-A + Slice B1 in-flight: 356/356 tests** (+16 new tests across the provider seam + ManifestVersionProvider + ResolveVersionsTaskRunner).
- **Northstar decisions locked** (2026-04-21):
  - **Slice order:** A → B1 → D → B2 → C → E (six waves; B split into B1 contract migration and B2 graph rewrite).
  - **NativeSmoke scope this pass:** three RIDs only (`win-x64`, `linux-x64`, `osx-x64`). Remaining four RIDs (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) deferred to Phase 2b PA-2 witness work running in GitHub Actions (native arm64 / 32-bit hosts not available locally).
  - **Commit policy:** private `feat/adr003-impl` branch; one local commit per slice; end-of-pass **merge commit** (not squash) to master, preserving per-slice bisectability.
- **This plan is the bridge from ADR prose to code.**

### 2.1 Progress log (`feat/adr003-impl`)

| Slice | Status | Commit | Landed |
| --- | --- | --- | --- |
| (pre-slice-A chore) | **DONE** | `282b707` | ADR-003 implementation plan + review prompts |
| **Slice A** — IPackageVersionProvider seam + ExplicitVersionProvider + Cake.CMake spike | **DONE** | `36b1cd5` | Provider seam (+ 5 unit tests); Cake.CMake 1.4.0 binding spike **PASS** (Slice D will use the addin directly, no repo-local CMakeTool fallback needed); `.github/workflows/release.yml` skeleton with trigger stanzas + stub `resolve-versions-stub` job. |
| **Slice B1** — Mapping contract migration (graph intact) | **DONE** | *filled at commit time* | All 11 sub-steps + opportunistic Cake-native JSON migration + end-to-end `SetupLocalDev --source=local --rid win-x64` green in 47s (15 nupkgs at per-family D-3seg versions). Graph intact (chain-based); B2 flattens. |
| **Slice D** — NativeSmoke extraction + Cake-native infrastructure + diagnostics | **DONE** | `1ef3a82` (code) + `d96b727` (witness fix) | Slice-D code + single witness-found NativeSmoke `BinaryPath` regression fix. Witnesses green on WSL linux-x64 + macOS Intel osx-x64 (29/29 NativeSmoke tests each, full Cake chain end-to-end, D-3seg packages + `Janset.Smoke.local.props` produced, PackageConsumerSmoke net9/net8 pass with per-platform net462 skip policy behaving as documented). macOS `otool -L` proves hybrid-static: SDL2_image/ttf/gfx/net satellites show 3 direct deps only (libSDL2 + self + libSystem) — zero leaked codec / font libs. |
| **Slice DA** — Smoke playbook Cake-first alignment | **DONE** | `5199d3c` | Cake-first doc sweep across the three smoke playbooks landed before the witness runs; enabled the runbook to be followed literally during witness without operator-side CLI translation. Ledger: `cross-platform-smoke-validation.md` swept (A–K + PA-2 → Cake-first with pre-Cake A/B bootstrap exception annotation; retired `--family` / `--family-version` → `--explicit-version <family>=<semver>`; new checkpoints N/O/P added to Active); new `unix-smoke-runbook.md` unifies WSL + macOS at 95% parity via `$PLATFORM`/`$RID`/`$PRESET` env vars; `TEMP-wsl-smoke-commands.md` + `TEMP-macos-smoke-commands.md` retired. Broader doc sweep (AGENTS.md, onboarding.md, cake-build-architecture.md, release-guardrails.md, release-lifecycle-direction.md, phase-2-adaptation-plan.md, plan.md) deferred to Slice E per §6.6. |
| (docs chore) | **DONE** | `ef49553` | `cross-platform-smoke-validation.md` footnote documenting the `$PWD` env-leakage artefact on cross-shell invocations (Git Bash → `wsl -c '…'`, non-interactive SSH). Not a code regression; interactive WSL / SSH sessions are unaffected. |
| **Slice B2** | **DONE** | `06f9ea4` | Graph flatten + `PostFlight` retire + `PackageFamilySelector` inlining + resolver-centric composition (junior's first pass) rolled back after Deniz review on 2026-04-21: `LocalArtifactSourceResolver` narrowed to verify-feed + stamp-props only (4-param ctor, mapping flows in via widened `IArtifactSourceResolver` signature); new `SetupLocalDevTaskRunner` under `Application/Packaging/` owns the `Preflight → EnsureVcpkg → Harvest → ConsolidateHarvest → Pack → resolver handoff` composition for the Local profile; `NativeSmokeTaskRunner` removed from the local-dev flow. Topological helper extracted to `Domain/Packaging/FamilyTopologyHelpers.cs` (deterministic Kahn + cycle detection) with 5 dedicated tests. `ConsolidateHarvestTaskRunner` soft-fail replaced with precondition-guard hard-fail. `SetupLocalDevTaskTests` deleted; `SetupLocalDevTaskRunnerTests` + `FamilyTopologyHelpersTests` added. Test count 391 → 398. Windows witness: `SetupLocalDev --source=local` 1:13 (5 families, 15 nupkgs) → `PackageConsumerSmoke` 0:58 (netstandard2.0 compile-sanity + net9.0 + net8.0 + net462 all Succeeded). ADR-003 §3.3 amendment v1.6 documents the design shift. |
| **Slice CA** (post-BA micro-slice) | **DONE** | *filled at commit time* | Witness hardening before Slice C: (1) `PackageConsumerSmokeRunner.RunDotNetCommand` gained an `echoStdout` parameter; `dotnet test package-smoke (<tfm>)` + `compile-sanity netstandard2.0 consumer` invocations set it `true`, surfacing TUnit's per-TFM pass/fail summary at the normal Cake log level instead of burying it at `_log.Verbose`; (2) new `Domain/Runtime/IMsvcDevEnvironment.cs` seam + `Infrastructure/Tools/Msvc/MsvcDevEnvironment.cs` impl that mirrors the `DumpbinTool` self-resolution pattern for the full MSVC environment — VSWhere locates the VS installation, `cmd /c vcvarsall.bat <arch> >nul && set` dumps env, parse emits the delta vs. current process env; `NativeSmokeTaskRunner.ApplyMsvcEnvironmentAsync` merges the delta into `CMakeSettings.EnvironmentVariables` + `CMakeBuildSettings.EnvironmentVariables` before each Cake.CMake invocation. Windows-only by contract — caller guards on `OperatingSystem.IsWindows()` for intent visibility, resolver throws `PlatformNotSupportedException` on non-Windows for defence-in-depth. `VCToolsInstallDir` already-sourced fast path preserved so Developer-PowerShell invocations skip the extra cmd spawn. Test count 398 → 400 (+1 `MsvcDevEnvironment` contract test, +1 `MsvcDevEnvironment` registration in `ProgramCompositionRootTests`). Windows witness: `./smoke-witness.cs ci-sim` 8/8 PASS in 81.3s from plain PowerShell (pre-CA: halted on NativeSmoke with `CMAKE_C_COMPILER` missing); `./smoke-witness.cs local` 3/3 PASS in 114.7s with TUnit per-TFM pass counts (`net9.0`: 12 passed, `net8.0`: 12 passed, `net462`: 11 passed) now visible in the tee'd log. Removes the need for `ilammy/msvc-dev-cmd@v1` in the eventual `release.yml` Windows matrix jobs. |
| **Slice BA** (post-B2 micro-slice) | **DONE** | `735ccb3` | `tests/scripts/smoke-witness.cs` — single-file .NET 10 file-based app that orchestrates the post-B2 flat graph into two witness flows (`local` = `CleanArtifacts → SetupLocalDev → PackageConsumerSmoke` for fast dev iterate; `ci-sim` = mini CI replay `CleanArtifacts → ResolveVersions → PreFlightCheck → EnsureVcpkgDependencies → Harvest → NativeSmoke → ConsolidateHarvest → Package`, each stage separate `Process.Start` with ResolveVersions-emitted mapping). Spectre.Console (CPM-native, `0.49.1` PackageVersion added to root `Directory.Packages.props`) powers Rule/Panel/Table/FigletText UI + final summary. Logs land at `/.logs/witness/` (new gitignore entry; outside `artifacts/` so `CleanArtifacts` cannot wipe witness logs mid-run). SDK scope handled via `tests/scripts/global.json` pinning `10.0.100+` (repo root stays on 9.x for the build-host). Shebang `#!/usr/bin/env dotnet` + `LF` enforced via `tests/scripts/.gitattributes`. Directory.Build.props inheritance retained (no isolation); only compat cost was `Build.csproj` `NoError` mirror for `CA1502`/`CA1505` (single-file Main absorbs all helper complexity). Witness on Windows: `local` 3/3 PASS in 132s; `ci-sim` 5/6 PASS through Harvest (22.4s), `NativeSmoke` halts on missing `cl.exe` (documented — MSVC Developer shell required for ci-sim on Windows, mini CI sim correctly surfacing a real env gap CI's `vcpkg-setup` resolves). PackageConsumerSmoke deliberately skipped in `ci-sim` until Slice C parameterises the runner on `--feed-path` + `--explicit-version` (ADR-003 §2.4 resolve-once via CLI, no local-props injection). |
| **Slice C**, **Slice E** | not started | — | — |

**Slice B1 sub-progress (worktree-level; one commit at close per plan §9):**

- [x] B1.1 — `ManifestVersionProvider` + 5 unit tests. Derives `<UpstreamMajor>.<UpstreamMinor>.0-<suffix>` per family from manifest upstream versions; scope filter; invalid-suffix / missing-family / malformed-upstream-version guards.
- [x] B1.2 — `ResolveVersionsTask` + `ResolveVersionsTaskRunner` (manifest source live; explicit / git-tag / meta-tag sources throw explicit "later-slice" messages) + `VersioningOptions` (`--version-source`, `--suffix`, `--scope`, `--explicit-version`) + `VersioningConfiguration` + `IPathService.GetResolveVersionsOutputFile()` + `ParsedArguments` extension + DI wire-up + 6 unit tests. End-to-end Windows smoke: `--target ResolveVersions --version-source=manifest --suffix=test.smoke` writes canonical 6-family flat JSON at `artifacts/resolve-versions/versions.json`.
- [x] Cake-native JSON extension migration (opportunistic; ADR-003 §3.4 "Cake-native, repo-native"). Added `WriteJsonAsync<T>(FilePath, model, options?)` + `SerializeJson<T>(model, options?)` + `DefaultJsonOptions` to `CakeExtensions`. Migrated **all 5** pre-existing `JsonSerializer.Serialize + WriteAllTextAsync` call sites (`NativePackageMetadataGenerator`, `HarvestTaskRunner` ×2, `ConsolidateHarvestTaskRunner` ×2, plus the one in `ResolveVersionsTaskRunner`). Every public `CakeExtensions` member now carries XML doc. Zero raw `JsonSerializer.Serialize` calls outside `CakeExtensions` implementation itself. Deserialize migration remains open — see §11.
- [ ] B1.3 — Rewrite `UpstreamVersionAlignmentValidator` to accept the mapping directly (collapses the `requestedFamilies.Count == 1` strict-minor special case).
- [ ] B1.4 — Reshape `PackageBuildConfiguration`: retire `Families` + `FamilyVersion`; add `ExplicitVersions: IReadOnlyDictionary<string, NuGetVersion>`. Cascade into `PackageTaskRunner`, `PackageFamilySelector`, `PackageConsumerSmokeRunner`, `LocalArtifactSourceResolver`, `PreflightTaskRunner`, and their test fixtures.
- [ ] B1.5 — Remove `--family` + `--family-version` CLI options. Parser rejects legacy flags with a migration-hint error.
- [ ] B1.6 — Retire `IPackageVersionResolver` + `PackageVersionResolver` + related result types (`PackageVersionResolutionResult`, `PackageVersionResolutionError`, `PackageVersion` wrapper).
- [ ] B1.7 — Rewire `LocalArtifactSourceResolver.PrepareFeedAsync` to consume `ManifestVersionProvider` (replaces today's inline manifest + timestamp version derivation).
- [ ] B1.8 — DI registration cleanup: `IPackageVersionProvider` factory reads `PackageBuildConfiguration.ExplicitVersions`; remove `IPackageVersionResolver` registration.
- [ ] B1.9 — `release.yml` grows real `resolve-versions` + `preflight` jobs consuming `--target ResolveVersions --version-source=manifest --suffix=ci.<run-id>` output via `needs:`.
- [x] B1.10 — Full test fixture rewire across `PackageBuildConfiguration`-touching tests (Program composition-root, PackageTaskRunner, PreflightTaskRunner, PackageConsumerSmokeRunner, LocalArtifactSourceResolver, UpstreamVersionAlignmentValidator, ExplicitVersionProvider, FakeRepoBuilder, PathConstructionTests).
- [x] B1.10-deserialize — Final Cake-native JSON cleanup. `CakeExtensions.DeserializeJson<T>(string, options?)` + `DeserializeJson<T>(ReadOnlySpan<byte>, options?)` static helpers landed with source-gen-compatible `JsonSerializerOptions` passthrough. 7 call sites migrated (`NativePackageMetadataValidator`, `ConsolidateHarvestTaskRunner`, `VcpkgManifestReader` ×2, `VcpkgCliProvider`, `CoverageBaselineReader` ×2). Zero raw `JsonSerializer` calls outside `CakeExtensions` implementation.
- [x] B1.11 — Slice B1 worktree health check green + committed as single atomic slice commit.

**Slice D sub-progress (worktree-level; one commit at witness close per plan §9):**

- [x] D.1 — Two B1-era blockers cleared (duplicate tests in `PathConstructionTests.cs` × 6 + S3267 loop-simplification in `NativeSmokeTaskRunner.EnsureHarvestPayloadReady`); Windows build + 364/364 tests green before adding new code.
- [x] D.2 — `NativeSmokeTaskRunner` hard-coded 3-RID cap removed (PA-2 cap now lives in `CMakePresets.json` where CMake naturally fails until Phase 2b grows entries; no duplicate policy in the Cake host).
- [x] D.3 — SharpCompress proposal retired before any code consumed it (`SymbolicLinkHandler` default-swallow trap + semantic drift vs production MSBuild tar). Directory.Packages.props + Build.csproj PackageReference reverted; scratch `temp/sharpinspect` + `temp/cmake-reflect` deleted; `/temp/` added to `.gitignore`; plan + prompt + §11 Q9 swept.
- [x] D.4 — `Infrastructure/Tools/Tar/` wrapper (Vcpkg/Dumpbin/Ldd/Otool golden shape). `TarExtractSettings` / `TarExtractTool : Tool<T>` / `TarAliases.TarExtract` + 6 unit tests (argument shape, verbose flag, strip-components, null guards). Universally available platform binary; preserves Unix symlinks/permissions/xattrs natively — matches MSBuild consumer-side extraction 1-to-1.
- [x] D.5 — PathService accessors added: `HarvestStagingRoot`, `MatrixOutputRoot` (rooted from the existing `GetMatrixOutputFile()`), plus earlier B1-era `SolutionFile`, `NativeSmokeProjectDir`, `GetNativeSmokeBuildPresetDir`, `GetNativeSmokeExecutableFile`, `InspectOutputRoot`, `GetInspectOutputRidDir`, `GetInspectOutputLibraryDir`, `GetMatrixOutputFile`, `PackageConsumerSmokeOutput`, `SmokeTestResultsOutput`. Tests added for each in `PathConstructionTests`.
- [x] D.6 — `CleanArtifactsTaskRunner` + `CleanArtifactsTask` (`[TaskName("CleanArtifacts")]`, no graph edges). Wipes `artifacts/{harvest_output,packages,package-consumer-smoke,test-results/smoke,harvest-staging,temp/inspect,matrix}` + `tests/smoke-tests/native-smoke/build`. Leaves `vcpkg_installed/` untouched (cold-rebuild cost). 3 unit tests: full wipe, vcpkg preservation, idempotency.
- [x] D.7 — `CompileSolutionTaskRunner` + `CompileSolutionTask` (`[TaskName("CompileSolution")]`, no graph edges). Thin `DotNetBuild` wrapper on `Janset.SDL2.sln` with `NoLogo = true`; `Configuration` sourced from `DotNetBuildConfiguration`. 4 ctor-guard unit tests (DotNetBuild internals validated by the end-to-end witness, not by a brittle tool-locator mock).
- [x] D.8 — `InspectHarvestedDependenciesTaskRunner` + `InspectHarvestedDependenciesTask` (`[TaskName("Inspect-HarvestedDependencies")]`, no graph edges). Platform-aware: Unix RIDs extract `native.tar.gz` via the new tar wrapper into `artifacts/temp/inspect/<rid>/<lib>/` (symlinks preserved), Windows RIDs read `runtimes/<rid>/native/` directly. Resolves primary binary via `LibraryManifest.PrimaryBinaries[<os>].Patterns` glob match (shortest-path tie-break). Dispatches to Dumpbin / Ldd / Otool aliases. 6 unit tests: platform-unknown guard, missing harvest dir, missing tarball, unknown library, missing primary binary, missing primary_binaries entry for OS.
- [x] D.9 — `GenerateMatrixTaskRunner` + `GenerateMatrixTask` (`[TaskName("GenerateMatrix")]`, no graph edges). Emits `artifacts/matrix/runtimes.json` in GitHub-Actions `include[]` shape (`rid` + `triplet` + `strategy` + `runner` + `container_image`) from `manifest.runtimes[]`. Single 7-RID output consumed by both harvest + native-smoke jobs per the symmetric-pivot decision. 3 unit tests: 7-RID parity with real manifest, per-field projection fidelity, empty-runtimes guard.
- [x] D.10 — `NativeSmokeTaskRunner` strategy-A unit tests (precondition paths only; CMake + process dispatch validated by the Linux witness): missing harvest native dir, library-not-in-manifest rejection, absent-directory remediation hint, symmetric-cap regression guard (`win-arm64` RID accepted at input, fails later at harvest-output precondition — proving the 3-RID cap is gone).
- [x] D.11 — `DependentsTask` vestigial `ToolPath = context.Tools.Resolve("dumpbin.exe")` line deleted.
- [x] D.12 — `ConsolidateHarvestTask` default-ctor fallback deleted (DI ctor is already primary); 15 fixture sites updated to pass `new ConsolidateHarvestTaskRunner()` explicitly.
- [x] D.13 — `release.yml` grows `generate-matrix` + `harvest` (7-RID matrix) + `native-smoke` (7-RID matrix with inline YAML comment pinning the PA-2 expected-failure surface) + `consolidate-harvest` (aggregation) jobs. Pack / ConsumerSmoke jobs remain stubbed for Slice C.
- [x] D.14 — DI registrations added in `Program.cs` for `NativeSmokeTaskRunner`, `CleanArtifactsTaskRunner`, `CompileSolutionTaskRunner`, `InspectHarvestedDependenciesTaskRunner`, `GenerateMatrixTaskRunner`.
- [x] D.15 — **Linux WSL + macOS Intel osx-x64 witnesses landed.** Both green end-to-end against the `unix-smoke-runbook.md` section-by-section sequence. One real regression surfaced during the WSL pass — `NativeSmokeTaskRunner.RunCmakeBuild` called `Cake.CMake.CMakeBuild` without `BinaryPath`, and the addin's runner validates it as required; configure had already succeeded so the build step faulted with `ArgumentNullException (BinaryPath)`. Fix pointed the build invocation at the preset's configured `binaryDir` (`${sourceDir}/build/${presetName}`, matching `PathService.GetNativeSmokeBuildPresetDir`); landed in `d96b727` and verified green on both platforms. macOS `otool -L` diagnostic confirmed the hybrid-static target shape at the dep-graph layer — SDL2_image / SDL2_ttf / SDL2_gfx / SDL2_net each expose exactly three direct `LC_LOAD_DYLIB` entries (`libSDL2-2.0.0.dylib` + self + `libSystem.B.dylib`) with zero leaked codec / font libraries, confirming vcpkg hybrid-static bake-in behaves as designed end-to-end. WSL `ldd` showed 51 transitive entries for `libSDL2_net.so` — correct ldd behaviour (transitive closure); a follow-up `readelf -d | grep NEEDED` supplement for direct-only output on Linux is logged as a minor improvement for the broader Slice E doc sweep, not blocking. Aside (operator-harness only, not a code regression): `$PWD` leaks stale via `wsl -c` / non-interactive SSH; documented in `cross-platform-smoke-validation.md` Known Gotchas via `ef49553`, no code touched.
- [x] D.16 — Slice D closure: plan §2.1 progress log + §14 change log + sub-checklist ledger flipped to DONE after witnesses; `cross-platform-smoke-validation.md` "Last validated" header bumped; commit = this D.16 doc-only commit on `feat/adr003-impl`.

---

## 3. Guiding Principles (apply continuously)

These principles are the background contract for every decision below. If an implementation step conflicts with a principle, the plan changes — the principle does not.

### 3.1 Vision-first, architecture before refactoring cost

ADR-003 §"Design Principle — Vision First" is the first-among-equals rule:

> Existing Cake tasks, services, seams, and helpers are reusable lego pieces, not fixed architectural truth. If a component sits behind the wrong boundary, in the wrong task, or in the wrong orchestration layer, it may be retained, relocated, narrowed, split, or retired. Lower churn is desirable, but only after the target architecture is coherent. A cheaper wrong boundary is still the wrong boundary.

Applied to this pass: when current code shape conflicts with the target (stage independence, resolve-once, single CLI scope axis), current code is refactored, not preserved. Examples: `--family-version` retires, `--family` retires, `[IsDependentOn]` chains on stage tasks retire, `PostFlight` retires, `IPackageVersionResolver` retires.

### 3.2 Everything is subject to change until end-of-pass

Any decision taken here — slice boundary, namespace choice, CLI option shape, task name, validation mechanism — is revisable if implementation discovery contradicts it. The plan is honest intent, not a contract.

If discovery during implementation contradicts the plan, the response is:

1. Stop.
2. Report the drift in conversation.
3. Re-scope the slice or the plan.
4. Proceed.

This matches ADR-003 Reading Note ("pseudocode-level mental model; shapes will change during implementation") and the general deep-dive-review anti-bias rules.

### 3.3 Commit policy — private branch, merge-commit to master

The entire pass lands on `master` as **one merge commit** (not squash), preserving per-slice history behind the merge. Mechanics:

- Work proceeds on a private branch `feat/adr003-impl`.
- Each slice closes with **one local commit** on the feat branch, created only when the slice's worktree health check is green.
- Slices are development + commit boundaries, in that order.
- No temporary bridges, no pass-through adapters, no "retire in the next commit" debt. If an abstraction is obsolete by end-of-pass, it is removed inside the pass.
- End-of-pass: `git merge --no-ff feat/adr003-impl` on master; history shows master's merge commit + feat branch commits reachable behind it.
- This is deliberate for this pass: bisect stays possible (per-slice commits on feat branch); master history stays compact (single entry point for the entire pass); individual slices are never published as incremental releases.

### 3.4 Cake-native, repo-native

New code uses Cake's own abstractions, not raw BCL primitives:

- **Filesystem / process:** `ICakeContext.FileSystem`, `ICakeContext.StartAndReturnProcess`, Cake `FilePath` / `DirectoryPath`. `IPathService` for every on-disk path (ADR-002 + auto-memory `feedback_always_use_pathservice`). No bare `System.IO.File.*` / `Directory.*` / `Process.Start` in new code.
- **CLI wrapping:** repository's existing `Tool<TSettings>` / `Aliases` / `Settings` triad convention (`Vcpkg*`, `Dumpbin*`, `Ldd*`, `Otool*` are the references). New CLI wrappers (`CMake`, `NativeSmokeRunner`, possibly `Tar`) follow the same shape. Shell-outs outside a Cake tool wrapper are flagged for review.
- **Already-built Cake aliases preferred over re-wrapping:** `DotNetBuild`, `DotNetPack`, `DotNetTest`, `DotNetRestore`, `CleanDirectory`, `CopyFile` and peers live in `Cake.Common` and are consumed directly from Application-layer code. Only CLIs Cake does not natively expose get new wrappers.
- **External tooling:** `Cake.CMake` NuGet addin is the preferred first path for CMake-based native-smoke invocation. Binding validity is spiked in slice A, not discovered in slice D. Fallback path if it fails: repo-local `CMakeTool`/`CMakeAliases`/`CMakeSettings` triad following the Vcpkg pattern (template is already in-repo).

### 3.5 Stage independence (the core graph principle)

Post-refactor, every **stage task** is independently invocable; no `[IsDependentOn]` between stages. Input arrives via CLI + artifacts; prerequisites are validated by runner-level precondition checks. The **umbrella task** `SetupLocalDev` carries **zero** `[IsDependentOn]` as well — all composition is internal to the resolver's body (service-to-service via DI), not graph pass-through. Cake's `[IsDependentOn]` mechanism is not used anywhere in the post-refactor Cake surface.

This is the shape CI/CD actually needs: every `release.yml` job is an independent invocation of one Cake target; cross-stage coordination happens via `needs:` + artifact upload/download, not via Cake's `IsDependentOn` mechanism.

Local-dev ergonomics are preserved by `SetupLocalDev`'s resolver-internal composition staying ADR-003 §3.3 Option A (private composition).

### 3.6 Resolve-once, immutable-downstream

ADR-003 §2.4. Within any single invocation (CI workflow run OR local Cake composite run), the version mapping is resolved exactly once and passed immutably to all downstream stages. Re-resolve, re-derive, or shadow inside the same invocation is a contract violation.

Mechanized this way in this pass:

- CI path: `ResolveVersions` job runs once, emits JSON; downstream jobs consume via `needs:` outputs + `--explicit-version` arg.
- Local path: `LocalArtifactSourceResolver.PrepareFeedAsync` calls a provider once, passes the resulting mapping through every stage runner it composes.
- Stage tasks (`Package`, `PackageConsumerSmoke`, `PreFlightCheck`) accept only `--explicit-version` — they never see `ManifestVersionProvider` / `GitTagVersionProvider` directly. Those providers are reachable only via `ResolveVersions` target (for CI) and via the `SetupLocalDev` resolver (for local dev).

### 3.7 Single scope axis (scope = versions.keys)

ADR-003 §2.2. The version mapping's key set IS the scope. Separate `--family` + `--explicit-version` inputs would duplicate scope information and invite drift. One axis only.

Implication: `--family` CLI option retires alongside `--family-version`. `PackageFamilySelector` reads scope from the mapping keys. `Harvest`'s `--library` remains (library-scoped, not family-scoped; a different axis).

### 3.8 CI skeleton grows in parallel with Cake refactor

`release.yml` is authored slice-by-slice alongside the Cake surface it invokes. Each slice deliverable includes:

- Cake target additions / modifications.
- Test suite adjustments (unit / fixture).
- `release.yml` job(s) that invoke the new / changed target.
- Documentation touches (if the user-facing contract changed).

The workflow file is not useful end-to-end until slice E closes, but it is readable at each milestone and proves CI/local parity slice-by-slice.

### 3.9 Local-first: every CI stage is reproducible locally via Cake

The long-term goal of this pass (and ADR-003 in general) is that every step `release.yml` performs is also callable directly via `dotnet run --project build/_build -- --target <X>` on a developer workstation. The `docs/playbook/cross-platform-smoke-validation.md` A–K checkpoint walkthrough is the human-readable specification; `release.yml` is the mechanical execution.

Where today's playbook still shells out to bash for things that belong in Cake (tar extraction, `ldd` batch inspection, solution build, native-smoke CMake invocation), those move into Cake targets in this pass (slice D + slice E).

### 3.10 Test + validation is continuous

Every slice ends with the worktree in a provable health state: build green, test suite green, at least one end-to-end target runnable. Slice D additionally runs a Linux witness. Slice E additionally runs the full three-platform matrix.

No slice closes on "I think this works"; each closes on "this target ran green end-to-end with the stated checks."

---

## 4. Research and Thought Process

This section records the evidence trail that shaped the plan. The goal is that a future reader (human or agent) can reconstruct "why this shape?" without replaying the original session.

### 4.1 Mandatory grounding reads

In plan-first order:

- `AGENTS.md` — operating rules, communication style, Build-Host Reference Pattern (DDD map).
- `docs/onboarding.md` — project purpose, strategic decisions, repo layout, glossary (family / family version / SDL2 vs SDL3).
- `docs/plan.md` — current phase status, strategic decisions table (April 2026 block), roadmap, known issues #19 / #20 / #21 (ADR-003 status, sweep status, test-count baseline).
- `docs/decisions/2026-04-18-versioning-d3seg.md` (ADR-001) — D-3seg, package-first, profile abstraction, G54–G57.
- `docs/decisions/2026-04-19-ddd-layering-build-host.md` (ADR-002) — four-layer DDD, interface three-criteria rule, `LayerDependencyTests`, Tasks/ exception, Wave 6 runner-extraction closure.
- `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (ADR-003) — three axes, resolve-once, stage-owned validation, three providers, Option A composition, PD-7/8/13 direction-selected, G58 new.
- `.github/prompts/general-deep-dive-code-reviewer.prompt.md` — the evidence + anti-bias rubric applied throughout.

### 4.2 Code inspection trail

Read end-to-end (where relevant):

- `build/_build/Program.cs` — composition-root DI wiring; CLI option surface; `IRuntimeScanner` host-RID-bound dispatch. Confirmed: `ConsolidateHarvestTaskRunner` IS DI-registered (line 130 `services.AddSingleton<ConsolidateHarvestTaskRunner>();`).
- `build/_build/Context/BuildContext.cs` — task-boundary binding (confirmed simple, unchanged by this pass).
- `build/_build/Context/Configs/PackageBuildConfiguration.cs` — `Families` list + `FamilyVersion` string; both earmarked for retire under the new mapping contract.
- `build/_build/Context/Options/PackageOptions.cs` — `--family`, `--family-version`, `--source` CLI bindings.
- `build/_build/Context/Models/ManifestConfigModels.cs` — `PackageFamilyConfig` already carries `DependsOn` + `LibraryRef` + `ChangePaths`; topological ordering already implemented in `PackageFamilySelector`.
- `build/_build/Application/Packaging/PackageTaskRunner.cs` — per-family pack loop, harvest precondition gate, G23 + cross-family range rewrite (G56-adjacent).
- `build/_build/Application/Packaging/LocalArtifactSourceResolver.cs` — already Option A: resolver owns pack loop internally; currently resolves per-family versions inline from manifest + timestamp (duplicates a future `ManifestVersionProvider`'s job).
- `build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs` — reads `IRuntimeProfile` for host-RID (bound at composition-root); reads `PackageBuildConfiguration.FamilyVersion` for optional explicit version; default path trusts `Janset.Smoke.local.props` written by SetupLocalDev. Carries two `PostFlight` references in XML doc comments (lines 433, 458) that need to retire alongside PostFlightTask.
- `build/_build/Application/Packaging/ArtifactSourceResolverFactory.cs` + `UnsupportedArtifactSourceResolver.cs` — dispatch on `--source=local|remote|release`; remote/release stubbed with `CakeException` throws (Phase 2b scope).
- `build/_build/Application/Preflight/PreflightTaskRunner.cs` — five validators (version consistency, strategy coherence, core library identity, upstream version alignment G54, csproj pack contract); G54 reads from `PackageBuildConfiguration` today.
- `build/_build/Domain/Preflight/UpstreamVersionAlignmentValidator.cs` — G54 implementation. Reads `PackageBuildConfiguration.Families` (line 29) + `PackageBuildConfiguration.FamilyVersion` (line 35); branches on `requestedFamilies.Count == 1` for strict-minor-alignment gate (line 64). Under mapping-only input, the strict-gate logic changes shape substantially (every mapping entry is a single-family assertion, so strict-minor-alignment applies to all entries unconditionally). Full rewrite of this validator is in slice B1 scope.
- `build/_build/Application/Harvesting/HarvestTaskRunner.cs` — full Harvest body (library loop, RID-status emission, cross-RID invalidation, Spectre rendering); 615 lines. Already extracted from task (Wave 6 complete). RID-status generation + closure walker + planner + deployer + hybrid-static validator (G19) + primary-count post-check (G50) all in place.
- `build/_build/Application/Harvesting/ConsolidateHarvestTaskRunner.cs` — staged-replace swap (`*.tmp` → final), license union / divergence detection, per-library failure aggregation. Uses Cake FileSystem/FilePath abstractions correctly (SHA256 via `ICakeContext.FileSystem`).
- `build/_build/Domain/Packaging/PackageFamilySelector.cs` — already does topological sort over `DependsOn`. Good foundation for `GitTagVersionProvider` multi-family ordering (ADR-003 §3.1).
- `build/_build/Domain/Packaging/PackageVersionResolver.cs` + `IPackageVersionResolver.cs` — scalar string-in/string-out; no manifest knowledge; no git knowledge; no mapping. Earmarked for retire.
- `build/_build/Tasks/Harvest/HarvestTask.cs` — thin adapter; 10 lines of body delegating to runner.
- `build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs` — thin adapter with **vestigial default-ctor fallback** (lines 13-15) alongside a DI ctor (line 17). Since `ConsolidateHarvestTaskRunner` IS DI-registered, Cake.Frosting resolves via the DI ctor; the default ctor is dead weight. Slice D removes it.
- `build/_build/Tasks/Packaging/PackageTask.cs` — thin adapter; `ShouldRun` gates on `FamilyVersion` non-empty; `[IsDependentOn(PreFlightCheckTask)]`. Carries two `PostFlight` references in XML doc comments + one user-facing log message (lines 23, 34, 46) that need to retire alongside PostFlightTask.
- `build/_build/Tasks/Packaging/PackageConsumerSmokeTask.cs` — thin adapter; `[IsDependentOn(PackageTask)]`.
- `build/_build/Tasks/Packaging/SetupLocalDevTask.cs` — thin adapter; `[IsDependentOn(ConsolidateHarvestTask)]`.
- `build/_build/Tasks/PostFlight/PostFlightTask.cs` — body-less umbrella; `[IsDependentOn(PackageConsumerSmokeTask)]`. ADR-003 §2 retires this.
- `build/_build/Tasks/Dependency/DependentsTask.cs` + `LddTask.cs` + `OtoolAnalyzeTask.cs` — diagnostic targets; vestigial `ToolPath = context.Tools.Resolve(...)` on `DependentsTask` to clean.
- `build/_build/Infrastructure/Tools/Vcpkg/*` — canonical Tool/Aliases/Settings convention reference.
- `build/_build/Infrastructure/Tools/Dumpbin/*`, `Ldd/*`, `Otool/*` — same pattern, per-tool variations.
- `build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs` — three invariants (Domain no outward; Infrastructure no Application/Tasks; Tasks only through Application + DTO/Tools exceptions). The `IsDomainOrInfrastructureDtoOrTool` predicate allowing `.Models.` / `.Results.` / `Tools.*` is the **canonical permanent allowance** per ADR-002 §2.8 invariant #3, not a transitional relaxation. The transitional part retired in Wave 6 (Tasks → Domain/Infrastructure **interface** references); that is closed.
- `tests/smoke-tests/native-smoke/CMakePresets.json` — three RIDs present (`win-x64`, `linux-x64`, `osx-x64`); four PA-2 RIDs (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) not present. Drives the 3-RID NativeSmoke cap this pass.

Task graph inventory (all `[IsDependentOn]` attributes as of HEAD):

```text
Info                       (standalone)
EnsureVcpkgDependencies   → Info
Harvest                    → EnsureVcpkgDependencies
ConsolidateHarvest         → Harvest
SetupLocalDev              → ConsolidateHarvest
PreFlightCheck             (standalone)
Package                    → PreFlightCheck
PackageConsumerSmoke       → Package
PostFlight                 → PackageConsumerSmoke
Coverage-Check             (standalone)
Dumpbin-Dependents, Ldd-Dependents, Otool-Analyze  (standalone diagnostics)
```

This is the single-machine-linear-pipeline shape. ADR-003 §3.5 requires this to flatten.

### 4.3 Web research trail

Performed because `Cake.CMake` availability was a real unknown for slice D:

- Searched NuGet / cake-build for Cake.CMake / Cake.Frosting 6 / CMake addin.
- Findings: `Cake.CMake` 1.4.0 published via `cake-contrib`, last updated 2023-10; targets net6.0 + net7.0; exposes `CMake(settings)`, `CMake(directoryPath, settings)`, `CMakeBuild(settings)` aliases. Installation via `dotnet add package Cake.CMake --version 1.4.0` in a Frosting project.
- Judgment: net6/net7 targeting not a hard blocker — Cake.Core 6.x kept the core `ICakeContext` + `Tool<T>` binary interfaces extremely stable, so an addin built against earlier Cake core usually binds fine under 6.1.0. Two-year staleness the bigger concern.
- Spike plan: one 30–60 minute PackageReference-and-trivial-call spike in slice A (not slice D). Binding pass → slice D uses the addin. Binding fail → slice D builds a repo-local `CMakeTool` triad following the `VcpkgTool<T>` template (estimated 3–4 hours). Early decision prevents mid-slice-D surprise.

Tar extraction for `Inspect-HarvestedDependencies` shells out to the native `tar` binary through a repo-local `Infrastructure/Tools/Tar/` Cake wrapper (Vcpkg/Dumpbin/Ldd/Otool triad shape). `tar` is guaranteed on every Inspect-target platform (WSL/Linux runners, macOS runners), preserves Unix symlinks/permissions/xattrs natively — matching exactly what production MSBuild extraction does in `native.tar.gz` consumers — and keeps the Cake host free of a NuGet archive dependency plus the explicit `SymbolicLinkHandler` opt-in trap that SharpCompress requires (default handler silently drops symlinks). The earlier SharpCompress proposal is retired; see §14 change log 2026-04-21.

### 4.4 Two substantive corrections during plan-first

**Correction 1 — SetupLocalDev [IsDependentOn] chain was proposed, then retired.**

First draft kept a `[IsDependentOn(PreFlightCheck, Harvest, NativeSmoke, ConsolidateHarvest, Package)]` chain on `SetupLocalDev` for local-dev ergonomics. Deniz challenged: "stage'ler birbirine `IsDependentOn` ile bağlı olması saçma değil mi — Consolidate ayrı runner'da çalışıyor, CI'da bu chain yok."

Cross-check against ADR-003 §3.3 v1.4 Option A note confirmed the challenge:

> Internal composition goes through Application-layer runners injected via DI, not nested Cake target invocations.

Plus ADR-003 §2.4 (resolve-once) mechanically prevents a Cake `[IsDependentOn]` chain from honoring the immutable-mapping invariant: `IsDependentOn` triggers the dependency before the umbrella body runs, so the mapping cannot exist yet — PreFlight would run with no mapping, violating §2.3 "version-aware by contract."

Correction: `SetupLocalDev` carries zero `[IsDependentOn]`. All composition is internal to `LocalArtifactSourceResolver.PrepareFeedAsync`. The task body is a one-liner (delegate to resolver). This propagates through the rest of the graph: no stage task has `[IsDependentOn]` on another stage task. Umbrella + stages all flat.

**Correction 2 — Slice B's scope made the composition impossible; split into B1 + B2 + reordered with D.**

After three-reviewer pass, slice B was identified as simultaneously doing (a) CLI contract swap, (b) config reshape, (c) graph-flatten of five `[IsDependentOn]` attributes, (d) PostFlight retire, (e) resolver internal-composition rewrite, (f) `ManifestVersionProvider` + `ResolveVersions` task introduction. The composition rewrite's internal composition list included `NativeSmoke`, but NativeSmoke lands in slice D — the slice B health check was self-contradictory.

Correction: split slice B into **B1** (contract migration; graph intact) and **B2** (graph flatten + resolver rewrite + PostFlight retire), and reorder so that **D lands between B1 and B2** (slice sequence: A → B1 → D → B2 → C → E). This way the resolver rewrite in B2 has all runners it needs (including NativeSmoke) already present, and the composition rewrite happens exactly once rather than twice.

### 4.5 Conflicts closed during plan-first

| # | Conflict | Resolution |
| --- | --- | --- |
| 1 | `SetupLocalDev [IsDependentOn(...)]` chain vs ADR-003 §3.3 Option A + §2.4 resolve-once | Umbrella body-only delegation; internal service composition; zero IsDependentOn. |
| 2 | `--family` + `--explicit-version` input duplication vs ADR-003 §2.2 "scope = versions.keys" | `--family` retires; scope derived from mapping keys. |
| 3 | Stage tasks directly CLI-reachable by ManifestVersionProvider / GitTagVersionProvider vs ADR-003 §3.1 "providers are not Cake CLI targets" | Stage tasks only see `ExplicitVersionProvider` (validates pre-resolved mapping). Other providers reachable only via `ResolveVersions` target + `SetupLocalDev` resolver. |
| 4 | `[IsDependentOn]` chain on stage tasks (Package → PreFlight, ConsumerSmoke → Package, Consolidate → Harvest) vs ADR-003 §3.5 stage independence + CI matrix re-entry | All stage-task IsDependentOn removed; preconditions via runner-level artifact checks; `PostFlight` retires. |
| 5 | G54 duplication if both PreFlight and providers validate upstream coherence | One `UpstreamVersionAlignmentValidator` class; both PreFlight and providers invoke it; one implementation, two call sites. Validator is rewritten in slice B1 to accept a mapping (not `Families`+`FamilyVersion`). |
| 6 | `--explicit-version` CLI syntax ambiguity (comma-separated vs repeated option) | Locked: repeated option with `ArgumentArity.ZeroOrMore`. Each occurrence parses as `family=semver`. PD-8 examples use repeated-option shape. |
| 7 | Cake.CMake addin viability as open risk on slice D critical path | Pre-gated by a 30–60 min binding spike in slice A; outcome decides slice D Cake.CMake-vs-repo-local-CMakeTool direction before slice D opens. |
| 8 | LayerDependencyTests "DTO exception" framed as transitional in first draft | Corrected: the `.Models.` / `.Results.` / `Tools.*` allowance is the **canonical permanent shape** per ADR-002 §2.8 invariant #3. The transitional part retired in Wave 6 (Tasks holding Domain/Infrastructure **interfaces**) and is already closed. Slice D/E do not "tighten" the DTO exception; they confirm the interface-allowance retirement. |
| 9 | `ResolveVersions` JSON output shape open on slice B1 critical path | Pinned at plan-lock: flat `{"family-id": "semver-string", ...}` object. Matches provider signature `IReadOnlyDictionary<FamilyId, NuGetVersion>` + YAML `fromJson` consumption + `ExplicitVersionProvider` CLI parser, 1:1. |

### 4.6 Open questions carried into implementation

These are open at plan-lock but small-surface enough not to block the slice order:

- **`NativeSmoke` RID-status emission shape:** whether it emits its own `rid-status/*.json` (mirroring Harvest) or writes into the existing Harvest RID status file as a new field. Decided during slice D based on what ConsolidateHarvest + downstream consumers actually need.
- **`PackageConsumerSmoke` post-smoke assertions (symlink verification, payload integrity):** whether to add them inside the runner (attractive — keeps consumer smoke authoritative) or leave as a separate diagnostic target. Decided during slice C.
- **Full-train meta-tag mechanism format:** ADR-003 §6 PD-7 picked direction ("meta-tag + manifest-driven topological ordering") but did not lock tag format (`train-<YYYYMMDD>`? `train-<semver>`? something else). Out of scope for this pass; locked when Stream D-ci lands.

---

## 5. Target Architecture Summary

### 5.1 Cake task graph (post-refactor)

**All stage tasks are flat — no `[IsDependentOn]` between them.** The umbrella task also carries zero `[IsDependentOn]`.

**Stage tasks** (each a CI job entry point; also operator-callable for PD-8 manual escape):

- `ResolveVersions` — manifest / git-tag / meta-tag → JSON mapping. CI entrypoint. Not used by local `SetupLocalDev` (resolver composes providers internally).
- `PreFlightCheck` — version-aware by contract; consumes mapping.
- `EnsureVcpkgDependencies` — vcpkg bootstrap + install for current triplet.
- `Harvest` — per-RID binary closure + deployment; version-blind.
- `NativeSmoke` — per-RID native load + codec proof (new stage). **Scope capped this pass at the three locally-validatable RIDs (`win-x64` / `linux-x64` / `osx-x64`)**; `CMakePresets.json` only declares those three today. Extending to the four PA-2 rows requires preset + C++ harness adaptation + GitHub-Actions-only execution (no local arm64 / 32-bit hosts); that is Phase 2b scope.
- `ConsolidateHarvest` — aggregation runner; reads per-RID status files; produces manifest + license consolidation.
- `Package` — single-runner pack; consumes consolidated harvest + mapping.
- `PackageConsumerSmoke` — per-RID matrix re-entry; stateless-callable (RID + mapping + feed as input).
- `GenerateMatrix` — emits JSON matrix from `manifest.runtimes[]` for CI fan-out consumption. Owner: slice D.
- `Publish` — single-runner transfer; feed + auth. **Stub in this pass** (concrete `PublishStaging` / `PublishPublic` bodies are Phase 2b Stream D-ci); the stub lands during slice E so `release.yml` can declare the job without ever successfully executing it until Phase 2b.

**Umbrella task** (operator ergonomics; internal composition via DI):

- `SetupLocalDev` — Local profile feed prep. Zero `[IsDependentOn]`. Body delegates to `LocalArtifactSourceResolver.PrepareFeedAsync`, which internally composes: resolve mapping (ManifestVersionProvider with local-suffix) → PreflightTaskRunner → per-RID (EnsureVcpkgRunner → HarvestTaskRunner → NativeSmokeTaskRunner) → ConsolidateHarvestTaskRunner → PackageTaskRunner → write `Janset.Smoke.local.props`.
- Remote / Release profiles: interface preserved; concrete implementations Phase 2b.

**Diagnostic / utility tasks** (standalone):

- `CleanArtifacts` — new; wipe `artifacts/{harvest_output,packages,package-consumer-smoke,test-results/smoke}` + native-smoke build dirs. Replaces the bash `rm -rf` preamble in the WSL playbook.
- `Inspect-HarvestedDependencies` — new; per-RID; for Unix RIDs extracts the harvest tarball via the repo-local `Infrastructure/Tools/Tar/` native-tar wrapper into `artifacts/temp/inspect/<rid>/<lib>/` (preserves SONAME symlinks), for Windows RIDs reads `runtimes/<rid>/native/` directly, then invokes the platform scanner (Dumpbin/Ldd/Otool) on the primary binary and logs the expected-vs-actual dep set. Replaces the WSL playbook §5 bash loop.
- `CompileSolution` — new; invokes `DotNetBuild` alias on `Janset.SDL2.sln`. Replaces the WSL playbook §8 solution-build step.
- `Coverage-Check` — existing; standalone ratchet gate.
- `Dumpbin-Dependents` / `Ldd-Dependents` / `Otool-Analyze` — existing single-file diagnostics.
- `Info` — existing.

**Retire:**

- `PostFlight` (body-less umbrella; its role is covered by explicit `--target PackageConsumerSmoke` or the `SetupLocalDev` umbrella). Retire sweep includes three repo-local comment + log-message references (see §7.1).

### 5.2 CI workflow topology (post-slice-E)

Hypothetical shape — exact form firms up as slices land. Job graph:

```text
tag-push OR workflow_dispatch
        │
        ▼
resolve-versions          (calls build-host ResolveVersions)
        │
        ▼
preflight                 (Cake PreFlightCheck + Coverage-Check)
        │
        ▼
generate-matrix           (reads manifest.runtimes[])
    │
    ├──────────► harvest (N RID matrix; fan-out)
    │               │
    │               ▼
    │           native-smoke (3-RID matrix this pass; 7-RID Phase 2b)
    │               │
    │               ▼ (per-RID artifact upload: rid-status + runtimes)
    ▼
consolidate-harvest       (aggregation; reads all RID artifacts)
        │
        ▼
pack                      (single runner; consumes consolidated harvest + mapping)
        │
        ▼
consumer-smoke            (N RID matrix RE-ENTRY; fan-out)
        │
        ▼
publish-staging / publish-public   (stubbed in this pass; bodies Phase 2b Stream D-ci)
```

Every job is a thin YAML wrapper: checkout + Cake target invocation + artifact upload/download. All policy lives in the Cake host.

### 5.3 Version resolution flow

**Provider roles:**

- `IPackageVersionProvider` — scope-aware, async, returns `IReadOnlyDictionary<FamilyId, NuGetVersion>`.
  - `ExplicitVersionProvider` — validates operator-supplied mapping (G54 per entry). Sole provider seen by stage tasks.
  - `ManifestVersionProvider` — composes `<UpstreamMajor>.<UpstreamMinor>.0-<suffix>` per family from manifest upstream versions. Used by `ResolveVersions` (CI manifest-derived mode) and by `LocalArtifactSourceResolver` (with local suffix).
  - `GitTagVersionProvider` — reads family-specific git tag (or meta-tag → per-family resolution) with a `GitTagScope` parameter; validates G54 per entry. Used only by `ResolveVersions` (CI tag-push mode).

**Flow:**

- **Local dev:** `SetupLocalDev` → resolver → `ManifestVersionProvider` (suffix=`local.<ts>`) → mapping → all runners.
- **CI tag push:** `ResolveVersions --source=git-tag` → `GitTagVersionProvider` → JSON → downstream jobs → `ExplicitVersionProvider` in-job validation.
- **CI `workflow_dispatch`:** `ResolveVersions --source=manifest --suffix=ci.<run-id>` or `--source=explicit --from-input` → JSON → downstream.
- **PD-8 manual escape:** operator calls stage targets directly with `--explicit-version k=v` (repeated option); each invocation is its own scope (cross-invocation consistency is operator's responsibility per ADR-003 §2.4 scope clause).

**Invariant:** within a single invocation, mapping is resolved once and flows immutably. Stage tasks never call `ManifestVersionProvider` / `GitTagVersionProvider` directly.

**`ResolveVersions` JSON output shape (locked):** flat object `{"family-id": "semver-string", ...}`. Example:

```json
{"sdl2-core":"2.32.0-ci.run-id-12345","sdl2-image":"2.8.0-ci.run-id-12345"}
```

This maps 1:1 to the provider signature and to YAML `fromJson(...)` consumption with no intermediate parsing. `ExplicitVersionProvider` accepts the same shape from `--explicit-version sdl2-core=2.32.0-ci.run-id-12345` repeated-option CLI input.

### 5.4 Artifact preconditions (per-stage fail-fast)

Each stage runner begins with a precondition check. The existing pattern in `PackageTaskRunner.EnsureHarvestOutputReadyAsync` + `AssertPayloadSubtreesPopulated` is the template; extended to all stages:

- `PreFlightCheck`: manifest present, vcpkg.json present, mapping non-empty.
- `EnsureVcpkgDependencies`: vcpkg submodule present.
- `Harvest`: `vcpkg_installed/<triplet>/` present for current triplet; at least one library in the resolved library list.
- `NativeSmoke`: `artifacts/harvest_output/<lib>/runtimes/<current-rid>/native/` present for each family in scope; `CMakePresets.json` preset present for current RID.
- `ConsolidateHarvest`: at least one `rid-status/*.json` under each library directory.
- `Package`: `harvest-manifest.json` present per library in scope; consolidation receipt valid (already implemented).
- `PackageConsumerSmoke`: at least one nupkg per `<managed-package-id>.*.nupkg` and `<native-package-id>.*.nupkg` under the feed path.
- `Publish`: feed URL + auth token present; nupkgs present locally.

Error messages must state the missing prerequisite and the remediation target — e.g., "Run `--target Harvest --rid <rid>` first" — not raise generic `NullReferenceException`.

---

## 6. Implementation Slices

Six waves, sequenced A → B1 → D → B2 → C → E. This ordering:

- **A** introduces the provider seam with zero behavior change, plus a Cake.CMake binding spike and ResolveVersions JSON shape pin (both cheap, both pre-gate slice D risks).
- **B1** is contract migration only: CLI swap, config reshape, provider-adoption by runners, `IPackageVersionResolver` retire, `UpstreamVersionAlignmentValidator` rewrite. The Cake graph stays the old chain-based shape; health check runs against today's SetupLocalDev path with mapping-driven versions.
- **D** extracts NativeSmoke, lands Cake-native infrastructure (Cake.CMake binding, `Infrastructure/Tools/Tar/` native-tar wrapper for the Inspect diagnostic, new utility tasks), cleans the vestigial default-ctor fallback. Graph is still chain-based but now has one more chain link (`NativeSmokeTask [IsDependentOn(HarvestTask)]`, `ConsolidateHarvestTask [IsDependentOn(NativeSmokeTask)]`). Ends with a Linux witness.
- **B2** flattens the graph, rewrites `SetupLocalDev` resolver to internal-composition with all runners (NativeSmoke now included), retires PostFlight (task + reference sweep). This is the biggest structural change; it happens exactly once, after every runner it needs to call exists.
- **C** introduces per-stage request records, `GitTagVersionProvider`, G58 validator, stateless `PackageConsumerSmoke`.
- **E** closes the playbook cake-ification gaps, runs the full three-platform matrix, lands `Publish` stubs + `release.yml` finalization.

One local commit on `feat/adr003-impl` per slice close. End-of-pass `git merge --no-ff feat/adr003-impl` to master.

### 6.1 Slice A — Version-provider seam + ExplicitVersionProvider + Cake.CMake spike

**Goal:** land the new abstraction with a single implementation that mirrors existing behavior. No behavior change visible to operators. Pre-gate slice D risks.

**Scope:**

- Introduce `IPackageVersionProvider` interface in the new `Application.Versioning` namespace.
- Introduce `ExplicitVersionProvider` as its first implementation; validates each entry against `ManifestConfig` via the existing `IUpstreamVersionAlignmentValidator` (G54 reuse; pre-rewrite shape still works because mapping is still scalar-backed at this slice).
- Register in DI.
- Add unit tests: happy-path mapping, G54 reject, empty-scope reject.
- **Cake.CMake binding spike:** add `PackageReference Include="Cake.CMake" Version="1.4.0"` to `Build.csproj` + `Directory.Packages.props`. Write one trivial Cake.CMake-alias-invocation test case (e.g., `context.CMake(new CMakeSettings { SourcePath = somePath })` wrapped in a try/catch that captures binding exceptions). Spike result recorded in-conversation: addin usable → slice D uses it; addin binding fails → slice D scope includes repo-local `CMakeTool` triad.
- **`ResolveVersions` JSON shape pin:** add an inline comment at the top of `ExplicitVersionProvider` (or a dedicated `VersionMappingContract.cs` Domain record) documenting the flat `{family-id: semver}` shape as the canonical CLI+JSON+provider contract.

**Intentionally out of scope:**

- `ManifestVersionProvider`, `GitTagVersionProvider`.
- Any change to `PackageTaskRunner` / `LocalArtifactSourceResolver` / `PackageConsumerSmokeRunner` consumption shape.
- CLI option changes.
- Per-stage request records.
- `NativeSmoke` task.
- G58 validator.
- Graph flattening.

**CI deliverable:** create `.github/workflows/release.yml` skeleton with trigger stanzas (`push.tags`, `workflow_dispatch`) and a single `resolve-versions-stub` job with a placeholder step. Establishes file and trigger shape; no real work yet.

**Worktree health check:**

- `dotnet build build/_build` green.
- `dotnet test build/_build.Tests` green (baseline plus new provider tests).
- `LayerDependencyTests` green (new `Application.Versioning` namespace respects layer invariants).
- Cake.CMake spike outcome recorded.

**Close slice A with one commit on `feat/adr003-impl`:** `slice A: IPackageVersionProvider seam + ExplicitVersionProvider + Cake.CMake spike`.

### 6.2 Slice B1 — Mapping contract migration (graph intact)

**Goal:** flip the consumption path from scalar `--family-version` + `--family` to mapping-based `--explicit-version`. Reshape `PackageBuildConfiguration`. Adopt provider pattern everywhere. Graph stays chain-based; resolver keeps its current composition shape (with inline version derivation replaced by `ManifestVersionProvider`).

**Scope:**

- Retire `--family-version` CLI option + `--family` CLI option simultaneously. Parser rejects with migration-hint error on any attempt to use them.
- Introduce `--explicit-version` CLI option (repeated `ArgumentArity.ZeroOrMore`; each occurrence parses as `family=semver`).
- Redesign `PackageBuildConfiguration` to carry `ExplicitVersions: IReadOnlyDictionary<string, NuGetVersion>` only (remove `Families` list and `FamilyVersion` string in one hit).
- `PackageTaskRunner` consumes the mapping; existing per-family loop reads from mapping keys (scope). `PackageFamilySelector` reads from mapping keys.
- `PackageTask.ShouldRun` gates on "mapping non-empty" instead of "`FamilyVersion` non-empty."
- Retire `IPackageVersionResolver` + `PackageVersionResolver` + related result types in the same hit (callers now use `ExplicitVersionProvider`).
- Introduce `ManifestVersionProvider`; wire into `LocalArtifactSourceResolver` (replaces inline version synthesis); register in DI.
- Introduce `ResolveVersions` stage task (manifest source only for this slice; git-tag source added in slice C). Output JSON to stdout in the pinned flat shape; argparse validates `--source`, `--suffix`, `--scope`.
- **Rewrite `UpstreamVersionAlignmentValidator` (G54)** to accept a mapping instead of `(Families, FamilyVersion)`. Every mapping entry becomes a strict-minor-alignment assertion (the old `requestedFamilies.Count == 1` special case collapses). PreFlight invocation updated; provider-side invocation added.
- `PackageConsumerSmokeRunner` consumes the mapping; existing local.props-based default path still works.
- **Graph stays chain-based for this slice.** `[IsDependentOn]` attributes unchanged. `LocalArtifactSourceResolver.PrepareFeedAsync` still uses today's composition shape (inline pack loop through `PackageTaskRunner`); the inline version-derivation body is replaced by a single `_manifestVersionProvider.ResolveAsync(...)` call at the top.
- Test fixtures: update every test that constructed `PackageBuildConfiguration(families, familyVersion)` to construct with a mapping. Update every test that mocked `IPackageVersionResolver` to mock `IPackageVersionProvider` or use `ExplicitVersionProvider` directly with fixture mappings. Rewrite `UpstreamVersionAlignmentValidatorTests` for mapping shape.

**Out of scope for B1 (lands in B2):**

- Removing `[IsDependentOn]` from stage tasks.
- `SetupLocalDev` resolver internal-composition rewrite.
- `PostFlight` retirement.

**CI deliverable:** `release.yml` gets real `resolve-versions` job (invokes `--target ResolveVersions --source=manifest --suffix=ci.<run-id>`) + real `preflight` job (invokes `--target PreFlightCheck --explicit-version ...` with `needs: resolve-versions` output). Pack / ConsumerSmoke jobs remain stubbed.

**Worktree health check:**

- `dotnet build build/_build` green.
- `dotnet test build/_build.Tests` green.
- `LayerDependencyTests` green.
- `SetupLocalDev --source=local --rid win-x64` end-to-end green on Windows host (unchanged composition shape; new mapping-driven versions behind the scenes).
- `dotnet run --project build/_build -- --target ResolveVersions --source=manifest --suffix=test` emits valid JSON to stdout matching the pinned shape.

**Close slice B1 with one commit on `feat/adr003-impl`:** `slice B1: mapping contract migration + ManifestVersionProvider + ResolveVersions (manifest source)`.

### 6.3 Slice D — NativeSmoke extraction + Cake-native polish + one Linux witness

**Goal:** extract the C++ native-smoke harness into a Cake target; land CMake tooling; clean the `ConsolidateHarvestTask` default-ctor fallback; introduce Cake-native diagnostic + utility targets that fold WSL-playbook bash steps into Cake; run a Linux witness to de-risk before slice B2's bigger structural change.

**Scope:**

- **CMake tooling:** based on slice A spike outcome.
  - If `Cake.CMake` 1.4.0 binds cleanly: use the addin directly from `Application/Harvesting/NativeSmokeTaskRunner.cs`.
  - If binding fails: add `Infrastructure/Tools/CMake/CMakeTool.cs` + `CMakeAliases.cs` + `CMakeSettings.cs` triad following the `VcpkgTool<T>` template.
- Add `Infrastructure/Tools/NativeSmoke/NativeSmokeRunnerTool.cs` — thin Cake-native wrapper that invokes the built `native-smoke` executable and captures stdout / exit code.
- Add `Application/Harvesting/NativeSmokeTaskRunner.cs` + `Tasks/Harvest/NativeSmokeTask.cs`. Runner responsibilities: precondition-check harvest output for current RID, invoke CMake configure (preset per-RID from `CMakePresets.json`) → CMake build → native-smoke executable; emit per-RID pass/fail evidence. The target shape is symmetric 7-RID per the manifest; today's `CMakePresets.json` only ships win-x64/linux-x64/osx-x64 presets, so the four PA-2 RIDs (win-arm64, win-x86, linux-arm64, osx-arm64) naturally fail at `cmake --preset <rid>` until the preset file grows in Phase 2b — no hard-coded cap in the runner.
- Remove vestigial default-ctor fallback from `ConsolidateHarvestTask` (DI ctor is already the resolved path; the fallback is dead weight).
- Clean `DependentsTask.cs` ToolPath vestige (line already noted).
- Add `Infrastructure/Tools/Tar/` wrapper (Vcpkg/Dumpbin/Ldd/Otool triad shape) for `Inspect-HarvestedDependencies` — shells out to the platform `tar` binary. Pivot from the earlier SharpCompress proposal: native `tar` preserves Unix symlinks/permissions/xattrs by default (critical for SONAME chains), matches exactly what production MSBuild extracts in `native.tar.gz` consumers, and avoids the `SymbolicLinkHandler` silent-drop trap SharpCompress imposes. See §14 change log 2026-04-21 + §11 Q9.
- Add new diagnostic/utility tasks:
  - `CleanArtifacts` — wipes `artifacts/{harvest_output,packages,package-consumer-smoke,test-results/smoke,harvest-staging,temp/inspect,matrix}` + native-smoke build dirs (Cake-native via `ICakeContext.DeleteDirectory`).
  - `Inspect-HarvestedDependencies --rid <rid>` — per-library: for Unix RIDs extract harvest tarball via the native-tar wrapper into `artifacts/temp/inspect/<rid>/<lib>/`, for Windows RIDs read `runtimes/<rid>/native/` directly; locate primary binary per manifest pattern; invoke platform scanner (`Dumpbin-Dependents` / `Ldd-Dependents` / `Otool-Analyze` aliases) on the resolved file; log dep set.
  - `CompileSolution` — invokes `DotNetBuild` alias on `Janset.SDL2.sln`.
  - `GenerateMatrix` — emits the GitHub-Actions-shape `matrix` JSON into `artifacts/matrix/runtimes.json` from `manifest.runtimes[]`. Full 7-RID symmetric (harvest + native-smoke matrices both read the same file; 4 PA-2 rows surface as natural CMake-preset failures until Phase 2b).
- **Graph adjustment within the old chain model:** `NativeSmokeTask [IsDependentOn(HarvestTask)]`; `ConsolidateHarvestTask [IsDependentOn(NativeSmokeTask)]` (replaces its old `[IsDependentOn(HarvestTask)]`). Other chain attributes unchanged at this slice (they all retire in B2).
- Tests: unit test `NativeSmokeTaskRunner` precondition paths (library resolution, harvest-output presence, RID filtering) on fake filesystem; unit test `Inspect-HarvestedDependencies` runner (fake filesystem + tar-wrapper mock / stub); unit test `CleanArtifacts` (fake filesystem); unit test `GenerateMatrix` (fake filesystem + manifest seed); unit test the `Infrastructure/Tools/Tar/` wrapper's argument shape via Cake.Testing fixture.

**CI deliverable:** `release.yml` grows `generate-matrix` job (emits JSON matrix from `manifest.runtimes[]` via the new `GenerateMatrixTask` — decision: D scope, self-contained), `harvest` matrix job, `native-smoke` matrix job (symmetric 7-RID; 4 PA-2 rows will fail at `cmake --preset <rid>` until `tests/smoke-tests/native-smoke/CMakePresets.json` grows in Phase 2b — explicit YAML comment pinned to the job), `consolidate-harvest` aggregation job. All with real Cake invocations. Pack / ConsumerSmoke jobs still stubbed.

**Worktree health check:**

- `dotnet build build/_build` green with `Cake.CMake` reference + the repo-local `Infrastructure/Tools/Tar/` wrapper.
- `dotnet test build/_build.Tests` green.
- `LayerDependencyTests` green.
- `CleanArtifacts` → `SetupLocalDev --source=local --rid win-x64` green on Windows (NativeSmoke now runs inside the old chain between Harvest and Consolidate).
- Standalone `--target NativeSmoke --rid win-x64` invocation green against pre-existing harvest output.
- `Inspect-HarvestedDependencies --rid win-x64` produces expected per-library dep dump matching WSL-playbook §5 bash output.
- **Linux witness — early, before diagnostics** (F-XPLAT mitigation; Deniz direction 2026-04-21): on WSL, run `--target NativeSmoke --rid linux-x64` immediately after `NativeSmokeTaskRunner` + CMake tooling lands — before `Inspect-HarvestedDependencies` / `CleanArtifacts` / `CompileSolution` diagnostic tasks + `ConsolidateHarvestTask` default-ctor cleanup. If Cake.CMake binding / `NativeSmokeRunnerTool` process invocation breaks on Linux, we catch it with the smallest-possible set of moving parts. Then add the diagnostic utility tasks. Then re-run the full witness (`--target CleanArtifacts` → `--target NativeSmoke --rid linux-x64` → `--target Inspect-HarvestedDependencies --rid linux-x64`) as the slice close check. Two passes but each cheap (~5-10 minutes each); first-pass signal is maximum about the platform-sensitive infrastructure itself.

**Close slice D with one commit on `feat/adr003-impl`:** `slice D: NativeSmoke task + Cake-native polish + Linux witness`.

### 6.3.1 Slice DA — Smoke playbook Cake-first alignment (doc-only)

**Opened 2026-04-21** during Slice D pre-witness review after Deniz's "Slice D'ye kadar eklediğimiz her ama her command'in cake üzerinden çalışıyor olması gerekiyor" direction. Slice-D code landed in `1ef3a82` with the full Cake target surface in place; the smoke playbooks still referenced retired CLI flags (`--family` / `--family-version`), raw shell blocks where dedicated Cake targets exist (`rm -rf artifacts/…`, `tar -xzf native.tar.gz`, `cmake --preset <rid>`, per-TFM `dotnet test` loop), and obsolete planned-vs-active checkpoint status (`GenerateMatrix` listed under Stream C Planned when it landed Slice D Active). DA closes that gap for the smoke-witness trio before the first Unix witness runs; broader doc sweep remains Slice E scope per §6.6.

**Goal:** every command in the smoke-validation surface invokes a Cake target, with the pre-Cake bootstrap exception (checkpoints A + B) explicitly annotated. Playbook readers can literal-copy-paste the witness sequence without translating retired flags or raw shell blocks.

**Scope (tight):**

- `docs/playbook/cross-platform-smoke-validation.md` — A–K command reference + Clean-Slate Artifact Rule + Stage-Owned Validation Mapping + PA-2 Per-Triplet Witness Invocations all swept to Cake-first. Planned checkpoints table: `H (GenerateMatrix)` promoted out (now Active as checkpoint `O` under Slice D landed additions); `I (PreFlightCheck as a CI gate)` marked landed (B1); `N (CleanArtifacts)`, `O (GenerateMatrix)`, `P (CompileSolution)` added as landed-Slice-D Active rows. Cake-first lead + raw shell fallback for F1 (Inspect) and G (NativeSmoke); `--family-version` → `--explicit-version key=value` everywhere; `--target PostFlight --family ... --family-version ...` → `--target SetupLocalDev --source=local --rid <rid>` as primary K path; PA-2 witness section migrated to `SetupLocalDev --suffix=pa2-witness.1` primary + explicit-version mapping alternative.
- **New file** `docs/playbook/unix-smoke-runbook.md` — unified operational witness runbook for Linux (WSL) + macOS Intel (SSH), platform-param via `$PLATFORM` / `$RID` / `$PRESET` env vars. Covers pre-Cake bootstrap (§3), full Cake pipeline (§4 PreFlight → EnsureVcpkg → Harvest → NativeSmoke → Consolidate), Cake-first dependency inspection (§5), SetupLocalDev + PackageConsumerSmoke (§6), ancillary symlink check (§7), quick summary + what-to-send-back (§8-9), failure triage. Single file replaces both TEMP playbooks at 95% parity.
- **Retire** `docs/playbook/TEMP-wsl-smoke-commands.md` (`git rm`).
- **Retire** `docs/playbook/TEMP-macos-smoke-commands.md` (`git rm`).

**Out of scope (deferred to Slice E §6.6):**

- `AGENTS.md`, `docs/onboarding.md`, `docs/plan.md` — broader command-surface references.
- `docs/knowledge-base/cake-build-architecture.md` — task surface + stage mapping.
- `docs/knowledge-base/release-guardrails.md` — stage-ownership mapping from operator side.
- `docs/knowledge-base/release-lifecycle-direction.md` — policy-level CLI references.
- `docs/phases/phase-2-adaptation-plan.md` — PD-10 / PD-14 CLI references.

Slice E already scopes "Rewrite the WSL temp runbook to invoke Cake targets exclusively. Bash wrapping limited to environment setup + log-tee." Slice DA closes the `unix-smoke-runbook.md` part of that scope ahead of witness. Slice E's remaining obligation narrows to the broader doc sweep + post-ADR-003-CI-rewrite companion update.

**Worktree health check:**

- `dotnet run --project build/_build/Build.csproj -- --description` resolves every task name referenced in the three touched playbooks (no broken references).
- Retired-flag grep (`--family` / `--family-version`) returns zero hits in the touched playbooks (the flags still live in the legacy `_archive/` directory and in v2.* change-log entries, which are historical).
- Markdown lint + GFM table column-count warnings clear on all touched files (TEMP files deleted cleanly; no dangling cross-references remain in the repo from the retired files outside historical change logs).
- `git status` clean after DA commit.

**Close slice DA with one commit on `feat/adr003-impl`:** `slice DA: smoke playbooks Cake-first alignment (cross-platform-smoke-validation + unix-smoke-runbook; retire TEMP-wsl + TEMP-macos)`.

After DA lands, Slice D witness (D.15) runs against the updated `unix-smoke-runbook.md` directly on WSL + macOS. Slice D closure commit (D.16) follows witness success.

### 6.4 Slice B2 — Graph flattening + SetupLocalDev composition split + PostFlight retire

**Goal:** fully flatten the Cake task graph per ADR-003 §3.5; split pipeline composition out of the `IArtifactSourceResolver` seam into a dedicated `SetupLocalDevTaskRunner`; retire `PostFlight` (task + reference sweep); drop `NativeSmokeTaskRunner` from the local-dev flow.

> **Amendment (2026-04-21, post-initial-review).** The originally proposed "resolver internal-composition rewrite" landed in the working tree and was rolled back after Deniz review. Two concerns held: (a) `LocalArtifactSourceResolver` growing to 11 constructor dependencies violated its "resolver" framing (a resolver consumes an existing feed; it does not produce one); (b) `NativeSmokeTaskRunner` in the local-dev flow imposes CMake + MSVC Developer shell as a prereq for managed-only iteration, which is orthogonal to feed materialisation. Replaced with the split below. ADR-003 §3.3 amendment (v1.6) documents the decision.

**Scope:**

- **Graph flattening:** remove every `[IsDependentOn(...)]` between stage tasks:
  - `HarvestTask`: remove `[IsDependentOn(EnsureVcpkgDependenciesTask)]`.
  - `NativeSmokeTask`: remove `[IsDependentOn(HarvestTask)]`.
  - `ConsolidateHarvestTask`: remove `[IsDependentOn(NativeSmokeTask)]`.
  - `SetupLocalDevTask`: remove `[IsDependentOn(ConsolidateHarvestTask)]`.
  - `PackageTask`: remove `[IsDependentOn(PreFlightCheckTask)]`.
  - `PackageConsumerSmokeTask`: remove `[IsDependentOn(PackageTask)]`.
- **SetupLocalDev composition split (amended approach):**
  - Widen `IArtifactSourceResolver` so `PrepareFeedAsync` and `WriteConsumerOverrideAsync` both accept a resolved `IReadOnlyDictionary<string, NuGetVersion>` mapping — mapping flows in; the seam stops resolving from mutable DI state.
  - Narrow `LocalArtifactSourceResolver` to a 4-param ctor (`ICakeContext`, `ICakeLog`, `IPathService`, `ManifestConfig`) with only two responsibilities: verify the expected managed + native nupkgs exist under `PackagesOutput` for the supplied mapping, and stamp `Janset.Smoke.local.props`. No pipeline runners injected.
  - Introduce `Application/Packaging/SetupLocalDevTaskRunner` owning the ordered Local-profile composition:
    1. Resolve mapping via `ManifestVersionProvider` (local suffix).
    2. `PreflightTaskRunner.Run(context, packageBuildConfiguration)` — fail-fast on G54 / G49 / etc.
    3. `EnsureVcpkgDependenciesTaskRunner.Run(context)`.
    4. `HarvestTaskRunner.RunAsync(context)`.
    5. `ConsolidateHarvestTaskRunner.RunAsync(context)`.
    6. `PackageTaskRunner.RunAsync(packageBuildConfiguration, ct)`.
    7. `IArtifactSourceResolver.PrepareFeedAsync(context, mapping, ct)` → `WriteConsumerOverrideAsync(context, mapping, ct)`.
  - Non-Local profiles (`RemoteInternal`, `ReleasePublic`) skip steps 1–6 and delegate straight to the resolver — `UnsupportedArtifactSourceResolver` surfaces the Phase-2b not-implemented error until remote / release acquisition lands.
  - **NativeSmoke is deliberately absent from this composition.** It requires CMake + a platform C/C++ toolchain (MSVC Developer shell on Windows) which is orthogonal to feed materialisation. NativeSmoke remains reachable via the standalone `--target NativeSmoke --rid <rid>` invocation and via the CI harvest matrix per-RID.
  - `SetupLocalDevTask` becomes a thin adapter: inject `SetupLocalDevTaskRunner`, delegate `RunAsync(context)`.
- **Topological helper extraction:** the `PackageFamilySelector` topological-sort logic (previously inlined in `PackageTaskRunner` during the initial B2 pass) moves to `Domain/Packaging/FamilyTopologyHelpers.cs` as a deterministic Kahn-based static helper with cycle detection and alphabetical tie-break. Dedicated unit tests (`FamilyTopologyHelpersTests`) cover independent / dependent / out-of-scope-dep / cycle / empty scenarios. `PackageTaskRunner` calls the helper instead of carrying a copy.
- **Precondition-check pattern rollout:** each runner's `RunAsync` verifies input-artifact presence at top of body and raises a precondition-specific `CakeException` with remediation hint. `PackageTaskRunner.EnsureHarvestOutputReadyAsync` is the existing template; extend to `PreflightTaskRunner`, `HarvestTaskRunner`, `NativeSmokeTaskRunner`, `ConsolidateHarvestTaskRunner`, `PackageConsumerSmokeRunner`. (Request-record shapes arrive in Slice C; B2 runners accept their existing-shape inputs plus the mapping.)
- **`ConsolidateHarvestTaskRunner` soft-fail → hard-fail:** previous `return` + warning on missing `harvest_output` / empty library tree becomes a precondition guard with a CI-aware remediation hint ("Run `--target Harvest` first, or fetch the per-RID harvest artifacts into this path when consolidating in a multi-runner CI pipeline"). Aligns with ADR-003 §4 stage-owned validation.
- **PostFlight retirement sweep:**
  - Delete `build/_build/Tasks/PostFlight/PostFlightTask.cs` (and the folder).
  - Remove `PostFlight` reference from `PackageTask.cs` line 23 XML doc comment.
  - Remove `PostFlight` reference from `PackageTask.cs` line 34 XML doc comment.
  - Rewrite `PackageTask.cs` line 46 user-facing log message (replace "PostFlight release chain always supplies the flag" with wording reflecting the new mapping-driven invocation surface).
  - Rewrite `PackageConsumerSmokeRunner.cs` line 433 XML doc comment (replace "a single PostFlight run" with platform-neutral wording).
  - Rewrite `PackageConsumerSmokeRunner.cs` line 458 XML doc comment (same pattern).
- Update tests that mocked the `[IsDependentOn]` chain behavior to inject runners directly.

**CI deliverable:** `release.yml` jobs now invoke stage tasks in the flat shape (each job is its own Cake invocation; `needs:` carries artifact + mapping; no inherited task chain from prior jobs). `generate-matrix` may land here if it didn't in slice D.

**Worktree health check:**

- `dotnet build build/_build` green.
- `dotnet test build/_build.Tests` green.
- `LayerDependencyTests` green; interface-allowance permanent closure verified (no Task → Domain/Infrastructure interface refs; `.Models.`/`.Results.`/`Tools.*` refs allowed per canonical ADR-002 §2.8 invariant #3).
- `dotnet run --project build/_build -- --tree` shows flat graph: all stage tasks standalone, SetupLocalDev standalone, no PostFlight. Task list matches §5.1.
- `SetupLocalDev --source=local --rid win-x64` end-to-end green on Windows (resolver now drives full internal composition).
- Standalone `--target PreFlightCheck --explicit-version sdl2-core=2.32.0-local.<ts>,...` green against an already-populated feed.
- Standalone `--target Package --explicit-version ...` fails fast with remediation hint if harvest output is missing (verify precondition-check messages are user-readable).

**Close slice B2 with one commit on `feat/adr003-impl`:** `slice B2: graph flatten + resolver internal-composition rewrite + PostFlight retire`.

### 6.4.1 Slice BA — Smoke witness file-based app (post-B2 micro-slice)

**Goal:** lift the post-B2 full-smoke ceremony (CleanArtifacts → SetupLocalDev → PackageConsumerSmoke + mini CI replay of the flat stage graph) out of shell copy-paste and into a single-file, cross-platform .NET 10 witness tool.

**Scope:**

- New `tests/scripts/smoke-witness.cs` file-based .NET 10 app ([MS Learn reference][msdocs-file-based-apps]) with two subcommands (default `local`):
  - `local`: `CleanArtifacts → SetupLocalDev → PackageConsumerSmoke` — fast dev iterate, validates resolver handoff end-to-end.
  - `ci-sim`: `CleanArtifacts → ResolveVersions → PreFlightCheck → EnsureVcpkgDependencies → Harvest → NativeSmoke → ConsolidateHarvest → Package` — mini CI replay, every stage separate `Process.Start` with the `ResolveVersions`-emitted mapping (ADR-003 §2.4 resolve-once invariant enforced at the script boundary). `PackageConsumerSmoke` deliberately skipped until Slice C parameterises the runner on `--feed-path` + `--explicit-version` (no local-props injection from the script; mini CI sim stays faithful to CI's job-level contract).
- Spectre.Console for the UI (Rule per stage, final Panel + Table summary with pass/fail + duration, `FigletText` header). `System.Diagnostics.Process` for per-stage invocations; `System.Diagnostics.Process.StandardOutput.ReadToEndAsync()` + `File.WriteAllTextAsync` to tee each step's stdout + stderr to `.logs/witness/<platform>-<mode>-<runId>/NN-<step>.log`.
- Log root `/.logs/` (new gitignore entry) kept **outside** `artifacts/` so `CleanArtifacts` cannot wipe witness logs mid-run. Each invocation clears its timestamped subdir first for a guaranteed clean slate even on same-second-collision.
- Concrete-family filter: script reads `build/manifest.json` itself and filters `ResolveVersions` output to families with both `managed_project` + `native_project` declared; mirrors `SetupLocalDevTaskRunner`'s internal filter so ci-sim drives exactly the set `release.yml` pack job would.
- SDK scoping: `tests/scripts/global.json` pins `10.0.100+` (file-based apps are .NET 10 only); repo-root `global.json` stays on `9.0.200` for the build-host. Launch UX: `cd tests/scripts/ && dotnet run smoke-witness.cs [mode]` on Windows; `chmod +x tests/scripts/smoke-witness.cs && ./tests/scripts/smoke-witness.cs [mode]` on Unix (shebang `#!/usr/bin/env dotnet` + `LF` enforced via `tests/scripts/.gitattributes`).
- CPM-native package reference: `Spectre.Console 0.49.1` added to root `Directory.Packages.props`; script uses `#:package Spectre.Console` (no inline version) per the file-based-apps CPM contract.
- Directory.Build.props inheritance retained (Deniz direction: "zararı yok"). Only compat cost was mirroring `Build.csproj`'s `NoError` pattern for `CA1502`/`CA1505` (single-file Main absorbs all helper complexity into `<Main>$`).
- `tests/scripts/README.md` documents UX (Unix shebang vs Windows `dotnet run`), requirements (.NET 10 SDK, git, CMake + MSVC Developer shell only for ci-sim NativeSmoke on Windows), design notes (each stage = separate process, ResolveVersions is the single version source, logs outside `artifacts/`), and troubleshooting (SDK selection, JNSMK001 on local, shebang CRLF failures).

**Health check (Windows):**

- `./smoke-witness.cs local` — 3/3 PASS, 132.6s (`CleanArtifacts` 4.4s, `SetupLocalDev` 61.0s, `PackageConsumerSmoke` 67.3s). Smoke csproj restore + net9.0 + net8.0 + net462 TUnit pass + ns2.0 compile-sanity all Succeeded.
- `./smoke-witness.cs ci-sim` — 5/6 PASS through `Harvest` in 22.4s (`CleanArtifacts` 2.0s → `ResolveVersions` 1.9s → `PreFlightCheck --explicit-version sdl2-core=... +4 more` 1.9s → `EnsureVcpkgDependencies --rid win-x64` 6.5s → `Harvest --rid win-x64` 6.2s). `NativeSmoke --rid win-x64` halts with `CMake Error: No CMAKE_C_COMPILER could be found` because the shell used for the witness did not source `vcvarsall.bat`. This is **expected and correct** mini-CI-sim behavior — in CI the `vcpkg-setup` composite action sources the MSVC environment; on a plain PowerShell / bash terminal locally it is not. Run `ci-sim` from **Developer PowerShell for VS 2022** to exercise the full chain on Windows; Unix hosts (gcc/clang on `$PATH`) handle it without ceremony. Documented in `tests/scripts/README.md` Requirements + Troubleshooting.

**Why a post-B2 micro-slice, not Slice C territory:** the Cake stage surface stabilises at B2; every downstream witness improvement (script orchestration, eventual `ci-sim` PackageConsumerSmoke success once Slice C lands) rides that flat graph. BA lands the witness tool now so Slice C and Slice E can use it for their own worktree health checks without rewiring later.

**Close slice BA with one commit on `feat/adr003-impl`:** `slice BA: smoke-witness file-based app (Spectre + local / ci-sim modes)` (`735ccb3`).

[msdocs-file-based-apps]: https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps

### 6.4.2 Slice CA — Witness hardening (TUnit output + MSVC self-sourcing)

**Goal:** close two operational gaps Slice BA surfaced before Slice C's stage-record + CI re-entry work begins: (a) `PackageConsumerSmoke` hides TUnit's per-TFM pass/fail summary behind `_log.Verbose`; (b) `NativeSmoke` requires a Developer PowerShell on Windows because the Cake.CMake invocation has no access to `vcvarsall.bat`-set env vars. Both bite the witness script today and would bite `release.yml` in Slice C/E; CA resolves them in-process.

**Scope:**

- **TUnit output visibility:** add `bool echoStdout = false` to `PackageConsumerSmokeRunner.RunDotNetCommand`; `dotnet test package-smoke (<tfm>)` + `compile-sanity netstandard2.0 consumer` invocations set it `true`. Stdout flows to `_log.Information` (normal Cake log level); stderr to `_log.Warning`. Build-server-shutdown + other chatty internals stay at `Verbose`. Single-helper change, no behavioural shift on failures (combined output still surfaces in the `CakeException` message).
- **MSVC self-sourcing (dumpbin pattern):** new `Domain/Runtime/IMsvcDevEnvironment.cs` seam + `Infrastructure/Tools/Msvc/MsvcDevEnvironment.cs` impl. Windows-only by contract (caller gates on `OperatingSystem.IsWindows`; resolver throws `PlatformNotSupportedException` defensively). Flow:
  1. Fast path — `VCToolsInstallDir` env var set → parent shell already has MSVC live → return empty delta (no work).
  2. `cakeContext.VSWhereLatest(...)` with `Requires = "Microsoft.VisualStudio.Component.VC.Tools.x86.x64"` locates the VS installation.
  3. `<VS>/VC/Auxiliary/Build/vcvarsall.bat <host-arch>` spawned via `cmd /c "... >nul && set"`; stdout parsed into `KEY=VALUE` entries.
  4. Delta vs. current process env returned — only the missing/changed entries. Session-cached (reference-field write is atomic in .NET; one-time resolution per process).
- **Wiring:** `NativeSmokeTaskRunner` ctor takes `IMsvcDevEnvironment`; new `ApplyMsvcEnvironmentAsync(ToolSettings)` helper merges delta into both `CMakeSettings.EnvironmentVariables` (configure) and `CMakeBuildSettings.EnvironmentVariables` (build). `RunCmakeConfigure` / `RunCmakeBuild` become async.
- **DI:** `services.AddSingleton<IMsvcDevEnvironment, MsvcDevEnvironment>()` in `Program.cs`.
- **Tests:** `MsvcDevEnvironmentTests` (platform-guard contract — non-Windows hosts assert the throw; Windows hosts skip the throw assertion and pin the interface shape); `ProgramCompositionRootTests` adds `IMsvcDevEnvironment → MsvcDevEnvironment` assertion; `NativeSmokeTaskRunnerTests` 4 construction sites take `Substitute.For<IMsvcDevEnvironment>()` (precondition-failure tests never reach the CMake path). Test count 398 → 400.
- **Docs:** `tests/scripts/README.md` Requirements note updated — plain PowerShell is now sufficient for `ci-sim` on Windows; Developer PowerShell is no longer a prereq.

**Worktree health check (Windows):**

- `./smoke-witness.cs local` — 3/3 PASS in 114.7s; TUnit per-TFM pass counts visible in the tee'd `03-PackageConsumerSmoke.log` (`net9.0`: 12 passed, `net8.0`: 12 passed, `net462`: 11 passed).
- `./smoke-witness.cs ci-sim` — 8/8 PASS in 81.3s from **plain PowerShell** (pre-CA: halted on NativeSmoke with `No CMAKE_C_COMPILER could be found`). NativeSmoke step: 13s.

**CI impact:** once `release.yml` Windows matrix jobs start calling `NativeSmoke`, no `ilammy/msvc-dev-cmd@v1` step is needed — the Cake host self-sources via the same `IMsvcDevEnvironment` path. Composite `vcpkg-setup` continues to handle submodule bootstrap + binary cache; MSVC env is decoupled from it now.

**Close slice CA with one commit on `feat/adr003-impl`:** `slice CA: witness hardening — TUnit output + MSVC self-sourcing`.

### 6.5 Slice C — Per-stage request records + GitTagVersionProvider + G58 + ConsumerSmoke stateless

**Goal:** formalize stage inputs as Domain-layer request records; add the git-tag version source; stateless-parameterize consumer smoke for matrix re-entry; add the G58 cross-family dependency resolvability validator.

**Scope:**

- Introduce per-stage request records under `Domain/<Module>/Models/`:
  - `PreflightRequest(Manifest, Versions)`
  - `HarvestRequest(Rid, Libraries, VcpkgConfig)`
  - `NativeSmokeRequest(Rid, HarvestOutputRoot)`
  - `ConsolidateHarvestRequest(RootOutput)` — may be empty-body if runner reads environment solely from `BuildContext.Paths`.
  - `PackRequest(Versions, ConsolidatedHarvestRoot, PackagesOutput)`
  - `PackageConsumerSmokeRequest(Rid, Versions, FeedPath)`
  - `PublishRequest(Packages, FeedTarget, AuthToken)` (record exists even though `PublishTask` is a stub — shape locked for Phase 2b consumption).
  - These sit under `.Models.` namespaces, which is the canonical ADR-002 §2.8 permanent allowance for Task → Domain value-object refs.
- Runner signatures: each Application runner gains a `RunAsync(TRequest, CancellationToken)` overload. Existing overloads either adapt to the request (construct the request from DI config internally for back-compat) or retire. Internal choice per runner.
- Introduce `GitTagVersionProvider` (single class; `GitTagScope.Single(FamilyId) | GitTagScope.Multi(IReadOnlySet<FamilyId>)` parameter).
- Introduce `Infrastructure/Git/IGitCommandRunner.cs` + `GitCommandRunner.cs` — Cake-native wrapper for `git describe` / `git rev-parse` / `git tag -l`. Legitimate interface seam under ADR-002 §2.3 criterion 2 (axis of change: process invocation vs pure parse logic).
- Wire `GitTagVersionProvider` into `ResolveVersions` task (source=`git-tag` or `meta-tag`).
- Introduce `G58CrossFamilyDepResolvabilityValidator` in `Domain/Packaging/`. Scope-contains check mandatory; feed-probe check flag-gated (feed URL supplied → probe runs).
- Wire G58 into `PackageOutputValidator` (Pack stage owns it per ADR-003 §4).
- `PackageConsumerSmokeRunner`: parameterize on `PackageConsumerSmokeRequest` (RID, Versions, FeedPath all inputs); `IRuntimeProfile` injection narrowed (host-RID as default fallback, request overrides). Decide here: symlink-verification / payload-integrity post-assertions inside the runner vs separate diagnostic target. Default recommendation: inside the runner.
- Test fixtures: new per-provider tests (git-tag happy path + G54 reject + meta-tag discovery + scope ordering); new G58 tests (in-scope / missing-lower-bound / missing-family); update consumer-smoke fixtures for request shape.

**CI deliverable:** `release.yml` gets real `pack` job (`--target Package --explicit-version ...`) and real `consumer-smoke` matrix re-entry job (fan-out on `manifest.runtimes[]`; each runner downloads nupkg artifact + invokes `--target PackageConsumerSmoke --rid <rid> --explicit-version ...`). Publish jobs still stubbed.

**Worktree health check:**

- `dotnet build build/_build` green.
- `dotnet test build/_build.Tests` green.
- `LayerDependencyTests` green.
- End-to-end `SetupLocalDev --source=local --rid win-x64` green on Windows.
- Standalone `--target PackageConsumerSmoke --rid win-x64 --explicit-version sdl2-core=2.32.0-local.<ts>,...` green against feed from prior `SetupLocalDev` run.
- `--target ResolveVersions --source=git-tag --scope sdl2-core` emits valid JSON mapping at a commit that carries a `sdl2-core-*` tag (create a throwaway tag locally to validate; delete after).

**Close slice C with one commit on `feat/adr003-impl`:** `slice C: per-stage request records + GitTagVersionProvider + G58 + stateless ConsumerSmoke`.

### 6.6 Slice E — Cross-platform validation + playbook cake-ification closure + release.yml completion

**Goal:** close the remaining WSL-playbook bash-in-place gaps; run the full `docs/playbook/cross-platform-smoke-validation.md` A–K checkpoint sequence on WSL and macOS; finalize `release.yml` (with `Publish` stubs) as the CI mirror of the playbook.

**Scope:**

- Identify any remaining WSL-playbook steps that should be Cake-native but still shell out. Candidates: solution-build (now `CompileSolution` task from slice D), per-TFM smoke invocations (subsumed into `PackageConsumerSmoke` post-slice-C), symlink-verification (slice-C default: inside runner).
- Rewrite the WSL temp runbook (`docs/playbook/TEMP-wsl-smoke-commands.md`) to invoke Cake targets exclusively. Bash wrapping limited to environment setup (PATH, DOTNET_ROOT exports) and log-tee + summary extraction.
- Add `PublishTask` stub + `Application/Publishing/PublishTaskRunner.cs` stub — task present, runner present, body throws `NotImplementedException` with a clear Phase-2b message. Slice E does not execute `Publish`; it only wires the empty shape so `release.yml` can declare `publish-staging` / `publish-public` jobs without them disappearing from the job graph.
- Complete `release.yml` — **E1 / E2 / E3 absorb the `prepare-native-assets-*.yml` family into matrix-driven jobs** (Deniz direction 2026-04-21 after Slice D pre-witness checkpoint review):
  - **E1. CI absorption via reusable-workflow dispatch.** Rewrite the Slice-D first-draft `harvest` / `native-smoke` / `consolidate-harvest` jobs to dispatch per-platform reusable workflows (`prepare-native-assets-windows/linux/macos.yml`, possibly renamed under a single unified shape) via `uses:` + matrix-derived inputs from `generate-matrix`. The Slice-D job bodies are intentionally minimal — they exercise the Cake target shape + matrix wiring + artifact-upload contract, but they do NOT yet carry the battle-tested prereq + cache + submodule discipline the `prepare-*` family already solves. Slice E absorbs all seven of the following points from the `prepare-*` family:
    1. Linux container apt prereqs (`build-essential` + SDL2 video/audio backends + `freepats` for Timidity MIDI + `autoconf 2.72` source build on ubuntu:20.04). `freepats` is a NativeSmoke hard requirement — `Mix decoder: MIDI` fails without it (see the inline comment in `prepare-native-assets-linux.yml` + `docs/playbook/cross-platform-smoke-validation.md`).
    2. `vcpkg-setup` composite action (`.github/actions/vcpkg-setup`) — submodule-commit + overlay triplet/port hash keying + bootstrap + binary cache.
    3. `actions/checkout` with `submodules: recursive` + `fetch-depth: 0` for the vcpkg submodule + git-tag / MinVer provenance.
    4. Workspace-local NuGet cache (`NUGET_PACKAGES` env + `actions/cache`).
    5. Cake host compiled once (`dotnet build`) and invoked many times via the built `Build` executable — NOT `dotnet run` per target.
    6. `--repo-root "$(pwd)"` on Unix; shell defaults per platform (bash vs pwsh).
    7. Artifact name convention `harvested-assets-{os}-{rid}` (the `prepare-*` family's shape). `ConsolidateHarvest`'s `download-artifact` step aligns with this pattern.
  - **E2. Custom Ubuntu container image (GHCR-hosted, dedicated build workflow).** Move the Linux apt-prereq preamble (and the autoconf-2.72 source build) **out of the runtime workflow entirely**. Build a pinned custom Ubuntu image hosted on `ghcr.io/janset2d/sdl2-bindings-linux-builder:<tag>` that ships every non-vcpkg prerequisite baked in. New companion workflow `.github/workflows/build-linux-container.yml` builds + pushes the image (cadence: manual dispatch + scheduled rebuild; multi-arch for `linux-x64` + `linux-arm64`; provenance: sha + vcpkg-overlay-triplets hash in the tag). `manifest.runtimes[].container_image` points at the pinned tag so a single manifest edit drives both the image reference and the matrix shape. Rationale: (a) removes the 30-90s apt-install preamble from every matrix run; (b) pins transitive package versions (apt package drift between ubuntu:20.04 index updates is a quiet source of CI flakiness); (c) matches our vcpkg-ABI-hash + overlay-hash cache-key discipline; (d) when we eventually do Debian-based PA-2 variants or a musl hybrid, the image is the versioning axis, not ad-hoc workflow edits. Open: registry-auth shape, multi-arch buildx configuration, rebuild-cadence policy — tracked in §11 Q16.
  - **E3. Retire (or narrow) `prepare-native-assets-*.yml`.** After E1 + E2 land green across all three platforms, the per-platform workflows either retire entirely or narrow to a callable-from-release.yml shape (if any other caller needs them, which currently nobody does). Retirement is preferred — one orchestration surface for CI.
  - **E4.** `publish-staging` + `publish-public` jobs — guarded, never-execute-in-this-pass.
  - **E5.** Final review that every `run:` step invokes a Cake target; no bash orchestration logic embedded.
- Execute end-to-end cross-platform validation:
  - **Windows host** (primary dev machine): clean artifacts → `SetupLocalDev --source=local --rid win-x64` → standalone `PackageConsumerSmoke --rid win-x64` → `NativeSmoke --rid win-x64` → `Inspect-HarvestedDependencies --rid win-x64`. All green.
  - **WSL (Linux)**: same sequence with `--rid linux-x64` and `--repo-root` override.
  - **macOS Intel (SSH)**: same sequence with `--rid osx-x64` and `--repo-root` override.
  - Record results in `cross-platform-smoke-validation.md` "Last validated" header.

**Until Slice E closure: no release tag push.** The Slice-D first-draft job bodies in `release.yml` (harvest / native-smoke / consolidate-harvest) will fail on the first Linux matrix entry — apt prereqs + vcpkg cache + submodule recursion + NuGet cache are absent. Operator-facing CI harvest remains `prepare-native-assets-main.yml` (workflow_dispatch) until E1–E3 land.

**CI deliverable:** `release.yml` reads as a thin CI mirror of the playbook; every job invokes a Cake target (either directly or via a reusable per-platform harvest workflow dispatched with matrix-derived inputs); no bash orchestration logic in YAML; `prepare-native-assets-*.yml` retired (or narrowed); custom GHCR image drives the Linux matrix; `Publish` jobs present but gated.

**Worktree health check (= end-of-pass):**

- `dotnet build build/_build` green.
- `dotnet test build/_build.Tests` green; test count ≥ baseline + slice additions.
- `LayerDependencyTests` green.
- Full three-platform matrix green per playbook A–K checkpoints.
- `release.yml` passes an `actionlint` (or equivalent) pass and invokes only Cake targets for payload steps.

**Close slice E with one commit on `feat/adr003-impl`:** `slice E: cross-platform closure + playbook cake-ification + release.yml finalization + Publish stubs`.

**End-of-pass merge:** on master, `git merge --no-ff feat/adr003-impl` → single merge commit plus the six slice commits reachable via the merge parent.

---

## 7. Retire / Add / Modify Inventory

This is the plan-level summary of file-level surface. Exact file paths confirm during implementation; the table is **shape-level**, not pin-accurate.

### 7.1 Retire (inside the pass; coordinated with the slice that lands each)

- `build/_build/Domain/Packaging/PackageVersionResolver.cs` + `IPackageVersionResolver.cs` + related result types. (Slice B1.)
- `build/_build/Tasks/PostFlight/PostFlightTask.cs` entire folder. (Slice B2.)
- **PostFlight reference sweep** (Slice B2):
  - `PackageTask.cs` lines 23, 34 — XML doc comments mentioning PostFlight.
  - `PackageTask.cs` line 46 — user-facing log message mentioning PostFlight.
  - `PackageConsumerSmokeRunner.cs` lines 433, 458 — XML doc comments mentioning PostFlight.
- `PackageOptions.FamilyOption` + `PackageOptions.FamilyVersionOption`. (Slice B1.)
- `PackageBuildConfiguration.Families` + `PackageBuildConfiguration.FamilyVersion` (remaining `ExplicitVersions` mapping is the replacement). (Slice B1.)
- `UpstreamVersionAlignmentValidator` old-shape fields — the validator is rewritten around the mapping; `Families`-based and `FamilyVersion`-based branches retire. (Slice B1.)
- All stage-task `[IsDependentOn(...)]` attributes (listed in §6.4 scope). (Slice B2.)
- `DependentsTask.cs` vestigial ToolPath-resolve line. (Slice D.)
- `ConsolidateHarvestTask` default-ctor fallback (vestigial; DI ctor is the real path). (Slice D.)

### 7.2 Add

- `build/_build/Application/Versioning/IPackageVersionProvider.cs` + three impls. (A, B1, C.)
- `build/_build/Application/Versioning/ExplicitVersionProvider.cs`. (A.)
- `build/_build/Application/Versioning/ManifestVersionProvider.cs`. (B1.)
- `build/_build/Application/Versioning/GitTagVersionProvider.cs`. (C.)
- `build/_build/Domain/Versioning/GitTagScope.cs` (sum-type record). (C.)
- `build/_build/Infrastructure/Git/IGitCommandRunner.cs` + `GitCommandRunner.cs`. (C.)
- `build/_build/Domain/<Module>/Models/<Stage>Request.cs` — one per stage (seven records). (C.)
- `build/_build/Domain/Packaging/G58CrossFamilyDepResolvabilityValidator.cs`. (C.)
- `build/_build/Application/Harvesting/NativeSmokeTaskRunner.cs`. (D.)
- `build/_build/Tasks/Harvest/NativeSmokeTask.cs`. (D.)
- `build/_build/Tasks/Versioning/ResolveVersionsTask.cs` + `Application/Versioning/ResolveVersionsTaskRunner.cs`. (B1 manifest source; C git-tag source added.)
- `build/_build/Tasks/Common/CleanArtifactsTask.cs` + `Application/Common/CleanArtifactsTaskRunner.cs`. (D.)
- `build/_build/Tasks/Common/CompileSolutionTask.cs` + runner. (D.)
- `build/_build/Tasks/Dependency/InspectHarvestedDependenciesTask.cs` + runner. (D.)
- `build/_build/Tasks/Common/GenerateMatrixTask.cs` + runner. (D or B2 — decided at slice-D open based on where the task cleanly lives; likely D since it emits CI-consumable JSON with no graph-order implications.)
- `build/_build/Tasks/Publish/PublishTask.cs` + `Application/Publishing/PublishTaskRunner.cs` — stub only; body throws with Phase-2b message. (E.)
- `build/_build/Infrastructure/Tools/CMake/*` — OR Cake.CMake addin PackageReference; decided at slice A spike close. (A spike → D impl.)
- `build/_build/Infrastructure/Tools/NativeSmoke/NativeSmokeRunnerTool.cs`. (D.)
- `build/_build/Infrastructure/Tools/Tar/` triad (`TarExtractSettings.cs` / `TarExtractTool.cs` / `TarAliases.cs`). (D.)
- NuGet PackageReferences: `Cake.CMake` (tentative, decided in A). (D.)
- Test mirrors under `build/_build.Tests/Unit/...` for every production addition.
- `.github/workflows/release.yml` — skeleton (A), grows slice-by-slice, finalized (E).

### 7.3 Modify

- `build/_build/Program.cs` — DI registrations (providers, request-aware runners, new tools); CLI option surface (add `--explicit-version`, remove `--family` + `--family-version`). (B1 + D + C additions; B2 resolver runner dependencies.)
- `build/_build/Context/Options/PackageOptions.cs` — option swap. (B1.)
- `build/_build/Context/Configs/PackageBuildConfiguration.cs` — mapping-only shape. (B1.)
- `build/_build/Domain/Preflight/UpstreamVersionAlignmentValidator.cs` — mapping-shaped rewrite; old `Families`/`FamilyVersion` branches retire. (B1.)
- `build/_build/Application/Packaging/PackageTaskRunner.cs` — mapping consumption; request-overload (C); precondition-check rollout (B2 — already the template).
- `build/_build/Application/Packaging/LocalArtifactSourceResolver.cs` — mapping-based in B1 (minimal change: replace inline version-derivation with `ManifestVersionProvider.ResolveAsync`); full internal-composition rewrite in B2.
- `build/_build/Application/Packaging/PackageConsumerSmokeRunner.cs` — mapping consumption (B1); stateless-callable via request (C); PostFlight reference sweep (B2).
- `build/_build/Application/Preflight/PreflightTaskRunner.cs` — mapping consumption (B1); request-driven (C); precondition-check (B2).
- `build/_build/Application/Harvesting/HarvestTaskRunner.cs` — request-driven (C); precondition-check (B2).
- `build/_build/Application/Harvesting/ConsolidateHarvestTaskRunner.cs` — request-driven (C); precondition-check (B2).
- `build/_build/Domain/Packaging/PackageOutputValidator.cs` — G58 wiring (C).
- `build/_build/Tasks/Packaging/PackageTask.cs` — `ShouldRun` gate swap (B1); remove `[IsDependentOn]` (B2); PostFlight reference sweep (B2).
- `build/_build/Tasks/Packaging/SetupLocalDevTask.cs` — no `[IsDependentOn]` (B2); body unchanged (still delegates to resolver).
- `build/_build/Tasks/Packaging/PackageConsumerSmokeTask.cs` — no `[IsDependentOn]` (B2); request-passing (C).
- `build/_build/Tasks/Harvest/HarvestTask.cs` — no `[IsDependentOn]` (B2).
- `build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs` — default-ctor fallback removed (D); no `[IsDependentOn]` (B2).
- `build/_build/Tasks/Vcpkg/EnsureVcpkgDependenciesTask.cs` — keep `[IsDependentOn(InfoTask)]` only if Info is still the pre-run banner; re-evaluate at B2.
- Most `build/_build.Tests/Unit/Application/Packaging/*Tests.cs` + `Unit/Domain/Packaging/*Tests.cs` — fixture rewiring for mapping and request shapes (B1 for mapping; C for request).
- `docs/playbook/cross-platform-smoke-validation.md` — "Last validated" header after slice E; update A–K command references for new CLI shape. (E.)
- `docs/playbook/TEMP-wsl-smoke-commands.md` — cake-native rewrite. (E.)

---

## 8. Test and Validation Methodology

### 8.1 Baseline (confirmed 2026-04-21)

```text
dotnet test build/_build.Tests/Build.Tests.csproj -c Release
→ Passed: 340, Failed: 0, Skipped: 0, Total: 340, Duration: 511ms
TUnit 1.33.0.0 | .NET 9.0.12 | win-x64 | Microsoft Testing Platform v2.2.1
```

Matches `plan.md` 2026-04-20 measurement exactly. Slice A kickoff sits on this baseline.

### 8.2 Per-slice worktree health check

Every slice closes with (at minimum):

1. Clean build:

   ```text
   dotnet build build/_build/Build.csproj -c Release
   ```

2. Test suite:

   ```text
   dotnet test build/_build.Tests/Build.Tests.csproj -c Release
   ```

3. Architecture tests specifically:

   ```text
   dotnet test build/_build.Tests/Build.Tests.csproj --filter "FullyQualifiedName~LayerDependency"
   ```

4. Task tree:

   ```text
   dotnet run --project build/_build/Build.csproj -- --tree
   ```

5. One end-to-end Cake invocation specific to the slice (listed in §6 per-slice health checks).
6. Count delta: tests added / retired per slice. Expected net-positive across the pass.

If any step fails, the slice does not close; the failure is triaged (environment vs code vs plan drift) and the plan is either updated or the issue is fixed in-place. Only after the health check closes green does a local commit land on `feat/adr003-impl`.

### 8.3 Platform witness schedule

- **Slice A through C:** Windows host only.
- **Slice D:** Windows host primary + one Linux witness at close (`NativeSmoke` + `Inspect-HarvestedDependencies` on WSL). Single pass, ~15 minutes.
- **Slice E:** full three-platform matrix (Windows + WSL + macOS Intel SSH) per playbook A–K.

Any red in the slice-E matrix reopens the pass; the refactor does not merge to master until all three platforms close green on A–K.

### 8.4 Architecture-test catchnet discipline

`LayerDependencyTests` runs in every step of §8.2. Post-refactor, the three invariants hold:

- Domain no outward dependencies.
- Infrastructure no Application / Tasks.
- Tasks only through Application for services; `.Models.` / `.Results.` / `Tools.*` references permitted (canonical permanent allowance per ADR-002 §2.8 invariant #3).

The Wave-6-era "Task → Domain/Infrastructure interface" transitional allowance was closed pre-ADR-003 and stays closed. This pass does not tighten the DTO exception — that exception IS the canonical shape.

Violations are never suppressed silently; they either:

- Represent real drift → fix in the same slice.
- Represent intentional exception → documented in the test with a rationale comment.

### 8.5 Coverage ratchet

`Coverage-Check` task's static floor in `build/coverage-baseline.json` remains authoritative. If the refactor reduces measured coverage below the floor, the floor is not lowered silently — either tests are added to recover coverage, or the floor change is an explicit, documented decision. Coverage ratchet status is checked at slice E close.

### 8.6 Observability during implementation

Per-slice, implementation progress produces:

- Test-count delta (before / after).
- Line-count delta per layer (Domain / Application / Infrastructure / Tasks).
- New interfaces added + justification per ADR-002 §2.3 three criteria.
- Task graph `--tree` snapshot.
- `release.yml` diff since prior slice.
- Slice commit message draft.

These form the slice handoff summary and are the raw material for the eventual merge-commit message.

---

## 9. Commit Structure

Single `feat/adr003-impl` branch; six commits; end-of-pass merge to master.

### 9.1 Branch lifecycle

- Branch from `master` HEAD at slice A kickoff: `git checkout -b feat/adr003-impl`.
- Per-slice local commit on the feat branch only when the §8.2 health check closes green. No push until end-of-pass or until approved explicitly.
- If a slice needs to roll back partially, the rollback is a **new commit** on the feat branch (not amend / not rebase — per AGENTS.md guidance on commit-safety).
- End-of-pass: `git checkout master && git pull && git merge --no-ff feat/adr003-impl`. Merge commit message captures the pass as a whole (summary + ADR-003 closure statement + PD-13 closure); six slice commits remain visible in history via the merge-parent chain.

### 9.2 Slice commit messages (draft shape)

Each slice commit is one line summary + optional body. Body captures: what changed, test-count delta, end-to-end validation evidence.

- `slice A: IPackageVersionProvider seam + ExplicitVersionProvider + Cake.CMake spike`
- `slice B1: mapping contract migration + ManifestVersionProvider + ResolveVersions (manifest source)`
- `slice D: NativeSmoke task + Cake-native polish + Linux witness`
- `slice B2: graph flatten + resolver internal-composition rewrite + PostFlight retire`
- `slice C: per-stage request records + GitTagVersionProvider + G58 + stateless ConsumerSmoke`
- `slice E: cross-platform closure + playbook cake-ification + release.yml finalization + Publish stubs`

The merge commit on master captures the pass-level summary.

### 9.3 No mid-pass publication

Slices are not promoted individually. Master sees the work once, at the end, as the merge commit. CI runs on feat branch for developer feedback (optional; this pass does not require it) but the feat branch is not a release surface.

---

## 10. Risks and Mitigations

| # | Risk | Probability | Severity | Mitigation |
| --- | --- | --- | --- | --- |
| 1 | Merge-commit blast radius on master is large — a single ADR-003 merge touches ~800–1200 LOC. | High | Medium | Per-slice commits on feat branch preserve bisectability; if post-merge regression surfaces, revert target is the merge commit on master while the per-slice history stays queryable behind the merge parent. |
| 2 | `Cake.CMake` 1.4.0 fails to bind under Cake.Frosting 6.1.0 or lacks needed features. | Low-Medium | Low | Pre-planned spike in slice A (not slice D); spike outcome decides direction before slice D opens. Fallback is repo-local `CMakeTool` triad following the Vcpkg pattern; estimated 3–4h implementation added to slice D if triggered. |
| 3 | Graph flattening breaks local-dev muscle memory (operator expects `--target Package` to cascade through Harvest). | Medium | Low | Precondition-check messages are the UX channel: "Run Harvest + ConsolidateHarvest first" with full command hint; `SetupLocalDev` remains the one-command umbrella for "do everything locally." |
| 4 | `PackageBuildConfiguration` field removal cascades test-fixture churn unpredictably. | High | Medium | Roslyn's CS0117 "does not contain a definition" gives the full call-site list mechanically; fixture update is bulk-find-and-replace with targeted patching. Slice B1 is the slice absorbing this churn. |
| 5 | `LayerDependencyTests` catches unexpected layer violation after graph flattening. | Low | Low | Invariants only forbid Domain → outward and Infrastructure → Application/Tasks; Application → Application is allowed by ADR-002 §2.2. No expected violation. |
| 6 | PreFlight becoming version-aware-by-contract breaks invocations that didn't supply mapping before. | Medium | Low | All in-repo invocation paths are controlled; CI calls PreFlight with mapping after B1; local dev invokes PreFlight only through `SetupLocalDev` resolver (always supplies mapping); operator PD-8 escape supplies `--explicit-version`. Precondition check fails fast with remediation hint. |
| 7 | `release.yml` inadvertently ends up doing build-host logic in YAML. | Medium | High | CI step language is strict from slice B1 onward: YAML steps are `actions/checkout`, `dotnet run --project build/_build -- --target <X>` invocations, and `needs:` output capture. Any logic in a `run:` block is flagged in review. |
| 8 | `SetupLocalDev` internal composition grows too large. | Medium | Low | Composition is linear and readable; no branching on scenario. If it grows past ~100 lines of orchestration, extract named phase methods. |
| 9 | Slice E surfaces platform-specific regression introduced earlier. | Medium | High | Slice D's Linux witness catches the biggest class (CMake / native-tar wrapper / symlink preservation in `Inspect-HarvestedDependencies` extraction) early. Slice E's macOS pass is the final gate; pass does not merge until macOS closes green. |
| 10 | NativeSmoke CMake presets / triplet assumptions surface Unix issues in slice D Linux witness. | Medium | Medium | Already flagged; Linux witness exists at D close specifically to catch this; CMakePresets.json + C++ harness already validated for linux-x64 historically per `cross-platform-smoke-validation.md` last-validated evidence. |
| 11 | `ResolveVersions` JSON shape change under pressure during implementation breaks downstream consumers. | Low | Medium | Shape pinned at plan-lock (flat `{family-id: semver-string}` object); any change is a deliberate plan amendment with downstream fixup in the same slice. |
| 12 | `UpstreamVersionAlignmentValidator` rewrite loses a pre-existing corner case (e.g., skip-no-family-version or skip-minor-for-multi-family). | Medium | Medium | Pre-rewrite test coverage inventoried in slice B1 open; every branch is replicated in the new shape or explicitly dropped with rationale. G54 semantic is preserved; shape changes. |

---

## 11. Open Questions (plan-lock)

Tracked as-of-plan-lock state; updated as slices land.

- **`NativeSmoke` RID-status emission shape:** decided during slice D based on downstream consumer needs. Not a blocker for B1.
- **`PackageConsumerSmoke` post-smoke assertions placement:** decided during slice C; default recommendation is inside the runner.
- **Full-train meta-tag format:** Phase 2b Stream D-ci. Not this pass.
- **`GenerateMatrixTask` slice placement (D vs B2):** decided at slice D open. Both are valid; D likely since the task is self-contained and emits JSON without graph-order implications.
- **`PackageFamilySelector` inlining into `PackageTaskRunner`** *(opened Slice B1 closure, 2026-04-21)*. Post-B1 the selector's role narrows to "concrete-filter + topological-sort" with scope already arriving via the mapping's keys. Repository survey shows `IPackageFamilySelector` has exactly one production consumer (`PackageTaskRunner`) + one docstring reference (`PackageConsumerSmokeRunner`, stylistic). Deniz direction: inline into `PackageTaskRunner` if the selector is not required for any later slice (D / B2 / C). Check at Slice B2 open (after graph flatten, when the resolver composition is the only remaining selector caller) or Slice D open if a new consumer surfaces. Retire target: the interface + impl + `PackageFamilySelectionResult` + `PackageFamilySelectionError` + `PackageFamilySelection` model. Criterion-2 check: concrete filter is a one-liner; topological sort is a stable helper that can move into `PackageTaskRunner` privately or into `Domain/Packaging/FamilyTopologyHelpers.cs` as a static.
- **`JsonSerializer.Deserialize` migration to Cake-native surface** *(opened Slice B1, 2026-04-21; scheduled as the final Slice B1 sub-step per Deniz direction)*. Parallel to the Serialize migration that landed opportunistically during B1.2. Seven remaining call sites use raw `JsonSerializer.Deserialize`:
  - `NativePackageMetadataValidator` line 171 — ZIP-entry string → model (validator-time; no `ICakeContext` in ctor today).
  - `ConsolidateHarvestTaskRunner` line 209 — file-content string → model (has `ICakeContext` via `BuildContext` parameter).
  - `VcpkgManifestReader` lines 16, 30 — Infrastructure reader; ctor takes `IFileSystem` only.
  - `VcpkgCliProvider` line 53 — Infrastructure adapter; stdout string → model.
  - `CoverageBaselineReader` lines 25, 39 — Infrastructure reader; ctor takes `IFileSystem` only.
  **Direction (locked at B1.3 kickoff, per Deniz):** helpers live on `CakeExtensions` (same home as `WriteJsonAsync` / `SerializeJson`); System.Text.Json source-generation compatibility must stay open for a future AOT-friendly pass (caller-supplied `JsonSerializerOptions.TypeInfoResolver = JsonContext.Default` must continue to work — no hard-coded reflection paths). Two shapes needed: `DeserializeJson<T>(string, options?)` and `DeserializeJson<T>(ReadOnlySpan<byte>, options?)`. Packaging: plain static helpers on `CakeExtensions` (not `ICakeContext` extensions) so Infrastructure readers (`VcpkgManifestReader`, `CoverageBaselineReader`) consume them without acquiring `ICakeContext` (DI bloat avoided). Land as the final Slice B1 sub-step, just before B1.11 health check.
- **Q16 — Custom Ubuntu builder container (GHCR-hosted, dedicated build workflow)** *(opened 2026-04-21 at Slice D pre-witness checkpoint, per Deniz direction)*. **Decision:** yes, bake every non-vcpkg Linux prerequisite into a pinned custom Ubuntu image hosted on `ghcr.io/janset2d/sdl2-bindings-linux-builder:<tag>`, built by a companion workflow `.github/workflows/build-linux-container.yml`. Referenced from `manifest.runtimes[].container_image`. Lands in Slice E as part of the E1–E3 CI absorption pass.
  **Open sub-questions to resolve at E2 kickoff:**
  - **Rebuild cadence.** Manual dispatch only? Scheduled (weekly apt-index refresh)? Renovate/Dependabot on the base image pin? Recommended default: manual + scheduled weekly, with the tag incorporating date + base-image-digest so unattended rebuilds produce distinct immutable tags.
  - **Tag scheme.** `<ubuntu-base>-<short-sha>` (e.g., `focal-a1b2c3d`) vs semver-style `v<major>.<minor>.<patch>` vs date-anchored `<yyyymmdd>-<short-sha>`. Leaning toward `<ubuntu-base>-<yyyymmdd>-<short-sha>` to make both the base image and the rebuild date operator-visible in `manifest.runtimes[]`.
  - **Multi-arch.** `linux-x64` and `linux-arm64` need separate images (apt packages differ; cross-compile preambles differ). Single image with `buildx --platform linux/amd64,linux/arm64` vs two images tagged separately vs an image that carries an explicit arch suffix (`-amd64` / `-arm64`). Single buildx multi-arch image is cleanest if the apt prereqs are symmetric; if ubuntu:24.04 (arm64) + ubuntu:20.04 (amd64) stay split (current shape), two images make the version axis cleaner.
  - **Registry auth.** GHCR pull in CI: default `GITHUB_TOKEN` has `packages: read` out of the box; `docker login ghcr.io` step may or may not be needed depending on whether the container is public. Leaning toward public image (nothing sensitive baked in), which removes the auth step entirely for dependent workflows.
  - **Non-vcpkg prerequisites to bake in.** The full list enumerated in `prepare-native-assets-linux.yml` lines 37–80: `build-essential` + `pkg-config` + `tar` + `unzip` + `zip` + `ca-certificates` + `python3` + `python3-pip` + `python3-venv` + `python3-jinja2` + `bison` + `autoconf` + `automake` + `autoconf-archive` + `libltdl-dev` + `libtool` + SDL2 video/audio backends (`libx11-dev` + `libxext-dev` + `libxrandr-dev` + `libxinerama-dev` + `libxcursor-dev` + `libxi-dev` + `libxss-dev` + `libxrender-dev` + `libwayland-dev` + `libxkbcommon-dev` + `wayland-protocols` + `libegl1-mesa-dev` + `libdrm-dev` + `libvulkan-dev` + `libasound2-dev`) + `freepats` + dynamic `libicu*` + `autoconf-2.72`-from-source on ubuntu:20.04. Container recipe is a direct copy of the `prepare-native-assets-linux.yml` preamble minus the workflow boilerplate.
  - **macOS Homebrew preamble — also worth absorbing?** `prepare-native-assets-macos.yml` runs `brew install pkg-config autoconf automake libtool`. Options: leave it inline (cheap, macOS runners pre-warm brew), bake into a Packer-built AMI (expensive, little gain), or move to a composite action so it's reusable. Default: leave inline for now; revisit if `brew install` cadence becomes a CI flake source.
  - **vcpkg version pinning relationship.** The container image ships apt-level build prereqs; vcpkg's own submodule version is orthogonal. `.github/actions/vcpkg-setup` already keys the cache on vcpkg submodule commit + overlay hashes — nothing to change there. Open sub-question: should the container image *also* hash the vcpkg submodule commit in its tag so vcpkg bumps rebuild the container? Probably no — the image has no vcpkg binary baked in, only the apt-level toolchain that vcpkg drives. Leaving them on independent axes.
  - **Image size target.** Rough estimate: base `ubuntu:20.04` (~70MB) + apt layer (~600MB with video/audio backends + python + autotools) = ~700MB. Worth a `apt-get clean && rm -rf /var/lib/apt/lists/*` step at the end of the Dockerfile. Target: <1GB.

### Previously open, now closed

- **Q8 (LocalPipeline umbrella):** closed — not added. `SetupLocalDev` + explicit `PackageConsumerSmoke` invocation cover the use case.
- **Q9 (tar extraction mechanism):** reopened 2026-04-21 and re-closed — native `tar` via repo-local `Infrastructure/Tools/Tar/` wrapper. SharpCompress was initially proposed (and briefly added to CPM) but dropped before any code consumed it: the library requires an explicit `SymbolicLinkHandler` opt-in and silently drops SONAME symlinks with the default handler — an unacceptable failure mode for SDL2 binary-closure inspection, which depends on SONAME resolution matching production MSBuild extraction. Native `tar` is universally available on every Inspect-target platform (WSL/Linux runners, macOS runners), preserves symlinks/permissions/xattrs natively, and matches the bash invocation in `docs/playbook/TEMP-wsl-smoke-commands.md` §5 one-to-one. See §14 change log 2026-04-21.
- **Q10 (Coverage-Check in PreFlight chain):** closed — not added; stays standalone.
- **Q11 (stage independence principle):** closed — locked.
- **Q12 (CI skeleton slice-by-slice):** closed — locked.
- **Q13 (`--family` retire):** closed — yes, B1.
- **Q14 (`ResolveVersions` task name):** closed — kept as `ResolveVersions`.
- **Q15 (baseline test run):** closed — 340/340 green confirmed.
- **F6 (ResolveVersions JSON shape):** closed — flat `{family-id: semver-string}`.
- **F7 (Cake.CMake spike):** closed — slice A scope.
- **F2 (LayerDependencyTests tightening):** closed — no tightening; `.Models.`/`.Results.`/`Tools.*` is canonical per ADR-002 §2.8 invariant #3.
- **F3 (ConsolidateHarvestTaskRunner DI claim):** closed — factually wrong in first draft; corrected in §4.2 to "vestigial default-ctor fallback."
- **G54 double-call (ExplicitVersionProvider + PreflightTaskRunner both invoke the same validator):** closed 2026-04-21 — locked as **defense-in-depth**. The same `IUpstreamVersionAlignmentValidator` implementation is called at provider-entry (reject bad operator input before the mapping ever reaches a stage) and at PreFlight stage-entry (gate the whole pipeline). ADR-003 §4 "every guardrail belongs to exactly one stage" is about fail semantics — PreFlight is the stage that fails the pipeline on G54 violation; provider-entry is early-fail-with-helpful-error, not a separate guardrail. One implementation, two call sites.

---

## 12. Out of Scope for This Pass

To keep the pass tractable, these are explicitly deferred:

- **NativeSmoke for the four PA-2 RIDs** (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`). Local machines do not cover these RIDs; C++ CMakePresets and native-smoke harness adaptation + GitHub-Actions-only execution is Phase 2b PA-2 witness work.
- **`RemoteArtifactSourceResolver` + `ReleaseArtifactSourceResolver` concrete implementations.** The interface contract is preserved (Phase 2b Stream D-ci).
- **`publish-staging` / `publish-public` job bodies.** Stubs land in slice E; real implementation is Phase 2b Stream D-ci.
- **PD-7 full-train meta-tag format finalization.** ADR-003 picked the mechanism direction (manifest-driven topological ordering via `manifest.package_families[].depends_on`); exact tag format is deferred.
- **PD-14 Linux end-user MIDI packaging.** Separate decision thread.
- **PD-15 sdl2-gfx Unix visibility regression guard.** Separate decision thread.
- **`dotnet-affected` change-detection (Stream E full impl).** Scope-reduced to 2a spike only; full impl is Phase 2b.
- **`IPayloadLayoutPolicy` extraction.** Deferral conditional on PackageTask landing; PackageTask has landed; the extraction-or-retirement decision is deferred past this pass unless pure-dynamic strategy re-enters scope.
- **`phase-x-modernization-2026-04-20.md` items (M0–M8).** Orthogonal modernization track.

---

## 13. Relationship to Canonical Docs

This plan links forward from ADRs and back to playbooks:

- **ADRs** (upstream contract): ADR-001 locks external version/consumer contracts; ADR-002 locks internal layering; ADR-003 locks orchestration graph. This plan implements them.
- **Phase 2 adaptation plan** (ledger): `phase-2-adaptation-plan.md` tracks Phase 2 stream state; this plan is the implementation mechanism for stream "Cake refactor" + stream "CI/CD workflow rewrite." On close, the adaptation plan gets the status bump.
- **Plan.md** (status roll-up): post-slice-E, plan.md's Phase 2 block updates: ADR-003 implementation done; `release.yml` live; PA-2 behavioral witnesses queued for Phase 2b.
- **Playbook** (validation contract): `cross-platform-smoke-validation.md` is the spec slice E validates against; its A–K checkpoint table is the acceptance criterion.
- **Knowledge base** (reference): `cake-build-architecture.md` gets shape updates to reflect the flat task graph + new providers + new stage task. `release-guardrails.md` gets G58 addition. `release-lifecycle-direction.md` stays policy-only (already narrowed during sweep).

On end-of-pass merge to master, any drift between code and canonical docs is closed in the same merge — not deferred.

---

## 14. Change Log

| Date | Change | Editor |
| --- | --- | --- |
| 2026-04-21 | Initial authoring (v1) during plan-first session. Captured research trail, principle set, slice plan, test methodology, and open question residue. | Deniz İrgin + session collaboration |
| 2026-04-21 | v2 — post three-reviewer critical pass. Split slice B into B1/B2 and reordered slices to A → B1 → D → B2 → C → E. Corrected `ConsolidateHarvestTaskRunner` DI factual error (is registered; fallback is vestigial). Pinned `ResolveVersions` JSON shape + `--explicit-version` repeated-option syntax. Moved Cake.CMake binding spike from slice D to slice A. Added Linux witness at slice D close. Locked NativeSmoke scope at 3 RIDs this pass (4 PA-2 rows Phase 2b). Corrected LayerDependencyTests DTO-exception framing (canonical permanent per ADR-002 §2.8, not transitional). Expanded retire inventory with PostFlight reference sweep + G54 validator rewrite. Expanded add inventory with `GenerateMatrix`, `Publish` stubs, `UpstreamVersionAlignmentValidator` rewrite. Locked commit policy: private `feat/adr003-impl` + end-of-pass merge-commit to master (not squash). Confirmed 340/340 baseline. | Deniz İrgin + session collaboration |
| 2026-04-21 | v2.1 — progress-tracking update as Slice A committed (`36b1cd5`) and Slice B1 work-in-progress. Added §2.1 Progress log with per-slice status + Slice B1 sub-progress checklist. Captured the opportunistic Cake-native JSON extension migration that landed during B1.2 (`WriteJsonAsync` + `SerializeJson` + `DefaultJsonOptions` on `CakeExtensions`; 5 pre-existing `JsonSerializer.Serialize` call sites migrated; XML docs added to every public `CakeExtensions` member). Added `JsonSerializer.Deserialize` migration as a new open question in §11 with 7 call sites enumerated and decision framing (recommended path: static helpers on `CakeExtensions` alongside extension methods). No slice-order or scope changes; test count 340 → 356 in-flight. | Deniz İrgin + session collaboration |
| 2026-04-21 | v2.2 — Slice B1 closure. All 11 B1 sub-steps landed in worktree (ManifestVersionProvider, ResolveVersionsTask + runner, UpstreamVersionAlignmentValidator rewrite, PackageBuildConfiguration reshape, legacy CLI retire, IPackageVersionResolver retire, LocalArtifactSourceResolver rewire to ManifestVersionProvider, DI update, release.yml real jobs, Cake-native JSON end-to-end including Deserialize). End-to-end `SetupLocalDev --source=local --rid win-x64` validated on Windows: 47s total, 15 nupkgs at per-family D-3seg versions (sdl2-core 2.32.0-local, sdl2-gfx 1.0.0-local, sdl2-image/mixer 2.8.0-local, sdl2-ttf 2.24.0-local), `Janset.Smoke.local.props` written correctly. Test count 340 → 355. G54 double-call closed as defense-in-depth. `PackageFamilySelector` inlining logged as open cleanup item (single production consumer; retire candidate post-graph-flatten). §6.3 Slice D updated: WSL witness runs early (after `NativeSmokeTaskRunner` + CMake tooling, before diagnostic utility tasks) per Deniz direction — maximizes platform-drift signal isolation. | Deniz İrgin + session collaboration |
| 2026-04-21 | v2.3 — Slice D pre-witness worktree checkpoint. Landed on `feat/adr003-impl` working tree (not committed yet): (a) `NativeSmokeTaskRunner` + `NativeSmokeTask` + `Infrastructure/Tools/NativeSmoke/` triad (Settings/Tool/Aliases/Result) using Cake.CMake for configure + build; runner injects `ManifestConfig` + `IRuntimeProfile` + `IPathService` + `ICakeContext` + `ICakeLog`. (b) `Infrastructure/Tools/Tar/` triad (Settings/Tool/Aliases) shelling out to the platform `tar` — SharpCompress proposal retired mid-slice before any code consumed it because its default `SymbolicLinkHandler` silently drops SONAME symlinks, a semantic drift from MSBuild production extraction we cannot accept. `/temp/` scratch folder (`sharpinspect` + `cmake-reflect` API probes) deleted; `/temp/` added to `.gitignore`. §11 Q9 re-closed. (c) Diagnostics: `CleanArtifactsTaskRunner` + task wiping the eight ephemeral artifact subtrees (including `vcpkg_installed/` preservation); `CompileSolutionTaskRunner` + task wrapping `DotNetBuild` on `Janset.SDL2.sln`; `InspectHarvestedDependenciesTaskRunner` + task doing platform-aware extract-or-direct + primary-binary pattern match + Dumpbin/Ldd/Otool dispatch. (d) `GenerateMatrixTaskRunner` + task emitting `artifacts/matrix/runtimes.json` — single symmetric 7-RID matrix consumed by both the harvest and native-smoke CI jobs (the 3-RID cap was corrected to 7 per Deniz 2026-04-21 direction; the four PA-2 RIDs fail at `cmake --preset <rid>` until the preset file grows in Phase 2b, which IS the PA-2 witness signal, not a Cake-level cap). `NativeSmokeTaskRunner.SupportedRids` hard-coded allow-list removed. (e) `DependentsTask` vestigial `ToolPath = context.Tools.Resolve("dumpbin.exe")` line deleted; `ConsolidateHarvestTask` default-ctor fallback deleted (15 test fixture sites updated). (f) `release.yml` grows `generate-matrix` + `harvest` (7-RID matrix) + `native-smoke` (7-RID matrix with inline YAML comment pinning the PA-2 expected-failure surface) + `consolidate-harvest` aggregation jobs. (g) PathService accessors expanded: `HarvestStagingRoot`, `MatrixOutputRoot`, `SolutionFile`, native-smoke path family, inspect-output path family, `PackageConsumerSmokeOutput`, `SmokeTestResultsOutput`. Test count 355 → 390. Full Windows build + test suite green. WSL Linux witness is the next (separate) session per Deniz's 2026-04-21 "önce işimizi bitirelim, WSL'de ayrı iterasyon yaparız" direction — commit + push first, then WSL-side `git pull` on the clone with populated `vcpkg_installed/x64-linux-hybrid`, then witness run. Slice D closure commit follows witness. | Deniz İrgin + session collaboration |
| 2026-04-21 | v2.4 — Slice D pre-commit CI disciplining pass. During Slice D pre-witness checkpoint review Deniz flagged that the first-draft `release.yml` matrix jobs (`harvest` / `native-smoke` / `consolidate-harvest`) miss the full prereq + cache + submodule discipline of the `prepare-native-assets-*.yml` family (apt prereqs, `vcpkg-setup` composite, submodule recursion, NuGet workspace cache, Cake-compile-once, Unix `--repo-root`, per-platform shell defaults, `harvested-assets-{os}-{rid}` naming). **Decision:** Slice D keeps the first-draft job bodies in place as shape-proof (matrix wiring + Cake target invocation contract), explicitly annotated as first-draft with inline WIP banners pointing to §6.6 E1–E3. No deletion, no `if: false` gating — visibility over hiding. §6.6 Slice E scope expanded to formalize the absorption plan: E1 reusable-workflow dispatch from `release.yml` using matrix-derived inputs from `generate-matrix`; E2 custom Ubuntu builder container hosted on GHCR with a dedicated companion workflow `.github/workflows/build-linux-container.yml` (multi-arch, manifest-referenced via `container_image`); E3 retire-or-narrow `prepare-native-assets-*.yml`. §11 Q16 opened with decision-level direction and sub-question inventory (rebuild cadence, tag scheme, multi-arch, registry auth, prerequisite baking list, macOS brew relationship, vcpkg pinning relationship, image size target). Until Slice E closure: no release tag push — operator-facing CI harvest remains `prepare-native-assets-main.yml` (workflow_dispatch). Rationale: the container pivot removes a 30-90s apt-install preamble per matrix row, pins transitive apt package versions against silent drift, matches our existing vcpkg-ABI-hash + overlay-hash cache-key discipline, and when PA-2 Debian variants or a musl hybrid eventually land they version via the image tag, not ad-hoc workflow edits. | Deniz İrgin + session collaboration |
| 2026-04-21 | v2.6 — Slice D closure (D.16). Witnesses landed end-to-end green on WSL linux-x64 and macOS Intel osx-x64 against `unix-smoke-runbook.md` (post-DA Cake-first sequence). One regression surfaced during WSL NativeSmoke step (`CMakeBuildSettings.BinaryPath` null) and fixed in `d96b727` — point the addin at the preset's configured `binaryDir`, which `PathService.GetNativeSmokeBuildPresetDir` already resolves. macOS `otool -L` diagnostic produced the hybrid-static bake-in proof Slice D was designed to unlock: SDL2_image / SDL2_ttf / SDL2_gfx / SDL2_net satellites each carry exactly three direct `LC_LOAD_DYLIB` entries (libSDL2 + self + libSystem), zero codec / font libraries leaking. WSL `ldd` reports transitive closure by design (51 entries for libSDL2_net.so) — that is standard `ldd` behaviour and Harvest's G19 hybrid-leak guardrail gates the direct-dep invariant already; a `readelf -d` NEEDED-filter supplement for Linux direct-only output is logged as a follow-up doc-sweep item for Slice E, not blocking. Aside (harness-specific only, not a code regression): `$PWD` inherits stale across `wsl -c` / non-interactive SSH invocations; documented at `cross-platform-smoke-validation.md` Known Gotchas via `ef49553`. §2.1 progress log marks Slice D + DA + the `ef49553` docs chore as DONE with SHAs. Sub-checklist D.15 + D.16 flipped to done. `cross-platform-smoke-validation.md` "Last validated" header bumped to 2026-04-21 post-Slice-D witnesses. Branch order remaining: B2 → C → E, all not started. | Deniz İrgin + session collaboration |
| 2026-04-21 | v2.5 — Slice DA opened after Slice D code commit `1ef3a82`. Doc-only slice closing the smoke-playbook Cake-first gap before the first Unix witness runs. Scope is explicitly narrow (3 files): `cross-platform-smoke-validation.md` swept (A–K + Clean-Slate Rule + Stage-Owned Validation + PA-2 Witness → Cake-first with pre-Cake A/B bootstrap exception annotation; retired `--family` / `--family-version` CLI replaced with `--explicit-version <family>=<semver>`; new checkpoints N (CleanArtifacts) / O (GenerateMatrix) / P (CompileSolution) added to Active); new `unix-smoke-runbook.md` unifies WSL + macOS witness at 95% parity via `$PLATFORM`/`$RID`/`$PRESET` env vars; `TEMP-wsl-smoke-commands.md` + `TEMP-macos-smoke-commands.md` retired. Broader doc sweep (`AGENTS.md`, `onboarding.md`, `cake-build-architecture.md`, `release-guardrails.md`, `release-lifecycle-direction.md`, `phase-2-adaptation-plan.md`, `plan.md`) deferred to Slice E per §6.6 — DA is not the broader sweep, only the witness-trio. Slice §6.3.1 added in plan. §2.1 progress row added for Slice DA between D and B2. Slice order remains A ✓ → B1 ✓ → D (WIP code `1ef3a82`) → DA → [D.15 witness] → [D.16 closure commit] → B2 → C → E. | Deniz İrgin + session collaboration |
| 2026-04-21 | v2.9 — Slice CA (post-BA witness-hardening micro-slice). Two operational gaps BA surfaced closed before Slice C's stage-record + CI re-entry work begins: (a) `PackageConsumerSmoke` now echoes TUnit stdout at the normal log level via `RunDotNetCommand(... echoStdout: true)`, so per-TFM pass/fail counts land in the tee'd witness log (`net9.0`/`net8.0`/`net462` all visible post-CA vs. Verbose-hidden pre-CA); (b) `IMsvcDevEnvironment` + `MsvcDevEnvironment` mirror the `DumpbinTool` self-resolution pattern for the full MSVC env — VSWhere + `vcvarsall.bat <host-arch>` + env-delta parse — so `NativeSmokeTaskRunner` merges the delta into Cake.CMake's `ToolSettings.EnvironmentVariables` and `cmake --preset` + Ninja + `cl.exe` inherit a live toolchain without Developer PowerShell. Windows-only by contract (caller gate + defensive `PlatformNotSupportedException` in resolver); `VCToolsInstallDir` fast path preserved for Developer PowerShell shells. Test count 398 → 400. Windows witness from plain PowerShell: `./smoke-witness.cs ci-sim` 8/8 PASS in 81.3s (NativeSmoke 13s); `./smoke-witness.cs local` 3/3 PASS in 114.7s with TUnit per-TFM counts visible. Removes the need for `ilammy/msvc-dev-cmd@v1` in the eventual `release.yml` Windows matrix jobs; composite `vcpkg-setup` stays for submodule + cache. §2.1 row added between BA and C; §6.4.2 authored (CA scope + health check). | Deniz İrgin + session collaboration |
| 2026-04-21 | v2.8 — Slice BA (post-B2 micro-slice). `tests/scripts/smoke-witness.cs` file-based .NET 10 witness app landed as `735ccb3`, committed after Deniz's .NET 10 direction on shebang + Spectre.Console (confirmed file-based apps GA in .NET 10 SDK `10.0.100+`; shebang Unix-native, Windows takes `dotnet run`). Two subcommands (default `local` = fast dev iterate `CleanArtifacts → SetupLocalDev → PackageConsumerSmoke`; `ci-sim` = mini CI replay of the flat B2 stage graph with `ResolveVersions`-emitted mapping threaded through every downstream `--explicit-version` invocation). Per-stage `Process.Start` (no in-process Cake composition; no custom local-dev helpers injected into ci-sim — the script stays faithful to CI's job-level isolation per ADR-003 §2.4 resolve-once contract). PackageConsumerSmoke deliberately skipped in ci-sim until Slice C parameterises the runner on `PackageConsumerSmokeRequest(RID, Versions, FeedPath)`, at which point the natural `--feed-path` + `--explicit-version` CLI surface replaces today's `Janset.Smoke.local.props` dependency. Witness on Windows: `local` 3/3 PASS in 132s; `ci-sim` 5/6 PASS through Harvest (22.4s) with NativeSmoke halting on missing `cl.exe` — mini-CI-sim correctly surfacing a real env gap (CI's `vcpkg-setup` composite sources `vcvarsall.bat`; local shell does not), not a script bug. Directory.Build.props inheritance retained per Deniz 2026-04-21 direction ("zararı yok"); only compat cost was mirroring `Build.csproj` `NoError` pattern for `CA1502`/`CA1505` (top-level + nested local functions all roll into `<Main>$` complexity). Spectre.Console `0.49.1` added to root `Directory.Packages.props` (CPM-native; script uses `#:package Spectre.Console` sans version). Logs land at `/.logs/witness/` (new gitignore entry) intentionally outside `artifacts/` so `CleanArtifacts` cannot wipe witness logs mid-run. §2.1 Progress log flipped B2 to DONE + added BA row between B2 and C. §6.4.1 authored (Slice BA scope + health check). | Deniz İrgin + session collaboration |
| 2026-04-21 | v2.7 — Slice B2 pre-commit amendment pass. A junior-authored pre-pass honoured the original Slice B2 prompt literally (resolver-centric composition per ADR-003 §3.3 Option A with all six pipeline runners injected into `LocalArtifactSourceResolver`). Deniz review flagged two concerns: (a) a `LocalArtifactSourceResolver` with 11 ctor deps and ownership of the full pipeline breaks the "resolver" framing — a resolver resolves from an on-disk feed, it does not produce one; (b) `NativeSmokeTaskRunner` in the local-dev flow imposes CMake + MSVC Developer shell on contributors who only touch managed bindings, which is friction orthogonal to feed materialisation (NativeSmoke is per-RID native payload validation, not a Pack prereq per ADR-003 §4). Amendment landed in the working tree with tests at 398/398 green: (1) widened `IArtifactSourceResolver.PrepareFeedAsync` + `WriteConsumerOverrideAsync` to accept the resolved mapping; (2) narrowed `LocalArtifactSourceResolver` to a 4-param ctor owning verify-feed + stamp-props responsibilities only; (3) introduced `Application/Packaging/SetupLocalDevTaskRunner` composing `Preflight → EnsureVcpkg → Harvest → ConsolidateHarvest → Pack → resolver handoff` for the Local profile, with Non-Local profiles delegating straight to the resolver; (4) dropped `NativeSmokeTaskRunner` from the local-dev composition; (5) extracted the `PackageFamilySelector` topological-sort into `Domain/Packaging/FamilyTopologyHelpers.cs` (Kahn + cycle detection + deterministic tie-break + 5 dedicated tests); (6) `ConsolidateHarvestTaskRunner` soft-fail replaced with hard-fail precondition guard carrying a local-dev + multi-runner-CI remediation hint; (7) `SetupLocalDevTaskTests` (tautological passthrough coverage) retired; `SetupLocalDevTaskRunnerTests` added covering Local happy path + Non-Local delegation; `LocalArtifactSourceResolverTests` reshaped to narrow-responsibility scope only. ADR-003 §3.3 carries a v1.6 amendment note documenting the split (SetupLocalDev composition lives in a dedicated Application-layer runner, not inside the resolver). Test count 391 → 398. Build-host green on Windows. End-to-end Windows witness (`SetupLocalDev --source=local --rid win-x64`) pending at commit time. | Deniz İrgin + session collaboration |
