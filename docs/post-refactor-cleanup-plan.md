# Post-Refactor Cleanup Plan

> Phase X / build-host refactor closed 2026-05-10 ([`ADR-002`](decisions/2026-05-05-target-centric-build-host.md), [`ADR-003`](decisions/2026-05-12-build-host-data-layer.md)). This file tracks the time-boxed hardening window — correctness gaps, style consistency, deferred extractions, test debt, and UX polish surfaced during S11–S16 reviewer follow-ups. When all checkboxes resolve, retire this file; any survivors fold back into [`plan.md`](plan.md) Phase X or a follow-up phase.

## Execution Surface

Active slice plans live in [`refactor-followup/`](refactor-followup/README.md), tiered:

- **Tier 1** — full plans, ready to execute: `01-cancellation-plumbing`, `02-ctor-policy`, `03-quick-wins`
- **Tier 2** — outlines awaiting promotion: 9 cohort sketches under [`refactor-followup/tier-2/`](refactor-followup/tier-2/)
- **Tier 3** — one-line deferred + watch list: [`refactor-followup/tier-3-deferred.md`](refactor-followup/tier-3-deferred.md)

Section headers below cross-reference the relevant tier folder slug. LLM sessions iterate via `superpowers:writing-plans` + `superpowers:executing-plans` against the Tier 1 plan files.

## 1. Correctness Gaps

- [x] Forward `CancellationToken` across migrated tasks (landed via [`refactor-followup/01-cancellation-plumbing`](refactor-followup/01-cancellation-plumbing.md); pre-refactor pipelines threaded ct, re-inline path lost it). `Targets/Package/PackageConsumerSmoke/PublishStaging` tasks now read `context.CancellationToken` and forward to async collaborators; `BuildContext.CancellationToken` wired in `Program.cs` to `Console.CancelKeyPress` + `PosixSignalRegistration` for SIGTERM. `IProjectMetadataReader.Read` (sync) deliberately out of scope.
- [ ] Narrow `Targets/Harvest/Services/BinaryClosureWalker.cs` catch-all (file-scope `#pragma warning disable CA1031` + `catch (Exception ex)` at line 139). Mirror `HarvestTask.IsOperationalHarvestException` and let programmer bugs (NRE / OOM / contract violations) propagate instead of folding into typed `Result`.
- [ ] Fix `BinaryClosureWalker.MatchesPattern` (line 188) — currently silent-fails on multi-`*` patterns (`parts.Length != 2 → return false`). Either support multi-`*` or throw with an explicit "pattern shape not supported" diagnostic.

## 2. UX & CLI Surface

- [ ] Redesign `FakeCakeWorld` fluent API — `WithRid` / `WithVersionsFile` / `WithSuffix` / `WithFamilyVersions` set CLI option values; `WithTextFile` / `WithManifestFile` seed the fake filesystem. Same `With*` prefix conflates two concerns. Split into distinct method namespaces (`world.Cli.Rid(...)` vs `world.Files.Manifest(...)` or similar).
- [ ] Unify diagnostic target UX — bring `Targets/DumpbinDependents/DumpbinDependentsTask.cs` (41 LOC raw dump) and `Targets/LddDependents/LddDependentsTask.cs` (36 LOC `key => value` log) up to `Targets/OtoolAnalyze` elaboration: per-platform system-library classifier, dependency table, `manifest.system_exclusions` suggestion section. Reuse existing scanner implementations.
- [ ] Unify diagnostic target binary inputs — collapse `--dll` and `--library` into a single auto-detecting option (path vs manifest name). `Inspect-HarvestedDependencies` already manifest-resolves; the others gain manifest-resolution via `PrimaryBinary` patterns. Update `launchSettings.json`, `tools.cs`, docs; provide a deprecation cycle for the old flags.
- [ ] Resolve `Otool-Analyze` stale `--dll` fallback — walks `vcpkg_installed/{x64-osx-dynamic, arm64-osx-dynamic}/lib/*.dylib`, which are pre-hybrid-static triplets that no longer match the project's packaging model. Either delete the vcpkg-mode entirely or migrate it to hybrid-static awareness via `IRuntimeProfile.Triplet`.
- [ ] Rename `tools.cs:45` `build` subcommand → `cake`. `build` is misleading (it's a Cake passthrough, doesn't compile anything). Update `CLAUDE.md`, `tests/smoke-tests/README.md:49`, `README.md:110`, and any `.github/prompts/*` references to `build --tree`.
- [ ] Strip "(post-C feed-probe wiring)" phrase from `Targets/Package/PackageTask.cs:91` G58 error message — leaks an internal phase reference to operator output. Replace with "pass `--feed <URL>`".

