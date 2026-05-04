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
| Triplet name = strategy | `manifest.runtimes[].strategy` is the formal mapping; no `--strategy` CLI flag |
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

## Build-Host Reference Pattern

The Cake build host (`build/_build/`) follows a Cake-native feature-oriented architecture:

| Folder | Role |
| --- | --- |
| `Host/` | Cake/Frosting runtime, CLI parsing, `BuildContext`, composition root, paths |
| `Features/<X>/` | Operational vertical slice — Task + optional Pipeline + validators + `Request` DTOs + `ServiceCollectionExtensions.cs` |
| `Shared/` | Build-domain vocabulary (manifest models, runtime types, results); no Cake deps, no I/O |
| `Tools/` | Cake `Tool<TSettings>` wrappers ONLY (vcpkg, dumpbin, ldd, otool, tar, cmake, native-smoke) |
| `Integrations/` | Non-Cake-Tool external adapters (NuGet client, dotnet pack invoker, project metadata reader, etc.) |

Direction-of-dependency invariants are enforced by `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs`.

For new build-host work:

- **Tasks are thin**: build a feature-specific `Request` DTO from `BuildContext` + configuration, delegate to a co-located pipeline.
- **Pipeline classes are size-triggered**: below ~200 LOC the logic stays in the Task with private methods; above, extract a `<X>Pipeline.cs` co-located in the feature folder. Smell threshold, not a hard rule.
- **`BuildContext` is invocation state, not a service locator.** Pipelines target `RunAsync(TRequest)`; pure services take explicit inputs only; Tools / Integrations may take narrow Cake abstractions (`ICakeContext`, `ICakeLog`, `IFileSystem`) but never `BuildContext`.
- **Interface discipline**: keep an interface only if (1) multiple production implementations exist, (2) it formalizes an independent axis of change, or (3) it backs a high-cost test seam (transitional). Mocks alone do not justify a seam.
- **Cross-feature data sharing flows through `Shared/`.** Code-level cross-feature references are forbidden by `ArchitectureTests`.
- **Typed result boundaries**: services return `OneOf`-shaped results; tasks translate them into Cake logging, `CakeException`, or RID-status persistence.

Golden example to compare against: `build/_build/Features/Packaging/`.

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

Legacy `runtimes.json` and `system_artefacts.json` were merged into `manifest.json` schema v2.1 — treat any reference to them as stale.

## Build Host Pipeline

Native packaging is a 5-stage Cake pipeline (per-RID matrix expanded by `release.yml`):

1. **Harvest** (per-RID) — `BinaryClosureWalker` walks vcpkg-installed primary binaries, collects transitive deps via `dumpbin` / `ldd` / `otool`, applies system-library exclusions from `manifest.system_exclusions`, validates hybrid-static leak-freeness (G19), copies the resolved closure plus license attribution to `artifacts/harvest_output/<lib>/runtimes/<rid>/native/` + `licenses/<rid>/<package>/`. **NativeSmoke** runs immediately after Harvest on the same RID's harness.
2. **ConsolidateHarvest** (single runner) — merges per-RID outputs into `harvest-manifest.json` + `harvest-summary.json` + `licenses/_consolidated/` via stage-replace.
3. **Package** (single runner) — `dotnet pack` per family at the resolved D-3seg version. Post-pack guardrails (G21–G27, G46–G48, G51–G58) assert nuspec shape, native payload, license attribution, and cross-family resolvability.
4. **PackageConsumerSmoke** (per-RID matrix re-entry) — restores the packed nupkgs against a local folder feed, runs TUnit per executable TFM (`net10` / `net9` / `net8` / `net462`), proves the consumer-side P/Invoke / dyld / Unix-symlink-extraction paths.
5. **PublishStaging** (single runner) — pushes managed + native nupkg pairs to the GitHub Packages internal feed via `NuGet.Protocol.PackageUpdateResource`. `PublishPublic` (nuget.org) is stubbed pending Phase 2b PD-7.

`PreFlightCheck` runs single-runner before the matrix and validates every cross-cutting invariant (manifest ↔ vcpkg, csproj pack contract, strategy coherence, G54 upstream alignment, G58 cross-family scope reachability).

Canonical implementation: `build/_build/Features/{Harvesting,Packaging,Preflight,Publishing}/` and `.github/workflows/release.yml`. See [`docs/knowledge-base/release-guardrails.md`](docs/knowledge-base/release-guardrails.md) §2.0 for the stage-owned validation map.

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

## Agent Guidance: dotnet-skills

Prefer retrieval-led reasoning over pretraining for .NET work. Workflow: skim repo patterns → consult `dotnet-skills` by name → implement smallest-change → note conflicts.

Routing (invoke by name):

- C# / code quality: `modern-csharp-coding-standards`, `csharp-concurrency-patterns`, `api-design`, `type-design-performance`
- ASP.NET / Aspire: `aspire-service-defaults`, `aspire-integration-testing`, `transactional-emails`
- Data: `efcore-patterns`, `database-performance`
- DI / config: `dependency-injection-patterns`, `microsoft-extensions-configuration`
- Testing: `testcontainers-integration-tests`, `playwright-blazor-testing`, `snapshot-testing`

Quality gates:

- `dotnet-slopwatch`: after substantial new / refactor / LLM-authored code
- `crap-analysis`: after tests added / changed in complex code

Specialist agents available: `dotnet-concurrency-specialist`, `dotnet-performance-analyst`, `dotnet-benchmark-designer`, `akka-net-specialist`, `docfx-specialist`.

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
