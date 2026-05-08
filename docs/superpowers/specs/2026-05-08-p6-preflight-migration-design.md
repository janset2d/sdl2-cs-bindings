# S12 — P6 PreFlightCheck Target Migration Design

- **Status:** Accepted
- **Date:** 2026-05-08
- **ADR:** [ADR-002](../../decisions/2026-05-05-target-centric-build-host.md)
- **Refactor plan:** [§11 P6](../../refactoring/target-centric-build-host-refactor-plan.md)
- **Parent slice:** S11 (P5 closed — strategy retirement)
- **Lifecycle:** Temporary per-slice doc per [docs/refactoring/README.md](../../refactoring/README.md). Working-tree scratch only; never enters git history. Migrate any durable findings to canonical docs before deletion.

## 1. Goal

Migrate `Features/Preflight/` → `Targets/PreFlightCheck/`. Retire `PreflightPipeline`. Delete 5 OneOf-shaped result types (`VersionConsistencyResult`, `CoreLibraryIdentityResult`, `CsprojPackContractResult`, `UpstreamVersionAlignmentResult`, `PreflightError`). Establish a root-level `Validation/` named concept with domain alt-folders (`Manifest/`, `Versioning/`, `Packaging/`) holding all build-host validators. Add `IVcpkgManifestRepository` for repository symmetry. Add `ManifestFamilyNameInvariantValidator` (G59). **Fully retire `IReadOnlyDictionary<string, NuGetVersion>` from every task/service boundary**, including unmigrated `PackagePipeline`, `PackageConsumerSmokePipeline`, and `PublishStagingPipeline`. Migrate every touched test file to V2.

## 2. Scope

**In scope:**

- New target module `Targets/PreFlightCheck/` (task only — no Validation/Reporting subfolders, validators live in `Validation/`).
- New named concept `Validation/` at repo root (3 domain alt-folders).
- New repository `IVcpkgManifestRepository` + impl in `Repositories/`.
- Behavior-first renames (G58-prefixed → CrossFamilyDependency*).
- New validator `ManifestFamilyNameInvariantValidator` (G59).
- Universal `PackageFamilyVersionSet` boundary across PreFlight + Pack + Smoke + Publish (4 task surfaces, 3 pipelines, 1 config sink).
- All touched test files migrate to V2 (broader-than-target rule per testing-guidelines.md).
- Doc sweep + code doc sweep on touched surfaces.
- Cross-platform smoke validation (Windows + WSL + macOS) per playbook.

**Out of scope:**

- `Features/Packaging/Pack*Pipeline` internal refactor (P7 boss fight) — only boundary types touched.
- `Features/Packaging/PackageConsumerSmokePipeline` internal refactor (P9) — only boundary types touched.
- `Features/Publishing/PublishStagingPipeline` internal refactor (P9) — only boundary types touched.
- `Host/Configuration` retirement (P10) — `PackageBuildConfiguration` keeps existing as a shape but its sole field type swaps.
- `BuildContext.Manifest` retirement (P10) — stays for unmigrated consumers.
- `BuildContext.Options` aggregate / `Configurations` retirement (P10).
- `Shared/Packaging/` cleanup beyond G58 — `DotNetPack*`, `PackagingError`, `ProjectMetadata*` are P7/P10.
- `Features/Harvesting/`, `Features/Ci/`, `Features/Vcpkg/` (other phases).

## 3. Settled design decisions (brainstorming output)

| # | Decision | Rationale |
|---|---|---|
| D1 | Root-level `Validation/` concept with domain alt-folders (`Manifest/`, `Versioning/`, `Packaging/`) holds all 7 validators. | Maintenance: tek registration noktası, tek navigation root. ADR §6 "named concept" ruhu korunur — alt-folders domain anlamlıdır, catch-all bucket değil. ADR §6 hard rule "single-consumer code stays with target" için bilinçli kabul edilen istisna; istisna alt-folder isimlendirmesi ile neutralize edildi. |
| D2 | Tüm validator'lar `sealed class` + `IFoo` interface + `AddSingleton<IFoo, Foo>()` ile DI registered. Statik validator kalmıyor. | (Revised mid-execution per Deniz: original "no interface" decision flipped to "interface + concrete" for ALL 7.) Uniformity: tüm task constructor'ları interface-typed contract'a depend ediyor — DI ergonomisi + test mocking ergonomisi homogeneous. ADR §8 son iki bullet justifies it: validators ARE the seam where the rules change independently from task orchestration; they're important task collaborator contracts. Bonus: interface implementation can't be marked static, so `CA1822`/`S2325` analyzers silently satisfied — no `[SuppressMessage]` attributes anywhere in `Validation/`. `Validation/` is an explicit ADR-002 §8 exception (uniform DI shape). ADR-002 §8 carries an amendment note documenting this. |
| D3 | `IReadOnlyDictionary<string, NuGetVersion>` izi tüm repo'dan silinir. `PackageBuildConfiguration.FamilyVersionMapping` tipi `PackageFamilyVersionSet` olur. PackagePipeline + Smoke + Publish boundary'leri tipli'ye geçer. | ADR §6 / review checklist §6 hard rule ("Version APIs avoid raw IReadOnlyDictionary boundaries"). "P7'de yapılır" punt'ı yarı-temiz repo bırakıyordu; P5'in "P6'da yapılır" geçiş anlaşması gibi sürünüyordu. P6'da hard rule fully enforced. |
| D4 | `IVcpkgManifestRepository` eklenir (`ManifestRepository` paralel). | ADR §6 simetri: file-backed source-of-truth state repository'lerin altında. PreFlightCheckTask path plumbing yapmak zorunda kalmaz. `EnsureInputsReady` defensive double-check ortadan kalkar — repository'ler kendi exception'larını atar. |
| D5 | PreFlightCheckTask body'sinde path metadata yok. Reporter mesajları "build/manifest.json" / "vcpkg.json" hardcode kullanır (operatör zaten bu isimleri biliyor). | Validator'lar `Validate(ManifestConfig, VcpkgManifest)` veya `Validate(ManifestConfig, PackageFamilyVersionSet)` saf data signature'ı alır. `VersionConsistencyValidation.ManifestPath/VcpkgManifestPath` property'leri silinir. Path private wrapper'lar (`GetManifestPath`, `GetVcpkgManifestPath`) yok. |
| D6 | Private method audit: bu slice'ta dokunduğumuz tüm class'lar (whole-repo audit DEĞİL). Passthrough wrapper'lar inline edilir; narrative helper / algoritma kategorisindekiler kalır. | Slice scope kontrolü. Whole-repo audit P10 cleanup'a bırakılır. |
| D7 | Documentation sweep + code documentation sweep dahil. | Plan/ADR/parking-lot/release-guardrails/AGENTS.md durum tutarlılığı; XML doc / file-header / inline comment'lar yeni topology'ye göre güncel. ADR §10 code/document boundary kuralı: stale phase-number / migration-timing comment'lar silinir. |
| D8 | Session sonu cross-platform validation (Windows + WSL + macOS) + GitHub Actions release pipeline manuel trigger. | Manifest schema (`PackageBuildConfiguration` type) + DI composition + Pack/Smoke/Publish boundary'lerine dokunuyor — playbook §"When to run cross-platform" listesinin 3/4 kriterine giriyor. |

