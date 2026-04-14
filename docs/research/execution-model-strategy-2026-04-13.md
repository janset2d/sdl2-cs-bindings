# Strategy: Local Development, Controlled Parallelism, and Cake Build-Host Evolution

**Date:** 2026-04-13
**Author:** Shared draft
**Status:** Working strategy document
**Scope:** local development model, transition from waterfall to controlled parallelism, build-host evolution, monorepo source-vs-package modes, native asset acquisition strategy, validation/testing workflow

---

## 1. Why this document exists

The packaging verdict answers **what native packaging model the project should converge toward**.

This document answers a different but equally important set of questions:

- How should local development work without committing native binaries into git?
- How do we stop working in a strict waterfall sequence without turning the project into chaos?
- How should the Cake build host evolve so that it can support multiple native packaging strategies, multiple native asset sources, and both source-mode and package-mode validation?
- How should the monorepo balance **fast inner-loop development** with **real package-consumer validation**?
- How do we make testing, local development, package validation, and future binding generation reinforce each other instead of blocking each other?

This document is not the packaging verdict itself. It is the **execution strategy** document for how to work effectively while the packaging architecture is being implemented.

---

## 2. Core thesis

The project should move away from a strict waterfall model, but it should **not** switch to unconstrained parallel work.

The right operating model is:

> Controlled parallelism on top of a shared integration spine

In practical terms, that means:

1. Several workstreams can move at the same time.
2. They must all converge on the same small set of shared contracts.
3. Local development and package validation must be treated as **different modes**, not forced into one mechanism.
4. The Cake build host should evolve into a **strategy-driven but not freeform** pipeline system.
5. Native binaries should not live in git, but the project must still provide a first-class local path for:
   - acquiring them
   - validating them
   - packaging them
   - smoke-testing them

---

## 3. The current pain points

The current confusion is not caused by one bug or one missing task. It comes from several pressures colliding at once.

## 3.1 Waterfall pressure

Several threads are currently treated as if they should happen one after another:

- first finish packaging strategy
- then fix Cake packaging
- then validate local development
- then add smoke tests
- then think about autogen

This is too sequential for the actual dependencies between the topics.

For example:

- local development strategy affects how smoke tests should acquire native assets
- smoke tests influence what the package boundary must look like
- package-mode validation affects how PackageTask should behave
- binding generation should be aware of public boundary decisions early

A hard waterfall approach artificially delays useful feedback.

## 3.2 Local development ambiguity

The repo wants all of the following at once:

- do not commit native binaries to git
- still allow local inner-loop development
- still support source-based project references in the monorepo
- still validate true package-consumer behavior
- eventually support independently releasable packages

If these are all forced through a single build mode, the system becomes hard to reason about.

## 3.3 Source graph vs shipping graph confusion

Inside the monorepo, projects reference each other as source projects.

But once packaged, they are supposed to become independently consumable NuGet packages with package dependency relationships.

These are two different truths:

- **source graph**
- **shipping graph**

Trying to make a single project configuration behave as both at all times leads to unnecessary complexity.

## 3.4 Build-host evolution pressure

The current build host already has real infrastructure:

- runtime scanners
- closure walking
- package ownership lookup
- system filtering
- RID manifests
- consolidation
- partial staging-path support
- a parked overrides concept

The challenge is not “do we need a build system at all?”

The challenge is:

> “How do we evolve this build host into a more flexible architecture without over-engineering it?”

---

## 4. Strategic conclusions

The discussion leads to four core conclusions.

## 4.1 Local development and package validation must be separate modes

This is the most important practical conclusion.

The project should stop treating “local development” and “real package consumption” as one build mode.

They should be explicitly modeled as separate operating modes.

## 4.2 Parallel workstreams are good, but they must share a narrow integration spine

Parallelism is valuable only if all streams converge against the same truth.

The key integration truth should be:

- a package consumer can restore the package
- the right native assets arrive
- the native libraries load
- the expected SDL-family initialization/shutdown behavior works

That is the spine.

## 4.3 The Cake host should become strategy-driven, not endlessly generic

The build host should support selectable strategies and asset sources.

