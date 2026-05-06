---
name: "S06 ADR-002 target-centric refactor - P4 ResolveVersions complete, Slopwatch ready"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after ResolveVersionsFromManifest and ResolveVersionsFromExplicit migrated to ADR-002 target modules at e893600 on 2026-05-06. P4 is underway with Info + ResolveVersions migrated, full local tools verification passed, and Slopwatch baseline/config prepared for commit. Recommended next: finalize the Slopwatch baseline/docs commit, then decide the versions.json path contract before more stage-target version refactoring."
argument-hint: "Start by verifying whether Deniz committed the doc sweep + Slopwatch baseline, then choose between versions.json path research and the next P4 target migration"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering the janset2d/sdl2-cs-bindings repository after the first P4 migration slice landed. `Info`, `ResolveVersionsFromManifest`, and `ResolveVersionsFromExplicit` now use the ADR-002 target-centric shape. The next session should first verify the current git state because this pickup was authored while a final documentation/Slopwatch sweep was intentionally left uncommitted for Deniz to review and commit.

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-05-06` - `s06-p4-resolveversions-complete`)** and verify against the live repo, git log, and canonical docs before acting.

## What Just Happened

### P4 ResolveVersions migration landed

Commit: `e893600 refactor: migrate ResolveVersions targets to ADR-002 modules`.

| Area | State after `e893600` |
| --- | --- |
| `ResolveVersionsFromManifest` | Moved to `build/_build/Targets/ResolveVersionsFromManifest/ResolveVersionsFromManifestTask.cs`. Reads `BuildContext.ResolveVersionsSuffix` / `ResolveVersionsScope`, loads manifest via `IManifestRepository`, writes `PackageFamilyVersionSet` via `IVersionFileRepository`. |
| `ResolveVersionsFromExplicit` | Moved to `build/_build/Targets/ResolveVersionsFromExplicit/ResolveVersionsFromExplicitTask.cs`. Reads `ExplicitVersionEntries` / `ExplicitVersions`, validates input shape separately from parsing, enforces G54 through typed version-set validation, writes through `IVersionFileRepository`. |
| Version parsing | `ExplicitVersionParser` moved from `Features/Versioning` to the named `Build.Versioning` concept and now returns `PackageFamilyVersionSet`. |
| Retired code | `VersionsJsonWriter`, old ResolveVersions feature tasks, and `Features/Versioning/ServiceCollectionExtensions.cs` are gone. |
| BuildContext | ResolveVersions CLI values are named properties; public `BuildContext.ParsedArguments` is gone. `ParsedArguments` remains a composition-root binding input only. |
| DI | Repository registrations live in `Build.Repositories.ServiceCollectionExtensions.AddRepositories()`. Cake task classes are not explicitly registered; Cake discovers `[TaskName]` task classes and constructs them from the service provider. |
| Tests | Old feature tests were replaced with V2 scenario tests under `build/_build.Tests/Scenarios/ResolveVersionsFromManifest/` and `ResolveVersionsFromExplicit/`. Parser tests moved to `Unit/Versioning`. |
| Spectre | Packages were aligned to stable 0.55.x and `tools.cs` command signatures were updated for Spectre.Console.Cli 0.55. Build-host runtime Spectre API drift was cleaned up. |

### Verification witness

Run on merged `master` after `e893600`:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0 --no-restore
# 578 total, 578 passed

dotnet run --file tools.cs -- build --tree
# Target discovery succeeded and lists ResolveVersionsFromManifest + ResolveVersionsFromExplicit.

dotnet run --file tools.cs -- setup --source=local
# 7/7 stages passed on win-x64.

dotnet run --file tools.cs -- ci-sim
# 9/9 stages passed on win-x64, including NativeSmoke and PackageConsumerSmoke.
```

### Slopwatch baseline prepared, not committed by the agent

Deniz asked to leave commit control to him. At authoring time, these files were intentionally uncommitted:

| File | Purpose |
| --- | --- |
| `.slopwatch/baseline.json` | Baseline for existing repo-owned Slopwatch findings. Generated/vendor/build output paths were excluded. |
| `.slopwatch/slopwatch.json` | Strict rule severities. Do not rely on its exclude behavior alone; use the explicit command below until Slopwatch config handling is verified. |
| `.gitignore` | Allows tracking only `.slopwatch/baseline.json` and `.slopwatch/slopwatch.json` while keeping other `.slopwatch/*` files ignored. |
| `AGENTS.md` | Promotes `dotnet-slopwatch` to Tier 1 mandatory and documents the exact repo command. Also warns that ADR-002 migration worktrees are not native-build workspaces unless explicitly provisioned. |

