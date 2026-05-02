---
name: "S19 Adım 13 close + P3 entry pickup (post-cross-tier-cleanup wave)"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the Phase X Adım 13 follow-up wave landed end-to-end on master across 9 atomic sub-step commits + 1 macOS analyzer fix (commits d79daa1 → dfa4ed9, 2026-05-02). Adım 13 closed with all 24 of 26 phase-x §14.2 cross-tier violations resolved (rows 1-11, 14-20); 2 IPathService rows P4-deferred via in-test named exception inline allowlist. ArchitectureTests 5/5 invariants active (3 [Skip] → 0; one named exception). Tests 502 → 515 (+13 ServiceCollectionExtensions smokes); cake-build-architecture.md rewritten to ADR-004 shape; Program.cs DI chain reads as 16 chained AddX*() calls (3 cross-cutting groupings + 13 features). Cross-platform milestone-loop verified across 4 baselines: Win local 95.3s + Win ci-sim 118.8s + WSL Linux 263.6s + macOS Intel 164.4s. P3 (interface review per ADR-004 §2.9) is the canonical next gate; Phase 2b PD-7 (public release) remains the parallel competing track per Deniz's call."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a software engineer entering `janset2d/sdl2-cs-bindings` after the **Phase X Adım 13 follow-up wave landed fully on master** across 9 atomic sub-step commits + a single macOS analyzer fix (commits `d79daa1` → `dfa4ed9` — all 2026-05-02). The repo's build-host has now closed every cross-tier architecture violation that was deferred through P2 (24 of 26 closed; the 2 remaining `IPathService` Host-couplings are documented as a named in-test exception with explicit P4 §8.3 BuildPaths fluent-split deadline). All 5 `ArchitectureTests` invariants are actively asserting; the `ServiceCollectionExtensions` smoke suite (+13 tests) is green; the canonical `cake-build-architecture.md` knowledge-base doc was rewritten to ADR-004 shape; and the milestone-loop `verify-baselines.cs` was verified MATCH on Win local + Win ci-sim + WSL Linux + macOS Intel.

This prompt supersedes `s18-phase-x-p2-close-adim13-pickup.prompt.md` for sessions that start after the Adım 13 close session. Phase 2b PD-7 (public release / Trusted Publishing OIDC / first nuget.org publish) is still in flight as a parallel track — see "Recommended Next Step" for the A vs B fork.

## First Principle

Treat every claim here as **current-as-of-authoring (2026-05-02 — Adım 13 close)** and verify against the live repo, git log, and canonical docs before acting.

The codebase is in its strongest post-Adım-13 state:

- ADR-004 5-folder shape live in production with all cross-tier types correctly homed in `Shared/<X>/` (Harvesting, Coverage, Packaging, Versioning) post Adım 13.1-13.4.
- `ArchitectureTests` 5/5 invariants active (zero `[Skip]` annotations); 1 named exception inline (in-test `p4DeferredAllowlist` HashSet for the 2 IPathService rows, lift at P4 §8.3).
- 515 / 515 passed / 0 skipped.
- Composition root reads as 16 chained `AddX*()` calls per ADR-004 §2.12: `AddHostBuildingBlocks(parsedArgs).AddIntegrations().AddToolWrappers()` + 13 `AddXFeature()`.
- smoke-witness behaviour signal verified across 4 hosts at Adım 13 close: Win local 95.3s, Win ci-sim 118.8s, WSL Linux 263.6s, macOS Intel 164.4s — all MATCH.

That means the highest-value next move is **either**:

