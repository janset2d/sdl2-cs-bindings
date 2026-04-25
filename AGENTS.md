# Agent Instructions — Janset.SDL2 / Janset.SDL3

These are operating rules for LLM/code agents working in this repository.

## First Steps

**Before doing anything, read these files in order:**

1. `docs/onboarding.md` — Project overview, strategic decisions, repo layout, glossary
2. This file (`AGENTS.md`) — Operating rules and approval gates
3. `docs/plan.md` — Current status, active phase, roadmap, version tracking

Then branch to relevant docs based on your task (see `docs/README.md` for navigation).

## Communication Style (Deniz Preferences)

- Be talkative and conversational with practical, sometimes clever humor.
- Be innovative, but prioritize what will actually work.
- Challenge decisions when needed; explain reasoning clearly.
- Avoid yes-person behavior.
- Prefer clarity over cleverness.
- Talk like a millennial (Gen Y).
- Bilingual context: Deniz communicates in Turkish and English interchangeably.

## Approval Gate (Hard Rule)

### Do NOT

- Start coding new features
- Refactor production code
- Modify build system (Cake Frosting tasks, MSBuild targets)
- Change CI/CD pipelines (GitHub Actions workflows)
- Update vcpkg.json or manifest.json (legacy `runtimes.json` / `system_artefacts.json` retired — merged into manifest.json schema v2.1)
- Modify project files (.csproj, .sln, Directory.Build.props)
- Run deployment or publish commands
- Commit changes

...unless explicitly approved ("go", "apply", "proceed", "başla", "yap", etc.).

### Exceptions

- Documentation-only edits
- Broken internal link fixes
- Minor comment improvements

### Before Any Commit

- Present:
  - Summary of changes
  - Proposed commit message
- Ask for approval before committing.

### Before Any Deployment / Apply

For infrastructure or deployment operations:

- Planning / dry-run operations are allowed.
- Apply / mutate operations require explicit approval.

If unsure → stop and ask.

## Project Context

### What This Project Is

Modular C# bindings for SDL2 (and upcoming SDL3) with cross-platform native libraries built from source via vcpkg, distributed as NuGet packages. Foundation for the Janset2D game framework.

### Key Technologies

| Technology | Role |
| --- | --- |
| .NET 9.0 / C# 13 | Managed binding projects |
| Cake Frosting 6.1.0 | Build automation (native binary harvesting) |
| vcpkg | Cross-platform native library builds |
| GitHub Actions | CI/CD (cross-platform build matrix) |
| NuGet | Package distribution |

### Target Platforms (7 RIDs)

All 7 runtime rows ship under `hybrid-static` per PA-2 (2026-04-18). Authoritative source: `build/manifest.json runtimes[]`.

| RID | vcpkg Triplet |
| --- | --- |
| win-x64 | x64-windows-hybrid |
| win-x86 | x86-windows-hybrid |
| win-arm64 | arm64-windows-hybrid |
| linux-x64 | x64-linux-hybrid |
| linux-arm64 | arm64-linux-hybrid |
| osx-x64 | x64-osx-hybrid |
| osx-arm64 | arm64-osx-hybrid |

### SDL Libraries in Scope

**SDL2** (priority — finish first): SDL2, SDL2_image, SDL2_mixer, SDL2_ttf, SDL2_gfx, SDL2_net
**SDL3** (future): SDL3, SDL3_image, SDL3_mixer, SDL3_ttf (no SDL3_net yet — upstream WIP)

## Settled Strategic Decisions

These are final. Do not re-debate unless Deniz explicitly reopens them.

