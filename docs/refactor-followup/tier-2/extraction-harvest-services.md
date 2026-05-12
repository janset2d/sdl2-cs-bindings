# Harvest Services Extraction: BinaryClosureWalker BFS Split + ArtifactPlanner Statistics (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §5](../../post-refactor-cleanup-plan.md#5-extractions--re-shapes)

## Goal

Two harvest-side methods carry `MA0051` suppressions and either a `CA1031` suppression or a 175+ LOC body: `BinaryClosureWalker.BuildClosureAsync` (~108 LOC, file-scope `CA1031` + `MA0051`) and `ArtifactPlanner.CreatePlanAsync` (~180 LOC, `[SuppressMessage("MA0051")]`). Splitting them into named phases — package-graph walk + runtime-dep walk for the walker, action-emission + statistics-builder for the planner — removes the suppressions and surfaces the BFS structure that's currently hidden inside one long body.

## Open questions

- BFS split for the walker: do both phases stay inside `BinaryClosureWalker` as private methods, or split into `PackageGraphWalker` + `RuntimeDependencyWalker` collaborators?
- Statistics extraction shape: pure private static `BuildStatistics(actions, closure, strategy)` vs a sealed `DeploymentStatisticsBuilder` collaborator. The former is enough today; the latter buys a test seam.
- Does `BinaryClosureWalker` catch-all narrowing (cleanup-plan §1, currently Tier 3) bundle into this slice or stay separate? Narrowing changes pre-migration behavior, so probably stays Tier 3 until the BFS split is stable.

## Sketch of scope

- `BinaryClosureWalker.BuildClosureAsync` → `WalkPackageGraphAsync` (Queue<(OwnerPackage, OriginPackage)>) + `WalkRuntimeDependenciesAsync` (Queue<FilePath>) phases.
- `ArtifactPlanner.CreatePlanAsync` → `EmitActionsAsync` + `BuildStatistics` (pure projection over emitted actions).
- Remove `[SuppressMessage("MA0051")]` on `ArtifactPlanner.CreatePlanAsync`.
- File-scope `MA0051` on `BinaryClosureWalker` reduces or disappears; `CA1031` stays (separate Tier 3 narrowing).

## Promotion criteria

- `02-ctor-policy` must land first.
- Verify post-S14 Harvest scenario tests cover both BFS phases independently before splitting (avoid losing parity).
- Decide collaborator-vs-private-method per [`extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) before promotion.

## References

- [ADR-002](../../decisions/2026-05-05-target-centric-build-host.md) §5 + §8
- [`extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md)
- [post-refactor-cleanup-plan.md §5](../../post-refactor-cleanup-plan.md#5-extractions--re-shapes)
- Reviewer C+D convergent finding at S14 P8 review (statistics phase as pure projection)
- Reviewer D at S14 P8 (walker BFS split flagged as deferred extraction)
- Related: [GitHub issue #87](https://github.com/janset2d/sdl2-cs-bindings/issues/87) (HarvestTask extraction boundaries)