The baseline gate command that passed:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch\baseline.json
```

Why the explicit excludes matter: Slopwatch 0.4.0 did not reliably apply the config-file excludes during baseline creation in this session, while the CLI comma-separated exclude argument did. Generated package caches, vendored submodules, native install trees, and build outputs should not be baselined as project slop.

### Session learnings promoted to canonical docs

| Learning | Canonical home |
| --- | --- |
| Cake task classes should not be explicitly registered in DI. | `AGENTS.md`, ADR-002, refactor plan, review checklist |
| Migrated targets read named `BuildContext` properties and should not consume public raw parser state. | ADR-002/refactor plan/review checklist |
| `ToLegacyBuildContext` stays frozen for unmigrated tests only. | ADR-002/refactor plan/review checklist |
| Isolated ADR-002 worktrees are not native-build workspaces unless explicitly provisioned. | `AGENTS.md` |
| `versions.json` path semantics need a focused decision before further stage-target version loading refactors. | `docs/refactoring/target-centric-build-host-refactor-plan.md` Post-P4 research task |

## Onboarding Snapshot

| Concern | Current state |
| --- | --- |
| Stack | .NET 10 / C# 14, Cake Frosting 6.1, vcpkg, TUnit/Microsoft.Testing.Platform, GitHub Actions |
| Build host | `build/_build/`, migrating from `Features/` to target-centric `Targets/` |
| Local orchestration | `tools.cs` remains the day-to-day surface; direct Cake invocation is for target discovery and CI debugging |
| Migrated targets | `Targets/Info`, `Targets/ResolveVersionsFromManifest`, `Targets/ResolveVersionsFromExplicit` |
| Tests | 578 build-host tests after ResolveVersions migration |
| Full local witness | `tools.cs setup --source=local` and `tools.cs ci-sim` passed from the main provisioned checkout after merge |
| Remaining P4 candidates | `CleanArtifacts`, `CompileSolution`, `Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`, `Inspect-HarvestedDependencies` |
| Open design question | `--versions-file` / `artifacts/resolve-versions/versions.json` contract |

## Current State You Should Assume Until Verified

- **Master HEAD at authoring:** `e893600` - ResolveVersions P4 migration.
- **Origin at authoring:** `origin/master` still pointed at `79155eb`; verify whether Deniz pushed.
- **Working tree at authoring:** uncommitted Slopwatch/doc sweep plus an existing untracked `.github/prompts/s05-p3-complete-p4-ready.prompt.md`. Verify before editing.
- **Prompt freshness:** if this file is committed, prefer it over the older S05 pickup for the next ADR-002 session.
- **Slopwatch baseline:** prepared locally and verified with explicit CLI excludes, but may or may not be committed by the time you read this.

## Recommended Next Step

1. **Finalize Slopwatch baseline/docs commit (small, low risk).**
   - Verify `.slopwatch/baseline.json`, `.slopwatch/slopwatch.json`, `.gitignore`, `AGENTS.md`, and this prompt are in the intended state.
   - Run:
     ```pwsh
     slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch\baseline.json
     git diff --check
     ```
   - Present summary + conventional commit message to Deniz. Do not commit without approval.

2. **Decide the `versions.json` path contract (research/decision, recommended before more version-loading refactors).**
   - Read `docs/refactoring/target-centric-build-host-refactor-plan.md` §Post-P4 research task.
   - Verify producer/consumer paths in `release.yml`, `tools.cs`, `BuildContext`, `VersionFileRepository`, and stage tasks.
   - Choose one contract:
     - remove `--versions-file` and make stage targets read the predetermined ResolveVersions output path;
     - support `--versions-file` for both ResolveVersions writers and stage readers;
     - keep reader-only `--versions-file` but rename/register repositories so writer-output and stage-input semantics are explicit.
   - Document the decision before changing code.

3. **Continue P4 target migrations (after or in parallel with the research decision).**
   - Next likely candidates: `CleanArtifacts` then `CompileSolution`.
   - Keep using V2 scenario tests, named `BuildContext` properties, no task DI registration, no `ToLegacyBuildContext` expansion.
   - Do not run full native/vcpkg flows from isolated worktrees. Use managed tests and target discovery in a migration worktree, then run `tools.cs setup` / `ci-sim` from the main provisioned checkout after merge.

Talk to Deniz before committing to which path. Master-direct commits are acceptable in this repo, but ADR-002 migration slices still require the documented review/approval flow.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `CLAUDE.md`
4. `docs/plan.md`
5. `docs/decisions/2026-05-05-target-centric-build-host.md`
6. `docs/refactoring/target-centric-build-host-refactor-plan.md`
7. `docs/refactoring/target-centric-build-host-review-checklist.md`
8. `docs/refactoring/extraction-guidelines.md`
9. `build/_build/Targets/ResolveVersionsFromManifest/ResolveVersionsFromManifestTask.cs`
10. `build/_build/Targets/ResolveVersionsFromExplicit/ResolveVersionsFromExplicitTask.cs`
11. `build/_build/Repositories/ServiceCollectionExtensions.cs`
12. `build/_build/Host/BuildContext.cs`
13. `tools.cs`
14. `.github/workflows/release.yml`

## Locked Policy Recap

- No commits without Deniz approving the summary and proposed commit message.
- Docs-only edits are allowed; production code/refactor/build-system changes need explicit go/apply/proceed/başla/yap.
- `dotnet-slopwatch` is mandatory after LLM-authored code/project/test changes.
- Cake task classes are discovered via `[TaskName]`; do not explicitly register task classes in DI.
- Migrated tasks read named `BuildContext` properties; do not reintroduce public `BuildContext.ParsedArguments`.
- V2 tests instantiate task classes from the provider without adding task types as services.
- `ToLegacyBuildContext` is frozen and only supports unmigrated tests.
- `tools.cs` stays standalone and may not reference `build/_build` internals.
- Build-host IO/process/path/logging/tooling uses Cake-native abstractions by default.
- Guardrail IDs are metadata/report labels, not primary code names.
- Isolated ADR-002 worktrees are not native-build workspaces unless explicitly provisioned.

## Final Steering Note

P4 has moved from "pattern proven once" to "pattern repeated." `Info` proved the target module shape; ResolveVersions proved repositories, typed version sets, task-owned validation, no task DI registration, and V2 scenario migration under real local orchestration.

The next sharp edge is not another file move; it is the `versions.json` contract. Decide that before pulling PreFlight/Package deeper into typed version loading, and the rest of the migration will be far less swampy. Hold the line.
