# Agent Instructions — Janset.SDL2 / Janset.SDL3

Operating rules for LLM/code agents working in this repository.

## First Steps

Read in order before doing anything:

1. `docs/onboarding.md` — project overview, repo layout, glossary
2. This file — operating rules and approval gates
3. `docs/plan.md` — current status, active phase, roadmap

Branch out via `docs/README.md` based on the task.

## Communication Style (Deniz Preferences)

- Be talkative and conversational with practical, sometimes clever humor.
- Be innovative, but prioritize what will actually work.
- Challenge decisions when needed; explain reasoning clearly.
- Avoid yes-person behavior.
- Prefer clarity over cleverness.
- Talk like a millennial (Gen Y).
- Bilingual: Deniz communicates in Turkish and English interchangeably.

## Approval Gate (Hard Rule)

### Do NOT (without explicit "go / apply / proceed / başla / yap")

- Start coding new features
- Refactor production code
- Modify the build system (Cake Frosting tasks, MSBuild targets, `tools.cs`)
- Change CI/CD pipelines (GitHub Actions workflows)
- Update `vcpkg.json` or `build/manifest.json`
- Modify project files (`.csproj`, `.sln`, `Directory.Build.props`)
- Run deployment or publish commands
- Commit changes

### Exceptions

- Documentation-only edits
- Broken internal link fixes
- Minor comment improvements

### Before Any Commit

Present a summary of changes and a proposed commit message; ask for approval first.

### Before Any Deployment / Apply

Planning and dry-run operations are allowed; apply / mutate operations require explicit approval. If unsure → stop and ask.

## Project Context

Modular C# bindings for SDL2 (and upcoming SDL3) with cross-platform native libraries built from source via vcpkg, distributed as NuGet packages. Foundation layer for the Janset2D game framework, but designed as a fully independent open-source project.

| Stack | Role |
| --- | --- |
| .NET 10 / C# 14 | Managed bindings + Cake build host + `tools.cs` |
| Cake Frosting 6.1 | CI build automation (native harvesting, packaging, validation) |
| `tools.cs` (.NET 10 file-based app) | Local dev orchestration; forwards to Cake |
| vcpkg | Native library builds (custom hybrid overlay triplets) |
| GitHub Actions | CI/CD across 7 RIDs |
| NuGet | Package distribution |

### Target Platforms (7 RIDs, all hybrid-static)

`win-{x64,x86,arm64}`, `linux-{x64,arm64}`, `osx-{x64,arm64}`. Authoritative source: `build/manifest.json runtimes[]`.

### SDL Libraries in Scope

- **SDL2** (priority — finish first): SDL2, SDL2_image, SDL2_mixer, SDL2_ttf, SDL2_gfx, SDL2_net (binding pending)
- **SDL3** (future): SDL3, SDL3_image, SDL3_mixer, SDL3_ttf

## Settled Strategic Decisions

Final unless Deniz explicitly reopens.

| Decision | Detail |
| --- | --- |
| Dual SDL support | SDL2 and SDL3 in the same monorepo; SDL2 priority |
| Full RID coverage | 7+ targets, no scope reduction |
| vcpkg-based builds | All natives built from source; no downloads |
| Separate `.Native` packages | Per-family split (SkiaSharp / LibGit2Sharp pattern) |
| Hybrid-static / dynamic-core | Transitive deps static-baked into satellites; SDL core stays dynamic; encoded in `vcpkg-overlay-triplets/` |
| LGPL-free codec stack | Drop mpg123 / libxmp / fluidsynth; use bundled minimp3 / drflac / libmodplug / Timidity / native MIDI |
| tar.gz for Unix symlinks | NuGet can't preserve symlinks; `buildTransitive/Janset.SDL2.Native.Common.targets` extracts at consumer build time |
| D-3seg versioning | `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>` per family; UpstreamMajor.Minor anchored to `manifest.library_manifests[].vcpkg_version` (G54) |
| Hybrid-static encoded by triplets | Triplet names encode the build model; `manifest.runtimes[].strategy` field retired in S11 (2026-05-08); PreFlight validates triplet→overlay coherence via `HybridStaticOverlayValidator` (G16). No `--strategy` CLI flag |
| Validator uses vcpkg metadata | No manually maintained expected-deps lists; binary closure walker output is ground truth |
| Package-first consumer contract | Smoke / sample / sandbox csprojs consume packages via local folder feed; `Janset.Local.props` carries family versions |
| CppAst for binding autogen | Phase 4 — replaces SDL2-CS imports |
| `external/sdl2-cs` is transitional | Untrusted for production testing; retires when CppAst generator ships |
| C++ native smoke test | CMake/vcpkg IDE-debuggable harness for OS-level hybrid validation |
| TUnit + MTP for testing | Microsoft.Testing.Platform; characterization tests before refactoring |

