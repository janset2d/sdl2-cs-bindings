# Parking Lot — Preserved Ideas and Partial Threads

> Deliberate landing zone for ideas that are valid or partially implemented but not active work today. The goal: no important idea should survive only because it is buried in retired notes or half-expressed in code.

## How To Use This File

- Items here are worth preserving but not in the active phase.
- Promote items into [`plan.md`](plan.md), a phase doc, a playbook, or a knowledge-base doc when work actually starts.
- Remove items only when they are clearly rejected or superseded — record why.

## Status Legend

| Status | Meaning |
| --- | --- |
| `partially-implemented` | Real code/config/workflow scaffolding exists but isn't fully wired or production-ready |
| `planned` | Idea is intentionally kept; no working implementation yet |
| `parked` | Valuable, but not on the current roadmap |
| `hardening-backlog` | Quality / reliability / maintenance work to revisit |

## Partially Implemented

### Result Pattern Discipline

- Status: `partially-implemented`
- The build host uses `OneOf` (with `OneOf.SourceGenerator`, `OneOf.Monads`). Some manual wrappers and `FromXxx` / `ToXxx` ceremony still exist.
- Preserve:
  - Audit wrapper-local conversion helpers; trim dead ceremony before adding heavier abstraction.
  - Decide an explicit style between named success accessors (`ValidationSuccess`, `Closure`, `DeploymentPlan`) and the generic `SuccessValue()` API so new code stops mixing both idioms.
  - Async chaining + cancellation-aware result variants — already in build host; keep usage consistent.

### PreFlight Validator Growth Guardrails

- Status: `hardening-backlog`
- `PreFlightCheckTask` is a thin orchestrator over per-rule validators; that shape can bloat again if package-family or CI-flow policy folds into the same classes without discipline.
- Preserve:
  - Keep follow-through narrow unless the rule surface actually grows.
  - Prefer splitting loader/parser concerns from rule evaluation only when the added behavior justifies the seams.
  - Avoid reintroducing `BuildContext` leakage or task-level policy into validator internals.

### Harvesting Component Refactors

