# Package Topology Refactor Research — DEFERRED

**Status:** Research complete. Execution deferred.
**Date:** 2026-05-12 (research authored), 2026-05-12 (parked).
**Unpark policy:** See [`../../release-strategy.md`](../../release-strategy.md) §Deferred Decisions.

## What This Is

Complete strategic + mechanical research for splitting current 2-package families (`Janset.SDL<M>.<Role>` + `Janset.SDL<M>.<Role>.Native`) into 3-package families (`Janset.SDL<M>.<Role>` role-meta + `.Bindings` + `.Native`).

The hypothesis: lock in override-friendly topology before public NuGet ship, since renaming `Janset.SDL2.Image` → `Janset.SDL2.Image.Bindings` after stable publication would create avoidable migration churn.

## Why Deferred

After review on 2026-05-12, the cost/benefit profile shifted:

- Zero user signal for the override use cases (bindings-only, system-provided SDL, BYO-native). All six justifications in `strategy-brief.md` are forward-looking, not pain-driven.
- Public ship moved to prerelease-first via `-preview.N` labels (per [`../../release-strategy.md`](../../release-strategy.md) §NuGet Labeling). Prerelease window naturally accommodates package-ID renames; the "lock-in before public ship" argument has weaker force.
- AST-first prioritization (Phase 4 promoted ahead of first public preview) means the bigger upstream design pressure is binding-generator output shape, not package topology. Topology design should follow AST output, not precede it.
- Implementation cost (46–68h focused + ongoing maintenance multiplier on 15 NuGet IDs) is non-trivial; better justified against real demand than hypothesis.

## Unpark Triggers

Resume execution when **any** of:

1. ≥3 GitHub issues request bindings-only / native-only consumption paths from real consumers.
2. `learning-sdl2` or another active downstream surfaces friction with the current 2-package shape.
3. SDL3 launch planning concludes new topology should land at SDL3 introduction.
4. v1.0 stable launch planning concludes topology refactor should ship as part of the launch wave.

When unparking: this directory's content is still mostly accurate. Re-read `phase-planning-methodology.md` §Outstanding Corrections to impact-map.md plus the two pending corrections surfaced in the 2026-05-12 fact-check (impact-map §4.C `PackageFamilyVersionSet` constructor signature actually takes `IEnumerable<PackageFamilyVersion>`, not `IReadOnlyDictionary<string, NuGetVersion>`; `DependencyRangeNormalizer` 3-mode extension is a strong collaborator-extraction candidate per `extraction-guidelines.md`, not a single-method extend).

## Salvageable Artifacts (Carve-Outs)

Two findings from this research are useful **independent of the refactor** and should be acted on separately:

| Finding | Where to act | Standalone effort |
| --- | --- | --- |
| Cross-family lower-bound bug in `DependencyRangeNormalizer.cs:110` (uses current packing family's version as lower bound for cross-family deps; should use dep family's resolved version) | Standalone fix landing under Phase 2b hardening | ~1–2h |
| `PackageFamilyVersionSet` (already present at `build/_build/Data/Versions/PackageFamilyVersionSet.cs`) is the canonical plumbing for the bug fix — type already exists, just needs threading through `PackageFamilyPacker` → `DependencyRangeNormalizer` | Same fix slice | included above |

## File Index

| File | Contents |
| --- | --- |
| [`strategy-brief.md`](strategy-brief.md) | WHY / HOW / WHAT decision hypothesis. 3-tier topology, V1 family-lock, ultimate-meta deferral. |
| [`impact-map.md`](impact-map.md) | Mechanical surface map — every file, class, method, manifest field, guardrail the refactor touches. ~1180 lines. |
| [`phase-planning-methodology.md`](phase-planning-methodology.md) | Per-phase planning convention, reference discipline, lifecycle. |
| [`implementation-plan-phase-1.md`](implementation-plan-phase-1.md) | Phase 1 characterization-test plan. Never executed. |

## Cross-Reference

- [`../../release-strategy.md`](../../release-strategy.md) — current release strategy + unpark policy
- [`../../plan.md`](../../plan.md) — current tactical roadmap
- [`../../decisions/2026-05-05-d3seg-and-package-first.md`](../../decisions/2026-05-05-d3seg-and-package-first.md) — ADR-001 (would have been superseded)
- [`../../parking-lot.md`](../../parking-lot.md) — other deferred ideas
