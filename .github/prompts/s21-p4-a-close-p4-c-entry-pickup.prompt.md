---
name: "S21 P3+P4-A close → P4-C entry pickup"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after Phase X P3 (interface review) + P4-A (Pipeline RunAsync cut-over) landed as a single atomic commit on master (d1127e4, 2026-05-02). P3: 4 mock-only/stateless interfaces removed, 28 retained with criterion labels. P4-A: 11 Pipelines + 2 interface signatures + 15 Cake Tasks cut over from RunAsync(BuildContext, TRequest) → RunAsync(TRequest); ADR-004 §2.11.1 migration exception closed. VcpkgBootstrapTool relocated to Integrations/Vcpkg/. IPathService fluent split permanently discarded — every canonical doc updated. Cross-platform: 515/515 tests + ci-sim 9/9 on Windows, WSL Linux, and macOS Intel. P4-C (large Pipeline decomposition) + P5 (naming cleanup) are the canonical next gates; Phase 2b PD-7 (public release) remains the parallel competing track."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` after **Phase X P3 (interface review) + P4-A (Pipeline RunAsync cut-over) landed as a single atomic commit on master** at `d1127e4` (2026-05-02). The build host has never been in stronger architectural shape — ADR-004's discipline is fully enforced in both interface design and invocation contracts.

## First Principle

Treat every claim here as **current-as-of-authoring (2026-05-02 — P3+P4-A close)** and verify against the live repo, git log, and canonical docs before acting.

The codebase is in its strongest post-P4-A state:

- **P3 closed**: 4 stateless/mock-only interfaces removed (`ICoverageThresholdValidator`, `IVersionConsistencyValidator`, `ICoreLibraryIdentityValidator`, `IStrategyCoherenceValidator`); 28 interfaces retained with explicit ADR-004 §2.9 criterion labels (5 C1 multi-impl, 13 C2 independent-axis, 10 C3 transitional debt).
- **P4-A closed**: 11 Pipelines + 2 interface signatures (`IPackagePipeline`, `IPackageConsumerSmokePipeline`) + 15 Cake Tasks cut over from `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest, CT)`. ADR-004 §2.11.1 migration exception **closed** — zero Pipelines accept `BuildContext` in their entry-point method. Two known deferrals documented inline: `EnsureVcpkgDependenciesPipeline.Run(BuildContext)` (P4-deferred — needs own Request DTO) and `SetupLocalDevFlow.RunAsync(BuildContext, CT)` (P4-deferred — awaits sub-pipeline and own cut-over).
- **CoverageCheckPipeline (the 11th)** was initially missed because it is sync (`void Run(BuildContext)`) — caught by adversarial review and cut over with `ICakeArguments` injected for `--coverage-file` override.
- **VcpkgBootstrapTool** relocated from `Tools/Vcpkg/` to `Integrations/Vcpkg/` (sealed concrete wrapping `bootstrap-vcpkg.bat`/.sh, not a Cake `Tool<TSettings>`). `AddToolWrappers()` is now a clean no-op body.
- **IPathService fluent split permanently discarded** — the 50+-member interface split into `BuildPaths` sub-groups didn't justify its churn cost. Every canonical doc updated (ADR-004 §3.5 rewritten, phase-x §8.3 removed + renumbered, ArchitectureTests allowlist entries marked permanent).
- **515/515 tests, 0 skipped** on all 3 maintainer hosts.
- **Cross-platform verification at `d1127e4`**: Win local MATCH 88.4s + ci-sim MATCH 107.4s; WSL Linux local MATCH 77.0s + ci-sim 9/9 PASS 82.4s; macOS Intel local MATCH 122.3s + ci-sim 9/9 PASS 143.2s.
- Review-driven cleanups landed: `p4DeferredAllowlist` → `permanentIntegrationsAllowlist`, `InspectHarvestedDependenciesTaskRunnerTests` → `PipelineTests`, HarvestPipeline call-site whitespace normalized, `OtoolAnalyzePipeline` "system_artefacts.json" → "manifest.json system_exclusions", stale `using SDL2;` removal in `Compile.NetStandard/Probe.cs` reverted.

That means the highest-value next move is **either**:

