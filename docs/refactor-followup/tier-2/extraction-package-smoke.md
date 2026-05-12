# PackageConsumerSmoke God-Service Extraction (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §5](../../post-refactor-cleanup-plan.md#5-extractions--re-shapes)

## Goal

`PackageConsumerSmokeTask` carries 10 ctor dependencies, a ~112-LOC `RunAsync` body with an `MA0051` suppression, and a chain of 11 private helpers that mix three distinct concerns: preflight gating, workspace preparation, and per-TFM smoke orchestration. Three extractions symmetrize the cohort with existing peer collaborators (`MonoAvailabilityProbe`, `SmokeScopeComparator`, `DotNetSmokeRunner`, `PackageConsumerSmokeReporter`) and remove the suppression.

## Open questions

- Does `Net4xRuntimeSkipPolicy` need its own interface, or can it be a sealed concrete behind a primary ctor? (Cohort has both shapes.)
- `SmokePreflightChecker` — keep the boundary as "ensure-*" methods returning void with `throw CakeException` on failure, or evolve to return a `ValidationReport` (`HarvestReadinessValidator` decision applies here too — see [`tier-2/harvest-readiness-gate-decision`](harvest-readiness-gate-decision.md)).
- `PrepareWorkspace` — service vs static helper? It needs `ICakeContext` + `IPathService` only, no policy.
- Should `SmokePackage` nested record promotion ([cleanup-plan §5](../../post-refactor-cleanup-plan.md#5-extractions--re-shapes)) fold into this slice or stay a Tier 3 one-liner?

## Sketch of scope

- New collaborator: `SmokePreflightChecker` — folds `ResolveSmokePackages`, `EnsureSelectionSupportsCurrentSmokeScope`, `EnsureSmokeCsprojsMatchManifestScopeAsync`, `EnsurePackageArtifactsExist`.
- New collaborator: `Net4xRuntimeSkipPolicy` — folds `ShouldSkipTfm`; symmetrizes with `MonoAvailabilityProbe` (which it already consumes).
- New collaborator (or static helper): `PrepareWorkspace` — bin/obj/workingRoot purge block.
- `PackageConsumerSmokeTask.RunAsync` collapses to a linear orchestration over the new collaborators — removes the `[SuppressMessage("MA0051")]` attribute.
- Ctor drops from 10 → ~7 deps once preflight + workspace collaborators absorb their helpers.

## Promotion criteria

- `02-ctor-policy` must land first — new collaborators are born into the agreed ctor style.
- Decide the `HarvestReadinessValidator` validator-vs-gate question ([`tier-2/harvest-readiness-gate-decision`](harvest-readiness-gate-decision.md)) before deciding the `SmokePreflightChecker` boundary shape, to avoid inconsistent preflight idioms across Pack/Smoke stages.

## References

- [ADR-002](../../decisions/2026-05-05-target-centric-build-host.md) §5 (target-owned story) + §8 (collaborator extraction)
- [`extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) — private-method-vs-collaborator decision tree
- [post-refactor-cleanup-plan.md §5](../../post-refactor-cleanup-plan.md#5-extractions--re-shapes)
- Phase 4.11 boss-fight self-review (Deniz accepted the god-service shape per ADR §5 at the time)