## Test Naming Convention (TUnit)

Pattern: `<MethodName>_Should_<Verb>_<optional When/If/Given>`

- PascalCase method name (no inner underscores).
- Underscores between every other word segment.
- `Should` always present.

Examples:

- `IsSystemFile_Should_Return_True_When_Windows_System_Dll`
- `ParseSemanticVersion_Should_Throw_ArgumentException_When_Invalid_Format`

## Engineering Preferences

- Flag repetition aggressively (DRY).
- Prefer "engineered enough": not hacky, not over-abstracted.
- Bias toward explicit over clever.
- Prefer handling more edge cases, not fewer.
- Strong preference for tests when changing behavior.
- Cross-platform correctness is critical — always consider all 3 OS families.
- vcpkg and Cake Frosting are the build backbone — proposals must work within these tools.
- Prefer small public sealed classes with explicit collaborators over large classes with many private helper methods. Private methods are for local mechanics and narrative flow. If a private method contains business rules, branching-heavy logic, algorithmic behavior, or deserves independent tests, extract it into a named collaborator. Do not create ceremonial interfaces for single-implementation classes. See [`docs/refactoring/extraction-guidelines.md`](docs/refactoring/extraction-guidelines.md) for the full decision tree.

## Build-Host Reference Pattern

The Cake build host (`build/_build/`) is migrating to the target-centric architecture accepted in [`docs/decisions/2026-05-05-target-centric-build-host.md`](docs/decisions/2026-05-05-target-centric-build-host.md). Treat that ADR plus [`docs/refactoring/target-centric-build-host-refactor-plan.md`](docs/refactoring/target-centric-build-host-refactor-plan.md) as canonical for new build-host work and refactoring. Existing code may still contain the old `Features/`, `Shared/`, `Integrations/`, `Host/Configuration`, `*Pipeline`, strategy, and coverage-gate shapes until the migration reaches them.

Target architecture:

| Concept | Rule |
| --- | --- |
| `Targets/<CakeTargetName>/` | Primary navigation unit. Folder names follow Cake target names from `tools.cs`, `release.yml`, and `[TaskName]`. |
| Task class | Owns high-level orchestration, target input validation, request construction, expected-error reporting, and `CakeException` translation. |
| Request DTO | Immutable input contract passed from a task to collaborators when useful. It does not replace the Cake `RunAsync(BuildContext context)` signature. |
| Target collaborators | Named behavior/policy/IO adapters extracted only when complexity, reuse, testability, dependencies, or change reasons justify it. |
| Named concepts | Cross-target code is promoted only to real concepts such as `Manifest`, `Runtime`, `Versioning`, `Packaging`, `Results`, or file-backed repositories. |
| `Integrations/` | Not a default target-state layer. Existing adapters should move to target-local services, named concepts, or `Tools/` if they are Cake `Tool<TSettings>` wrappers. |
| `Tools/` | Cake `Tool<TSettings>` wrappers ONLY (vcpkg, dumpbin, ldd, otool, tar, cmake, native-smoke). |

For new or migrated build-host work:

- **Tasks are not pass-through shells.** Simple targets may keep behavior inline; larger targets extract named collaborators instead of mandatory pipelines.
- **Requests are earned but standard for non-trivial behavior.** Non-trivial executable targets use `<Target>Request` when collaborators need stable input; trivial/no-op/default/fully-inline targets are exempt.
- **Cake targets model user-visible lifecycle.** Do not split large work into internal pseudo-target chains just to reduce LOC; use named collaborators.
- **Retire `*Pipeline` as a default pattern.** Do not replace it with generic `Runner` / `Operation` / `Processor` wrappers.
- **Retire `Host/Configuration`.** `BuildContext` exposes named readonly CLI properties; file-backed state flows through repositories such as `ManifestRepository` and `VersionFileRepository`.
- **No catch-all `Shared` / `Common`.** Promote code only when it represents a named concept; otherwise keep it with the target that owns it.
- **Reuse must be real.** A second real consumer must exist, or be introduced in the same migration slice/phase, before code is promoted out of a target.
- **Interface discipline remains hybrid.** Keep interfaces for multiple implementations, independent change axes, expensive seams, or important task collaborator contracts. Do not create ceremonial `IFoo` / `Foo` pairs.
- **Cake nativeness is a hard rule at build boundaries.** For build-host IO, process execution, paths, logging, environment access, and tool invocation, use Cake-native abstractions by default: Cake aliases/addins first, then `Tool<TSettings>`, then named library/API adapters. Raw BCL IO or raw process invocation requires slice-review justification.
- **Pure code stays pure.** Pure policies, algorithms, and value objects should stay Cake-free when simple domain values are enough.
- **Code comments must be self-contained.** In `.cs`, `.csproj`, `.props`, `.targets`, workflow YAML, and local orchestration scripts, comments explain local logic directly; they do not point to docs as a substitute for explanation. Phase numbers, ADR references, and migration timing belong in canonical docs, not logic-bearing comments.
- **Guardrail IDs are not code names.** IDs such as `G58` belong in reports/log metadata/test data/docs mappings. Types, files, methods, and test classes use behavior-first names.
- **FrostingLifetime is not hidden target state.** Use it only for true host lifecycle setup/teardown; do not preload manifest/version target state there by default.
- **`tools.cs` stays standalone.** It may perform minimal manifest parsing from `build/manifest.json`, but must not reference `build/_build` internals.
- **Prefer modern C# without cleverness theater.** Use immutable records/value objects and pattern matching where they clarify the build domain; avoid over-abstracted functional cosplay.
- **Check ecosystem tools first.** Before writing wrappers around external tools, check existing Cake aliases/addins/plugins or Cake abstractions and document why they are not used.
- **Warning suppressions are last resort.** Avoid broad `#pragma warning disable`; if needed, keep suppressions local and justified.
- **Typed result boundaries are simple.** Use `Result<T,TError>` for expected operation failures and `ValidationReport` / `ValidationCheck` for multi-check validations. Avoid OneOf-style result hierarchies.
- **Architecture tests are retired as design police.** Use the ADR, refactor plan, AGENTS.md, and [`docs/refactoring/target-centric-build-host-review-checklist.md`](docs/refactoring/target-centric-build-host-review-checklist.md) instead.
- **Isolated worktrees are not native-build workspaces unless explicitly provisioned.** Do not initialize/update submodules or run vcpkg/native flows in ADR-002 migration worktrees just to verify target refactors; run managed tests and target discovery there, then run full `tools.cs setup` / `ci-sim` from the main provisioned checkout after merge.

### ADR-002 migration execution rules

- Migrate on an isolated branch/worktree from clean `master`. Do not push during iterative work; merge to `master` only after the full refactor is accepted.
- Per migration slice flow: `brainstorming` skill → `writing-plans` skill → user approval → `executing-plans` skill → walk through the [review checklist](docs/refactoring/target-centric-build-host-review-checklist.md) → present summary + proposed commit message → user approval → commit.
- [`docs/refactoring/extraction-guidelines.md`](docs/refactoring/extraction-guidelines.md) is canon for private-method, collaborator extraction, and interface decisions during migration slices.
- [`docs/refactoring/testing-guidelines.md`](docs/refactoring/testing-guidelines.md) is canon for test data policy (embedded fixtures vs centralized inline), V2/V1 infrastructure, filesystem seeding, and test anti-patterns.
- [`docs/refactoring/conversation-history.md`](docs/refactoring/conversation-history.md) holds the design dialogue archive; consult only when ADR/plan/checklist rationale is unclear. The plan and ADR are self-contained for execution.

