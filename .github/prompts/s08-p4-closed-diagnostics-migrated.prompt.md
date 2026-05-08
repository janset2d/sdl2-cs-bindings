---
name: "S08 ADR-002 target-centric refactor - P4 closed (CompileSolution retired, lifecycle codified, diagnostic targets migrated)"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the P4 closer landed. Two slices shipped in this run: (1) CompileSolution retirement + docs/refactoring lifecycle codification + drift-corrected §4 inventory, (2) all four diagnostic targets migrated to Targets/<PascalCase>/ + 3 collaborators extracted (LibraryClassifier, OtoolReporter, HarvestPayloadInspector) + 2 BuildContext properties added (Dlls, Libraries) + IAnsiConsole injection for OtoolReporter. P4 is closed; next is P5 (strategy + Coverage-Check retirement) or P6 (PreFlight migration). 591 tests pass, slopwatch 0."
argument-hint: "Start by verifying current git state (HEAD should include CompileSolution retirement + temp doc cleanup + diagnostic-targets migration commits). Then design S10 — P5 strategy retirement is the natural next per ADR-002 §11 sequencing, but P6 PreFlight migration is also valid if Deniz wants the boss fight first. Real domain work either way."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering the janset2d/sdl2-cs-bindings repository after the **P4 closer** of the ADR-002 build-host refactor. Two slices shipped in the last run:

1. **S08 — CompileSolution retired from Cake** (zero callers; mirrors the S07 `CleanArtifacts` retirement) plus the **`docs/refactoring/` lifecycle principle codified** in `README.md` (temp per-slice specs/plans never enter git history; audit findings/learnings/follow-ups must migrate to canonical docs before deletion). Also drift-corrected §4 inventory by dropping the stale `CleanArtifacts` row left by S07.

2. **S09 — Diagnostic targets migrated** to `Targets/<PascalCaseTargetName>/`: `Dumpbin-Dependents`, `Ldd-Dependents`, `Otool-Analyze`, `Inspect-HarvestedDependencies`. Three collaborators extracted (`LibraryClassifier` static, `OtoolReporter` with `IAnsiConsole`, `HarvestPayloadInspector`); two `BuildContext` named properties added (`Dlls`, `Libraries`); `OtoolAnalyzePipeline` and `InspectHarvestedDependenciesPipeline` retired; `Features/{DependencyAnalysis, Diagnostics}/` folders gone. Plus a hyphen-folder convention reversal: `Targets/` folders are now PascalCase across the board (Cake target hyphens stay on `[TaskName]` only).

**P4 is closed.** Next phase is P5 (strategy + Coverage-Check retirement) or P6 (PreFlight migration). ADR-002 §11 sequencing puts P5 first because strategy code is concentrated in PreFlight/Package/Harvest and retiring it before P6 reduces mixed-architecture drag.

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-05-08` — `s08-p4-closed-diagnostics-migrated`)** and verify against the live repo, git log, and canonical docs before acting.

## What Just Happened

### S08 — CompileSolution retirement + docs/refactoring lifecycle (commit `4beaee2` + cleanup `81f647f`)

`CompileSolution` was a Cake target wrapping `dotnet build Janset.SDL2.sln -c <config> --nologo`. Audit found zero callers across `release.yml`, `tools.cs`, `.github/actions/`, `[IsDependentOn]` dependencies, `launchSettings.json` profiles, and the playbook. Stronger retirement case than CleanArtifacts (which at least had a `tools.cs` caller). Bare `dotnet build` covers the use case.

Deletions: `Features/Maintenance/{CompileSolutionTask, CompileSolutionPipeline, ServiceCollectionExtensions}.cs`, the entire `Features/Maintenance/` folder, `Unit/Features/Maintenance/CompileSolutionPipelineTests.cs`, the `AddMaintenanceFeature_Should_Register_All_Pipeline_And_Validator_Types` smoke test method, and `Program.cs`'s `.AddMaintenanceFeature()` registration + import.

`docs/refactoring/README.md` gained a "Document lifecycle" section codifying the rule:

> Files in this directory split into two lifecycles:
> - **Durable** — accepted plans, ADRs, checklists, guidelines. Canonical homes for cross-slice knowledge.
> - **Temporary per-slice** — design specs and execution plans for a single ADR-002 migration slice. Working-tree scratch only; never enter git history.
>
> When a slice ships, audit the temp docs before deletion: unique findings, learnings, or open follow-ups must be migrated to their canonical home before the temp doc is deleted. The repo carries "what to know going forward", not "what was done".

Index drift was also fixed: `testing-guidelines.md` and `conversation-history.md` rows added; stale `p1-baseline-notes.md` row removed.

§4 target inventory in `target-centric-build-host-refactor-plan.md` had both retired-from-Cake rows (`CleanArtifacts` from S07, `CompileSolution` from S08) dropped together — drift correction.

### S09 — Diagnostic targets migration (commit will be top-of-log when next agent reads)

Migrated four targets from `Features/{DependencyAnalysis, Diagnostics}/` to `Targets/<PascalCaseTargetName>/`. Cake target hyphens stay on `[TaskName]` (operator-facing CLI); folders + namespaces are C#-conventional. Matches existing `Targets/Info/` and `Targets/ResolveVersionsFromManifest/` shape.

Production layout:

```text
build/_build/Targets/
  DumpbinDependents/DumpbinDependentsTask.cs        (renamed from DependentsTask)
  LddDependents/LddDependentsTask.cs                (renamed from LddTask)
  OtoolAnalyze/
    OtoolAnalyzeTask.cs
    Services/LibraryClassifier.cs                   (static, pure policy)
    Reporting/OtoolReporter.cs                      (IAnsiConsole-injected)
    ServiceCollectionExtensions.cs
  InspectHarvestedDependencies/
    InspectHarvestedDependenciesTask.cs
    Services/HarvestPayloadInspector.cs
    ServiceCollectionExtensions.cs