## 4. Architecture: Validation/ root concept

### 4.1 Folder layout

```text
build/_build/Validation/
  Manifest/
    VersionConsistencyValidator.cs           // sealed class — was static
    CoreLibraryIdentityValidator.cs          // sealed class — was static
    ManifestFamilyNameInvariantValidator.cs  // YENİ (G59) — sealed class
    CsprojPackContractValidator.cs           // sealed class — was instance with interface
  Versioning/
    UpstreamVersionAlignmentValidator.cs     // sealed class — was instance with interface
    CrossFamilyDependencyResolvabilityValidator.cs  // sealed class — was instance with interface
  Packaging/
    HybridStaticOverlayValidator.cs          // sealed class — already instance no interface
  Models/                                     // Shared validation report shapes that don't fit a single domain
    VersionConsistencyValidation.cs          // moved out of PreflightValidationModels.cs
    LibraryVersionCheck.cs                   // (+enum)
    CoreLibraryIdentityValidation.cs         // moved out of PreflightValidationModels.cs
    CoreLibraryIdentityCheck.cs              // (+enum)
    UpstreamVersionAlignmentValidation.cs    // moved from Shared/Versioning/
    UpstreamVersionAlignmentCheck.cs         // (+enum)
    CrossFamilyDependencyValidation.cs       // renamed from G58CrossFamilyValidation
    CrossFamilyDependencyCheck.cs            // (+enum) renamed from G58CrossFamilyCheck
    CsprojPackContractValidation.cs          // moved from Features/Preflight/CsprojPackContractModels.cs
    CsprojPackContractCheck.cs               // (+enum)
  Conventions/
    FamilyIdentifierConventions.cs           // moved from Features/Preflight/
  ServiceCollectionExtensions.cs              // tek AddValidators() — 7 validator'ı register
```

**Note on `Models/` and `Conventions/`:** These are the domain shapes the validators emit and the naming utility used by `CsprojPackContractValidator` + `PackageFamilyId` consumers. Single owner is "Validation domain"; placing them under `Validation/Models/` and `Validation/Conventions/` is consistent with ADR §6 named concept. Alternative is to fold them per-validator-domain (e.g., `Validation/Manifest/Models/...`) but that fragments small types and makes cross-validator reuse (e.g., `FamilyIdentifierConventions` consumed by both Csproj validator and `PackageFamilyId` lookups) awkward. Single Models folder is the simpler shape.

### 4.2 Validator shape rule (D2)

All 7 validators:

- `sealed class` (no abstract base, no inheritance).
- Each implements `IFoo` interface; existing interfaces (`IUpstreamVersionAlignmentValidator`, `ICsprojPackContractValidator`) kept and retargeted to typed boundaries; new interfaces added where missing (`IVersionConsistencyValidator`, `ICoreLibraryIdentityValidator`, `IManifestFamilyNameInvariantValidator`, `ICrossFamilyDependencyResolvabilityValidator`, `IHybridStaticOverlayValidator`); G58-prefixed interface renamed to `ICrossFamilyDependencyResolvabilityValidator`.
- Constructor injection for any DI dependency (`ICakeContext`, `IPathService`, `IFileSystem`).
- Pure validators (no DI deps) still get a parameterless constructor.
- Registered as `Singleton` via `AddSingleton<IFoo, Foo>()`.
- Single public `Validate(...)` method or overload set.

(Original spec wording said "no interface". Revised mid-execution per Deniz to "interface + concrete" — see §3 D2 amended row and §9 surviving cleanup. ADR-002 §8 amendment note documents `Validation/` as an explicit exception to the ceremonial-IFoo discouragement, justified by uniform DI shape + analyzer side-effects.)

Per-validator shape:

| Validator | DI deps | Validate signature | Returns |
| --- | --- | --- | --- |
| `VersionConsistencyValidator` | none | `Validate(ManifestConfig, VcpkgManifest)` | `VersionConsistencyValidation` |
| `CoreLibraryIdentityValidator` | none | `Validate(ManifestConfig)` | `CoreLibraryIdentityValidation` |
| `ManifestFamilyNameInvariantValidator` | none | `Validate(ManifestConfig)` | `ValidationReport` |
| `CsprojPackContractValidator` | `IFileSystem` | `Validate(ManifestConfig, DirectoryPath repoRoot)` | `CsprojPackContractValidation` |
| `UpstreamVersionAlignmentValidator` | none | `Validate(ManifestConfig, PackageFamilyVersionSet)` | `UpstreamVersionAlignmentValidation` |
| `CrossFamilyDependencyResolvabilityValidator` | none | `Validate(PackageFamilyVersionSet, ManifestConfig)` | `CrossFamilyDependencyValidation` |
| `HybridStaticOverlayValidator` | `ICakeContext`, `IPathService` | `Validate(IImmutableList<RuntimeInfo>)` | `ValidationReport` |

**Notes:**

- `VersionConsistencyValidator.Validate` no longer takes `manifestPath` / `vcpkgManifestPath` — those parameters are removed (D5). Reporter messages reference well-known names directly.
- Domain-specific `*Validation` shapes (`VersionConsistencyValidation`, etc.) are preserved because the reporter switches on typed fields. Only `ValidationReport`/`ValidationCheck` shapes are used for `ManifestFamilyNameInvariant` (new — no domain logic to switch on) and `HybridStaticOverlay` (already canonical).

### 4.3 Single registration point

```csharp
// build/_build/Validation/ServiceCollectionExtensions.cs
namespace Build.Validation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IManifestFamilyNameInvariantValidator, ManifestFamilyNameInvariantValidator>();
        services.AddSingleton<IVersionConsistencyValidator, VersionConsistencyValidator>();
        services.AddSingleton<ICoreLibraryIdentityValidator, CoreLibraryIdentityValidator>();
        services.AddSingleton<ICsprojPackContractValidator, CsprojPackContractValidator>();
        services.AddSingleton<ICrossFamilyDependencyResolvabilityValidator, CrossFamilyDependencyResolvabilityValidator>();
        services.AddSingleton<IUpstreamVersionAlignmentValidator, UpstreamVersionAlignmentValidator>();
        services.AddSingleton<IHybridStaticOverlayValidator, HybridStaticOverlayValidator>();

        return services;
    }
}
```

