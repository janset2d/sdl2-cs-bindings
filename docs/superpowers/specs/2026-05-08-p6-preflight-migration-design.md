# S12 — P6 PreFlightCheck Target Migration Design

- **Status:** Accepted
- **Date:** 2026-05-08
- **ADR:** [ADR-002](../decisions/2026-05-05-target-centric-build-host.md)
- **Refactor plan:** §11 P6
- **Parent slice:** S11 (P5 closed — strategy retirement)

## 1. Goal

Migrate `Features/Preflight/` → `Targets/PreFlightCheck/`. Retire the `PreflightPipeline` orchestrator, delete 4 OneOf-shaped result types + `PreflightError` base class, relocate G58 + UpstreamVersionAlignment from `Shared/` to root-level named concepts, rename G58-prefixed types to behavior-first names, add the manifest lowercase family-name invariant validator, and migrate all 15 touched test files to V2 infrastructure.

## 2. Task shape

```csharp
[TaskName("PreFlightCheck")]
public sealed class PreFlightCheckTask(
    IManifestRepository manifestRepo,
    IVersionFileRepository versionRepo,
    HybridStaticOverlayValidator hybridStaticOverlayValidator,
    UpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    CsprojPackContractValidator csprojPackContractValidator,
    CrossFamilyDependencyResolvabilityValidator crossFamilyDependencyResolvabilityValidator,
    ManifestFamilyNameInvariantValidator manifestFamilyNameInvariantValidator,
    PreflightReporter reporter,
    ICakeContext cakeContext)
    : AsyncFrostingTask<BuildContext>
```

- Reads `BuildContext.VersionsFilePath` (named property).
- Loads manifest via `IManifestRepository`, versions via `IVersionFileRepository` (returns `PackageFamilyVersionSet`).
- Version consistency + core library identity remain static methods (pure, no DI needed).
- Runs 7 validators, each validated result passed to reporter.
- `HasErrors` checks inline — no OneOf `.OnError()` ceremony.
- Single `CakeException` at task boundary if any validator failed.

## 3. File operations

### 3.1 Shared/Versioning/ → root-level Versioning/

`Shared/Versioning/` becomes empty after this slice (all 4 files accounted for).

| Source | Operation | Destination |
|---|---|---|
| `Shared/Versioning/UpstreamVersionAlignmentValidator.cs` | `git mv` | `Versioning/UpstreamVersionAlignmentValidator.cs` |
| `Shared/Versioning/UpstreamVersionAlignmentValidation.cs` | `git mv` | `Versioning/UpstreamVersionAlignmentValidation.cs` |
| `Shared/Versioning/IUpstreamVersionAlignmentValidator.cs` | Delete | — |
| `Shared/Versioning/UpstreamVersionAlignmentResult.cs` | Delete (OneOf) | — |

### 3.2 Shared/Packaging/ G58 files → root-level Versioning/

G58 is a version-scope-cross-family check consumed by PreFlight and Package. It belongs in `Versioning/` (the domain is version mapping + dependency graph, not packaging mechanics). Root-level `Packaging/` doesn't exist yet and will be created in P7.

| Source | Operation | Destination |
|---|---|---|
| `Shared/Packaging/G58CrossFamilyDepResolvabilityValidator.cs` | `git mv` → rename | `Versioning/CrossFamilyDependencyResolvabilityValidator.cs` |
| `Shared/Packaging/G58CrossFamilyCheckModels.cs` | `git mv` → rename | `Versioning/CrossFamilyDependencyModels.cs` |
| `Shared/Packaging/IG58CrossFamilyDepResolvabilityValidator.cs` | Delete (single impl) | — |

All G58-prefixed type names become behavior-first:

| Old | New |
|---|---|
| `G58CrossFamilyDepResolvabilityValidator` | `CrossFamilyDependencyResolvabilityValidator` |
| `IG58CrossFamilyDepResolvabilityValidator` | Deleted |
| `G58CrossFamilyValidation` | `CrossFamilyDependencyValidation` |
| `G58CrossFamilyCheck` | `CrossFamilyDependencyCheck` |
| `G58CrossFamilyCheckStatus` | `CrossFamilyDependencyCheckStatus` |

Guardrail ID `"G58"` stays as `ValidationCheck.Code` metadata in PreflightReporter messages.

Remaining files in `Shared/Packaging/` (`DotNetPack*`, `PackagingError`, `ProjectMetadata*`) are P7/P10 concern.

### 3.3 Features/Preflight/ → Targets/PreFlightCheck/

| Source | Operation | Destination |
|---|---|---|
| `PreFlightCheckTask.cs` | Rewrite in place → `git mv` | `Targets/PreFlightCheck/PreFlightCheckTask.cs` |
| `PreflightReporter.cs` | `git mv` | `Targets/PreFlightCheck/Reporting/PreflightReporter.cs` |
| `HybridStaticOverlayValidator.cs` | `git mv` | `Targets/PreFlightCheck/Validation/HybridStaticOverlayValidator.cs` |
| `VersionConsistencyValidator.cs` | `git mv` | `Targets/PreFlightCheck/Validation/VersionConsistencyValidator.cs` |
| `CoreLibraryIdentityValidator.cs` | `git mv` | `Targets/PreFlightCheck/Validation/CoreLibraryIdentityValidator.cs` |
| `CsprojPackContractValidator.cs` | `git mv` | `Targets/PreFlightCheck/Validation/CsprojPackContractValidator.cs` |
| `CsprojPackContractModels.cs` | `git mv` | `Targets/PreFlightCheck/Models/CsprojPackContractModels.cs` |
| `FamilyIdentifierConventions.cs` | `git mv` | `Targets/PreFlightCheck/Validation/FamilyIdentifierConventions.cs` |
| `PreflightValidationModels.cs` | Split, `git mv` pieces | `Models/VersionConsistencyModels.cs` + `Models/CoreLibraryIdentityModels.cs` |
| `ServiceCollectionExtensions.cs` | Rewrite | `Targets/PreFlightCheck/ServiceCollectionExtensions.cs` |
| `PreflightPipeline.cs` | Delete | — |
| `PreflightRequest.cs` | Delete | — |
| `VersionConsistencyResult.cs` | Delete (OneOf) | — |
| `CoreLibraryIdentityResult.cs` | Delete (OneOf) | — |
| `CsprojPackContractResult.cs` | Delete (OneOf) | — |
| `ICsprojPackContractValidator.cs` | Delete (single impl) | — |
| `PreflightError.cs` | Delete (OneOf base) | — |

### 3.4 New files

| File | Description |
|---|---|
| `Targets/PreFlightCheck/Validation/ManifestFamilyNameInvariantValidator.cs` | Validates every `package_families[].name` matches `^sdl[0-9]+-[a-z][a-z0-9-]*$` (lowercase kebab). Returns `ValidationReport`. |

### 3.5 Empty directory cleanup

| Directory | Fate |
|---|---|
| `Features/Preflight/` | Delete — all files moved or deleted. `Features/` stays for unmigrated targets (P7/P8/P9). |
| `Shared/Versioning/` | Delete — all 4 files accounted for (2 `git mv`, 2 delete). |

`Shared/Packaging/` stays — still has `DotNetPack*`, `PackagingError`, `ProjectMetadata*` (P7/P10).

### 3.6 Files NOT touched

`Shared/Results/BuildError.cs` stays — it still has `HarvestingError` and `PackagingError` consumers (P7/P8 concern).

## 4. Target module layout

