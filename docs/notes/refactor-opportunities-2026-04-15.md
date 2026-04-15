# Refactor Opportunities — Collected 2026-04-15 (Coverage Ratchet Session)

> **Purpose.** Running tally of best-practice / quality-of-life improvements noticed while landing the coverage ratchet (#86). None of these block current work; they are candidates for a dedicated refactor tour, to be scheduled separately.
>
> **Status.** Raw notebook. No commitments attached. Items may be promoted to canonical docs, issues, or parking-lot entries once Deniz triages them.

## Notebook Entries

### 1. `ICakeLog` has no culture-aware overload

**What.** `ICakeLog.Information(string format, params object[] args)` does not accept a `CultureInfo`. `context.Log.Information(CultureInfo.InvariantCulture, "...", args)` does not compile.

**Impact.** Default-culture formatting leaks into log output: a Turkish host prints `62,62%` where an invariant host prints `62.62%`. CI parsers that key off log lines are exposed to host locale.

**Current state in Coverage task.** Worked around with `string.Create(CultureInfo.InvariantCulture, $"...")` inside `CoverageCheckTask.LogMetrics`. Clean, idiomatic C# 10+, but only one task uses it.

**Opportunity.** Audit every `context.Log.*("{0:F2}", value)` call across the build host; standardize on `string.Create` + `InvariantCulture` where numbers or dates are formatted. Could be a small helper (`InvariantLog.Write`) to keep call sites terse.

### 2. `ThrowIfError` is misnamed on result monads

**What.** `ValidationResult.ThrowIfError(Action<ValidationError>)`, `ClosureResult.ThrowIfError(Action<HarvestingError>)`, and now `CoverageCheckResult.ThrowIfError(Action<CoverageError>)` all just invoke the action when the result is an error. They do not throw themselves; the caller's lambda is expected to throw.

**Impact.** Reading `result.ThrowIfError(...)` suggests "this method throws on error," when actually the contract is "this method calls my handler on error, which *might* throw." Confusing at call sites; easy to write a handler that logs but forgets to throw.

**Opportunity.** Rename to `OnError` (or `InvokeOnError` / `IfError`) across all three result monads. Consider a companion `ThrowOnError` that actually throws a provided exception (so the "I want throw semantics" use case is explicit). One cross-module rename — low risk because it is internal surface.

### 3. Synchronous `ReadToEnd` forces `#pragma warning disable MA0045`

**What.** `CoberturaReader.ParseFile` and `CoverageBaselineReader.ParseFile` call `StreamReader.ReadToEnd()` (sync). Meziantou analyzer flags this as MA0045; both files carry a file-level `#pragma warning disable MA0045`.

**Impact.** Pragma suppressions accumulate. Each new reader will inherit the same workaround.

**Opportunity.** Either:

- Make the readers async (`ParseFileAsync(FilePath, CancellationToken)`) and switch `CoverageCheckTask` to `AsyncFrostingTask<BuildContext>`, mirroring `HarvestTask`'s async shape, or
- Use `Cake.Common.IO.FileAliases.ReadFile(context, path)` (if it exposes a sync text read) so no analyzer complaint and no pragma.

Decision needs a pass at other sync I/O in the host (e.g., `context.ToJson<T>(path)` inside `PreFlightCheckTask.LoadManifestFile`) to stay consistent.

### 4. `PreFlightCheckTask` is static-everything

**What.** Nearly every helper inside `PreFlightCheckTask.cs` is a `private static` method, and the task itself has no ctor dependencies. Comparison with the strategy layer (`HybridStaticValidator`, `PureDynamicValidator`) or the harvest layer (`BinaryClosureWalker`, `ArtifactPlanner`) shows a consistent interface + class + DI pattern everywhere *else*.

**Impact.** Unit tests must run the whole task with a real `BuildContext` and temp-directory fixtures (see `PreFlightCheckTaskRunTests`), rather than isolating smaller units. Stream C will expand this task considerably (trigger-aware version resolution, package_families integrity, MinVer resolution) — the static shape will not scale.

**Opportunity.** Before Stream C, extract the concerns into services: `IVersionConsistencyValidator`, `IStrategyCoherenceValidator`, etc. Each becomes independently testable with NSubstitute, and `PreFlightCheckTask.Run` becomes a thin orchestrator.

### 5. `launchSettings.json` could be enriched for local debugging

**What.** Deniz's note, 2026-04-15: "Quality-of-life improvement — `build/_build/Properties/launchSettings.json` can carry the full CLI surface so contributors debug locally without re-reading the README every time."

**Impact.** Contributors hit F5 and need to reconstruct arguments manually (target, library, rid, vcpkg dir, etc.). Ramp-up friction.

**Opportunity.** Seed `launchSettings.json` with named profiles per common scenario:

- `Info` → `--target Info`
- `Preflight` → `--target PreFlightCheck`
- `Coverage-Check (local)` → `--target Coverage-Check`
- `Harvest win-x64` → `--target Harvest --library SDL2 --rid win-x64`
- `Consolidate` → `--target Consolidate`

Non-blocking. Nice onboarding artifact.

### 6. `ValidationResult` exposes success value two ways

**What.** `ValidationResult` has both `public ValidationSuccess ValidationSuccess => SuccessValue();` and an inherited `SuccessValue()` method. Two names for the same value.

**Impact.** Inconsistent call sites (`result.ValidationSuccess` vs `result.SuccessValue()`). My new `CoverageCheckResult` intentionally mirrored the pattern (`CheckSuccess`) for familiarity; compounding the duplication.

**Opportunity.** Pick one form and remove the other across all three result types. Likely keep the named property (`ValidationSuccess`, `CheckSuccess`, `Closure`) and drop direct `SuccessValue()` usage from consumer sites.

### 7. `HarvestTask` generic catches with `#pragma warning disable CA1031`

**What.** `HarvestTask.cs` declares `#pragma warning disable CA1031, MA0051` at the top and uses `catch (Exception ex)` to emit error status files before re-throwing.

**Impact.** The `catch (Exception)` swallows everything including `OperationCanceledException` and `OutOfMemoryException`. Re-throw mitigates the lost-exception problem but not the cancellation/OOM-handling problem.

**Opportunity.** Split into specific catches (`CakeException`, `IOException`, `JsonException`) with targeted error-status generation. Remove the pragma. Aligns with the analyzer's intent without losing the error-status emission.

### 8. Test fixtures duplicate build-context setup

**What.** `TaskTestHelpers.CreateBuildContext` and `TaskTestHelpers.CreateBuildContextForRepoRoot` share most of their setup (environment, file system, context substitution) with small variations.

**Impact.** Adding a new `IPathService` method means remembering to mock it in both factories (I almost missed this in `CreateBuildContext` when adding `GetCoverageBaselineFile`; only `CreateBuildContextForRepoRoot` got the mock because that is the only one currently used by run-level tests — but silently diverging factories is a smell).

**Opportunity.** Collapse to a single builder with fluent options (`BuildContextBuilder.Default().WithRepoRoot(path).WithLibraries(libs).Build()`). Additional benefit: discoverability of what *can* be overridden in a test.

### 9. Task-level tests still hit disk via `System.IO.File` — **promoted to a dedicated plan**

**What.** `PreFlightCheckTaskRunTests`, `CoverageCheckTaskRunTests`, `HarvestTaskTests`, `ConsolidateHarvestTests`, and `ProgramCompositionRootTests` create real temp directories and write real files (via `TempDirectoryTestBase` + `TaskTestHelpers.CreateBuildContextForRepoRoot`). Reader unit tests were migrated to `FakeFileSystem` during the ratchet session; the task-level tier is the remaining asymmetry. The ratchet session itself also added to the debt by cloning the temp-dir pattern into `CoverageCheckTaskRunTests`.

**Scope and execution.** See [test-infra-modernization-plan-2026-04-15.md](./test-infra-modernization-plan-2026-04-15.md) for the full plan: `FakeRepoBuilder` abstraction design, five-class migration sequence, scope boundaries (characterization tests stay on real disk), risks, and success criteria. Execute in a dedicated session once the ratchet session commits land.

### 10. (meta) Coverage ratchet `measured_*` fields are fragile against tiny refactors

**What.** During this very session, measured coverage moved from 60.80 → 62.62 → 62.76 → 62.62 as implementation details shifted. Each movement updated `build/coverage-baseline.json`.

**Impact.** The `measured_*` fields are informational, but churn obscures signal: is the floor rising because test coverage rose, or because lines/branches shifted in implementation?

**Opportunity.** Either:

- Accept the churn (measured is a snapshot, not a tracked metric), or
- Emit `measured_*` automatically from `CoverageCheckTask` on a dedicated `--target=Coverage-Ratchet-Raise` run that both computes the new values and updates the JSON in-place. Removes the hand-editing loop when coverage improves.

## How to Use This Notebook

- Items are **candidates**, not tickets. Pick and choose what to action.
- When an item lands as real work, delete it from here and track it wherever the canonical model dictates (GitHub issue, parking-lot, phase doc, etc.).
- Add new items as they surface; keep the file small. If it grows past ~15 entries, promote older ones to formal tracking or drop them.