| Decision | Detail |
| --- | --- |
| Dual SDL support | SDL2 AND SDL3 in the same monorepo |
| Full RID coverage | 7+ targets, no scope reduction |
| vcpkg-based builds | All natives built from source, not downloaded |
| Separate .Native packages | Per-library split (SkiaSharp/LibGit2Sharp pattern) |
| tar.gz for Unix symlinks | NuGet can't preserve symlinks; MSBuild extracts at build time |
| CppAst for binding autogen | Phase 4 — replaces SDL2-CS imports with generated bindings |
| Nx rejected | .NET-native tooling only (dotnet-affected, .slnx, Cake expansion) |
| Maximum feature coverage | Both X11 + Wayland, all image/audio codecs, Harfbuzz |
| Hybrid Static + Dynamic Core | Transitive deps static-baked into satellites; SDL2 core dynamic; custom vcpkg overlay triplets |
| LGPL-free codec stack | Drop mpg123/libxmp/fluidsynth; use bundled minimp3/drflac/libmodplug/Timidity/Native MIDI |
| external/sdl2-cs removal | Transitional, not trusted — CppAst generator (Phase 4) replaces it |
| C++ native smoke test | CMake/vcpkg IDE-debuggable project for testing hybrid natives directly (Phase 2b) |
| Triplet = strategy | No `--strategy` CLI flag; triplet name encodes the strategy; manifest `runtimes[].strategy` is the formal mapping |
| Config merge | 3 config files (manifest.json, runtimes.json, system_artefacts.json) → single manifest.json (schema v2.1, package_families[] added) |
| Validator uses vcpkg metadata | No manually maintained expected-deps lists; BinaryClosureWalker output = ground truth |
| TUnit for testing | TUnit 1.33.0 + Microsoft.Testing.Platform; test-first approach; characterization tests before refactoring |

## Test Naming Convention

**Pattern:** `<MethodName>_Should_<Do/Have/Return/Throw/etc.>_<optional When/If/Given etc.>`

- Method name is PascalCase, no underscores within it
- Every other word segment separated by underscores
- `Should` is always present

**Examples:**

- `IsSystemFile_Should_Return_True_When_Windows_System_Dll`
- `ParseSemanticVersion_Should_Throw_ArgumentException_When_Invalid_Format`
- `Validate_Should_Reject_Transitive_Dep_Leak_In_Hybrid_Mode`

## Docs-First Workflow

Before proposing changes, review the documentation. If your change affects behavior, topology, or infrastructure, update the relevant docs in the same change. Documentation is a first-class artifact.

### Canonical Docs — Read Before Proposing or Making Changes

- [`docs/onboarding.md`](docs/onboarding.md) — Project overview, strategic decisions, repo layout
- [`docs/plan.md`](docs/plan.md) — Canonical status, phase roll-up, version tracking, roadmap
- [`docs/phases/README.md`](docs/phases/README.md) — Phase workflow, active vs completed phases

> **Living docs rule:** If you discover a new fact, workflow decision, or implementation constraint while coding, update the relevant canonical doc immediately — usually `docs/plan.md` or the active phase doc.

### Where to Start (Quick Orientation)

- Start with `docs/onboarding.md` (project overview + strategic decisions).
- Read `docs/plan.md` next (current status + roadmap).
- Use `docs/phases/README.md` to determine which phase is active.
- Use `docs/README.md` for the full documentation map.
- For build system work: `docs/knowledge-base/cake-build-architecture.md`
- For CI/CD work: `docs/knowledge-base/ci-cd-packaging-and-release-plan.md` (live-state pointer + canonical-content-locations index; pre-Slice-E design preserved at `docs/_archive/`)
- For ADR-003 release lifecycle orchestration (provider/scope/version axes, stage-owned validation, matrix re-entry): `docs/decisions/2026-04-20-release-lifecycle-orchestration.md`
- For native harvesting: `docs/knowledge-base/harvesting-process.md`
- For overlay triplets & ports: `docs/playbook/overlay-management.md`
- For native smoke test (C++ validation): `tests/smoke-tests/native-smoke/README.md`
- For "how do I...?" questions: `docs/playbook/*`
- For design rationale: `docs/research/*`
- For broader tool/framework context: `docs/reference/*`