```
Targets/
  PreFlightCheck/
    PreFlightCheckTask.cs
    ServiceCollectionExtensions.cs
    Validation/
      VersionConsistencyValidator.cs
      CoreLibraryIdentityValidator.cs
      HybridStaticOverlayValidator.cs
      CsprojPackContractValidator.cs
      CrossFamilyDependencyResolvabilityValidator.cs   (import from Versioning/)
      UpstreamVersionAlignmentValidator.cs              (import from Versioning/)
      ManifestFamilyNameInvariantValidator.cs            (NEW)
      FamilyIdentifierConventions.cs
    Reporting/
      PreflightReporter.cs
    Models/
      VersionConsistencyModels.cs
      CoreLibraryIdentityModels.cs
      CsprojPackContractModels.cs
```

`CrossFamilyDependencyResolvabilityValidator` and `UpstreamVersionAlignmentValidator` live at root-level `Versioning/` but are imported by PreFlight (and by Package/P7 later). They are NOT physically inside `Targets/PreFlightCheck/`.

## 5. OneOf retirement

5 types deleted this slice (4 in PreFlight + 1 in Shared/Versioning):

| Type | Lines | Replaced by |
|---|---|---|
| `VersionConsistencyResult` | ~60 | Direct `VersionConsistencyValidation` return |
| `CoreLibraryIdentityResult` | ~60 | Direct `CoreLibraryIdentityValidation` return |
| `CsprojPackContractResult` | ~60 | Direct `CsprojPackContractValidation` return |
| `UpstreamVersionAlignmentResult` | ~60 | Direct `UpstreamVersionAlignmentValidation` return |
| `PreflightError` | ~8 | Nothing — `BuildError` stays for P7/P8 |

Domain validation types (`*Validation`, `*Check`) are preserved — they carry typed fields the reporter switches on. The `.OnError(error => ThrowPreflightFailure(...))` pattern becomes `if (validation.HasErrors) throw new CakeException(...)`.

Parking-lot update: 11 → 6 surviving OneOf types after P6.

## 6. ManifestFamilyNameInvariantValidator

```csharp
public sealed class ManifestFamilyNameInvariantValidator
{
    public ValidationReport Validate(ManifestConfig manifest)
    {
        // Pattern: ^sdl[0-9]+-[a-z][a-z0-9-]*$
        // - Must start with "sdl"
        // - Followed by one or more digits (major version)
        // - Followed by "-"
        // - Followed by lowercase letter
        // - Optionally followed by lowercase letters, digits, or hyphens
        // - No uppercase allowed anywhere
        //
        // Returns one ValidationCheck per violating family name.
        // Severity: Error. Code: "GXX" (new guardrail ID, TBD).
    }
}
```

This catches a hand-edited `manifest.json` where a `package_families[].name` like `SDL2-Core` (mixed-case) would bypass `PackageFamilyId` ordinal-exact lookups in downstream consumers.

## 7. Test migration

All 15 touched test files migrate to V2. New folder structure:

```
build/_build.Tests/
  Unit/Targets/PreFlightCheck/Validation/
    HybridStaticOverlayValidatorTests.cs
    VersionConsistencyValidatorTests.cs
    CoreLibraryIdentityValidatorTests.cs
    CsprojPackContractValidatorTests.cs
    FamilyIdentifierConventionsTests.cs
    SemanticVersionParsingTests.cs
    VersionConsistencyTests.cs
    ManifestFamilyNameInvariantValidatorTests.cs   (NEW)
  Scenarios/PreFlightCheck/
    PreFlightCheckTaskRunTests.cs   (migrated from Unit/, now full V2 scenario)
  Unit/Versioning/
    CrossFamilyDependencyResolvabilityValidatorTests.cs   (from Unit/Features/Packaging/)
    UpstreamVersionAlignmentValidatorTests.cs             (if exists)
```

Key test migrations:

- `PreFlightCheckTaskRunTests` — currently V1 `Unit/Features/Preflight/`, becomes V2 `Scenarios/PreFlightCheck/` using `FakeCakeWorldV2` + `TargetTestHostV2<PreFlightCheckTask>`
- `PreflightRequestTests` — deleted (PreflightRequest retired)
- `G58CrossFamilyDepResolvabilityValidatorTests` — V2, rename types in assertions to match new behavior-first names
- `ManifestCharacterizationTests` — update G58-related assertions