`Program.cs` composition root calls `services.AddValidators()` once. Both `AddPreFlightCheck()` (which only registers `PreflightReporter`) and unmigrated `AddPackagingFeature()` rely on `AddValidators()` having been called — duplicate registrations are eliminated.

## 5. Architecture: PreFlightCheck target module

### 5.1 Folder layout

```text
build/_build/Targets/PreFlightCheck/
  PreFlightCheckTask.cs
  ServiceCollectionExtensions.cs
  Reporting/
    PreflightReporter.cs                      // moved from Features/Preflight/
```

No `Validation/` subfolder under `Targets/PreFlightCheck/` — validators live at `Validation/` root (D1). No `Models/` subfolder — validation models live at `Validation/Models/`. No `Requests/` subfolder — task reads `BuildContext.VersionsFilePath` directly; no `<Target>Request` ceremony for this slice (refactor plan §5.2: "Trivial/no-op/default/fully-inline targets are exempt from request ceremony"; PreFlight's input is just the versions-file path).

### 5.2 PreFlightCheckTask body

```csharp
using Build.Host;
using Build.Repositories;
using Build.Results;
using Build.Targets.PreFlightCheck.Reporting;
using Build.Validation.Manifest;
using Build.Validation.Packaging;
using Build.Validation.Versioning;
using Cake.Core;
using Cake.Frosting;

namespace Build.Targets.PreFlightCheck;

[TaskName("PreFlightCheck")]
[TaskDescription("Validates manifest+vcpkg consistency, hybrid-static overlay coherence, core identity, family name invariants, csproj pack contract, upstream version alignment, and cross-family dependency resolvability before any build operation.")]
public sealed class PreFlightCheckTask(
    IManifestRepository manifestRepository,
    IVcpkgManifestRepository vcpkgManifestRepository,
    IVersionFileRepository versionFileRepository,
    VersionConsistencyValidator versionConsistencyValidator,
    HybridStaticOverlayValidator hybridStaticOverlayValidator,
    CoreLibraryIdentityValidator coreLibraryIdentityValidator,
    ManifestFamilyNameInvariantValidator manifestFamilyNameInvariantValidator,
    CsprojPackContractValidator csprojPackContractValidator,
    UpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    CrossFamilyDependencyResolvabilityValidator crossFamilyDependencyResolvabilityValidator,
    PreflightReporter reporter)
    : AsyncFrostingTask<BuildContext>
{
    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.VersionsFilePath is null)
        {
            throw new CakeException(
                "PreFlightCheck requires --versions-file <path>. " +
                "Run --target ResolveVersionsFromManifest first to produce a versions.json, " +
                "then re-run with --versions-file artifacts/resolve-versions/versions.json.");
        }

        var manifest = _manifestRepository.Load();
        var vcpkgManifest = _vcpkgManifestRepository.Load();
        var versions = _versionFileRepository.Load(context.VersionsFilePath);

        if (versions.Count == 0)
        {
            throw new CakeException(
                "PreFlightCheck requires a non-empty version mapping. " +
                "Re-run --target ResolveVersionsFromManifest or --target ResolveVersionsFromExplicit.");
        }

        _reporter.ReportRunStart();

        var versionConsistency       = _versionConsistencyValidator.Validate(manifest, vcpkgManifest);
        var hybridStaticOverlay      = _hybridStaticOverlayValidator.Validate(manifest.Runtimes);
        var coreLibraryIdentity      = _coreLibraryIdentityValidator.Validate(manifest);
        var manifestFamilyName       = _manifestFamilyNameInvariantValidator.Validate(manifest);
        var csprojPackContract       = _csprojPackContractValidator.Validate(manifest, context.Paths.RepoRoot);
        var upstreamVersionAlignment = _upstreamVersionAlignmentValidator.Validate(manifest, versions);
        var crossFamilyDependency    = _crossFamilyDependencyResolvabilityValidator.Validate(versions, manifest);

        _reporter.ReportVersionConsistency(versionConsistency);
        _reporter.ReportHybridStaticOverlay(hybridStaticOverlay);
        _reporter.ReportCoreLibraryIdentity(coreLibraryIdentity);
        _reporter.ReportManifestFamilyNameInvariant(manifestFamilyName);
        _reporter.ReportCsprojPackContract(csprojPackContract);
        _reporter.ReportUpstreamVersionAlignment(upstreamVersionAlignment);
        _reporter.ReportCrossFamilyDependencyResolvability(crossFamilyDependency);

        var fatal =
            versionConsistency.HasErrors
            || !hybridStaticOverlay.IsValid
            || coreLibraryIdentity.HasErrors
            || !manifestFamilyName.IsValid
            || csprojPackContract.HasErrors
            || upstreamVersionAlignment.HasErrors
            || crossFamilyDependency.HasErrors;

        if (fatal)
        {
            throw new CakeException(
                "Pre-flight check failed. Review the errors above and fix manifest.json / vcpkg.json / csproj files.");
        }

        return Task.CompletedTask;
    }
}
```

**No private methods.** `EnsureInputsReady` removed (repositories throw on missing files with friendly messages). `GetManifestPath` / `GetVcpkgManifestPath` removed (repositories know their paths via DI). Path propagation through validator parameters removed.

### 5.3 ServiceCollectionExtensions

```csharp
namespace Build.Targets.PreFlightCheck;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPreFlightCheck(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<PreflightReporter>();
        return services;
    }
}
```

Validators are registered by `AddValidators()` at composition root; this extension only registers PreFlight-specific collaborators (currently just the reporter).

### 5.4 PreflightReporter changes

- Add `ReportManifestFamilyNameInvariant(ValidationReport report)` (G59 messaging).
- Rename `ReportG58CrossFamilyResolvability` → `ReportCrossFamilyDependencyResolvability`. Parameter type `G58CrossFamilyValidation` → `CrossFamilyDependencyValidation`.
- `ReportRunStart` scope-line message updated to mention all 7 validators.
- `ReportVersionConsistency` no longer logs `ManifestPath` / `VcpkgManifestPath` (those fields removed from `VersionConsistencyValidation`); hardcode "build/manifest.json" / "vcpkg.json".
- All G-number references in error messages stay as bracketed metadata (`"[G54]"`, `"[G58]"`, `"[G16]"`) — behavior-first code names with guardrail IDs as report metadata per ADR §10.
- All log methods use `_cakeContext.Log` (existing pattern).

## 6. Architecture: VcpkgManifestRepository

### 6.1 Interface + impl

```csharp
// build/_build/Repositories/IVcpkgManifestRepository.cs
namespace Build.Repositories;

public interface IVcpkgManifestRepository
{
    VcpkgManifest Load();
}
```

```csharp
// build/_build/Repositories/VcpkgManifestRepository.cs
using Build.Integrations.Vcpkg;     // IVcpkgManifestReader stays in current location for P6
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Repositories;

public sealed class VcpkgManifestRepository : IVcpkgManifestRepository
{
    private readonly ICakeContext _context;
    private readonly IVcpkgManifestReader _reader;
    private readonly FilePath _vcpkgManifestPath;

    public VcpkgManifestRepository(ICakeContext context, IVcpkgManifestReader reader, FilePath vcpkgManifestPath)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _vcpkgManifestPath = vcpkgManifestPath ?? throw new ArgumentNullException(nameof(vcpkgManifestPath));
    }

    public VcpkgManifest Load()
    {
        if (!_context.FileExists(_vcpkgManifestPath))
        {
            throw new CakeException(
                $"VcpkgManifestRepository cannot load vcpkg manifest: file does not exist at '{_vcpkgManifestPath.FullPath}'. " +
                "Run from the repository root or pass --repo-root to point Cake at a valid checkout.");
        }

        return _reader.ParseFile(_vcpkgManifestPath);
    }
}
```

### 6.2 DI registration in `Repositories/ServiceCollectionExtensions.cs`

Existing `AddRepositories()` extension grows by one line:

```csharp
services.AddSingleton<IVcpkgManifestRepository>(sp => new VcpkgManifestRepository(
    sp.GetRequiredService<ICakeContext>(),
    sp.GetRequiredService<IVcpkgManifestReader>(),
    sp.GetRequiredService<IPathService>().GetVcpkgManifestFile()));
```

Mirrors the existing `IManifestRepository` factory shape.

`IVcpkgManifestReader` itself keeps current registration (`AddVcpkgFeature` or wherever it lives) — repository wraps the reader, doesn't replace it. Future P7/P8 may decide whether the reader retires entirely.

## 7. Architecture: Dictionary retirement (D3)

### 7.1 The dictionary footprint

Current `IReadOnlyDictionary<string, NuGetVersion>` boundary points:

```text
Host/Configuration/PackageBuildConfiguration.cs
  → FamilyVersionMapping: IReadOnlyDictionary<string, NuGetVersion>

Program.cs (composition root factory)
  → reads versions.json into dict, supplies PackageBuildConfiguration

Consumers (all via PackageBuildConfiguration.FamilyVersionMapping):
  - Features/Preflight/PreFlightCheckTask.cs       (deleted in this slice; new task uses IVersionFileRepository)
  - Features/Packaging/PackageTask.cs              (boundary update)
  - Features/Packaging/PackagePipeline.cs          (boundary update)
  - Features/Packaging/PackageConsumerSmokeTask.cs (boundary update)
  - Features/Publishing/PublishStagingTask.cs      (boundary update)

Validator boundaries (current dict-shaped):
  - IUpstreamVersionAlignmentValidator (dict + typed overload)
  - IG58CrossFamilyDepResolvabilityValidator (dict only)
```

### 7.2 The endpoint shape

After P6, **zero `IReadOnlyDictionary<string, NuGetVersion>` references in `build/_build/`** (excluding inside `PackageFamilyVersionSetJsonConverter` if its internal implementation uses dict — but it doesn't currently).

`PackageBuildConfiguration`:

```csharp
public sealed class PackageBuildConfiguration(PackageFamilyVersionSet familyVersions)
{
    public PackageFamilyVersionSet FamilyVersions { get; } =
        familyVersions ?? throw new ArgumentNullException(nameof(familyVersions));
}
```

Note: property rename `FamilyVersionMapping` → `FamilyVersions` (`Mapping` was dict-implying).

`Program.cs` composition factory:

```csharp
// Old: builds a Dictionary<string, NuGetVersion> from versions.json
// New: builds a PackageFamilyVersionSet from versions.json
//      (or from explicit-version CLI inputs, same as today)
```

### 7.3 Validator boundary changes

```csharp
// UpstreamVersionAlignmentValidator
public UpstreamVersionAlignmentValidation Validate(ManifestConfig manifest, PackageFamilyVersionSet versions);
// (dict overload deleted)

// CrossFamilyDependencyResolvabilityValidator
public CrossFamilyDependencyValidation Validate(PackageFamilyVersionSet versions, ManifestConfig manifest);
// (was: IReadOnlyDictionary<string, NuGetVersion> mapping)
```

Internal lookups inside `CrossFamilyDependencyResolvabilityValidator`:

```csharp
// Old: foreach (var (dependentFamilyName, dependentVersion) in mapping)
// New: foreach (var entry in versions)  // PackageFamilyVersion struct
//        var dependentFamilyName = entry.Family.Value;
//        var dependentVersion = entry.Version;

// Old: if (mapping.ContainsKey(dependencyFamilyName))
// New: if (versions.Contains(new PackageFamilyId(dependencyFamilyName)))
```

### 7.4 PackagePipeline boundary changes (P6 surgical, NOT migration)

Only the type surface changes; internal orchestration stays untouched until P7.

```csharp
// PackRequest
public sealed record PackRequest(PackageFamilyVersionSet Versions);

// PackagePipeline.RunAsync
var explicitVersions = request.Versions; // PackageFamilyVersionSet
if (explicitVersions.Count == 0) { /* throw */ }

// G58 call: typed signature
var crossFamilyValidation = _crossFamilyDependencyResolvabilityValidator.Validate(explicitVersions, _manifestConfig);

// ResolveSelectedFamilies signature
private IReadOnlyList<PackageFamilyConfig> ResolveSelectedFamilies(PackageFamilyVersionSet explicitVersions)
{
    var selectedFamilies = new List<PackageFamilyConfig>(explicitVersions.Count);
    foreach (var entry in explicitVersions)
    {
        var requestedFamily = entry.Family.Value;
        var family = _manifestConfig.PackageFamilies.SingleOrDefault(...);
        // ...
    }
}

// PackFamilyAsync version lookup
var familyVersion = explicitVersions.RequireVersion(new PackageFamilyId(family.Name)).ToNormalizedString();
```

Same `PackageFamilyVersionSet` boundary swap on `PackageConsumerSmokePipeline` and `PublishStagingPipeline`. Internal orchestration of these pipelines is **NOT** touched — that's their respective phase work (P7/P9).

### 7.5 Documentation update

`Versioning/PackageFamilyVersionSet.cs` XML doc currently says: *"Replaces raw `IReadOnlyDictionary<string, NuGetVersion>` at task/service boundaries."* Update to: *"Typed family→version mapping. Used at every task/service boundary in the build host."* (No "replaces" framing — the old shape is fully retired, no comparative reference needed.)

## 8. ManifestFamilyNameInvariantValidator (G59)

### 8.1 Specification

- **Pattern:** `^sdl[0-9]+-[a-z][a-z0-9-]*$`
- **Subjects:** every `ManifestConfig.PackageFamilies[].Name`.
- **Returns:** `ValidationReport`. One `ValidationCheck` per violating family. Severity: `Error`. Code: `"G59"`.
- **Empty/null name handling:** Empty/whitespace names produce a separate violation message ("must not be empty") rather than "does not match pattern". Plan implementation prefers `ArgumentException.ThrowIfNullOrWhiteSpace` upstream, but defensive check at validator level catches manifest authoring mistakes.

### 8.2 Sealed class shape

```csharp
namespace Build.Validation.Manifest;

public sealed partial class ManifestFamilyNameInvariantValidator
{
    [GeneratedRegex(@"^sdl[0-9]+-[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FamilyNamePattern();

    public ValidationReport Validate(ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var checks = new List<ValidationCheck>();
        foreach (var family in manifest.PackageFamilies)
        {
            if (string.IsNullOrWhiteSpace(family.Name))
            {
                checks.Add(new ValidationCheck(
                    Name: "Manifest family name invariant",
                    Severity: ValidationSeverity.Error,
                    Message: "Manifest package_families[] entry has an empty name. Every family must declare a 'sdl<major>-<role>' kebab-case identifier.",
                    Code: "G59"));
                continue;
            }

            if (!FamilyNamePattern().IsMatch(family.Name))
            {
                checks.Add(new ValidationCheck(
                    Name: "Manifest family name invariant",
                    Severity: ValidationSeverity.Error,
                    Message: $"Family name '{family.Name}' does not match required pattern 'sdl<major>-<role>' (lowercase kebab, e.g. 'sdl2-core', 'sdl2-image'). Mixed-case or underscore-delimited names break PackageFamilyId ordinal-exact lookups in downstream consumers.",
                    Code: "G59"));
            }
        }

        return checks.Count == 0 ? ValidationReport.Empty : new ValidationReport(checks);
    }
}
```

`partial` is required for `[GeneratedRegex]`. `sealed partial class` with `[GeneratedRegex]` is the supported pattern in .NET 7+ source generators. `private static partial Regex` is generated; instance method calls it.

### 8.3 Tests

`build/_build.Tests/Unit/Validation/Manifest/ManifestFamilyNameInvariantValidatorTests.cs`:

- `Validate_Should_Return_Empty_Report_When_All_Names_Match`
- `Validate_Should_Return_Error_When_Name_Has_Uppercase`
- `Validate_Should_Return_Error_When_Name_Is_Underscore_Delimited`
- `Validate_Should_Return_Error_When_Name_Is_Empty`
- `Validate_Should_Return_Error_When_Major_Is_Not_Digits`
- `Validate_Should_Return_Multiple_Errors_For_Multiple_Violations`

## 9. OneOf cleanup (5 types deleted)

| Type | Replaced by | Notes |
|---|---|---|
| `Features/Preflight/VersionConsistencyResult.cs` | Direct `VersionConsistencyValidation` return | Validation type stays, moves to `Validation/Models/`. |
| `Features/Preflight/CoreLibraryIdentityResult.cs` | Direct `CoreLibraryIdentityValidation` return | Same. |
| `Features/Preflight/CsprojPackContractResult.cs` | Direct `CsprojPackContractValidation` return | Validation type moves from `CsprojPackContractModels.cs` to `Validation/Models/`. |
| `Shared/Versioning/UpstreamVersionAlignmentResult.cs` | Direct `UpstreamVersionAlignmentValidation` return | Validation type moves to `Validation/Models/`. |
| `Features/Preflight/PreflightError.cs` | Nothing | `BuildError` base survives for Harvesting/Packaging errors. |

Parking-lot count: 11 → 6 surviving OneOf types after P6.

The `.OnError(error => ThrowPreflightFailure(...))` consumption pattern in `PreflightPipeline` and `ResolveVersionsFromExplicitTask` becomes `if (validation.HasErrors) throw new CakeException(...)` (single point of decision per validator).

## 10. Test policy (broader-than-target V2 rule)

### 10.1 Touched test files migrate to V2

Per testing-guidelines.md altın kuralı: "Touching a test? Migrate it to V2."

Production-side touched tests:

- `Unit/Features/Preflight/*` (8 files) → `Unit/Validation/Manifest/*` + `Unit/Validation/Versioning/*` + `Unit/Validation/Packaging/*` + `Unit/Validation/Conventions/*`
- `Unit/Features/Packaging/G58CrossFamilyDepResolvabilityValidatorTests.cs` → `Unit/Validation/Versioning/CrossFamilyDependencyResolvabilityValidatorTests.cs`
- `Unit/Shared/Versioning/UpstreamVersionAlignmentValidatorTests.cs` → `Unit/Validation/Versioning/UpstreamVersionAlignmentValidatorTests.cs`
- `Unit/Features/Preflight/PreFlightCheckTaskRunTests.cs` → DELETE; replaced by V2 `Scenarios/PreFlightCheck/PreFlightCheckTaskScenarioTests.cs`
- `Unit/Features/Preflight/PreflightRequestTests.cs` → DELETE (PreflightRequest retired)
- `Unit/Features/Packaging/PackagePipelineTests.cs` — touched (boundary type swap, V2 migration if not already)
- `Unit/Features/Packaging/PackageConsumerSmokePipelineTests.cs` — touched
- `Unit/Features/Publishing/PublishStagingPipelineTests.cs` — touched
- `Unit/Features/Packaging/PackageOutputValidatorTests.cs` — likely touched if it constructs versions; check during implementation
- `Characterization/ConfigContract/ManifestDeserializationTests.cs` — touched (G58 type names appear in assertions)
- `Unit/CompositionRoot/ProgramCompositionRootTests.cs` — touched (DI shape change for `AddValidators()`)
- `Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` — touched
- `Scenarios/ResolveVersionsFromExplicit/ResolveVersionsFromExplicitTaskScenarios.cs` — touched (`UpstreamVersionAlignmentValidator` interface drop)

New tests:

- `Unit/Validation/Manifest/ManifestFamilyNameInvariantValidatorTests.cs`
- `Unit/Repositories/VcpkgManifestRepositoryTests.cs`
- `Scenarios/PreFlightCheck/PreFlightCheckTaskScenarioTests.cs` (boss-fight scenario establishes V2 pattern for cross-cutting validation tasks)

### 10.2 Scenario test pattern for PreFlightCheckTask

```csharp
[Test]
public async Task RunAsync_Should_Pass_When_All_Validators_Green()
{
    var world = FakeCakeWorldV2.CreateWindows()
        .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
        .WithTextFile("vcpkg-overlay-triplets/x64-windows-hybrid.cmake", "# overlay")
        .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"))
        .WithVersionsFile("artifacts/resolve-versions/versions.json")
        .WithTextFile("artifacts/resolve-versions/versions.json",
            FixtureLoader.Load("Versions/versions-valid.json"));

    var result = await CreateHost(world).RunAsync();

    await Assert.That(result.Success).IsTrue();
}

private static TargetTestHostV2<PreFlightCheckTask> CreateHost(FakeCakeWorldV2 world)
{
    return new TargetTestHostV2<PreFlightCheckTask>(world)
        .WithServices(services =>
        {
            services.AddRepositories();
            services.AddValidators();
            services.AddPreFlightCheck();
            // IVcpkgManifestReader registration via AddVcpkgFeature() or fake stub
        });
}
```

Failure-path scenarios cover: missing versions-file, empty version mapping, manifest family-name invariant violation, hybrid-static overlay missing, version-consistency mismatch, csproj path mismatch, cross-family scope hole.

New `Fixtures/Data/Vcpkg/` folder: `vcpkg-valid.json`, `vcpkg-version-mismatch.json`, `vcpkg-port-version-mismatch.json` embedded fixtures.

## 11. Private method audit (D6)

Slice scope only — classes touched in this slice. Whole-repo audit deferred to P10.

Audit candidates (per extraction-guidelines.md decision tree):

| Class | Private methods | Decision |
|---|---|---|
| `PreFlightCheckTask` (new) | (none — by design) | N/A |
| `VersionConsistencyValidator` | `CreateVcpkgOverrideLookup`, `ValidateLibrary`, `ParseSemanticVersion`, `TryParseSemanticVersion` | `ParseSemanticVersion`+`TryParseSemanticVersion` is a wrapper soup; merge into single `TryParseSemanticVersion` (no exception path). `ValidateLibrary` is algorithm — keep. `CreateVcpkgOverrideLookup` is a 4-line helper — keep. |
| `CoreLibraryIdentityValidator` | (mostly inline already) | Audit during instance refactor; expect no extraction needed. |
| `CsprojPackContractValidator` | `ValidateManagedCsproj`, `ValidateNativeCsproj`, `LoadCsprojDocument`, `CheckManagedPackageId`, `CheckNativeProjectReferencePath`, `ValidateDependsOn`, `ValidateLibraryRef`, `ReadPropertyValue`, `PathsEqual` | `ReadPropertyValue` is XML LINQ — keep (used twice). `PathsEqual` is a normalization utility — keep but consider moving to `Validation/Conventions/PathComparison.cs` if it gets a second consumer; otherwise inline if used once after audit. `Validate*` and `Check*` methods are narrative helpers — keep. `LoadCsprojDocument` carries XML parsing + error path — keep. |
| `UpstreamVersionAlignmentValidator` | `ValidateUniqueManifestKeys`, `ValidateEntry` | Algorithm, narrative — keep. |
| `CrossFamilyDependencyResolvabilityValidator` | `EvaluateDependency` | Algorithm — keep. |
| `HybridStaticOverlayValidator` | (none) | N/A |
| `ManifestFamilyNameInvariantValidator` (new) | (none — pattern + loop) | N/A |
| `PreflightReporter` | (per-validator switch helpers) | Narrative — keep. |
| `PackagePipeline` | (boundary type swap only this slice) | NO audit this slice — internal refactor is P7 boss fight. |

Audit happens during the migration step for each class, not as a standalone task.

## 12. Documentation sweep (D7)

### 12.1 Canonical docs to update

| Doc | Update |
|---|---|
| `docs/refactoring/target-centric-build-host-refactor-plan.md` | §3 hotspots row: `Features/Preflight/PreflightPipeline.cs` retired. §11 P6 status block: closed (date, slice ID, OneOf count delta, Validation/ concept established, IVcpkgManifestRepository added, dict retired). §6 named concepts: add `Validation` row. |
| `docs/refactoring/target-centric-build-host-review-checklist.md` | §6 add Validation/ as elevated named concept. §11 retired abstractions: PreflightPipeline → confirmed gone. §13 V2 — confirm new tests on V2. |
| `docs/parking-lot.md` | OneOf count 11 → 6. Surviving 6 listed: `PackageInfoResult`, `DotNetPackResult`, `ProjectMetadataResult`, `ArtifactPlannerResult`, `ClosureResult`, `CopierResult`, `PackageValidationResult` (count: actually 7 — re-verify during implementation). |
| `docs/knowledge-base/release-guardrails.md` | G59 row added (Manifest Family Name Invariant). G16/G49/G54/G58 owner column updates: `Validation/...` paths. |
| `AGENTS.md` | "Settled Strategic Decisions" — optional new row "Validation as named concept" if we decide to make this an explicit settled decision (or leave as plan §6 amendment). "Configuration File Relationships" — `IReadOnlyDictionary<string, NuGetVersion>` no longer mentioned. |
| `docs/decisions/2026-05-05-target-centric-build-host.md` | §6 amendment note: `Validation` is an elevated named concept; alt-folders (`Manifest`/`Versioning`/`Packaging`) provide domain organization while keeping single registration. Or footnote referencing this slice. |
| `docs/plan.md` | Phase X status: P6 closed. Bumped test count. Active phase = P7. |
| `docs/playbook/local-development.md` | If any command surface changed (none expected) update; otherwise no change. |
| `docs/playbook/cross-platform-smoke-validation.md` | Confirm session ran successfully against this playbook; no doc change unless hostnames / paths changed. |

### 12.2 Slopwatch baseline

`slopwatch init -f --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"` after slice ships. Drops stale entries pointing at deleted `Features/Preflight/`, `Shared/Versioning/`, `Shared/Packaging/G58*` paths.

## 13. Code documentation sweep (D7)

### 13.1 ADR §10 boundary

Logic-bearing comments must explain the local logic directly; no internal-doc-link substitutes. Stale phase numbers, ADR references, and migration timing comments belong in canonical docs, not in `.cs`/`.csproj` comments.

### 13.2 Sweep targets

Touched files only — `.cs`, `.csproj`, `.props`, `.targets`, workflow YAML, local orchestration scripts.

| Sweep type | Action |
| --- | --- |
| XML doc comments | Update `<see cref="..."/>` to new namespaces and class names. Drop "S11/S12/P6" references in production XML doc unless they're truly load-bearing. |
| File-header copyright comments | Keep as-is (license-required SPDX headers stay). |
| Inline migration-timing comments | Delete unless they explain a current behavior. Examples: comments saying "introduced in S11", "retired in P6", "replaced by..." — delete; canonical docs carry timing. |
| ADR-link comments | Keep only when explaining a non-obvious local decision; otherwise prefer self-contained explanation. |
| G-number comments | Keep guardrail IDs as report metadata (e.g., `Code: "G16"`). Drop G-number-first naming ("G58 validation") in favor of behavior names ("Cross-family dependency resolvability validation"). Guardrail ID as bracketed metadata (`[G58]`) in error messages is fine. |
| TODO comments | Delete or convert to canonical docs / GitHub issue. |
| `using` cleanup | Remove unused imports left by deletions. |

Specific known sites for sweep:

- `CoreLibraryIdentityValidator` XML doc references "S11" — drop, replace with self-contained explanation.
- `CsprojPackContractValidator` XML doc remarks include retired guardrail history (G1-G5, G8) — trim to current rules (G6, G7, G17, G18) and brief retirement note.
- `PreflightReporter` log strings reference "G58" — switch to "[G58]" metadata position consistently.
- `PackagePipeline` log error string "G58 ..." → "[G58] ..." (within boundary update scope).
- Any `// Phase X` / `// P5` / `// P6` comments in production code — delete; canonical docs carry phase metadata.

## 14. Cross-platform smoke validation gate (D8)

Per [docs/playbook/cross-platform-smoke-validation.md](../../playbook/cross-platform-smoke-validation.md). Trigger criteria from playbook §"When to run cross-platform" matched by this slice:

- ✅ Cake `BuildContext` / DI composition (touched: `PackageBuildConfiguration` shape change, `AddValidators()` introduction)
- ✅ Package, NativeSmoke, PackageConsumerSmoke pipelines (touched: boundary type swap)
- ✅ `IRuntimeProfile`, `IPathService`, or any platform-conditional code (touched: `IPathService.GetVcpkgManifestFile()` consumer migrated)

Workflow:

1. **Windows** local: `dotnet run --file tools.cs -- ci-sim` → 8/8 PASS.
2. **Commit + push** after Deniz approves summary + commit message (per AGENTS.md).
3. **WSL** non-interactive: `wsl -- zsh -lc 'cd /home/deniz/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim'`
4. **macOS** SSH: `ssh Armut@192.168.50.178 'zsh -lc "cd /Users/armut/repos/sdl2-cs-bindings && git fetch origin && git reset --hard origin/master && git submodule update --init --recursive && dotnet run --file tools.cs -- ci-sim"'`
5. **GitHub Actions release pipeline** manual `workflow_dispatch` trigger by Deniz.

Failures triaged per playbook §"Failure triage": fix on Windows (source of truth), commit+push, re-pull on validation host, re-run.

## 15. Risks

| Risk | Likelihood | Mitigation |
| --- | --- | --- |
| `PackagePipeline` boundary type swap breaks dict-shaped internal logic in unexpected ways | Medium | Boundary swap is mechanical; internal logic uses `.ToNormalizedString()` and `.ContainsKey()` patterns that map cleanly to `PackageFamilyVersionSet` API. Scenario tests catch regressions. |
| Pack-time G58 validation receives subtly different ordering after PackageFamilyVersionSet conversion | Low | `PackageFamilyVersionSet` enumerates in deterministic ordinal-key order; G58 validation result independent of order. |
| `PackageBuildConfiguration` consumers (Package + Smoke + Publish) compile but test setup breaks because mock dict-construction utilities propagate | Medium | Touch all V1→V2 migrations of those tests in same slice. |
| `[GeneratedRegex]` source generator interaction with `sealed partial class` requires specific .NET 7+ tooling | Very Low | .NET 10 SDK with C# 14 supports this. Existing `IsExternalInit` and other partial source generation patterns prove tooling. |
| Cross-platform smoke surfaces a PreFlight-only path bug Windows missed | Low-medium | Three-host validation is the gate. `IPathService.GetVcpkgManifestFile()` is `RepoRoot.CombineWithFilePath("vcpkg.json")` — case-sensitive on Linux, but path is lowercase by convention. |
| Slice scope creep due to PackageBuildConfiguration touching | Medium | Hard cap: pipeline INTERNAL refactor stays P7/P9. P6 only swaps boundary types. Internal `Dictionary<string, NuGetVersion>` use in PackagePipeline orchestration outside G58/version lookup stays — only those two touch points become typed. |

## 16. Exit criteria

- [ ] `Targets/PreFlightCheck/PreFlightCheckTask.cs` exists, owns orchestration, no private methods.
- [ ] `Features/Preflight/` directory deleted.
- [ ] `Validation/` root concept established with `Manifest/`, `Versioning/`, `Packaging/`, `Models/`, `Conventions/` folders.
- [ ] All 7 validators are sealed classes implementing `IFoo` interfaces, registered as `Singleton` via `Validation/ServiceCollectionExtensions.AddValidators()` using `AddSingleton<IFoo, Foo>()`.
- [ ] `PreflightPipeline` deleted.
- [ ] `PreflightRequest` deleted.
- [ ] 5 OneOf result types deleted (`VersionConsistencyResult`, `CoreLibraryIdentityResult`, `CsprojPackContractResult`, `UpstreamVersionAlignmentResult`, `PreflightError`).
- [ ] G58-prefixed interface renamed to `ICrossFamilyDependencyResolvabilityValidator`; `IUpstreamVersionAlignmentValidator` and `ICsprojPackContractValidator` retargeted to typed boundaries; new `IVersionConsistencyValidator` / `ICoreLibraryIdentityValidator` / `IManifestFamilyNameInvariantValidator` / `IHybridStaticOverlayValidator` interfaces added.
- [ ] G58-prefixed types renamed to `CrossFamilyDependency*`.
- [ ] `Shared/Versioning/` directory deleted (empty after relocations).
- [ ] G58 files relocated from `Shared/Packaging/` to `Validation/Versioning/`.
- [ ] `IVcpkgManifestRepository` + `VcpkgManifestRepository` exist in `Repositories/`, registered via `AddRepositories()`.
- [ ] `ManifestFamilyNameInvariantValidator` (G59) implemented + registered + tested.
- [ ] `PackageBuildConfiguration.FamilyVersionMapping: IReadOnlyDictionary<string, NuGetVersion>` replaced with `FamilyVersions: PackageFamilyVersionSet`.
- [ ] `Program.cs` composition factory builds `PackageFamilyVersionSet` (not dict).
- [ ] `PackagePipeline`, `PackageConsumerSmokePipeline`, `PublishStagingPipeline` boundary types swapped to `PackageFamilyVersionSet`.
- [ ] `grep -r "IReadOnlyDictionary<string, NuGetVersion>" build/_build/` returns ZERO matches in production code.
- [ ] All touched test files on V2 infrastructure (`FakeCakeWorldV2`, `TargetTestHostV2`, `TestLogV2`).
- [ ] V2 scenario tests for `PreFlightCheckTask` exist (success + 5+ representative failure paths).
- [ ] `dotnet build build/_build/Build.csproj` → 0 warnings, 0 errors.
- [ ] `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0` → all green.
- [ ] `dotnet run --file tools.cs -- ci-sim` → 8/8 PASS on Windows.
- [ ] `dotnet run --file tools.cs -- ci-sim` → 8/8 PASS on WSL.
- [ ] `dotnet run --file tools.cs -- ci-sim` → 8/8 PASS on macOS.
- [ ] GitHub Actions release pipeline manual trigger → all jobs PASS, packages pushed to staging.
- [ ] Slopwatch baseline rebuilt; `slopwatch analyze` → 0 issues.
- [ ] Doc sweep complete (refactor plan, parking-lot, release-guardrails, AGENTS.md, plan.md, ADR-002).
- [ ] Code doc sweep complete (XML doc comments, stale phase/migration comments, G-number-first → behavior-first names).
- [ ] No `_pipeline` / `*Pipeline` / `*Configuration` ceremonies introduced.
- [ ] `tools.cs` and `release.yml` command contracts unchanged.

## 17. Open implementation questions

| Question | Recommendation | Final decision |
| --- | --- | --- |
| Should `FamilyIdentifierConventions` and validation models stay in shared `Validation/Models/` and `Validation/Conventions/` folders, or fragment per-domain? | Single shared folders — small types, cross-validator reuse. | **Resolved:** kept single shared folders. |
| Does `PackagePipeline` keep an internal `Dictionary<string, NuGetVersion>` for tracking selected family versions, or convert internally to `PackageFamilyVersionSet`? | Originally "either acceptable, P7 will rewrite". | **Resolved (B-mode):** Deniz called the half-typed boundary mode *"yarım iş"* mid-execution; chose full typed end-to-end. Phase 4 finish converted PackagePipeline + PackageConsumerSmokePipeline + PublishPipeline internal helpers to consume `PackageFamilyVersionSet` directly. ZERO `Dictionary<string, NuGetVersion>` / `IReadOnlyDictionary<string, NuGetVersion>` remain in production code. |
| Where does `PathsEqual` private utility live if it gets a second consumer in P7? | Defer — keep private to `CsprojPackContractValidator` for now. Promotion to `Validation/Conventions/` only on second real consumer. | **Deferred:** kept private. P7 follow-up. |
| Does `BuildContext.Manifest` get a deprecation comment in this slice (signaling P10 retirement)? | No — comments rot. P10 will retire it cleanly. Migrated targets simply don't read it. | **Resolved:** no deprecation comment. |
| `CharacterizationTests` for old strategy field removal — do any need updates beyond G58 type rename? | Inspect during implementation; expect minimal touch. | **Resolved:** minimal G58 type-rename touch only. |
| `VersionConsistencyValidator.ParseSemanticVersion` / `TryParseSemanticVersion` wrapper soup (audit gap #5 surfaced post-execution). | Spec §11 said merge to single `TryParseSemanticVersion` (no exception path). | **Resolved:** Phase 7 merged to single `TryParseSemanticVersion(string, out tuple)` returning `bool`. `SemanticVersionParsingTests` updated to assert `bool` return + `out` tuple. |
| `PreFlightCheckTask` ctor verbosity (audit gap #6 surfaced post-execution): 11 primary-ctor params + 11 explicit `private readonly` fields with `ArgumentNullException.ThrowIfNull` checks. Sibling targets use primary-ctor-as-capture pattern. | Decide style consistency. | **Resolved:** keep verbose null-check style for cross-cutting validation gate. Style inconsistency between PreFlightCheckTask and migrated PreflightReporter / sibling targets is acknowledged but not resolved here — candidate for ADR-002 §8 amendment or build-host coding-style guideline. Phase X follow-up. |

## 18. Validation flow for the slice

Estimated commit boundaries (subject to refinement during writing-plans phase):

1. Establish `Validation/` root + move + rename G58 files.
2. Move + rename UpstreamVersionAlignment files.
3. Move PreFlight validators to `Validation/Manifest/` and `Validation/Packaging/`.
4. Static→instance refactor for VersionConsistency + CoreLibraryIdentity validators.
5. Drop interfaces (`ICsprojPackContractValidator`, `IG58...`, `IUpstreamVersionAlignment...`).
6. Add `IVcpkgManifestRepository`.
7. Add `ManifestFamilyNameInvariantValidator` + tests.
8. Rewrite `PreFlightCheckTask` + new `Targets/PreFlightCheck/` module.
9. Universal `PackageFamilyVersionSet` boundary swap (`PackageBuildConfiguration` + Program.cs + 3 pipelines + 4 tasks + 2 validators).
10. Delete OneOf result types.
11. Migrate all touched test files to V2.
12. Add V2 scenario tests for `PreFlightCheckTask`.
13. Update DI registrations (`AddValidators`, `AddRepositories`, `AddPreFlightCheck`, drop dupes from Packaging).
14. Doc sweep.
15. Code doc sweep.
16. Slopwatch baseline rebuild.
17. Cross-platform validation (Windows + WSL + macOS).
18. Summary + commit message preview, Deniz approval, single conceptual squash if appropriate (or keep multi-commit per slice if it tells a clearer story).
19. GitHub Actions release pipeline manual trigger.

## 19. Definition of done

PreFlightCheck cross-cutting validation is migrated. The build host now has:

- A real `Validation/` named concept with domain alt-folders that holds every cross-cutting validator the build host needs.
- `PreFlightCheckTask` that reads as a build story: load manifest → load vcpkg manifest → load versions → run 7 validators → throw once if anything red.
- Repository symmetry for both manifest files.
- Zero raw dictionary boundaries in version handling — `PackageFamilyVersionSet` everywhere.
- One canonical guardrail (G59) catching mixed-case manifest authoring mistakes.
- Five fewer OneOf result types; three fewer ceremonial interfaces.
- All build-host validators registered once via `AddValidators()`.
- Cross-platform validation against the manifest schema + DI composition + Pack/Smoke/Publish boundary changes.

P6 closed; P7 (Package boss fight) becomes the next slice.
