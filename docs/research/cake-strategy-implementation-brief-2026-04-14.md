# Cake Build Host Strategy Implementation Brief

**Date:** 2026-04-14
**Status:** Implementation design — revised after alignment discussion
**Issue:** [#85](https://github.com/janset2d/sdl2-cs-bindings/issues/85)
**Related:** [execution-model-strategy-2026-04-13.md](execution-model-strategy-2026-04-13.md), [knowledge-base/cake-build-architecture.md](../knowledge-base/cake-build-architecture.md), [tunit-testing-framework-2026-04-14.md](tunit-testing-framework-2026-04-14.md)

## Revision Summary (from original brief)

| Original | Revised | Rationale |
| --- | --- | --- |
| 3 separate config files | Single `manifest.json` (schema v2) | Single source of truth, atomic updates, no cross-file drift |
| `--strategy` CLI flag | Triplet = strategy (no flag) | Triplet already determines vcpkg build; separate flag = two-headed authority |
| Strategy inferred from triplet substring only | Explicit `"strategy"` field in runtimes section, validated against triplet | Formal mapping, CI-readable, PreFlightCheck coherence validation |
| `expected_transitive_deps` in manifest | Dropped — validator uses BinaryClosureWalker output | vcpkg metadata + runtime scan = ground truth; manual lists can't be maintained across versions |
| `hybrid_transitive_deps_baked_in` in manifest | Dropped — informational only, no validation use | Same reason; if needed, derive from vcpkg at build time |
| Implementation-first | Test-first: characterization tests → config merge → TDD strategy code | Zero test coverage today; refactoring without tests = risk |

## Goal

Evolve the Cake Frosting build host from a harvest-only pipeline into a strategy-aware pipeline that supports both pure-dynamic (backward compat) and hybrid-static (new default) packaging models. Repurpose existing runtime scanners as packaging guardrails.

## Current Architecture (What Exists)

### Entry Point

[build/_build/Program.cs](../../build/_build/Program.cs) — System.CommandLine CLI parsing + Cake Frosting DI bootstrap.

**CLI args parsed:** `--target`, `--repo-root`, `--vcpkg-dir`, `--vcpkg-installed-dir`, `--library`, `--rid`, `--config`, `--dll`

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

Config models (from JSON — currently 3 files, merging to 1):
├── RuntimeConfig ← build/runtimes.json → build/manifest.json runtimes section
├── ManifestConfig ← build/manifest.json → build/manifest.json library_manifests section
└── SystemArtefactsConfig ← build/system_artefacts.json → build/manifest.json system_exclusions section
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

### Key Files — Stability Assessment

| File | Role | Changes needed? |
| --- | --- | --- |
| `Program.cs` | CLI args + DI wiring | **Yes** — register new services, load merged manifest |
| `Context/BuildContext.cs` | Cake context, holds DI services | **Yes** — add strategy + validation references |
| `Modules/PathService.cs` | All path construction | **Minor** — 3 file methods → 1 (`GetManifestFile()` stays, others removed) |
| `Modules/RuntimeProfile.cs` | RID↔triplet resolution, system file filtering | **No** — stable, reuse as-is |
| `Modules/Harvesting/BinaryClosureWalker.cs` | Two-stage graph walk (vcpkg metadata + binary scan) | **No** — stable, output feeds into new validator |
| `Modules/Harvesting/ArtifactPlanner.cs` | Plans deployment (copy/archive) per platform | **Minor** — may need strategy-aware filtering |
| `Modules/Harvesting/ArtifactDeployer.cs` | Executes plan (file copy + tar.gz) | **No** — stable |
| `Modules/DependencyAnalysis/WindowsDumpbinScanner.cs` | dumpbin /dependents parser | **No** — repurposed as guardrail input |
| `Modules/DependencyAnalysis/LinuxLddScanner.cs` | ldd output parser | **No** — same |
| `Modules/DependencyAnalysis/MacOtoolScanner.cs` | otool -L parser | **No** — same |
| `Tasks/Harvest/HarvestTask.cs` | Orchestrates harvest pipeline | **Yes** — thin out, delegate to pipeline service |
| `Tasks/Harvest/ConsolidateHarvestTask.cs` | Merges RID results | **No** — stable |
| `Tasks/Preflight/PreFlightCheckTask.cs` | Version consistency validation | **Yes** — add triplet↔strategy coherence check |

### Result Monad Pattern

The codebase uses `OneOf<Error, Success>` monads:

```csharp
ClosureResult = OneOf<HarvestingError, BinaryClosure>
ArtifactPlannerResult = OneOf<HarvestingError, DeploymentPlan>
CopierResult = OneOf<CopierError, Success>
```

New services should follow this same pattern.

## Config Merge: manifest.json Schema v2

### Before (3 files)

```
build/manifest.json          ← library definitions
build/runtimes.json          ← RID→triplet→runner mapping
build/system_artefacts.json  ← OS library exclusion lists
```

### After (1 file)

```
build/manifest.json          ← everything
```

### Merged Schema

```json
{
  "schema_version": "2.0",

  "packaging_config": {
    "validation_mode": "strict",
    "core_library": "sdl2"
  },

  "runtimes": [
    {
      "rid": "win-x64",
      "triplet": "x64-windows-hybrid",
      "strategy": "hybrid-static",
      "runner": "windows-latest",
      "container_image": null
    },
    {
      "rid": "win-arm64",
      "triplet": "arm64-windows",
      "strategy": "pure-dynamic",
      "runner": "windows-latest",
      "container_image": null
    },
    {
      "rid": "win-x86",
      "triplet": "x86-windows",
      "strategy": "pure-dynamic",
      "runner": "windows-latest",
      "container_image": null
    },
    {
      "rid": "linux-x64",
      "triplet": "x64-linux-hybrid",
      "strategy": "hybrid-static",
      "runner": "ubuntu-24.04",
      "container_image": "ubuntu:20.04"
    },
    {
      "rid": "linux-arm64",
      "triplet": "arm64-linux-dynamic",
      "strategy": "pure-dynamic",
      "runner": "ubuntu-latest-arm64",
      "container_image": "ubuntu:24.04"
    },
    {
      "rid": "osx-x64",
      "triplet": "x64-osx-hybrid",
      "strategy": "hybrid-static",
      "runner": "macos-15-intel",
      "container_image": null
    },
    {
      "rid": "osx-arm64",
      "triplet": "arm64-osx-dynamic",
      "strategy": "pure-dynamic",
      "runner": "macos-latest",
      "container_image": null
    }
  ],

  "system_exclusions": {
    "windows": {
      "system_dlls": [
        "kernel32.dll", "kernelbase.dll", "ntdll.dll", "user32.dll",
        "gdi32.dll", "winmm.dll", "imm32.dll", "advapi32.dll",
        "shell32.dll", "shlwapi.dll", "ole32.dll", "oleaut32.dll",
        "version.dll", "setupapi.dll", "winspool.dll", "comdlg32.dll",
        "comctl32.dll", "ws2_32.dll", "iphlpapi.dll", "crypt32.dll",
        "d3d9.dll", "d3d11.dll", "dxgi.dll", "ucrtbase.dll",
        "msvcp*.dll", "vcruntime*.dll", "api-ms-win-*.dll"
      ]
    },
    "linux": {
      "system_libraries": [
        "linux-vdso.so*", "ld-linux-*.so*", "libc.so*", "libm.so*",
        "libpthread.so*", "libdl.so*", "librt.so*", "libutil.so*",
        "libresolv.so*", "libnss_*.so*", "libstdc++.so*", "libgcc_s.so*",
        "libsystemd.so*", "libdbus-*.so*", "libexpat.so*", "libasound.so*",
        "libatopology.so*", "libcap.so*", "libpsx.so*", "liblzma.so*",
        "liblz4.so*", "libzstd.so*", "libblkid.so*", "libmount.so*",
        "libcrypt.so*", "libxcrypt.so*", "libowcrypt.so*"
      ]
    },
    "osx": {
      "system_libraries": [
        "libSystem.B.dylib", "libobjc.A.dylib",
        "CoreVideo.framework", "Cocoa.framework", "IOKit.framework",
        "ForceFeedback.framework", "Carbon.framework", "CoreAudio.framework",
        "AudioToolbox.framework", "AVFoundation.framework", "Foundation.framework",
        "GameController.framework", "Metal.framework", "QuartzCore.framework",
        "CoreHaptics.framework", "AppKit.framework", "CoreFoundation.framework",
        "CoreGraphics.framework", "CoreServices.framework"
      ]
    }
  },

  "library_manifests": [
    {
      "name": "SDL2",
      "vcpkg_name": "sdl2",
      "vcpkg_version": "2.32.10",
      "vcpkg_port_version": 0,
      "native_lib_name": "SDL2.Core.Native",
      "native_lib_version": "2.32.10.0",
      "core_lib": true,
      "primary_binaries": [
        { "os": "Windows", "patterns": ["SDL2.dll"] },
        { "os": "Linux", "patterns": ["libSDL2*"] },
        { "os": "OSX", "patterns": ["libSDL2*.dylib"] }
      ]
    }
  ]
}
```

**Key design points:**

- `schema_version` enables future breaking changes
- `runtimes[].strategy` is the formal triplet→strategy mapping — triplet is the authority, strategy is the validated declaration
- `system_exclusions` replaces the standalone `system_artefacts.json`
- `library_manifests` unchanged from current manifest.json
- `packaging_config` unchanged

### Migration Impact

| Component | Change | Complexity |
| --- | --- | --- |
| `PathService` | Remove `GetRuntimesFile()`, `GetSystemArtifactsFile()` | Trivial |
| `IPathService` | Same method removals | Trivial |
| `Program.cs` DI | 3 JSON loads → 1 load, extract 3 typed configs | Low |
| Model files | 3 files → 1 combined `BuildManifestModels.cs` | Low |
| Consumer code | Zero changes — same typed DI injections | None |
| CI workflows | No changes (they don't read these files directly yet) | None |

## Triplet = Strategy

### Design

The triplet name encodes the packaging strategy. No `--strategy` CLI argument exists.

**Authority chain:** triplet → strategy. The `runtimes[].strategy` field in manifest.json is a formal declaration, not a separate authority. If triplet and strategy field are inconsistent, `PreFlightCheckTask` fails the build.

**Convention:**

- Triplet contains `-hybrid` → `PackagingModel.HybridStatic`
- Otherwise → `PackagingModel.PureDynamic`

### Strategy Resolution

```csharp
// Single resolution point — reads from manifest runtimes section
public static PackagingModel ResolveStrategy(RuntimeInfo runtime)
{
    var expectedFromTriplet = runtime.Triplet.Contains("-hybrid", StringComparison.OrdinalIgnoreCase)
        ? PackagingModel.HybridStatic
        : PackagingModel.PureDynamic;

    var declared = runtime.Strategy switch
    {
        "hybrid-static" => PackagingModel.HybridStatic,
        "pure-dynamic" => PackagingModel.PureDynamic,
        _ => throw new InvalidOperationException($"Unknown strategy '{runtime.Strategy}' for RID {runtime.Rid}")
    };

    if (expectedFromTriplet != declared)
        throw new InvalidOperationException(
            $"Triplet '{runtime.Triplet}' implies {expectedFromTriplet} but manifest declares {declared} for RID {runtime.Rid}. " +
            $"Fix the strategy field in manifest.json or use the correct triplet.");

    return declared;
}
```

### PreFlightCheckTask Enhancement

In addition to existing version consistency checks, PreFlightCheck gains triplet↔strategy coherence validation for all runtimes in the manifest.

## Proposed Architecture

### Three New Interfaces (Reduced from Four)

```csharp
// 1. What packaging model are we targeting?
public interface IPackagingStrategy
{
    PackagingModel Model { get; } // PureDynamic | HybridStatic
    bool IsCoreLibrary(string vcpkgName);
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
```

**Note:** `IPayloadLayoutPolicy` is deferred. Current `ArtifactPlanner` already handles Windows direct-copy vs Unix archive. Policy extraction can happen when PackageTask is implemented.

### Enums

```csharp
public enum PackagingModel { PureDynamic, HybridStatic }
public enum NativeSource { VcpkgBuild, Overrides, CiArtifact }
public enum ValidationMode { Off, Warn, Strict }
```

### Validator — Uses BinaryClosureWalker Output, No Manual Lists

The key insight: in hybrid-static mode, `BinaryClosureWalker` already reveals the ground truth. If zlib was successfully baked into SDL2_image.dll, then `dumpbin /dependents` won't show `zlib1.dll`. If bake failed, zlib appears in the closure.

```csharp
public class HybridStaticValidator : IDependencyPolicyValidator
{
    private readonly IRuntimeProfile _profile;
    private readonly IPackagingStrategy _strategy;
    private readonly ValidationMode _mode;

    public ValidationResult Validate(BinaryClosure closure, LibraryManifest manifest)
    {
        // Core libraries have no policy constraints
        if (manifest.IsCoreLib) return ValidationResult.Pass();

        // For satellite libraries in hybrid mode:
        // Every non-system, non-core binary in the closure = transitive dep leak
        var violations = closure.Nodes
            .Where(node =>
                !_profile.IsSystemFile(node.Path)
                && !_strategy.IsCoreLibrary(node.OwnerPackage)
                && !closure.IsPrimaryFile(node.Path))
            .ToList();

        if (violations.Count == 0) return ValidationResult.Pass();

        return _mode switch
        {
            ValidationMode.Strict => ValidationResult.Fail(violations),
            ValidationMode.Warn => ValidationResult.Warn(violations),
            ValidationMode.Off => ValidationResult.Pass(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
```

**Data sources — zero manual maintenance:**

- "Is system file?" → `IRuntimeProfile.IsSystemFile()` ← reads `system_exclusions` from manifest
- "Is core library?" → `IPackagingStrategy.IsCoreLibrary()` ← reads `packaging_config.core_library` from manifest
- "Is primary file?" → `BinaryClosure.IsPrimaryFile()` ← resolved from `library_manifests[].primary_binaries` patterns
- Everything else in the closure = violation

### DI Registration (Program.cs changes)

```csharp
// Strategy resolved from manifest runtimes section — no CLI arg
services.AddSingleton<IPackagingStrategy>(sp =>
{
    var runtimeProfile = sp.GetRequiredService<IRuntimeProfile>();
    var manifest = sp.GetRequiredService<ManifestConfig>();
    // Strategy is resolved from triplet + validated against manifest declaration
    return runtimeProfile.Strategy == PackagingModel.HybridStatic
        ? new HybridStaticStrategy(manifest)
        : new PureDynamicStrategy(manifest);
});

services.AddSingleton<IDependencyPolicyValidator>(sp =>
{
    var strategy = sp.GetRequiredService<IPackagingStrategy>();
    var profile = sp.GetRequiredService<IRuntimeProfile>();
    var manifest = sp.GetRequiredService<ManifestConfig>();
    var mode = manifest.PackagingConfig.ParseValidationMode();

    return strategy.Model == PackagingModel.HybridStatic
        ? new HybridStaticValidator(profile, strategy, mode)
        : new PureDynamicValidator();
});
```

### HarvestTask Refactor

```csharp
// AFTER: thin orchestrator
[TaskName("Harvest")]
public class HarvestTask : AsyncFrostingTask<BuildContext>
{
    private readonly IHarvestPipeline _pipeline;

    public HarvestTask(IHarvestPipeline pipeline) => _pipeline = pipeline;

    public override async Task RunAsync(BuildContext ctx)
    {
        foreach (var library in ctx.ResolveLibrariesToHarvest())
        {
            await _pipeline.RunAsync(library, ctx);
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

    public async Task RunAsync(LibraryManifest manifest, BuildContext ctx)
    {
        // 1. Walk closure (unchanged — BinaryClosureWalker is stable)
        var closureResult = await _closureWalker.BuildClosureAsync(manifest);
        closureResult.ThrowIfError(...);

        // 2. Validate against strategy (NEW — the guardrail)
        var validation = _validator.Validate(closureResult.Closure, manifest);
        validation.ThrowIfStrictFail();

        // 3. Plan deployment (unchanged — ArtifactPlanner is stable)
        var planResult = await _planner.CreatePlanAsync(manifest, closureResult.Closure, outputBase);
        planResult.ThrowIfError(...);

        // 4. Deploy (unchanged — ArtifactDeployer is stable)
        await _deployer.DeployArtifactsAsync(planResult.DeploymentPlan);

        // 5. Status file (moved from HarvestTask inline code)
        await GenerateStatusFileAsync(manifest, validation);
    }
}
```

## File Organization

### New Files to Create

```
build/_build/
├── Context/
│   ├── Models/
│   │   └── BuildManifestModels.cs      ← NEW: merged config models (replaces 3 model files)
├── Modules/
│   ├── Strategy/
│   │   ├── IPackagingStrategy.cs        ← NEW
│   │   ├── HybridStaticStrategy.cs      ← NEW
│   │   ├── PureDynamicStrategy.cs       ← NEW
│   │   ├── IDependencyPolicyValidator.cs ← NEW
│   │   ├── HybridStaticValidator.cs     ← NEW
│   │   ├── PureDynamicValidator.cs      ← NEW (passthrough — allows all deps)
│   │   ├── ValidationResult.cs          ← NEW
│   │   └── StrategyResolver.cs          ← NEW (triplet↔strategy coherence)
│   ├── Pipeline/
│   │   ├── IHarvestPipeline.cs          ← NEW
│   │   └── HarvestPipeline.cs           ← NEW (extracted from HarvestTask)

build/_build.Tests/                       ← NEW: TUnit test project
├── _build.Tests.csproj
├── Fixtures/
│   ├── ManifestFixture.cs
│   ├── RuntimeProfileFixture.cs
│   └── BinaryClosureBuilder.cs
├── Unit/
│   ├── RuntimeProfile/
│   │   └── IsSystemFileTests.cs
│   ├── PathService/
│   │   └── PathConstructionTests.cs
│   ├── PreFlight/
│   │   └── SemanticVersionParsingTests.cs
│   ├── BinaryClosureWalker/
│   │   └── PatternMatchingTests.cs
│   ├── Config/
│   │   └── ManifestDeserializationTests.cs
│   └── Strategy/
│       ├── HybridStaticValidatorTests.cs
│       └── StrategyResolutionTests.cs
└── Integration/
    └── Pipeline/
        └── HarvestPipelineTests.cs
```

### Files to Remove After Migration

```
build/runtimes.json          ← content moved to manifest.json
build/system_artefacts.json  ← content moved to manifest.json
```

## Implementation Order

### Phase 0: Documentation (CURRENT)

Update all canonical docs to reflect alignment decisions before any code changes.

### Phase 1: Characterization Tests

Cover the status quo with tests BEFORE any refactoring. Zero production code changes.

1. Create TUnit test project (`build/_build.Tests/`)
2. Add `"test": { "runner": "Microsoft.Testing.Platform" }` to global.json
3. Add TUnit + NSubstitute to Directory.Packages.props
4. Write ~20 characterization tests for existing pure functions:
   - `RuntimeProfile.IsSystemFile()` — 3 platforms × system/non-system files
   - `PreFlightCheckTask.ParseSemanticVersion()` — standard, pre-release, invalid
   - `BinaryClosureWalker.MatchesPattern()` — exact, wildcard, case-insensitive (needs InternalsVisibleTo or extraction)
   - Config deserialization — parse real manifest.json, runtimes.json, system_artefacts.json
   - `PathService` path construction — harvest dirs, vcpkg dirs
5. Verify all tests pass against current code

### Phase 2: Config Merge

Merge 3 config files → 1 manifest.json. Characterization tests provide safety net.

1. Create merged manifest.json (schema v2)
2. Create combined `BuildManifestModels.cs`
3. Update `PathService` (remove 2 methods)
4. Update `Program.cs` DI (1 load → extract 3 typed configs)
5. Delete old files (runtimes.json, system_artefacts.json, old model files)
6. Verify all characterization tests still pass
7. Update config deserialization tests for new schema

### Phase 3: TDD Strategy + Validator

Write tests FIRST, then implement to make them pass.

1. Write `HybridStaticValidatorTests` (~8 tests):
   - Core library → always passes
   - Satellite with only core dep → passes
   - Satellite with transitive dep leak → fails (strict), warns (warn), passes (off)
   - System files filtered correctly
2. Write `StrategyResolutionTests` (~5 tests):
   - Triplet with `-hybrid` → HybridStatic
   - Triplet without `-hybrid` → PureDynamic
   - Triplet↔strategy field mismatch → throws
3. Implement `IPackagingStrategy`, `IDependencyPolicyValidator`, `StrategyResolver`
4. Wire into DI in `Program.cs`
5. Enhance `PreFlightCheckTask` with coherence check
6. Verify all tests pass

### Phase 4: Pipeline Extraction

Extract orchestration from HarvestTask into HarvestPipeline service.

1. Write `HarvestPipelineTests` (~4 tests):
   - Full flow deploys successfully
   - Validation failure in strict mode → throws
   - Validation failure in warn mode → continues
2. Create `IHarvestPipeline` + `HarvestPipeline`
3. Thin out `HarvestTask` to delegate to pipeline
4. Add validation step between closure walk and artifact planning
5. Verify all tests pass
6. Run real harvest on win-x64 and compare output before/after

### Phase 5: CI Updates (Phase 2b scope)

Deferred to Phase 2b — not part of the foundation spike.

1. Add `--overlay-triplets` to vcpkg-setup action
2. Update prepare-native-assets workflows to use hybrid triplets
3. Implement dynamic matrix generation from manifest.json
4. Update local development playbook

## Scanner Repurposing — Key Architectural Insight

The existing runtime scanners (`WindowsDumpbinScanner`, `LinuxLddScanner`, `MacOtoolScanner`) were built for **dependency discovery**. In the new architecture, they gain a second role: **packaging guardrails**.

**Current role (preserved):** `BinaryClosureWalker` calls scanners to discover transitive deps → feeds into `ArtifactPlanner`.

**New role (added):** `HybridStaticValidator` consumes the same `BinaryClosure` output. For hybrid builds, the closure of a satellite should be near-empty — only SDL2 core + system libs. If the scanner finds `zlib1.dll` in SDL2_image's closure, that means the static bake failed and zlib leaked. The validator catches this.

```text
Scanner output (same data, two consumers):

BinaryClosureWalker
    → BinaryClosure { Nodes: [...], PrimaryFiles: [...] }
        ├── ArtifactPlanner (existing)  → "what to copy/package"
        └── HybridStaticValidator (NEW) → "did anything leak?"
```

**Zero changes to scanner code.** The scanners produce the same output as before. The validator is a pure consumer.

## What NOT to Change

- **PathService path construction logic** — stable, well-covered by tests
- **BinaryClosureWalker algorithm** — two-stage graph walk is solid
- **Runtime scanners** (dumpbin/ldd/otool output parsers) — stable
- **RuntimeProfile** — RID/triplet resolution + system file regex
- **ConsolidateHarvestTask** — reads status files and merges
- **ArtifactDeployer** — file copy and tar.gz creation
- **OneOf result monad pattern** — follow existing conventions
- **Spectre.Console output** — keep rich console reporting

## Testing Strategy

See [tunit-testing-framework-2026-04-14.md](tunit-testing-framework-2026-04-14.md) for full TUnit adoption plan.

**Test naming convention:** `<MethodName>_Should_<Verb>_<When/If/Given>`

**Approach:** Characterize status quo → TDD new code → integration tests.

**Cake-specific testing:** File system operations go through `ICakeContext`. Unit tests mock Cake interfaces where needed (NSubstitute). A separate research pass on Cake unit testing best practices (including `System.IO.Abstractions` / TestableIO compatibility) will be done during Phase 1 when concrete testing questions arise.

## Risk Notes

1. **HarvestTask refactor is the riskiest change** — extract to pipeline service incrementally. Run harvest output comparison before/after.
2. **Config merge is low risk** — consumers don't change, only the loading path.
3. **System.CommandLine is old** (`2.0.0-beta4`) — no new CLI args needed for strategy (triplet = strategy), so no System.CommandLine changes required.
4. **BinaryClosureWalker OriginPackage filtering** — ArtifactPlanner already filters core-originated deps. Validator should reuse the same logic.

## CI/CD Integration (Phase 2b)

These are documented here for completeness but deferred to Phase 2b:

- **Dynamic matrix from manifest.json** — GitHub Actions `fromJson()` replaces hardcoded YAML matrices
- **vcpkg-setup action** — needs `--overlay-triplets=./vcpkg-overlay-triplets` flag
- **Workflow triplet updates** — switch from stock triplets to hybrid triplets defined in manifest
- **Validation mode in CI** — `packaging_config.validation_mode: "strict"` enforced in CI, `"warn"` available for local dev
- **Distributed CI consolidation** — separate staging paths per RID for multi-runner artifact aggregation
