# Documentation

Date: 2026-11-06

Current rules for documentation ownership, authority, references and lossless maintenance.

## Document roles

| Role | Home | Meaning |
|---|---|---|
| Agent entry point | [AGENTS.md](../../AGENTS.md) | Universal rules and task routing |
| Architecture | [Architecture](../architecture/) | Current normative technical rules |
| Engineering | [Engineering](./) | Current authoring, quality and workflow rules |
| Decision rationale | [Decision records](../adr/) | Context, trade-offs and consequences |
| Domain vocabulary | [GLOSSARY.md](../../GLOSSARY.md) | Terms and meanings |
| Operational state | [Roadmap](../ROADMAP.md) | Now, next, open questions and known gaps |
| Consumer documentation | [README](../../README.md) | How to use the shipped packages |
| Research and session operations | Private companion repository | Non-normative evidence and handoffs |

## Companion repository

Research, finished plans and specs, handoffs and host notes live in the private repository
`janset2d/sdl2-cs-bindings-internal`. Public canon is complete without access to it.
Repository files and current canon beat memory, dated evidence, temporary plans and handoffs.
If current canonical sources disagree, follow [Deviation](workflow.md#deviation).

## One owner per fact

Each current fact has one canonical owner. Other appearances are short relays to that owner.
One topic per file keeps each owner clear. Read change-prone values from their mechanical source:
versions and TFMs from [build properties](../../Directory.Build.props) and [package pins](../../Directory.Packages.props),
RIDs and schema version from the [manifest](../../build/manifest.json), test counts from the test run.
Do not copy those values into prose.

[AGENTS.md](../../AGENTS.md) owns universal rules that no other canon holds. It relays the
reserved-action, attribution and Deviation rules owned by [workflow](workflow.md).
[build/AGENTS.md](../../build/AGENTS.md) relays build-boundary and test rules
owned by [build-host architecture](../architecture/build-host.md) and
[testing style](testing-style.md). A relay changes in the same change as its owner.

## Dates

Every hand-written document under this documentation tree carries `Date: YYYY-MM-DD` on its own
line within the first ten lines, except temporary specs and plans under `docs/superpowers/`.
For current-state and operational documents, it is the latest substantive update.
For an ADR, it is the decision date; a material revision moves it, an editorial link fix does not.
For a historical snapshot, an editorial correction does not rewrite its date.

## Lifecycle

Per-slice specs and plans live temporarily in `docs/superpowers/specs/` and `docs/superpowers/plans/`.
When the slice ships, move durable insight to canon or an ADR, copy the temporary files
byte-identically to `docs/archive/superpowers/` in the companion repository, then delete the public copies.
Sentences that change when work completes belong in the operational-state document, not evergreen canon.
Keep documentation current in the same change as the behaviour it describes.

## References

References point one way: documentation may cite code; code artifacts never cite documentation.
Code, project and workflow comments explain local logic, without doc paths, plan or ADR numbers.
An empty-at-rest output directory is named as a code span; every other path is a checked Markdown link.
Use section names rather than line-number ranges for references to prose.
Canon cites private evidence inline as `internal research, YYYY-MM-DD, "Title"`, never by path
or internal numbering. Canon never cites roadmap content; naming the roadmap as the home of operational state
is routing and is allowed.

## Moves

Read a retiring document in full. List every unique useful passage and its destination section.
Delete only after each passage is preserved, proven duplicate, or proven obsolete without historical value.
Git history is not a substitute for accessible useful canon.
Run `git mv` before editing. A pure move is its own changeset: renames, forced link fixes and matching
baseline-path changes only. Verify rename detection at 90% similarity before handing it over.

Copy departing material byte-identically before public deletion. Run byte copies in bash, not PowerShell
redirection. Use this companion mapping:

| Public path | Companion destination |
|---|---|
| `docs/research/<f>` | `docs/research/<f>` |
| Any other `docs/<p>` | `docs/archive/<p>` |
| Non-`docs/` path `<p>` | `docs/archive/<p>` |
| Temporary specs and plans | `docs/archive/superpowers/` |

Compare Git blob hashes to prove byte identity.

## Consumer documentation

The root [README](../../README.md) is packed into every NuGet package. It must stand alone for consumers.
Its `JANSET:MAPPING-TABLE` marker block is generated and checked by `ReadmeMappingTableValidator`;
preserve the block when editing surrounding prose. Use absolute GitHub blob URLs on `master` for
repository links in the README, so they work on nuget.org and the link gate checks them.
The README may restate the supported RID set and Linux glibc floor for consumers.
The [manifest](../../build/manifest.json) owns the RID set;
[ADR-0010](../adr/0010-linux-builder-glibc-floor.md) owns the floor.
A change to either updates the README in the same change.

## Repository gates

The [repository gates](../../build/_build.Tests/Integration/RepositoryGates/) check tracked Markdown
links with exact case, date lines, private companion paths, and documentation citations in non-Markdown files.
Baselines shrink only: remove fixed or deleted entries in the same change.
The sole growth exception is a pure move into a gate's scope: add only the moved paths for existing debt.
Commands and completion evidence belong in [quality gates](quality-gates.md).