But it should not become a freeform orchestration framework or a build-system science project.

## 4.4 Native binaries should stay out of git, but native asset acquisition must become first-class

If binaries are not committed, then the project must provide a real supported way to acquire native payloads in local workflows.

This cannot stay a manual “copy files around until it works” story.

---

## 5. The recommended operating model

## 5.1 Three distinct modes

The project should explicitly support **three modes**.

### Mode A — Source Mode

This is the fast inner loop for contributors.

Characteristics:

- monorepo projects use `ProjectReference`
- managed code builds directly from source
- native assets are acquired for the **current RID only**
- goal is fast local development and debugging

Questions Source Mode answers:

- does the code compile?
- does the local RID run?
- does this change break the local source graph?

### Mode B — Package Validation Mode

This is the most important integration mode.

Characteristics:

- consumer test/sample projects use `PackageReference`, not `ProjectReference`
- packages are restored from a local folder feed or equivalent local test source
- native assets are tested exactly as they would be for a real external consumer
- package shape matters here

Questions Package Validation Mode answers:

- can a package consumer restore these packages?
- do the expected native assets land correctly?
- does runtime loading behave correctly?
- do package boundaries actually work?

### Mode C — Release / Published Package Mode

This is the external or CI-backed final validation mode.

Characteristics:

- packages come from internal feed, local staged feed, or public feed depending on phase
- release discipline, signing, publication, and metadata are in scope
- this mode validates real distribution behavior

Questions Release Mode answers:

- are the release artifacts publishable and consumable?
- does the package family behave correctly under real distribution conditions?

## 5.2 Why this split matters

Because trying to force a single mode to satisfy all three sets of goals makes the system much harder than it needs to be.

Different modes can have different priorities:

- Source Mode optimizes for speed
- Package Validation Mode optimizes for realism
- Release Mode optimizes for distributability and confidence

---

## 6. Local development strategy

## 6.1 Goal

Provide a local development model that:

- does **not** rely on committed native binaries
- works for normal contributors
- does not require a public package feed
- still allows package-consumer validation when needed

## 6.2 Strategic principle

Native assets should be treated as **acquired build inputs**, not as hand-curated repo content.

That means the project needs a supported concept of:

- where native assets come from
- where they are staged locally
- how source-mode projects see them
- how package-mode projects see them

## 6.3 Recommended native asset acquisition model

The project should recognize multiple native asset sources:

- `vcpkg-build`
- `overrides`
- `harvest-output`
- `ci-artifact`
- later possibly `local-feed-derived` or `prebuilt-cache`

This is a crucial conceptual distinction.

These are **asset sources**, not packaging strategies.

The packaging strategy answers:

- pure dynamic?
- hybrid static + dynamic core?

The native asset source answers:

- where are the binaries coming from for this run?

## 6.4 Why `use-overrides` is not the center of the model

The old `--use-overrides` idea is still useful, but it should not become the conceptual center.

It is better treated as:

- a compatibility flag
- a shortcut for selecting a native asset source

rather than a whole strategy dimension by itself.

A cleaner mental model is:

- **strategy** = the intended dependency boundary and packaging model
- **native source** = where the actual binaries came from

## 6.5 Recommended Source Mode flow

Source Mode should look approximately like this:

1. Resolve config, RID, strategy, native asset source
2. Acquire or build native assets for the current RID
3. Make them available in the local source-mode runtime path
4. Build source projects via `ProjectReference`
5. Run local smoke tests for the current RID only

This keeps the inner loop tight.

## 6.6 Recommended local package-validation flow

Package Validation Mode should look like this:

1. Resolve config and strategy
2. Acquire/build native assets
3. Harvest and validate them
4. Pack native and managed projects
5. Publish to a local folder feed
6. Restore and build dedicated package-consumer test projects
7. Run smoke tests

This is the true external-consumer simulation.

---

## 7. Controlled parallelism strategy

## 7.1 Why controlled parallelism is needed

The project now has several threads that should move in parallel because they inform one another:

- build-host restructuring
- local development strategy
- smoke tests / headless validation
- package validation workflow
- binding-generation spike

