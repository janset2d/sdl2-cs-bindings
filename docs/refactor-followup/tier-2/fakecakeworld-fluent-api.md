# FakeCakeWorld Fluent API Redesign (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §2](../../post-refactor-cleanup-plan.md#2-ux--cli-surface)

## Goal

`FakeCakeWorld` is the canonical test infrastructure (per `knowledge-base/testing-guidelines.md`). Its current fluent API conflates two distinct concerns under the same `With*` prefix: CLI option seeding (`WithRid`, `WithVersionsFile`, `WithSuffix`, `WithFamilyVersions`, `WithScope`, `WithExplicitVersion`) and fake filesystem seeding (`WithTextFile`, `WithManifestFile`, `WithJsonFile`). New contributors and LLMs alike misread `WithVersionsFile` as "write a versions.json into the fake FS" when it actually just sets the `--versions-file` CLI option value. Redesign should make the two layers visually and structurally distinct.

## Open questions

- API shape options:
  - (a) **Sub-builders:** `world.Cli.Rid("win-x64").VersionsFile(...)` + `world.Files.Manifest(...)` — clean separation, requires builder properties.
  - (b) **Method prefix:** `WithCli*` (e.g., `WithCliRid`) + `WithFile*` (e.g., `WithFileManifest`) — shallowest change, ugly names.
  - (c) **Two `With()` overloads:** `world.With(cli => cli.Rid(...).VersionsFile(...))` + `world.With(files => files.Manifest(...))` — concise but requires Action-based fluent chains.
  - (d) **Named methods:** `world.SetRid("win-x64")` + `world.SeedManifestFile(...)` — drop fluent entirely, just method calls. Easiest to read; loses chaining ergonomics.
- Migration strategy: deprecate old `With*` methods with `[Obsolete]` and provide new API, or do a hard cut across all test files?
- Does the test-infrastructure consolidation (cleanup-plan §7 / `test-discipline-cluster`) need this slice first, or can they run in parallel?

## Sketch of scope

- Pick API shape via brainstorm.
- Implement new API on `FakeCakeWorld`.
- Migrate all test files under `build/_build.Tests/` to the new API (~50+ test files reference these methods).
- Update `knowledge-base/testing-guidelines.md` to document the new shape.
- Optional: keep old methods as `[Obsolete]` shims for 1-2 slices if migration churn is too disruptive.

## Promotion criteria

- API shape decision via brainstorm.
- Estimate migration scope by `grep -r "world\.With" build/_build.Tests/` to count callsites.
- Consider Rider mass-rename feasibility per user's "Rider for mass renames" feedback memory.

## References

- [`knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — canonical test infrastructure
- [post-refactor-cleanup-plan.md §2](../../post-refactor-cleanup-plan.md#2-ux--cli-surface)
- [`FakeCakeWorld.cs`](../../../build/_build.Tests/Fixtures/FakeCakeWorld.cs)
- ADR-002 §14 — test infrastructure consolidation
