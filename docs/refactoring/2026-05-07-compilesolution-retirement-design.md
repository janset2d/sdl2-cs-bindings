# Slice S08 — Retire CompileSolution from Cake

- **Status:** Draft, awaiting plan + approval
- **Date:** 2026-05-07
- **Decision pillar:** [ADR-002 §1](../decisions/2026-05-05-target-centric-build-host.md) — _"Some abstractions exist because they were generated or anticipated, not because the current build domain needs them."_
- **Precedent:** Slice S07 — `CleanArtifacts` retirement (commit `7179aaf`).
- **Lifecycle:** Temp per-slice design spec. Deletes after the slice ships, per the `docs/refactoring/` lifecycle convention.

## 1. Context

`CompileSolution` is a Cake target under `build/_build/Features/Maintenance/` whose body is a thin pass-through:

```csharp
// CompileSolutionPipeline.cs
public Task RunAsync()
{
    var solution = _pathService.SolutionFile;
    var settings = new DotNetBuildSettings
    {
        Configuration = _buildConfiguration.Configuration,
        NoLogo = true,
    };

    _log.Information("Building solution '{0}' (Configuration={1}).", solution.FullPath, _buildConfiguration.Configuration);
    _cakeContext.DotNetBuild(solution.FullPath, settings);

    return Task.CompletedTask;
}
```

Behavior: `dotnet build Janset.SDL2.sln -c <config> --nologo`.

P4 of the ADR-002 refactor flagged it as a remaining migration candidate ([refactor plan §11](target-centric-build-host-refactor-plan.md), priming prompts S05/S06/S07). Step-back review during slice S08 surfaced that no part of the build invokes it.

## 2. Caller audit

| Surface | Reference? |
| --- | --- |
| `.github/workflows/release.yml` | No |
| `.github/actions/*` | No |
| `tools.cs` | No |
| Cake target dependencies (`[IsDependentOn(typeof(CompileSolutionTask))]`) | No |
| `build/_build/Properties/launchSettings.json` profiles | No |
| `docs/playbook/*` | No |

Repo-wide grep evidence: `CompileSolution` appears only in (a) the target's own production+test files, (b) the ADR-002 refactor plan inventory and remaining-candidates list, (c) `docs/plan.md` Phase X status sentence, and (d) historical priming prompts S05/S06/S07 (immutable date-stamped artifacts).

The target hosts no logic that bare `dotnet build Janset.SDL2.sln -c Release --nologo` does not already provide. The lone differentiator is the `--nologo` flag (cosmetic console suppression).

## 3. Decision

**Retire `CompileSolution` from the Cake build host instead of migrating it.**

Rationale aligns with three load-bearing rules:

1. **ADR-002 §1** — abstractions must earn their place; orphaned targets do not.
2. **Slice S07 precedent** (`CleanArtifacts` retirement) — local-dev convenience that nobody invokes is a documentation/playbook concern, not a Cake target. CompileSolution is a strictly stronger case than CleanArtifacts: CleanArtifacts had a real `tools.cs` caller; CompileSolution has none.
3. **AGENTS.md Engineering Preferences** — _"engineered enough: not hacky, not over-abstracted."_ Migrating an unused target to the new architecture is over-abstraction.

**Out of scope:** adding a `tools.cs` convenience replacement. The `--nologo` flag is not worth a wrapper command. Developers run `dotnet build Janset.SDL2.sln` (or `dotnet build src/SDL2.Core/SDL2.Core.csproj` for managed-only, already documented in `CLAUDE.md`).

## 4. Scope

### 4.1 Production deletions

| Path | Action |
| --- | --- |
| `build/_build/Features/Maintenance/CompileSolutionTask.cs` | Delete |
| `build/_build/Features/Maintenance/CompileSolutionPipeline.cs` | Delete |
| `build/_build/Features/Maintenance/ServiceCollectionExtensions.cs` | Delete |
| `build/_build/Features/Maintenance/` (folder) | Delete (last resident gone) |
| `build/_build/Program.cs` | Edit: remove `.AddMaintenanceFeature()` call (line ~130) and the `using Build.Features.Maintenance;` import |

### 4.2 Test deletions

