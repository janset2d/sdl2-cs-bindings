# HarvestReadinessValidator: Validator-vs-Gate Decision (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §5](../../post-refactor-cleanup-plan.md#5-extractions--re-shapes)

## Goal

`HarvestReadinessValidator` lives in `Validation/Packaging/`, follows the `…Validator` naming convention, but throws `CakeException` on failure rather than returning a `ValidationReport` like its sibling validators. Either the type belongs elsewhere (it's a Pack-stage gate, not a multi-check validator) or it should be reshaped to return a typed report and let `PackageFamilyPacker` translate to a throw at the boundary. Picking one resolves a real cohort inconsistency.

## Open questions

- **Option (a) — Rename + relocate:** `HarvestReadinessValidator` → `HarvestReadinessGate`, move to `Targets/Package/Services/`. Acknowledges gate semantics; carves out from ADR §8 "Validation/ alt-folder" by treating it as a Pack-target collaborator. Minimal test reshape (only the rename matters).
- **Option (b) — Reshape to return `ValidationReport`:** Keep the location + name, change the return type, have `PackageFamilyPacker` throw at the boundary. Cohort consistency win with sibling `Validation/Packaging/` types. Reshapes 7 unit tests' `ThrowsAsync` assertions into report-checks.
- Does option (a) imply other `Validation/Packaging/` types are mis-named/mis-located (e.g., `PackageConsumerSmokePreconditionsValidator` also throws)? If yes, the carve-out becomes a small pattern.
- ADR-002 §8 amendment: should the rule be "Validation/<area>/ types return ValidationReport; gates live in their target Services/" — making (b) wrong-shaped?

## Sketch of scope

(Both options share the same blast radius — `PackageFamilyPacker`, the 7 unit tests, and any DI registration.)

- Option (a): rename type + interface + file; update 1 DI registration + 7 unit tests' class name; update `PackageFamilyPacker` field name; relocate file.
- Option (b): change return type signature; remove `EnsureReadyAsync` throws; add throw at `PackageFamilyPacker.PackAsync` Phase 1; reshape 7 unit tests from `ThrowsAsync` to `AssertReport.HasErrors` patterns.

## Promotion criteria

- Read ADR-002 §8 carefully; decide whether the rule favors (a) or (b).
- Survey other `Validation/Packaging/` types — do `NativePackageMetadataValidator`, `PackageOutputValidator`, `SatelliteUpperBoundValidator`, `ReadmeMappingTableValidator`, `HybridStaticOverlayValidator` follow ValidationReport-shape, or do some throw?
- If decision is (b), `PackageConsumerSmokePreconditionsValidator` likely needs the same treatment (consistency).

## References

- [ADR-002](../../decisions/2026-05-05-target-centric-build-host.md) §8 — Validation/ layer rules
- [post-refactor-cleanup-plan.md §5](../../post-refactor-cleanup-plan.md#5-extractions--re-shapes)
- Reviewer (S13 multi-agent review, T3.2 finding)
- [`HarvestReadinessValidator.cs`](../../../build/_build/Validation/Packaging/HarvestReadinessValidator.cs)
