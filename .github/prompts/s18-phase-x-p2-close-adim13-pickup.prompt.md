---
name: "S18 Phase X P2 close + Adım 13 pickup (post-build-host-modernization wave)"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the Phase X build-host modernization wave landed P0+P1+P2 on master (commits b18002f, 651ac2f, e602b6c, b6de515, 3ab2e68 — 2026-05-02). The ADR-002 DDD-layered build-host shape is fully retired in production code; the ADR-004 5-folder shape (Host/Features/Shared/Tools/Integrations) is live with 13 feature folders, per-feature ServiceCollectionExtensions × 13, BuildContext slimmed to 4 properties, *TaskRunner → *Pipeline rename complete, LayerDependencyTests → ArchitectureTests rewrite with 5 invariants (3 currently skipped under explicit P3-deadline tracking). Adım 13 follow-up wave (cross-tier violation cleanup + un-skip + ServiceCollectionExtensions smokes + cake-build-architecture.md ADR-004 rewrite) is the canonical next gate; Phase 2b PD-7 (public release) remains the parallel competing track per Deniz's call. Cross-platform smoke loop (Win fast / WSL Linux + ci-sim Win + macOS Intel milestone) operationalized via tests/scripts/verify-baselines.cs file-based-app helper."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` after the **Phase X build-host modernization wave landed P0+P1+P2 on master** across five atomic commits (`b18002f`, `651ac2f`, `e602b6c`, `b6de515`, `3ab2e68` — all 2026-05-02). The repo's build-host has migrated end-to-end from the ADR-002 DDD-layered shape (`Tasks/Application/Domain/Infrastructure/Context`) to the ADR-004 Cake-native feature-oriented shape (`Host/Features/Shared/Tools/Integrations`), and the cross-platform smoke validation loop has been hardened with a deterministic `verify-baselines.cs` gate.

This prompt supersedes `s17-phase-2b-public-release-pickup.prompt.md` for sessions that start after the Phase X P2 close session. Phase 2b PD-7 (public release / Trusted Publishing OIDC / first nuget.org publish) is still in flight as a parallel track — see "Recommended Next Step" for the A vs B fork.

## First Principle

Treat every claim here as **current-as-of-authoring (2026-05-02)** and verify against the live repo, git log, and canonical docs before acting.

The codebase is in a strong post-P2 state:

- ADR-004 5-folder shape live in production; `ArchitectureTests` enforces 5 direction-of-dependency invariants (3 currently skipped under §11 risk #4 named-exclusion-with-deadline pattern — Adım 13 closes them).
- 502 / 499 passed / 3 skipped at P2 close commit `3ab2e68`.
- smoke-witness behaviour signal byte-equal to P0 baseline across Win fast-loop + Win ci-sim milestone + WSL Linux milestone (commits along the wave verify each commit boundary; macOS Intel milestone last verified at P0 close `e602b6c` because the host has been asleep since).
- 5 doc-only updates pending commit at session-end (phase-x doc + ADR-004 + tests/scripts/README + playbook/cross-platform-smoke-validation + plan.md). See "Worktree expectation" below.

That means the highest-value next move is **either**:

1. **Adım 13 (post-P2 follow-up wave)** — cross-tier violation cleanup (~26 violations across 20 callsites), un-skip the 3 skipped `ArchitectureTests` invariants, land 13 `ServiceCollectionExtensions` smoke tests, rewrite `docs/knowledge-base/cake-build-architecture.md` to ADR-004 shape. This is the natural pre-P3 gate — phase-x §14 has the full inventory + 9-step sub-plan already.
2. **OR** — Phase 2b PD-7 (the bigger horizon) — public-release work: Trusted Publishing OIDC setup + `PublishPublicTask` real impl + first prerelease publish to nuget.org (#63) + `playbook/release-recovery.md` (PD-8). Phase X is intentionally non-gating for this — phase-x §1 declares it standalone.

Talk to Deniz before committing to which one. Adım 13 is ~2-3 sessions of focused refactor work (similar shape to the P2 wave — Shared/X promotes + un-skip + smokes). PD-7 is a multi-session arc with research-first cadence.

This repo still runs **master-direct commits** per Deniz direction. Do not create a branch unless there is a concrete reason.

## What Just Happened

This session covered an end-to-end **build-host modernization wave** plus a closing **doc sweep**. Five atomic commits on master + 5 doc-only modifications pending commit at session end:

### P0 Safety Baseline (2026-05-02)

- **`b18002f` — `feat(scripts): smoke-witness --emit-baseline + verify-baselines.cs (Phase X P0 kickoff)`** — `tests/scripts/smoke-witness.cs` gains additive `--emit-baseline <path>` flag (no behavioural change to existing `local`/`remote`/`ci-sim` modes); new `tests/scripts/verify-baselines.cs` .NET 10 file-based-app helper that spawns smoke-witness + diffs JSON via `JsonSerializer`, with fast-loop default + `--milestone` cadence per phase-x §2.1.5; first fast-loop baseline `tests/scripts/baselines/smoke-witness-local-win-x64.json` committed (3/3 PASS, 101.8s); `tests/scripts/baselines/test-count.txt = 500`; `tests/scripts/baselines/cake-targets.txt = 20 targets`. Phase-x doc + ADR-004 §2.14 rename criterion landed inline.
- **`651ac2f` — `feat(scripts): milestone-loop baselines (Linux + ci-sim Win) + macOS analyzer fix`** — milestone baselines for WSL Linux (3/3 PASS, 184.0s with hot vcpkg cache) + Windows ci-sim (9/9 PASS, 119.2s — full pipeline replay) committed; `CA1869` (JsonSerializerOptions reuse) added to `NoError`/`NoWarn` directives in both `smoke-witness.cs` and `verify-baselines.cs` (Mac-only TreatWarningsAsErrors trip — repo root inheritance); `osx-arm64` references corrected to `osx-x64` throughout (maintainer's macOS host is Intel).
- **`e602b6c` — `feat(scripts): macOS Intel milestone baseline + smoke-witness silent-mode log fix`** — `tests/scripts/baselines/smoke-witness-local-osx-x64.json` committed (3/3 PASS, 145.7s, mono on `$PATH` so `net462` slice runs); silent-mode-log persistence fix (`smoke-witness.cs InvokeProcessAsync` rewrite) — `verbose=off` now still writes the per-step `NN-<step>.log` so flaky failures leave forensic evidence on disk without requiring a verbose rerun. The fix split `InvokeProcessAsync` into `DrainSilentlyAsync`, `DrainVerboselyAsync`, `WriteLogAsync` helpers to pass `MA0051` (method too long) + `VSTHRD103` (sync blocks) on Mac.

### P1 Folder Migration (2026-05-02)

- **`b6de515` — `refactor(build-host): P1 retire ADR-002 layered shape, establish ADR-004 5-folder shape (Phase X P1)`** — 291 git mv (production + test mirror) + Rider Adjust Namespaces sweep + 3 stale `using` cleanup. ADR-002 namespaces (`Build.Tasks.*`, `Build.Application.*`, `Build.Domain.*`, `Build.Infrastructure.*`, `Build.Context`) empty in production code; ADR-004 5-folder shape live with 13 feature folders (`Features/Coverage`, `Features/Diagnostics`, `Features/Harvesting`, `Features/Info`, `Features/LocalDev`, `Features/Maintenance`, `Features/Packaging`, `Features/PackageConsumerSmoke`, `Features/Preflight`, `Features/Publishing`, `Features/Setup`, `Features/Vcpkg`, `Features/Versioning`). Verification all green: build clean, 500/500 tests, `LayerDependencyTests` natural-shrinkage green (transitional exception list shrunk to zero entries by content migration alone, per phase-x §5.4 rewrite), Win fast-loop MATCH 89.8s. Single landing commit despite 291-file scope — ADR-004 §1.5 mechanical-vs-structural waves classifies P1 as 100% mechanical relocation.

### P2 Terminology + DI rewrite (2026-05-02)

- **`3ab2e68` — `refactor(build-host): P2 BuildContext slim + *TaskRunner → *Pipeline + ServiceCollectionExtensions × 13 + ArchitectureTests rewrite (Phase X P2)`** — 12 sub-steps:
   - **Cosmetic warmup**: namespace tail + comment-noise cleanup left over from P1 sweeps.
   - **`Host/Cake/CakeExtensions.cs` split** into `CakeJsonExtensions.cs` + `CakePlatformExtensions.cs` + `CakeFileSystemExtensions.cs` per ADR-004 §2.2 (kitchen-drawer pattern fix).
   - **`BuildOptions` aggregate record** introduced at `Host/Configuration/BuildOptions.cs` per ADR-004 §2.11.1 — composes `VcpkgConfiguration`, `PackageBuildConfiguration`, `DotNetConfiguration`, `DumpbinConfiguration`, etc. Singleton DI registration in `Program.cs`.
   - **`BuildContext` slim from 6 → 4 properties** (`Paths`, `Runtime`, `Manifest`, `Options`); constructor 7-arg → 5-arg; `ManifestConfig` flows in as data (no longer a separate DI service); `Repo` / `DotNetConfiguration` / `Vcpkg` / `DumpbinConfiguration` removed (consumers route through `Options.X`).
   - **Sub-validator DI injection** (ADR-004 §1.1.6 anti-pattern fix) — `PackageOutputValidator` switches from `new XValidator()` construction to constructor injection of `NativePackageMetadataValidator`, `ReadmeMappingTableValidator`, `SatelliteUpperBoundValidator`. Same pattern applied to other `new XValidator()` / `new XGenerator()` callsites that surfaced.
   - **`*TaskRunner` → `*Pipeline` rename** × 16 classes + 2 interfaces + 14 test classes via regex `(?<=\w)TaskRunner\b → Pipeline` plus targeted `(?<=ConsumerSmoke)Runner\b → Pipeline`. Cake-native `LddRunner` / `OtoolRunner` (Cake-Tool naming triad) preserved per ADR-004 §2.10.
   - **`SetupLocalDevTaskRunner` → `SetupLocalDevFlow`** rename (the designated multi-feature orchestration slice gets `Flow` suffix, not `Pipeline`, per ADR-004 §2.5).
   - **Per-feature `ServiceCollectionExtensions` × 13** — each `Features/<X>/` folder gains a `ServiceCollectionExtensions.cs` with `AddXFeature(this IServiceCollection)`. Packaging extension takes a `string source` parameter (CLI `--source` value) because the `IArtifactSourceResolver` factory closure consumes it at composition time. LocalDev registered last in the chain per §2.5 + §2.13 invariant #4 allowlist.
   - **`Program.cs` DI chain collapse** — `ConfigureBuildServices` body reads as the feature roster: 13 `services.AddXFeature()` calls. Tools / Integrations / Host registrations remain inline (their grouping into `AddToolWrappers` / `AddIntegrations` / `AddHostBuildingBlocks` is **deferred to Adım 13** alongside the cross-tier-violation fixes — no point grouping registrations the architecture tests are about to refute). 3 stale `using Build.Infrastructure.DotNet;` directives cleaned.
   - **`Shared/Runtime` Cake-decoupling** (P1 transitional exception per ADR-004 §2.6 closes here) — `Cake.Core.PlatformFamily` references in `Shared/Runtime/IRuntimeProfile.cs` + `RuntimeProfile.cs` replaced with new build-host-local `Build.Shared.Runtime.RuntimeFamily` enum (Windows / Linux / OSX, value names match Cake's enum so `ToString()` callsites stay compatible). `IRuntimeProfile.IsSystemFile(FilePath)` → `IsSystemFile(string fileName)`. `RuntimeProfile.PlatformFamily` property → `Family` (cleaner, parallels Cake's `ICakePlatform.Family`). 7 production callsites + 9 test callsites updated. Cake-tier code (Tools, Integrations, ArtifactPlanner, CakePlatformExtensions) continues to consume `Cake.Core.PlatformFamily` directly via `ICakePlatform.Family` — that is the Cake-native side of the boundary.
   - **`LayerDependencyTests.cs` → `ArchitectureTests.cs`** rewrite — `git mv` + 5 invariants per ADR-004 §2.13: `Shared_Should_Have_No_Outward_Or_Cake_Dependencies`, `Tools_Should_Have_No_Feature_Dependencies`, `Integrations_Should_Have_No_Feature_Dependencies`, `Features_Should_Not_Cross_Reference_Except_From_LocalDev`, `Host_Is_Free`. **3 invariants currently `[Skip(...)]`'d** under phase-x §11 risk #4 named-exclusion-with-deadline pattern: invariant #1 (`Shared` has 6 violations against `Features/Harvesting/{BinaryClosure, BinaryNode}`), invariant #3 (`Integrations` has 7 violations against `Features.{Coverage, Packaging, Harvesting}.*` result types + 2 against `Host/Paths/IPathService`), invariant #4 (`Features` has 4 cross-references — `HarvestManifest`, G58 validator + result, `IUpstreamVersionAlignmentValidator`). Adım 13 closes these.
   - **502 tests / 499 passed / 3 skipped** at P2 close. Win fast-loop MATCH 92.5s, Win ci-sim 9/9 PASS 109.0s, WSL Linux 3/3 PASS 198.7s. macOS Intel skipped (sleep, non-gating per §10.5).

### Doc sweep (uncommitted at session end)

5 doc files modified (267 insertions / 27 deletions across `docs/decisions/...`, `docs/phases/...`, `docs/plan.md`, `docs/playbook/...`, `tests/scripts/README.md`):

- **`docs/phases/phase-x-build-host-modernization-2026-05-02.md`** (+156 / −23) — top-of-doc Wave-Status Snapshot table (P0/P1/P2 closed + Adım 13 pending + P3–P5 not started); §4.3 P0 + §6.5 P2 success criteria checked off with closure metrics; §13 references rename to `ArchitectureTests.cs`; **§14 Adım 13 (post-P2 follow-up wave) added** — full inventory of 26 cross-tier violations + 9-step sub-plan + risks + "must close before P3" gate; §15 change log entry; status header at top now reads "P0 + P1 + P2 CLOSED ... Adım 13 pending ... P3–P5 not started".
- **`docs/decisions/2026-05-02-cake-native-feature-architecture.md`** (+1) — change log entry: P0 + P1 + P2 closed on master + 5 commit references + Shared/Runtime Cake-decoupling realisation + Adım 13 cross-link. ADR invariants unchanged — only realisation status.
- **`tests/scripts/README.md`** (+35 / −3) — smoke-witness silent-mode log persistence note; verify-baselines design notes gain "fast-loop / milestone-loop dedup" + "per-host gating semantics" paragraphs; troubleshooting `Microsoft.Testing.Platform.dll is denied` entry (Win-only lingering-`testhost.exe` flake) with build-server shutdown ritual.
- **`docs/playbook/cross-platform-smoke-validation.md`** (+78 / −7) — Lingering process manual fallback extended to target both `dotnet` AND `testhost` process names (was `dotnet` only); pre-`verify-baselines.cs` ritual paragraph (1-minute aggressive age threshold for tight test-then-verify loops); **new "Host Liveness Pre-flight" section** with Mac SSH `BatchMode=yes`/`ConnectTimeout=5` probe pattern + exit-code semantics + WSL `wsl zsh -c '…'` absolute-path-bind cross-link; Failure Triage gains category 4 (lingering process flake — not a regression).
- **`docs/plan.md`** (+24 / −7) — Last-updated 2026-05-02; "Current Phase" → "Current Phases" with Phase 2 + Phase X parallel tracks; new Phase X 5-row wave table (P0 closed / P1 closed / P2 closed / Adım 13 pending / P3–P5 not started) + non-gating note explaining Phase X interleave with Phase 2.

**These 5 doc changes are unstaged in the worktree at session end** — Deniz had not yet approved a commit when the session closed (per Approval Gate). Confirm with `git status` and seek explicit "go" before committing.

## Onboarding Snapshot

This repo is the packaging/build/distribution layer for **modular C# SDL2 bindings first, SDL3 later**, with **native libraries built from source via vcpkg** and shipped through **NuGet**.

Build backbone:

- `.NET 9 / C# 13` (build host); `.NET 10 SDK` for `tests/scripts/*.cs` file-based apps (pinned via `tests/scripts/global.json`)
- `Cake Frosting 6.1.0` build host under `build/_build/` — **now ADR-004 5-folder shape** (`Host/Features/Shared/Tools/Integrations`); ADR-002 layered shape retired in production code at P1 close `b6de515`
- `vcpkg` for native builds (custom `*-hybrid` overlay triplets at `vcpkg-overlay-triplets/`)
- `GitHub Actions` for the RID matrix + release pipeline + builder image
- `build/manifest.json` schema v2.1 as the single source of truth (legacy `runtimes.json` / `system_artefacts.json` retired — merged in)
- `NuGet.Protocol 7.3.1` as the in-process feed client (read + write)
- `TUnit 1.33.0 + Microsoft.Testing.Platform` for test running; **502 tests at P2 close, 3 skipped under explicit P3-deadline tracking**

Locked decisions still not open for casual re-debate:

- **Hybrid static + dynamic core** packaging model
- **Triplet = strategy** (no standalone `--strategy` CLI flag)
- **Package-first consumer contract** (ADR-001)
- **Cake owns orchestration policy; YAML stays thin**
- **7 RID coverage remains in scope**
- **LGPL-free codec stack** for SDL2_mixer
- **GH Packages = internal CI staging only**; external consumers via nuget.org (PD-7)
- **`local.*` prerelease versions cannot reach staging feed** (PublishPipeline guardrail; renamed from PublishTaskRunner at P2)
- **ADR-004 5-folder shape** (`Host/Features/Shared/Tools/Integrations`) supersedes ADR-002 DDD layering — production code is fully migrated
- **`BuildContext` is invocation state, not a service locator** (ADR-004 §2.11)
- **Pipelines target `RunAsync(BuildContext, TRequest)`** at P2 (interim); P4 closes the cut-over to `RunAsync(TRequest)` only
- **`Shared/` no Cake dependency** (ADR-004 §2.6 transitional exception closed at P2)

Target RIDs (canonical in `build/manifest.json runtimes[]`):

- `win-x64` / `win-x86` / `win-arm64`
- `linux-x64` / `linux-arm64`
- `osx-x64` / `osx-arm64`

(Note: maintainer's macOS host is Intel `osx-x64`; `osx-arm64` is CI-only until Apple Silicon hardware enters rotation.)

## Current State You Should Assume Until Verified

- **Master HEAD**: most recent commit is `3ab2e68` (P2 close). `git log --oneline -10` should show the 5-commit Phase X arc above. **5 doc-only files unstaged** awaiting commit approval (see "Doc sweep" above + `git status`).
- **Worktree expectation**: 5 modified doc files in unstaged state; otherwise clean (no untracked production changes).
- **Build-host tests**: 502 / 499 passed / 3 skipped at P2 close. 3 skipped invariants: `Shared_Should_Have_No_Outward_Or_Cake_Dependencies`, `Integrations_Should_Have_No_Feature_Dependencies`, `Features_Should_Not_Cross_Reference_Except_From_LocalDev` — all skip messages reference phase-x §14 Adım 13.
- **Behaviour signal (smoke-witness baselines)**:
  - `smoke-witness-local-win-x64.json` — fast-loop, MATCH at every Phase X commit boundary on the Windows dev host.
  - `smoke-witness-ci-sim-win-x64.json` — milestone, MATCH at every P-wave close commit.
  - `smoke-witness-local-linux-x64.json` — milestone, MATCH at every P-wave close commit (WSL Ubuntu).
  - `smoke-witness-local-osx-x64.json` — milestone, last verified at P0 close `e602b6c` (3/3 PASS, 145.7s). **Not re-verified at P1 / P2 close** because the macOS Intel host has been asleep — non-gating per phase-x §10.5; opportunistic re-verification on next macOS rotation is appropriate.
- **`ArchitectureTests` invariants**: 5 total, 2 currently green, 3 skipped under phase-x §11 risk #4 named-exclusion pattern with explicit P3-deadline tracking. Adım 13 un-skips them.
- **Phase 2b PD-7 / public release**: **untouched in this session**. Last state remains as captured in `s17-phase-2b-public-release-pickup.prompt.md` — implementation-closed PD-5, gate-lift on tag-push staging done at 2026-04-29, multi-platform `--source=remote` witness on Linux/macOS still pending, PD-7 (Trusted Publishing OIDC + first nuget.org publish) not started.

## Cross-Platform Testing Learnings (session-derived; folded into docs)

The Phase X wave drove a tight test-then-verify loop across Windows + WSL Linux + macOS Intel. The following gotchas surfaced and were folded into the docs — re-read them before driving the next cross-platform run:

1. **Lingering `testhost.exe` flake (Windows only).** TUnit / Microsoft.Testing.Platform's `testhost.exe` survives `dotnet build-server shutdown`'s 10-minute reuse window and holds an open file handle on `Microsoft.Testing.Platform.dll`. The very next `01-CleanArtifacts` step in `smoke-witness.cs` then trips with `Access to the path 'Microsoft.Testing.Platform.dll' is denied`. Mitigation captured at:
   - `tests/scripts/README.md` Troubleshooting `Microsoft.Testing.Platform.dll is denied` entry — quick-reference recipe.
   - `docs/playbook/cross-platform-smoke-validation.md` Lingering dotnet processes mitigation — full background + manual fallback PowerShell ritual + pre-`verify-baselines.cs` ritual paragraph (1-minute aggressive age threshold for tight loops).
   - **Recipe**: `dotnet build-server shutdown; Get-Process dotnet, testhost | Where-Object { ... -gt 1 ... } | Stop-Process -Force; Start-Sleep -Seconds 3` before the next `verify-baselines.cs` invocation. Targeting both `dotnet` AND `testhost` process names is required (was `dotnet` only in pre-Phase-X playbook).

2. **Mac SSH liveness probe.** macOS Intel host at `Armut@192.168.50.178` auto-sleeps. Before driving an SSH milestone-loop run from a script, probe with `ssh -o ConnectTimeout=5 -o BatchMode=yes Armut@192.168.50.178 'echo MAC_AWAKE'`. Exit `0` → proceed; exit `255` + `Connection timed out` → host asleep, skip macOS slot (phase-x §10.5 non-gating); exit `255` + `Permission denied (publickey,…)` → key auth needs setup. `BatchMode=yes` is the critical flag — suppresses interactive prompts so the probe fails fast with deterministic exit codes. Captured at `docs/playbook/cross-platform-smoke-validation.md` "Host Liveness Pre-flight" section.

3. **WSL `wsl zsh -c '…'` PWD env leakage.** When driving WSL from a Windows PowerShell harness via `wsl zsh -c '…'`, PowerShell's `$PWD` leaks into WSL's environment via the WSLENV bridge and shadows zsh's autoinit-on-cd workdir. Variable expansions (`$REPO`) interpolated through this bridge can stale-out before zsh sees the value. **Always bind absolute paths up front** (`cd /home/deniz/repos/sdl2-cs-bindings && …`) rather than relying on shell-variable expansion. The PowerShell-side `$REPO` substitution caused `fatal: not a git repository` in the first WSL milestone-loop attempt — fixed by switching to literal absolute paths. Already documented at `docs/playbook/cross-platform-smoke-validation.md` "PWD env leakage in non-interactive cross-shell invocations" section; "Host Liveness Pre-flight" cross-references it.

4. **macOS analyzer strict mode.** Mac builds inherit `TreatWarningsAsErrors=true` from repo-root `Directory.Build.props` even for `tests/scripts/*.cs` file-based apps. `CA1869` (JsonSerializerOptions reuse) tripped on Mac but not on Windows / Linux for the same source — added to `NoError` / `NoWarn` directives in both `smoke-witness.cs` and `verify-baselines.cs` at commit `651ac2f`. Pattern to remember: when a `.cs` script-tier file fails on Mac with an analyzer error, the fix is the file's `// <#:property NoError="..."/>` directive (script-tier convention), not changing global analyzer config.

5. **`smoke-witness.cs` silent-mode log persistence.** Pre-fix, `--verbose=off` runs dropped the per-step `NN-<step>.log` file entirely — flaky failures left zero forensic evidence on disk and required a verbose rerun to diagnose. Post-fix at commit `e602b6c`, both modes write the log file; verbose mode just adds live console echo on top. The fix split `InvokeProcessAsync` into `DrainSilentlyAsync`, `DrainVerboselyAsync`, `WriteLogAsync` async helpers to pass `MA0051` (method too long) + `VSTHRD103` (sync blocks) on Mac.

6. **`verify-baselines.cs` `BuildEntries` dedup.** Original implementation double-ran `smoke-witness-local-linux-x64.json` on Linux when invoked with `--milestone` (once as fast-loop host-match + once as milestone catalogue entry). Fix at P0 close: dedup entries by `(mode, target_rid)` so host-matched fast-loop slot wins. Cosmetic but observable in the run summary panel.

7. **`dotnet test build/_build.Tests/...` TestRunParameters timing.** Tests occasionally exhibit a single-pass flake on first run after a clean rebuild — typically resolved by a re-run within the same session. If `dotnet test` reports an unexpected failure, re-run once before treating it as a regression. (Not specific to Phase X; surfaced multiple times during P1/P2 sub-step verification.)

## What Changed In Canonical Docs

(See "Doc sweep" above for full diff stats. Listing here for navigation; verify with `git diff --stat` before writing follow-up.)

- `docs/phases/phase-x-build-host-modernization-2026-05-02.md` — **the single most important doc to read for Adım 13 entry.** Top-of-doc Wave-Status Snapshot table; §4.3 P0 + §6.5 P2 success criteria checked off with metrics; §14 Adım 13 (post-P2 follow-up wave) — full violation inventory + 9-step sub-plan + risks + must-close-before-P3 gate.
- `docs/decisions/2026-05-02-cake-native-feature-architecture.md` — change log entry for P2 close; ADR invariants unchanged.
- `tests/scripts/README.md` — silent-mode log persistence note; verify-baselines design notes; troubleshooting entry for the `Microsoft.Testing.Platform.dll` flake.
- `docs/playbook/cross-platform-smoke-validation.md` — lingering process v2 (testhost extension + 1-min ritual); new Host Liveness Pre-flight section; Failure Triage category 4.
- `docs/plan.md` — Last-updated 2026-05-02; Current Phases (Phase 2 + Phase X parallel) + Phase X 5-row wave table.

**Not yet updated** (Adım 13 work):

- `docs/knowledge-base/cake-build-architecture.md` still describes the ADR-002 layered shape. Per phase-x §14.3 sub-step 14.3.8, this rewrite lands atomic with the cross-tier violation cleanup commit. Do not start the rewrite ahead of Adım 13 — the doc's content depends on where the cross-tier types end up settling.
- `AGENTS.md` / `CLAUDE.md` build-host reference patterns: re-checked at P2 close, already point at ADR-004 from prior session. No edits needed.

## Recommended Next Step

### Recommended pickup A — Adım 13 (post-P2 follow-up wave)

Lowest-friction path because phase-x §14 already lays out the full 9-sub-step plan. The 26 cross-tier violations cluster into 5 promotion targets:

1. **Adım 13.1 — Shared/Harvesting promote.** `BinaryClosure`, `BinaryNode`, `HarvestManifest`, `PackageInfoResult / PackageInfoError` from `Features/Harvesting/` → `Shared/Harvesting/`. Closes 11 of 26 violations.
2. **Adım 13.2 — Shared/Coverage promote.** `CoverageMetrics`, `CoverageBaseline`, `CoverageCheckResult / Success / Error` → `Shared/Coverage/`. Closes 4 violations.
3. **Adım 13.3 — Shared/Packaging promote.** `DotNetPackResult / DotNetPackError`, `ProjectMetadataResult / ProjectMetadataError`, G58 result + interface → `Shared/Packaging/`. Closes 5–6 violations. **Decision needed inline**: does `IG58CrossFamilyDepResolvabilityValidator` move to Shared (treating G58 as a cross-feature seam — §2.9 criterion 2) or stay in Packaging behind a Preflight-side facade? Talk to Deniz before committing.
4. **Adım 13.4 — Shared/Versioning promote.** `IUpstreamVersionAlignmentValidator` (+ result) → `Shared/Versioning/`. Multi-impl §2.9-criterion-1 seam (manifest + git-tag + explicit providers all consume it). Closes 3 violations.
5. **Adım 13.5 — Integrations Host-decoupling.** Replace `IPathService` type-references in `Integrations/DotNet/*` and `Integrations/Vcpkg/*`. **Decision needed inline**: type-decouple now (likely needs a temporary record adapter) or accept as P4 prerequisite + named exception on `Integrations_Should_Have_No_Feature_Dependencies`? Phase-x §14.5 "IPathService Host-coupling" risk paragraph has the framing.

Then:

6. **Adım 13.6 — Un-skip 3 ArchitectureTests invariants.** Remove `[Skip(...)]` annotations; assert green. Test count target: 502 → 502 (skip→pass within the same test methods). Wave-close gating only.
7. **Adım 13.7 — Per-feature ServiceCollectionExtensions smoke tests × 13.** Land `TestHostFixture.AddTestHostBuildingBlocks()` shared infrastructure (Cake fakes via `FakeFileSystem` + Shared vocabulary fakes + integration substitutes), then add `Add<X>Feature_Should_Register_All_Pipeline_And_Validator_Types` smoke per feature. Test count target: 502 → ~515 (+13 smokes).
8. **Adım 13.8 — `cake-build-architecture.md` ADR-004 rewrite.** Atomic same-commit update of the canonical architecture doc. Replace ADR-002 `Tasks/Application/Domain/Infrastructure` tree with ADR-004 5-folder shape, LocalDev orchestration-feature exception, Pipeline / Flow vocabulary, BuildContext discipline rule.
9. **Adım 13.9 — Optional: `AddToolWrappers` / `AddIntegrations` / `AddHostBuildingBlocks` extension grouping.** Polish the `Program.cs` DI section into 16 `AddX*()` calls (13 features + 3 cross-cutting groups). Optional — only do if Deniz signals appetite.

Closure criteria + risk table + step-by-step gating in phase-x §14.4–§14.5. Each sub-step is its own atomic commit with fast-loop verify-baselines.cs MATCH; milestone loop runs at Adım 13 close.

### Recommended pickup B — Phase 2b PD-7 (public release horizon)

Genuinely a multi-session arc. Carry-over from `s17-phase-2b-public-release-pickup.prompt.md`:

1. **Multi-platform `--source=remote` witness pre-PD-7.** Run `tests/scripts/smoke-witness.cs remote --verbose` on WSL Linux + macOS Intel. Mac SSH liveness probe first (recipe in cross-platform-smoke-validation.md "Host Liveness Pre-flight").
2. **Decide promotion-path mechanism** — `Promote-To-Public.yml` separate workflow, OR a stage on `release.yml` itself, OR a meta-tag-driven full-train workflow per PD-7. ADR-003 §6 has the framing.
3. **Trusted Publishing (OIDC) for nuget.org** — GitHub Actions OIDC token → nuget.org trust relationship → keyless push. nuget.org docs + Andrew Lock 2025 article.
4. **`PublishPublicTask` real impl** — Cake-side counterpart. Likely a separate `*Pipeline` (per ADR-004 §2.10 naming) from the existing `PublishPipeline` (renamed from `PublishTaskRunner` at P2) because the auth model differs (OIDC vs PAT) and the source feed differs (read from staging, push to public).
5. **`docs/playbook/release-recovery.md`** drafted (PD-8). Unhappy-path runbook.
6. **First prerelease publish** to nuget.org (#63).

If Deniz says "go big", pickup B; otherwise default to A. **Pickup B can technically interleave with pickup A** — Phase X is non-gating for Phase 2b release work per phase-x §1 standalone-phase declaration. But scope-control suggests finishing Adım 13 first because it un-skips invariants that PD-7 PR review will want green.

## Recommended Workflow For The Next Agent

1. Read the mandatory grounding below in order.
2. Run `git log --oneline -10` and confirm the 5-commit Phase X arc (`b18002f` through `3ab2e68`) is on master HEAD.
3. Run `git status` to confirm 5 doc-only files in unstaged state. **Ask Deniz whether to commit the doc sweep first or fold it into Adım 13's first commit** — both are reasonable; Deniz's instinct trends toward larger commits.
4. Run `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo` and confirm 502 / 499 passed / 3 skipped. The 3 skip messages should reference phase-x §14 Adım 13.
5. Run the pre-`verify-baselines.cs` ritual (cross-platform-smoke-validation.md), then `cd tests/scripts && dotnet run verify-baselines.cs`. Expect MATCH on `smoke-witness-local-win-x64.json`.
Then branch by which pickup Deniz signals:

#### If pickup A (Adım 13)

- Read phase-x §14 in full — it's the canonical inventory.
- Inspect `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs` to confirm the 3 skip messages match the §14.2 inventory.
- For each Adım 13.1–13.5 sub-step: identify production callsites, plan the `git mv` set, draft the sub-step commit, run `verify-baselines.cs` fast-loop, commit, repeat.
- Adım 13.6 (un-skip) lands as its own atomic commit after 13.1–13.5 are all green.
- Adım 13.7 (smokes) lands after un-skip — building the `TestHostFixture` infrastructure is a multi-hundred-line file; budget for it explicitly.
- Adım 13.8 (cake-build-architecture.md rewrite) lands atomic with… probably 13.6 or 13.7's commit, Deniz's call.
- Milestone-loop verify-baselines.cs at Adım 13 close (WSL Linux + Win ci-sim minimum; macOS Intel if reachable per liveness probe).

#### If pickup B (Phase 2b PD-7)

- Re-read `s17-phase-2b-public-release-pickup.prompt.md` "Recommended pickup B" section — most of the framing carries over.
- Research-first pass on Trusted Publishing OIDC for nuget.org (Andrew Lock 2025 article + nuget.org docs).
- Sketch the workflow / `*Pipeline` shape with Deniz before writing code.

## If Pickup A Lands Cleanly

Move to P3 (interface review per ADR-004 §2.9). **Do not start P3 with skipped ArchitectureTests invariants** — the whole point of the §11 risk #4 P3-deadline tracking is that P3 starts on a green architecture. Adım 13 closure is the gate.

P3 scope: review every `I*` interface in `build/_build/` against §2.9 admission criteria (multi-impl OR independent-axis-of-change OR high-cost test seam). Single-impl interfaces backed only by mocks get pruned to `internal sealed class` registered concretely. Test wall-time budget per phase-x §12.3: total `dotnet test` wall time ≤ (P2 close wall time × 1.20). Capture P2-close wall time at session start before Adım 13 work begins so the P3 gate has a baseline.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `CLAUDE.md`
4. `docs/plan.md` (especially the new Current Phases + Phase X 5-row wave table)
5. `docs/phases/phase-x-build-host-modernization-2026-05-02.md` (**the most important doc** — §14 Adım 13 has the full violation inventory + sub-step plan)
6. `docs/decisions/2026-05-02-cake-native-feature-architecture.md` (governing ADR-004)
7. `docs/decisions/2026-04-19-ddd-layering-build-host.md` (superseded ADR-002 — historical context for what was just retired)
8. `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs` (the 5 invariants + 3 skips)
9. `build/_build/Host/BuildContext.cs` (slim 4-property contract post-P2)
10. `build/_build/Host/Configuration/BuildOptions.cs` (aggregate per ADR-004 §2.11.1)
11. `build/_build/Program.cs` (DI chain shape post-P2: 13 `services.AddXFeature()` calls + inline Tools/Integrations/Host registrations awaiting Adım 13 grouping)
12. Any one feature's `ServiceCollectionExtensions.cs` for shape reference — recommended `build/_build/Features/Packaging/ServiceCollectionExtensions.cs` (the largest feature, includes the `string source` parameter pattern)
13. `docs/playbook/cross-platform-smoke-validation.md` (especially Lingering process mitigation + Host Liveness Pre-flight + Failure Triage)
14. `tests/scripts/README.md` + `tests/scripts/smoke-witness.cs` + `tests/scripts/verify-baselines.cs` (the witness loop)

For pickup B specifically (re-grounding, items 15–18):

- `.github/prompts/s17-phase-2b-public-release-pickup.prompt.md`
- `docs/phases/phase-2-adaptation-plan.md` (PD-7 / PD-8 entries)
- `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (§6 PD-7 framing)
- `.github/workflows/release.yml`

Live-state snapshots:

- `docs/knowledge-base/cake-build-architecture.md` — **stale; describes ADR-002 layered shape**. Read it knowing the rewrite is part of Adım 13.8; do not act on its content for current shape-of-truth.
- `docs/knowledge-base/release-lifecycle-direction.md`
- `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`

Historical archaeology only when needed:

- `.github/prompts/s17-phase-2b-public-release-pickup.prompt.md` (Phase 2b state circa 2026-04-29)
- `.github/prompts/s5-ddd-layering-post-adr002-continuation.prompt.md` (the ADR-002 layered-shape origin session — useful only if comparing why ADR-004 superseded it)
- `docs/_archive/`

External references for pickup B:

- nuget.org Trusted Publishing docs (NuGet team blog 2024/2025).
- Andrew Lock — "Easily publishing NuGet packages from GitHub Actions with Trusted Publishing" (2025).

## Locked Policy Recap

These still do not change without explicit Deniz override:

- **Master-direct commits**
- **No commit without approval** (Deniz says "go" / "yap" / "apply" / "proceed" / "başla" — the Approval Gate per AGENTS.md is binding)
- **Cake remains the policy owner; YAML stays thin**
- **`BuildContext` is invocation state, not a service locator** (ADR-004 §2.11; P4 closes the `RunAsync(BuildContext, TRequest)` interim signature)
- **Pipelines are size-triggered, not convention-triggered** (~200 LOC threshold; below stays in Task with private methods, above extracts to `<X>Pipeline.cs`)
- **`Shared/` no Cake dependency** (ADR-004 §2.6; P1 transitional exception closed at P2)
- **`Tools/` is Cake `Tool<TSettings>` wrappers ONLY** (ADR-004 §2.10)
- **`Integrations/` is non-Cake-Tool external adapters** (NuGet protocol client, dotnet pack invoker, project metadata reader, coverage XML readers, vcpkg manifest reader, MSVC env resolver)
- **Cross-feature data sharing flows through `Shared/`** (ADR-004 invariant #4; one allowlist exception for `Features/LocalDev/`)
- **Lock-file strict mode stays scoped to the build host**
- **GH Packages NuGet always requires PAT auth** — anonymous read is not supported.
- **External consumer feed = nuget.org** (Phase 2b PD-7); GH Packages stays internal CI staging only.
- **`local.*` prerelease versions cannot reach staging feed** — `PublishPipeline` guardrail enforces this.
- **`skipDuplicate=false` on push** — re-push at the same version fails loud.
- **Test naming convention**: `<MethodName>_Should_<Verb>_<optional When/If/Given>` (PascalCase method name + underscores between every other word segment + `Should` always present)
- **Test folders mirror production**: `Unit/Features/Packaging/PackagePipelineTests.cs` asserts the contract of `Features/Packaging/PackagePipeline.cs`
- **Living docs rule**: if a code change shifts behaviour / topology / infrastructure, update `docs/plan.md` or the active phase doc in the same change
- **Commit message style**: short subject + 4-8 short bullets, not prose paragraphs

## Final Steering Note

P0 + P1 + P2 closed cleanly across 5 atomic commits + cross-platform smoke loop hardened. The natural rhythm for the next session:

- **Default**: pickup A (Adım 13). Phase-x §14 is the canonical pre-flight; the 9-sub-step plan reads commit-by-commit.
- **If Deniz signals "public release horizon"**: pickup B (PD-7). Re-ground via s17 + phase-2-adaptation-plan PD-7. Adım 13 can park briefly without rotting — the skipped invariants don't drift.
- **Both wrong**: ask. Don't open with five parallel futures. The repo just absorbed a 5-commit refactor wave; the next move should be deliberate, not opportunistic.

The build-host is in its best shape since the project started — hold the line.
