# P1 — Baseline and Inventory

Date: 2026-05-05
Status: complete

## 1. Environment

| Item | Value |
| --- | --- |
| OS | Windows 11 Pro 10.0.26200 (x64) |
| .NET SDK | 10.0.203 |
| Cake Version | 6.1.0.0 |
| RID | win-x64 |
| Vcpkg Triplet | x64-windows-hybrid |
| Branch | master |
| HEAD | 85de4ec |

## 2. Test Baseline

```
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

- **total**: 482
- **passed**: 482
- **failed**: 0
- **skipped**: 0
- **duration**: 1s 472ms

All 482 tests pass on a clean master. This is the pre-migration baseline.

## 3. Target Inventory

20 Cake targets across 12 Features modules. No `IsDependentOn`/`IsDependeeOf` calls exist — the Cake target graph is flat; all orchestration is external.

| # | Target Name | Task Class | Feature Module | Risk |
| --- | --- | --- | --- | --- |
| 1 | CleanArtifacts | `CleanArtifactsTask` | Maintenance | Low |
| 2 | CompileSolution | `CompileSolutionTask` | Maintenance | Low |
| 3 | ConsolidateHarvest | `ConsolidateHarvestTask` | Harvesting | Medium |
| 4 | Coverage-Check | `CoverageCheckTask` | Coverage | N/A — retired P5 |
| 5 | Dumpbin-Dependents | `DependentsTask` | DependencyAnalysis | Low (dev tool) |
| 6 | EnsureVcpkgDependencies | `EnsureVcpkgDependenciesTask` | Vcpkg | Medium |
| 7 | GenerateMatrix | `GenerateMatrixTask` | Ci | Medium |
| 8 | Harvest | `HarvestTask` | Harvesting | High |
| 9 | Info | `InfoTask` | Info | Low (P2 first target) |
| 10 | Inspect-HarvestedDependencies | `InspectHarvestedDependenciesTask` | Diagnostics | Low (dev tool) |
| 11 | Ldd-Dependents | `LddTask` | DependencyAnalysis | Low (dev tool, Linux-only) |
| 12 | NativeSmoke | `NativeSmokeTask` | Harvesting | Medium |
| 13 | Otool-Analyze | `OtoolAnalyzeTask` | DependencyAnalysis | Low (dev tool, macOS-only) |
| 14 | Package | `PackageTask` | Packaging | High |
| 15 | PackageConsumerSmoke | `PackageConsumerSmokeTask` | Packaging | High |
| 16 | PreFlightCheck | `PreFlightCheckTask` | Preflight | High |
| 17 | PublishPublic | `PublishPublicTask` | Publishing | Low (stub) |
| 18 | PublishStaging | `PublishStagingTask` | Publishing | Medium |
| 19 | ResolveVersionsFromExplicit | `ResolveVersionsFromExplicitTask` | Versioning | Low (golden example) |
| 20 | ResolveVersionsFromManifest | `ResolveVersionsFromManifestTask` | Versioning | Low (golden example) |

### Golden examples (pre-migration)

`ResolveVersionsFromManifestTask` and `ResolveVersionsFromExplicitTask` are cited in AGENTS.md as "closer to the desired task-owned orchestration style." These two use DI collaborators directly (`ManifestConfig`, `VersionsJsonWriter`) rather than `*Pipeline` wrappers, and are the reference point for the P2 Info task migration.

### Key architectural observations

- **No Cake-level dependencies.** Targets are called individually via `--target X`. The 9-stage CI pipeline in `release.yml` orchestrates the sequence. `tools.cs` orchestrates a subset for local `ci-sim`.
- **All tasks (except Dumpbin-Dependents) use primary-constructor DI.** Dependencies flow through `Program.cs` ConfigureBuildServices → 12 `AddXFeature()` calls + 3 cross-cutting registrations (HostBuildingBlocks, Integrations, ToolWrappers).
- **14 of 20 tasks use `*Pipeline` wrappers** as their primary collaborator. The golden examples (ResolveVersionsFromManifest, ResolveVersionsFromExplicit) and a few simple tasks (Dumpbin-Dependents, Ldd-Dependents, Otool-Analyze) skip the pipeline pattern.

## 4. Command-Contract Call Sites

### release.yml (CI pipeline)

Target references with their CI stage context:

```
Coverage-Check           — post-build coverage gate
ResolveVersionsFromManifest — version resolution (suffix from manifest)
ResolveVersionsFromExplicit — version resolution (explicit CLI)
PreFlightCheck           — pre-harvest validation
GenerateMatrix           — RID matrix generation
Info                     — pre/post-harvest environment info
Harvest                  — per-RID native harvesting
NativeSmoke              — per-RID C++ harness validation
ConsolidateHarvest       — cross-RID merge
Package                  — dotnet pack with version propagation
PackageConsumerSmoke     — per-RID consumer-side TUnit smoke
PublishStaging           — push to GitHub Packages
```

### tools.cs (local dev orchestration)

Two command surfaces reference Cake targets:

- **`ci-sim`** (mini CI replay): CleanArtifacts → ResolveVersionsFromManifest → PreFlightCheck → EnsureVcpkgDependencies → Harvest → NativeSmoke → ConsolidateHarvest → Package → PackageConsumerSmoke
- **`build`** (passthrough): CleanArtifacts → ResolveVersionsFromManifest → PreFlightCheck → EnsureVcpkgDependencies → Harvest → ConsolidateHarvest → Package

### .github/actions/

Three action files (nuget-cache, vcpkg-setup, platform-build-prereqs) — infrastructure only, zero Cake target references.

## 5. Old-Architecture Test Inventory

### ArchitectureTests.cs (5 invariants)

`build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs`

| Test | What it enforces |
| --- | --- |
| `Shared_Should_Have_No_Outward_Or_Cake_Dependencies` | `Build.Shared.*` → no Cake, no Host/Features/Tools/Integrations refs |
| `Tools_Should_Have_No_Feature_Dependencies` | `Build.Tools.*` → Cake + Shared only, no Feature/Host/Integrations refs |
| `Integrations_Should_Have_No_Feature_Dependencies` | `Build.Integrations.*` → Cake + Shared only, no Feature/Host/Tools refs (2 permanent Host→Integrations allowlist entries) |
| `Features_Should_Not_Cross_Reference` | `Build.Features.X.*` → may not reference `Build.Features.Y.*` |
| `Host_Is_Free` | `Build.Host.*` may reference any layer |

Marked for deletion in P10 per ADR-002 §P10. Must NOT be updated during migration — will break naturally as layers are dismantled.

### Tests coupled to Features/ namespaces

42 test files across 12 feature-namespaced test directories. All use `using Build.Features.*` imports and `namespace Build.Tests.Unit.Features.*` namespaces. These will need namespace updates as targets migrate from `Features/` to `Targets/`.

Additional couplings:
- `ServiceCollectionExtensionsSmokeTests.cs` — 12 `using Build.Features.*` imports
- `ProgramCompositionRootTests.cs` — 4 `using Build.Features.*` imports
- `FakeRepoBuilder.cs:215` — `new Configurations(...)` (Host/Configuration aggregate, retired P5)

## 6. tools.cs Duplication

### Full inline replica of FamilyIdentifierConventions

`tools.cs` lines 650–698 contain a point-for-point replica of `Build.Features.Preflight.FamilyIdentifierConventions`:

| tools.cs method | Cake host equivalent |
| --- | --- |
| `ManagedPackageId(string)` | `FamilyIdentifierConventions.ManagedPackageId(string)` |
| `NativePackageId(string)` | `FamilyIdentifierConventions.NativePackageId(string)` |
| `VersionPropertyName(string)` | `FamilyIdentifierConventions.VersionPropertyName(string)` |
| `ParseFamily(string)` → `(string Major, string Role)` | `FamilyIdentifierConventions.Parse(string)` → `(string SdlMajor, string Role)` |
| `ToPascalCase(string)` | `FamilyIdentifierConventions.ToPascalCase(string)` (private) |

The logic is identical; only exception messages differ slightly. `tools.cs` uses `dotnet run --file tools.cs -- setup` to generate `Janset.Local.props` from family data and needs these helpers at the orchestration layer.

### Partial manifest-parsing duplication

`tools.cs` lines 624–648 duplicate a subset of what `ManifestRepository` does in the Cake host:
- `ManifestConfig` / `PackageFamilyConfig` records
- `GetConcreteFamilies(string repoRoot)` — reads `build/manifest.json`, filters families with both `managed_project` and `native_project`

This is a known tension: `tools.cs` is standalone by design (must not reference `build/_build` internals per AGENTS.md), so duplication is currently intentional. P2/P3 should evaluate whether to:
- Accept the duplication as a bounded contract
- Extract family-identifier conventions to a shared source (linked file or source-only NuGet)
- Move the conventions into `manifest.json` schema as computed fields

## 7. DI Verification

`InfoTask(InfoPipeline infoPipeline)` resolved and ran successfully via `--target Info` in 185ms. All 20 tasks use primary-constructor DI. Pattern confirmed viable for target-centric migration.

## 8. Doc-Reference Comments Inventory

ADR-002 §10: "Comments ... should explain the local logic or external constraint directly. They should not point readers to internal or external documentation as a substitute for explaining why the code behaves the way it does."

3 instances found in production code (low count — not a systemic problem):

| File | Line | Comment | Fix |
| --- | --- | --- | --- |
| `Features/Maintenance/CompileSolutionPipeline.cs` | 11 | "Replaces the WSL-playbook §8 solution-build step." | Describe what the pipeline does, not what playbook section it replaces. |
| `Features/Vcpkg/EnsureVcpkgDependenciesPipeline.cs` | 24 | "phase-x §8.2 (remaining P4-B debt — EnsureVcpkgDependenciesPipeline)" | Explain the vcpkg bootstrap/install flow directly; move debt tracking to issue/plan. |
| `Features/Packaging/PackageValidation.cs` | 7 | "Each constant maps 1:1 to a guardrail in the release-guardrails documentation." | Name each constant's purpose inline or remove the cross-reference. |

These are small enough to fix during their respective migration slices. No dedicated phase needed.

## 9. G-Number Naming Inventory

ADR-002 §5.8: "Types, files, methods, and test classes should use explicit build-domain names ... A guardrail ID may appear as report metadata, log evidence, test data, or documentation mapping, but the code identity should still be readable without knowing the release-guardrails document."

### Primary G-number identities (must be renamed)

All concentrated in the cross-family dependency resolvability area (G58):

| Current (G-number identity) | Proposed (behavior-first) | Location |
| --- | --- | --- |
| `G58CrossFamilyDepResolvabilityValidator` | `CrossFamilyDependencyResolvabilityValidator` | `Shared/Packaging/G58CrossFamilyDepResolvabilityValidator.cs` |
| `IG58CrossFamilyDepResolvabilityValidator` | `ICrossFamilyDependencyResolvabilityValidator` | `Shared/Packaging/IG58CrossFamilyDepResolvabilityValidator.cs` |
| `G58CrossFamilyCheckStatus` | `CrossFamilyDependencyCheckStatus` | `Shared/Packaging/G58CrossFamilyCheckModels.cs` |
| `G58CrossFamilyCheck` | `CrossFamilyDependencyCheck` | `Shared/Packaging/G58CrossFamilyCheckModels.cs` |
| `G58CrossFamilyValidation` | `CrossFamilyDependencyValidation` | `Shared/Packaging/G58CrossFamilyCheckModels.cs` |
| `G58CrossFamilyDepResolvabilityValidatorTests` | `CrossFamilyDependencyResolvabilityValidatorTests` | `_build.Tests/Unit/Features/Packaging/` |

### G-number metadata-only usage (acceptable)

Guardrail IDs in error message strings (e.g., `"G54: family '{name}' was not found..."`) and xmldoc summaries (e.g., `/// Guardrail G54: every entry...`) are acceptable as report metadata per ADR-002 §5.8. These appear in:

- `UpstreamVersionAlignmentValidator.cs` (G54)
- `PackageOutputValidator.cs` (G21-G27, G47, G48, G51, G55, G56, G57)
- `SatelliteUpperBoundValidator.cs` (G56)
- `NativePackageMetadata.cs` (G55)
- `ReadmeMappingTable.cs` (G57)
- `PackagePipeline.cs` (G56, G58)
- `PreflightReporter.cs` (G49, G54, G58)
- `CsprojPackContractValidator.cs` (G6, G7, G17, G18)

### Rename scope

The 6 primary identities above should be renamed during P5 (cross-family code is adjacent to strategy-era retirement). The interface (`IG58CrossFamilyDepResolvabilityValidator`) is justified per ADR-002 §8 — multiple production consumers exist (PreflightPipeline + PackagePipeline).

## 10. Risk Summary

| Risk | Detail |
| --- | --- |
| ArchitectureTests.cs | Will break during migration. Must not be updated — just let them fail and delete in P10. |
| 42 Feature-namespaced test files | Namespace updates needed as targets move. Bulk rename operation in late phases. |
| tools.cs duplication | Bounded and intentional today. Needs a P2/P3 decision on dedup strategy. |
| Pipeline wrappers | 14 tasks use `*Pipeline` as primary collaborator. Refactoring these is the bulk of P3–P7. |
| Configurations aggregate | `Host/Configuration/Configurations` with 5 axes. Retired P5 — tasks will read `BuildContext` properties directly. |
| Coverage-Check target | Retired P5 but still in release.yml and Cake graph. Remove target + CI reference together. |
| Strategy abstraction | `strategy` field in manifest, `StrategyResolver`, `StrategyCoherenceValidator`. Retired P5. |
| G58 G-number naming | 6 types/tests carry G58 as primary identity. Rename during P5 alongside strategy retirement. |
| Doc-reference comments | 3 instances — minor, fix inline during respective target migrations. |