Golden examples to compare against during the migration: current `ResolveVersionsFromManifestTask` and `ResolveVersionsFromExplicitTask` are closer to the desired task-owned orchestration style than the large Packaging/Harvesting pipelines.

> **Cake host vs `tools.cs`.** The Cake build host is a CI-only production pipeline for native harvesting, packaging, and validation. Day-to-day dev orchestration (setup, ci-sim, passthrough) lives in `tools.cs` at the repo root. Direct `dotnet run --project build/_build` invocations are for CI debugging and target discovery only.

## Configuration File Relationships

```text
vcpkg.json                    ← What vcpkg builds (deps + features)
    ↕ must match
build/manifest.json           ← Single source of truth (schema v2.1):
    ├── packaging_config      ← validation mode, core library
    ├── runtimes[]            ← RID ↔ triplet ↔ strategy ↔ CI runner ↔ container image
    ├── package_families[]    ← family identity (managed_project, native_project, library_ref, depends_on)
    ├── system_exclusions     ← OS libraries excluded from packages
    └── library_manifests[]   ← library versions, binary patterns
    ↕ validated by
PreFlightCheckTask            ← G14/G15/G16/G49/G54/G58 + family-scope guardrails
```

Legacy `runtimes.json` and `system_artefacts.json` were merged into `manifest.json` schema v2.1 — treat any reference to them as stale. The `runtimes[].strategy` field retired in S11 (2026-05-08); the diagram above reflects current schema.

## Build Host Pipeline

Native packaging is a 5-stage Cake pipeline (per-RID matrix expanded by `release.yml`):

1. **Harvest** (per-RID) — `BinaryClosureWalker` walks vcpkg-installed primary binaries, collects transitive deps via `dumpbin` / `ldd` / `otool`, applies system-library exclusions from `manifest.system_exclusions`, validates hybrid-static leak-freeness (G19), copies the resolved closure plus license attribution to `artifacts/harvest_output/<lib>/runtimes/<rid>/native/` + `licenses/<rid>/<package>/`. **NativeSmoke** runs immediately after Harvest on the same RID's harness.
2. **ConsolidateHarvest** (single runner) — merges per-RID outputs into `harvest-manifest.json` + `harvest-summary.json` + `licenses/_consolidated/` via stage-replace.
3. **Package** (single runner) — `dotnet pack` per family at the resolved D-3seg version. Post-pack guardrails (G21–G27, G46–G48, G51–G58) assert nuspec shape, native payload, license attribution, and cross-family resolvability.
4. **PackageConsumerSmoke** (per-RID matrix re-entry) — restores the packed nupkgs against a local folder feed, runs TUnit per executable TFM (`net10` / `net9` / `net8` / `net462`), proves the consumer-side P/Invoke / dyld / Unix-symlink-extraction paths.
5. **PublishStaging** (single runner) — pushes managed + native nupkg pairs to the GitHub Packages internal feed via `NuGet.Protocol.PackageUpdateResource`. `PublishPublic` (nuget.org) is stubbed pending Phase 2b PD-7.

`PreFlightCheck` runs single-runner before the matrix and validates every cross-cutting invariant (manifest ↔ vcpkg, csproj pack contract, current strategy coherence until ADR-002 removes it, G54 upstream alignment, G58 cross-family scope reachability).