If done sequentially, learning is delayed.

If done without coordination, assumptions drift.

Controlled parallelism solves that.

## 7.2 The integration spine

All parallel workstreams should converge on one shared definition of success:

### Integration spine

A package-consumer project can:

- restore the package(s)
- receive the right native payload
- load the native SDL-family binaries
- call basic initialization and shutdown APIs successfully
- do so on the intended RID without accidental hidden dependencies

This should be the shared integration contract.

## 7.3 Recommended parallel workstreams

### Workstream A — Build-host and packaging strategy

Focus:

- strategy-driven build-host evolution
- native source selection model
- policy validation model
- PackageTask contract

### Workstream B — Local development and native asset ingress

Focus:

- Source Mode workflow
- asset acquisition and staging
- no-binaries-in-git local workflows
- overrides / artifact reuse / vcpkg reuse model

### Workstream C — Smoke tests and headless validation

Focus:

- minimal initialization/load/shutdown test contracts
- package-consumer validation projects
- RID-specific validation shape

### Workstream D — Binding-generation spike

Focus:

- validate public boundary assumptions early
- ensure generated bindings fit source-mode and package-mode constraints
- avoid waiting until the whole packaging story is “finished” before learning anything

## 7.4 What “controlled” means here

Controlled parallelism does **not** mean each workstream defines its own contracts.

It means each workstream progresses independently **within a shared contract framework**.

Examples of shared contracts:

- source mode vs package-validation mode are distinct
- managed/native pair versioning stays strict
- public managed API remains SDL-family scoped
- package consumer smoke test is the integration truth
- native binaries do not live in git

---

## 8. Cake build-host evolution strategy

## 8.1 Goal

Evolve the existing build host into something more configurable and strategy-driven **without** turning it into a freeform orchestration engine.

## 8.2 Strategic principle

The build host should be:

> Strategy-driven, not endlessly generic

That means:

- keep the common spine fixed
- allow policy variation where needed
- allow asset-source variation where needed
- avoid building a giant abstract workflow meta-engine

## 8.3 Recommended architectural split

### Stable build-host spine

These pieces should remain foundational and reusable:

- config and options loading
- RID / triplet / runtime profile resolution
- vcpkg package info lookup
- runtime scanners (`dumpbin`, `ldd`, `otool`)
- binary closure walking
- system artifact filtering
- RID manifest generation and consolidation

### Variable layers

The build host should gain explicit seams for:

- packaging strategy
- native asset source selection
- dependency policy validation
- payload layout/staging policy

## 8.4 Why this is better than a freeform orchestrator

Because the project does not need infinite workflow freedom.

It needs a small family of intentionally supported models on top of one reliable build spine.

The danger of a freeform orchestrator is that the system becomes harder to understand than the product itself.

## 8.5 Task-graph guidance

The project should continue using Cake Frosting’s natural task graph rather than inventing an entirely separate orchestration universe.

However, task classes should remain thin.

The preferred direction is:

- task graph in Cake
- orchestration logic in services/pipelines
- policy decisions in dedicated strategy/policy services

This preserves readability and avoids giant task classes.

---

## 9. Source graph vs shipping graph strategy

## 9.1 The problem

Inside the monorepo, projects want source-level references.

When shipped, they need package-level dependency boundaries.

Trying to keep one project graph that behaves as both realities all the time is the source of a lot of complexity.

## 9.2 Recommended answer

Treat them as two real graphs:

### Source graph

- used by `src/*`
- optimized for developer productivity
- relies on `ProjectReference`

### Shipping graph

- validated by dedicated consumer projects
- optimized for package realism
- relies on `PackageReference`

## 9.3 Why this is healthier

Because it avoids forcing every production project file to constantly switch personalities.

The test consumer projects become the proof that the shipping graph works.

The source graph remains fast and readable for contributors.

## 9.4 Consequence

The repo should embrace the fact that **source truth and package truth are both real, but different**.

That is not a flaw. That is normal in a packaging-heavy monorepo.

---

## 10. Testing strategy

## 10.1 Source-mode testing

Purpose:

