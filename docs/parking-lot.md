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

- Status: `parked`
- Refactor seams identified in earlier reviews; not active work.
- Preserve as pressure-tested directions only when real change volume justifies them:
  - Splitting `BinaryClosureWalker` into primary-binary resolution / package-dep walking / binary scanning / file classification roles.
  - Extracting `SystemFileFilter` if `RuntimeProfile` keeps accumulating non-profile logic.
  - `HarvestPipeline` orchestration extraction ([#87](https://github.com/janset2d/sdl2-cs-bindings/issues/87), deferred).

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

### OneOf-Shaped Result Types (Surviving Post-S12)

- Status: `parked`
- ADR-002 §11 retires "OneOf-style result hierarchies for expected build failures" in favor of `Result<T, TError>` (binary) or `ValidationReport`/`ValidationCheck` (multi-check). S11 retired 5 strategy-related OneOf result types and demonstrated the collapse pattern (`ValidationResult`/`ValidationError`/`ValidationSuccess` → `ValidationReport`). S12 (P6 PreFlight migration, 2026-05-08) retired 5 more: `VersionConsistencyResult`, `CoreLibraryIdentityResult`, `CsprojPackContractResult`, `UpstreamVersionAlignmentResult`, `PreflightError` — each replaced by direct typed `*Validation` returns or, for `PreflightError`, deletion outright (no replacement; `BuildError` survives for `HarvestingError` / `PackagingError`).
- Preserve:
  - Seven OneOf-shaped result types survive post-S12: `PackageInfoResult`, `DotNetPackResult`, `ProjectMetadataResult`, `ArtifactPlannerResult`, `ClosureResult`, `CopierResult`, `PackageValidationResult`.
  - Each retires within its respective target migration (P7/P8/P9) when that target's pipeline gets reshaped.
  - The OneOf package dependency stays in `Build.csproj` and `Directory.Packages.props` until all surviving types retire (likely P10 final cleanup).
  - The collapse pattern: OneOf-shaped `Result<TError, TSuccess>` → either `ValidationReport` (multi-check with severities) OR a homegrown `Result<T, TError>` record struct (binary success/failure) per ADR-002 §11.

### Performance And Caching

- Status: `hardening-backlog`
- Caching for expensive package-info queries; smarter reuse of repeated filesystem or dependency-analysis work.

### Tool Reproducibility

- Status: `hardening-backlog`
- Explicit tool-path selection where environment drift matters; reproducible CI vs local tool resolution guidance.

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

### CsprojPackContractValidatorTests Pending V2 Migration

- Status: `hardening-backlog`
- Surfaced during S12 multi-agent review (P6 PreFlight migration, 2026-05-08). Per `testing-guidelines.md` "Touched tests migrate to V2", the relocated `Unit/Validation/Manifest/CsprojPackContractValidatorTests.cs` should have moved to V2 fixtures. It still constructs `FakeRepoBuilder` (V1) and pulls `handles.FileSystem` directly. Mechanical relocation happened in S12 but body conversion was deferred to keep the slice scope bounded.
- Preserve:
  - When next touched (e.g. during P7 Package boss-fight or a dedicated test-infra cleanup pass), rewrite to construct `FakeCakeWorldV2.CreateWindows()` + seed csproj XML via `WithTextFile`, then pass `world.CakeContext.FileSystem` (`IFileSystem`) into the validator under test.
  - Drop the V1 `FakeRepoBuilder` dependency for this file entirely.
  - ~50-100 LOC test rewrite; deferred deliberately to avoid slice scope creep.

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

## Packaging, Supply Chain, And Release Detail

### SBOM Generation

- Status: `planned`
- Software bill of materials generation for native and managed artifacts.

### Native Symbol Handling

- Status: `planned`
- Managed `.snupkg` publication is live. Deferred: native symbol handling strategy (per-platform `.pdb` / `.dSYM` / `.debug` payloads + symbol server publish).

## Retention Rule

Retired material disappears only after the useful parts are either:

- moved into canonical docs ([`plan.md`](plan.md), phase docs, playbook, knowledge-base), or
- preserved here as an intentionally parked thread, or
- explicitly rejected (with the why captured).
