# Review — Build.Tests Deep Dive

**Date:** 2026-04-18
**Status:** Ongoing
**Mode:** Read-only review
**Validation performed:**

- `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo` -> passed (`324` passed, `0` failed)
- `dotnet build Janset.SDL2.sln -c Release --nologo` -> failed during smoke-project restore (`NU1603`, `NU1605`) while `Build.Tests` stayed green

## A. Scope And Assumptions

This note records findings from this session only. Review scope in this pass:

- `build/_build.Tests/*`
- Live validation already performed in this session against the current repository state

Existing documents under `docs/reviews/` were not used as input for this note.

## B. Findings First

### [High] The suite is green while the real build-host and MSBuild contracts that matter remain untested

- **Location:** [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L135), [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L187), [build/_build.Tests/Unit/Modules/Packaging/PackageOutputValidatorTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/PackageOutputValidatorTests.cs#L350), [build/_build.Tests/Unit/Modules/Packaging/SmokeScopeComparatorTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/SmokeScopeComparatorTests.cs#L121)
- **Evidence type:** Observed in code / observed in executable validation
- **Confidence:** High
- **The reality:** the composition-root coverage stops at invoking `ConfigureBuildServices` and asserting resolved service types. The smoke/package coverage stops at synthetic csproj XML and fake nupkg entries where `buildTransitive/*.targets` is represented as bare `<Project />`. There is no blackbox test here that executes the public build-host entrypoint or the real packed MSBuild targets. In the same session, `Build.Tests` passed cleanly while `dotnet build Janset.SDL2.sln` failed on the actual smoke/build contract.
- **Why it matters:** this is the exact failure mode that hurts contributors and CI credibility: the repo can look green at the build-host unit layer while the real orchestration surface is broken.
- **Recommended fix:** add a small integration layer outside the fast unit suite that does two things:
  1. exercises the build-host through its public CLI seam rather than only through DI wiring
  2. runs one smoke-project restore/build against a temporary local feed using the real packed `Janset.Smoke.*` and `Janset.SDL2.Native.Common.targets` content
- **Tradeoff:** slower and more operationally complex tests, but they cover the contracts that actually broke.

### [Medium] Composition-root tests are coupled to compiler-generated local-function names and private implementation details

- **Location:** [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L32), [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L61), [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L125), [build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs](../../build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs#L234)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** these tests resolve helpers by reflecting on non-public static methods whose names contain compiler-generated fragments such as `g__IsVerbosityArg`, `g__GetEffectiveCakeArguments`, and `g__DetermineRepoRootAsync`. That is not a stable contract. Harmless refactors like moving a local function, changing compiler output, or inlining a helper can break tests without changing runtime behavior.
- **Why it matters:** the suite becomes noisy in the wrong way. It raises false regressions for implementation reshaping while still not validating the real public entrypoint contract.
- **Recommended fix:** move argument/repo-root helper logic behind an internal testable seam with stable names, or test it through the public command-line parsing surface. Keep reflection away from local-function name fragments.
- **Tradeoff:** slightly more structure in the build-host bootstrap, but a much more honest test boundary.

### [Medium] Shared fixtures leak live workspace state and output-layout assumptions into tests labeled as unit coverage

- **Location:** [build/_build.Tests/Fixtures/WorkspaceFiles.cs](../../build/_build.Tests/Fixtures/WorkspaceFiles.cs#L9), [build/_build.Tests/Fixtures/WorkspaceFiles.cs](../../build/_build.Tests/Fixtures/WorkspaceFiles.cs#L43), [build/_build.Tests/Fixtures/RuntimeProfileFixture.cs](../../build/_build.Tests/Fixtures/RuntimeProfileFixture.cs#L9), [build/_build.Tests/Fixtures/RuntimeProfileFixture.cs](../../build/_build.Tests/Fixtures/RuntimeProfileFixture.cs#L31), [build/_build.Tests/Unit/Modules/RuntimeProfile/PlatformDetectionTests.cs](../../build/_build.Tests/Unit/Modules/RuntimeProfile/PlatformDetectionTests.cs#L16), [build/_build.Tests/Unit/Tasks/Preflight/VersionConsistencyTests.cs](../../build/_build.Tests/Unit/Tasks/Preflight/VersionConsistencyTests.cs#L64)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** `WorkspaceFiles` resolves repo root by fixed parent hopping from `AppContext.BaseDirectory`, then shared fixtures load the live `build/manifest.json` and `vcpkg.json`. That means some tests in `Unit/*` are not actually hermetic; they are partly repo-contract checks that depend on current workspace layout and production config contents.
- **Why it matters:** this blurs the line between unit and characterization coverage. A benign manifest edit or output-layout change can fail “unit” tests even when the code under test is fine, and the resulting signal is harder to trust.
- **Recommended fix:** split fixture responsibilities cleanly:
  - keep live-repo assertions in explicit characterization/integration fixtures
  - keep unit fixtures pinned to local test data or in-memory models
  - replace fixed parent hopping with marker-based repo discovery or test-output fixture copies where live files are truly required
- **Tradeoff:** more fixture plumbing and a bit of duplicated sample data, but far better test determinism.

### [Medium] Task-layer coverage is still heavily Windows-shaped despite a seven-RID build contract

- **Location:** [build/_build.Tests/Unit/Tasks/Coverage/CoverageCheckTaskRunTests.cs](../../build/_build.Tests/Unit/Tasks/Coverage/CoverageCheckTaskRunTests.cs#L13), [build/_build.Tests/Unit/Tasks/Harvest/HarvestTaskTests.cs](../../build/_build.Tests/Unit/Tasks/Harvest/HarvestTaskTests.cs#L27), [build/_build.Tests/Unit/Tasks/Harvest/HarvestTaskTests.cs](../../build/_build.Tests/Unit/Tasks/Harvest/HarvestTaskTests.cs#L437), [build/_build.Tests/Unit/Tasks/Harvest/ConsolidateHarvestTests.cs](../../build/_build.Tests/Unit/Tasks/Harvest/ConsolidateHarvestTests.cs#L120), [build/_build.Tests/Unit/Tasks/Preflight/PreFlightCheckTaskRunTests.cs](../../build/_build.Tests/Unit/Tasks/Preflight/PreFlightCheckTaskRunTests.cs#L14), [build/_build.Tests/Unit/Modules/Packaging/PackageTaskRunnerTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/PackageTaskRunnerTests.cs#L25)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** the task boundary is dominated by `FakeRepoPlatform.Windows`, and `HarvestTaskTests` even hardcodes a Windows runtime-profile stub. Lower-level modules do have some Linux/macOS validation, but the orchestration layer where file layout, RID handling, packaging flow, and user-facing failures converge is overwhelmingly exercised through Windows assumptions.
- **Why it matters:** this repo’s contract is cross-platform. If task behavior diverges on Linux/macOS, the current task suite is not where that regression is likely to get caught.
- **Recommended fix:** parameterize representative task tests across at least Windows and Unix fake repos, and add one Linux/macOS-flavored case at each task boundary (`PreFlight`, `Harvest`, `ConsolidateHarvest`, `PackageTaskRunner`, `Coverage` where meaningful).
- **Tradeoff:** broader test matrices and more setup helpers, but much stronger confidence at the layer that actually orchestrates the build.

## C. Broader Systemic Observations

- The suite has useful depth at service and model level, but the closer it gets to real repo entrypoints, the more synthetic the coverage becomes.
- `Characterization` and `Unit` boundaries are currently mixed in the fixture layer. That makes the suite look more hermetic than it really is.
- Cross-platform confidence is strongest in lower-level modules and weakest in tasks, which is the opposite of where operator pain usually shows up.

## D. Open Questions / Confidence Limiters

- Is the current split between fast unit tests and slower build-host integration tests intentional, with another suite planned elsewhere, or is `Build.Tests` expected to carry that burden?
- Should live manifest and `vcpkg.json` assertions remain under `Unit/*`, or should they move fully into `Characterization/*` so the signal is easier to interpret?

## E. What Was Not Verified

- No new end-to-end smoke/build-host execution was added inside `Build.Tests` during this session
- Linux/macOS task execution was not run live on this machine
- I did not run a new temporary-feed integration harness for packed MSBuild targets

## F. Brief Summary

`build/_build.Tests` has solid local depth, but its weak spots are exactly where the repo just proved fragile: public entrypoints, real MSBuild target behavior, hermetic test boundaries, and cross-platform task orchestration. The current suite catches a lot of internal regressions; it does not yet give strong confidence that the repo’s actual build surfaces still work.