### Documentation Loading Rules

- Load `docs/onboarding.md` and `docs/plan.md` before doing anything.
- Resolve the active phase before loading detailed phase docs.
- Load playbooks, research docs, and reference docs only when the task actually needs them.
- When docs conflict, prefer `plan.md` for status and code for runtime behavior.

### Change Hygiene

- Prefer consolidation over new files. Only create a new doc when it clearly reduces complexity.
- Avoid duplicating tables/registries across documents. If duplication is unavoidable, state which one is authoritative.
- When you rename or move docs, update all internal references.
- Research docs must always carry a date.

## Issue Management

Issue tracking is part of the software delivery lifecycle in this repo.

- Roadmap-worthy work should exist in GitHub issues, not only in chat, docs, or commit history.
- Issues should use the current roadmap model from `docs/plan.md` and `docs/phases/README.md`, not retired planning terminology.
- Issue bodies should capture current reality, why the work matters, links to canonical docs, and concrete exit criteria.
- If scope changes, update the issue instead of letting the tracker drift away from the docs.
- PRs are optional in this repo. Direct commits are acceptable when appropriate.
- When possible, map commits back to issues with references such as `refs #123` or `closes #123`.
- If work is intentionally deferred, park it explicitly in canonical docs or backlog issues instead of leaving it implied.

## Engineering Preferences (Guidance For Recommendations)

- Flag repetition aggressively (DRY matters).
- Prefer "engineered enough": not hacky, not over-abstracted.
- Bias toward explicit over clever.
- Prefer handling more edge cases, not fewer.
- Strong preference for tests when changing behavior.
- Cross-platform correctness is critical — always consider all 3 OS families.
- vcpkg and Cake Frosting are the build backbone — proposals should work within these tools.

## Build-Host Reference Pattern

The Cake build host is DDD-layered per [ADR-002](../docs/decisions/2026-04-19-ddd-layering-build-host.md). Four top-level layers under `build/_build/` plus a Cake-native `Tasks/` exception:

| Layer | Folder | Role |
| --- | --- | --- |
| Presentation | `Tasks/` | Cake Frosting task classes. Own dependency mapping + user-facing failure rendering. |
| Application | `Application/<Module>/` | Use-case orchestrators (TaskRunners, Resolvers, SmokeRunner). Coordinate Domain + Infrastructure. |
| Domain | `Domain/<Module>/` | Models, value objects, domain services, result types, domain-level abstractions (e.g. `IPathService`). No outward dependencies. |
| Infrastructure | `Infrastructure/<Module>/` | Adapters for external systems: filesystem, process, Cake Tool wrappers. Implement Domain abstractions. |

Layer discipline is enforced by `build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs` (three invariants: Domain no outward deps; Infrastructure no Application; target shape is Tasks -> Application plus DTO/results).

Reference patterns for new build-host work:

- **Task layer shape:** thin adapter — inject a single TaskRunner from Application + read-only configs from Context, delegate `RunAsync`. `PackageTask` is the golden example; `HarvestTask`, `ConsolidateHarvestTask`, `NativeSmokeTask`, `PackageConsumerSmokeTask`, `PublishStagingTask`, `PublishPublicTask` all follow the same shape post-Slice-D refactor (Wave 6 runner extraction landed). Fat-task layout retired.
- **Keep `BuildContext` at the task boundary.** Tasks own orchestration, user-facing policy, and failure rendering, but the preferred behavior surface is the Application-layer runner.
- **Interface discipline:** keep an interface only if (1) multiple implementations exist today or (2) it formalizes an independent axis of change. Test mocks are supporting evidence, not a standalone reason to create or retain a seam. Otherwise use `internal sealed class` and register concrete in Program.cs.
- **Cake dependencies flow inward across layers, not outward.** Infrastructure may take `ICakeContext`/`IFileSystem`/`IPathService`; Domain may take `IFileSystem` and `IPathService` for file contract validation but not `ICakeContext`; Application may take any Cake type; Tasks sit on top.
- **Result / error boundaries stay typed.** Services return `OneOf`-shaped results; tasks translate them into logging, `CakeException`, RID-status persistence, or cancellation semantics.
- **Test folders mirror production.** `Unit/Domain/Packaging/PackageVersionResolverTests.cs` asserts the contract of `Domain/Packaging/PackageVersionResolver.cs`. Integration tests (when added) live under `Integration/<Scenario>/` and are not mirrored.
- **When in doubt, compare the shape of Packaging (Wave 1 golden) before inventing a new build-host pattern.**

