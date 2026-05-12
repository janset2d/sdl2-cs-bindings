# Post-Migration Build Host Review — 2026-05-12

Multi-agent review of `build/_build/` and `build/_build.Tests/` after ADR-002 migration completion (S16/P10). Six reviewers across three perspectives: full-context (ADR-002 + all docs), moderate-context (summarized rules), and unbiased (pure .NET/C#).

**Overall verdict:** The production code is near-perfectly ADR-002 compliant. All retired abstractions are genuinely gone. The architecture is disciplined. The test infrastructure has real issues — V1→V2 migration is incomplete and real filesystem leaks persist.

---

## Blockers

### B1. `RepositoryOptions.RepoRooOption` typo

- **File:** `build/_build/Host/Cli/Options/RepositoryOptions.cs`, line 7
- **Finding:** C# field named `RepoRooOption` (double 'o') instead of `RepoRootOption`. The `--repo-root` CLI alias is correct, and System.CommandLine binds to `ParsedArguments.RepoRoot` by property name, so this works at runtime. But it's clearly a typo and will confuse anyone reading the option registration code.
- **Fix:** Rename field to `RepoRootOption`.

### B2. `ManifestConfigSeeder` reads real filesystem via `File.ReadAllText` + `AppContext.BaseDirectory`

- **File:** `build/_build.Tests/Fixtures/Seeders/ManifestConfigSeeder.cs`, lines 91–108
- **Finding:** `FromDefaultFixture()` and `LocateFixturePath()` use `File.ReadAllText(path)` reading from a path resolved via `AppContext.BaseDirectory`. Directly violates testing-guidelines.md: "No `AppContext.BaseDirectory` traversal to find repo files" and "Real `File.ReadAllText` / `File.WriteAllText` from fixture helpers or seeders" is never allowed.
- **Fix:** Replace with `FixtureLoader.Load("manifest.fixture.json")`. The fixture file already exists at `Fixtures/Data/manifest.fixture.json` as an embedded resource.

### B3. `WorkspaceFiles` reads real `build/manifest.json` via `AppContext.BaseDirectory` traversal

- **File:** `build/_build.Tests/Fixtures/WorkspaceFiles.cs`, lines 43–49
- **Finding:** `ResolveRepoRoot()` walks up `AppContext.BaseDirectory` with 5 `GetParent()` calls to find repo root, then reads real `build/manifest.json`. Used by `ManifestFixture.RealManifest` and `RuntimeProfileFixture` (system exclusions). Same guidelines violation as B2.
- **Impact:** Tests using `ManifestFixture.RealManifest` (`GenerateMatrixTaskScenarioTests`, `RealManifestContractTests`) depend on real filesystem data and brittle path assumptions. Build output directory structure changes will break them.
- **Fix:** Replace `ManifestFixture.RealManifest` with an embedded fixture. `RuntimeProfileFixture` should load exclusions from an embedded fixture rather than the production file.

### B4. `HarvestTask.ProcessLibraryAsync` fragile catch ordering

- **File:** `build/_build/Targets/Harvest/HarvestTask.cs`, lines 156–165
- **Finding:** The catch block hierarchy catches `OperationCanceledException` (re-throws), then `CakeException` (re-throws via `throw;`), then `Exception` with a filter `IsOperationalHarvestException`. The filter excludes `CakeException`, so the `catch (CakeException)` block is correctly scoped — but `CreateLibraryFailureAsync` inside the general catch also produces a `CakeException`. If someone later adds `CakeException` to the operational filter, it creates an infinite throw-catch loop.
- **Fix:** Restructure to avoid fragile catch ordering. Either make `CreateLibraryFailureAsync` return the exception rather than throwing it, or unify the two operational catch blocks into one.

---

## High

### Production

#### H1. `BinaryClosureWalker.BuildClosureAsync` — sync-over-async with `Task.FromResult`

- **File:** `build/_build/Targets/Harvest/Services/BinaryClosureWalker.cs`, lines 41, 74–75
- **Finding:** `await Task.FromResult(GetPackageInfo(...))` wraps the synchronous `GetPackageInfo` method in a completed task just to await it. Forces an extra state machine allocation with zero benefit — the method is not actually asynchronous. Appears twice in this file.
- **Fix:** Call `GetPackageInfo(...)` synchronously and remove the `await Task.FromResult` wrapping, or change `GetPackageInfo` to be genuinely async if vcpkg package-info queries will ever become I/O-bound.

#### H2. `PackageOutputValidator` is an 847-line god class

- **File:** `build/_build/Validation/Packaging/PackageOutputValidator.cs`, lines 38–847
- **Finding:** This single class handles nuspec loading (XML parsing), canonical metadata checks (G26/G27), dependency group evaluation (G21/G22), within-family version coherence (G23), symbol package validation (G25), native package layout (G47/G48), build-transitive contract, license payload presence (G51), and per-RID payload shape. Violates Single Responsibility Principle — every new guardrail adds more surface area.
- **Fix:** Split into separate validator classes aligned by concern (e.g., `NuspecMetadataValidator`, `DependencyGroupValidator`, `NativePackageLayoutValidator`, `SymbolPackageValidator`), each implementing a narrow interface. The orchestrator would compose them.

#### H3. Constructor over-injection in `PreFlightCheckTask` and `PackageConsumerSmokeTask`

- **Files:** `build/_build/Targets/PreFlightCheck/PreFlightCheckTask.cs` (11 ctor params, lines 21–44), `build/_build/Targets/PackageConsumerSmoke/PackageConsumerSmokeTask.cs` (10 ctor params, lines 36–57)
- **Finding:** Both classes inject 10+ services. PreFlightCheckTask coordinates 7 validators sequentially; PackageConsumerSmokeTask orchestrates preconditions, smoke runners, reporters, metadata readers, version repositories, manifest repositories, runtime profiles, and path services. While DI is appropriate, this many dependencies signals the class is orchestrating too many distinct responsibilities.
- **Fix:** Group related validators behind a composite facade (e.g., `IPreFlightValidator` with `ValidateAll()`). Consider extracting a `SmokeOrchestrator` from `PackageConsumerSmokeTask` that encapsulates the 3-phase flow (compile sanity → per-TFM smoke → reporting).

#### H4. `VcpkgTool.GetAlternativeToolPaths` throws during tool discovery

- **File:** `build/_build/Tools/Vcpkg/VcpkgTool.cs`, lines 56–59
- **Finding:** The override throws `CakeException` when `settings.VcpkgRoot` does not exist. Cake's tool resolution framework calls `GetAlternativeToolPaths` as part of normal tool discovery before it knows whether the tool is actually needed. An exception here during composition root or unrelated Cake setup will surface a confusing error about vcpkg even when the task being run does not use vcpkg.
- **Fix:** Return `base.GetAlternativeToolPaths(settings)` instead of throwing when the directory does not exist. Let tool resolution fall through to its standard PATH-based lookup, and let the actual tool invocation produce the "not found" error.

#### H5. `Program.cs` — broad exception catch, static `AnsiConsole`, and `#pragma warning disable`

- **File:** `build/_build/Program.cs`
- **Finding (a):** Line 1 — file-scoped `#pragma warning disable CA1031, MA0045, MA0051, CA1502, CA1505`.
- **Finding (b):** Lines 135–138 — `catch (Exception ex)` around git rev-parse swallows `OutOfMemoryException`, `StackOverflowException`, and other non-recoverable exceptions.
- **Finding (c):** Lines 112, 137, 142, 150, 156, 208, 218 — static `AnsiConsole.MarkupLine` calls instead of `IAnsiConsole` injection. Every other migrated target uses injected `IAnsiConsole`; Program.cs is the last holdout.
- **Fix:** Narrow the exception catch to `Win32Exception` + `InvalidOperationException`. Consider extracting `GitRepositoryDetector` with `IAnsiConsole` injection. Restrict suppressions to the minimum scope.

#### H6. `RuntimeProfile` XML doc references retired `Shared/Features`

- **File:** `build/_build/Host/Runtime/RuntimeProfile.cs`, lines 99–101
- **Finding:** The enum doc comment says "Shared / Features code that talks to `IRuntimeProfile.Family` uses this local enum." Both `Shared/` and `Features/` namespaces/directories were retired.
- **Fix:** Update the comment to remove references to retired patterns.

#### H7. XML doc references to retired `NativeSmokePipeline`

- **Files:** `build/_build/Targets/NativeSmoke/Requests/NativeSmokeRequest.cs` (line 4), `build/_build/Targets/NativeSmoke/Services/MsvcTargetArch.cs` (line 32)
- **Finding:** Both XML doc comments reference `NativeSmokePipeline` which was retired during the refactoring. `NativeSmokeRequest` says "Request for `NativeSmokePipeline`", but the pipeline no longer exists; the request is consumed by `NativeSmokeTask` directly.
- **Fix:** Update to reference `NativeSmokeTask` instead of the retired pipeline.

### Test

#### H8. Platform-guarded tests silently pass as no-ops

- **Files:** `build/_build.Tests/Unit/Targets/NativeSmoke/Services/MsvcDevEnvironmentTests.cs` (lines 21–27), `build/_build.Tests/Unit/Targets/PackageConsumerSmoke/Services/DotNetRuntimeEnvironmentTests.cs` (lines 39–43)
- **Finding:** Both tests use `if (OperatingSystem.IsWindows()) return;` — when run on Windows, the test returns immediately without asserting anything but reports as passing. A CI matrix running only Windows runners would never exercise these assertions. This is a skip disguised as a pass.
- **Fix:** Use `[Skip("Windows-only test")]` attribute or `Assume.That(...)` so the test is explicitly recorded as skipped rather than silently passing.

#### H9. All `IFixtureSeeder` implementations are V1-only

- **Files:** `ManifestConfigSeeder.cs`, `HarvestOutputSeeder.cs`, `HarvestStatusSeeder.cs`, `VcpkgInstalledSeeder.cs` — all implement `void Apply(FakeRepoBuilder builder)`.
- **Finding:** All 4 seeders are locked to V1 infrastructure (`FakeRepoBuilder`). Writing a V2 scenario test requiring complex fixture data (multi-RID harvest output, full vcpkg_installed layout) forces manual repetition of seeder logic. The `VcpkgPackageInfoProcessSeeder` already provides V2 extension methods on `FakeCakeWorldV2` — proving the pattern works.
- **Fix:** Add `ApplyTo(FakeCakeWorldV2 world)` extension methods for each seeder, following the `VcpkgPackageInfoProcessSeeder` pattern.

#### H10. `RuntimeProfileFixture` reads system exclusions from real manifest

- **File:** `build/_build.Tests/Fixtures/RuntimeProfileFixture.cs`, lines 31–37
- **Finding:** `LoadSystemExclusionsFromManifest()` calls `WorkspaceFiles.ReadAllText(WorkspaceFiles.ManifestPath)`. Consumers include `BinaryClosureWalkerTests` and others. Brittle dependency on production file at test time.
- **Fix:** Extract system exclusions as an embedded fixture or hardcode a minimal test subset.

---

## Medium

### Production (10 items)

#### M1. `InfoTask` — sync `WaitForExit()` in async lambda

- **File:** `build/_build/Targets/Info/InfoTask.cs`, lines 52–94
- **Finding:** The `StartAsync` lambda uses `process.WaitForExit()` (synchronous blocking) and returns `Task.FromResult(Task.CompletedTask)` (nested task). This is both blocking the async path and returning a nested task.
- **Fix:** Make the lambda `async (ctx) => { ... }` and use `await process.WaitForExitAsync()`.

#### M2. `MsvcDevEnvironment.CaptureEnvironmentDeltaAsync` — no process timeout

- **File:** `build/_build/Targets/NativeSmoke/Services/MsvcDevEnvironment.cs`, lines 186–248
- **Finding:** If `vcvarsall.bat` hangs (network share, antivirus, user input prompt), `process.WaitForExitAsync()` blocks indefinitely. The `CancellationToken` parameter is unused for the process itself.
- **Fix:** Add a configurable timeout (30s) via `CancellationTokenSource.CreateLinkedTokenSource` with a timeout, or `process.WaitForExitAsync(timeoutCt)`.

#### M3. `NuGetProtocolFeedClient.PushAsync` — hardcoded 60s timeout, CancellationToken not forwarded

- **File:** `build/_build/Targets/PublishStaging/Services/NuGetProtocolFeedClient.cs`, line 119
- **Finding:** `timeoutInSecond: 60` is a magic number. Large NuGet packages over slow connections could timeout prematurely. The `CancellationToken ct` parameter is passed to `GetResourceAsync` but not to `resource.Push`.
- **Fix:** Accept a `TimeSpan` parameter or derive from configuration. Pass the cancellation token to `resource.Push` if the NuGet SDK supports it.

#### M4. `EnsureVcpkgDependenciesTask` checks both bootstrap scripts unconditionally

- **File:** `build/_build/Targets/EnsureVcpkgDependencies/EnsureVcpkgDependenciesTask.cs`, lines 56–64
- **Finding:** Checks for both `bootstrap-vcpkg.bat` and `bootstrap-vcpkg.sh`, throwing if either is missing. On Windows, the `.bat` file is sufficient; on macOS/Linux only the `.sh` file matters.
- **Fix:** Guard each script check with `OperatingSystem.IsWindows()` / `OperatingSystem.IsLinux()` / `OperatingSystem.IsMacOS()`.

#### M5. `ConsolidateHarvestTask` file-scoped CA1031 suppression is broader than needed

- **File:** `build/_build/Targets/ConsolidateHarvest/ConsolidateHarvestTask.cs`, line 5
- **Finding:** `#pragma warning disable CA1031` is file-scoped (no restore), suppressing CA1031 for `RunAsync`, `BuildManifest`, and `CleanupTempArtifacts` — not just the per-library catch where it belongs.
- **Fix:** Restrict the suppression to the `TryConsolidateLibraryAsync` method only via `#pragma warning disable/restore` around that method.

#### M6. `ArtifactDeployer.CreateArchiveFileListAsync` — raw `File.WriteAllLinesAsync` instead of Cake `IFileSystem`

- **File:** `build/_build/Targets/Harvest/Services/ArtifactDeployer.cs`, line 142
- **Finding:** Writes a temp file list with `await File.WriteAllLinesAsync(...)` instead of routing through `IFileSystem`. The surrounding method already uses Cake-native `_ctx.Directory()` and `_ctx.EnsureDirectoryExists()` — this single call is inconsistent.
- **Fix:** Use Cake `IFileSystem` for consistency with the rest of the build host.

#### M7. `DumpbinDependentsTask` swallows file-not-found as warning-only

- **File:** `build/_build/Targets/DumpbinDependents/DumpbinDependentsTask.cs`, lines 24–25
- **Finding:** When the specified DLL is not found, it logs a warning but continues to invoke the tool on the non-existent file. Same pattern in `LddDependentsTask.cs`.
- **Fix:** Either throw `CakeException` or guard the tool invocation with an early return.

#### M8. `Result<T, TError>` struct — `default` produces semantically invalid instance

- **File:** `build/_build/Results/Result.cs`, lines 19–73
- **Finding:** `default(Result<T, TError>)` has `IsSuccess = false` with `_error = default!`. If `TError` is a reference type, calling `.Error` on a default instance throws `NullReferenceException` (not the documented `InvalidOperationException`).
- **Fix:** Add a guard in the `Error` property, or use a class instead of struct.

#### M9. `PackageFamilyPacker` runs native and managed `dotnet pack` sequentially

- **File:** `build/_build/Targets/Package/Services/PackageFamilyPacker.cs`, lines 91–95
- **Finding:** The two `dotnet pack` invocations are independent I/O-bound processes that could run in parallel, cutting total wall-clock time roughly in half.
- **Fix:** Run concurrently with `Task.WhenAll`.

#### M10. Reporter emoji in CI log output

- **Files:** `HarvestReporter.cs` (lines 16–17, 33, 37, 51), `PreflightReporter.cs` (lines 16–17, 25, 30)
- **Finding:** Emoji characters (magnifying glass, checkmark, cross mark) in `ICakeLog` messages render as missing-glyph boxes in CI log viewers and headless terminals.
- **Fix:** Remove emoji from `ICakeLog` calls, or gate them behind a terminal-capability check.

### Test (6 items)

#### M11. `FakeCakeWorldV2.Create*` factory methods auto-seed `WithDefaultProcessResult(exitCode: 0)` — masks fail-fast

- **File:** `build/_build.Tests/Fixtures/FakeCakeWorldV2.cs`, lines 89–113 (all three factory methods)
- **Finding:** Factory methods call `WithDefaultProcessResult(exitCode: 0, ...)`. Any scenario that forgets to configure a specific command silently gets exit code 0 instead of the "unconfigured process" error. Directly contradicts the guideline: "Unconfigured process commands fail fast (no silent default exit code 0)."
- **Fix:** Remove `WithDefaultProcessResult(...)` from factory methods. Tests that intentionally invoke tools they don't care about must opt in explicitly.

#### M12. V1 fixtures remain in active use across migrated target code

- **Files:** `CsprojPackContractValidatorTests` (uses `FakeRepoBuilder`), `ProgramCompositionRootTests`, `ServiceCollectionExtensionsSmokeTests` (uses `TestHostFixture`), `FakeCakeToolContextBuilder` (10+ test files — Dumpbin, Ldd, Otool, Tar, Vcpkg tool tests, dependency analysis tests)
- **Finding:** V1 fixtures remain in active use for post-migration code (`CsprojPackContractValidator` is post-migration). The deferral has no completion date. Testing-guidelines.md says "Touching a test → migrate to V2."
- **Fix:** (1) Migrate `CsprojPackContractValidatorTests` to `FakeCakeWorldV2`. (2) Migrate tool/dependency-analysis tests to V2 pattern with `WithProcessResult`. (3) Set a milestone for `ServiceCollectionExtensionsSmokeTests` V2 migration. (4) Remove V1 fixtures once all consumers migrate.

#### M13. Five seeders + `FakeRepoBuilder` — dead code

- **Finding:** `HarvestStatusSeeder`, `HarvestOutputSeeder`, `VcpkgInstalledSeeder`, `ManifestConfigSeeder`, `IFixtureSeeder`, and `FakeRepoBuilder` have zero references from any test file.
- **Fix:** Either wire them into consumers or delete them. Keeping dead seeder code creates confusion about which infrastructure is current.

#### M14. `RealManifestContractTests` re-reads manifest file in every test method

- **File:** `build/_build.Tests/Unit/Manifest/RealManifestContractTests.cs`
- **Finding:** Every test method reads the manifest JSON file from disk and deserializes it. `ManifestFixture.RealManifest` already caches the deserialized manifest via `Lazy<ManifestConfig>`.
- **Fix:** Use `ManifestFixture.RealManifest` directly instead of re-reading the file in each test.

#### M15. `ProgramCompositionRootTests` uses mutable global state and reflection on private methods

- **File:** `build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs`
- **Finding (a):** Uses `ParseRootLock` and `EnvironmentVariableLock` for thread safety on a shared `RootCommand` and env var manipulation. Environment variable manipulation in tests is inherently risky even with locks.
- **Finding (b):** Uses `GetProgramHelper("g__IsVerbosityArg", typeof(string))` (line 35) — reflection on compiler-generated naming. Creates coupling to internal compiler implementation details.
- **Fix:** Remove locks (use `[NotInParallel]` if needed). Make helpers `internal` with `[assembly: InternalsVisibleTo]` or extract into a testable class.

#### M16. `PackageOutputValidatorTests.CreateArtifacts` — 10-parameter mega-factory

- **File:** `build/_build.Tests/Unit/Validation/Packaging/PackageOutputValidatorTests.cs`, lines 339–426
- **Finding:** This single method has 10 optional parameters, five boolean flags controlling conditional behavior, and creates actual `ZipArchive` instances in the fake filesystem. Called by 15+ tests. Any change to the validator's expectations could require updating this method or its callers in subtle ways.
- **Fix:** Extract builder variants or use explicit factory methods for each fixture variant instead of branching logic inside one mega-factory.

---

## Low

### Production

| # | Finding | File |
|---|---------|------|
| **L1** | `DumpbinDependentsTask` and `LddDependentsTask` declare `async` but wrap sync work in `Task.Run` — could be `FrostingTask<BuildContext>` (sync) | Both task files |
| **L2** | `OtoolAnalyzeTask.AnalyzeOneAsync` catches all `Exception` types including `OutOfMemoryException` | `Targets/OtoolAnalyze/OtoolAnalyzeTask.cs:106-111` |
| **L3** | `DependencyRangeNormalizer.ResolveCrossFamilyUpperBound` — `SingleOrDefault` with loose string matching, no error locality for duplicate names | `Targets/Package/Services/DependencyRangeNormalizer.cs:158,167` |
| **L4** | `PackageFamilyId` implicit conversion to `string` — can silently convert in contexts where type mismatch should be a compiler error | `Data/Versions/PackageFamilyVersionSet.cs:21` |
| **L5** | `LicenseUnionWriter.GetRelativePathSegments` — manual array walk instead of Cake-native `root.GetRelativePath(file)` | `Targets/ConsolidateHarvest/Services/LicenseUnionWriter.cs:187-199` |
| **L6** | `PackageOutputValidator.EvaluateNativePayloadShapePerRid` — uses `[("runtimes/".Length)..]` magic length constant | `Validation/Packaging/PackageOutputValidator.cs:748` |
| **L7** | `ArtifactDeployer.CreateArchiveFileListAsync` — misleading log message says "using absolute paths" but produces only filenames | `Targets/Harvest/Services/ArtifactDeployer.cs:140-144` |
| **L8** | Four tasks duplicate the same `--versions-file` null check and empty-versions guard with near-identical error messages | PreFlightCheckTask, PackageTask, PackageConsumerSmokeTask, PublishStagingTask |
| **L9** | `ParsedArguments` record lives at the bottom of `Program.cs` instead of a dedicated file | `Program.cs:232-247` |
| **L10** | `ManifestFamilyNameInvariantValidator` `GeneratedRegex` timeout of 100ms — overkill for a linear pattern | `Validation/Manifest/ManifestFamilyNameInvariantValidator.cs:23` |
| **L11** | `CakeJsonExtensions.SerializeJson` — `cakeContext` parameter is unused, kept "by convention" | `Host/Cake/CakeJsonExtensions.cs:176-186` |
| **L12** | `BinaryClosureWalker.MatchesPattern` — only supports single `*` glob, returns `false` for multi-`*` patterns silently | `Targets/Harvest/Services/BinaryClosureWalker.cs:188-210` |
| **L13** | `LicenseUnionWriter.CopyFileAsync` — uses `FileShare.None` on output; safe today but would break under parallelization | `Targets/ConsolidateHarvest/Services/LicenseUnionWriter.cs:218-226` |

### Test

| # | Finding | File |
|---|---------|------|
| **L14** | Test naming convention drift — `VcpkgAliasesTests` and `InspectHarvestedDependenciesTaskScenarios` use `On_` instead of `When_` | Multiple files |
| **L15** | `BinaryClosureWalkerTests` uses `using IoPath = System.IO.Path` alias instead of Cake-native path operations | Line 10, 118, 155 |
| **L16** | `MsvcTargetArchTests` uses synchronous `Assert.Throws<>()` + `await Task.CompletedTask` instead of fluent `await Assert.That(...).Throws<>()` | Lines 31, 39–41 |
| **L17** | `FakeCakeWorldV2Tests` wraps sync code in `Task.CompletedTask` inside `Assert.ThrowsAsync` — fragile if method becomes genuinely async | Lines 133–143 |
| **L18** | `HarvestReporterTests.LogCompleted_Should_Write_Green_Completion_Rule` — test name promises color assertion but only checks text content | Line 57 |
| **L19** | `SuccessRidStatus` helper generates `DateTimeOffset.UtcNow` — non-deterministic timestamps in test output | `Scenarios/ConsolidateHarvest/ConsolidateHarvestTaskScenarioTests.cs:168` |
| **L20** | `ResultTests.Default_Should_Be_Failure_With_Default_Error` — tests struct default behavior that may not be a supported contract | `Unit/Results/ResultTests.cs:110-119` |
| **L21** | `FakeCakeWorldV2Tests` and `FakeCakeWorldV2PlatformFactoriesTests` (~20 tests, ~400 lines) test test infrastructure itself — consider trimming to edge-case tests only | Both files |
| **L22** | `ManifestRepositoryUnitTests` and `VersionFileRepositoryUnitTests` — 5+ lines of test code per null-guard assertion for behavior the type system already guarantees | Both files |
| **L23** | Mixed synchronous/async assertion patterns across the codebase — some use `Assert.Throws<>()`, most use `await Assert.That(...).Throws<>()` | Multiple files |
| **L24** | `HarvestTaskScenarioTests.CreateHost` accepts 4 optional override parameters — use `WithServices` pattern at call site instead | `Scenarios/Harvest/HarvestTaskScenarioTests.cs:279-325` |

---

## Missing Coverage

| # | Target / Behavior | Detail |
|---|-------------------|--------|
| **X1** | `StageVersions` | Zero scenario tests. Target exists in build host with no behavioral coverage. |
| **X2** | `EnsureVcpkgDependencies` | Zero scenario tests. Target exists but is only exercised through full CI pipeline. |
| **X3** | Process fail-fast behavior | Guideline requires "unconfigured process commands fail fast" but this property is untested. `FakeCakeWorldV2.BuildCakeContext()` throws `InvalidOperationException` — no test proves it. |
| **X4** | `VcpkgInstalledSeeder` | 60+ line fluent builder with zero unit tests. Bugs silently corrupt fixture data for all consumers. |
| **X5** | `InfoTask` failure paths | Only 2 scenario tests (dotnet success + non-zero exit). Missing: dotnet not installed, platform-specific display sections. |
| **X6** | `DumpbinDependentsTask` / `LddDependentsTask` failure paths | Missing: fail-fast on unconfigured process, platform mismatch. |
| **X7** | `OtoolAnalyzeTask` failure paths | Happy-path only. Missing: otool non-zero exit. |
| **X8** | `PackageOutputValidator` specific edge cases | `DependencyRangeNormalizer` — 3 throw branches uncovered (nuspec entry missing, library_ref missing in manifest, vcpkg_version invalid). HarvestReadinessValidator — `DivergentLicenses.Count > 0` warning branch 0% covered. |
| **X9** | `PackageTask` orchestration depth | Happy-path scenario only checks "2 packs invoked" — doesn't assert topological pack order (core before image) or `headSha` propagation. |
| **X10** | Lost regression: `--project` argument shape | Deleted V1 `PackageConsumerSmokePipelineTests.CreateSmokeTestArguments_Should_Use_Project_Option_For_Project_Path` asserted `dotnet test --project <path>`. New V2 happy-path scenario only checks "test" + TFM appear somewhere in args. |

---

## ADR-002 Rule-by-Rule Compliance

| Rule | Description | Status |
|------|-------------|--------|
| §5 | Tasks own orchestration; no mandatory Pipeline | ✅ PASS |
| §6 | Named `BuildContext` properties; no raw `ParsedArguments` in tasks | ✅ PASS |
| §7 | Cake-native abstractions for IO/process/path/logging/tooling | ✅ PASS (2 minor exceptions noted) |
| §8 | Interface discipline — concrete unless interface earned | ✅ PASS |
| §9 | Behavior-first naming; guardrail IDs are metadata only | ✅ PASS |
| §10 | Code/document boundary — comments explain local logic | ✅ PASS |
| §11 | `Result<T,TError>` not OneOf; `ValidationReport` for multi-check | ✅ PASS |
| §12 | Testing model — unit/scenario/integration taxonomy, FakeFileSystem | ⚠️ V1 infra still in use, real FS leaks |
| S13 | Validator + repository interfaces | ✅ PASS |
| S15 | No implicit Result conversions | ✅ PASS |
| P10 | Features/, Shared/, Integrations/, Host/Configuration/ gone | ✅ PASS |

---

## Retired Abstraction Verification

| Abstraction | Status |
|-------------|--------|
| `Features/` directory | ✅ Gone |
| `Shared/` directory | ✅ Gone |
| `Integrations/` directory | ✅ Gone |
| `Host/Configuration/` directory | ✅ Gone |
| Mandatory `*Pipeline` classes | ✅ Gone (all 4 retired) |
| Strategy abstraction + `manifest.strategy` field | ✅ Gone |
| Cake-owned `Coverage-Check` gate | ✅ Gone |
| Architecture dependency tests | ✅ Gone |
| OneOf NuGet packages | ✅ Gone (zero references in .csproj + Directory.Packages.props) |
| `IReadOnlyDictionary<string, NuGetVersion>` boundaries | ✅ Gone |
| `Configurations` aggregate | ✅ Gone |

---

## Priority Order

1. **B2 + B3** — Real filesystem leaks in test infrastructure (violates core testing rule)
2. **H8** — Silent-pass tests on wrong platform (coverage gap disguised as green)
3. **H9 + H10** — V1 fixture migration blockers for test authoring
4. **M11** — `FakeCakeWorldV2` default process result masking fail-fast
5. **M12 + M13** — V1 fixture cleanup and dead code removal
6. **H1–H7** — Production HIGH findings
7. **X1–X10** — Missing coverage
8. **L1–L24** — Low-severity polish

---

## Review Methodology

Six agents reviewed the codebase in parallel on 2026-05-12:

| # | Perspective | Scope | Context |
|---|-------------|-------|---------|
| 1 | Full-context architect | Production code | ADR-002 + refactoring plan + checklist + extraction/testing guidelines + AGENTS.md |
| 2 | Full-context architect | Test code | Same full documentation set |
| 3 | Moderate-context | Production code | Summarized ADR-002 rules |
| 4 | Moderate-context | Test code | Summarized testing rules |
| 5 | Unbiased .NET/C# | Production code | Zero project context — pure code quality |
| 6 | Unbiased .NET/C# | Test code | Zero project context — pure testing practices |

Files reviewed: ~90+ production files across Targets/, Validation/, Data/, Host/, Tools/, Results/; ~70+ test files across Scenarios/, Unit/, Integration/, Fixtures/. Each agent read 15–40+ files in depth.

---

## References

- [ADR-002: Target-Centric Cake Build Host Architecture](../decisions/2026-05-05-target-centric-build-host.md)
- [Target-Centric Build Host Refactoring Plan](target-centric-build-host-refactor-plan.md)
- [Target-Centric Build Host Review Checklist](target-centric-build-host-review-checklist.md)
- [Extraction Guidelines](extraction-guidelines.md)
- [Testing Guidelines](testing-guidelines.md)
- [AGENTS.md](../../AGENTS.md)
- [Parking Lot](../parking-lot.md)
