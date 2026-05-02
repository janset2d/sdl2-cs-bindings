---
name: "S20 P4-A close — rigorous code review"
description: "Requests a cold, adversarial code review of the P3-close + P4-A batch landed in the working tree. Agent should treat every diff as suspect and flag regressions, missed patterns, test gaps, and architectural drift."
argument-hint: "Optional focus area (e.g., 'only review Harvesting changes', 'focus on test quality')"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are a senior software engineer doing a **cold, adversarial code review** of a batch of changes on `janset2d/sdl2-cs-bindings` master. The working tree contains uncommitted changes spanning three logical units of work. Your job is to find problems — regressions, missed patterns, test gaps, architectural drift, inconsistent naming, anything that would fail a rigorous PR review.

Assume nothing. Verify everything against the code.

## What happened in this batch

The working tree carries **3 stacked waves** executed sequentially on 2026-05-02:

### Wave 1 — P3 Interface Review close (ADR-004 §2.9)

32 production `I*` interfaces were audited. Four stateless/mock-only interfaces were removed; 28 retained with explicit criterion labels.

**Removed interfaces:**
- `ICoverageThresholdValidator` → `static class CoverageThresholdValidator` (pure stateless pass/fail rule)
- `IVersionConsistencyValidator` → `static class VersionConsistencyValidator` (pure stateless manifest/vcpkg comparison)
- `ICoreLibraryIdentityValidator` → `static class CoreLibraryIdentityValidator` (pure stateless manifest invariant)
- `IStrategyCoherenceValidator` → concrete `sealed class StrategyCoherenceValidator` kept in DI (has real `IStrategyResolver` dependency, but the interface added no independent seam)

**Files touched (production):**
- `Features/Coverage/ICoverageThresholdValidator.cs` — deleted
- `Features/Coverage/CoverageThresholdValidator.cs` — `sealed class : I*` → `static class` with `static` method
- `Features/Coverage/CoverageCheckPipeline.cs` — constructor drops `ICoverageThresholdValidator`; calls static `CoverageThresholdValidator.Validate()`
- `Features/Coverage/ServiceCollectionExtensions.cs` — DI registration removed
- `Features/Preflight/ICoreLibraryIdentityValidator.cs` — deleted
- `Features/Preflight/IStrategyCoherenceValidator.cs` — deleted
- `Features/Preflight/IVersionConsistencyValidator.cs` — deleted
- `Features/Preflight/CoreLibraryIdentityValidator.cs` → `static class`
- `Features/Preflight/VersionConsistencyValidator.cs` → `static class`
- `Features/Preflight/StrategyCoherenceValidator.cs` — `: IStrategyCoherenceValidator` removed from class decl
- `Features/Preflight/PreflightPipeline.cs` — constructor takes concrete `StrategyCoherenceValidator`; calls `VersionConsistencyValidator.Validate()` and `CoreLibraryIdentityValidator.Validate()` statically
- `Features/Preflight/ServiceCollectionExtensions.cs` — 3 interface registrations removed; `StrategyCoherenceValidator` concrete added

**Test files touched:**
- `ProgramCompositionRootTests.cs` — removed assertions for deleted interface types
- `CoverageCheckTaskRunTests.cs` — constructor no longer passes `ICoverageThresholdValidator`
- `CoverageThresholdValidatorTests.cs` — `CreateValidator()` helper removed; all calls switched to static `CoverageThresholdValidator.Validate(...)`
- `SetupLocalDevFlowTests.cs` — 3 NSubstitute mocks removed; uses concrete `StrategyCoherenceValidator(new StrategyResolver())`
- `CoreLibraryIdentityValidatorTests.cs` — calls switched to static `CoreLibraryIdentityValidator.Validate(...)`
- `PreFlightCheckTaskRunTests.cs` — 2 concrete validators removed from `CreateTask`; kept `StrategyCoherenceValidator`
- `VersionConsistencyValidatorTests.cs` — calls switched to static `VersionConsistencyValidator.Validate(...)`

**Docs touched:** `phase-x-build-host-modernization-2026-05-02.md` §7 rewritten (removal ledger + retention table + checked criteria), `plan.md`, `AGENTS.md`, `CLAUDE.md`, `cake-build-architecture.md`, `onboarding.md`, `phase-2-adaptation-plan.md`.

### Wave 2 — VcpkgBootstrapTool relocation + IPathService fluent split discarded