- Status: `partially-resolved` (S14, 2026-05-09)
- `HarvestPipeline` orchestration extraction landed in S14 ([#87](https://github.com/janset2d/sdl2-cs-bindings/issues/87) — `Targets/Harvest/` decomposes into HarvestTask + walker/planner/deployer/repo/reporter cohort).
- Preserve as remaining pressure-tested directions:
  - Splitting `BinaryClosureWalker.BuildClosureAsync` into two BFS phases (package-graph walk + runtime-dep walk) — currently 110-line method with file-scope MA0051 + CA1031 suppression. Reviewer D (S14, P8 review) flagged as deferred extraction; pre-migration parity preserved.
  - `ArtifactPlanner.CreatePlanAsync` 175-line method splits action emission from statistics derivation. Statistics phase is a pure projection over actions — extract to `BuildStatistics(actions, closure, strategy)` private static or `DeploymentStatisticsBuilder`. Reviewers C+D (S14) convergent finding.
  - `IPathService` god-service smell — 12+ harvest-shaped path methods consumed by HarvestStatusRepository + ConsolidateHarvest stack. Lift `IHarvestPaths` segregation. Reviewer D (S14).
  - `HarvestStatusRepository` naming + scope reconsideration — `Invalidate` cleans cross-target consolidate paths (consolidated dir + manifest + summary) so the "Status" name is narrower than the type's responsibilities. Either rename to `HarvestArtifactsRepository` or split into `IHarvestStatusWriter` + `IHarvestArtifactsCleaner`. Reviewer D-S3 (S14).
  - `DeploymentStatistics` cross-namespace dependency — root `Build.Repositories.IHarvestStatusRepository` references `Build.Targets.Harvest.Models.DeploymentStatistics`. Move `DeploymentStatistics` to `Build.Harvesting/` (cross-target named concept) for clean layering. Reviewers A/B/C/D convergent (S14).
  - Extracting `SystemFileFilter` if `RuntimeProfile` keeps accumulating non-profile logic.

### S14 P8 Reviewer Follow-Ups

- Status: `parked` (post-P8 hardening)
- Multi-agent review at S14 Phase 11 surfaced Tier-3 findings absorbed below; tracked here for future slices when change volume justifies.
- **ConfigureAwait collaborator sweep**: HarvestTask uses `.ConfigureAwait(false)` consistently; collaborators (Walker, Planner, Deployer, MsvcDevEnvironment, LicenseUnionWriter, HarvestArtifactMerger) didn't. Resolved in S14 closure cleanup.
- **ConsolidateHarvest aggregate exception log-and-throw amplification** (Reviewer B-N5): per-library failure logged via reporter + each library's message embedded in aggregate `CakeException`. ADR §11 "log-and-throw noise" risk. Trim aggregate exception to count-only summary; log carries detail.
- **`StagedArtifactSwapper.SwapAtomic` name vs reality** (C-S13): the XML doc admits "not truly atomic"; rename to `Swap` or `ReplaceWithStaged`. Either small naming pass or accept and clarify doc-comment in place.
- **`MsvcDevEnvironment` process management defensive cleanup** (C-S8): `cmd.exe` child not explicitly killed on cancellation; small risk of orphan process if `ct` fires mid-vcvars. Add `try/finally { if (!process.HasExited) process.Kill(true); }` guard.
- **Modernization opportunities**: `Convert.ToHexStringLower(hash)` (.NET 9+) replaces StringBuilder hex loop in `LicenseUnionWriter.ComputeSha256Async`; `SHA256.HashDataAsync` static replaces per-file `SHA256.Create()`.
- **`ResolveLibrariesToHarvest` / `ResolveLibrariesToValidate` duplication** (C-S10, D-N1): identical algorithm in HarvestTask + NativeSmokeTask. Extract `ManifestConfig.ResolveRequested(...)` extension method.

### S15 P9 Reviewer Follow-Ups

- Status: `parked` (post-P9 hardening)
- Multi-agent review at S15 Phase 13 (5 reviewers parallel: full-context A / necessary-context B / unbiased C# C / unbiased SOLID D / test discipline E) surfaced findings absorbed below in slice closure: package-info error coupling, 3 stale doc/comment crefs, dead PackageConsumerSmokeRequest retire, error-code phase-ID strip, B3 Cake-native IO patch (replace raw `OpenRead()` + `StreamReader` with `context.ReadAllTextAsync`). **Post-review architectural correction:** `PackageBuildConfiguration` retirement initially landed as direct `PackageFamilyVersionSet` DI registration via lazy factory in Program.cs (Option α from the design doc). Deniz raised the pattern as inconsistent — every other consumer reads `BuildContext.<NamedCliProperty>` and loads through a file-backed repository at task entry. Three tasks (`PackageTask`, `PackageConsumerSmokeTask`, `PublishStagingTask`) reshaped to mirror the canonical PreFlight pattern: `IVersionFileRepository.Load(context.VersionsFilePath)` with file-existence + non-empty validation at task entry. Computed-state DI factory removed; test fixtures dropped the corresponding registration. Other findings below were deferred to follow-up slices.

- **Cancellation token plumbing across migrated tasks** (C1+C2+C3): `PublishStagingTask` calls `_feedClient.PushAsync(..., CancellationToken.None)` for awaited network IO; `PackageConsumerSmokeTask` calls `EnsureSmokeCsprojsMatchManifestScopeAsync(..., default)` and `ProjectMetadataReader.ReadAsync(smokeProject)` / `IDotNetRuntimeEnvironment.ResolveAsync(...)` without forwarding ct. ~6-8 LOC patch across 2 files. Pre-S15 the surrounding `PublishPipeline.RunAsync(..., CancellationToken)` did thread ct; the re-inline path lost it. **Real correctness gap on new code, not pre-existing.**
- **Catch-all `Exception` narrowing** (C4+C5+C6, pre-existing): `BinaryClosureWalker.BuildClosureAsync` uses `#pragma warning disable CA1031` + `catch (Exception ex)` to convert every error into a typed Result. Masks programmer bugs (NRE, OOM, contract violations). File-scope comment in BinaryClosureWalker explicitly defers ("Narrowing to operational types … changes pre-migration behavior; deferred to a follow-up slice"). Tracked here so the deferral surfaces.
- **Phase-ID-in-comment violation** (B5): `Host/ServiceCollectionExtensions.cs:50-53` IRuntimeProfile factory comment says "VcpkgConfiguration retired in S15 (P9)". ADR-002 §10 + AGENTS.md "Phase numbers, ADR references, and migration timing belong in canonical docs, not logic-bearing comments". Trim to keep only the rule explanation.
- ~~**Vcpkg package-info provider test migration** (A7+E8)~~ — closed after the package-info provider was absorbed into `Build.Tools.Vcpkg`: the old provider tests were deleted, typed parser/alias tests use `FakeCakeWorld.WithProcessResult`, and the fixture now initializes redirected stdout/stderr streams consistently.
- **PackageConsumerSmokeTask god-service smell** (A10, B8, B9, C7, D4): 10 ctor dependencies; `RunAsync` ~100 LOC with `MA0051` suppression and a chain of private helpers (`ResolveSmokePackages`, `EnsureSelectionSupportsCurrentSmokeScope`, `EnsureSmokeCsprojsMatchManifestScopeAsync`, `EnsurePackageArtifactsExist`, `RunCompileSanity`, `RunSmokeForTfm`, `AppendNet4xPlatformArgument`, `AppendFeedArguments`, `AppendSmokePackageVersionProperties`, `ShouldSkipTfm`, `DeleteDirectoryIfExists`). Boss-fight self-review (Phase 4.11) accepted these as task-owned story per ADR §5. Re-extraction candidates: `SmokePreflightChecker` collaborator for the ensure-* helpers; `Net4xRuntimeSkipPolicy` for `ShouldSkipTfm` (D5) — would symmetrize the cohort with existing probe/comparator/runner/reporter peers. `PrepareWorkspace` extraction (B8) for the bin/obj/workingRoot purge block to remove the MA0051 suppression. Defer until pressure justifies (post-P10 cleanup pass).
- **`SmokePackage` nested record promotion** (B7): `private sealed record SmokePackage` with `FromFamily` factory does cross-family identifier derivation (managed/native/version-property names from `FamilyIdentifierConventions`). ADR §4 `Models/` subfolder vocabulary suggests promotion to `Targets/PackageConsumerSmoke/Models/SmokePackage.cs`. Trivial relocation; fold into the post-P10 model-folder normalization pass.
- **`DotNetSmokeRunner` method collapse** (D1): `RunCompileSanity` and `RunSmokeForTfm` differ only by an optional environment-variables dictionary. Two public entry points add no behavioral discrimination. Collapse to single `Run(description, args, workingDir, env = null)`. Cohort symmetry win.
- **`PublishStagingTask` auth-token resolver extraction** (D6): `ResolveAuthToken` (env-var chain probing) is reusable across publish flows (PublishPublic stub will need it). Re-inlining was an explicit ADR §5 design choice this slice, but the env-var chain is an isolated policy. Promote to `IGitHubAuthTokenResolver` peer of `INuGetFeedClient` when PD-7 (PublishPublic real implementation) lands.
- ~~**`Host/Configuration/RepositoryConfiguration` namespace residue** (B6)~~ — closed in S16 (P10): `PathService` ctor accepts `DirectoryPath repoRoot` directly, `AddHostBuildingBlocks(parsedArgs, repoRoot)`, `Host/Configuration/` retired entirely.
- **Lost regression coverage on `--project` argument shape** (E NIT): A retired PackageConsumerSmoke test asserted `dotnet test --project <path>` argument shape. The current happy-path scenario only checks "test" + TFM appear somewhere in args — loses the regression. Add an explicit `Arguments.Contains("--project", StringComparison.Ordinal)` assertion to the happy-path scenario, or a dedicated argument-shape unit test on `DotNetSmokeRunner`.
- **Test discipline polish cluster** (E1-E7 + NIT cluster): `BuildScopeManifest` dead helper, happy-path cardinality assertions missing, `NewWorld(feedClient)` unused param, verbose `Arg.Is<T>(t => t == x)` patterns, fully-qualified `LogLevel` repeats, MonoAvailabilityProbe whitespace-PATH edge case, "When_Happy_Path" test naming convention drift, PublishPublic test missing exception-type assertion, validator separate Code+Message asserts (couple them), "3 TFMs" pluralization brittleness. Pure polish; bundle with the next test-infrastructure pass.
- **C# code-quality SUGGEST cluster** (C7-C20 + NIT): `ShouldSkipTfm` `out string` → `(bool, string)` tuple, `SmokeScopeComparator.Descendants()` typed-query optimization, `ReadmeMappingTableBlock.BuildBlock` StringBuilder + LF/CRLF inconsistency, `BinaryClosureWalker.MatchesPattern` silent failure on multi-`*` patterns, `AuthEnvVarChain` shape (ImmutableArray vs string[]), `ResolveConcreteFamiliesInScope` O(N·M) → Dictionary lookup, `MonoAvailabilityProbe` doesn't recognize `mono.exe` on Windows (acceptable since caller gates non-Windows but probe class reads platform-agnostic). Heterogeneous polish; address per-file as next slices touch them.
- ~~**Stale `ArchitectureTests` cleanup** (A8)~~ — closed in S16 (P10): file deleted entirely per ADR-002 §14.

### General-Purpose Reporter Infrastructure

- Status: `parked` (post-P8 design)
- 4 reporters now share a recurring shape: `LogStarting`, `StartLibrary(name)`, `FinishLibrary(name, ...)`, `ReportLibraryFailure` / `ReportPhaseFailure`, `LogCompleted`. PreflightReporter (ICakeContext-only) is the outlier; HarvestReporter + PackageReporter + ConsolidateHarvestReporter share IAnsiConsole + ICakeLog cohort.
- Preserve:
  - Rule of three has triggered. If a fifth reporter lands (P9 PackageConsumerSmoke or Publish surface), evaluate `IReporter` base or shared `RuleRenderer`.
  - HarvestReporter has 9 public methods — design §15 risk register cap is 10. Watch the next addition.

### Path / File CLI Option Drift

- Status: `parked` (post-P8 cleanup item)
- BuildContext CLI properties expose `VersionsFilePath` (FilePath), `ResolveVersionsScope` (IReadOnlyList<string>), `ExplicitVersionEntries`, etc. — typed where it matters but inconsistent shape across `--versions-file` (FilePath) vs `--scope` (string list) vs others. After P9/P10, audit BuildContext property types for cohort consistency + consider extension methods on BuildContext for common "resolve to canonical FilePath" shapes.

## Planned Operational Features

### Known-Issues Skip List

- Status: `planned`
- Skip known-bad `library/version/RID` combos in CI instead of repeatedly burning time on predictable failures.
- Intended artifact: `build/known-issues.json`.
- Preserve:
  - Shape for keyed entries (`library/version/RID` or equivalent).
  - CI behavior: skip vs warn vs hard-fail.
  - Expiry / revalidation policy so the list does not become permanent sediment.
  - Guidance for proving an item can be removed after upstream fixes.

### Recovery Procedures (beyond PD-8 manual escape)

- Status: `planned`
- Package yanking strategy (internal + public feeds).
- Internal / public feed rollback procedures.
- Temporary artifact backups during publish.

### Maintenance Mode / Health Checks

- Status: `planned`
- Scheduled feed health checks.
- Stale package validation.
- `known-issues.json` drift detection.
- Recovery time benchmarking.

## Hardening Backlog

### Invariant-Culture Logging Hygiene

- Status: `hardening-backlog`
- Preserve:
  - Keep numeric / date logging culture-invariant anywhere the build host prints metrics or timestamps.
  - If invariant-format logging appears in multiple tasks, factor a tiny helper instead of repeating ad hoc `string.Create(CultureInfo.InvariantCulture, ...)` shapes.

### Optional Coverage Signal

- Status: `parked`
- ADR-002 §14 left the door open for coverage to return later as an optional non-blocking signal (not a build-host target concern). The Cake-owned `Coverage-Check` gate retired in S10 (2026-05-08); coverage instrumentation came out of CI entirely.
- Preserve:
  - Re-introduce only when there is concrete motivation (PR-comment summary, dashboard surface, external SaaS, etc.) and a deliberate design.
  - Do NOT reintroduce as a build-host Cake target — that shape was rejected. If it returns, it returns as a CI-side signal owned by the workflow, not a build-host gate.
  - Re-add must include the rationale and the surface it serves; otherwise it stays parked.

### OneOf-Shaped Result Types (Surviving Post-S13)

- Status: `parked`
- ADR-002 §11 retires "OneOf-style result hierarchies for expected build failures" in favor of `Result<T, TError>` (binary) or `ValidationReport`/`ValidationCheck` (multi-check). S11 retired 5 strategy-related OneOf result types and demonstrated the collapse pattern. S12 (P6 PreFlight migration, 2026-05-08) retired 5 more: `VersionConsistencyResult`, `CoreLibraryIdentityResult`, `CsprojPackContractResult`, `UpstreamVersionAlignmentResult`, `PreflightError`. S13 (P7 Package migration, 2026-05-09) retired additional package-stage result wrappers; current package flow now uses direct Cake pack invocation plus reporter-owned diagnostics, `ProjectMetadataResult` became `Result<ProjectMetadata, ProjectMetadataError>`, and package validation collapsed to `ValidationReport` along with `PackageValidation` / `PackageValidationCheck` / `GuardrailKind` enum — guardrail IDs now live in `ValidationCheck.Code` strings; reporters format as `[Gnn] message`.
- Preserve:
  - One OneOf-shaped result type survives post-S14: `PackageInfoResult` (vcpkg integration boundary). S14 retired `ArtifactPlannerResult`, `ClosureResult`, `CopierResult` as part of P8.
  - `PackageInfoResult` retires within P9 ConsumerSmoke / vcpkg integration relocation; OneOf package dependency removal lands at P10 final cleanup.
  - The OneOf package dependency stays in `Build.csproj` and `Directory.Packages.props` until all surviving types retire (likely P10 final cleanup).
  - The collapse pattern: OneOf-shaped `Result<TError, TSuccess>` → either `ValidationReport` (multi-check with severities) OR `Build.Results.Result<T, TError>` record struct (binary success/failure) per ADR-002 §11.

### Performance And Caching

- Status: `hardening-backlog`
- Caching for expensive package-info queries; smarter reuse of repeated filesystem or dependency-analysis work.

### Tool Reproducibility

- Status: `hardening-backlog`
- Explicit tool-path selection where environment drift matters; reproducible CI vs local tool resolution guidance.

### Release CI Log Hygiene

- Status: `hardening-backlog`
- Surfaced during inspection of green Release workflow run `25637587638`. No failing job or hidden test failure was found, but several noisy log patterns are worth cleaning up so future failures are easier to spot.
- Preserve:
  - `actions/download-artifact@v8` emits repeated `[DEP0005] Buffer()` deprecation warnings. Likely upstream/action noise, but track in case an action upgrade or pin can remove it.
  - Build-host invocations repeatedly warn that `--vcpkg-dir` and `--vcpkg-installed-dir` were not specified before defaulting to repository-relative paths. Either pass explicit paths from CI or downgrade expected default-path messages.
  - NativeSmoke CMake configure warns that `VCPKG_OVERLAY_TRIPLETS` is manually specified but unused on every RID. Check whether the variable is unnecessary for the native-smoke CMake project.
  - Windows vcpkg setup downloads PowerShell 7.5.4 during compiler hash detection. Consider caching/preinstalling if this remains repeated runtime noise.
  - linux-x64 Harvest logs `Package info not found for dependency gperf, continuing.` for each SDL family. Harmless in the green run, but it may deserve an explicit exclusion or clearer diagnostic.

### PreflightReporter `IAnsiConsole` Migration

- Status: `parked`
- `PreflightReporter` (`Targets/PreFlightCheck/Reporting/PreflightReporter.cs`) constructor-injects `ICakeContext` and uses `_cakeContext.Log.Information/Error` for all output. Migrated targets that own visual output (`InfoTask`, `OtoolAnalyze`, the diagnostic targets) consume `IAnsiConsole` for richer Spectre tables/panels/colors.
- Surfaced during S12 (P6 PreFlight migration, 2026-05-08). Deniz's note: *"using logging :D We can use IAnsiConsole here, but definitely not right now."* Slice scope was migration only; visual upgrade deferred.
- Preserve:
  - When the broader build-host audit decides on `IAnsiConsole`-everywhere, sweep PreflightReporter alongside any other `ICakeLog`-static-style consumers in migrated/relocated code.
  - Format upgrade likely also touches `PackagePipeline`, `HarvestPipeline`, etc. — easier as a single visual-consistency pass than per-slice.

### Validation/ Namespace Duplication With Build.Versioning

- Status: `parked`
- Two namespaces named "Versioning" coexist post-S12: `Build.Versioning` (root concept — `PackageFamilyId`, `PackageFamilyVersionSet`, `ExplicitVersionParser`) and `Build.Validation.Versioning` (alt-folder under Validation — Upstream + CrossFamily validators).
- Both names accurately describe their domain (the validation alt-folder validates *versions*; the root concept *defines* version primitives) but the import-statement neighborhood gets confusing.
- Preserve:
  - Re-read post-P10 once the rest of the build-host migration settles. Candidates: rename Validation alt-folder (`Validation/VersionAlignment/`?), rename root concept (`Build.PackageFamilies`?), or accept both names.
  - No migration during S12 — both names defensible; folder churn during ongoing target migrations would be more expensive than a single post-refactor reorganization slice.

### PreFlightCheckTask Ctor Style Inconsistency

- Status: `parked`
- `PreFlightCheckTask` constructor declares 11 primary-ctor parameters AND 11 explicit `private readonly` fields with `ArgumentNullException.ThrowIfNull` checks. Sibling targets (`PreflightReporter` line 9, `OtoolAnalyzeTask`, etc.) use the primary-ctor-as-capture pattern (no field declarations, params used directly in method bodies).
- Surfaced during S12 audit (P6 PreFlight migration, 2026-05-08). Decision was to keep the verbose null-check style for the cross-cutting validation gate; style inconsistency acknowledged but not resolved.
- Preserve:
  - Candidate for an ADR-002 §8 amendment or a separate build-host coding-style guideline document: when do DI-injected collaborators warrant defensive null-checks vs. primary-ctor-as-capture?
  - Probably resolves naturally during P10 final cleanup when the broader code-style sweep settles.

### Validator Analyzer Suppressions Closure Audit

- Status: `parked`
- During S12 brainstorming, a backlog item captured concern about `CA1822` / `S2325` `[SuppressMessage]` attributes on validators that could-be-static. The interface-introduction decision (all 7 validators implement `IFoo` per the in-flight Deniz clarification) made this moot — interface implementations cannot be marked static, so the analyzers are silently satisfied. **No `[SuppressMessage]` attributes remain in `Validation/`** as of S12.
- Item kept here for documentation closure: confirm during P10 cleanup that no `Validation/` class accumulates `CA1822` / `S2325` `[SuppressMessage]` attributes during normal evolution.

### PreFlightCheckTask Sync vs Async Signature

- Status: `hardening-backlog`
- `Targets/PreFlightCheck/PreFlightCheckTask.cs` derives from `AsyncFrostingTask<BuildContext>` and overrides `Task RunAsync(BuildContext)` but the body is fully synchronous (returns `Task.CompletedTask`). Surfaced during S12 cold review. False async affordance for future maintainers; signature implies IO that doesn't exist.
- Preserve:
  - Switch to `FrostingTask<BuildContext>` (sync) when next touched, OR keep `AsyncFrostingTask` and add a code comment explaining the intentional sync-only design.
  - If any future validator requires async IO (e.g. P10 feed-probe surface in G58), the signature is already correct — keeping it is defensible.
  - Decide alongside the broader "build-host async surface" pass.

### PackageFamilyVersionSet API Refinement

- Status: `hardening-backlog`
- Surfaced during S12 multi-agent review. `Versioning/PackageFamilyVersionSet.cs` ships several refinement opportunities flagged by independent reviewers:
  - `IReadOnlyCollection<PackageFamilyVersion>` interface boundary may be the wrong abstraction — primary consumer pattern is lookup (`TryGetVersion`, `RequireVersion`, `Contains`), not iteration. Drop the interface OR justify why both shapes are needed.
  - `GetEnumerator()` rebuilds a LINQ projection (`Families.Select(...)`) on every call. Cache an `IReadOnlyList<PackageFamilyVersion>` at construction; allocation-free iteration.
  - `GetHashCode()` iterates `Families` and performs two dictionary lookups per element. Iterating `_versions` directly is O(n) with no extra lookups.
  - `Equals(PackageFamilyVersionSet?)` could short-circuit on `Count` mismatch before allocating any keys.
- Preserve:
  - Pure refinements; no functional defect today (set is immutable, comparisons + iteration both correct).
  - Worth a P10 sweep alongside any other foundation-type performance review.

### `tools.cs` Command Naming Clarity (build vs setup vs ci-sim)

- Status: `parked`
- Surfaced post-S12 (2026-05-09). `tools.cs` exposes three subcommands with conceptually distinct roles:
  - **`build`** — Cake passthrough escape hatch. Forwards args to Cake Frosting raw (e.g., `tools.cs build --target Info`, `tools.cs build --tree`). Doesn't actually BUILD anything by itself.
  - **`setup [--source local|remote-github|remote-nuget] [--no-clean]`** — Local-dev environment bootstrap. Cleans `artifacts/`, runs Cake stages `ResolveVersions → PreFlightCheck → EnsureVcpkgDeps → Harvest → ConsolidateHarvest → Package`, verifies the family nupkgs land, writes `Janset.Local.props` so consumer csprojs pick up local versions. **Actual local-dev setup workflow.**
  - **`ci-sim [-v]`** — Full CI replay (8-stage pipeline + per-step logs). Mirrors `release.yml`.
- Confusion: `build` is misleading — it doesn't compile or pack on its own; it's just "run a Cake target with these args". The clearer names would be:
  - **Option A: rename `build` → `cake`** — honest about what it is. Self-documenting at the CLI layer. Costs: `tools.cs:45` rename + `CLAUDE.md` examples + 2 historical priming prompts under `.github/prompts/` reference `build --tree`. ~5 sites.
  - **Option B: rename `build` → `target`** — `tools.cs target --target Info` reads redundantly. Worse than A.
  - **Option C: keep `build`, add `cake` as alias** — backward-compatible but two names for the same thing.
  - **Option D: doc-only clarification** — keep names, add a one-liner to `CLAUDE.md` "Common Commands" header explaining the layering. Smallest change.
- Preserve:
  - Decision pending. Worth bundling with a future Phase X command-surface review (alongside `tools.cs setup --source=remote-nuget` PD-7 wiring + any new commands).
  - If renaming, update `CLAUDE.md`, `tests/smoke-tests/README.md` (line 49 references `setup --source=local`), `README.md` (line 110), and any historical priming prompts that reference `build --tree`.

### Vcpkg Cache Key Mutability + CI Cache Audit

- Status: `hardening-backlog`
- Surfaced post-S12 (2026-05-09). Two distinct concerns surfaced together:
  1. **Cache invalidation cost** (confirmed empirically): every time something busts the cache key (vcpkg.json edit, overlay-triplet/port edit, vcpkg submodule bump), the next release pipeline pays ~18-20 min cold-build cost. Recent example: run `25550425138` (post-S11 retirement on `392c35e`) was a cold MISS (`Cache not found for input keys: vcpkg-bin-windows-2025-...`, `Restored 0 package(s)`, all 29 packages compiled from source). Total pipeline 30 min vs the next run on the same key (run `25575839739`) which HIT cleanly and finished in 12 min. Pattern repeats whenever `vcpkg-overlay-triplets/**` or `vcpkg-overlay-ports/**` content changes. The S11 strategy retirement touched overlay triplets, busting the cache.
  2. **Silent-recompile risk** (latent, not yet observed): cache key includes `windows-2025` / `macos-15-intel` / `macos-26` / `windows-11-arm` (mutable runner labels) but NOT runner image version or compiler ABI hash. If GitHub patches MSVC / Apple updates Xcode CLT / Linux container content shifts, the GH Actions cache HIT can be a false positive while vcpkg's compiler-ABI hash differs, forcing 29-package recompile (5-30 min) WITHOUT any "cache miss" log indicator. Deniz reports having hit this in past projects.
- Cache key composition (current — `vcpkg-setup` action.yml lines 122-135):
  - `vcpkg-bin-{platform-identity}-{triplet}-{hashFiles(vcpkg.json + overlay-triplets/** + overlay-ports/**)}-{vcpkg submodule SHA}`
  - `platform-identity` = container digest for Linux (immutable) OR raw runner label for Windows + macOS (mutable)
- Concrete fixes (Phase X "CI cache audit" candidate):
  1. **Include runner image version in cache key for Windows + macOS** — guard against silent MSVC / Xcode-CLT patches. Inject the runner image version (e.g., from `Image Release` field in the job log header, or `runner.image_version` if exposed). Busts cache deterministically on runner-image rolls.
  2. **Cache `external/vcpkg/downloads/` alongside `.vcpkg-cache`** — Windows runs currently re-download CMake 4.2.3 (~9s) and PowerShell 7.5.4 (~22s) every run because vcpkg's vendored tools live outside the cache path. Adding the downloads folder saves ~31s per Windows run × 3 Windows RIDs = ~1.5 min total per release pipeline.
  3. **Pre-warm the cache on master push** (separate trigger): a workflow that runs `vcpkg-setup` only on every master push primes the cache for subsequent release pipelines. Costs one cold-build-equivalent run after a cache-busting commit; saves all subsequent release pipelines from paying it.
  4. **Consider matching Linux's container-digest approach for Windows** — Linux already uses `ghcr.io/janset2d/sdl2-bindings-linux-builder@sha256:...` (immutable digest). Windows can't easily container-fy, but baking a Windows runner image with prebuilt vcpkg + CMake + PS7 would be equivalent at higher infra cost.
- Other variance hotspots from the same investigation (separate from caching):
  - **macOS `platform-build-prereqs` (42-63s)** — `brew install` of build tooling on every run. Cache `/opt/homebrew` keyed on a brewfile hash. Largest single optimization win across all platforms.
  - **Windows checkout submodule init (28-50s)** — `submodules: false` + separate `git submodule update --init --depth=1 external/vcpkg` step. Saves ~10-15s.
  - **Defender exclusion for Windows `.vcpkg-cache/`** — `Add-MpPreference -ExclusionPath` could shave ~5-10s off NTFS extraction.
- Preserve:
  - Run `25575839739` had cache key working correctly (cold-build paid by previous run `25550425138`).
  - Real symptom: ~18-min penalty per cache-busting commit. Confirmed empirically. Worth a dedicated "CI cache audit" Phase X slice once P7-P9 boss fights settle.

### Windows Runner Architecture Plan (3 CPU architectures)

- Status: `parked` (current setup is correct; preserve for cache key plan)
- Surfaced post-S12 (2026-05-09). Manifest maps Windows RIDs to runners as:
  - `win-x64` → `windows-2025-vs2026` (Intel x64 native; migrated from `windows-2025` 2026-05-09 ahead of 2026-05-12 GitHub deprecation) ✅
  - `win-arm64` → `windows-11-arm` (Windows 11 Desktop ARM64 image, native ARM hardware) ✅
  - `win-x86` → `windows-2025-vs2026` (Intel x64 host cross-compiling x86 via MSVC `/arch:IA32`; migrated 2026-05-09) ✅
- Verification per public docs:
  - Windows arm64 runners GA Sep 2024 ([changelog](https://github.blog/changelog/2024-09-03-github-actions-arm64-linux-and-windows-runners-are-now-generally-available/)); public-repo preview Apr 2025 ([changelog](https://github.blog/changelog/2025-04-14-windows-arm64-hosted-runners-now-available-in-public-preview/)); private-repo standard Jan 2026 ([changelog](https://github.blog/changelog/2026-01-29-arm64-standard-runners-are-now-available-in-private-repositories/)). Label: `windows-11-arm`, 4 vCPUs free in public, Windows 11 Desktop image with full toolchain.
  - **No native Windows x86 (32-bit) hosted runner exists.** GitHub Actions only ships 64-bit Windows. x86 builds must cross-compile from x64 host using MSVC's vendored x86 toolchain (vcpkg's `x86-windows-hybrid` triplet handles this transparently). Confirmed via vcpkg discussions and CI cross-build write-ups.
- Verdict: **current mapping is correct for all 3 Windows architectures.** Deniz's intuition was half-correct (win-x86 IS x64-host cross-compile) and half-incorrect (win-arm64 is native, not x64-host).
- Pending action items (next CI cache audit slice):
  1. **Pin runner image versions explicitly** for cache-key stability (see Vcpkg Cache Key Mutability entry above). Per-arch consideration: each Windows runner label rolls independently — `windows-2025-vs2026` patches affect both win-x64 and win-x86 (same cache key bust); `windows-11-arm` patches affect only win-arm64.
  2. **No need to add native x86 runners** — they don't exist on GitHub Actions and cross-compile from x64 is the standard pattern. vcpkg's binary cache works correctly across host/target arch since the cache key includes the target triplet.
  3. **Optional: investigate larger ARM64 runner** — public-repo ARM64 runners ship 4 vCPUs; if private-repo conversion happens later, larger runners (8/16/32 vCPU) may speed Harvest on win-arm64 (currently 1m38s-2m18s).

## Packaging, Supply Chain, And Release Detail

### SBOM Generation

- Status: `planned`
- Software bill of materials generation for native and managed artifacts.

### Native Symbol Handling

- Status: `planned`
- Managed `.snupkg` publication is live. Deferred: native symbol handling strategy (per-platform `.pdb` / `.dSYM` / `.debug` payloads + symbol server publish).

### PackageTask HEAD SHA Resolver Func vs Interface

- Status: `parked`
- `PackageTask` carries an optional `Func<ICakeContext, DirectoryPath, string>` ctor hook for HEAD SHA resolution. Default impl wraps `context.GitLogTip(repoRoot)` (Cake.Frosting.Git → LibGit2Sharp). Cake.Git bypasses `ICakeContext.FileSystem` and hits System.IO directly, which means `FakeFileSystem`-backed scenario tests can't be served by the default — they inject a stub lambda via DI registration.
- Surfaced during S13 (P7 Package migration, 2026-05-09). Considered extracting `IGitHeadResolver` for uniformity but kept the Func as the minimal change. Revisit if a second consumer appears (e.g., Harvest-time or Publish-time SHA stamping).
- Preserve:
  - Func vs interface is a small surface, not load-bearing on the migration. The current shape works and tests it via DI registration.
  - When P10 retrospects on cross-target stamping needs, decide whether `IGitHeadResolver` (in a `Git/` named concept) earns its existence.

### PackageFamilyPacker Ctor Style Consistency (S13 carry-forward)

- Status: `parked`
- Same shape question as the existing `PreFlightCheckTask` ctor style item: `PackageFamilyPacker` declares 10 explicit `private readonly` fields with `ArgumentNullException.ThrowIfNull` checks. `PackageTask` uses an explicit ctor with field declarations too (because of the optional Func hook). Sibling targets like `OtoolAnalyzeTask` use the primary-ctor-as-capture pattern.
- Surfaced during S13 (P7 Package migration, 2026-05-09). Kept the verbose null-check style for consistency with PreFlight and to support the optional Func parameter clearly.
- Preserve:
  - Roll into the same future ADR-002 §8 amendment / build-host coding-style guideline that addresses PreFlightCheckTask. A single sweep across migrated tasks will be cleaner than per-slice decisions.

### S13 Reviewer Follow-Ups (Code/Test Cleanup)

- Status: `parked`
- Multi-agent code review on the S13 staged diff (7 reviewers: full-context architect, necessary-context architect, two unbiased architects, QA test specialist, plus two external reviewers) consensus surfaced findings beyond the cleanup that landed in S13. Address in a future cleanup slice; not blocking shipping.
- Preserve:
  - **Cross-family dependency lower-bound semantics (T3.1) — verify intent.** `DependencyRangeNormalizer` writes the *satellite's own* version as the lower bound of its dependency on the core family (e.g. sdl2-image=2.8.0 → emits sdl2-core dep range `[2.8.0, 3.0.0)`, not `[2.32.0, 3.0.0)`). This is *existing pre-S13 behavior preserved*, but Agent 2 raised whether it's intentional G56 policy or latent bug. Verify against `release-guardrails.md` G56 intent and either document the policy explicitly or fix to use the dependency family's resolved version (would change package output → behavior risk).
  - **HarvestReadinessValidator validator-vs-gate naming (T3.2).** Lives in `Validation/Packaging/`, named `…Validator`, but throws `CakeException` (gate semantics) instead of returning `ValidationReport` like the rest of the cohort. Two options: (a) rename to `HarvestReadinessGate` + relocate to `Targets/Package/Services/` (carve-out from ADR §8 amendment), or (b) change to return `ValidationReport` and have `PackageFamilyPacker` translate to throw at the boundary. (b) brings it into cohort consistency with sibling Packaging validators but requires reshaping the 7 unit tests' `ThrowsAsync` assertions.
  - **NIT cluster (Tier 4 from review).**
    - "(post-C feed-probe wiring)" phrase in `PackageTask` G58 error message leaks an internal phase reference to operator output — strip to "pass `--feed <URL>`".
    - `Build.Results.Unit.Value => default` could be `public static readonly Unit Value;` (zero-init field; one IL load instead of getter call on every Result construction).
    - Fully-qualified type names inline in `PackageFamilyPacker.cs` (`Build.Validation.Conventions.FamilyIdentifierConventions`, called twice) and inline `Build.Versioning.PackageFamilyVersionSet` in `FakeCakeWorld.cs` — add `using` statements instead.
  - **QA test debt (Tier 5 from review).** Highest-leverage items:
    - `Scenarios/Package/PackageTaskScenarioTests.RunAsync_Should_Pack_All_Selected_Families_When_Happy_Path` has anemic orchestration assertions — assert dotnet pack process invocations per family, verify topological pack order (sdl2-core before sdl2-image), and verify `headSha` propagates to `nativeMetadataGen.GenerateAsync(family, version, headSha, ct)`.
    - `Unit/Validation/Packaging/HarvestReadinessValidatorTests.EnsureReadyAsync_Should_Return_When_All_Gates_Pass` only asserts "no throw" — assert on the success-path `_log.Information("...will pack harvest payload for successful RIDs:...")` line and add coverage for the `DivergentLicenses.Count > 0` warning branch (currently 0% covered).
    - `Unit/Targets/Package/Services/PackageFamilyPackerTests` lacks tests for `dotnet pack` failure (native + managed) and `IProjectMetadataReader` failure paths the Packer explicitly handles. Add 2-3 tests stubbing `Result<>.Failure` from those collaborators and asserting on the `CakeException` "See log." anchor + reporter-emitted log line.
    - Two payload-subtree-missing tests in `HarvestReadinessValidatorTests` are indistinguishable on assertion (both check `"harvest payload directory" + "is missing"`). Tighten to assert on the unique path component (`runtimes` vs `_consolidated`).
    - `DependencyRangeNormalizer` — 3 throw branches uncovered (nuspec entry missing per S13 cleanup adds a new check too, library_ref missing in manifest, vcpkg_version invalid).

### Features/Packaging/ServiceCollectionExtensions ConsumerSmoke-only Half

- Status: `parked`
- After S13 (P7 Pack migration), `Features/Packaging/ServiceCollectionExtensions.AddPackagingFeature()` registers only ConsumerSmoke-side artifacts (`IPackageConsumerSmokePipeline`, generators consumed by both Pack and ConsumerSmoke). The Pack side moved to `Targets/Package/ServiceCollectionExtensions.AddPackage()`. The split surface is asymmetric — `AddPackagingFeature` retains a name that no longer fits its content.
- Surfaced during S13 (P7 Package migration, 2026-05-09). Renaming + restructuring deferred to P9 (ConsumerSmoke migration), at which point `Features/Packaging/` should retire entirely.
- Preserve:
  - Decide rename / structure during P9 migration. Likely outcome: `AddPackagingFeature` retires; ConsumerSmoke registrations move into `Targets/PackageConsumerSmoke/ServiceCollectionExtensions.AddPackageConsumerSmoke()`. Generators (`INativePackageMetadataGenerator`, `IReadmeMappingTableGenerator`) split between Pack and ConsumerSmoke based on actual consumer.

## Retention Rule

Retired material disappears only after the useful parts are either:

- moved into canonical docs ([`plan.md`](plan.md), phase docs, playbook, knowledge-base), or
- preserved here as an intentionally parked thread, or
- explicitly rejected (with the why captured).
