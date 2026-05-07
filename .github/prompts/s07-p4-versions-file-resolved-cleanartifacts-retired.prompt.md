---
name: "S07 ADR-002 target-centric refactor - P4 --versions-file resolved, CleanArtifacts retired, testing-guidelines extracted"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the --versions-file path contract was resolved, CleanArtifacts was retired from Cake, and testing-guidelines.md was extracted as canonical test reference. 578 tests pass, slopwatch baseline updated (49 entries). Recommended next: continue P4 with CompileSolution or diagnostic targets."
argument-hint: "Start by verifying the current git state (HEAD should include the --versions-file resolution commit), then choose between CompileSolution migration and diagnostic target migration for the next P4 slice"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering the janset2d/sdl2-cs-bindings repository after a significant P4 cleanup slice. Three things landed together:

1. **`--versions-file` is now universal** — both ResolveVersions writers AND stage-task readers use it.
2. **`CleanArtifacts` was retired from Cake** — local artifact hygiene moved to `tools.cs`.
3. **`testing-guidelines.md` was extracted** — canonical test reference cross-referenced from ADR, plan, checklist, and AGENTS.md.

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-05-07` - `s07-p4-versions-file-resolved`)** and verify against the live repo, git log, and canonical docs before acting.

## What Just Happened

### --versions-file universal contract

`IVersionFileRepository.Load(FilePath path)` / `SaveAsync(FilePath path, PackageFamilyVersionSet versions)` — path moves from constructor injection to method parameters. `VersionFileRepository` constructor takes only `ICakeContext`.

`BuildContext.VersionsFilePath` → `FilePath?` (nullable), no `PathService` fallback. Every task validates at entry (defense in depth).

`VersionFileRepository` defense-in-depth checks:
- `Load()`: calls `_context.FileExists(path)` before deserializing, throws `CakeException` with clear "does not exist" message.
- `SaveAsync()`: ensures parent directory exists via `_context.CreateDirectory(directory)` before writing.

`IPathService.ResolveVersionsOutputDirectory` and `GetResolveVersionsOutputFile()` deleted.

`release.yml`: workflow-level `VERSIONS_FILE` env var deduplicates 5 hardcoded occurrences. `resolve-versions` job now passes `--versions-file ${{ env.VERSIONS_FILE }}` (symmetric with downstream jobs).

`tools.cs`: `Shared.CleanArtifacts()` creates `artifacts/` directory after deletion. All three paths (`setup --source=local`, `setup --source=remote-github`, `ci-sim`) pass `--versions-file` to ResolveVersions steps.

### CleanArtifacts retired from Cake

`CleanArtifactsTask`, `CleanArtifactsPipeline`, and `CleanArtifactsPipelineTests` deleted. `ServiceCollectionExtensions.AddMaintenanceFeature()` no longer registers the pipeline. Target discovery no longer shows `CleanArtifacts`.

Rationale: CI never calls CleanArtifacts (immutable runners). Only `tools.cs` called it. Cleaning artifacts is local-dev hygiene, not a build pipeline stage.

### Testing infrastructure improvements

`Fixtures/Data/Versions/` embedded JSON fixtures (auto-discovered via `.csproj` glob `<EmbeddedResource Include="Fixtures\Data\**\*.json" />`):

| File | Content |
|---|---|
| `versions-valid.json` | `{"sdl2-core":"2.32.0","sdl2-image":"2.8.0"}` |
| `versions-empty.json` | `{}` |
| `versions-invalid-semver.json` | `{"sdl2-core":"not-a-version"}` |
| `versions-single-family.json` | `{"sdl2-core":"2.32.0"}` |

`VersionFileRepositoryTests` uses `FixtureLoader.Load("Versions/...")` instead of inline JSON strings. `FakeCakeWorldV2.WithVersionsFile(string)` auto-resolves relative paths against `_repoRoot`.

`docs/refactoring/testing-guidelines.md` — standalone canonical reference (like `extraction-guidelines.md`). Covers: filesystem rule, test data policy (embedded vs centralized inline), V2 vs V1 infra, scenario test structure, filesystem seeding, and anti-patterns. Cross-referenced from ADR-002 §12, plan §9, checklist §10, and AGENTS.md.

`docs/plan.md` Phase X section updated with current status and a **post-refactor task**: redesign `FakeCakeWorldV2` fluent API — method names like `WithVersionsFile`/`WithSuffix`/`WithRid` are confusing (they set CLI option values, not file contents). Defer to after P10 when all targets are on V2.

### Slopwatch baseline updated

Regenerated with `--create-baseline`: 49 entries (SW002: 30, SW003: 1, SW005: 6, SW006: 12). Command: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch\baseline.json`

