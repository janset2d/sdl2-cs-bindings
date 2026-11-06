# Agent Instructions

Harness-neutral entry point for coding agents in this repository. It holds the rules that apply
before task context is known; everything else is reached through Task Routing. Current status,
open questions and known gaps live in [`docs/plan.md`](docs/plan.md).

## Purpose

Janset.SDL2, and later Janset.SDL3, ships modular C# bindings for SDL and its satellite libraries,
with native libraries built from source through vcpkg, as NuGet packages. It is the foundation of
the Janset2D game framework and an independent open-source project. North star: the best, most
modern, most .NET-friendly SDL binding library; design questions are settled with peer evidence.

## Universal Rules

- Repository artifacts are written in English. Scratch work lives under the gitignored `/temp/`;
  validated outcomes move into code or canonical documentation.
- Every change is correct on Windows, Linux and macOS. vcpkg and Cake Frosting are the build
  backbone; proposals work inside them.
- The Linux builder base (Ubuntu 20.04 focal, glibc 2.31) is a constitutional floor: it is never
  raised and raising it is never proposed. Going lower is welcome when it needs no bad toolchain.
- Generated binding sources change only by running the generator; review the complete diff.
- [`external/`](external/) submodules are read-only upstream evidence. [`external/sdl2-cs`](external/sdl2-cs) is transitional and
  untrusted for production testing; it retires when the generated binding surface ships.
- A question asks for an answer, not an unrequested change. When intent is ambiguous, present the
  options, recommend one, and ask.
- Read the canon and the code a change touches before writing. Take the shape the surrounding code
  already uses and prefer the smallest correct change.
- When code and canon disagree, stop the affected work and follow Deviation in
  [`workflow.md`](docs/engineering/workflow.md). Only Deniz reopens a decision.
- Write engineered-enough code: explicit over clever; handle every edge case the change can meet,
  and add nothing the change does not need. Flag repetition; use no workaround that hides an error.
  Behaviour changes come with tests.
- Comments in code, project files and workflows explain local logic; they cite no documentation
  path, plan, phase or ADR number.
- Documentation changes in the same change as the behaviour it describes. Every current fact has
  one owner; other mentions are short relays. Moves are lossless
  ([`documentation.md`](docs/engineering/documentation.md)).
- Report completion only after [`quality-gates.md`](docs/engineering/quality-gates.md) passes on
  the platforms the change touches. "Builds" and "works" are different claims.

## Git and Reserved Actions

- Work on a work branch. There, commit and push freely, open a pull request once the local check
  passes, and file GitHub issues for findings. Branch, squash and message conventions:
  [`workflow.md`](docs/engineering/workflow.md).
- Reserved for Deniz: merging into `master`; release dispatch and publish; tags; history rewrites
  (except restacking unmerged stack branches, see [workflow](docs/engineering/workflow.md));
  deleting branches or tags; repository creation and settings; anything touching the Linux builder
  base or glibc floor. At a reserved action, stop, report the evidence, and wait for his word on
  that action alone.
- Commit messages, tag messages, and PR descriptions carry no AI-tool attribution: no
  `Co-Authored-By: Claude …` (or any other AI co-author) trailer, no `Claude-Session:` line, no
  "Generated with Claude Code" footer. This repo rule overrides any tool or harness default that
  asks for such lines.

## Task Routing

Read the canon for the task before proposing or changing design. ADRs explain why a decision
exists; dated research is evidence and may hold superseded positions.

| Task | Read first |
|---|---|
| Cake build host, its tests, [`tools.cs`](tools.cs) | [`build/AGENTS.md`](build/AGENTS.md), [`build-host.md`](docs/architecture/build-host.md), [ADR-0002](docs/decisions/2026-05-05-target-centric-build-host.md), [ADR-0003](docs/decisions/2026-05-12-build-host-data-layer.md) |
| Build, test and slopwatch commands; completion checks; CI | [`quality-gates.md`](docs/engineering/quality-gates.md) |
| Local setup, local feed, `ci-sim`, cross-OS validation | [`local-development.md`](docs/playbook/local-development.md) |
| vcpkg, overlays, triplets, native harvest, Linux builder base | [`overlay-management.md`](docs/playbook/overlay-management.md); procedures in the same document |
| Manifest, packaging, guardrails, versioning, release | [`release-guardrails.md`](docs/knowledge-base/release-guardrails.md), [`release-strategy.md`](docs/release-strategy.md), [ADR-0001](docs/decisions/2026-05-05-d3seg-and-package-first.md) |
| Binding generator, generated sources, regeneration | [Implementation notes](spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md), [maintenance](spikes/binding-generators/docs/canonical/binding-generator-maintenance.md) |
| Binding API shape, type mapping, naming, ABI | [Binding generator constitution](spikes/binding-generators/docs/canonical/binding-generator-constitution.md) |
| Binding tests, generated-output review | [Testing strategy](spikes/binding-generators/docs/canonical/testing-strategy.md), [output review](spikes/binding-generators/docs/canonical/binding-output-oracle-validation.md) |
| Code structure and test design | [Extraction guidelines](docs/knowledge-base/extraction-guidelines.md), [testing guidelines](docs/knowledge-base/testing-guidelines.md) |
| Documentation, ADRs, repository workflow | [`documentation.md`](docs/engineering/documentation.md), [`workflow.md`](docs/engineering/workflow.md), [Decision records](docs/decisions/README.md) |
| Consumer documentation, [`README.md`](README.md) | [`documentation.md`](docs/engineering/documentation.md) (Consumer documentation) |
| Domain terms | [Onboarding glossary](docs/onboarding.md#glossary) |
| Status, next work, open questions, known gaps | [`docs/plan.md`](docs/plan.md), [settled decisions](docs/onboarding.md#settled-strategic-decisions) |
| Research, design history, session pickup | Companion Repository, below |

## Companion Repository

Research evidence, finished plans and specs, session handoffs and host notes live in the private
repository `janset2d/sdl2-cs-bindings-internal`, usually checked out in a sibling folder with the
same name. When that folder exists, read its README at session start: it names the current pickup
and indexes the research. Without access, work from this repository alone; the canon here is complete
without it.

## Authority

- Code wins over documentation for what the software does today.
- [`docs/architecture/`](docs/architecture/) and [`docs/engineering/`](docs/engineering/) own current normative rules;
  [decision records](docs/decisions/) explain why each decision was taken; the [onboarding glossary](docs/onboarding.md#glossary)
  owns domain terms; [`docs/plan.md`](docs/plan.md) is operational and shrinks as work lands.
- `docs/superpowers/` holds temporary per-slice specs and plans; they are deleted when their slice
  ships, after preservation under the [documentation lifecycle](docs/engineering/documentation.md#lifecycle).
- Repository files and current canon beat memory, plans, dated research and handoffs, including
  the companion repository. If two canonical sources disagree, follow Deviation; do not pick one
  silently.