## Configuration File Relationships

Understanding these is essential for build system work:

```text
vcpkg.json                    ← What vcpkg builds (dependencies + features)
    ↕ must match
build/manifest.json           ← Single source of truth (schema v2.1):
    ├── packaging_config      ← validation mode, core library
    ├── runtimes[]            ← RID ↔ triplet ↔ strategy ↔ CI runner ↔ container image
    ├── package_families[]    ← family identity (managed_project, native_project, library_ref, depends_on, change_paths)
    ├── system_exclusions     ← OS libraries to exclude from packages
    └── library_manifests[]   ← library versions, binary patterns
    ↕ validated by
PreFlightCheckTask            ← Fails if versions / triplet↔strategy / family scope inconsistent (G14, G15, G16, G49, G54, G58)
```

> **Note:** `manifest.json` is the authoritative source. Legacy `runtimes.json` and `system_artefacts.json` files may still exist in history or older notes, but they are no longer the source of truth.

## Agent Guidance: dotnet-skills

IMPORTANT: Prefer retrieval-led reasoning over pretraining for any .NET work.
Workflow: skim repo patterns -> consult dotnet-skills by name -> implement smallest-change -> note conflicts.

Routing (invoke by name)

- C# / code quality: modern-csharp-coding-standards, csharp-concurrency-patterns, api-design, type-design-performance
- ASP.NET Core / Web (incl. Aspire): aspire-service-defaults, aspire-integration-testing, transactional-emails
- Data: efcore-patterns, database-performance
- DI / config: dependency-injection-patterns, microsoft-extensions-configuration
- Testing: testcontainers-integration-tests, playwright-blazor-testing, snapshot-testing

Quality gates (use when applicable)

- dotnet-slopwatch: after substantial new/refactor/LLM-authored code
- crap-analysis: after tests added/changed in complex code

Specialist agents

- dotnet-concurrency-specialist, dotnet-performance-analyst, dotnet-benchmark-designer, akka-net-specialist, docfx-specialist

## When Deniz Asks For A Review

### Before You Start (Pick Review Depth)

Ask Deniz which mode to use:

1. Deep review (interactive): Architecture → Code Quality → Tests → Performance, up to 4 top issues per section.
2. Quick review (interactive): one focused question per section.

### What To Evaluate

Architecture:

- System boundaries and coupling
- Data flows and bottlenecks
- Cross-platform correctness (Windows/Linux/macOS)
- Build system coherence (vcpkg ↔ manifest ↔ Cake ↔ CI)

Code quality:

- Organization and module structure
- DRY violations (be aggressive)
- Error handling and missing edge cases
- Technical debt hotspots
- Over/under engineering relative to preferences above

### How To Report Issues

For each issue (bug, smell, design concern, or risk):

- Describe the problem concretely with file references (and line numbers when possible).
- Provide 2–3 options, including "do nothing" when reasonable.
- For each option: effort, risk, impact, and maintenance burden.
- Give a recommended option first, explain why, and ask Deniz to confirm direction before proceeding.

### Output Format (For Reviews)

- Number issues (`1`, `2`, `3`, ...).
- Label options with letters (`A`, `B`, `C`), and list the recommended option first.
- Keep the review interactive: ask Deniz to choose/confirm before doing big changes.