**VcpkgBootstrapTool** moved from `Tools/Vcpkg/` to `Integrations/Vcpkg/` (it is NOT a Cake `Tool<TSettings>` — sealed concrete wrapping `bootstrap-vcpkg.bat`/.sh). Registration moved from `Tools/ServiceCollectionExtensions.AddToolWrappers()` to `Integrations/ServiceCollectionExtensions.AddIntegrations()`. `AddToolWrappers()` is now a clean no-op body. ADR-004 §2.7 + §2.8 trees updated.

**IPathService fluent split discarded.** Every canonical doc that referenced "BuildPaths fluent split" or "P4 §8.3 IPathService split" was updated to reflect the permanent decision. The ArchitectureTests invariant #3 allowlist entries (`DotNetPackInvoker` + `VcpkgCliProvider → IPathService`) are now permanent named exceptions, not P4-deferred. Docs updated: `ArchitectureTests.cs` comment, `phase-x-*.md` §8.3 removed + §8.4→§8.3 renumbered, `ADR-004` §3.5 rewritten, `cake-build-architecture.md`, `plan.md`.

### Wave 3 — P4-A Pipeline `RunAsync(BuildContext, TRequest)` → `RunAsync(TRequest, CT)` cut-over

Every Pipeline in the build host no longer accepts `BuildContext` in `RunAsync()`. This closes ADR-004 §2.11.1 migration exception.

**10 pipelines + 2 interfaces changed:**

| Pipeline | Signature change | Key mechanical detail |
|---|---|---|
| `InfoPipeline` | `RunAsync(BuildContext)` → `RunAsync()` | Added `ICakeContext`, `ICakeLog` to ctor |
| `InspectHarvestedDependenciesPipeline` | `RunAsync(BuildContext)` → `RunAsync()` | Added `VcpkgConfiguration` to ctor |
| `OtoolAnalyzePipeline` | `RunAsync(BuildContext)` → `RunAsync()` | Added `ICakeContext`, `ICakeLog`, `IPathService`, `DumpbinConfiguration` to ctor |
| `ConsolidateHarvestPipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | Added `ICakeContext`, `ICakeLog`, `IPathService` to ctor; all static helpers → instance methods |
| `HarvestPipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | Added `ICakeContext`, `ICakeLog`, `IPathService` to ctor; all private method sigs updated |
| `NativeSmokePipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | Added `VcpkgConfiguration` to ctor; `ResolveLibrariesToValidate` signature changed |
| `PublishPipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | Already had all deps — just removed `BuildContext` param |
| `PackagePipeline` (+ `IPackagePipeline`) | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | Already had all deps; fixed `DefaultResolveHeadCommitSha` static method |
| `PackageConsumerSmokePipeline` (+ `IPackageConsumerSmokePipeline`) | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | Already had all deps — zero body changes |
| `PreflightPipeline` | `RunAsync(BuildContext, TReq, CT)` → `RunAsync(TReq, CT)` | Added `ICakeContext`, `ICakeLog`, `IPathService` to ctor; `EnsurePreflightInputsReady` → instance method |

**15 Cake Tasks** updated — all now call `pipeline.RunAsync(request)` instead of `pipeline.RunAsync(context, request)`.

**`PackageConsumerSmokeTask`** gained `IRuntimeProfile` + `IPathService` ctor params (replaces `context.Runtime.Rid` + `context.Paths.PackagesOutput`).

**Interfaces updated:** `IPackagePipeline.RunAsync` and `IPackageConsumerSmokePipeline.RunAsync` signatures changed (BuildContext removed).

**SetupLocalDevFlow** had its sub-pipeline calls updated (`_preflightPipeline`, `_consolidateHarvestPipeline`, `_harvestPipeline`, `_packagePipeline`) but still takes `BuildContext` itself — it needs it for `_ensureVcpkgDependenciesPipeline.Run(context)` which wasn't part of this wave.

**Test files touched (Wave 3):**
- `ConsolidateHarvestTests.cs` — `CreatePipeline(repo)` helper added; all `new ConsolidateHarvestPipeline()` → `CreatePipeline(repo)`
- `HarvestTaskTests.cs` — `CreateHarvestTask` helper gained `repo` param; all 6 callers updated; using `Cake.Testing` added
- `SetupLocalDevFlowTests.cs` — HarvestPipeline ctor + ConsolidateHarvestPipeline ctor + NSubstitute mock sigs updated
- `PackagePipelineTests.cs` — `repo.BuildContext, ` removed from all RunAsync calls
- `PublishPipelineTests.cs` — same
- `PackageConsumerSmokePipelineTests.cs` — same + unused `repo` variables removed
- `PackageConsumerSmokeTaskTests.cs` — `IRuntimeProfile` + `IPathService` params added to ctor calls
- `NativeSmokePipelineTests.cs` — `VcpkgConfiguration` added to ctor + `repo.BuildContext, ` removed
- `PreFlightCheckTaskRunTests.cs` — updated in Wave 1
- `InspectHarvestedDependenciesPipelineTests.cs` — updated in Wave 3 (P4-A.1)

