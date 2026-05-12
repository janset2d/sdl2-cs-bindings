# DependencyRangeNormalizer G56 Lower-Bound Semantics Verification (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §4](../../post-refactor-cleanup-plan.md#4-api--performance-refinements)

## Goal

`DependencyRangeNormalizer.NormalizeAsync` (line 110) writes the satellite's own version as the lower bound of its dependency on the core family — e.g., sdl2-image=2.8.0 emits sdl2-core dep range `[2.8.0, 3.0.0)`, not `[2.32.0, 3.0.0)` (sdl2-core's actual current upstream version). This is pre-S13 behavior preserved through the refactor, but it's not obviously correct: is the satellite's own version a meaningful lower bound on a *different* family? Verify against guardrail G56's intent and either codify the policy explicitly or fix to use the dependency family's resolved version.

## Open questions

- What is G56's stated intent in [`release-guardrails.md`](../../knowledge-base/release-guardrails.md)? Is "satellite's version as core lower bound" a deliberate G56 policy or an accidental implementation detail?
- If accidental: switching to "dependency family's resolved version" as the lower bound changes package output. Behavior risk — any downstream consumer that pinned via the current range would see a different lower bound on republish.
- If deliberate: what's the rationale? (Suggested theory: "the satellite version was tested against *at least* this core version, so use it as the minimum supported core.") Document the rationale inline so future readers don't reopen the question.
- Does the consumer-smoke matrix actually exercise the range constraint, or are versions always pinned in `Janset.Local.props` / explicit `--versions-file` so the dep range is never the deciding factor?

## Sketch of scope

(Three possible paths, decision-dependent.)

- Path 1 (deliberate policy): Add a paragraph to G56 in `release-guardrails.md` explaining the lower-bound rationale + add an inline comment at `DependencyRangeNormalizer.cs:110` referencing G56's documented policy.
- Path 2 (fix to dependency-family version): Change the lower bound to `dependencyFamilyConfig`'s resolved version from `familyVersions`. Update consumer tests to assert the new range shape. Coordinate with PD-7 (PublishPublic) so the first nuget.org publication uses the corrected semantics.
- Path 3 (hybrid): Make the lower-bound policy configurable in `manifest.json packaging_config` so each project can choose. Probably YAGNI — pick path 1 or 2.

## Promotion criteria

- Read G56 in `release-guardrails.md` and confirm the policy intent.
- Verify the 3 uncovered throw branches in `DependencyRangeNormalizer` (cleanup-plan §7 / `test-discipline-cluster`) are addressed in the same slice if reshape happens — both touch the same file.

## References

- [`release-guardrails.md`](../../knowledge-base/release-guardrails.md) §G56 (cross-family dependency range)
- [post-refactor-cleanup-plan.md §4](../../post-refactor-cleanup-plan.md#4-api--performance-refinements)
- Reviewer 2 at S13 P7 review (T3.1 finding — verified-intent flag)
- [`DependencyRangeNormalizer.cs`](../../../build/_build/Targets/Package/Services/DependencyRangeNormalizer.cs)
