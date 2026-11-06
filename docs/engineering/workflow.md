# Repository Workflow

Date: 2026-11-06

## Branches and changesets

Work on a work branch with WIP checkpoints and one squash commit per changeset to `master`.
A pure `git mv` is its own changeset; only forced links and matching gate-baseline paths change with it.
Merge current `origin/master` into a branch that
falls behind. Rewriting a pushed branch's history is a reserved action, except the restack below.
Stack related pull requests when they build on each other. Each has its own squash changeset.
Restacking unmerged stack branches with `gh stack` and `git push --force-with-lease` is authorized;
this exception does not permit rewriting `master`.

## Commits and pull requests

Use Conventional Commits. When a change relates to an issue, reference it with `refs #N` or `closes #N`.
Use `wip:` for checkpoints. A PR names its proposed squash title and message, changed files and
verification commands with results.
Commit messages, tag messages and PR descriptions carry no AI-tool co-author trailer, session link
or generated-with footer, regardless of harness defaults.

## Agent autonomy

On work branches agents commit and push freely, open PRs once the local check passes, and file GitHub issues
for product, test and documentation findings. Reserved for Deniz: merging into `master`, release dispatch and publish, tags,
history rewrites, deleting branches or tags, repository creation and settings, anything touching
the Linux builder base or glibc floor. At a reserved action, stop that action and report the evidence.

A brief for delegated work under [build](../../build/) names [build/AGENTS.md](../../build/AGENTS.md)
as required reading. Workers started at the repository root may not load those scoped rules.

## Findings

Product, test and documentation findings become GitHub issues. Capture current reality, why it
matters, canonical references and concrete exit criteria. Use existing `type:*` and `area:*` labels,
an optional `platform:*` label, and an existing milestone. Search for duplicates first.
Keep roadmap-worthy work in issues, not only in chat or commits. Deferred work has an explicit
backlog issue or operational-state entry. Close declined findings as not planned, with the reason.
Process findings belong in the companion repository.

## Deviation

Classify before acting; when two levels fit, take the higher.

| Level | Situation | Action |
|---|---|---|
| 0 — Implementation detail | Canon and the approved plan are silent, or the choice is below their resolution | Decide and continue; record it if a reviewer might wonder |
| 1 — Recorded fallback | The mechanism fails and current canon records a fallback for exactly this case | Execute that fallback; state its trigger and update affected docs in the same change |
| 2 — Canonical correction | Reality contradicts a current non-decision claim or an approved plan constraint, with no fallback | Stop affected work; report commands, output, source references, options and a recommendation; wait for approval |
| 3 — Locked-decision challenge | New evidence contradicts a normative decision or ADR | Stop the slice; only Deniz reopens the decision, through evidence and explicit confirmation or amendment |

Agents never self-resolve levels 2 or 3. A worker reports to its coordinator; the coordinator brings
the conflict to Deniz. Temporary, unapproved design exploration is reference material, not policy.
A deviation without a record is a defect. Correct the contradicted document with the approved change,
not as follow-up work. Neighboring work may continue only when the contradiction does not affect it.

## Isolated checkouts

Use the main checkout by default; use an isolated worktree for genuinely parallel work.
Worktrees and isolated checkouts run managed builds, tests and target discovery only.
Do not initialize submodules or run vcpkg and native flows merely to verify a managed refactor.
Run harvest and the `setup` and `ci-sim` commands on validation hosts in their provisioned main checkout
at the commit under validation.
The [development guide](../playbook/local-development.md) owns the local procedures.

## Dependency refresh

Refresh CPM packages, the SDK, dotnet tools, vcpkg pins and GitHub Action versions to latest when
the work touches them. Keep `rollForward: latestFeature` in [global.json](../../global.json).
This policy never upgrades OS or container base images. The Linux builder floor remains reserved.
Read pins from [central package management](../../Directory.Packages.props), the SDK file and the
[vcpkg manifest](../../vcpkg.json); do not maintain a second version table.
