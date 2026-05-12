# Reporter Cohort Consolidation: IReporter Base + PreflightReporter IAnsiConsole (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §6](../../post-refactor-cleanup-plan.md#6-reporter-cohort-consolidation)

## Goal

Six reporters now share a recurring shape (`LogStarting`, `StartLibrary`, `FinishLibrary`, `ReportLibraryFailure` / `ReportPhaseFailure`, `LogCompleted`): `OtoolReporter`, `HarvestReporter`, `PackageReporter`, `PackageConsumerSmokeReporter`, `ConsolidateHarvestReporter`, `PreflightReporter`. Five of them already use `IAnsiConsole` for Spectre tables/panels; `PreflightReporter` is the outlier (`ICakeContext`-only, `Log.Information/Error` × ~20 sites). Rule-of-five has triggered for both axes: shared shape and rendering API. A single pass consolidates both.

## Open questions

- **Base shape:** abstract `ReporterBase` class with template method pattern, OR `IReporter` interface + concrete sealeds, OR a shared `RuleRenderer` collaborator that all 6 inject? The first is the lowest-friction; the third has the cleanest unit-test seam but most ctor churn.
- Does the shared shape really fit `OtoolReporter`? It doesn't have a "library" loop in the same sense — it inspects a single binary. May need to bend the cohort's vocabulary or accept that `OtoolReporter` is partially outside.
- `PreflightReporter` migration: convert all `Log.Information` calls to `AnsiConsole.MarkupLine`, or design a unified "info / warn / error" abstraction that both backends implement? The latter is over-engineering.
- HarvestReporter's 9-method ceiling (cleanup-plan watch list / ADR-002 §15 risk register) — does the base extraction reduce or eliminate the ceiling concern?

## Sketch of scope

- Define base shape (abstract class OR interface + helpers).
- Migrate the 5 reporters with shared cohort methods to extend/implement.
- Migrate `PreflightReporter` from `ICakeContext`-only to `IAnsiConsole` (separate sub-slice if scope grows too large).
- Update DI registrations + unit tests to match the new shape.
- Reassess HarvestReporter's method count post-extraction.

## Promotion criteria

- Decide the base shape via brainstorm — collaborator vs base class vs interface affects every reporter ctor.
- Confirm `OtoolReporter` fits or accept it as an explicitly partial member of the cohort.
- Consider bundling with `tier-2/cross-target-relayering` (touches some reporters indirectly via repository moves) OR keep separate (independent change axes).

## References

- [ADR-002](../../decisions/2026-05-05-target-centric-build-host.md) §15 (risk register, reporter method-count ceiling) + §8 (collaborator extraction)
- [post-refactor-cleanup-plan.md §6](../../post-refactor-cleanup-plan.md#6-reporter-cohort-consolidation)
- S12 P6 PreFlight migration — `PreflightReporter` IAnsiConsole deferral
- All six reporter files under `build/_build/Targets/<X>/Reporting/`
