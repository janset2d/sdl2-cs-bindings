# Architecture Decision Records

Date: 2026-11-06

Each decision is a standalone `NNNN-slug.md` file, numbered sequentially. Scan this directory for
the highest number and increment it. The directory listing is the index; maintain no second index.

[Architecture](../architecture/) and [engineering](../engineering/) own current normative rules.
An ADR owns one accepted decision's context, trade-off, rationale, consequences and reversal triggers.
Dated evidence is non-normative and is cited inline by date and title.
[AGENTS.md](../../AGENTS.md) routes work to the relevant canon.

Current rules and their ADRs must agree. If they diverge, follow [Deviation](../engineering/workflow.md#deviation).
A material reversal updates canon and deliberately revises the record or supersedes it with a new ADR.

## When to write an ADR

All three conditions must hold:

1. **Hard to reverse:** changing course later has meaningful cost.
2. **Surprising without context:** a future reader would ask why it was done this way.
3. **A real trade-off:** genuine alternatives existed and one was chosen for specific reasons.

Otherwise put current conventions in engineering canon, technical rules in architecture canon,
and dated findings in the private companion repository.

## Format

```text
# Short title of the decision

Date: YYYY-MM-DD

One to three sentences: context, decision, why.
```

Add only sections that carry useful information: Considered options, Consequences or Reversal trigger.
Use `Status: proposed`, `accepted`, `deprecated` or `superseded by ADR-NNNN` when status needs stating.
The value is recording what was decided and why, not filling a template.
