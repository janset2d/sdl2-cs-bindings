# Refactoring Notes and Plans

This directory holds accepted refactoring plans and review aids for large internal codebase changes.

| Document | Purpose |
| --- | --- |
| [target-centric-build-host-refactor-plan.md](target-centric-build-host-refactor-plan.md) | Detailed execution plan for ADR-002 build-host modernization |
| [target-centric-build-host-review-checklist.md](target-centric-build-host-review-checklist.md) | Review checklist for target-centric build-host changes |
| [extraction-guidelines.md](extraction-guidelines.md) | Private-method decision tree, collaborator design, and extraction rules |
| [testing-guidelines.md](testing-guidelines.md) | Build-host test data policy, V2/V1 infrastructure rules, scenario test structure, anti-patterns |
| [conversation-history.md](conversation-history.md) | ADR-002 design dialogue archive — consult only when ADR/plan/checklist rationale is unclear |

## Document lifecycle

Files in this directory split into two lifecycles:

- **Durable** — accepted plans, ADRs, checklists, guidelines (refactor plan, review checklist, extraction-guidelines, testing-guidelines, conversation-history). Canonical homes for cross-slice knowledge.
- **Temporary per-slice** — design specs and execution plans for a single ADR-002 migration slice (typically `YYYY-MM-DD-<topic>-{design,plan}.md`). Working-tree scratch only; never enter git history.

When a slice ships, audit the temp docs before deletion: unique findings, learnings, or open follow-ups must be migrated to their canonical home (refactor plan, ADR, AGENTS.md, plan.md) **before** the temp doc is deleted. The repo carries "what to know going forward", not "what was done" — git history covers the latter.

