# Cake Build Host Strategy Implementation Brief

**Date:** 2026-04-14
**Status:** Implementation design — ready for coding
**Issue:** [#85](https://github.com/janset2d/sdl2-cs-bindings/issues/85)
**Related:** [execution-model-strategy-2026-04-13.md](execution-model-strategy-2026-04-13.md), [knowledge-base/cake-build-architecture.md](../knowledge-base/cake-build-architecture.md)

## Goal

Evolve the Cake Frosting build host from a harvest-only pipeline into a strategy-aware pipeline that supports both pure-dynamic (backward compat) and hybrid-static (new default) packaging models. Repurpose existing runtime scanners as packaging guardrails.

## Current Architecture (What Exists)

### Entry Point

[build/_build/Program.cs](../../build/_build/Program.cs) — System.CommandLine CLI parsing + Cake Frosting DI bootstrap.

**CLI args parsed:** `--target`, `--repo-root`, `--vcpkg-dir`, `--vcpkg-installed-dir`, `--library`, `--rid`, `--use-overrides`, `--config`, `--dll`

### DI Container (Program.cs ConfigureServices)

```
Singletons:
├── VcpkgConfiguration          ← --library, --rid from CLI
├── RepositoryConfiguration      ← --repo-root (git rev-parse fallback)
├── DotNetBuildConfiguration     ← --config (Release/Debug)
├── DumpbinConfiguration         ← --dll list
├── IPathService → PathService
├── IRuntimeProfile → RuntimeProfile
├── IPackageInfoProvider → VcpkgCliProvider
├── IBinaryClosureWalker → BinaryClosureWalker
├── IArtifactPlanner → ArtifactPlanner
├── IArtifactDeployer → ArtifactDeployer
└── IRuntimeScanner → platform-specific:
    ├── WindowsDumpbinScanner (Windows)
    ├── LinuxLddScanner (Linux)
    └── MacOtoolScanner (macOS)

Config models (from JSON):
├── RuntimeConfig ← build/runtimes.json
├── ManifestConfig ← build/manifest.json
└── SystemArtefactsConfig ← build/system_artefacts.json
```

### Task Graph

```
InfoTask (default) ← displays environment info
PreFlightCheckTask ← validates manifest.json ↔ vcpkg.json version consistency
HarvestTask [depends: InfoTask] ← main workhorse
    → BinaryClosureWalker.BuildClosureAsync()
    → ArtifactPlanner.CreatePlanAsync()
    → ArtifactDeployer.DeployArtifactsAsync()
    → generates RID status files
ConsolidateHarvestTask [depends: HarvestTask] ← merges per-RID results
LddTask, OtoolAnalyzeTask, DependentsTask ← diagnostic tools (not in main pipeline)
```

### Key Files to Understand

| File | Role | Changes needed? |
| --- | --- | --- |
| `Program.cs` | CLI args + DI wiring | **Yes** — add `--strategy`, `--native-source`, `--validation-mode` args; register new services |
| `Context/BuildContext.cs` | Cake context, holds DI services | **Yes** — add strategy + validation references |
| `Context/Configs/VcpkgConfiguration.cs` | Holds `--library`, `--rid` | **Maybe** — add strategy field |
| `Modules/PathService.cs` | All path construction | **No** — stable, reuse as-is |
| `Modules/RuntimeProfile.cs` | RID↔triplet resolution, system file filtering | **No** — stable, reuse as-is |
| `Modules/Harvesting/BinaryClosureWalker.cs` | Two-stage graph walk (vcpkg metadata + binary scan) | **No** — stable, output feeds into new validator |
| `Modules/Harvesting/ArtifactPlanner.cs` | Plans deployment (copy/archive) per platform | **Minor** — may need strategy-aware filtering |
| `Modules/Harvesting/ArtifactDeployer.cs` | Executes plan (file copy + tar.gz) | **No** — stable |
| `Modules/DependencyAnalysis/WindowsDumpbinScanner.cs` | dumpbin /dependents parser | **No** — repurposed as guardrail input |
| `Modules/DependencyAnalysis/LinuxLddScanner.cs` | ldd output parser | **No** — same |
| `Modules/DependencyAnalysis/MacOtoolScanner.cs` | otool -L parser | **No** — same |
| `Tasks/Harvest/HarvestTask.cs` | Orchestrates harvest pipeline | **Yes** — thin out, delegate to pipeline service |
| `Tasks/Harvest/ConsolidateHarvestTask.cs` | Merges RID results | **No** — stable |

### Result Monad Pattern

The codebase uses `OneOf<Error, Success>` monads:

```csharp
ClosureResult = OneOf<HarvestingError, BinaryClosure>
ArtifactPlannerResult = OneOf<HarvestingError, DeploymentPlan>
CopierResult = OneOf<CopierError, Success>
```

New services should follow this same pattern.

## Proposed Architecture

### Four New Interfaces

```csharp
// 1. What packaging model are we targeting?
public interface IPackagingStrategy
{
    PackagingModel Model { get; } // PureDynamic | HybridStatic
    bool IsCoreLibrary(string vcpkgName);
    IReadOnlySet<string> GetExpectedDynamicDeps(string libraryName);
}

// 2. Does the output match the strategy?
public interface IDependencyPolicyValidator
{
    ValidationResult Validate(BinaryClosure closure, LibraryManifest manifest);
}

// 3. Where do native binaries come from?
public interface INativeAcquisitionStrategy
{
    NativeSource Source { get; } // VcpkgBuild | Overrides | CiArtifact
    string GetBinaryDirectory(string triplet);
}

// 4. How do binaries go into the package?
public interface IPayloadLayoutPolicy
{
    DeploymentStrategy GetStrategy(string rid); // DirectCopy | Archive
    string GetOutputPath(string libraryName, string rid);
}
```

### Enums

```csharp
public enum PackagingModel { PureDynamic, HybridStatic }
public enum NativeSource { VcpkgBuild, Overrides, CiArtifact }
public enum ValidationMode { Off, Warn, Strict }
```

### Implementations

```csharp
// --- Strategy ---
public class HybridStaticStrategy : IPackagingStrategy
{
    // Core library (sdl2) is the only allowed external dynamic dep
    // All other transitive deps must be absent from closure
    public PackagingModel Model => PackagingModel.HybridStatic;
    public bool IsCoreLibrary(string vcpkgName) => vcpkgName == _coreLibName;
    public IReadOnlySet<string> GetExpectedDynamicDeps(string libraryName)
        => libraryName == _coreLibName
            ? new HashSet<string>() // core has no SDL deps
            : new HashSet<string> { _coreLibName }; // satellites depend only on core
}

public class PureDynamicStrategy : IPackagingStrategy
{
    // All transitive deps expected as separate files (legacy behavior)
    public PackagingModel Model => PackagingModel.PureDynamic;
    public IReadOnlySet<string> GetExpectedDynamicDeps(string libraryName)
        => /* all transitive deps from closure */;
}

// --- Validator ---
public class HybridStaticValidator : IDependencyPolicyValidator
{
    // Uses BinaryClosure output from BinaryClosureWalker
    // For non-core libraries: any dependency that is NOT:
    //   - a system artifact (RuntimeProfile.IsSystemFile)
    //   - the core SDL2 library
    // → is a POLICY VIOLATION (transitive dep leaked)
    public ValidationResult Validate(BinaryClosure closure, LibraryManifest manifest)
    {
        if (manifest.CoreLib) return ValidationResult.Pass();

        var violations = closure.Nodes
            .Where(n => n.OwnerPackage != "sdl2"
                     && !_runtimeProfile.IsSystemFile(n.Path))
            .ToList();

        return violations.Any()
            ? ValidationResult.Fail(violations, _validationMode)
            : ValidationResult.Pass();
    }
}
```

### Strategy Resolution — Fallback Chain + Coherence Check

Strategy is determined by a fallback chain. The first non-null source wins:

```text
1. CLI flag:        --strategy hybrid-static       (explicit override, highest priority)
2. Triplet name:    "x64-windows-hybrid" → infer HybridStatic
                    "x64-linux-dynamic"  → infer PureDynamic
3. Config default:  manifest.json packaging_config  (if it ever gets a strategy field)
4. Hardcoded:       HybridStatic                    (project default going forward)
```

**Triplet inference rule:** If the resolved triplet name contains `-hybrid`, strategy = `HybridStatic`. Otherwise, `PureDynamic`. This keeps runtimes.json as the single source of truth — the triplet encodes the strategy.

**Coherence check (mandatory):** After strategy resolution, validate that the triplet and strategy are compatible. If they conflict, fail early with a clear error:

```csharp
// In Program.cs or a dedicated StrategyResolver service
if (strategy == PackagingModel.HybridStatic && !triplet.Contains("hybrid"))
    throw new InvalidOperationException(
        $"Strategy is HybridStatic but triplet '{triplet}' is not a hybrid triplet. " +
        $"Use a hybrid triplet (e.g., x64-windows-hybrid) or set --strategy pure-dynamic.");

if (strategy == PackagingModel.PureDynamic && triplet.Contains("hybrid"))
    throw new InvalidOperationException(
        $"Strategy is PureDynamic but triplet '{triplet}' is a hybrid triplet. " +
        $"Use a stock triplet (e.g., x64-windows-release) or set --strategy hybrid-static.");
```

This prevents silent misconfiguration — you can't accidentally validate a hybrid build with pure-dynamic rules or vice versa.

### DI Registration (Program.cs changes)

```csharp
// New CLI args
// --strategy {pure-dynamic|hybrid-static}  (default: hybrid-static)
// --native-source {vcpkg-build|overrides|ci-artifact}  (default: vcpkg-build)
// --validation-mode {off|warn|strict}  (default: from manifest.json)
// --use-overrides  (deprecated alias for --native-source overrides)

// New registrations
services.AddSingleton<IPackagingStrategy>(sp =>
    strategy == PackagingModel.HybridStatic
        ? new HybridStaticStrategy(manifestConfig)
        : new PureDynamicStrategy(manifestConfig));

services.AddSingleton<IDependencyPolicyValidator>(sp =>
    strategy == PackagingModel.HybridStatic
        ? new HybridStaticValidator(sp.GetRequiredService<IRuntimeProfile>(), validationMode)
        : new PureDynamicValidator());

services.AddSingleton<INativeAcquisitionStrategy>(sp =>
    nativeSource switch {
        NativeSource.VcpkgBuild => new VcpkgBuildProvider(sp.GetRequiredService<IPathService>()),
        NativeSource.Overrides => new OverrideDirProvider(overridePath),
        NativeSource.CiArtifact => new CiArtifactProvider(artifactPath),
        _ => throw new ArgumentException()
    });

services.AddSingleton<IPayloadLayoutPolicy, DefaultPayloadLayoutPolicy>();
```

### HarvestTask Refactor

```csharp
// BEFORE (current): 200+ lines, all logic inline
[TaskName("Harvest")]
public class HarvestTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext ctx)
    {
        // ... inline closure walk, plan, deploy, status file generation
    }
}

// AFTER: thin orchestrator, ~30 lines
[TaskName("Harvest")]
public class HarvestTask : AsyncFrostingTask<BuildContext>
{
    private readonly IHarvestPipeline _pipeline;

    public HarvestTask(IHarvestPipeline pipeline) => _pipeline = pipeline;

    public override async Task RunAsync(BuildContext ctx)
    {
        foreach (var library in ctx.Vcpkg.Libraries)
        {
            await _pipeline.RunAsync(library, ctx.Vcpkg.Rid);
        }
    }
}

// Pipeline service: owns the orchestration
public class HarvestPipeline : IHarvestPipeline
{
    private readonly IBinaryClosureWalker _closureWalker;
    private readonly IDependencyPolicyValidator _validator;
    private readonly IArtifactPlanner _planner;
    private readonly IArtifactDeployer _deployer;
    private readonly IPackagingStrategy _strategy;

    public async Task RunAsync(string library, string rid)
    {
        // 1. Walk closure
        var closure = await _closureWalker.BuildClosureAsync(library, rid);

        // 2. Validate against strategy
        var validation = _validator.Validate(closure, manifest);
        validation.ThrowIfStrictFail();

        // 3. Plan deployment
        var plan = await _planner.CreatePlanAsync(manifest, closure, outputRoot);

        // 4. Deploy
        await _deployer.DeployArtifactsAsync(plan);

        // 5. Status file
        await GenerateStatusFileAsync(library, rid, validation);
    }
}
```

### Config Changes

**manifest.json** — already updated with `packaging_config`:

```json
{
  "packaging_config": {
    "validation_mode": "strict",
    "core_library": "sdl2"
  },
  "library_manifests": [...]
}
```

**runtimes.json** — already updated with hybrid triplets. No further changes.

### File Organization

New files to create:

```
build/_build/
├── Context/
│   ├── Configs/
│   │   └── PackagingConfiguration.cs       ← NEW: strategy, validation mode, native source
├── Modules/
│   ├── Strategy/
│   │   ├── IPackagingStrategy.cs            ← NEW
│   │   ├── HybridStaticStrategy.cs          ← NEW
│   │   ├── PureDynamicStrategy.cs           ← NEW
│   │   ├── IDependencyPolicyValidator.cs    ← NEW
│   │   ├── HybridStaticValidator.cs         ← NEW
│   │   ├── PureDynamicValidator.cs          ← NEW
│   │   ├── INativeAcquisitionStrategy.cs    ← NEW
│   │   ├── VcpkgBuildProvider.cs            ← NEW
│   │   ├── IPayloadLayoutPolicy.cs          ← NEW
│   │   └── DefaultPayloadLayoutPolicy.cs    ← NEW
│   ├── Pipeline/
│   │   ├── IHarvestPipeline.cs              ← NEW
│   │   └── HarvestPipeline.cs               ← NEW
│   ├── Harvesting/
│   │   ├── BinaryClosureWalker.cs           ← UNCHANGED
│   │   ├── ArtifactPlanner.cs               ← MINOR CHANGES
│   │   └── ArtifactDeployer.cs              ← UNCHANGED

build/_build.Tests/                           ← NEW: TUnit test project
├── _build.Tests.csproj
├── Strategy/
│   ├── HybridStaticValidatorTests.cs
│   └── StrategySelectionTests.cs
├── Config/
│   └── PackagingConfigParsingTests.cs
└── Pipeline/
    └── HarvestPipelineTests.cs
```

## Implementation Order

1. **Create TUnit test project first** (`build/_build.Tests/`) — write tests for validator logic before writing the validator
2. **Add enums + config parsing** (PackagingModel, NativeSource, ValidationMode, PackagingConfiguration)
3. **Implement IPackagingStrategy** (HybridStatic + PureDynamic — simple, stateless)
4. **Implement IDependencyPolicyValidator** (HybridStaticValidator — uses existing BinaryClosure output)
5. **Wire into DI** (Program.cs: new CLI args + service registrations)
6. **Extract HarvestPipeline** from HarvestTask (move logic, keep task thin)
7. **Add validation step** to HarvestPipeline (between closure walk and artifact planning)
8. **Test on win-x64** — hybrid build + harvest should pass with strict validation
9. **INativeAcquisitionStrategy + IPayloadLayoutPolicy** — can be minimal stubs initially, full implementation later

## Key Architectural Insight: Scanner Repurposing

The existing runtime scanners (`WindowsDumpbinScanner`, `LinuxLddScanner`, `MacOtoolScanner`) were built for **dependency discovery** — "what does this DLL depend on?" In the new architecture, they gain a second role: **packaging guardrails**.

**Current role (preserved):** `BinaryClosureWalker` calls scanners to discover transitive deps → feeds into `ArtifactPlanner` for deployment.

**New role (added):** `HybridStaticValidator` consumes the same `BinaryClosure` output. For hybrid-static builds, the closure of a satellite library should be **near-empty** — only SDL2 core + system libs. If the scanner finds `libz.so.1` in SDL2_image's dependency list, that means the static bake failed and zlib leaked as a separate shared library. The validator catches this and fails the build (in `Strict` mode).

```text
Scanner output (same data, two consumers):

BinaryClosureWalker
    → BinaryClosure { Nodes: [...], PrimaryFiles: [...] }
        ├── ArtifactPlanner (existing)  → "what to copy/package"
        └── HybridStaticValidator (NEW) → "did anything leak that shouldn't?"
```

This means **zero changes to the scanner code itself**. The scanners produce the same output as before. The new validator is a pure consumer of that output — it just asks a different question about the same data.

The `BinaryClosure.Nodes` list already contains `OwnerPackage` and `OriginPackage` per binary, which gives the validator everything it needs to distinguish "this is SDL2 core (expected)" from "this is zlib (unexpected leak)".

Validation commands for manual checking are documented in [playbook/overlay-management.md](../playbook/overlay-management.md) (Hybrid Build Sanity Checks section).

## What NOT to Change

- **PathService** — all path logic stays. Don't refactor.
- **BinaryClosureWalker** — the two-stage graph walk is solid. Don't touch the algorithm. Just consume its output.
- **RuntimeScanners** (dumpbin/ldd/otool) — these parse tool output. Stable, well-tested by usage. Don't modify.
- **RuntimeProfile** — RID/triplet resolution + system file regex matching. Stable.
- **ConsolidateHarvestTask** — just reads status files and merges. Stable.
- **OneOf result monad pattern** — follow existing conventions.
- **Spectre.Console output** — keep rich console tables/panels for user feedback.

## Testing Strategy

**TUnit test project** (`build/_build.Tests/`):

| Test | What it validates |
| --- | --- |
| `HybridStaticValidator_RejectsTransitiveDep` | Given a closure with zlib1.dll → validation fails |
| `HybridStaticValidator_AcceptsCoreDep` | Given a closure with only SDL2.dll → validation passes |
| `HybridStaticValidator_AcceptsSystemDeps` | Given system DLLs (kernel32, libc) → validation passes |
| `HybridStaticValidator_WarnMode_DoesNotThrow` | With ValidationMode.Warn → logs but doesn't throw |
| `StrategySelection_DefaultIsHybrid` | No `--strategy` flag → HybridStaticStrategy selected |
| `StrategySelection_CliOverride` | `--strategy pure-dynamic` → PureDynamicStrategy |
| `ConfigParsing_ReadsValidationMode` | manifest.json `packaging_config.validation_mode` parsed correctly |
| `ConfigParsing_CliOverridesConfig` | `--validation-mode warn` overrides manifest.json `strict` |

## Risk Notes

1. **HarvestTask refactor is the riskiest change** — it's the main workhorse. Extract to pipeline service incrementally, not big-bang. Run existing harvest output comparison before/after.
2. **BinaryClosureWalker OriginPackage filtering** — ArtifactPlanner already filters core-originated deps for non-core libraries. The new validator should use the same logic, not reinvent it.
3. **System.CommandLine breaking changes** — the project uses `2.0.0-beta4` which is old. New args should follow existing patterns in Program.cs, not introduce new System.CommandLine patterns.