## 3. Style Consistency Sweep

- [ ] Decide a constructor-style policy for cross-cutting multi-dep targets: verbose `private readonly` + `ArgumentNullException.ThrowIfNull` (current `PreFlightCheckTask`, `PackageFamilyPacker`, `PackageConsumerSmokeTask`, `PublishStagingTask`) vs primary-ctor-as-capture (`OtoolAnalyzeTask`, smaller sibling targets). Apply consistently across the 4 verbose targets. Candidate for an ADR-002 §8 amendment or a standalone coding-style guideline.
- [ ] `Targets/PreFlightCheck/PreFlightCheckTask.cs` — derives from `AsyncFrostingTask<BuildContext>` but returns `Task.CompletedTask`. Switch to `FrostingTask<BuildContext>` (sync) OR keep async with a code comment if G58 feed-probe etc. will need async IO.
- [ ] `Targets/Package/Services/PackageFamilyPacker.cs:186-187` — add `using Build.Validation.Conventions;`, drop fully-qualified `Build.Validation.Conventions.FamilyIdentifierConventions` (called twice).
- [ ] `build/_build.Tests/Fixtures/FakeCakeWorld.cs:44,60` — add `using Build.Data.Versions;`, drop fully-qualified `Build.Data.Versions.PackageFamilyVersionSet`.
- [ ] `Build.Results.Unit.cs:9` — convert `public static Unit Value => default;` to `public static readonly Unit Value;` (one IL load per Result construction vs getter call).
- [ ] Audit `Host/BuildContext.cs` CLI-derived property types for cohort consistency — current mix of `FilePath?` (`VersionsFilePath`), `IReadOnlyList<string>` (`Dlls`, `Libraries`, `ResolveVersionsScope`, `ExplicitVersionEntries`), `string?` (`ExplicitVersions`). Consider extension methods on `BuildContext` for common "resolve to canonical FilePath" shapes.

## 4. API & Performance Refinements

- [ ] `Data/Versions/PackageFamilyVersionSet.cs` — three refinements left:
  - Cache `IReadOnlyList<PackageFamilyVersion>` at construction; current `GetEnumerator` (line 88) rebuilds a LINQ projection on every call.
  - `GetHashCode` (line 127): iterate `_versions` directly; current path hits the dictionary twice per element.
  - Drop the `IReadOnlyCollection<PackageFamilyVersion>` interface boundary (line 42) OR justify both shapes — primary consumer pattern is lookup, not iteration.
- [ ] `Targets/PublishStaging/PublishStagingTask.ResolveConcreteFamiliesInScope` (line 129) — O(N·M) `SingleOrDefault` in loop; build a family-name dictionary once.
- [ ] `PublishStagingTask.AuthEnvVarChain` (line 36) — decide shape: keep `string[]` or upgrade to `ImmutableArray<string>` for the literal-pinned auth chain.
- [ ] `Targets/NativeSmoke/Services/MsvcDevEnvironment.cs:186-208` — add `try/finally { if (!process.HasExited) process.Kill(true); }` around the `cmd.exe` child; cancellation mid-vcvars currently risks an orphan process.
- [ ] `Targets/ConsolidateHarvest/ConsolidateHarvestTask.cs:78` — trim aggregate exception to count-only summary; per-library messages already flow through the reporter (log-and-throw amplification per ADR-002 §11).
- [ ] `Targets/ConsolidateHarvest/Services/StagedArtifactSwapper.cs` — `SwapAtomic` is not truly atomic (doc admits it). Rename to `Swap` / `ReplaceWithStaged`, OR keep the name with a clarified XML doc-comment.
- [ ] `Targets/ConsolidateHarvest/Services/LicenseUnionWriter.ComputeSha256Async` (lines 201-216) — replace `SHA256.Create()` + StringBuilder hex loop with `Convert.ToHexStringLower(hash)` (.NET 9+). The streaming-from-FakeFileSystem path precludes the `SHA256.HashDataAsync` static overload.
- [ ] `Targets/Package/Services/DependencyRangeNormalizer.cs:110` — verify cross-family lower-bound semantics against G56 intent in `release-guardrails.md`. Currently uses the satellite's own version as the lower bound on the core family dependency. Either document the policy explicitly OR fix to use the dependency family's resolved version (behavior risk — changes package output).