Migrated targets: `build/_build/Targets/{PreFlightCheck,Harvest,NativeSmoke,ConsolidateHarvest,Package,PackageConsumerSmoke,PublishStaging,PublishPublic}/`. Pre-migration residue: `build/_build/Features/{Ci,Vcpkg,DependencyAnalysis,Diagnostics}/` (P10 territory — these were not in P9 scope) and `.github/workflows/release.yml`. The legacy `Features/{Packaging,Publishing}/` folders dissolved in S15 (P9). See [`docs/knowledge-base/release-guardrails.md`](docs/knowledge-base/release-guardrails.md) §2.0 for the stage-owned validation map.

## Docs-First Workflow

Before proposing changes, review documentation. If the change shifts behavior, topology, or infrastructure, update the relevant doc in the same change. Documentation is a first-class artifact.

Canonical reading order before non-trivial work:

1. `docs/onboarding.md` (project overview)
2. `docs/plan.md` (current status + roadmap)
3. `docs/phases/README.md` (active phase)
4. `docs/README.md` (full doc map)

Conflict resolution:

1. **Code wins over docs** for runtime behavior questions.
2. **`plan.md` wins** for current status and phase information.
3. **`onboarding.md` wins** for strategic decisions.
4. **Knowledge-base / playbook wins** for repo-specific operational details.

Change hygiene:

- Prefer consolidation over new files.
- Avoid duplicating tables / registries; state which one is authoritative.
- When you rename or move docs, update all internal references.
- Research docs always carry a date.

## Issue Management

Issue tracking is part of the delivery lifecycle.

- Roadmap-worthy work belongs in GitHub issues, not only chat / docs / commits.
- Issues use the current roadmap model from `docs/plan.md` and `docs/phases/README.md`.
- Issue bodies capture current reality, why the work matters, links to canonical docs, concrete exit criteria.
- Label model: `type:*` plus `area:*`, plus optional `platform:*` for OS-specific work.
- PRs are optional in this repo; direct commits are acceptable.
- When possible, reference issues in commits (`refs #123` / `closes #123`).
- Deferred work goes to canonical docs or backlog issues — never implied.

## Skills Used in This Project

Skills encode workflow discipline and retrieval-led reasoning — not optional decoration. Workflow: skim repo patterns → consult skills by name (`Skill` tool) → implement smallest-change → note conflicts. Specialist agents (`Agent` tool) handle delegated scope. Order of precedence: **process skills first**, **implementation skills second**.

### Tier 1 — Always Active (Essential)

Run by default; no trigger needed.

**Process discipline (`superpowers:*`):**

| Skill | Why it's mandatory |
| --- | --- |
| `using-superpowers` | meta — establishes skill discovery; loaded at session start |
| `brainstorming` | every feature / refactor / design pass — pairs with §Approval Gate |
| `writing-plans` | multi-step tasks need a written plan before code |
| `test-driven-development` | TUnit + characterization tests are a settled decision (§Settled Strategic Decisions) |
| `verification-before-completion` | "evidence before assertions" — required before claiming done |
| `systematic-debugging` | every bug / test failure / unexpected behavior — no shotgun debugging |
| `requesting-code-review` / `receiving-code-review` | enforces §When Deniz Asks For A Review structure |
| `dotnet-slopwatch` | mandatory anti-slop gate after LLM-authored code/project/test changes; catches disabled tests, broad suppressions, empty catch blocks, delays, and CPM bypasses |

**.NET core (stack-mandated, `dotnet-skills:*`):**

| Skill | Why it's mandatory |
| --- | --- |
| `dotnet-project-structure` | `Directory.Build.props` (×3), `Directory.Packages.props`, `global.json` already in use — changes must respect the model |
| `package-management` | CPM is active; `Janset.SDL2.*` NuGets are the public deliverable |
| `modern-csharp-coding-standards` | .NET 10 / C# 14 baseline; records, pattern matching, primary ctors are in scope |
| `api-design` | public NuGet API + D-3seg versioning (G54) require extend-only discipline |
| `dotnet-local-tools` | Cake and friends are pinned via `dotnet-tools.json` |

