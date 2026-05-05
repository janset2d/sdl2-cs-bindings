---
name: "S02 ADR-002 target-centric refactor — P0 complete, P1 ready"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after ADR-002, detailed refactor plan, review checklist, and extraction guidelines landed on 2026-05-05. P0 (documentation and guardrails) is complete. Recommended next: P1 (baseline and inventory) or finish committing the doc sweep."
argument-hint: "Platform to start P1 on (windows/linux/macos — wherever dotnet SDK is available), or 'commit first' if the doc sweep hasn't been committed"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering the janset2d/sdl2-cs-bindings repository after the ADR-002 target-centric build-host refactor wave was designed and documented. The build host works, but the architecture was carrying legacy weight. A full refactor design sprint just closed — P0 documentation is done, P1 (baseline and inventory) is the immediate next step. No production code has been touched yet.

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-05-05` — `s02-p0-complete`)** and verify against the live repo, git log, and canonical docs before acting.

## What Just Happened

### ADR-002: Target-Centric Cake Build Host Architecture

Created [`docs/decisions/2026-05-05-target-centric-build-host.md`](docs/decisions/2026-05-05-target-centric-build-host.md). Accepted decision record that retires the current `Features/`, `Shared/`, `Integrations/`, `Host/Configuration`, mandatory `*Pipeline`, strategy abstraction, coverage gate, and architecture dependency tests. Replaces them with:

| Old | New |
| --- | --- |
| `Features/<X>/` with Pipeline wrappers | `Targets/<CakeTargetName>/` with task-owned orchestration |
| `Host/Configuration/` aggregate | `BuildContext` named readonly CLI properties + per-task validation |
| Catch-all `Shared/` | Named concepts: `Manifest`, `Runtime`, `Versioning`, `Packaging`, `Results`, repositories |
| `Integrations/` layer | Dissolved: target-local services, named concepts, `Tools/`, or deleted |
| Mandatory `*Pipeline` | Task class owns orchestration; extract only named collaborators |
| `strategy: "hybrid-static"` field + resolver/factory | Documented invariant, no runtime polymorphism |
| `Coverage-Check` target + ratchet | Retired; may return as optional CI signal |
| Architecture dependency tests | ADR + plan + review checklist |

### Detailed Refactoring Plan

Created [`docs/refactoring/target-centric-build-host-refactor-plan.md`](docs/refactoring/target-centric-build-host-refactor-plan.md). 16-section execution map covering:

- **§1** North star: task-first Cake-native readability
- **§2** Pain points table: task readability, pipeline classes, Features naming, Shared gravity, Configuration centralization, private method soup, interface ceremony, strategy abstraction, coverage gate, architecture tests
- **§3** Research signals: Cake Frosting docs, versioning task examples, current hotspots, TUnit lifecycle, external test repo inspiration
- **§4** Target inventory: 20 Cake targets with risk classification and migration notes
- **§5** Final architecture rules: target module layout, task anatomy, extraction rule (referencing extraction-guidelines.md), interface rule, Cake coupling rule, Cake target graph rule
- **§6** Shared concept map + Integrations dissolution policy (table: adapter kind → destination)
- **§7** BuildContext and configuration plan: what gets removed, target shape, file-backed repositories, version identity (`PackageFamilyId`, `PackageFamilyVersionSet`), runtime identity
- **§8** Results, validation, logging: `Result<T,TError>`, `ValidationReport`/`ValidationCheck`, logging boundary rules
- **§9** Testing architecture: `Unit/`, `Scenarios/`, `Integration/`, `Characterization/`, `Fixtures/Data/` taxonomy; scenario boundary; integration boundary; TUnit rules; filesystem rule
- **§10** Workflow and branch strategy: local-only branch/worktree, tools.cs north star, standalone tools.cs policy
- **§11** Phase plan: P0–P10
- **§12** Validation gates per change type
- **§13** Review protocol: before/during/after checklist per migration slice
- **§14** Open decisions: naming, `DotNetPackInvoker` placement, `RuntimeId`, scenario base class, characterization timeline
- **§15** Anti-goals: 13 things NOT to do
- **§16** Definition of done: 11 concrete exit criteria

### Target-Centric Review Checklist

Created [`docs/refactoring/target-centric-build-host-review-checklist.md`](docs/refactoring/target-centric-build-host-review-checklist.md). 11-section checklist for each migration slice: scope, target module shape, task orchestration, extraction quality, interfaces and DI, shared concepts, Cake coupling, results/validation/logging, testing, retired abstractions, docs.

### Extraction Guidelines

Created [`docs/refactoring/extraction-guidelines.md`](docs/refactoring/extraction-guidelines.md). Private-method decision tree with:

- When private is good (narrative helpers, local invariants, small technical details, API hygiene)
- When private smells (call chains, business rules, many parameters, state mutation, test urge, generic names)
- ASCII decision tree
- Extraction destination table (what to extract to: `*Policy`, `*Validator`, `*Calculator`, `*Adapter`, `*Repository`, etc.)
- Enterprise cosplay anti-pattern
- Interface rule (restated from ADR-002)
- LLM-specific prompt guidance
- Task-specific defaults

### AGENTS.md Overhaul

Updated `AGENTS.md` "Build-Host Reference Pattern" section to be ADR-002-aligned. Old `Features/` + `*Pipeline` canonical guidance replaced with target architecture table. Added extraction guidelines reference in Engineering Preferences.

### Docs Indexing

Updated `docs/README.md`, `docs/decisions/README.md`, and `docs/refactoring/README.md` to cross-link all new documents.

## Onboarding Snapshot

| Concern | State |
| --- | --- |
| Stack | .NET 10 / C# 14, Cake Frosting 6.1, vcpkg, TUnit, GitHub Actions |
| RID coverage | 7 targets: `win-{x64,x86,arm64}`, `linux-{x64,arm64}`, `osx-{x64,arm64}` |
| Build host | `build/_build/` — Cake Frosting, currently `Features/` layout, migrating to `Targets/` |
| Dev orchestration | `tools.cs` — .NET 10 file-based app, Spectre.Console + CliWrap, 3 commands (build/setup/ci-sim) |
| CI | `.github/workflows/release.yml` — 9-stage pipeline (build-cake-host → resolve-versions → preflight → generate-matrix → harvest → consolidate-harvest → pack → consumer-smoke → publish-staging) |
| Testing | TUnit + MTP, naming: `<Method>_Should_<Verb>_<When/If/Given>` |
| NuGet family pattern | Per-library managed+native packages, D-3seg versioning |
| Packaging strategy | Hybrid-static (invariant, not configurable) |
| Local feed | `build/msbuild/Janset.Local.props` generated by `tools.cs setup` |

## Current State You Should Assume Until Verified

- **Master HEAD**: `8d76b5e` — `refactor: streamline DotNetMSBuild invocation in ProjectMetadataReader`
- **Uncommitted changes**: Doc sweep pending commit — `AGENTS.md`, `docs/README.md`, `docs/decisions/README.md`, `docs/phases/README.md`, `docs/plan.md` modified; `docs/decisions/2026-05-05-target-centric-build-host.md` and `docs/refactoring/` (5 files) new. Confirm with `git status`.
- **Build-host tests**: Not yet verified in this session. Run `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0` as part of P1.
- **Build-host state**: Pre-migration. All code lives under `Features/`, `Shared/`, `Integrations/`, `Host/Configuration/`. No target has been moved to `Targets/` yet.
- **Phase**: P0 complete (documentation and guardrails). P1 (baseline and inventory) ready to start.
- **Architecture dependency tests**: Still exist at `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs` and enforce the OLD `Features`/`Shared`/`Integrations`/`Tools` layer rules. Marked for deletion in P10. Will break when old layers are dismantled — don't update them, don't delete them prematurely.

## Recommended Next Step

**1. Commit the doc sweep (if not yet committed)**

- **Classification**: Pre-flight, 1 commit
- **Pre-flight**: Verify `git status --short` shows only doc files (no `.cs` changes)
- **Files**: `AGENTS.md`, `docs/` tree
- **Commit message**: `docs: ADR-002 target-centric refactor — decision, plan, checklist, extraction guidelines`
- **Done when**: All docs committed, clean working tree on master

**2. P1 — Baseline and inventory (recommended primary next action)**

- **Classification**: Multi-session arc start, ~4-6 hours
- **Pre-flight**: Clean working tree, `dotnet run --file tools.cs -- build --tree` to verify Cake graph is reachable
- **What to do**:
  1. Create isolated local branch/worktree from clean master
  2. Run target discovery: `dotnet run --file tools.cs -- build --tree` and `dotnet run --file tools.cs -- build --target Info`
  3. Run build-host tests: `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0`
  4. Capture current target list from `[TaskName]` attributes → document in P1 notes
  5. Capture command-contract call sites in `tools.cs`, `release.yml`, `.github/actions/` → document
  6. Identify tests that assert old architecture (ArchitectureTests.cs + any tests coupled to `Configurations`/`Features` namespaces)
  7. Capture `tools.cs` package-family/manifest helper duplication → note for P2/P3 drift check decisions
  8. Invoke at least one Frosting task with primary-constructor DI (e.g. a version-resolution target) to prove task construction assumptions
- **Done when**: Baseline inventory is documented in a P1 commit message or brief `docs/refactoring/p1-baseline-notes.md`. No target migration started.

**3. Skip P1, do a lightweight smoke pass instead (if P1 already done or redundant)**

- **Classification**: Lightweight, optional
- **Pre-flight**: Confirm P1 baseline was already captured in another session
- **What to do**: Run `dotnet test` + `dotnet run --file tools.cs -- ci-sim` (local host only, not full matrix) to confirm nothing has broken since the doc sweep

Talk to Deniz before committing to which one. Master-direct commits are the default; do not branch unless the refactor plan explicitly calls for isolation.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md` — project overview, repo layout, glossary
2. `AGENTS.md` — operating rules, approval gate, settled decisions, Build-Host Reference Pattern (ADR-002 aligned)
3. `CLAUDE.md` — quick pointers, common commands
4. `docs/plan.md` — current status, active phase, roadmap
5. `docs/decisions/2026-05-05-target-centric-build-host.md` — ADR-002 (the north star for this refactor)
6. `docs/refactoring/target-centric-build-host-refactor-plan.md` — detailed execution plan (especially §4 target inventory, §11 phase plan, §13 review protocol)
7. `docs/refactoring/target-centric-build-host-review-checklist.md` — per-slice review checklist
8. `docs/refactoring/extraction-guidelines.md` — private-method decision tree and collaborator design rules
9. `.github/workflows/release.yml` — understand the 9-stage pipeline contract that must stay working
10. `tools.cs` — understand the local command contracts (build/setup/ci-sim)