- quick local validation on current RID
- fast feedback for contributors

Characteristics:

- current RID only
- relies on source-mode native asset acquisition
- low-friction smoke tests

## 10.2 Package-consumer testing

Purpose:

- validate what a real user experiences

Characteristics:

- dedicated package-consumer test projects
- restore from local folder feed or staged feed
- verify native payload flow
- verify load/init/shutdown behavior

## 10.3 CI matrix testing

Purpose:

- prove package-consumer behavior across supported RIDs

Characteristics:

- per-RID native package production
- per-RID package-consumer smoke tests
- platform-specific loader/runtime behavior validated where it matters

## 10.4 Why this matters strategically

The project should stop treating `dotnet build` on the source solution as the whole truth.

For this repo, the real truth is:

- source graph builds
- package graph restores and runs

Both are necessary.

---

## 11. Binding generation within this strategy

## 11.1 Role of binding generation in the near term

Binding generation should not be blocked behind total packaging completion.

But it also should not proceed as if packaging and local validation questions do not exist.

## 11.2 Recommended approach

Run binding generation as a **boundary-validation spike** in parallel, not as a full immediate migration.

Questions the spike should answer:

- does generated SDL-family code compile cleanly in source mode?
- does the public boundary remain SDL-family scoped?
- do generated bindings fit package-consumer testing assumptions?

## 11.3 Why this is useful now

Because it gives earlier feedback on:

- code shape
- API exposure
- generator assumptions

without forcing the project to finish every packaging and local-dev decision first.

---

## 12. Recommended strategic principles

This section states the principles bluntly.

## Principle 1

**Do not force one build mode to solve all problems.**

Use explicit modes.

## Principle 2

**Do not commit native binaries to git.**

Instead, make native acquisition a first-class supported workflow.

## Principle 3

**Do not confuse strategy with asset source.**

- strategy = packaging/dependency boundary model
- asset source = where the binaries came from

## Principle 4

**Do not overreact by building a giant abstract orchestration framework.**

Keep the build spine stable and understandable.

## Principle 5

**Parallelize workstreams, but centralize integration truth.**

The package-consumer smoke test is the spine.

## Principle 6

**Preserve the current build host’s strongest assets.**

Scanners, closure walking, filtering, manifests, and consolidation remain valuable.

## Principle 7

**Accept that source graph and shipping graph are different realities.**

Model them separately and validate both.

---

## 13. Proposed near-term roadmap

## Stage 1 — Conceptual cleanup

- Approve the three-mode model: Source / Package Validation / Release
- Approve strategy vs native-source separation
- Approve controlled parallelism as the execution model
- Approve package-consumer smoke test as the integration spine

## Stage 2 — Build-host refactor direction

- Introduce explicit seams for strategy, source, validation, and layout
- Keep the existing scanner/closure spine
- Keep Cake task graph thin and readable

## Stage 3 — Local development upgrade

- Formalize Source Mode
- Formalize native asset acquisition choices
- Replace manual local file-copy assumptions with a supported pipeline

## Stage 4 — Package-validation workflow

- Add local feed publication path
- Add dedicated package-consumer validation projects
- Add smoke tests against the local feed path

## Stage 5 — Controlled parallel execution

- let build-host evolution, local-dev evolution, smoke tests, and autogen spike proceed in parallel
- require all of them to converge on the same package-consumer integration contract

---

## 14. Final strategic verdict

The project should stop trying to answer every development and release question through one monolithic build mode.

The right path forward is:

- **explicit operating modes**
- **controlled parallelism**
- **strategy-driven build-host evolution**
- **first-class native asset acquisition**
- **separate source graph and shipping graph validation**
- **package-consumer smoke tests as the integration truth**

This gives the repo something it currently needs badly:

> a way to move faster without becoming less coherent

---

## 15. Relationship to the packaging verdict

This document complements, but does not replace, the packaging verdict.

The packaging verdict answers:

- what the native packaging model should converge toward

This strategy document answers:

- how the team should work while converging toward it
- how local development should behave
- how the build host should evolve structurally
- how to avoid false waterfall sequencing

The two documents should be read together.