| Path | Action |
| --- | --- |
| `build/_build.Tests/Unit/Features/Maintenance/CompileSolutionPipelineTests.cs` | Delete (4 ctor null-guard tests; the file's own comment acknowledges they are low-signal) |
| `build/_build.Tests/Unit/Features/Maintenance/` (folder) | Delete (last resident gone) |
| `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` | Edit: remove the `AddMaintenanceFeature_Should_Register_All_Pipeline_And_Validator_Types` test method only. File persists with other smoke tests. |

Test count delta: **578 → 573** (−4 ctor + −1 smoke). All-green is the gate; the absolute number is a witness, not a target.

### 4.3 Documentation updates

`docs/refactoring/target-centric-build-host-refactor-plan.md`:

- §4 target inventory table (lines 112–113): remove **both** the `CleanArtifacts` and `CompileSolution` rows. S07 retired `CleanArtifacts` but left its inventory row intact; this slice corrects that drift in addition to dropping its own row. After this edit, the §4 table no longer references either retired-from-Cake target.
- §11 P4 "Completed in this phase" list: add a numbered item for the retirement, mirroring item 5 (CleanArtifacts). Suggested wording:

  ```text
  N. CompileSolution **retired** (not migrated — zero Cake callers;
     bare `dotnet build Janset.SDL2.sln` covers the use case)
  ```

- §11 P4 "Remaining suggested order" (line ~751): drop the `CompileSolution` entry; only diagnostic targets remain (`Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`, `Inspect-HarvestedDependencies`).

`docs/plan.md`:

- Phase X status sentence (line ~17): replace `Remaining P4 candidates: CompileSolution and diagnostic targets` with `Remaining P4 candidates: diagnostic targets`.
- Phase X — Build-Host Modernization paragraph (line ~75): mirror the line-17 update; mention CompileSolution retirement alongside CleanArtifacts as the "retired, not migrated" pair.

### 4.4 Files explicitly NOT changed

| Surface | Reason |
| --- | --- |
| `build/_build/Host/Configuration/DotNetBuildConfiguration.cs` | Still consumed by `PackagePipeline`, `PackageConsumerSmokePipeline`, `FakeRepoBuilder`, `FakeCakeWorldV2`. Retires when those callers migrate or when the `Configurations` aggregate retirement closes. |
| `build/_build/Properties/launchSettings.json` | No `CompileSolution` profile exists. The pre-existing `Coverage-Check` profile is unrelated residue from before ADR-002 (covered under ADR-002 P5 retirement). |
| `.github/prompts/s05-*.md`, `s06-*.md`, `s07-*.md` | Historical date-stamped priming prompts, immutable artifacts. Consistent with S07 handling. |
| `tools.cs` | No replacement command added. `dotnet build` is one line. |
| Slopwatch baseline | Regenerated only if entries reference deleted symbols; otherwise untouched. |

## 5. Verification gate

Run after deletions, before commit:

```pwsh
dotnet build build/_build/Build.csproj
# Expect: 0 warnings, 0 errors

dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
# Expect: ~573 total, all passed

dotnet run --file tools.cs -- build --tree
# Expect: CompileSolution NOT in the target list

slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch\baseline.json
# Expect: 0 issue(s) found
# Regenerate baseline ONLY if entries reference deleted code (use --create-baseline)
```

Slopwatch is mandatory after LLM-authored changes per AGENTS.md and `dotnet-skills:dotnet-slopwatch`.

## 6. Approval gate

This slice deletes production code and modifies the build system / DI composition root. AGENTS.md "Approval Gate" applies: explicit `go / apply / proceed / başla / yap` is required before any code or build-system edits. The slice flow:

1. Brainstorming (this doc) → user approval of design
2. `writing-plans` skill → detailed implementation plan → user approval
3. `executing-plans` skill → execute plan → review checklist walk-through
4. Present commit summary + proposed commit message → user approval
5. Commit on `master` (master-direct mirroring slice S07; ADR-002 isolated-branch convention is satisfied because this slice has no native-build dependency).

The doc-only changes in §4.3 are themselves allowed under the AGENTS.md "Documentation-only edits" exception, but in practice they ship in the same commit as the code/test deletions for atomicity.

## 7. Forward-looking impact

After this slice lands:

- `Features/` contains only unmigrated boss-fight targets (Harvesting, Packaging, Preflight, Publishing, Vcpkg, Ci, DependencyAnalysis, Diagnostics, Coverage). All "easy P4" residents are gone — Maintenance retires entirely.
- P4 remaining work narrows to the four diagnostic targets (`Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`, `Inspect-HarvestedDependencies`). Slice S09 candidate.
- After diagnostics, P4 closes and the next active phase is P5 (strategy + Coverage-Check retirement) or P6 (PreFlight migration), per the ADR-002 sequencing.

## 8. References

- [ADR-002: Target-Centric Cake Build Host Architecture](../decisions/2026-05-05-target-centric-build-host.md)
- [Refactoring plan — §11 P4](target-centric-build-host-refactor-plan.md)
- [Review checklist](target-centric-build-host-review-checklist.md)
- [Testing guidelines](testing-guidelines.md)
- [AGENTS.md](../../AGENTS.md) — approval gate, build-host reference pattern, slopwatch mandate
- Slice S07 (CleanArtifacts retirement) — commit `7179aaf`, priming prompt `.github/prompts/s07-p4-versions-file-resolved-cleanartifacts-retired.prompt.md`