## 5. Extractions & Re-shapes

- [ ] `Targets/Harvest/Services/BinaryClosureWalker.BuildClosureAsync` (~108 lines, file-scope `MA0051` + `CA1031`) — split into two BFS phases: package-graph walk + runtime-dep walk.
- [ ] `Targets/Harvest/Services/ArtifactPlanner.CreatePlanAsync` (~180 lines, `[SuppressMessage("MA0051")]`) — extract the statistics-builder phase (`BuildStatistics(actions, closure, strategy)` private static OR `DeploymentStatisticsBuilder`). Pure projection over actions; removes the suppression.
- [ ] `Targets/PackageConsumerSmoke/PackageConsumerSmokeTask` (~112 LOC `RunAsync`, `MA0051` suppression) — extract collaborators:
  - `SmokePreflightChecker` — fold `ResolveSmokePackages`, `EnsureSelectionSupportsCurrentSmokeScope`, `EnsureSmokeCsprojsMatchManifestScopeAsync`, `EnsurePackageArtifactsExist`.
  - `Net4xRuntimeSkipPolicy` — fold `ShouldSkipTfm`; symmetrizes the cohort with existing probe/comparator/runner/reporter peers.
  - `PrepareWorkspace` — bin/obj/workingRoot purge block; removes the `MA0051` suppression.
