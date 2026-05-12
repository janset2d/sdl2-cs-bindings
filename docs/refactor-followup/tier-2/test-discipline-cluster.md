# Test Discipline & Coverage Cluster (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §7](../../post-refactor-cleanup-plan.md#7-test-discipline--coverage)

## Goal

S13 + S15 multi-agent reviews surfaced specific test gaps in scenarios, coverage, and assertion strength across `Package`, `HarvestReadinessValidator`, `PackageFamilyPacker`, `PackageConsumerSmoke`, and `DependencyRangeNormalizer`. They cluster naturally into one test-infrastructure pass that strengthens scenario assertions, fills coverage gaps in named-but-untested branches, and tightens redundant or weak assertions across the existing test surface.

## Open questions

- Should this slice run as one batch or bundle individual additions with the slices that touch the production code? E.g., `DependencyRangeNormalizer` uncovered throw branches naturally pair with the `dependency-range-semantics` slice; `HarvestReadinessValidator` divergent-license branch pairs with `harvest-readiness-gate-decision`.
- `FakeCakeWorld` fluent API redesign (tier-2/fakecakeworld-fluent-api) — does this slice depend on the redesign landing first, or stay on current API and migrate later?
- The "Polish cluster" (E1-E7 + NITs) is heterogeneous — should it be a separate sub-slice or fold into related production slices opportunistically?

## Sketch of scope

(7 specific additions/tightens + 1 polish cluster from cleanup-plan §7.)

- `PackageTaskScenarioTests.RunAsync_Should_Pack_All_Selected_Families_When_Happy_Path` — assert per-family pack invocations, topo order, headSha propagation.
- `HarvestReadinessValidatorTests.EnsureReadyAsync_Should_Return_When_All_Gates_Pass` — assert success-path log line; cover `DivergentLicenses.Count > 0` warning branch (currently 0%).
- `PackageFamilyPackerTests` — add dotnet pack failure (native + managed) + `IProjectMetadataReader` failure path tests.
- `HarvestReadinessValidatorTests` payload-subtree-missing tests — tighten assertion on unique path component (`runtimes` vs `_consolidated`).
- `DependencyRangeNormalizerTests` — 3 throw branches (nuspec entry missing, library_ref missing, vcpkg_version invalid).
- `PackageConsumerSmokeScenarioTests` happy-path — re-add `Arguments.Contains("--project", StringComparison.Ordinal)` regression.
- Polish cluster (E1-E7 + NITs): `BuildScopeManifest` dead helper, cardinality assertions, `NewWorld(feedClient)` unused param, verbose `Arg.Is<T>` patterns, fully-qualified `LogLevel` repeats, `MonoAvailabilityProbe` whitespace-PATH edge, `"When_Happy_Path"` naming drift, `PublishPublic` missing exception-type assertion, validator separate Code+Message couplings, "3 TFMs" pluralization brittleness.

## Promotion criteria

- Decide bundling strategy: standalone batch vs per-production-slice pairing.
- Run coverage report (`dotnet test /p:CollectCoverage=true`) to confirm the 0% / partial-coverage claims are still accurate before scoping.
- If `FakeCakeWorld` redesign is imminent, defer this slice until after — avoid re-migrating tests twice.

## References

- [post-refactor-cleanup-plan.md §7](../../post-refactor-cleanup-plan.md#7-test-discipline--coverage)
- [`knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — canonical test patterns
- S13 multi-agent review (7 reviewers; QA test specialist Tier 5 findings)
- S15 multi-agent review (Reviewer E test discipline cluster)
