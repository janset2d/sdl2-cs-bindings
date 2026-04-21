# Docs Archive

> Historical snapshots of canonical documents that were substantially rewritten. **Not authoritative.**

## Purpose

When a canonical document accumulates enough historical rationale (superseded decisions, retired mechanisms, dated amendments) that its signal-to-noise ratio drops below threshold, the document is rewritten in place at its original path with the current authoritative state. The **pre-rewrite body** is preserved here so historical rationale remains accessible for future "why was decision X retired?" questions.

## Convention

- **Filename pattern:** `<original-name>-<YYYY-MM-DD>.md` where the date is the original's last effective date before rewrite (not the archive date).
- **Banner:** Every archived file carries an "ARCHIVED YYYY-MM-DD" banner at the top with a link to the canonical replacement and a short rationale for why it was archived.
- **No updates:** Archived files are frozen. Any correction to historical content must happen in the canonical replacement or a new ADR / research note.

## What belongs here

Only files where:

1. The canonical document was substantially rewritten (not minor edits), **and**
2. Historical rationale has no other canonical home (ADRs, research notes, or git history alone are insufficient).

Files that don't belong here:

- Retired phase docs that were roadmap copies with no unique rationale — those become **retire-to-stub** at their original path, pointing at the new canonical home.
- Minor doc edits, typo fixes, or routine refreshes — git history covers those.
- Research / review snapshots with dated filenames — those live under `docs/research/` and `docs/reviews/` with their own immutability convention.

## Archive Index

| File | Original path | Archive date | Rationale |
| --- | --- | --- | --- |
| [phase-2-adaptation-plan-2026-04-15.md](phase-2-adaptation-plan-2026-04-15.md) | `docs/phases/phase-2-adaptation-plan.md` | 2026-04-21 | Pre-ADR-003 amendment archaeology (S1 Adoption Record, retired Stream A0, A-risky historical record, ADR-001 addendum, Strategy State Audit, closed PDs). Rewrite retains only current execution state + open PDs. |
