# Build Host Data Layer Refactor Plan

- **Status:** Proposed planning baseline
- **Scope:** `build\_build`, `build\_build.Tests`, and architecture docs that describe the build-host layout
- **Decision context:** Follow-up cleanup after ADR-002 target-centric migration
- **Non-goal:** No package behavior change, no new public release feature, no CI command contract change

This document is the durable plan for introducing a contract-centric `Data/` layer in the Cake build host. It exists because the ADR-002 migration successfully centralized file-backed repositories, but the resulting picture made one thing clearer: repository-owned data contracts should live with their repositories/readers instead of being scattered across root folders such as `Manifest`, `Versioning`, `Packaging`, `Harvesting`, and `Repositories`.

The goal is not to invent an enterprise "data access layer" for a build script. The goal is simpler and stricter: when the build host reads or writes a persistent contract, the contract model and the adapter that owns its storage format should be in the same place.

## 1. Why

### 1.1 What problem this solves

The current post-ADR-002 layout is much better than the old `Features/Shared/Integrations` era, but some root folders now exist mostly because they were useful during migration rather than because they are the final domain boundary.

Examples:

| Current shape | Problem |
| --- | --- |
| `build\_build\Repositories\ManifestRepository.cs` + `build\_build\Manifest\ManifestConfigModels.cs` | Repository and its entity/schema live in different root concepts. |
| `build\_build\Repositories\VersionFileRepository.cs` + `build\_build\Versioning\PackageFamilyVersionSet.cs` | `PackageFamilyVersionSet` is the `versions.json` contract, not a broad versioning service layer. |
| `build\_build\Packaging\ProjectMetadataReader.cs` | It reads MSBuild-evaluated project data; it is consumed by Package and PackageConsumerSmoke, but it is not packaging policy. |
| `build\_build\Harvesting\HarvestManifest.cs` + `build\_build\Repositories\HarvestStatusRepository.cs` | Per-RID status and consolidated harvest manifest are file contracts, while repository/write behavior is elsewhere. |
| `build\_build\Targets\Package\Models\NativePackageMetadata.cs` | `janset-native-metadata.json` is a packaged data contract, not merely a Package target model. |
| `RuntimeConfig` and `SystemArtefactsConfig` DI registrations | These are derived views of `ManifestConfig`, not independently meaningful services. |

This causes three maintainer problems:

1. **Navigation tax:** A maintainer looking for "who owns this JSON file?" has to jump through several root concepts.
2. **Layer ambiguity:** Root folders such as `Packaging` and `Versioning` can look like behavior layers even when they only contain data contracts or target-specific helpers.
3. **Future drift risk:** New readers/writers may be added next to the target that first needs them, even when the data contract is cross-target.

### 1.2 What good looks like

After the refactor, the answer to these questions should be obvious:

| Question | Expected answer |
| --- | --- |
| Where is `manifest.json` modeled and loaded? | `Data\Manifest\` |
| Where is `versions.json` modeled and loaded/saved? | `Data\Versions\` |
| Where is MSBuild-evaluated project metadata read? | `Data\ProjectMetadata\` |
| Where are `rid-status/*.json`, `harvest-manifest.json`, and `harvest-summary.json` modeled/read/written? | `Data\Harvest\` |
| Where is `janset-native-metadata.json` modeled/read/written? | `Data\NativePackageMetadata\` |
| Where does target workflow live? | `Targets\<CakeTargetName>\` |
| Where do validation rules live? | `Validation\` |
| Where do process/tool wrappers live? | `Tools\` |

The build host should become leaner because several root concepts stop pretending to be first-class behavior areas:

- `Repositories/` dissolves into `Data/<Contract>/`.
- `Manifest/` dissolves into `Data/Manifest/`.
- `Versioning/` dissolves into `Data/Versions/` plus target-local explicit input parsing.
- `Packaging/` dissolves into `Data/ProjectMetadata/`.
- `Harvesting/` dissolves into `Data/Harvest/` plus target-local Harvest models/services, if its remaining types are not cross-target data.

## 2. How

### 2.1 Definition of Data

`Data/` is the home for build-host data contracts that are persisted, packaged, or read through a stable external/tool boundary.

Data may contain:

- JSON schema models for files the build host owns or consumes.
- Repository/reader/writer interfaces for those contracts.
- Serialization/deserialization and package extraction logic.
- Small data-specific error types for expected read/write failures.
- Data registration through a single `AddData()` composition method.

Data must not contain:

- Cake target orchestration.
- Validation policy.
- Release guardrail logic.
- Package zip mutation policy.
- License union/consolidation policy.
- Diagnostic scanner behavior.
- Tar extraction or artifact deployment workflow.
- Generic "helper" code that has no storage contract.

### 2.2 The key boundary rule

Repositories should sit at the **data boundary**, not deep inside business flow.

Preferred flow:

```text
Task loads data through repository
  -> task validates target input
  -> task passes data object to services/validators
  -> services/validators perform policy/transform/checks
```

Avoid this flow:

```text
Service/validator secretly loads manifest through repository
  -> caller cannot see IO boundary
  -> tests need deeper DI setup
  -> repeated loads become easy to miss
```

This is why the plan removes derived config services first and then reduces `ManifestConfig` constructor injection. The repository is the IO adapter; the loaded manifest is just data and should be passed explicitly where practical.

### 2.3 Target layout

The intended final production layout is:

```text
build\_build\
  Data\
    ServiceCollectionExtensions.cs
    Manifest\
      ManifestConfigModels.cs
      PlatformExclusionsModels.cs
      RuntimeConfigModels.cs
      ValidationMode.cs
      VcpkgManifest.cs
      ManifestRepository.cs
      VcpkgManifestRepository.cs
    Versions\
      PackageFamilyVersionSet.cs
      VersionFileRepository.cs
    ProjectMetadata\
      EvaluatedProjectMetadata.cs
      ProjectMetadataError.cs
      ProjectMetadataReader.cs
    Harvest\
      HarvestManifest.cs
      HarvestStatusRepository.cs
      HarvestManifestRepository.cs
    NativePackageMetadata\
      NativePackageMetadata.cs
      NativePackageMetadataError.cs
      NativePackageMetadataRepository.cs

  Runtime\
    RuntimeFamily.cs
    RuntimeProfile.cs

  DependencyAnalysis\
  Results\
  Tools\
  Validation\
  Targets\
```

Namespace targets:

```text
Build.Data
Build.Data.Manifest
Build.Data.Versions
Build.Data.ProjectMetadata
Build.Data.Harvest
Build.Data.NativePackageMetadata
```

### 2.4 Interface rule

Repositories remain an explicit interface exception, like validators. The reason is still valid after moving them under `Data/`: they are file/tool-backed boundary adapters and important task collaborators.

Every repository should keep paired test coverage:

```text
<X>RepositoryUnitTests
<X>RepositoryRoundTripTests
```

Both test classes may live in one test file when that keeps the repository contract easy to review.

### 2.5 Composition rule

`Program.cs` should call:

```csharp
.AddData()
.AddValidators()
.AddDependencyAnalysis()
...
```

`AddData()` replaces `AddRepositories()`.

`AddHostBuildingBlocks()` should own host primitives such as `IPathService`, `IRuntimeProfile`, and parsed CLI state. It should not duplicate manifest file loading. Manifest loading belongs to `Data\Manifest\ManifestRepository`.

## 3. What belongs in Data

### 3.1 Definite Data candidates

| Contract | Current home | Target home | Why |
| --- | --- | --- | --- |
| `manifest.json` models | `Manifest\` | `Data\Manifest\` | Root repository data contract. |
| `vcpkg.json` model | `Manifest\VcpkgManifest.cs` | `Data\Manifest\` | Root repository data contract paired with `VcpkgManifestRepository`. |
| `ManifestRepository` | `Repositories\` | `Data\Manifest\` | Owner of `manifest.json` IO. |
| `VcpkgManifestRepository` | `Repositories\` | `Data\Manifest\` | Owner of `vcpkg.json` IO. |
| `versions.json` model | `Versioning\PackageFamilyVersionSet.cs` | `Data\Versions\` | File contract for resolved package family versions. |
| `VersionFileRepository` | `Repositories\` | `Data\Versions\` | Owner of `versions.json` IO. |
| MSBuild project metadata | `Packaging\ProjectMetadata*.cs` | `Data\ProjectMetadata\` | Tool-read project data, consumed by more than Package. The data record is named `EvaluatedProjectMetadata` to avoid a namespace/type-name conflict. |
| `rid-status/*.json` models | `Harvesting\HarvestManifest.cs` | `Data\Harvest\` | Per-RID persisted harvest status. |
| `harvest-manifest.json` models | `Harvesting\HarvestManifest.cs` | `Data\Harvest\` | Consolidated persisted harvest contract. |
| `HarvestStatusRepository` | `Repositories\` | `Data\Harvest\` | Owner of per-RID status writes and invalidation. |
| `HarvestManifestRepository` | does not exist yet | `Data\Harvest\` | Should own rid-status reads and manifest/summary temp writes. |
| `janset-native-metadata.json` model | `Targets\Package\Models\NativePackageMetadata.cs` | `Data\NativePackageMetadata\` | Packaged machine-readable metadata contract. |
| `NativePackageMetadataRepository` | does not exist yet | `Data\NativePackageMetadata\` | Should own metadata JSON writes and nupkg extraction reads. |

### 3.2 Definite non-Data candidates

| Component | Why it should not move to Data |
| --- | --- |
| `DependencyRangeNormalizer` | Mutates `.nupkg` contents and owns G56 dependency range policy. It is Package target behavior. |
| `LicenseUnionWriter` | Owns license union policy, SHA comparison, divergence handling, and staging decisions. It is ConsolidateHarvest target behavior. |
| `StagedArtifactSwapper` | Target-local staged replacement mechanic, not a data contract repository. |
| `ArtifactDeployer` | Executes deployment plans and tar creation. It is Harvest execution behavior. |
| `HarvestPayloadInspector` | Diagnostic target behavior: filesystem walk, tar extract, scanner dispatch. |
| `CsprojPackContractValidator` | Structural validation of csproj XML. It reads files, but its purpose is validation, not exposing project metadata. |
| `PackageOutputValidator` | Validates package artifacts and nuspec contents; it may use Data repositories for metadata extraction but stays in Validation. |
| Diagnostic tasks (`Otool-Analyze`, `Dumpbin-Dependents`, `Ldd-Dependents`) | Operator diagnostics, not durable data contracts. |

### 3.3 Deferred gray-area candidates

| Candidate | Decision |
| --- | --- |
| `ReadmeMappingTableGenerator` / `ReadmeMappingTableValidator` | Keep as Package/Validation for now. Add `Data\Readme\ReadmeMappingTableRepository` only if README block IO becomes shared or grows. |
| `GenerateMatrix` output models | Keep target-local for now. Add `Data\Matrix\` only if build-host code starts reading the emitted matrix or multiple targets share the contract. |
| `SmokeScopeComparator` csproj XML read call site | Keep pure comparator target-local. If the task remains too large, extract a target-local `SmokeProjectScopeReader`, not a Data repository. |

## 4. Execution plan

Each phase should be independently buildable and reviewable. Do not batch all phases into one mega-commit unless explicitly approved.

### Phase 0: Baseline and inventory

**Why:** The refactor touches folder topology and DI boundaries. A clean baseline prevents confusing existing failures with refactor regressions.

**What:**

- Record worktree state.
- Run build-host tests.
- Re-run the filesystem/JSON/metadata search used to classify Data candidates.

**How:**

```pwsh
git --no-pager status --short
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
rg -n "ToJsonAsync|ToJson<|WriteJsonAsync|DeserializeJson|ReadAllTextAsync|WriteAllText|GetFiles\(|ZipArchive|XDocument|DotNetMSBuild|FileExists|DirectoryExists|DeleteFile|DeleteDirectory|CreateDirectory" build\_build -g "*.cs"
```

**Acceptance:**

- Current baseline is known.
- Any existing failures are documented before edits.

### Phase 1: Create `Data/` and move existing repositories

**Why:** This creates the new navigation unit without changing data contracts or behavior.

**What:**

- Create `build\_build\Data\`.
- Move existing repositories into contract subfolders.
- Replace `AddRepositories()` with `AddData()`.
- Move repository tests under `Unit\Data\...`.

**How:**

Use `git mv` for repository files:

```pwsh
New-Item -ItemType Directory -Force build\_build\Data\Manifest,build\_build\Data\Versions,build\_build\Data\Harvest
git mv build\_build\Repositories\ManifestRepository.cs build\_build\Data\Manifest\ManifestRepository.cs
git mv build\_build\Repositories\VcpkgManifestRepository.cs build\_build\Data\Manifest\VcpkgManifestRepository.cs
git mv build\_build\Repositories\VersionFileRepository.cs build\_build\Data\Versions\VersionFileRepository.cs
git mv build\_build\Repositories\HarvestStatusRepository.cs build\_build\Data\Harvest\HarvestStatusRepository.cs
```

Create `build\_build\Data\ServiceCollectionExtensions.cs` with `AddData()` registering:

- `IManifestRepository`
- `IVcpkgManifestRepository`
- `IVersionFileRepository`
- `IHarvestStatusRepository`

Update `Program.cs`:

```csharp
using Build.Data;
```

and:

```csharp
.AddData()
```

instead of:

```csharp
.AddRepositories()
```

**Acceptance:**

```pwsh
rg -n "Build\.Repositories|AddRepositories" build\_build build\_build.Tests -g "*.cs"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

No production `Build.Repositories` or `AddRepositories` references remain.

### Phase 2: Move manifest, versions, and project metadata contracts

**Why:** This is the main ownership fix. Existing repositories/readers should own models at their own level.

**What:**

- Move manifest models into `Data\Manifest`.
- Move `PackageFamilyVersionSet` into `Data\Versions`.
- Move `ExplicitVersionParser` out of root `Versioning` into `Targets\ResolveVersionsFromExplicit\Services`.
- Move `ProjectMetadataReader` and `ProjectMetadataError` into `Data\ProjectMetadata`, and rename the data record to `EvaluatedProjectMetadata`.
- Delete `PackagingError` if it no longer has a real module to represent.

**How:**

Use these namespace destinations:

```text
Build.Data.Manifest
Build.Data.Versions
Build.Data.ProjectMetadata
Build.Targets.ResolveVersionsFromExplicit.Services
```

The important design split:

- `PackageFamilyVersionSet` is Data because it is the `versions.json` contract.
- `ExplicitVersionParser` is not Data because it parses operator CLI input for one target.
- `ProjectMetadataReader` is Data because it reads tool-evaluated project metadata through `dotnet msbuild -getProperty`.

**Acceptance:**

```pwsh
rg -n "Build\.Manifest|Build\.Versioning|Build\.Packaging" build\_build build\_build.Tests -g "*.cs"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Any remaining hits must be intentional and documented, otherwise removed.

### Phase 3: Complete harvest data ownership

**Why:** Harvest currently has both persisted JSON contracts and target behavior mixed under root/target folders. Data should own status/manifest IO; ConsolidateHarvest should own orchestration and license policy.

**What:**

- Move harvest JSON contract models to `Data\Harvest`.
- Add `IHarvestManifestRepository`.
- Move rid-status loading and harvest manifest/temp summary writing out of the old merger helper.
- Keep manifest aggregation task-local in `ConsolidateHarvestTask`.
- Keep license union and staged swapping target-local.

**How:**

`IHarvestManifestRepository` should expose:

```csharp
Task<IReadOnlyList<RidHarvestStatus>?> LoadRidStatusesAsync(string libraryName, CancellationToken ct = default);
Task<HarvestManifest> LoadManifestAsync(string libraryName, CancellationToken ct = default);
Task WriteManifestTempAsync(string libraryName, HarvestManifest manifest, CancellationToken ct = default);
```

Manifest aggregation should remain a private static helper on `ConsolidateHarvestTask`:

```csharp
private static HarvestManifest BuildManifest(
    string libraryName,
    IReadOnlyList<RidHarvestStatus> ridStatuses,
    ConsolidationState consolidationState)
```

`ConsolidateHarvestTask` should orchestrate:

```text
repository.LoadRidStatusesAsync
  -> licenseWriter.WriteUnionAsync
  -> BuildManifest
  -> repository.WriteManifestTempAsync
  -> staged swaps
```

**Acceptance:**

- No standalone `HarvestArtifactMerger` collaborator remains unless aggregation gains reuse or independent test pressure.
- `LicenseUnionWriter` remains target-local.
- `StagedArtifactSwapper` remains target-local.
- Repository tests cover invalid JSON, empty/missing status directories, successful status read, manifest read, and temp write parity.

### Phase 4: Add native package metadata repository

**Why:** `janset-native-metadata.json` is a data contract embedded into every `.Native` package. The generator and validator should not each own direct JSON/zip mechanics.

**What:**

- Move `NativePackageMetadata` to `Data\NativePackageMetadata`.
- Add `INativePackageMetadataRepository`.
- Keep manifest-derived metadata computation in `NativePackageMetadataGenerator`.
- Move JSON write and `.nupkg` metadata extraction into the repository.
- Keep coherence checks in `NativePackageMetadataValidator`.

**How:**

`INativePackageMetadataRepository` should expose:

```csharp
Task WriteAsync(FilePath path, NativePackageMetadata metadata, CancellationToken ct = default);

Task<Result<NativePackageMetadata, NativePackageMetadataError>> ReadFromPackageAsync(
    FilePath nativePackagePath,
    CancellationToken ct = default);
```

Repository responsibilities:

- Write metadata JSON to the harvest payload path.
- Open `.nupkg` as a zip.
- Locate root `janset-native-metadata.json`.
- Deserialize metadata.
- Return typed failures for missing file, invalid zip, missing entry, invalid JSON, or null payload.

Validator responsibilities:

- Compare metadata against manifest family/library/version/triplet/commit expectations.
- Return `ValidationCheck` failures with G55 context.
- Avoid direct zip/JSON extraction code.

**Acceptance:**

- `NativePackageMetadataValidator` no longer opens `ZipArchive`.
- `NativePackageMetadataGenerator` no longer depends on `ICakeContext` only to write JSON.
- New repository tests cover write, package read, missing entry, invalid JSON, and missing package.

### Phase 5: Remove derived manifest config DI

**Why:** `RuntimeConfig` and `SystemArtefactsConfig` are projections of `ManifestConfig`, not independent services. Keeping them in DI makes the composition root look like it owns data transformation.

**What:**

- Remove `RuntimeConfig` and `SystemArtefactsConfig` service registrations.
- Build `RuntimeProfile` directly from `ManifestConfig`.
- Load `ManifestConfig` through `IManifestRepository`.

**How:**

`IRuntimeProfile` registration should resolve:

```csharp
var manifest = sp.GetRequiredService<ManifestConfig>();
var runtimeInfo = manifest.Runtimes.Single(r => string.Equals(r.Rid, rid, StringComparison.Ordinal));
return new RuntimeProfile(runtimeInfo, manifest.SystemExclusions);
```

`ManifestConfig` registration should become:

```csharp
services.AddSingleton(provider =>
{
    var repository = provider.GetRequiredService<IManifestRepository>();
    return repository.Load();
});
```

This is an intermediate state. It keeps one cached manifest per invocation while ensuring the data adapter owns file loading.

**Acceptance:**

```pwsh
rg -n "GetRequiredService<RuntimeConfig>|GetRequiredService<SystemArtefactsConfig>|AddSingleton<RuntimeConfig>|AddSingleton<SystemArtefactsConfig>" build\_build build\_build.Tests -g "*.cs"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

No derived config DI remains.

### Phase 6: Clean ManifestConfig boundaries

**Why:** The final design should make IO boundaries explicit. `BuildContext.Manifest` and widespread constructor-injected `ManifestConfig` make manifest data look ambient.

**What:**

- Remove `ManifestConfig` from `BuildContext`.
- Convert task constructors that inject `ManifestConfig` to inject `IManifestRepository` where the task owns manifest loading.
- Pass loaded `ManifestConfig` into collaborators as method parameters where practical.
- Keep validators pure: validators should receive `ManifestConfig`, not load it themselves.

**How:**

Targets that should load manifest at task boundary:

```text
GenerateMatrixTask
HarvestTask
NativeSmokeTask
PackageTask
PackageConsumerSmokeTask
PublishStagingTask
InspectHarvestedDependenciesTask
ResolveVersionsFromManifestTask
ResolveVersionsFromExplicitTask
PreFlightCheckTask
```

Package collaborators should shift from constructor manifest injection to explicit parameters:

```text
DependencyRangeNormalizer.NormalizeAsync(..., ManifestConfig manifestConfig, ...)
NativePackageMetadataGenerator.GenerateAsync(ManifestConfig manifestConfig, ...)
ReadmeMappingTableGenerator.UpdateAsync(ManifestConfig manifestConfig, ...)
PackageFamilyPacker.PackAsync(ManifestConfig manifestConfig, ...)
```

This keeps data flow visible from the task orchestration story.

**Acceptance:**

```pwsh
rg -n "ManifestConfig manifestConfig|ManifestConfig manifest|GetRequiredService<ManifestConfig>|AddSingleton<ManifestConfig>" build\_build -g "*.cs"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

Remaining hits should be data models, local variables, method parameters, or explicitly justified compatibility points. Constructor injection and DI registration should be gone unless a slice review keeps a narrow exception.

### Phase 7: Remove leftover root concepts

**Why:** After Data owns file contracts, some root folders may contain only target-local leftovers. Keeping them would preserve the old navigation tax.

**What:**

- Delete empty `Repositories`, `Manifest`, `Versioning`, `Packaging` folders.
- Inspect `Harvesting`.
- Move `BinaryClosure` and `HarvestingError` target-local if they are only consumed by Harvest.

**How:**

Run:

```pwsh
Get-ChildItem build\_build\Harvesting -File | Select-Object -ExpandProperty Name
rg -n "Build\.Harvesting|HarvestingError|BinaryClosure|BinaryNode" build\_build build\_build.Tests -g "*.cs"
```

If only Harvest consumes them:

```pwsh
git mv build\_build\Harvesting\BinaryClosure.cs build\_build\Targets\Harvest\Models\BinaryClosure.cs
git mv build\_build\Harvesting\HarvestingError.cs build\_build\Targets\Harvest\Services\HarvestingError.cs
```

**Acceptance:**

```pwsh
rg -n "Build\.Repositories|Build\.Manifest|Build\.Versioning|Build\.Packaging|Build\.Harvesting" build\_build build\_build.Tests -g "*.cs"
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
```

No stale root namespaces remain.

### Phase 8: Documentation update

**Why:** This changes the canonical architecture after ADR-002. Future agents and humans need the new rule in durable docs, not only in chat.

**What:**

- Update `AGENTS.md`.
- Update `docs\decisions\2026-05-05-target-centric-build-host.md`.
- Update `docs\refactoring\target-centric-build-host-refactor-plan.md`.
- Update `docs\refactoring\target-centric-build-host-review-checklist.md`.
- Update `docs\README.md` and `docs\refactoring\README.md`.
- Update `docs\plan.md` / `docs\phases\README.md` only if they describe old current layout.

**How:**

Replace old "root Repositories/Manifest/Versioning/Packaging/Harvesting" language with:

```text
Data/ is the canonical home for file-backed and tool-read build contracts.
Subfolders are contract-centric: Manifest, Versions, ProjectMetadata, Harvest, NativePackageMetadata.
Data is not a Shared replacement; target orchestration, validation policy, and artifact mutation stay outside it.
```

Review checklist updates should include:

- Data contract lives beside repository/reader.
- Data does not contain target workflow.
- Repositories have paired unit/round-trip tests.
- Hidden IO inside validators/services is avoided.

**Acceptance:**

```pwsh
rg -n "Build\.Repositories|root `Repositories`|root-level `Repositories`|Build\.Manifest|Build\.Versioning|Build\.Packaging|Build\.Harvesting|Manifest/|Versioning/|Packaging/|Harvesting/" AGENTS.md docs -g "*.md"
```

Remaining references should be historical or explicitly marked retired.

### Phase 9: Full verification

**Why:** This refactor is mostly topology and DI. The only acceptable outcome is behavior parity.

**What:**

- Run build-host tests.
- Run Slopwatch.
- Run whitespace check.
- Run stale namespace searches.
- Present commit summary and proposed message for approval.

**How:**

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
git --no-pager diff --check
rg -n "Build\.Repositories|Build\.Manifest|Build\.Versioning|Build\.Packaging|Build\.Harvesting|AddRepositories|RuntimeConfig>|SystemArtefactsConfig>" build\_build build\_build.Tests -g "*.cs"
```

**Acceptance:**

- Tests pass.
- Slopwatch reports 0 issues.
- Diff check exits 0.
- No stale production namespaces remain.
- No commit is created until explicitly approved.

## 5. Testing strategy

### 5.1 Repository tests

Every Data repository should have repository-focused tests under:

```text
build\_build.Tests\Unit\Data\<Contract>\
```

Use the existing V2 fake Cake world and `Cake.Testing.FakeFileSystem` patterns. Repository tests should prefer byte-on-disk assertions for JSON contracts.

Expected examples:

```text
ManifestRepositoryTests.cs
VcpkgManifestRepositoryTests.cs
VersionFileRepositoryTests.cs
HarvestStatusRepositoryTests.cs
HarvestManifestRepositoryTests.cs
NativePackageMetadataRepositoryTests.cs
ProjectMetadataReaderTests.cs
```

### 5.2 Scenario tests

Scenario tests should remain target-centric. Do not move scenario tests under Data just because they exercise repositories indirectly.

Examples:

- `Scenarios\Harvest\...`
- `Scenarios\ConsolidateHarvest\...`
- `Scenarios\Package\...`
- `Scenarios\PackageConsumerSmoke\...`

### 5.3 Test data policy

Follow `docs\refactoring\testing-guidelines.md`:

- Use embedded fixtures under `Fixtures\Data\` for JSON/XML/zip payloads when payloads are non-trivial.
- Use centralized inline builders only for tiny, behavior-local values.
- Do not scatter raw JSON in test methods.

## 6. Review checklist for each slice

Before accepting a slice, verify:

- [ ] Behavior change is stated. Default is "no behavior change".
- [ ] Data models live beside their repository/reader.
- [ ] Data folder did not receive validation policy, target orchestration, zip rewrite policy, or diagnostic behavior.
- [ ] Target task still tells the high-level build story.
- [ ] Repository IO uses Cake-native abstractions.
- [ ] Pure transforms stay Cake-free where practical.
- [ ] Tests moved with the code they cover.
- [ ] New repositories have unit and round-trip coverage.
- [ ] No stale root namespace remains unless intentionally deferred.
- [ ] Documentation reflects any topology change in the same slice.

## 7. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| `Data/` becomes the new `Shared/` | Enforce the "persisted/tool-read contract only" rule. |
| Hidden IO moves from DI singletons into services | Load through repositories at task boundaries and pass data objects explicitly. |
| Namespace churn creates noisy review | Use small phases and verify after each phase. |
| Repository abstractions become ceremonial | Keep repository interface exception bounded to Data and file/tool boundaries. |
| Manifest cleanup becomes too large | Split derived config cleanup and manifest boundary cleanup into separate phases. |
| Tests become fragile due path moves | Move tests with production files and run focused stale namespace searches after each phase. |

## 8. Deferred follow-ups

These are intentionally not part of the first Data-layer refactor:

- `ReadmeMappingTableRepository`: add only if README mapping table IO becomes shared or grows.
- `Data\Matrix`: add only if matrix JSON becomes a read/write contract with multiple build-host consumers.
- `SmokeProjectScopeReader`: target-local extraction only if `PackageConsumerSmokeTask` remains too large after manifest cleanup.
- Diagnostic target input unification (`--dll` / `--library`): separate roadmap item.
- Public publishing implementation: Phase 2b PD-7, unrelated to this topology cleanup.
- CI log hygiene from the Phase 1 green run is captured in [`../parking-lot.md`](../parking-lot.md) under "Release CI Log Hygiene".
