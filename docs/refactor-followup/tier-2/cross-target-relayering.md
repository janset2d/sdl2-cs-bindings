# Cross-Target Re-Layering: DeploymentStatistics + HarvestStatusRepository + IHarvestPaths (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §5](../../post-refactor-cleanup-plan.md#5-extractions--re-shapes)

## Goal

Three layering problems compound in the harvest data path: `Build.Data.Harvest.IHarvestStatusRepository` references a target-local model (`Build.Targets.Harvest.Models.DeploymentStatistics`); `HarvestStatusRepository.Invalidate` cleans cross-target consolidated paths so its "Status" name is narrower than its scope; and `IPathService` carries 15+ `GetHarvestLibrary*` methods that only the harvest stack consumes. A single re-layering pass settles all three under ADR-003 (contract-centric data layer).

## Open questions

- Where should `DeploymentStatistics` live? Options: (a) `Build.Data.Harvest/` (data namespace), (b) new `Build.Harvesting/` cross-target named concept, (c) keep at `Build.Targets.Harvest.Models/` and accept the cross-namespace dep. ADR-003 favors (a) or (b).
- `HarvestStatusRepository` rename or split: `HarvestArtifactsRepository` (rename, single type) vs `IHarvestStatusWriter` + `IHarvestArtifactsCleaner` (split, two types). Split has better Single-Responsibility but doubles DI surface.
- `IHarvestPaths` segregation: lift all `GetHarvestLibrary*` methods to a new interface consumed by `HarvestStatusRepository`, `ConsolidateHarvest`, and the `Harvest` target itself. `IPathService` stays for cross-cutting paths (repo root, artifacts dir, vcpkg paths).
- Does the segregation strip enough from `IPathService` to also justify lifting `GetVcpkgInstalled*` to an `IVcpkgPaths`? (Probably out of scope for this slice — Tier 3 future.)

## Sketch of scope

- Move `DeploymentStatistics` (+ `DeploymentStrategy`, `DeploymentLocation`, `FileDeploymentInfo`, `ArtifactOrigin`, `DeploymentAction` hierarchy) from `Targets/Harvest/Models/DeploymentPlan.cs` to chosen home.
- Update consumers: `ArtifactPlanner`, `ArtifactDeployer`, `BinaryClosureWalker`, `HarvestStatusRepository`, `HarvestReporter`.
- Apply chosen `HarvestStatusRepository` shape decision.
- Define `IHarvestPaths` interface; implement on `PathService` (concrete keeps both interfaces); update `HarvestStatusRepository` + `ConsolidateHarvestTask` + `HarvestTask` to depend on `IHarvestPaths` not `IPathService`.

## Promotion criteria

- Read ADR-003 §N (data-layer namespace conventions) and confirm whether `Build.Data.Harvest/` is the canonical home for harvest-domain models, or whether a new `Build.Harvesting/` cross-target namespace is preferred.
- Confirm no other cross-target consumers of `DeploymentStatistics` exist (grep before promotion).
- This slice is independent of `02-ctor-policy`; can land in parallel with extraction slices.

## References

- [ADR-003](../../decisions/2026-05-12-build-host-data-layer.md) — contract-centric data layer
- [ADR-002](../../decisions/2026-05-05-target-centric-build-host.md) §4 (Models/ subfolder vocabulary) + §8
- [post-refactor-cleanup-plan.md §5](../../post-refactor-cleanup-plan.md#5-extractions--re-shapes)
- Reviewer A/B/C/D convergent finding at S14 P8 review (`DeploymentStatistics` cross-namespace dep)
- Reviewer D-S3 at S14 (`HarvestStatusRepository` naming)
- Reviewer D at S14 (`IPathService` god-service)