```

`OtoolAnalyzePipeline` and `InspectHarvestedDependenciesPipeline` retired entirely. `LibraryClassifier` became static (CA1822 — pure policy with no instance state); `OtoolReporter` and `HarvestPayloadInspector` are sealed classes registered via per-target `AddOtoolAnalyzeTarget()` / `AddInspectHarvestedDependenciesTarget()` extensions.

`BuildContext` gained `Dlls` and `Libraries` named CLI properties (read from `ParsedArguments.Dll` / `ParsedArguments.Library`). All migrated diagnostic tasks read from these properties; the `Configurations.Dumpbin.DllToDump` / `Configurations.Vcpkg.Libraries` reads in diagnostic code paths are gone. `Configurations` aggregate retirement is still in progress for unmigrated callers (Package, ConsumerSmoke, Harvest).

`InspectHarvestedDependenciesTask` swapped to `IManifestRepository.Load()` (P3 repository) instead of injected `ManifestConfig`. Aligns with the ResolveVersions migration pattern.

V2 test infra (`FakeCakeWorldV2`) gained four new fluent seeders + one bug fix:

- `WithDll(string)` / `WithDlls(params string[])` / `WithLibraries(params string[])` — CLI option seeders for the new `BuildContext.Dlls` / `BuildContext.Libraries` properties.
- `WithProcessSideEffect(cmd, world => ...)` — fires before the process result is returned, lets the test mutate the fake world (typically seeding files into the fake filesystem to simulate side effects of a real tool — e.g. `tar` extraction populating a destination directory). Required for happy-path scenarios that invoke tools producing filesystem output, because pre-seeding via `WithTextFile` alone gets wiped by the inspector's `DeleteDirectory` calls.
- `WithToolPath(toolName, path)` — per-tool override (existing global `WithToolPath(path)` stays as legacy default). Required when a scenario invokes multiple `Tool<TSettings>` wrappers and each must resolve to a distinct executable so process invocations get distinct filename keys (e.g. `tar` and `ldd` in Linux Inspect scenarios).
- Bug fix: `CreateLinux()` / `CreateOsx()` now set `_rid` correctly. They were previously leaving it at the default `"win-x64"`, silently breaking RuntimeFamily resolution for Unix scenarios. Latent bug; only surfaced when Inspect tests exercised platform-specific code paths.

`testing-guidelines.md` gained a paragraph documenting these new fixture methods and the `.exe`-suffix nuance for Windows tool process keys.

Three Phase X "Post-refactor tasks (after P10)" follow-ups added to `docs/plan.md`:

- **Unify diagnostic target UX** — bring Dumpbin/Ldd up to Otool's elaboration (per-platform system-library classifier, dependency table, manifest.json `system_exclusions` suggestion section). Consume `Integrations/DependencyAnalysis/{WindowsDumpbinScanner, LinuxLddScanner, MacOtoolScanner}` services for DRY with Harvest. Pairs with `Integrations/` dissolution.
- **Unify diagnostic target binary inputs** — collapse `--dll` and `--library` into a single CLI option that auto-detects path vs manifest name. CLI contract change with deprecation cycle.
- **Resolve `Otool-Analyze`'s stale hardcoded vcpkg triplets** — `x64-osx-dynamic` / `arm64-osx-dynamic` are pre-hybrid-static. Likely a stale dev convenience nobody uses; decide delete-or-modernize.

Architectural finding worth retaining: **diagnostic targets and Harvest are decoupled at the consumption level.** Harvest uses `Integrations/DependencyAnalysis/{WindowsDumpbinScanner, LinuxLddScanner, MacOtoolScanner}` services (higher-level scanners). Diagnostic targets bypass those and call `context.DumpbinDependents/LddDependencies/OtoolDependencies` Cake aliases directly. Two consumption paths over the same `Tools/{Dumpbin,Ldd,Otool}/` wrappers. Future UX-unification follow-up will likely converge on the Scanner services for DRY.

### Lifecycle convention reinforced

S08 introduced the lifecycle principle; S09 retroactively applied a corollary: **when migrating files between folders, run `git mv` FIRST before content edits**. This makes the commit record `rename + modify` instead of `delete + add`, preserving `git log --follow` and `git blame -C` history continuity. Files being retired (no destination — e.g. `*Pipeline.cs` dissolved into the task body) stay as true deletes.

In S09 four files used explicit `git mv`:
- `Features/DependencyAnalysis/DependentsTask.cs` → `Targets/DumpbinDependents/DumpbinDependentsTask.cs` (rename + content)
- `Features/DependencyAnalysis/LddTask.cs` → `Targets/LddDependents/LddDependentsTask.cs` (rename + content)
- `Features/DependencyAnalysis/OtoolAnalyzeTask.cs` → `Targets/OtoolAnalyze/OtoolAnalyzeTask.cs` (move + significant content rewrite)
- `Features/Diagnostics/InspectHarvestedDependenciesTask.cs` → `Targets/InspectHarvestedDependencies/InspectHarvestedDependenciesTask.cs` (move + modest content rewrite)

The rule is in agent memory under `feedback_git_mv_during_migrations.md`.

## Verification Witness (post-S09, all gates green)

```pwsh
git log -4 --oneline
# <hash> refactor: migrate diagnostic targets to Targets/, close P4   (S09 — top-of-log)
# 81f647f chore: delete S08 temp design + plan docs
# 4beaee2 refactor: retire CompileSolution from Cake, codify docs/refactoring lifecycle
# 7179aaf refactor: universal --versions-file contract, retire CleanArtifacts from Cake

dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
# 591 total, 591 passed (post-S09; was 573 pre-S09: −8 deleted, +26 added)

dotnet build build/_build/Build.csproj
# 0 warnings, 0 errors

slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**" --baseline .slopwatch/baseline.json
# 0 issue(s) found

dotnet run --file tools.cs -- build --tree
# Dumpbin-Dependents, Ldd-Dependents, Otool-Analyze, Inspect-HarvestedDependencies still listed
# (Cake [TaskName] attributes preserved through migration)
# CompileSolution and CleanArtifacts NOT in target list
# Features/DependencyAnalysis and Features/Diagnostics namespaces gone
```

## Onboarding Snapshot

| Concern | Current state |
| --- | --- |
| Stack | .NET 10 / C# 14, Cake Frosting 6.1, vcpkg, TUnit/Microsoft.Testing.Platform, GitHub Actions |
| Build host | `build/_build/`, P0–P4 of the ADR-002 target-centric refactor closed |
| Local orchestration | `tools.cs` — day-to-day surface; direct Cake invocation for CI debugging and target discovery only |
| Migrated targets | `Targets/{Info, ResolveVersionsFromManifest, ResolveVersionsFromExplicit, DumpbinDependents, LddDependents, OtoolAnalyze, InspectHarvestedDependencies}` |
| Retired (not migrated) | `CleanArtifacts` (S07), `CompileSolution` (S08), `Features/Versioning/ServiceCollectionExtensions.cs` (P3), `VersionsJsonWriter` (P3), `Features/Maintenance/` folder (S08), `Features/{DependencyAnalysis, Diagnostics}/` folders (S09), `OtoolAnalyzePipeline` (S09), `InspectHarvestedDependenciesPipeline` (S09), `PathService.ResolveVersionsOutputDirectory` (S07) |
| Unmigrated targets (`Features/`) | Harvesting, Packaging, Preflight, Publishing, Vcpkg, Ci, Coverage |
| Tests | 591 build-host tests, all green; V2 across migrated targets |
| Slopwatch | 49 baseline entries, strict rule severities |
| Phase status | P0–P4 closed. Active = P5 (strategy + Coverage-Check retirement) **or** P6 (PreFlight migration); ADR-002 §11 sequencing puts P5 first |
| Open Phase X follow-ups (after P10) | `FakeCakeWorldV2` fluent API redesign (S03 era); diagnostic target UX unification + binary input unification + hardcoded triplet cleanup (all S09 era) |

## Recommended Next Step

**S10 — P5 strategy + Coverage-Check retirement.** ADR-002 §11 puts P5 before P6 because strategy code is concentrated in PreFlight/Package/Harvest and retiring it before P6 reduces mixed-architecture drag during the PreFlight boss fight.

P5 scope per refactor plan §11:

**Coverage tasks:**
1. Remove `Coverage-Check` Cake target (`Features/Coverage/`).
2. Remove coverage baseline gate from `release.yml`.
3. Remove coverage-specific build-host code and tests.
4. Keep test result publishing/reporting independent of coverage.

**Strategy tasks:**
1. Remove `strategy` field from `build/manifest.json`.
2. Update manifest models.
3. Remove strategy resolver/factory/polymorphism code under `Shared/Strategy/`.
4. Remove strategy coherence validators (in PreFlight area).
5. Update preflight guardrails to validate the real invariant: all runtimes use the supported hybrid-static triplet model (encoded in `vcpkg-overlay-triplets/`).
6. Update tests asserting old strategy behavior.
7. Rename guardrail-ID-first names to behavior-first names where touched.

P5 closes when no Cake target named `Coverage-Check` exists, `manifest.json` has no `strategy` field, hybrid-static remains documented as invariant, and PreFlight still catches actual manifest/runtime mistakes.

Talk to Deniz before settling slice scope. Coverage retirement is mostly delete-only (clean and boring); strategy retirement is the meatier half. Could ship as one bundle (S10) or split (S10 coverage, S11 strategy) — Deniz's call based on appetite.

Alternative: **P6 first if Deniz wants to reverse the order.** PreFlight migration is the boss fight (cross-cutting validation, G54/G58 guardrails, manifest lowercase invariant addition). Doable but harder if strategy code is still alive.

After P5 + P6, P7 (Package), P8 (Harvest), P9 (Publishing/ConsumerSmoke), P10 (final cleanup) remain.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `CLAUDE.md`
4. `docs/plan.md` ← Phase X status reflects P4 closure
5. `docs/decisions/2026-05-05-target-centric-build-host.md`
6. `docs/refactoring/README.md` ← Document lifecycle section (S08)
7. `docs/refactoring/target-centric-build-host-refactor-plan.md` ← §11 P5 scope is current focus
8. `docs/refactoring/target-centric-build-host-review-checklist.md`
9. `docs/refactoring/extraction-guidelines.md`
10. `docs/refactoring/testing-guidelines.md` ← V2 fixture rules including new S09 additions
11. `build/_build/Targets/Info/InfoTask.cs` ← IAnsiConsole pattern reference
12. `build/_build/Targets/OtoolAnalyze/OtoolAnalyzeTask.cs` + `Services/LibraryClassifier.cs` + `Reporting/OtoolReporter.cs` ← extraction shape reference
13. `build/_build/Targets/InspectHarvestedDependencies/InspectHarvestedDependenciesTask.cs` + `Services/HarvestPayloadInspector.cs` ← Inspector pattern reference
14. `build/_build/Host/BuildContext.cs` ← BuildContext named properties
15. `build/_build/Features/Coverage/` ← P5 source (retire this)
16. `build/_build/Shared/Strategy/` ← P5 source (retire this)
17. `build/_build/Features/Preflight/` ← P5 strategy validators to remove; P6 boss fight target
18. `build/manifest.json` ← strategy field to remove (P5)
19. `tools.cs`
20. `.github/workflows/release.yml` ← coverage baseline gate to remove (P5)

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
- **Test data belongs in embedded fixture files** (`Fixtures/Data/`) or centralized static classes — never raw string literals in test methods.
- **`--versions-file` is universal** — both writers and readers use it. `IVersionFileRepository` path at call time. Tasks validate at entry.
- **`CleanArtifacts`, `CompileSolution` are not Cake targets.** Local cleanup and bare-build invocation are not pipeline concerns.
- **`docs/refactoring/` temp docs never enter git history.** Audit findings/learnings/TODOs and migrate to canonical docs before deletion.
- **Migrate files with `git mv` FIRST, then edit content.** Preserves `git log --follow` history. Files being retired (no destination) stay as true deletes.
- **`Targets/` folder names are PascalCase** (Cake target hyphens live on `[TaskName]` only). Matches `Targets/Info/`, `Targets/ResolveVersionsFromManifest/`, and the four S09 diagnostic targets.

## Final Steering Note

P0–P4 closed in eight slices (S02 through S09). The migration pattern is **mature**: brainstorming → writing-plans → executing-plans → review checklist → commit summary → user approval → commit, with single-bundle atomic commits, V2 test infra, and `git mv` for moves. S10 onwards engages real domain work — strategy retirement is opinionated cleanup, PreFlight is the cross-cutting boss fight. No more "boring" slices.

Phase X post-refactor backlog has six items now (FakeCakeWorldV2 fluent API redesign + 3 diagnostic UX/input/triplet items + the codified lifecycle). Don't start them until P10 closes; they're explicit deferrals.

Talk to Deniz before scoping S10. Coverage retirement is the easier half of P5; strategy retirement is the harder half. Bundling vs splitting is his call.