- [ ] Promote `SmokePackage` nested record → `Targets/PackageConsumerSmoke/Models/SmokePackage.cs`. Cross-family identifier derivation deserves a typed home in the canonical `Models/` subfolder.
- [ ] `PackageConsumerSmokeTask.ShouldSkipTfm` — convert `out string reason` → `(bool ShouldSkip, string Reason)` tuple.
- [ ] `Targets/PackageConsumerSmoke/Services/DotNetSmokeRunner` — `RunCompileSanity` vs `RunSmokeForTfm` differ on TWO axes today: env-vars dictionary AND build-server flag injection (`--disable-build-servers`, `-p:UseSharedCompilation=false`, `-nodeReuse:false`; MTP rejects unknown flags on the test path). Decide whether the divergence stays explicit at two methods or folds into one method with parameters.
- [ ] `Data/Harvest/HarvestStatusRepository` — rename to `HarvestArtifactsRepository` OR split into `IHarvestStatusWriter` + `IHarvestArtifactsCleaner`. `Invalidate` cleans cross-target consolidated paths so the "Status" name is narrower than the type's scope.
- [ ] Move `DeploymentStatistics` from `Targets/Harvest/Models/DeploymentPlan.cs` → `Data/Harvest/` (or new `Build.Harvesting/`). Currently `Build.Data.Harvest.IHarvestStatusRepository` references a target-local model — inverted layering.
- [ ] `Host/Paths/IPathService` segregation — 15+ `GetHarvestLibrary*` methods on a single god interface. Lift `IHarvestPaths` consumed by `HarvestStatusRepository` + `ConsolidateHarvest` stack.
- [ ] `Validation/Packaging/HarvestReadinessValidator` validator-vs-gate naming — lives in `Validation/Packaging/`, named `…Validator`, but throws `CakeException` (gate semantics). Two options:
  - (a) Rename to `HarvestReadinessGate` + relocate to `Targets/Package/Services/` (carve-out from ADR-002 §8).
  - (b) Change to return `ValidationReport`; `PackageFamilyPacker` translates to throw at the boundary (cohort consistency, but reshapes 7 unit tests' `ThrowsAsync` assertions).
- [ ] Extract `ManifestConfig.ResolveRequested(...)` extension method — `Targets/Harvest/HarvestTask.cs:181` `ResolveLibrariesToHarvest` and `Targets/NativeSmoke/NativeSmokeTask.cs:182` `ResolveLibrariesToValidate` duplicate the same algorithm.

## 6. Reporter Cohort Consolidation

- [ ] Six reporters now share a recurring shape (`LogStarting`, `StartLibrary`, `FinishLibrary`, `ReportLibraryFailure` / `ReportPhaseFailure`, `LogCompleted`): `OtoolReporter`, `HarvestReporter`, `PackageReporter`, `PackageConsumerSmokeReporter`, `ConsolidateHarvestReporter`, `PreflightReporter`. Rule-of-five has triggered. Evaluate an `IReporter` base or a shared `RuleRenderer`.
- [ ] `Targets/PreFlightCheck/Reporting/PreflightReporter` migration from `ICakeContext`-only (`Log.Information` / `Log.Error`) → `IAnsiConsole` (Spectre tables / panels / colors). Outlier among the cohort. Bundle with any other `ICakeLog`-static-style consumers in a single visual-consistency pass.

## 7. Test Discipline & Coverage

- [ ] `Scenarios/Package/PackageTaskScenarioTests.RunAsync_Should_Pack_All_Selected_Families_When_Happy_Path` — assert per-family `dotnet pack` invocations, topological order (sdl2-core before sdl2-image), and `headSha` propagation to `nativeMetadataGen.GenerateAsync(family, version, headSha, ct)`.
- [ ] `Unit/Validation/Packaging/HarvestReadinessValidatorTests.EnsureReadyAsync_Should_Return_When_All_Gates_Pass` — assert the success-path log line; add coverage for the `DivergentLicenses.Count > 0` warning branch (currently 0%).
- [ ] `Unit/Targets/Package/Services/PackageFamilyPackerTests` — add tests for `dotnet pack` failure (native + managed) and `IProjectMetadataReader` failure paths the packer explicitly handles. Assert on the `CakeException` "See log." anchor + reporter-emitted log line.
- [ ] `HarvestReadinessValidatorTests` payload-subtree-missing tests — tighten to assert on the unique path component (`runtimes` vs `_consolidated`); currently indistinguishable on assertion message.
- [ ] `DependencyRangeNormalizer` — 3 throw branches uncovered (nuspec entry missing, library_ref missing in manifest, vcpkg_version invalid).
- [ ] Re-add explicit `Arguments.Contains("--project", StringComparison.Ordinal)` regression to the PackageConsumerSmoke happy-path scenario; a retired test asserted `dotnet test --project <path>` shape and the current scenario only checks that "test" + TFM appear somewhere in args.
- [ ] Test discipline polish cluster: `BuildScopeManifest` dead helper, happy-path cardinality assertions, `NewWorld(feedClient)` unused param, verbose `Arg.Is<T>(t => t == x)` patterns, fully-qualified `LogLevel` repeats, `MonoAvailabilityProbe` whitespace-PATH edge case, `"When_Happy_Path"` naming-convention drift, `PublishPublic` missing exception-type assertion, validator separate Code+Message asserts (couple them), "3 TFMs" pluralization brittleness.

## Watch list (defer; conditional triggers)

Not actionable today but tracked so they don't get lost — each activates on a specific signal:

- **`IGitHubAuthTokenResolver` extraction** — promote when PD-7 (`PublishPublic` real implementation) needs the same env-var chain as `PublishStaging`.
- **`IGitHeadResolver` interface** — promote when a second consumer appears (Harvest-time or Publish-time SHA stamping). Today's `Func<ICakeContext, DirectoryPath, string>` on `PackageTask` works fine.
- **`SystemFileFilter` extraction from `RuntimeProfile`** — only if `RuntimeProfile` keeps accumulating non-profile logic.
- **`HarvestReporter` method-count ceiling** — 9 public methods today; ADR-002 §15 risk register cap is 10. Re-evaluate on the next addition.
- **C# opportunistic polish** — `ReadmeMappingTableBlock.BuildBlock` StringBuilder + LF/CRLF consistency, `SmokeScopeComparator.Descendants()` typed-query optimization, `MonoAvailabilityProbe.mono.exe` recognition on Windows. Address per-file as in-flight slices touch the same code; no dedicated pass.