## What should NOT have changed

- **Cake Task `RunAsync(BuildContext)` signature** — this is the Frosting contract, not touched
- **`SetupLocalDevFlow.RunAsync(BuildContext, CT)`** — still takes BuildContext (deferred — needs Vcpkg pipeline cut-over first)
- **`ArtifactSourceResolver` signatures** — `PrepareFeedAsync(BuildContext, ...)` unchanged (out of scope)
- **Behaviour** — all 515 tests pass, 0 skipped

## Review instructions

Do a cold read of the working tree diff. For every file changed, ask:

### Architecture & coupling
1. Does any Pipeline still receive `BuildContext` in `RunAsync()` that shouldn't?
2. Are constructor dependencies correctly narrowed (e.g., `ICakeContext` not `BuildContext`, `ICakeLog` not `context.Log`)?
3. Are the 4 removed interfaces genuinely stateless/mock-only — or did any of them have a hidden production multi-impl future?
4. Does `ConsolidateHarvestPipeline`'s conversion from all-static-helpers to instance methods leave any stale `static` keyword on methods that now use instance fields?
5. Are the `IPathService` allowlist entries in `ArchitectureTests.cs` correctly documented as permanent?

### Test quality
6. Did any test lose meaningful coverage? (Check that assertions weren't silently dropped when mocks were removed.)
7. Are the NSubstitute-based tests (`SetupLocalDevFlowTests`, `HarvestTaskTests`) still exercising real logic, or did the mock-heavy setup become a tautology after the cut-over?
8. Did `PackageConsumerSmokeTaskTests` gain meaningful `IRuntimeProfile` + `IPathService` values, or are they passing mocks that hide regressions?
9. Are there tests that should exist but don't? (E.g., no tests for `InfoPipeline.RunAsync()` with the new `ICakeContext` — was that coverage already missing?)

### Mechanical correctness
10. Does `HarvestPipeline.EnsureHarvestInputsReady()` call `_pathService.GetVcpkgInstalledTripletDir()` on the correct profile? (Verify `_runtimeProfile` is used, not a hardcoded value.)
11. Did `NativeSmokePipeline.ResolveLibrariesToValidate()` correctly switch from `context.Options.Vcpkg.Libraries` to `_vcpkgConfiguration.Libraries`?
12. Does `PackagePipeline.DefaultResolveHeadCommitSha` still correctly use its `ICakeContext context` parameter (not `_cakeContext`)?
13. Are there any leftover `context.` references in Pipeline bodies that would cause runtime `NullReferenceException`?

### Doc coherence
14. Do the doc changes in `phase-x-*.md` contradict each other? (E.g., does §8 still reference the deleted §8.3?)
15. Does `ADR-004` §3.5 correctly describe the discarded decision?
16. Are there remaining doc references to "P4 §8.3 fluent split" that should have been updated but weren't?

### Global invariants
17. `dotnet test` — 515/515, 0 skipped (verify)
18. `ArchitectureTests` — all 5 invariants green (verify)
19. No production code references `BuildContext` outside `Host/` and Cake `FrostingTask<BuildContext>` subclasses (verify with `grep`)

## How to review

1. Start with `git diff --stat` to see the full scope
2. Read `git diff` in full — don't skip the mechanical changes; typos hide there
3. Run `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo` and confirm 515/515
4. Grep for `context\.` in `build/_build/Features/` and `build/_build/Integrations/` — there should be zero hits outside Cake `Tool<T>` wrappers and `FrostingTask<BuildContext>` subclasses
5. Grep for leftover `BuildContext` in Pipeline `RunAsync` signatures: `RunAsync\(BuildContext`
6. Spot-check 3-4 Pipeline files for correct field usage (`_cakeContext` vs `_log` vs `_pathService`)
7. Report findings grouped by severity: **BLOCKER** (won't compile / test fails), **BUG** (compiles but wrong), **SMELL** (pattern problem, future risk), **NIT** (style/consistency)

Be adversarial. The author did this work in a single long session. Fatigue errors are likely. Find them.
