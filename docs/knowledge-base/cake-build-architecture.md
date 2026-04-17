# Knowledge Base: Cake Frosting Build Architecture

> Deep technical reference for the Cake Frosting build system in `build/_build/`.

## Overview

The build system is a .NET 9.0 console application using **Cake Frosting v6.1.0**. It orchestrates the native binary harvesting pipeline — collecting compiled SDL2/SDL3 libraries and their transitive dependencies from vcpkg output and organizing them for NuGet packaging.

## Current Implementation Notes

- Active harvest logic lives under `Tasks/Harvest/`.
- Harvesting is the current build-host reference standard for task/service boundaries: tasks keep `BuildContext`, services take explicit inputs, and Cake capabilities are injected where they are actually used.
- `PreFlightCheckTask` is implemented in the build host, but the release-candidate workflow does not invoke it yet.
- `PreFlightCheckTask` has since been aligned closer to the Harvesting pattern: DI-loaded `ManifestConfig`, explicit validators, typed validator results, `IVcpkgManifestReader`, `IStrategyResolver`, and a reporter that owns Cake context instead of taking `ICakeLog` through every public method.
- `CoverageCheckTask` keeps path resolution and task-level failure policy, but the pass/fail decision now lives behind an injectable `ICoverageThresholdValidator` instead of a static helper call.
- `PathService` already exposes `harvest-staging` helpers for future distributed CI, but current tasks and workflows still write to `artifacts/harvest_output/`.
- Native-source acquisition mode selection is intentionally deferred from the active CLI surface.
- The build host still uses hand-written `OneOf` result wrappers. Source-generator-based cleanup remains a parked follow-up, not active build-system behavior.
- **Packaging module (Stream D-local, S1 shape, 2026-04-17)** lives under `Tasks/Packaging/` + `Modules/Packaging/`. It follows the Harvesting reference pattern: thin task (`PackageTask`, `PackageConsumerSmokeTask`, `PostFlightTask`) + narrow services (`PackageTaskRunner`, `DotNetPackInvoker`, `PackageFamilySelector`, `PackageVersionResolver`, `ProjectMetadataReader`, `PackageOutputValidator`, `PackageConsumerSmokeRunner`) + typed Results with the full `OneOf.Monads` surface (implicit/explicit operators + `From*`/`To*` factories). Every service returns a typed `Result<PackagingError, T>` instead of throwing. `PackageOutputValidator` accumulates all guardrail observations (G21–G23, G25–G27, G47, G48) into a single `PackageValidation` aggregate so operators see the complete failure set, not first-throw-wins. 3-platform validated for the `sdl2-core` + `sdl2-image` slice on `win-x64` / `linux-x64` / `osx-x64`; pure-dynamic RIDs unexercised by design (see [phase-2-adaptation-plan.md "Strategy State Audit"](../phases/phase-2-adaptation-plan.md)).

## Strategy Layer Reality Check (2026-04-17)