## Verification Witness (post-slice, all gates green)

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
# 578 total, 578 passed

dotnet build build/_build/Build.csproj
# 0 warnings, 0 errors

slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch\baseline.json
# 0 issue(s) found

dotnet run --file tools.cs -- build --tree
# CleanArtifacts NOT in target list; ResolveVersionsFromManifest + ResolveVersionsFromExplicit present
```

## Onboarding Snapshot

| Concern | Current state |
| --- | --- |
| Stack | .NET 10 / C# 14, Cake Frosting 6.1, vcpkg, TUnit/Microsoft.Testing.Platform, GitHub Actions |
| Build host | `build/_build/`, migrating from `Features/` to target-centric `Targets/` |
| Local orchestration | `tools.cs` — day-to-day surface; direct Cake invocation for CI debugging and target discovery only |
| Migrated targets | `Targets/Info`, `Targets/ResolveVersionsFromManifest`, `Targets/ResolveVersionsFromExplicit` |
| Retired | `CleanArtifacts` (Cake target), `Features/Versioning/ServiceCollectionExtensions.cs`, `VersionsJsonWriter`, `Configurations` aggregate (in progress), `PathService.ResolveVersionsOutputDirectory` |
| Tests | 578 build-host tests, all V2 for migrated targets |
| Slopwatch | 49 baseline entries, strict rule severities |
| Remaining P4 candidates | `CompileSolution`, `Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`, `Inspect-HarvestedDependencies` |
| Open design | Fluent API redesign for `FakeCakeWorldV2` (tracked in plan.md Phase X post-refactor — deferred to after P10) |

## Recommended Next Step

1. **`CompileSolution` migration** — low-risk maintenance target. Follow the established P4 pattern: V2 scenario tests, `Targets/CompileSolution/`, no pipeline wrapper, Cake-native abstractions for process/build invocation.

Or:

2. **Diagnostic target migration** — `Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`, `Inspect-HarvestedDependencies`. These are tool wrappers that may belong under `Tools/` rather than `Targets/`. Evaluate destination before moving.

Talk to Deniz before choosing. Master-direct commits are acceptable, but ADR-002 migration slices still require the documented review/approval flow.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `CLAUDE.md`
4. `docs/plan.md`
5. `docs/decisions/2026-05-05-target-centric-build-host.md`
6. `docs/refactoring/target-centric-build-host-refactor-plan.md`
7. `docs/refactoring/target-centric-build-host-review-checklist.md`
8. `docs/refactoring/extraction-guidelines.md`
9. `docs/refactoring/testing-guidelines.md` ← NEW — canonical test reference
10. `build/_build/Targets/ResolveVersionsFromManifest/ResolveVersionsFromManifestTask.cs`
11. `build/_build/Targets/ResolveVersionsFromExplicit/ResolveVersionsFromExplicitTask.cs`
12. `build/_build/Repositories/VersionFileRepository.cs` ← defense-in-depth example
13. `build/_build/Repositories/ServiceCollectionExtensions.cs`
14. `build/_build/Host/BuildContext.cs`
15. `tools.cs`
16. `.github/workflows/release.yml`

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
- **V2 test infrastructure is the default for anything touched** — broader than the target-migration rule. V1 fixtures (`FakeRepoBuilder`, `TestHostFixture`) are frozen.
- **Test data belongs in embedded fixture files** (`Fixtures/Data/`) or centralized static classes — never raw string literals in test methods. See `testing-guidelines.md`.
- **`--versions-file` is universal** — both writers and readers use it. `IVersionFileRepository` path at call time. Tasks validate at entry.
- **`CleanArtifacts` is not a Cake target.** Local cleanup lives in `tools.cs`.

## Final Steering Note

P4 moved from "pattern proven" to "pattern repeated" to "pattern streamlined." The `--versions-file` asymmetry that blocked deeper version-loading refactors is gone. `CleanArtifacts` — a local-dev concern masquerading as a pipeline stage — is where it belongs. The test infrastructure now has a canonical rulebook that future agents can be pointed at.

The next slice should be boring: pick a target, move it, verify, commit. No more design decisions needed until P5 (strategy retirement) or P6 (PreFlight boss fight). Keep it boring.
