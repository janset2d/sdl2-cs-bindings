# Post-Refactor Follow-Up Plans

> Active execution surface for items in [`../post-refactor-cleanup-plan.md`](../post-refactor-cleanup-plan.md). Plans live here as **Tier 1** (full plan, ready to execute), **Tier 2** (outline, awaits promotion), **Tier 3** (one-line deferred, awaits trigger). LLM sessions iterate slice-by-slice via the [`superpowers:writing-plans`](https://github.com/anthropics/claude-skills/tree/main/superpowers) + [`superpowers:executing-plans`](https://github.com/anthropics/claude-skills/tree/main/superpowers) workflow.

## Status Legend

| Status | Meaning |
| --- | --- |
| `pending` | Plan written, not started |
| `outline` | Tier 2 only — sketch level; needs full plan when picked |
| `in-progress` | Actively being executed |
| `done` | Slice committed; remove from index after a grace period |
| `tracked` | Tier 3 placeholder; activates on a stated trigger |

## Index

| Tier | Slug | Title | Status | Depends on | Cleanup-plan § |
| --- | --- | --- | --- | --- | --- |
| 1 | [`01-cancellation-plumbing`](01-cancellation-plumbing.md) | Cancellation token plumbing | `done` | — | [§1](../post-refactor-cleanup-plan.md#1-correctness-gaps) |
| 1 | [`02-ctor-policy`](02-ctor-policy.md) | Constructor style policy + apply | `pending` | — | [§3](../post-refactor-cleanup-plan.md#3-style-consistency-sweep) |
| 1 | [`03-quick-wins`](03-quick-wins.md) | Quick wins batch | `pending` | `02-ctor-policy` | [§3](../post-refactor-cleanup-plan.md#3-style-consistency-sweep), [§4](../post-refactor-cleanup-plan.md#4-api--performance-refinements) |
| 2 | [`tier-2/extraction-package-smoke`](tier-2/extraction-package-smoke.md) | PackageConsumerSmoke god-service extraction | `outline` | `02-ctor-policy` | [§5](../post-refactor-cleanup-plan.md#5-extractions--re-shapes) |
| 2 | [`tier-2/extraction-harvest-services`](tier-2/extraction-harvest-services.md) | ArtifactPlanner + BinaryClosureWalker split | `outline` | `02-ctor-policy` | [§5](../post-refactor-cleanup-plan.md#5-extractions--re-shapes) |
| 2 | [`tier-2/cross-target-relayering`](tier-2/cross-target-relayering.md) | DeploymentStatistics + HarvestStatusRepository + IHarvestPaths | `outline` | — | [§5](../post-refactor-cleanup-plan.md#5-extractions--re-shapes) |
| 2 | [`tier-2/harvest-readiness-gate-decision`](tier-2/harvest-readiness-gate-decision.md) | Validator-vs-gate naming + relocation decision | `outline` | — | [§5](../post-refactor-cleanup-plan.md#5-extractions--re-shapes) |
| 2 | [`tier-2/dependency-range-semantics`](tier-2/dependency-range-semantics.md) | G56 cross-family lower-bound verification | `outline` | — | [§4](../post-refactor-cleanup-plan.md#4-api--performance-refinements) |
| 2 | [`tier-2/reporter-cohort`](tier-2/reporter-cohort.md) | `IReporter` base + `PreflightReporter` `IAnsiConsole` migration | `outline` | — | [§6](../post-refactor-cleanup-plan.md#6-reporter-cohort-consolidation) |
| 2 | [`tier-2/fakecakeworld-fluent-api`](tier-2/fakecakeworld-fluent-api.md) | CLI-seed vs filesystem-seed separation | `outline` | — | [§2](../post-refactor-cleanup-plan.md#2-ux--cli-surface) |
| 2 | [`tier-2/diagnostic-target-ux`](tier-2/diagnostic-target-ux.md) | Dumpbin/Ldd → Otool parity + binary input collapse | `outline` | — | [§2](../post-refactor-cleanup-plan.md#2-ux--cli-surface) |
| 2 | [`tier-2/test-discipline-cluster`](tier-2/test-discipline-cluster.md) | 7 test additions/tightens | `outline` | — | [§7](../post-refactor-cleanup-plan.md#7-test-discipline--coverage) |
| 3 | [`tier-3-deferred`](tier-3-deferred.md) | Deferred one-liners + watch list | `tracked` | — | various |

## Workflow

1. **Pick a slice.** Default to the next Tier 1 `pending` row; respect `Depends on`.
2. **Promote Tier 2 → Tier 1** when picked: move the file out of `tier-2/`, rename to a numbered prefix (`04-...`, `05-...`), expand outline using the Tier 1 plan template, fill in `## References` git commit refs from `git log -- <touched files>`.
3. **Execute the slice** using the `superpowers:executing-plans` skill against the Tier 1 plan. Iterate per the plan's `## Scope` section.
4. **Done.** Flip status to `done` in the index above and add the slice's commit SHA in a trailing column for one grace cycle. After 1–2 weeks fully remove the row.
5. **Folder retirement.** When zero `pending` / `outline` / `tracked` items remain, retire `docs/refactor-followup/` entirely. Any genuine survivors fold back into [`plan.md`](../plan.md) Phase X or a follow-up phase.

## Plan Template (Tier 1)

```markdown
# <Slice Title>

**Tier:** 1
**Status:** pending
**Depends on:** [list slugs | none]
**Slice size:** small (1 commit) | medium (1-2 days) | large (multi-day)
**Cleanup-plan section:** [post-refactor-cleanup-plan.md §N](../post-refactor-cleanup-plan.md#section-anchor)

## Goal
1-2 paragraphs — what changes, why now, success criteria in human terms.

## Scope
Concrete file:line changes with acceptance criteria. Bulleted.

## Out of scope
What this slice explicitly does NOT touch (signals reviewers what to ignore).

## Test strategy
- Existing tests touched
- New tests needed (FakeCakeWorld + TargetTestHost patterns per knowledge-base/testing-guidelines.md)
- Coverage gaps acknowledged

## Risk
- Behavior risk
- Surface risk (callers / consumers)
- Who-else-touches the same code path

## References
- [ADR-002](../decisions/2026-05-05-target-centric-build-host.md) §N — <rule applied>
- [ADR-003](../decisions/2026-05-12-build-host-data-layer.md) §N — <if relevant>
- [post-refactor-cleanup-plan.md](../post-refactor-cleanup-plan.md) §N — <item title>
- Related commits: `<sha>` (<subject>), `<sha>` (<subject>)
```

## Outline Template (Tier 2)

```markdown
# <Slice Title> (Outline)

**Tier:** 2
**Status:** outline
**Cleanup-plan section:** [link]

## Goal
1-paragraph sketch.

## Open questions
- Decision points to resolve before promotion to Tier 1.

## Sketch of scope
- High-level bullets, no file:line precision yet.

## Promotion criteria
- What must be true to become a Tier 1 full plan.

## References
- ADR + cleanup-plan section + prior commits.
```

## Tier 3 Shape

[`tier-3-deferred.md`](tier-3-deferred.md) is a single file. Each entry one bullet:

```markdown
- **<Item name>** — `<file:line>` — <one sentence what + why deferred>. **Activates when:** <trigger>. [Cleanup-plan §N]
```

## Cross-References

- [`../post-refactor-cleanup-plan.md`](../post-refactor-cleanup-plan.md) — canonical task list. Each cohort section links back here in its `Tracked at:` header.
- [`../plan.md`](../plan.md) Phase X — single pointer to cleanup-plan.
- [`../../AGENTS.md`](../../AGENTS.md) §Skills Used — `superpowers:*` workflow rules apply.
- [`../knowledge-base/testing-guidelines.md`](../knowledge-base/testing-guidelines.md) — canonical test infrastructure for plan `## Test strategy` sections.
- [`../knowledge-base/extraction-guidelines.md`](../knowledge-base/extraction-guidelines.md) — canonical extraction decisions for plan `## Scope` sections.
