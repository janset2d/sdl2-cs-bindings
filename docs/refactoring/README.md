# Refactoring Notes and Plans

This directory holds accepted refactoring plans and review aids for large internal codebase changes.

| Document | Purpose |
| --- | --- |
| [target-centric-build-host-refactor-plan.md](target-centric-build-host-refactor-plan.md) | Detailed execution plan for ADR-002 build-host modernization |
| [data-layer-refactor-plan.md](data-layer-refactor-plan.md) | Post-ADR-002 plan for moving file-backed/tool-read contracts into `build\_build\Data\` |
| [target-centric-build-host-review-checklist.md](target-centric-build-host-review-checklist.md) | Review checklist for target-centric build-host changes |
| [conversation-history.md](conversation-history.md) | ADR-002 design dialogue archive — consult only when ADR/plan/checklist rationale is unclear |

Durable engineering guidance that survived the refactor now lives under the knowledge base:

- [../knowledge-base/extraction-guidelines.md](../knowledge-base/extraction-guidelines.md)
- [../knowledge-base/testing-guidelines.md](../knowledge-base/testing-guidelines.md)

## Document lifecycle

Files in this directory now skew historical and migration-specific.

- **Historical / durable context** — accepted plans, review aids, and archives that still explain why the refactor landed the way it did (refactor plan, review checklist, conversation-history).
- **Temporary per-slice** — design specs and execution plans for a single ADR-002 migration slice (typically `YYYY-MM-DD-<topic>-{design,plan}.md`). Working-tree scratch only; never enter git history.

When a slice ships, audit the temp docs before deletion: unique findings, learnings, or open follow-ups must be migrated to their canonical home (refactor plan, ADR, AGENTS.md, plan.md) **before** the temp doc is deleted. The repo carries "what to know going forward", not "what was done" — git history covers the latter.
