# Refactor Opportunities — Curated 2026-04-16

> **Purpose.** Small, still-open build-host refactor backlog after the fake-filesystem migration, Cake 6.1 upgrade, and follow-up cleanup passes.
>
> **Status.** Completed items were removed to keep this file readable. Candidate items we may want to evaluate later stay here on purpose, even when they are not active implementation work yet.

## Active Notebook Entries

### 1. Culture-invariant metric logging should stay deliberate

**What.** `CoverageCheckTask.LogMetrics` already uses `string.Create(CultureInfo.InvariantCulture, ...)`, which solved the Turkish-locale decimal issue. That rule is still implicit rather than codified.

**Impact.** The next metrics-heavy task can easily regress into locale-sensitive log formatting if someone reaches for `context.Log.Information("{0:F2}", value)` out of habit.

**Opportunity.** Keep auditing human-readable metric/date output as it appears. If a second task needs the same pattern, introduce a tiny helper and stop repeating the `string.Create` shape by hand.

### 2. `PreFlightCheckTask` is still static-everything

**What.** Nearly every helper inside `PreFlightCheckTask.cs` is a `private static` method, and the task still has no ctor dependencies. Strategy and harvest code have already moved to service + DI seams; preflight has not.

**Impact.** Stream C work will keep inflating this file, and unit tests will stay forced through full task execution instead of isolated service tests.

**Opportunity.** Extract version consistency and strategy coherence into dedicated services (`IVersionConsistencyValidator`, `IStrategyCoherenceValidator`, etc.) and leave `Run` as orchestration only.

### 3. Result success accessor duplication remains unresolved

**What.** Result wrappers still expose named success properties such as `ValidationSuccess`, `CheckSuccess`, `Closure`, and `DeploymentPlan`, even though the inherited `SuccessValue()` API remains available underneath.

**Impact.** Consumer code still has two valid idioms for the same data, which keeps tests and production call sites stylistically mixed.

**Opportunity.** Make a deliberate choice: standardize on named properties everywhere, or delete the named properties and lean on the generic monad API. Either direction is fine; the ambiguity is the real problem.

### 4. `HarvestTask` generic catches still need tightening

**What.** `HarvestTask.cs` still declares `#pragma warning disable CA1031, MA0051` and retains a generic `catch (Exception ex)` path so it can emit error RID-status files before re-throwing.

**Impact.** The task still catches broader failure classes than it should, including cancellation-like flows that should probably stay special.

**Opportunity.** Split the error handling into specific catch blocks, keep RID-status emission, and remove the pragma instead of suppressing the analyzer wholesale.

### 5. Coverage ratchet `measured_*` fields are still noisy metadata

**What.** `build/coverage-baseline.json` still carries snapshot-style `measured_*` values that churn when implementation details move, even if the floor itself is unchanged.

**Impact.** The diff noise makes it harder to tell whether coverage actually improved or whether the current measurement merely shifted.

**Opportunity.** Either accept them as ephemeral snapshots, or add a dedicated ratchet-raise flow that recomputes and rewrites them automatically.

### 6. Build-host blackbox task E2E layer should be planned explicitly

**What.** The build-host test suite is now much healthier at the whitebox/unit level, but we still need a thin blackbox layer above it that locks task input/output behavior even when task internals are heavily refactored.

**Impact.** Without a small blackbox envelope, large refactors in `PreFlightCheckTask`, `HarvestTask`, or `ConsolidateHarvestTask` can keep unit tests green while still changing observable task behavior, emitted files, exit/failure semantics, or log/report shape in ways we did not intend.

**Opportunity.** Plan a dedicated blackbox task-flow layer for the build host. Scope it around stable behavior contracts rather than internals: given repo-shaped inputs, did the task succeed/fail, emit the expected output artifacts/status files, and preserve the intended error contract? This should sit above the hermetic unit tests, not replace them.

### 7. Typed result wrappers still have enough repetition to justify a small shared layer

**What.** A shared base error type now covers the common `Message` + optional `Exception` shape, but the typed result wrappers still repeat a recognizable pattern of conversions, `ToResult`, and named success properties.

**Impact.** The current design is readable, but the repeated wrapper boilerplate still makes each new result family more expensive than it should be.

**Opportunity.** If this keeps growing, introduce a light shared result-wrapper helper or generation pattern. Keep it small and domain-friendly; do not collapse everything into a mega-generic abstraction.

## Cake 6.1 Follow-Through Map

1. **Landed.** Package pins and the core build-host docs are on Cake 6.1.x. Keep version sweeps cheap whenever those docs are touched again.
2. **Open.** Keep numeric and date log output invariant anywhere the build host prints human-readable metrics.
3. **Landed.** `OnError` is now the canonical result-monad error hook. Remove stale `ThrowIfError` examples when they surface.
4. **Open.** Keep coverage readers free of `MA0045` suppressions. Only promote the coverage path to full async if more file I/O accumulates.
5. **Candidate.** `Cake.Sdk` evaluation should stay on the list for a future architecture pass; not active now, but explicitly worth revisiting later.
6. **Candidate.** `.slnx` adoption should stay on the list for a future tooling pass; not active now, but explicitly worth revisiting later.
7. **Candidate.** `.NET 10` upgrade evaluation should stay visible as a future platform/toolchain topic once the current packaging and build-host refactors settle down.

## How to Use This Notebook

- Keep only active or explicitly parked items here.
- Delete entries as soon as they land elsewhere.
- If an item turns into roadmap work, move it to the canonical tracking model and remove it from this notebook.