## 8. DI registration

```csharp
public static IServiceCollection AddPreFlightCheck(this IServiceCollection services)
{
    services.AddSingleton<HybridStaticOverlayValidator>();
    services.AddSingleton<CsprojPackContractValidator>();
    services.AddSingleton<CrossFamilyDependencyResolvabilityValidator>();
    services.AddSingleton<UpstreamVersionAlignmentValidator>();
    services.AddSingleton<ManifestFamilyNameInvariantValidator>();
    services.AddSingleton<PreflightReporter>();
    return services;
}
```

`PreFlightCheckTask` is NOT registered — Cake discovers it via `[TaskName]`.

`CrossFamilyDependencyResolvabilityValidator` registration moves from `Features/Packaging/ServiceCollectionExtensions` into `Versioning/ServiceCollectionExtensions` (or `AddPreFlightCheck` depending on consumer need). If Package also needs it, a shared `Versioning/ServiceCollectionExtensions.AddVersioning()` registers it once.

## 9. Consumer updates

### PackagePipeline (P7 concern, P6 impact)

`PackagePipeline` currently injects `IG58CrossFamilyDepResolvabilityValidator`. After P6:

- Interface gone, inject `CrossFamilyDependencyResolvabilityValidator` directly.
- Namespace changes: `Build.Shared.Packaging` → `Build.Versioning`.
- Method return type: `G58CrossFamilyValidation` → `CrossFamilyDependencyValidation`.

### ResolveVersionsFromExplicitTask

Injects `IUpstreamVersionAlignmentValidator` — interface gone, inject concrete `UpstreamVersionAlignmentValidator` directly.

### Upstream consumers of deleted namespaces

All `using Build.Shared.Packaging` (G58 types) and `using Build.Features.Preflight` update to new target locations.

## 10. Validation gates

```pwsh
# Build
dotnet build build/_build/Build.csproj

# Tests
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0

# PreFlight target
dotnet run --project build/_build -- --target PreFlightCheck --versions-file artifacts/resolve-versions/versions.json

# ci-sim
dotnet run --file tools.cs -- ci-sim

# Slopwatch
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

## 11. Exit criteria

- [ ] `PreFlightCheckTask` owns orchestration directly — no `PreflightPipeline`
- [ ] `PreflightRequest` deleted, task uses `PackageFamilyVersionSet`
- [ ] 4 OneOf result types + `PreflightError` deleted
- [ ] 3 unwarranted interfaces deleted (`ICsprojPackContract`, `IG58CrossFamily`, `IUpstreamVersionAlignment`)
- [ ] G58-prefixed type names replaced with behavior-first names
- [ ] `Shared/Versioning/` is empty (all files relocated to root-level `Versioning/`)
- [ ] G58 files relocated from `Shared/Packaging/` to `Versioning/`
- [ ] `ManifestFamilyNameInvariantValidator` enforced
- [ ] All 15+ test files on V2 infrastructure
- [ ] `tools.cs` + `release.yml` command contracts preserved
- [ ] `ci-sim` 8/8 PASS on Windows
- [ ] Slopwatch baseline rebuilt (`slopwatch init -f`) after all deletions

## 12. Open questions

- **Guardrail ID for `ManifestFamilyNameInvariantValidator`:** The new validator needs a G-number. `GXX` placeholder in the spec. Deniz karar verecek.
- **`CrossFamilyDependencyResolvabilityValidator` registration:** If both PreFlight and Package need it, a shared `Versioning/ServiceCollectionExtensions.AddVersioning()` registers it once. Currently `Features/Packaging/ServiceCollectionExtensions` owns it — P6 moves the registration to `AddPreFlightCheck` or `AddVersioning`. Decision during implementation based on whether `PackagePipeline` breaks without it.