## Locked Policy Recap

Curated from AGENTS.md and ADR-002 — the rules most likely to be relevant for P1–P4 work:

- **No code changes without explicit "go / apply / proceed / başla / yap"** from Deniz. Docs-only edits are exempt.
- **Before any commit**: present summary + proposed commit message, ask for approval.
- **Master-direct commits** are the default. Use local branch/worktree only when the phase plan calls for isolation.
- **`tools.cs` is the canonical local command surface**. Direct Cake invocation is valid for CI debugging and target discovery.
- **`BuildContext` is ambient invocation state**, not a service locator. Exposes named readonly CLI properties + paths + runtime info. File-backed state (manifest, versions) flows through repositories via DI.
- **`Tools/` is Cake `Tool<TSettings>` wrappers ONLY** (vcpkg, dumpbin, ldd, otool, tar, cmake, native-smoke).
- **`Integrations/` is not a default target-state layer**. Adapters dissolve to target-local services, named concepts, `Tools/`, or deletion.
- **No catch-all `Shared` or `Common`**. Cross-target code promotes to named concepts only.
- **`*Pipeline` as a mandatory pattern is dead**. Task classes own orchestration. Extract named collaborators only when earned.
- **`Host/Configuration` is dead**. Tasks read named `BuildContext` properties and validate at the task boundary.
- **Strategy is an invariant, not a configurable option**. `strategy` field and resolver/factory code will be deleted (P5).
- **Coverage gate is retired from the Cake host** (P5).
- **Architecture dependency tests will be deleted** (P10). Do not update them during migration.
- **Cake `FakeFileSystem` is the default** in unit/scenario tests. No `System.IO.Abstractions`.
- **Test naming**: `<MethodName>_Should_<Verb>_<optional When/If/Given>`
- **Interface rule**: Create an interface only when multiple production impls, expensive seams, independent change axes, or important task collaborator contracts justify it. Small pure helpers stay concrete.
- **Extraction rule**: Private methods are for local mechanics and narrative flow. Extract when code carries business rules, branching-heavy algorithms, or a concept that deserves a name. See `docs/refactoring/extraction-guidelines.md` for the full decision tree.
- **Migration is incremental**. Mixed architecture is acceptable during the migration. Each migrated target must retire its local pipeline/configuration/interface/private-method smells.

## Final Steering Note

The design sprint is over. ADR-002, the 16-section plan, the review checklist, and the extraction guidelines are all in place. No code has moved yet — the build host still lives under `Features/` with `*Pipeline` wrappers, `Host/Configuration`, and all the legacy shapes. That's intentional. P0 made the decisions durable; P1 establishes the baseline; P2 is where the first production code changes.

The natural rhythm for P1 is: isolate → inventory → document → commit. It should be boring and mechanical. Save the excitement for P2 when `Targets/Info/InfoTask.cs` becomes the first proof-of-concept for the new layout.

The build host has never been in better shape to refactor. Hold the line on the extraction rules — that's the taste that'll keep the next architecture from rotting.