The strategy seam landed with Stream B (#85 closed) and the #85 handoff note in [plan.md](../plan.md) describes it as "strategy primitives + runtime wiring landed." That is technically correct but easy to misread. Before assuming the strategy layer does anything more than it does, read [`phases/phase-2-adaptation-plan.md` "Strategy State Audit"](../phases/phase-2-adaptation-plan.md) for the brief-vs-code delta. Quick summary:

- `IPackagingStrategy` is a one-method lookup helper (`IsCoreLibrary`), not a dispatcher. The Packaging module does not consume it.
- `IDependencyPolicyValidator` has one real implementation (`HybridStaticValidator`) and one intentional pass-through (`PureDynamicValidator` — by design per [`research/cake-strategy-implementation-brief-2026-04-14.md`](../research/cake-strategy-implementation-brief-2026-04-14.md)).
- `INativeAcquisitionStrategy` was designed in the brief but never implemented; its role may have been implicitly subsumed by Source Mode (Stream F).
- `IPayloadLayoutPolicy` was deferred in the brief "until PackageTask lands"; PackageTask landed, the policy extraction did not follow.
- The scanner-as-validator repurposing (dumpbin / ldd / otool outputs consumed by `HybridStaticValidator` as a second consumer with zero scanner changes) **is fully landed as designed** — this is the one architectural move from the brief that is realized end to end.

Behavioral dispatch between `hybrid-static` and `pure-dynamic` in the current code is limited to: (1) which `IDependencyPolicyValidator` instance DI resolves at harvest time; (2) `PreFlightCheckTask`'s declarative triplet↔strategy coherence. Everything downstream (pack, smoke, validator, deployer) is strategy-agnostic.

## Architecture

```text
build/_build/
├── Program.cs              ← Entry point: DI configuration, repo root detection
├── Context/                ← Build context (state shared across tasks)
├── Models/                 ← Data models (DeploymentPlan, RuntimeProfile, etc.)
├── Modules/                ← DI modules (service registration)
├── Tasks/
│   ├── Common/             ← InfoTask (environment info display)
│   ├── Harvest/            ← HarvestTask, ConsolidateHarvestTask
│   ├── Preflight/          ← PreFlightCheckTask (partial gate: version + strategy coherence)
│   ├── Coverage/           ← CoverageCheckTask (ratchet policy against coverage-baseline.json)
└── Tools/                  ← Utility services
    ├── BinaryClosureWalker ← Platform-specific dependency scanning
    ├── ArtifactPlanner     ← Plans which files to deploy
    ├── ArtifactDeployer    ← Copies/archives files to output
    ├── PathService         ← Path resolution for configs and output
    └── RuntimeProfile      ← RID/triplet/platform abstraction
```

## Service Architecture (DI)

All services are registered via dependency injection in `Program.cs`:

| Service Interface | Implementation | Purpose |
| --- | --- | --- |
| `IPathService` | `PathService` | Resolves paths to manifest.json, output dirs |
| `IRuntimeProfile` | `RuntimeProfile` | Maps RID ↔ vcpkg triplet, detects current platform |
| `IPackageInfoProvider` | `VcpkgCliProvider` | Queries vcpkg for installed package metadata |
| `IBinaryClosureWalker` | `BinaryClosureWalker` | Two-stage graph walk: vcpkg metadata + runtime scan (dumpbin/ldd/otool) |
| `IArtifactPlanner` | `ArtifactPlanner` | Determines which binaries to include and how to deploy them |
| `IArtifactDeployer` | `ArtifactDeployer` | Copies binaries to output, creates tar.gz for Unix |
| `IRuntimeScanner` | Platform-specific | dumpbin (Windows), ldd (Linux), otool (macOS) |
| `IPackagingStrategy` | `HybridStaticStrategy` / `PureDynamicStrategy` | Packaging model and core-library interpretation, resolved per runtime strategy in DI |
| `IDependencyPolicyValidator` | `HybridStaticValidator` / `PureDynamicValidator` | Strategy-aware closure validation (hybrid leak enforcement, pure-dynamic pass-through) |
| `ICoberturaReader` | `CoberturaReader` | Parses cobertura XML (MTP `--coverage --coverage-output-format cobertura`) into aggregate `CoverageMetrics` |
| `ICoverageBaselineReader` | `CoverageBaselineReader` | Loads `build/coverage-baseline.json` into `CoverageBaseline` (line / branch floor + optional metadata) |
| `ICoverageThresholdValidator` | `CoverageThresholdValidator` | Applies the ratchet rule to parsed metrics and returns a typed coverage result |
| `IVcpkgManifestReader` | `VcpkgManifestReader` | Loads `vcpkg.json` into `VcpkgManifest` for PreFlight and future build-host consumers |
| `INativeAcquisitionStrategy` | `VcpkgBuildProvider` | **(Planned)** Where native binaries come from |

## Reference Pattern: Harvesting First

When a build-host refactor needs precedent, compare the shape of the Harvesting module before inventing a new seam.

- `HarvestTask` keeps `BuildContext`, task-only policy, and user-facing failure behavior.
- `BinaryClosureWalker`, `ArtifactPlanner`, and `ArtifactDeployer` take narrower dependencies and explicit domain inputs.
- Service boundaries return typed domain results/errors instead of forcing exception-only flow everywhere.
- Rich domain models (`BinaryClosure`, `DeploymentPlan`, `DeploymentStatistics`) carry intent better than raw path collections.
- Tests mirror this split: whitebox module tests for the services, task tests for behavior and output contracts.

This is a reference pattern, not a claim that every line in Harvesting is perfect. The point is to copy the boundary discipline before copying any implementation detail.

Recent alignment examples:

- Coverage keeps file-path resolution in the task, parsing in readers, and the threshold rule in an injectable validator. The module stays intentionally small without letting the task own the core policy decision.
- PreFlight now uses typed validator result boundaries and a dedicated `IVcpkgManifestReader`, while the task retains user-facing reporting and Cake-facing failure policy.
- “Golden standard” in this repo means: copy Harvesting's architecture shape first, not its exact implementation details.

## Configuration Files

### manifest.json — Single Source of Truth (Schema v2.1)

All build configuration lives in a single file. Previously split across `manifest.json`, `runtimes.json`, and `system_artefacts.json` — now merged.

```json
{
  "schema_version": "2.1",
  "packaging_config": {
    "validation_mode": "strict",
    "core_library": "sdl2"
  },
  "runtimes": [
    { "rid": "win-x64", "triplet": "x64-windows-hybrid", "strategy": "hybrid-static", "runner": "windows-latest", "container_image": null }
  ],
  "package_families": [
    { "name": "core", "library_ref": "SDL2", "depends_on": [], "change_paths": ["src/SDL2.Core/**"] }
  ],
  "system_exclusions": {
    "windows": { "system_dlls": ["kernel32.dll", "user32.dll", "..."] },
    "linux": { "system_libraries": ["libc.so*", "libstdc++.so*", "..."] },
    "osx": { "system_libraries": ["libSystem.B.dylib", "Cocoa.framework", "..."] }
  },
  "library_manifests": [
    {
      "name": "SDL2",
      "vcpkg_name": "sdl2",
      "vcpkg_version": "2.32.10",
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

Key sections:

- `packaging_config`: Validation mode and core library identification
- `runtimes[]`: RID ↔ triplet ↔ strategy ↔ CI runner mapping. Triplet = strategy authority; the `strategy` field is a formal declaration validated by PreFlightCheck
- `package_families[]`: family metadata for packaging/release orchestration
- `system_exclusions`: OS-level libraries that must NOT be bundled (used by `RuntimeProfile.IsSystemFile()`)
- `library_manifests[]`: Library definitions with vcpkg name/version and binary patterns
- `core_lib`: If true, this library's binary appears in other packages too (SDL2.dll in Image, Mixer, etc.)

For the full merged schema, see [research/cake-strategy-implementation-brief-2026-04-14.md](../research/cake-strategy-implementation-brief-2026-04-14.md).

## Task Pipeline

### HarvestTask

The core task that collects native binaries for a specific library and RID.

**Arguments**:

- `--library`: Library name(s) from manifest.json (e.g., `SDL2`, `SDL2_image`)
- `--rid`: Runtime identifier (e.g., `win-x64`, `linux-x64`)

**Pipeline per library**:

```text
1. Load manifest.json entry for library
2. Resolve vcpkg install path for triplet
3. Find primary binary using manifest patterns
4. IBinaryClosureWalker: Recursively scan dependencies
   ├── Windows: dumpbin /dependents → filter system_artefacts
   ├── Linux: ldd → filter system_artefacts
   └── macOS: otool -L → filter system_artefacts
5. IArtifactPlanner: Build deployment plan
   ├── Classify each file (primary, dependency, system-excluded)
   ├── Determine deployment strategy (direct copy vs archive)
   └── Generate DeploymentPlan model
6. IArtifactDeployer: Execute deployment
   ├── Windows: Direct file copy to runtimes/{rid}/native/
   ├── Linux/macOS: Create tar.gz preserving symlinks
   └── Generate per-RID status JSON
```

**Output**:

- Status JSON: `artifacts/harvest_output/{Library}/rid-status/{rid}.json`
- Native payload: `artifacts/harvest_output/{Library}/runtimes/{rid}/native/`

### ConsolidateHarvestTask

Merges per-RID status files into library-wide manifests.

**Input**: All `{rid}.json` files from `artifacts/harvest_output/{Library}/rid-status/`
**Output**: `harvest-manifest.json` + `harvest-summary.json`

## Writing Build-Host Tests

### Hermetic Task Tests: `FakeRepoBuilder`

Task-level tests in `build/_build.Tests/` should model repo state through the Cake-native fake filesystem, not temp directories or `System.IO.File.*` calls.

Canonical entry point: `build/_build.Tests/Fixtures/FakeRepoBuilder.cs`

What it provides:

- Fake repo root + `FakeFileSystem` + `FakeEnvironment`
- Real `PathService` topology over fake paths, so tests exercise the same semantic path model as production code
- Fluent writers for common repo artifacts: `manifest.json`, `vcpkg.json`, coverage baseline, cobertura report, harvest RID status files
- Async read helpers on the returned handles for output assertions (`ReadAllTextAsync`)
- Optional `VcpkgInstalledFake` layout builder for future tests that need a fake `vcpkg_installed/<triplet>/...` tree

Pattern:

```csharp
var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
  .WithManifest(manifest)
  .WithVcpkgJson(vcpkgManifest)
  .BuildContextWithHandles();

var task = new PreFlightCheckTask(
  manifest,
  new VersionConsistencyValidator(),
  new StrategyCoherenceValidator(new StrategyResolver()),
  new PreflightReporter(repo.BuildContext));
task.Run(repo.BuildContext);

await Assert.That(repo.Exists("artifacts/harvest_output/SDL2/rid-status/win-x64.json")).IsTrue();
```

### Real-Repo Characterization Tests: `WorkspaceFiles`

Characterization tests that intentionally inspect committed repo files (`build/manifest.json`, `vcpkg.json`) should still avoid `System.IO.File.*` and `System.IO.Directory.*` directly.

Canonical entry point: `build/_build.Tests/Fixtures/WorkspaceFiles.cs`

Use it to:

- Resolve the workspace repo root from `AppContext.BaseDirectory`
- Read committed files through Cake's physical `FileSystem`
- Keep the "real repo contract" intent while preserving the build host's Cake-native I/O discipline

### Production I/O Rule For Testability

If a build-host task or helper must read or write repo files that task tests need to fake, route that I/O through Cake abstractions.

Current canonical helpers live in `build/_build/Context/CakeExtensions.cs`:

- `ToJson<TModel>()` / `ToJsonAsync<TModel>()`
- `ReadAllTextAsync()`
- `WriteAllTextAsync()`

If new production code reaches for `System.IO.File.*` directly, it will likely bypass `FakeFileSystem` and force tests back onto real disk. That is considered regression territory for the build-host test infra.

### PreFlightCheckTask

Validates configuration consistency before builds (partial gate).

**Checks**:

- manifest.json library versions match vcpkg.json override versions
- Port versions match
- Runtime strategy coherence (`runtimes[].strategy` vs triplet-derived model)

**Out of scope (deferred to Stream C):** package-family integrity, dynamic CI matrix gating, and CI artifact-flow checks.

## Binary Closure Walking

The most complex part of the build system. Each platform uses different tools:

### Windows (dumpbin)

```text
dumpbin /dependents SDL2.dll
→ Lists: SDL2.dll depends on kernel32.dll, user32.dll, vcruntime140.dll, ...
→ Filter system_artefacts.json
→ Recursively scan remaining dependencies
```

Resolution note (current implementation):

- `DumpbinTool` first checks `VCToolsInstallDir` (Developer PowerShell/Developer Command Prompt scenario)
- then falls back to `vswhere` with `Microsoft.VisualStudio.Component.VC.Tools.x86.x64`
- then probes MSVC Host/Target bin combinations for `dumpbin.exe`

### Linux (ldd)

```text
ldd libSDL2-2.0.so.0
→ Lists: libSDL2-2.0.so.0 => /usr/lib/x86_64-linux-gnu/libSDL2-2.0.so.0
→ Filter system_artefacts.json
→ Handle symlink chains (libSDL2.so → libSDL2-2.0.so.0 → libSDL2-2.0.so.0.3200.4)
→ Recursively scan remaining dependencies
```

### macOS (otool)

```text
otool -L libSDL2.dylib
→ Lists: @rpath/libSDL2.dylib, /usr/lib/libSystem.B.dylib, ...
→ Filter system_artefacts.json and framework references
→ Handle @rpath, @loader_path references
→ Recursively scan remaining dependencies
```

## Output Structure

```text
artifacts/
├── harvest_output/
│   ├── SDL2/
│   │   ├── rid-status/
│   │   │   ├── win-x64.json
│   │   │   ├── linux-x64.json
│   │   │   └── osx-arm64.json
│   │   ├── harvest-manifest.json    ← Generated by ConsolidateHarvestTask
│   │   └── harvest-summary.json     ← Human-readable summary
│   ├── SDL2_image/
│   │   └── rid-status/
│   │       └── ...
│   └── ...
└── packages/                         ← Future: PackageTask output
    └── ...
```

## Extending the Build System

### Adding a New Task

1. Create a new class in `Tasks/` inheriting from `FrostingTask<BuildContext>`
2. Add `[TaskName("YourTask")]` attribute
3. Add `[IsDependentOn(typeof(...))]` for dependencies
4. Register any new services in the DI module

### Adding a New Platform

1. Update `runtimes.json` with new RID → triplet mapping
2. Ensure `IBinaryClosureWalker` handles the new platform's dependency scanning tool
3. Update `system_artefacts.json` with the platform's system libraries
4. Add CI workflow for the new platform

## Historical Note

The original build plan has been retired after migration into the current docs set. The current implementation still follows that earlier harvest-pipeline direction, but CI/CD and packaging details have evolved and should now be read from the active docs instead.

For repo-specific tradeoffs and architecture-review carry-over, see [design-decisions-and-tradeoffs.md](design-decisions-and-tradeoffs.md). For general Cake Frosting working patterns trimmed from the deep reference, see [cake-frosting-patterns.md](../playbook/cake-frosting-patterns.md), [cake-frosting-host-organization.md](../playbook/cake-frosting-host-organization.md), and [cake-frosting-build-expertise.md](../reference/cake-frosting-build-expertise.md).
