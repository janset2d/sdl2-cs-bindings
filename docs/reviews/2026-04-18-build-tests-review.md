# Review - Build.Tests Deep Dive

**Date:** 2026-04-18
**Status:** Ongoing
**Mode:** Read-only review
**Validation performed:** targeted code/docs inspection plus `dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo` (324 passed, 0 failed, 0 skipped)

## Scope And Assumptions

This pass focused on the `build/_build.Tests` project itself rather than the production build-host code. The review lens stayed the same: look for incorrect assumptions, weak assertions, coverage blind spots, platform bias, and docs-or-comments drift that can make the green bar less trustworthy than it looks.

Primary surfaces reviewed:

- `build/_build.Tests/Build.Tests.csproj`
- `build/_build.Tests/Fixtures/*`
- `build/_build.Tests/Unit/Modules/Packaging/*`
- `build/_build.Tests/Unit/Tasks/Preflight/*`
- `build/_build.Tests/Unit/CompositionRoot/*`
- `build/_build.Tests/Characterization/*`

## Findings

### [High] `RealVcpkgJson_Should_Deserialize_Successfully` can silently pass without exercising the contract it claims to cover

- **Location:** [Build.Tests.csproj](../../build/_build.Tests/Build.Tests.csproj#L10), [Build.Tests.csproj](../../build/_build.Tests/Build.Tests.csproj#L11), [VersionConsistencyTests.cs](../../build/_build.Tests/Unit/Tasks/Preflight/VersionConsistencyTests.cs#L66), [VersionConsistencyTests.cs](../../build/_build.Tests/Unit/Tasks/Preflight/VersionConsistencyTests.cs#L69), [VersionConsistencyTests.cs](../../build/_build.Tests/Unit/Tasks/Preflight/VersionConsistencyTests.cs#L72)
- **Evidence type:** Observed in code and validated by test-project configuration
- **Confidence:** High
- **The reality:** `RealVcpkgJson_Should_Deserialize_Successfully` exits early when `vcpkg.json` is unavailable and reports success instead of an explicit skip or failure. At the same time, the project suppresses `S2699` globally, so the analyzer that would normally complain about a no-assert test is disabled.
- **Why it matters:** this is a false-green pattern. In partial-checkout or misconfigured CI contexts, the suite can claim the real `vcpkg.json` contract is covered when the test never actually ran its assertions.
- **Recommended fix:** replace the bare `return` with an explicit skip primitive if the scenario is genuinely optional, or better, seed a fixture manifest and assert against it unconditionally. Narrow or remove the project-wide `S2699` suppression so accidental zero-assert tests stop slipping through.
- **Tradeoff:** stricter semantics may expose environments that were quietly under-provisioned before, but that is exactly the point of a trustworthy build-host suite.

### [Medium] Packaging-path tests are still anchored to the old proof slice instead of the current 7-RID contract

- **Location:** [ManifestFixture.cs](../../build/_build.Tests/Fixtures/ManifestFixture.cs#L75), [ManifestFixture.cs](../../build/_build.Tests/Fixtures/ManifestFixture.cs#L85), [ManifestFixture.cs](../../build/_build.Tests/Fixtures/ManifestFixture.cs#L87), [PackageTaskRunnerTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/PackageTaskRunnerTests.cs#L28), [PackageTaskRunnerTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/PackageTaskRunnerTests.cs#L38), [PackageOutputValidatorTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/PackageOutputValidatorTests.cs#L346), [PackageOutputValidatorTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/PackageOutputValidatorTests.cs#L347), [PackageOutputValidatorTests.cs](../../build/_build.Tests/Unit/Modules/Packaging/PackageOutputValidatorTests.cs#L367)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** the canonical test manifest fixture still models a single runtime, `win-x64`. `PackageTaskRunnerTests` build their happy-path and guardrail checks on top of that one-runtime fixture. `PackageOutputValidatorTests` broaden the shape check slightly, but still only synthesize the original proof-slice payload set: `win-x64`, `linux-x64`, and `osx-x64`. The `build/_build.Tests/Characterization/Packaging/` folder also exists but is currently empty.
- **Why it matters:** the repo now advertises a 7-RID runtime matrix, but the packaging-path tests still mostly defend the older 3-RID story. That leaves `win-arm64`, `win-x86`, `linux-arm64`, and `osx-arm64` outside the Build.Tests safety net for pack/consumer-path regressions.
- **Recommended fix:** add runtime-matrix-aware fixture variants instead of treating `CreateTestManifestConfig()` as the universal default for packaging tests. At minimum, introduce parameterized packaging checks for the four newly-covered rows and populate `Characterization/Packaging/` with artifact-shape or manifest-driven contract tests.
- **Tradeoff:** more fixture data and longer tests, but this is release-surface coverage, not decorative test count inflation.

### [Medium] Shared test fixtures are not hermetic and duplicate a brittle repo-root heuristic

- **Location:** [WorkspaceFiles.cs](../../build/_build.Tests/Fixtures/WorkspaceFiles.cs#L13), [WorkspaceFiles.cs](../../build/_build.Tests/Fixtures/WorkspaceFiles.cs#L15), [WorkspaceFiles.cs](../../build/_build.Tests/Fixtures/WorkspaceFiles.cs#L46), [ManifestFixture.cs](../../build/_build.Tests/Fixtures/ManifestFixture.cs#L130), [RuntimeProfileFixture.cs](../../build/_build.Tests/Fixtures/RuntimeProfileFixture.cs#L33), [ManifestDeserializationTests.cs](../../build/_build.Tests/Characterization/ConfigContract/ManifestDeserializationTests.cs#L13)
- **Evidence type:** Observed in code
- **Confidence:** High
- **The reality:** `WorkspaceFiles` resolves repo root by walking five fixed parents from `AppContext.BaseDirectory`, then exposes live `build/manifest.json` and `vcpkg.json` paths to the suite. `ManifestFixture` and `RuntimeProfileFixture` both consume those live files, and the characterization tests read the live manifest repeatedly rather than from a sealed fixture copy.
- **Why it matters:** this blurs the line between hermetic unit tests and characterization tests. It also means a test-host layout change or a repo-topology change can break large parts of the suite for reasons unrelated to the behavior under test.
- **Recommended fix:** keep `WorkspaceFiles` usage confined to true characterization tests, move unit fixtures onto seeded test data, and replace fixed parent-hopping with marker-based discovery or injected paths where live-workspace reads are still intentional.
- **Tradeoff:** the suite becomes a bit more explicit and slightly less convenient to write, but much easier to trust and maintain.

## Broader Systemic Observations

- The suite is heavily Windows-shaped in practice even where the code under test is cross-platform. `FakeRepoBuilder` defaults to `FakeRepoPlatform.Windows`, and many task/preflight/packaging tests instantiate Windows repos explicitly. Runtime-profile classification tests do exercise Linux and macOS labels, but the broader file-system and path-shape story is still Windows-first.
- The project already contains a better pattern than some of the older tests use: `ManifestConfigSeeder.FromDefaultFixture()` and the fixture-data copy rule in [Build.Tests.csproj](../../build/_build.Tests/Build.Tests.csproj#L26) support hermetic seeded data, but several tests still bypass that path and read live workspace files directly.
- Comments in the test suite still carry some stale guard naming from the pre-S1 era. That is low-risk by itself, but it makes the suite harder to audit because the tests do not always speak the same guardrail vocabulary as the live code and docs.

## Open Questions / Confidence Limiters

- Is the intended long-term role of `Characterization/Packaging/` to hold live-artifact contract tests, or was the folder created speculatively and never staffed?
- Should partial-checkout CI really be a supported mode for `Build.Tests`, or would it be cleaner to fail fast when required workspace files are absent?

## What Was Not Verified

- I did not run OS-specific test hosts; the `dotnet test` validation in this pass ran on the current Windows environment only.
- I did not execute end-to-end pack or consumer-smoke commands as part of this review note.
- I did not inspect every single test file line-by-line; this pass prioritized shared fixtures, packaging/preflight tests, and characterization boundaries.

## Short Summary

`Build.Tests` is fast and currently green, but the signal is narrower than it looks. The most important defect is the silent-pass pseudo-skip in `VersionConsistencyTests`; the most important structural gap is that packaging-path coverage still mostly defends the old proof slice instead of the current 7-RID contract.