Slopwatch command for this repo: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"`. The excludes keep generated package caches, vendored submodules, native install trees, and build outputs out of the anti-slop gate. After a slice that deletes code or moves files, rebuild the baseline with `slopwatch init -f --exclude "..."` (same exclude list) to drop stale entries pointing at deleted paths.

### Tier 2 — Context-Triggered

Decision rows: skill ↔ trigger ↔ reason ↔ action. Invoke only when the trigger fires; do not pre-emptively load.

| Skill | When (trigger) | Why (reason) | How (action) |
| --- | --- | --- | --- |
| `type-design-performance` | designing P/Invoke structs, hot-path types, sealed/readonly choices | bindings cross managed↔native boundary; struct layout matters | invoke when touching `SDL2.Core` types or interop wrappers |
| `csharp-concurrency-patterns` | adding async / `Task.Run` / `lock` / `Channel<T>` | wrong primitive → deadlocks or wasted threads | invoke before adding any synchronization primitive |
| `dependency-injection-patterns` | adding/editing `Targets/<X>/ServiceCollectionExtensions.cs` or composition root | target-centric host relies on grouped registrations (§Build-Host Reference Pattern) | invoke when wiring a new target module |
| `microsoft-extensions-configuration` | new strongly-typed config / `IOptions` / `IValidateOptions` | settings drift causes silent CI failures | invoke when adding `BuildContext`-adjacent config |
| `crap-analysis` | tests added/changed in complex code | flags untested high-complexity paths | invoke after non-trivial test additions |
| `snapshot-testing` | manifest / nuspec / harvest-output baseline work | catches unintended schema/output drift | invoke when designing characterization tests with structured output |
| `ilspy-decompile` | inspecting SDL2-CS internals, NuGet payloads, framework behavior | external binaries are opaque without decompilation | invoke before assuming behavior of imported assemblies |
| `using-git-worktrees` | parallel feature work needing workspace isolation | prevents stomping in-progress changes | invoke before starting an isolated branch |
| `executing-plans` | executing a written plan in a separate session | enforces review checkpoints | invoke after `writing-plans` produces a plan |
| `subagent-driven-development` / `dispatching-parallel-agents` | independent tasks suitable for parallelism | maximizes throughput without context bleed | invoke when ≥2 tasks have no shared state |
| `finishing-a-development-branch` | implementation complete, tests green | structures merge/PR/cleanup decision | invoke before commit-and-push |

### Tier 3 — Specialist Agents (`Agent` tool)

Delegate when scope or analysis depth warrants it:

- `dotnet-concurrency-specialist` — racy tests, deadlocks, async timing bugs
- `dotnet-performance-analyst` — profiler/benchmark interpretation, regression detection
- `dotnet-benchmark-designer` — designing new BenchmarkDotNet suites
- `roslyn-incremental-generator-specialist` — Phase 4 CppAst binding generator
- `Explore` — broad codebase search (>3 query rounds)
- `Plan` — implementation strategy design

### Out of Scope (Skip)

Not applicable to this stack — do not invoke without explicit reason:
`akka-net-*`, `aspire-*`, `mailpit`, `mjml-*`, `verify-email-snapshots`, `efcore-patterns`, `database-performance`, `testcontainers-integration-tests`, `playwright-*`, `dotnet-devcert-trust`, `OpenTelemetry-NET-Instrumentation`, `marketplace-publishing`, `skills-index-snippets`, `docfx-specialist`.

## When Deniz Asks For A Review

Pick depth first:

1. **Deep review** (interactive): Architecture → Code Quality → Tests → Performance, up to 4 top issues per section.
2. **Quick review** (interactive): one focused question per section.

For each issue:

- Describe concretely with file references (and line numbers when possible).
- Provide 2–3 options including "do nothing" when reasonable. For each: effort, risk, impact, maintenance burden.
- Recommend one, explain why, ask Deniz to confirm before proceeding.

Output format:

- Number issues (`1`, `2`, ...).
- Label options (`A`, `B`, `C`); list the recommended one first.
- Keep interactive: ask Deniz to choose / confirm before big changes.