1. **P3 — Interface Review (ADR-004 §2.9)** — review every `I*` interface in `build/_build/` against admission criteria (multi-impl OR independent-axis-of-change OR high-cost test seam). Single-impl interfaces backed only by mocks get pruned to `internal sealed class` registered concretely. **Capture P2-close `dotnet test` wall time at session start as the §12.3 P3 gate baseline (target: post-P3 wall time ≤ 1.20× this baseline).** This is the natural next gate Adım 13 just unlocked.
2. **OR — Phase 2b PD-7** (the bigger horizon) — public-release work: Trusted Publishing OIDC setup + `PublishPublicTask` real impl + first prerelease publish to nuget.org (#63) + `playbook/release-recovery.md` (PD-8). Phase X is intentionally non-gating for this — phase-x §1 declares it standalone. Adım 13 close means PD-7 PR review will see the architecture invariants green.

Talk to Deniz before committing to which one. P3 is ~1-2 sessions of focused interface pruning + targeted test-rewrite work. PD-7 is a multi-session arc with research-first cadence.

This repo still runs **master-direct commits** per Deniz direction. Do not create a branch unless there is a concrete reason.

## What Just Happened

This session covered the **Adım 13 post-P2 follow-up wave end-to-end** plus the macOS milestone-loop close. Nine atomic sub-step commits + one macOS analyzer fix, then 4-host milestone-loop verification:

### Adım 13.1 — Shared/Harvesting promote + Cake-decoupling (commit `d79daa1`)

Promoted `BinaryClosure`, `BinaryNode`, `HarvestManifest` cluster (6 records: HarvestManifest, RidHarvestStatus, HarvestStatistics, HarvestSummary, ConsolidationState, DivergentLicense), `PackageInfo`, `PackageInfoResult`, `PackageInfoError`, `HarvestingError` from `Features/Harvesting/` → `Shared/Harvesting/`. Extracted `Unit` struct from `CopierResult.cs` tail → `Shared/Results/Unit.cs` (cross-feature OneOf void-marker per ADR-004 §2.6.1). **Cake-decoupled** the moved value types: `BinaryClosure.PrimaryFiles`, `BinaryNode.Path`, `PackageInfo.OwnedFiles` switched from `Cake.Core.IO.FilePath` to `string` (canonical FullPath form) so the moved types satisfy ArchitectureTests invariant #1's `forbidCakeReferences: true` check. Internal callers (BinaryClosureWalker, ArtifactPlanner, VcpkgCliProvider) keep FilePath fluency by wrapping `new FilePath(node.Path)` at use sites. `HybridStaticValidator` switched from `node.Path.GetFilename().FullPath` (Cake) to `IoPath.GetFileName(node.Path)` (System.IO.Path via alias). Bundled the P2-close 5-doc sweep that established phase-x §14 Adım 13 as canonical (was unstaged at session start). Test count 502 → 502 (the 3 skips still in place at this commit boundary). Win fast-loop verify-baselines.cs MATCH 93.1s.

### Adım 13.2 — Shared/Coverage promote (commit `c305cad`)

5 git mv → `Shared/Coverage/` (CoverageBaseline, CoverageMetrics, CoverageCheckResult, CoverageCheckSuccess, CoverageError + concrete `CoverageThresholdViolation` subclass in same file). Pure mechanical move — no Cake-decoupling needed (none of these types reference Cake). Closes phase-x §14.2 inventory rows 7-8. fast-loop MATCH 99.0s.

### Adım 13.3 — Shared/Packaging promote + G58 placement decision (commit `0ce5f0a`)

9 git mv → `Shared/Packaging/`: `PackagingError` (module base), `DotNetPackResult`, `DotNetPackError`, `ProjectMetadata`, `ProjectMetadataResult`, `ProjectMetadataError`, `G58CrossFamilyCheckModels`, `IG58CrossFamilyDepResolvabilityValidator`, **and `G58CrossFamilyDepResolvabilityValidator` impl** — both interface and impl moved to Shared (matches the IDependencyPolicyValidator + HybridStaticValidator/PureDynamicValidator pattern in Shared/Strategy/) since multi-feature consumption (Preflight + Packaging) satisfies §2.9 criterion 2 and the impl has no Cake deps. Cake-decoupled `DotNetPackError` + `ProjectMetadataError`: `FilePath? ProjectPath` → `string? ProjectPath` per Shared no-Cake invariant. `DotNetPackInvoker` + `ProjectMetadataReader` convert at construction sites (5 callsites). `PackagingError` doc cref to `PreflightError` simplified to `<c>PreflightError</c>` text reference (was `<see cref>` requiring `using Build.Features.Preflight` which would re-introduce a Shared → Features dependency). Closes inventory rows 9-10 (Integrations.DotNet → Features.Packaging) + 16-17 (Features.Preflight → Features.Packaging G58). fast-loop MATCH 103.6s.

### Adım 13.4 — Shared/Versioning promote (commit `bf34387`)

3 git mv → `Shared/Versioning/`: `IUpstreamVersionAlignmentValidator`, `UpstreamVersionAlignmentValidator` impl, `UpstreamVersionAlignmentResult`. Extracted upstream-alignment models out of `Features/Preflight/PreflightValidationModels.cs` into a new `Shared/Versioning/UpstreamVersionAlignmentValidation.cs` file (3 types: enum + 2 records). `UpstreamVersionAlignmentError` base changed from `PreflightError` to `BuildError` directly — Versioning concern shouldn't carry a Preflight chain. `PreflightPipeline.ThrowPreflightFailure` helper signature widened from `PreflightError` to `BuildError` to accommodate (other Preflight error types still flow through it unchanged). Test mirror: `Unit/Features/Preflight/UpstreamVersionAlignmentValidatorTests.cs` → `Unit/Shared/Versioning/UpstreamVersionAlignmentValidatorTests.cs` + namespace update. Multi-impl §2.9-criterion-1 seam (manifest + git-tag + explicit providers all consume it). Closes inventory rows 18-20. fast-loop MATCH 98.0s.

### Adım 13.5 — IPathService Host-coupling P4-deferral (commit `e4d9815`)

Per phase-x §14.5 risk #3 framing, **decision: defer the 2 IPathService violations to P4 §8.3 BuildPaths fluent split** rather than decouple now. Decoupling now would create churn the P4 wave immediately re-touches (P4 dissolves IPathService into per-axis path services). Production code unchanged. Skip message on `Integrations_Should_Have_No_Feature_Dependencies` updated to name the 2 specific remaining violations (`DotNetPackInvoker` + `VcpkgCliProvider`) and cite the lift-deadline. fast-loop MATCH 88.8s.

### Adım 13.6 — Un-skip 3 ArchitectureTests invariants (commit `ba6231a`)

Removed `[Skip]` from invariants #1 (`Shared_Should_Have_No_Outward_Or_Cake_Dependencies`) and #4 (`Features_Should_Not_Cross_Reference_Except_From_LocalDev`) — both genuinely empty post-13.4. Converted invariant #3 from `[Skip]` to phase-x §14.4 inline named-exception pattern: `p4DeferredAllowlist` HashSet inside the test method tolerates the 2 known IPathService rows + asserts `unexpectedViolations.IsEmpty()`. **Bundled bug fix**: `OrchestrationFeatureAllowlist` had a trailing-dot mismatch (`"Build.Features.LocalDev."` vs `sourceType.Namespace = "Build.Features.LocalDev"`) that masked SetupLocalDevFlow as in-scope; trailing dot dropped + allowlist check switched to `ExtractFeatureRoot` comparison so the orchestration-feature exception works against feature-root namespace strings. Test count 502 → 502 (skip → pass within same test methods); skip count 3 → 0. fast-loop MATCH 100.0s.

### Adım 13.7 — TestHostFixture + 13 ServiceCollectionExtensions smokes (commit `a4e5c56`)

Landed `build/_build.Tests/Fixtures/TestHostFixture.cs` static `AddTestHostBuildingBlocks(IServiceCollection, FakeRepoBuilder?)` extension that registers Cake fakes (via `FakeRepoBuilder`) + Host singletons (`IPathService`, `IRuntimeProfile`, `ManifestConfig`, `BuildOptions` + per-axis sub-records, `RuntimeConfig`, `SystemArtefactsConfig`) + Tools/Integrations NSubstitute fakes for 10 interfaces + concrete `VcpkgBootstrapTool`. Plus 13 `[Test]` methods in `Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` named `Add<X>Feature_Should_Register_All_Pipeline_And_Validator_Types` per phase-x §10.6. Each smoke seeds the fixture, invokes one `AddXFeature()` (plus declared cross-feature DI prerequisites for Versioning/Preflight/Harvesting/Packaging/LocalDev), captures the descriptor count, builds the provider, iterates only the descriptors the feature added, asserts each resolves via `provider.GetService(...)` without throwing. Cross-feature DI pre-registrations documented inline:

- `AddPreflightFeature` smoke pre-registers Packaging (PreflightPipeline → IG58CrossFamilyDepResolvabilityValidator).
- `AddVersioningFeature` smoke pre-registers Packaging + Preflight (IPackageVersionProvider closure → IUpstreamVersionAlignmentValidator from Preflight).
- `AddHarvestingFeature` smoke pre-registers Packaging + Preflight (HarvestPipeline → IDependencyPolicyValidator → DependencyPolicyValidatorFactory → IStrategyResolver from Preflight).
- `AddPackagingFeature` smoke pre-registers Preflight + Versioning.
- `AddLocalDevFeature` smoke pre-registers all 6 sibling features.

Test count 502 → 515 (+13 smokes). Phase-x §10.4 P2-close ratchet expectation met retroactively. CA1031 file-top suppression matches existing production-code pattern in HarvestPipeline / ArtifactPlanner. fast-loop MATCH 95.7s.

### Adım 13.8 — cake-build-architecture.md ADR-004 rewrite (commit `2eb9acb`)

Rewrote `docs/knowledge-base/cake-build-architecture.md` end-to-end to match the post-Adım-13 ADR-004 5-folder shape. Key changes:

- Header rewrite: removed "mid-migration status" + "mental mapping for legacy text" paragraphs; added explicit 5-folder enumeration (Host / Features / Shared / Tools / Integrations) with role for each, per-Shared sub-namespace inventory, ArchitectureTests reference + IPathService P4-deferral named exception note.
- "Current Implementation Notes" section rewritten: thin-Task discipline (§2.4), BuildContext invocation-state rule (§2.11), LocalDev orchestration-feature exception (§2.5 + §2.13 invariant #4), per-feature ServiceCollectionExtensions composition root (§2.12), DI smoke pattern via TestHostFixture, OneOf result discipline + Shared/Results/Unit cross-feature void-marker.
- Added "Pipeline / Flow vocabulary" section (new): explicit suffix taxonomy. `*Pipeline` for single-feature slices (15 instances enumerated). `*Flow` for multi-feature orchestration (SetupLocalDevFlow only). `RunAsync(BuildContext, TRequest, CT)` interim signature + P4 cut-over note.
- Architecture tree section: full ADR-002 layered tree replaced with ADR-004 5-folder tree. Each folder enumerated with its sub-namespaces + type list + Adım-13.1-13.4 promotion citations. Tools/Integrations subdirectories listed.
- Added "ArchitectureTests invariants" section (new): all 5 invariants enumerated with semantics + IPathService named-exception inline allowlist pattern.
- Service Architecture (DI) table: `*TaskRunner` → `*Pipeline` rename throughout; `IPackagePipeline` + `IPackageConsumerSmokePipeline` rows added; each row tagged with the layer the impl lives in. New rows for `IPackageOutputValidator`, `IUpstreamVersionAlignmentValidator`, `INuGetFeedClient`, `IProjectMetadataReader`, `IStrategyResolver`. `UnsupportedArtifactSourceResolver` row dropped (retired at P2).
- Production I/O Rule paths updated: `Context/CakeExtensions.cs` → `Host/Cake/CakeJsonExtensions.cs` + `CakeFileSystemExtensions.cs` + `CakePlatformExtensions.cs` per ADR-004 §2.2 (P2 wave kitchen-drawer fix).
- "Adding a New Task" reframed feature-folder-scoped + new "Adding a New Feature" section documents the per-feature ServiceCollectionExtensions + smoke-test convention.

Two intentional historical references retained: line 13 (LayerDependencyTests → ArchitectureTests rename); line 30 (PostFlightTask retired in Slice B2). Doc-only commit; tests 515/515 unchanged. fast-loop MATCH 110.3s.

### Adım 13.9 — AddToolWrappers / AddIntegrations / AddHostBuildingBlocks DI grouping (commit `5cdbe82`)

Optional polish per phase-x §14.3 step 13.9 + ADR-004 §2.12. Created 3 new ServiceCollectionExtensions files:

- `Tools/ServiceCollectionExtensions.cs` — `AddToolWrappers()` (registers `VcpkgBootstrapTool` concrete; sealed class wrapping `bootstrap-vcpkg.bat`/.sh dispatch). Cake `Tool<T>` aliases register themselves through Cake's automatic discovery and don't need DI bindings.
- `Integrations/ServiceCollectionExtensions.cs` — `AddIntegrations()` (9 interface bindings: IPackageInfoProvider → VcpkgCliProvider; ICoberturaReader, ICoverageBaselineReader, IVcpkgManifestReader, IProjectMetadataReader, IDotNetPackInvoker, IDotNetRuntimeEnvironment, INuGetFeedClient, IMsvcDevEnvironment).
- `Host/ServiceCollectionExtensions.cs` — `AddHostBuildingBlocks(ParsedArguments)` (IPathService, IRuntimeProfile, IRuntimeScanner factories + manifest-derived ManifestConfig, RuntimeConfig, SystemArtefactsConfig). Takes ParsedArguments directly because IPathService composes its layout from CLI overrides — same closure-capture pattern as `AddPackagingFeature(string source)`.

`Program.cs ConfigureBuildServices` collapsed: 99 LOC of inline registrations → 16 chained `AddX*()` calls (3 cross-cutting groupings + 13 features). Imports reduced from 6 `Build.Integrations.*` + 4 supporting → `using Build.Integrations` + `using Build.Tools`. fast-loop MATCH 94.7s.

### Mac CA1812 fix + 4-host milestone-loop close (commit `dfa4ed9`)

When running the milestone-loop on macOS Intel (Armut@192.168.50.178) at Adım 13 close, build failed with `CA1812: 'BaselineStep' / 'BaselineSignal' is an internal class that is apparently never instantiated` — both records are deserialized exclusively via `JsonSerializer.Deserialize` (reflection-only construction the analyzer can't see). Pattern matches the prior CA1869 Mac-strict-analyzer mitigation at commit `651ac2f`: added `CA1812` to the file's `#:property NoError` / `NoWarn` directives. No-op on Windows + WSL Linux where CA1812 did not fire for the same source.

After the fix landed, milestone-loop closure across all 4 baselines:

| Host | Mode | Baseline | Result |
|------|------|----------|--------|
| Win | local | smoke-witness-local-win-x64.json | MATCH 95.3s |
| Win | ci-sim | smoke-witness-ci-sim-win-x64.json | MATCH 118.8s (after retry — see "Cross-Platform Testing Learnings #1" below) |
| WSL Linux | local | smoke-witness-local-linux-x64.json | MATCH 263.6s |
| macOS Intel | local | smoke-witness-local-osx-x64.json | MATCH 164.4s |

## Onboarding Snapshot

This repo is the packaging/build/distribution layer for **modular C# SDL2 bindings first, SDL3 later**, with **native libraries built from source via vcpkg** and shipped through **NuGet**.

Build backbone:

- `.NET 9 / C# 13` (build host); `.NET 10 SDK` for `tests/scripts/*.cs` file-based apps (pinned via `tests/scripts/global.json`)
- `Cake Frosting 6.1.0` build host under `build/_build/` — **ADR-004 5-folder shape complete**: `Host/Features/Shared/Tools/Integrations`. ADR-002 layered shape fully retired.
- `vcpkg` for native builds (custom `*-hybrid` overlay triplets at `vcpkg-overlay-triplets/`)
- `GitHub Actions` for the RID matrix + release pipeline + builder image
- `build/manifest.json` schema v2.1 as the single source of truth
- `NuGet.Protocol 7.3.1` as the in-process feed client (read + write)
- `TUnit 1.33.0 + Microsoft.Testing.Platform` for test running; **515 tests at Adım 13 close, 0 skipped, 1 named exception inline (ArchitectureTests invariant #3 IPathService P4-deferral)**

Locked decisions still not open for casual re-debate:

- **Hybrid static + dynamic core** packaging model
- **Triplet = strategy** (no standalone `--strategy` CLI flag)
- **Package-first consumer contract** (ADR-001)
- **Cake owns orchestration policy; YAML stays thin**
- **7 RID coverage remains in scope**
- **LGPL-free codec stack** for SDL2_mixer
- **GH Packages = internal CI staging only**; external consumers via nuget.org (PD-7)
- **`local.*` prerelease versions cannot reach staging feed** (`PublishPipeline` guardrail)
- **ADR-004 5-folder shape** (`Host/Features/Shared/Tools/Integrations`) supersedes ADR-002 DDD layering — production code is fully migrated **and all cross-tier types correctly homed in `Shared/<X>/` per Adım 13**
- **`BuildContext` is invocation state, not a service locator** (ADR-004 §2.11)
- **Pipelines target `RunAsync(BuildContext, TRequest)`** at present (interim); P4 closes the cut-over to `RunAsync(TRequest)` only
- **`Shared/` no Cake dependency** (ADR-004 §2.6; Cake-decoupling closed at Adım 13.1 + 13.3 for the 4 path-typed records)
- **`Tools/` is Cake `Tool<TSettings>` wrappers ONLY** (ADR-004 §2.10; VcpkgBootstrapTool is a sealed concrete with a P1.18 follow-up note flagging Integrations/Vcpkg/ relocation)
- **`Integrations/` is non-Cake-Tool external adapters**

Target RIDs (canonical in `build/manifest.json runtimes[]`):

- `win-x64` / `win-x86` / `win-arm64`
- `linux-x64` / `linux-arm64`
- `osx-x64` / `osx-arm64`

(Note: maintainer's macOS host is Intel `osx-x64`; `osx-arm64` is CI-only until Apple Silicon hardware enters rotation.)

## Current State You Should Assume Until Verified

- **Master HEAD**: most recent commit is `dfa4ed9` (CA1812 fix). `git log --oneline -12` should show the 9-sub-step Adım 13 arc (`d79daa1` → `5cdbe82`) + the CA1812 fix on top. Nothing pending to commit; all 4-host milestone-loop runs verified MATCH.
- **Worktree expectation**: clean (no unstaged changes, no untracked production files).
- **Build-host tests**: 515 / 515 passed / 0 skipped at Adım 13 close. ArchitectureTests 5/5 active with 1 named exception inline (invariant #3, P4 §8.3 deadline).
- **Behaviour signal (smoke-witness baselines)**:
  - `smoke-witness-local-win-x64.json` — fast-loop, MATCH at every Adım 13 sub-step commit boundary on the Windows dev host.
  - `smoke-witness-ci-sim-win-x64.json` — milestone, MATCH at Adım 13 close (118.8s, after manual testhost cleanup retry — see learnings #1).
  - `smoke-witness-local-linux-x64.json` — milestone, MATCH at Adım 13 close (263.6s).
  - `smoke-witness-local-osx-x64.json` — milestone, MATCH at Adım 13 close (164.4s, with CA1812 fix `dfa4ed9` applied).
- **`ArchitectureTests` invariants**: 5/5 active. Invariant #3 (`Integrations_Should_Have_No_Feature_Dependencies`) carries an inline `p4DeferredAllowlist` HashSet tolerating 2 IPathService Host-couplings (`DotNetPackInvoker` + `VcpkgCliProvider`) until P4 §8.3 BuildPaths fluent split. Lift the allowlist entries when P4 lands.
- **Phase 2b PD-7 / public release**: **untouched in this session**. Last state remains as captured in `s17-phase-2b-public-release-pickup.prompt.md` and reiterated in s18: PD-5 closed 2026-04-29, multi-platform `--source=remote` witness on Linux/macOS still pending, PD-7 (Trusted Publishing OIDC + first nuget.org publish) not started.

## Open documentation drift (deliberate; needs closing in next session)

The following two files were **not updated** as Adım 13 sub-steps closed (each sub-step focused on its specific scope and the wave-status updates were deferred):

- **`docs/phases/phase-x-build-host-modernization-2026-05-02.md`** — top-of-doc "Wave-Status Snapshot" table still shows Adım 13 as 🚧 PENDING; §14.4 success-criteria checkboxes are still unchecked. **First TODO for the next session**: tick the checkboxes (all 9 satisfied + the optional 13.9 polish landed) + flip the wave row to ✅ CLOSED with the 9-commit arc cited.
- **`docs/plan.md`** — Phase X 5-row wave table still shows Adım 13 as 🚧 PENDING and the "Last updated" line still reads 2026-05-02 from the P2-close session. Update the row + bump last-updated to whatever date the next session lands.

Both are doc-only; can land as a single small commit at session start or be folded into the first P3 commit.

## Cross-Platform Testing Learnings (session-derived; not yet folded into docs)

The Adım 13 close milestone-loop drove a tight 4-host verification sequence (Win local + Win ci-sim + WSL Linux + macOS Intel). Two new gotchas surfaced beyond the s18-captured set; **fold these into the docs in a follow-up session**:

1. **Mid-script intra-mode testhost flake (Windows only — distinct from s18 #1).** s18's testhost flake recipe handles the BEFORE-`verify-baselines` ritual (kill processes >1min old). But Adım 13 close hit a **mid-script** variant: when `verify-baselines.cs --milestone` runs `local` (mode that ends with `dotnet test` per TFM via PackageConsumerSmoke) and then ci-sim back-to-back on the same host, the local-mode `testhost.exe` survives between iterations and locks `Microsoft.Testing.Platform.dll` for ci-sim's first step (`CleanArtifacts`). Workaround on 2026-05-02 was an aggressive manual cleanup (`pwsh -Command "dotnet build-server shutdown; Get-Process dotnet, testhost | Stop-Process -Force; Start-Sleep -Seconds 5"`) between the two runs; retry then matched. **Scheduled fix** for 2026-05-09: routine `verify-baselines-intra-mode-cleanup-pr` (id `trig_01RnZpZ4vLjDMTqZDspSzysQ`) opens a small PR that inserts a host-conditional cleanup step BETWEEN BuildEntries iterations in `verify-baselines.cs` itself. Until then, document the manual workaround in `docs/playbook/cross-platform-smoke-validation.md` Failure Triage category 4 + cite this prompt.
2. **macOS analyzer second pattern: CA1812 on JsonSerializer-only types.** `BaselineStep` and `BaselineSignal` records in `verify-baselines.cs` are constructed only via `JsonSerializer.Deserialize` (reflection); macOS's stricter analyzer evaluator surfaces CA1812 ("internal class apparently never instantiated") while Windows + Linux do not. Same Mac-strict-analyzer pattern as the CA1869 mitigation at `651ac2f` (s18 §learning 4); fix landed at `dfa4ed9` by adding `CA1812` to the file's `#:property NoError` / `NoWarn` directive. **Pattern reinforced**: when a `.cs` script-tier file fails on Mac with an analyzer rule that doesn't fire on other platforms, the fix is the file's `#:property NoError` directive, not changing global analyzer config — and the most likely culprits are reflection-only constructed types (CA1812) or service-pattern reuse warnings (CA1869).
3. **WSL + Mac SSH non-interactive shell PATH (new — extending s18 #2).** `wsl zsh -c '…'` and `ssh Armut@<host> '…'` non-interactive non-login shells do NOT load `.zshrc` / `.bashrc`, so `dotnet` is missing from PATH even when interactive shells have it. Fix: use `wsl zsh -lc '…'` (login shell loads profile) on WSL; explicit `export PATH="/usr/local/share/dotnet:$PATH"` (or absolute `/usr/local/share/dotnet/dotnet`) in the SSH command on Mac. Captured during the Adım 13 close milestone-loop after the first run failed with `command not found: dotnet`.
4. **Repo paths on remote hosts**:
   - WSL: `/home/deniz/repos/sdl2-cs-bindings`
   - macOS Intel: `~/repos/sdl2-cs-bindings` (resolves to `/Users/armut/repos/sdl2-cs-bindings`)
5. **Schedule remote-agent constraint**: the Claude GitHub App is **NOT installed** on `janset2d/sdl2-cs-bindings` as of session end. Scheduled remote agents (e.g., `verify-baselines-intra-mode-cleanup-pr`) cannot push branches or open PRs via the API. Agent prompts must instruct the agent to output a unified diff in its final message instead of attempting `git push` / PR creation. Install the App at `https://claude.ai/code/onboarding?magic=github-app-setup` if PR-opening agents become a recurring need.

## What Changed In Canonical Docs

(See Adım 13.8 commit `2eb9acb` for the full rewrite.)

- **`docs/knowledge-base/cake-build-architecture.md`** — fully rewritten to ADR-004 5-folder shape (header, Architecture tree, Service DI table, Pipeline/Flow vocabulary, ArchitectureTests invariants). All ADR-002 layered-tree references retired except 2 historical mentions (LayerDependencyTests rename + PostFlightTask Slice B2 retirement).

**Not yet updated** (deliberate doc-drift; first TODOs of next session):

- `docs/phases/phase-x-build-host-modernization-2026-05-02.md` — Wave-Status Snapshot + §14.4 success criteria.
- `docs/plan.md` — Phase X wave table + Last-updated line.

`AGENTS.md` / `CLAUDE.md` / `docs/onboarding.md` were re-checked at the P2 close (per s18 §"Not yet updated") and already point at ADR-004 from prior batches; no further audit edits were observed needed during Adım 13.

## Recommended Next Step

### Recommended pickup A — P3 Interface Review (ADR-004 §2.9)

**The natural gate Adım 13 just unlocked.** Pre-flight:

1. **Capture P2-close `dotnet test` wall-time baseline** at session start — phase-x §12.3 P3 gate is "total `dotnet test` wall time ≤ (P2 close wall time × 1.20)". Run `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo` 3 times back-to-back, take median wall-time, record in a session-local note. Adım 13 added 13 smokes (test count 515) so the absolute baseline is post-Adım-13, not pre. Decide whether to anchor against pre-Adım-13 (502 tests) or post-Adım-13 (515 tests). Likely: post-Adım-13 since smokes are cheap.
2. **Read ADR-004 §2.9 admission criteria** in full. The three criteria for keeping an interface:
   - Multiple production implementations exist today.
   - Formalizes an independent axis of change.
   - Backs a high-cost test seam (transitional debt — flagged for P3 review).
3. **Inventory every `I*` type in `build/_build/`**. Categorize per criterion. Single-impl-only-mocked interfaces are pruning candidates.

Likely pruning candidates (informed guesses; verify against current code):

- **`IBinaryClosureWalker` / `IArtifactPlanner` / `IArtifactDeployer`** (Features/Harvesting) — all single-impl. Backed by mocks in tests. Candidates for prune to `internal sealed class` + concrete DI registration, with tests rewritten as fixture-based concrete tests using `FakeRepoBuilder` + `TestHostFixture`.
- **`ICoverageThresholdValidator`** (Features/Coverage) — single-impl, mock-backed.
- **`IPackageOutputValidator`** (Features/Packaging) — single-impl.
- **`INativePackageMetadataGenerator` / `IReadmeMappingTableGenerator`** (Features/Packaging) — single-impl.
- **`IPackagePipeline` / `IPackageConsumerSmokePipeline`** (Features/Packaging) — single-impl. **But careful**: `IPackagePipeline` is consumed by `SetupLocalDevFlow` as a cross-feature seam (LocalDev allowlist exception). The interface might be retained as an indirection seam that LocalDev consumes through, vs the concrete which is the production binding. Decide per §2.9 criterion 2 ("independent axis of change").
- **`ICoberturaReader` / `ICoverageBaselineReader`** (Integrations/Coverage) — single-impl, but Integrations adapter pattern often retains interfaces for swap-in-substitute test hygiene; lean toward retain.
- **`IRuntimeScanner`** — multi-impl (WindowsDumpbinScanner / LinuxLddScanner / MacOtoolScanner). **RETAIN** per criterion 1.
- **`IDependencyPolicyValidator` / `IPackagingStrategy`** (Shared/Strategy) — multi-impl (HybridStatic / PureDynamic). **RETAIN** per criterion 1.
- **`IPackageVersionProvider`** (Features/Versioning) — multi-impl (Manifest / Explicit / GitTag). **RETAIN** per criterion 1.
- **`IArtifactSourceResolver`** (Features/Packaging) — multi-impl (Local / Remote). **RETAIN** per criterion 1.

Per phase-x §7.4 + §12.3, P3 close criteria:

- All `I*` interfaces audited; pruned-or-retained decisions documented in commit messages or a phase-x §7 audit table.
- Test count drops (from interface removal + test rewrite consolidation) require commit-message gerekçe per removed test method.
- Total `dotnet test` wall time ≤ 1.20× the captured baseline.
- fast-loop verify-baselines.cs MATCH at every P3 commit; milestone-loop at P3 close.

Each interface decision is its own atomic commit. Bundle related ones (e.g., 3 Harvesting interfaces in one commit) when the test rewrites cluster.

### Recommended pickup B — Phase 2b PD-7 (public release horizon)

Genuinely a multi-session arc. Carry-over from `s17-phase-2b-public-release-pickup.prompt.md` (still authoritative for PD-7 framing; s18 + s19 only added cross-platform learnings, not PD-7 progress):

1. **Multi-platform `--source=remote` witness pre-PD-7.** Run `tests/scripts/smoke-witness.cs remote --verbose` on WSL Linux + macOS Intel. Mac SSH liveness probe first (recipe in s18 §learning 2 + cross-platform-smoke-validation.md "Host Liveness Pre-flight").
2. **Decide promotion-path mechanism** — `Promote-To-Public.yml` separate workflow, OR a stage on `release.yml` itself, OR a meta-tag-driven full-train workflow per PD-7. ADR-003 §6 has the framing.
3. **Trusted Publishing (OIDC) for nuget.org** — GitHub Actions OIDC token → nuget.org trust relationship → keyless push. nuget.org docs + Andrew Lock 2025 article.
4. **`PublishPublicTask` real impl** — Cake-side counterpart. Likely a separate `*Pipeline` (per ADR-004 §2.10 naming) from the existing `PublishPipeline` because the auth model differs (OIDC vs PAT) and the source feed differs (read from staging, push to public).
5. **`docs/playbook/release-recovery.md`** drafted (PD-8). Unhappy-path runbook.
6. **First prerelease publish** to nuget.org (#63).

Adım 13 close means PD-7 PR review will see the architecture invariants green — that's a real benefit if Deniz wants to start opening external PRs.

### Optional: VcpkgBootstrapTool relocation (small targeted fix)

Per the user's persistent memory note, `VcpkgBootstrapTool` (sealed concrete in `Tools/Vcpkg/`) is a pending P1.18 follow-up: it should move to `Integrations/Vcpkg/` because it is NOT a Cake `Tool<TSettings>` (sealed concrete instead). ADR-004 §2.7 needs an inline update in the same commit. Phase-x §2.7 explicitly flags the candidacy. Could be picked up as a 30-minute interlude before P3 starts — tiny scope, low risk, removes a known irritant.

If Deniz says "go big", pickup B; if "iterate the architecture wave", pickup A. **Pickup B can interleave with pickup A** — Phase X is non-gating for Phase 2b release work. But scope-discipline still suggests finishing P3 first because P3 closes the open architecture concerns that P4/P5 then build on.

## Recommended Workflow For The Next Agent

1. Read the mandatory grounding below in order.
2. Run `git log --oneline -15` and confirm the 9-sub-step Adım 13 arc (`d79daa1` → `5cdbe82`) + CA1812 fix `dfa4ed9` is on master HEAD.
3. Run `git status` to confirm worktree is clean (no unstaged files).
4. Run `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo` and confirm 515 / 515 passed / 0 skipped. Take median wall-time across 3 runs as the P3 §12.3 baseline.
5. Run the pre-`verify-baselines.cs` ritual (cross-platform-smoke-validation.md), then `cd tests/scripts && dotnet run verify-baselines.cs`. Expect MATCH on `smoke-witness-local-win-x64.json`.
6. **Ask Deniz which doc-cleanup approach** to use for the Adım 13 close: standalone docs commit at session start (recommended — small, fast, isolated) vs fold into first P3 commit. Then close phase-x §14.4 + plan.md per "Open documentation drift" above.
7. Branch by which pickup Deniz signals:

#### If pickup A (P3 Interface Review)

- Read ADR-004 §2.9 in full — admission criteria + §2.9.1 delegate-hook patterns for tests that must keep a seam.
- Inventory `I*` types: `grep -rn "public interface I" build/_build/` (or use Glob/Grep tool). Classify per §2.9 criteria.
- For each pruning candidate: identify production callsites + test consumers; plan the commit (interface deletion + concrete DI re-registration + test rewrite).
- Each interface or interface-cluster decision is its own atomic commit. fast-loop verify-baselines.cs at every commit; milestone-loop at P3 close.
- Track wall-time after every commit if you suspect drift; the §12.3 1.20× ceiling needs awareness, not micro-optimization.

#### If pickup B (Phase 2b PD-7)

- Re-read `s17-phase-2b-public-release-pickup.prompt.md` "Recommended pickup B" section — most of the framing carries over.
- Research-first pass on Trusted Publishing OIDC for nuget.org (Andrew Lock 2025 article + nuget.org docs).
- Sketch the workflow / `*Pipeline` shape with Deniz before writing code.
- Adım 13 didn't touch this track — start by running multi-platform `--source=remote` witnesses on Linux + macOS to confirm the existing `RemoteArtifactSourceResolver` still works post-Adım-13 type promotions (high confidence yes — Adım 13 was pure structure, not behavior — but verify).

## If Pickup A Lands Cleanly

Move to **P4 — API Surface Refactors** (per phase-x §8). P4 scope:

- Pipeline `RunAsync(BuildContext, TRequest, CT)` → `RunAsync(TRequest, CT)` cut-over. Closes the §2.11.1 migration exception. Tasks gain `Request.From(context, config)` factory call sites; pipelines no longer take BuildContext per-call.
- `IPathService` fluent split into `BuildPaths.Harvest` / `.Packages` / `.Smoke` / `.Vcpkg`. **Closes the ArchitectureTests invariant #3 named exception** (P4 §8.3 deadline). Hundreds of callsite rewrites: `paths.GetHarvestStageNativeDir(lib, rid)` → `paths.Harvest.GetStageNativeDir(lib, rid)`.
- Optional internal refactor of large Pipelines (PackageConsumerSmoke 688 LOC, Harvest 628, Package 556) into smaller per-concern co-located helpers if the §2.4 size threshold smells get worse during the cut-over.

P4 is the largest signature-evolution wave. Phase-x §8 has the inventory. After P4 closes, **P5** is mechanical naming cleanup (`PreFlightCheck` → `Preflight`, `Coverage-Check` → `CoverageCheck`, `Inspect-HarvestedDependencies` → `InspectHarvestedDependencies`) atomic across Cake host + smoke-witness + release.yml + docs in one commit per rename.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `CLAUDE.md`
4. `docs/plan.md` (especially the Phase X 5-row wave table — flip Adım 13 to ✅ CLOSED in your first commit)
5. `docs/phases/phase-x-build-host-modernization-2026-05-02.md` (especially §7 P3, §8 P4, §9 P5; tick §14.4 success criteria + Wave-Status Snapshot in your first commit)
6. `docs/decisions/2026-05-02-cake-native-feature-architecture.md` (governing ADR-004 — especially §2.9 interface admission criteria + §2.13 invariant rule set)
7. `docs/knowledge-base/cake-build-architecture.md` (rewritten at Adım 13.8 — current shape-of-truth for the build host)
8. `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs` (5 active invariants + invariant #3's `p4DeferredAllowlist` named exception inline)
9. `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` (the 13 per-feature smoke tests — pattern reference for new feature work)
10. `build/_build.Tests/Fixtures/TestHostFixture.cs` (shared DI seam for all smokes — extend if your P3 work needs new fakes)
11. `build/_build/Program.cs` (post-13.9 shape: 16 chained `AddX*()` calls; 3 cross-cutting groupings + 13 features)
12. `build/_build/Host/ServiceCollectionExtensions.cs` + `Integrations/ServiceCollectionExtensions.cs` + `Tools/ServiceCollectionExtensions.cs` (the 3 cross-cutting groupings landed at 13.9)
13. `docs/playbook/cross-platform-smoke-validation.md` (especially Lingering process mitigation + Host Liveness Pre-flight + Failure Triage; will gain category 5 once §learning 1 above gets folded in)
14. `tests/scripts/README.md` + `tests/scripts/smoke-witness.cs` + `tests/scripts/verify-baselines.cs` (the witness loop)

For pickup B specifically (re-grounding, items 15–18):

- `.github/prompts/s17-phase-2b-public-release-pickup.prompt.md` (Phase 2b state circa 2026-04-29; still authoritative for PD-7 framing)
- `docs/phases/phase-2-adaptation-plan.md` (PD-7 / PD-8 entries)
- `docs/decisions/2026-04-20-release-lifecycle-orchestration.md` (§6 PD-7 framing)
- `.github/workflows/release.yml`

Live-state snapshots:

- `docs/knowledge-base/release-lifecycle-direction.md`
- `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`

Historical archaeology only when needed:

- `.github/prompts/s18-phase-x-p2-close-adim13-pickup.prompt.md` (Adım 13 entry conditions; this prompt's predecessor — useful only if reasoning about why Adım 13 looked like X going in vs Y coming out)
- `.github/prompts/s17-phase-2b-public-release-pickup.prompt.md` (Phase 2b state pre-Adım-13)
- `docs/_archive/`

External references for pickup B:

- nuget.org Trusted Publishing docs (NuGet team blog 2024/2025).
- Andrew Lock — "Easily publishing NuGet packages from GitHub Actions with Trusted Publishing" (2025).

Scheduled routines (live):

- `trig_01RnZpZ4vLjDMTqZDspSzysQ` — `verify-baselines-intra-mode-cleanup-pr`. One-time at 2026-05-09T09:00Z (Saturday 12:00 Istanbul). Drafts a unified-diff PR for the intra-mode testhost cleanup gap surfaced in §learning 1 above. Output goes to remote-agent final message (Claude GitHub App not installed → no API PR creation); manual apply by maintainer. Manage at https://claude.ai/code/routines/trig_01RnZpZ4vLjDMTqZDspSzysQ.

## Locked Policy Recap

These still do not change without explicit Deniz override:

- **Master-direct commits**
- **No commit without approval** (Deniz says "go" / "yap" / "apply" / "proceed" / "başla" — the Approval Gate per AGENTS.md is binding). Adım 13 explicit per-commit approval was relaxed to "iterate test commit through P3" mode after 13.1 — confirm with Deniz whether that mode persists into P3 or reverts to per-commit gate.
- **Cake remains the policy owner; YAML stays thin**
- **`BuildContext` is invocation state, not a service locator** (ADR-004 §2.11; P4 closes the `RunAsync(BuildContext, TRequest)` interim signature)
- **Pipelines are size-triggered, not convention-triggered** (~200 LOC threshold)
- **`Shared/` no Cake dependency** (ADR-004 §2.6; closed at Adım 13.1 + 13.3 for the path-typed records)
- **`Tools/` is Cake `Tool<TSettings>` wrappers ONLY** (ADR-004 §2.10; VcpkgBootstrapTool relocation to Integrations/Vcpkg/ is a pending P1.18 follow-up — see "Optional" pickup above)
- **`Integrations/` is non-Cake-Tool external adapters**
- **Cross-feature data sharing flows through `Shared/`** (ADR-004 invariant #4; one allowlist exception for `Features/LocalDev/`)
- **Lock-file strict mode stays scoped to the build host**
- **GH Packages NuGet always requires PAT auth**
- **External consumer feed = nuget.org** (Phase 2b PD-7); GH Packages stays internal CI staging only.
- **`local.*` prerelease versions cannot reach staging feed** — `PublishPipeline` guardrail enforces this.
- **`skipDuplicate=false` on push** — re-push at the same version fails loud.
- **Test naming convention**: `<MethodName>_Should_<Verb>_<optional When/If/Given>` (PascalCase method name + underscores between every other word segment + `Should` always present)
- **Test folders mirror production**: `Unit/Features/Packaging/PackagePipelineTests.cs` asserts the contract of `Features/Packaging/PackagePipeline.cs`. Adım 13.4 moved `Unit/Features/Preflight/UpstreamVersionAlignmentValidatorTests.cs` → `Unit/Shared/Versioning/UpstreamVersionAlignmentValidatorTests.cs` per this convention.
- **Living docs rule**: if a code change shifts behaviour / topology / infrastructure, update `docs/plan.md` or the active phase doc in the same change. Adım 13 sub-steps deferred wave-status updates explicitly — close that gap in the next session's first commit.
- **Commit message style**: short subject + structured prose body (Adım 13 commits ran 4-8 sub-bullets per section, not strict 4-8 total). Match the existing repo style — see `git log` for the Adım 13 arc as the active reference.

## Final Steering Note

The Adım 13 wave closed cleanly across 9 atomic commits + 1 macOS analyzer fix + 4-host milestone-loop verification. Every architecture invariant is actively asserting; the cross-tier violation backlog from P2 close is emptied (24 of 26 directly closed; 2 documented as a deadline-tracked named exception). The ServiceCollectionExtensions smoke suite catches DI graph regressions at CI gate time. The canonical `cake-build-architecture.md` knowledge-base doc is now an honest reflection of the live shape.

The natural rhythm for the next session:

- **Default**: pickup A (P3 interface review). Phase-x §7 + §12.3 are the canonical pre-flight; ADR-004 §2.9 is the substantive admission criteria. Adım 13 just unlocked this gate — interface decisions taken on a green architecture are cleaner than ones taken under a skipped invariant.
- **First TODO regardless**: close the documentation drift (phase-x §14.4 + plan.md Phase X table) as a small standalone commit at session start. Adım 13 deserves a clean ✅ in the canonical state.
- **If Deniz signals "public release horizon"**: pickup B (PD-7). Re-ground via s17. The P4-deferred IPathService named exception will not block PD-7 PR review (it's documented in-test).
- **If Deniz signals "small interlude"**: VcpkgBootstrapTool relocation to Integrations/Vcpkg/ — 30-minute scope, P1.18 follow-up that pre-dates Adım 13.
- **Both fork directions wrong**: ask. Don't open with five parallel futures.

The build-host has never been in better shape. Hold the line.