1. **P4-C — Large Pipeline internal decomposition** (ADR-004 §8.3) — `PackageConsumerSmokePipeline` (~685 LOC), `HarvestPipeline` (~628 LOC), `PackagePipeline` (~554 LOC) are candidates for breaking into smaller per-concern co-located concrete helpers. **No new interfaces** unless §2.9 criteria justify. Per-Pipeline judgment — not all three need to refactor. This is optional; the ~200 LOC threshold is a smell signal, not a hard rule.
2. **P5 — Naming Cleanup + Atomic Wave** (ADR-004 §9) — `PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`. Each rename is one atomic commit spanning `[TaskName]` attribute + `smoke-witness.cs` step labels + `release.yml` `--target` references + `cross-platform-smoke-validation.md` checkpoints + live docs. Retire `UnsupportedArtifactSourceResolver`.
3. **OR — Phase 2b PD-7** (public release horizon) — Trusted Publishing OIDC + `PublishPublicTask` real impl + first prerelease publish to nuget.org (#63). Adım 13 + P3 + P4-A close means PD-7 PR review will see architecture invariants green and Pipeline signatures fully modernized.

Talk to Deniz before committing to which one. P4-C is ~1 session per pipeline (or a single skip if Deniz decides the current size is fine). P5 is one session of 3 atomic rename commits. PD-7 is a multi-session arc.

This repo still runs **master-direct commits** per Deniz direction. Do not create a branch unless there is a concrete reason.

## What Just Happened

This session covered P3 close + VcpkgBootstrapTool relocation + IPathService fluent split discard + P4-A Pipeline RunAsync cut-over + adversarial review fixes + cross-platform verification:

### P3 — Interface Review (ADR-004 §2.9)

Four interfaces failed the §2.9 criteria and were removed:

| Interface | Replacement | Why |
|---|---|---|
| `ICoverageThresholdValidator` | `static CoverageThresholdValidator.Validate()` | Pure stateless pass/fail rule |
| `IVersionConsistencyValidator` | `static VersionConsistencyValidator.Validate()` | Pure stateless manifest/vcpkg comparison |
| `ICoreLibraryIdentityValidator` | `static CoreLibraryIdentityValidator.Validate()` | Pure stateless manifest invariant |
| `IStrategyCoherenceValidator` | Concrete `StrategyCoherenceValidator` in DI | Single impl with real `IStrategyResolver` dep; interface added no seam |

28 interfaces retained with explicit criterion labels in phase-x §7.3. DI registrations updated. Tests rewritten (NSubstitute mocks → concrete/static calls). 515/515 tests, 0 skipped.

### VcpkgBootstrapTool relocation

Moved from `Tools/Vcpkg/` (namespace `Build.Tools.Vcpkg`) to `Integrations/Vcpkg/` (namespace `Build.Integrations.Vcpkg`). Registration moved from `Tools/ServiceCollectionExtensions.AddToolWrappers()` to `Integrations/ServiceCollectionExtensions.AddIntegrations()`. `AddToolWrappers()` is now a clean no-op body. The strict ADR-004 §2.10 invariant — `Tools/` is Cake `Tool<TSettings>` wrappers ONLY — is now fully enforced in the DI layer too.

### IPathService fluent split permanently discarded

The `IPathService` fluent split (originally scoped to P4 §8.3 as `BuildPaths.Harvest` / `.Packages` / `.Smoke` / `.Vcpkg`) was permanently discarded. The 50+-member interface consumed at hundreds of callsites would have required a multi-commit sub-wave of mechanical rewrites whose cost-to-benefit ratio didn't justify the churn. `IPathService` remains the canonical Host-tier path abstraction; Integrations adapters may consume it as a cross-cutting concern. Every canonical doc was updated:

- **ADR-004 §3.5**: "Why PathService split is also deferred" → "Why PathService split was discarded"
- **phase-x §8.3**: Entire section removed, §8.4→§8.3 renumbered, §8.5→§8.4
- **ArchitectureTests**: Allowlist entries marked permanent, comment says "permanently tolerated"
- **cake-build-architecture.md**: Tree comment updated, invariant #3 description updated
- **plan.md**: P4 row split, IPathService reference removed

### P4-A — Pipeline RunAsync(BuildContext, TRequest) → RunAsync(TRequest, CT) cut-over

Every Pipeline in the build host no longer accepts `BuildContext` in its entry-point method. This closes ADR-004 §2.11.1 migration exception (Pipelines were allowed to accept `BuildContext` as a transitional state during P1/P2 migration).

| Pipeline | Signature change | Constructor additions |
|---|---|---|
| `InfoPipeline` | `RunAsync(BuildContext)` → `RunAsync()` | `ICakeContext`, `ICakeLog` |
| `InspectHarvestedDependenciesPipeline` | `RunAsync(BuildContext)` → `RunAsync()` | `VcpkgConfiguration` |
| `OtoolAnalyzePipeline` | `RunAsync(BuildContext)` → `RunAsync()` | `ICakeContext`, `ICakeLog`, `IPathService`, `DumpbinConfiguration` |
| `ConsolidateHarvestPipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | `ICakeContext`, `ICakeLog`, `IPathService` |
| `HarvestPipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | `ICakeContext`, `ICakeLog`, `IPathService` |
| `NativeSmokePipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | `VcpkgConfiguration` |
| `PublishPipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | (already had all deps) |
| `PackagePipeline` (+ `IPackagePipeline`) | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | (already had all deps) |
| `PackageConsumerSmokePipeline` (+ `IPackageConsumerSmokePipeline`) | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | (already had all deps) |
| `PreflightPipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | `ICakeContext`, `ICakeLog`, `IPathService` |
| `CoverageCheckPipeline` | `void Run(BuildContext)` → `void Run()` | `ICakeContext`, `ICakeLog`, `IPathService`, `ICakeArguments` |

15 Cake Tasks updated — all now call `pipeline.RunAsync(request)` instead of `pipeline.RunAsync(context, request)`. `PackageConsumerSmokeTask` gained `IRuntimeProfile` + `IPathService` ctor params (replaces `context.Runtime.Rid` + `context.Paths.PackagesOutput`).

Two known deferrals:
- `EnsureVcpkgDependenciesPipeline.Run(BuildContext)` — P4-deferred with tracking comment citing phase-x §8.2
- `SetupLocalDevFlow.RunAsync(BuildContext, CT)` — P4-deferred; awaits own Request DTO + sub-pipeline cut-overs

### Adversarial review fixes

Two independent agents reviewed the entire diff cold. BLOCKER/BUG fixes applied:

- **CoverageCheckPipeline** (11th, sync `void Run(BuildContext)`) missed in initial cut-over — caught by both reviewers
- **Whitespace** in 8 HarvestPipeline call sites normalized
- **`p4DeferredAllowlist`** → `permanentIntegrationsAllowlist` in ArchitectureTests.cs
- **`InspectHarvestedDependenciesTaskRunnerTests`** → `InspectHarvestedDependenciesPipelineTests`
- **`EnsureVcpkgDependenciesPipeline`** P4-deferred tracking comment added
- **`TestHostFixture`** duplicate comment consolidated
- **Doc echoes**: `cake-build-architecture.md` `p4DeferredAllowlist` → `permanentIntegrationsAllowlist`, `phase-x.md` `BuildPaths` → `IPathService`
- **OtoolAnalyzePipeline** "system_artefacts.json" → "manifest.json system_exclusions"
- **Linter regression**: `using SDL2;` restored in `Compile.NetStandard/Probe.cs` (linter incorrectly flagged as unused — the file uses `typeof(SDL)`, `typeof(SDL_image)` etc. as compile-time probes)
- **Probe.cs fix** unstaged change caught; `git add` before commit

## Onboarding Snapshot

This repo is the packaging/build/distribution layer for **modular C# SDL2 bindings first, SDL3 later**, with **native libraries built from source via vcpkg** and shipped through **NuGet**.

Build backbone:

- `.NET 9 / C# 13` (build host); `.NET 10 SDK` for `tests/scripts/*.cs` file-based apps (pinned via `tests/scripts/global.json`)
- `Cake Frosting 6.1.0` build host under `build/_build/` — **ADR-004 5-folder shape complete**: `Host/Features/Shared/Tools/Integrations`. ADR-002 layered shape fully retired.
- `vcpkg` for native builds (custom `*-hybrid` overlay triplets at `vcpkg-overlay-triplets/`)
- `GitHub Actions` for the RID matrix + release pipeline + builder image
- `build/manifest.json` schema v2.1 as the single source of truth
- `NuGet.Protocol 7.3.1` as the in-process feed client (read + write)
- `TUnit 1.33.0 + Microsoft.Testing.Platform` for test running; **515 tests at P3+P4-A close, 0 skipped, 2 named exceptions inline** (ArchitectureTests invariant #3 permanent IPathService allowlist + invariant #3 P4-deferred EnsureVcpkgDependenciesPipeline)
- **11 Pipelines all use `RunAsync(TRequest, CT)`** (ADR-004 §2.11.1 closed); 2 known deferrals with inline tracking comments

Locked decisions still not open for casual re-debate:

- **Hybrid static + dynamic** core packaging model
- **Triplet = strategy** (no standalone `--strategy` CLI flag)
- **Package-first consumer contract** (ADR-001)
- **Cake owns orchestration policy; YAML stays thin**
- **7 RID coverage remains in scope**
- **LGPL-free codec stack** for SDL2_mixer
- **GH Packages = internal CI staging only**; external consumers via nuget.org (PD-7)
- **`local.*` prerelease versions cannot reach staging feed** (`PublishPipeline` guardrail)
- **ADR-004 5-folder shape** (`Host/Features/Shared/Tools/Integrations`) supersedes ADR-002 DDD layering — production code is fully migrated
- **`BuildContext` is invocation state, not a service locator** (ADR-004 §2.11)
- **`Shared/` no Cake dependency** (ADR-004 §2.6; Cake-decoupling closed at Adım 13.1 + 13.3)
- **`Tools/` is Cake `Tool<TSettings>` wrappers ONLY** (ADR-004 §2.10; VcpkgBootstrapTool relocated to Integrations/Vcpkg/ at P4-A)
- **`Integrations/` is non-Cake-Tool external adapters**
- **Pipelines target `RunAsync(TRequest, CT)`** — ADR-004 §2.11.1 migration exception **closed at P4-A**
- **Interface discipline (ADR-004 §2.9)**: 4 removed, 28 retained with criterion labels — **P3 closed**
- **IPathService fluent split permanently discarded** — `IPathService` remains the canonical Host-tier path abstraction
- **`PackageConsumerSmokeTask` takes `IRuntimeProfile` + `IPathService` via DI** (instead of `context.Runtime.Rid` + `context.Paths.PackagesOutput`)

Target RIDs (canonical in `build/manifest.json runtimes[]`):

- `win-x64` / `win-x86` / `win-arm64`
- `linux-x64` / `linux-arm64`
- `osx-x64` / `osx-arm64`

(Note: maintainer's macOS host is Intel `osx-x64`; `osx-arm64` is CI-only until Apple Silicon hardware enters rotation.)

## Current State You Should Assume Until Verified

- **Master HEAD**: `d1127e4` — P3 close + P4-A Pipeline RunAsync cut-over (515/515, MATCH). `git log --oneline -5` should show this as the latest commit.
- **Worktree expectation**: clean (no unstaged changes, no untracked production files). Doc updates from the session-end sweep may be unstaged — commit separately.
- **Build-host tests**: 515 / 515 passed / 0 skipped at P4-A close. ArchitectureTests 5/5 active with 1 permanent named exception inline (invariant #3 IPathService allowlist) + 1 P4-deferred exception (EnsureVcpkgDependenciesPipeline → BuildContext).
- **Behaviour signal (smoke-witness baselines)** at commit `d1127e4`:
  - Win local: `smoke-witness-local-win-x64.json` — MATCH 88.4s
  - Win ci-sim: `smoke-witness-ci-sim-win-x64.json` — MATCH 107.4s
  - WSL Linux local: `smoke-witness-local-linux-x64.json` — MATCH 77.0s
  - WSL Linux ci-sim: 9/9 PASS 82.4s (no committed baseline — first full ci-sim run on Linux)
  - macOS Intel local: `smoke-witness-local-osx-x64.json` — MATCH 122.3s
  - macOS Intel ci-sim: 9/9 PASS 143.2s (no committed baseline — first full ci-sim run on macOS)
- **`ArchitectureTests` invariants**: 5/5 active. Invariant #3 (`Integrations_Should_Have_No_Feature_Dependencies`) carries a `permanentIntegrationsAllowlist` HashSet tolerating 2 IPathService Host-couplings (`DotNetPackInvoker` + `VcpkgCliProvider`). These are **permanent** — do not remove without explicit approval.
- **Phase 2b PD-7 / public release**: **untouched**. Last state remains as captured in `s17-phase-2b-public-release-pickup.prompt.md`.

## Recommended Next Step

### Recommended pickup A — P5 Naming Cleanup (lightweight, high-impact)

P5 is the natural follow-up to P4-A — it's the last mechanical wave and it's well-scoped: 3 renames, each one atomic commit. The build host is in its strongest-ever shape for a rename wave (no skipped tests, all invariants active, full cross-platform baseline coverage). See phase-x §9 for the full inventory.

Pre-flight:
1. Read phase-x §9.2 — the 3 rename targets: `PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`
2. Each rename updates: `[TaskName]` attribute, `smoke-witness.cs` step labels + cake args, `release.yml` `--target` references, `cross-platform-smoke-validation.md` checkpoints, live docs (AGENTS.md, CLAUDE.md, onboarding.md, cake-build-architecture.md)
3. One atomic commit per rename — cake-target-name, smoke-witness labels, CI workflow references, and live-doc mentions all in the same commit
4. `PackageConsumerSmoke` is **unchanged** per ADR-004 §2.14
5. Retire `UnsupportedArtifactSourceResolver` per ADR-004 §2.15

### Recommended pickup B — P4-C Large Pipeline decomposition (optional)

If the ~200 LOC threshold smells feel real, tackle one pipeline at a time. Per-phase-x §8.3: concrete classes, explicit DI, no new interfaces unless §2.9 criteria justify. Per-Pipeline judgment — skip any that read clearly. Candidates: `PackageConsumerSmokePipeline` (685 LOC), `HarvestPipeline` (628 LOC), `PackagePipeline` (554 LOC).

### Recommended pickup C — Phase 2b PD-7 (public release horizon)

Multi-session arc. Re-ground via `s17-phase-2b-public-release-pickup.prompt.md`. Adım 13 + P3 + P4-A close means PD-7 PR review will see architecture invariants green and Pipeline signatures fully modernized.

### If Deniz signals "small interlude"

- **Commit the doc sweep** from the session-end — plan.md, phase-x, cake-build-architecture.md all have pending updates reflecting P3+P4-A CLOSED at `d1127e4`
- **Emit Linux + macOS ci-sim baselines** — the first full ci-sim runs on WSL (82.4s) and macOS (143.2s) completed successfully; the baseline JSON files could be committed to `tests/scripts/baselines/` for future `verify-baselines.cs --milestone` coverage on non-Windows hosts

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `CLAUDE.md`
4. `docs/plan.md` (especially the Phase X wave table — P3 + P4-A are ✅ CLOSED at `d1127e4`)
5. `docs/phases/phase-x-build-host-modernization-2026-05-02.md` (especially §7 P3 retention ledger, §8.2 P4-A checklist, §8.3 P4-C, §9 P5)
6. `docs/decisions/2026-05-02-cake-native-feature-architecture.md` (ADR-004 — especially §2.9 interface criteria, §2.11.1 migration exception closed, §3.5 discarded split)
7. `docs/knowledge-base/cake-build-architecture.md` (post-P4-A shape — Pipelines target `RunAsync(TRequest, CT)`)
8. `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs` (5 active invariants + `permanentIntegrationsAllowlist`)
9. `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` (13 per-feature smokes)
10. `build/_build.Tests/Fixtures/TestHostFixture.cs` (shared DI seam)
11. `build/_build/Program.cs` (16 chained `AddX*()` calls)
12. `build/_build/Host/ServiceCollectionExtensions.cs` + `Integrations/ServiceCollectionExtensions.cs` + `Tools/ServiceCollectionExtensions.cs`
13. `docs/playbook/cross-platform-smoke-validation.md` (cross-platform witness matrix + lingering process mitigation)
14. `tests/scripts/README.md` + `tests/scripts/smoke-witness.cs` + `tests/scripts/verify-baselines.cs`

For pickup C specifically (re-grounding):

- `.github/prompts/s17-phase-2b-public-release-pickup.prompt.md` (Phase 2b state circa 2026-04-29)
- `docs/phases/phase-2-adaptation-plan.md` (PD-7 / PD-8 entries)
- `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (§6 PD-7 framing)

Live-state snapshots:

- `docs/knowledge-base/release-lifecycle-direction.md`
- `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`

Historical archaeology only when needed:

- `.github/prompts/s19-adim13-close-p3-entry-pickup.prompt.md` (Adım 13 close state — this prompt's predecessor)
- `.github/prompts/s20-p4-a-close-code-review.prompt.md` (adversarial review prompt for the P3+P4-A batch)
- `docs/_archive/`

## Locked Policy Recap

These still do not change without explicit Deniz override:

- **Master-direct commits**
- **No commit without approval** (Deniz says "go" / "yap" / "apply" / "proceed" / "başla" — the Approval Gate per AGENTS.md is binding)
- **Cake remains the policy owner; YAML stays thin**
- **`BuildContext` is invocation state, not a service locator** (ADR-004 §2.11; P4-A closed the `RunAsync(BuildContext, TRequest)` interim signature)
- **Pipelines target `RunAsync(TRequest, CT)`** (ADR-004 §2.11.1 closed at P4-A)
- **`PackageConsumerSmokeTask` takes `IRuntimeProfile` + `IPathService` via DI** (P4-A change — these replace `context.Runtime.Rid` + `context.Paths.PackagesOutput`)
- **Pipelines are size-triggered, not convention-triggered** (~200 LOC threshold)
- **`Shared/` no Cake dependency** (ADR-004 §2.6; closed at Adım 13.1 + 13.3)
- **`Tools/` is Cake `Tool<TSettings>` wrappers ONLY** (ADR-004 §2.10; VcpkgBootstrapTool relocated to Integrations/Vcpkg/)
- **`Integrations/` is non-Cake-Tool external adapters**
- **Interface discipline (ADR-004 §2.9)**: keep only if multi-impl (C1), independent-axis (C2), or high-cost test seam (C3)
- **IPathService fluent split discarded** — do not resurrect without Deniz's explicit call
- **Cross-feature data sharing flows through `Shared/`** (ADR-004 invariant #4; one allowlist exception for `Features/LocalDev/`)
- **Lock-file strict mode stays scoped to the build host**
- **GH Packages NuGet always requires PAT auth**
- **External consumer feed = nuget.org** (Phase 2b PD-7)
- **`local.*` prerelease versions cannot reach staging feed**
- **`skipDuplicate=false` on push** — re-push at the same version fails loud
- **Test naming convention**: `<MethodName>_Should_<Verb>_<optional When/If/Given>`
- **Test folders mirror production**: `Unit/Features/<X>/<Y>Tests.cs` asserts `Features/<X>/<Y>.cs`
- **Living docs rule**: if a code change shifts behaviour / topology / infrastructure, update `docs/plan.md` or the active phase doc in the same change
- **Commit message style**: short subject + structured prose body

## Final Steering Note

The P3+P4-A wave closed cleanly in a single atomic commit (`d1127e4`) after adversarial review and full cross-platform verification (3 hosts, 515/515 tests, ci-sim 9/9 on all platforms). Every Pipeline now consumes only its Request DTO; the interface surface is audited and documented; the Tools/Integrations boundary is fully enforced in code; and every stale reference to the discarded fluent split has been excised from the canonical docs.

The natural rhythm for the next session:

- **Default**: pickup A (P5 naming cleanup). Lightweight, well-scoped, mechanical — the build host has never been better positioned for a rename wave.
- **If Deniz signals "decomposition"**: pickup B (P4-C). But the ~200 LOC threshold is a smell signal — if the pipelines read clearly, skip it.
- **If Deniz signals "public release horizon"**: pickup C (PD-7). Re-ground via s17.
- **First TODO regardless**: commit the session-end doc sweep if it wasn't folded into the `d1127e4` commit (plan.md, phase-x, cake-build-architecture.md updates reflecting CLOSED status).
- **All three fork directions wrong**: ask. Don't open with five parallel futures.

The build-host has never been in better shape. Hold the line.
