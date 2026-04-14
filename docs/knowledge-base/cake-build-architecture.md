# Knowledge Base: Cake Frosting Build Architecture

> Deep technical reference for the Cake Frosting build system in `build/_build/`.

## Overview

The build system is a .NET 9.0 console application using **Cake Frosting v5.0.0**. It orchestrates the native binary harvesting pipeline — collecting compiled SDL2/SDL3 libraries and their transitive dependencies from vcpkg output and organizing them for NuGet packaging.

## Current Implementation Notes

- Active harvest logic lives under `Tasks/Harvest/`. `Tasks/Vcpkg/` currently contains empty legacy placeholder files.
- `PreFlightCheckTask` is implemented in the build host, but the release-candidate workflow does not invoke it yet.
- `PathService` already exposes `harvest-staging` helpers for future distributed CI, but current tasks and workflows still write to `artifacts/harvest_output/`.
- The build host exposes a `--use-overrides` CLI option, but override-based native sourcing is not wired into the current task pipeline.
- The build host still uses hand-written `OneOf` result wrappers. Source-generator-based cleanup remains a parked follow-up, not active build-system behavior.

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
│   ├── Preflight/          ← PreFlightCheckTask (version validation)
│   └── Vcpkg/              ← Stubs (empty, not used)
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
| `IPackagingStrategy` | `HybridStaticStrategy` / `PureDynamicStrategy` | **(Planned)** Packaging model, core library identification |
| `IDependencyPolicyValidator` | `HybridStaticValidator` / `PureDynamicValidator` | **(Planned)** Validates closure against strategy (leak detection) |
| `INativeAcquisitionStrategy` | `VcpkgBuildProvider` | **(Planned)** Where native binaries come from |

## Configuration Files

### manifest.json — Single Source of Truth (Schema v2)

All build configuration lives in a single file. Previously split across `manifest.json`, `runtimes.json`, and `system_artefacts.json` — now merged.

```json
{
  "schema_version": "2.0",
  "packaging_config": {
    "validation_mode": "strict",
    "core_library": "sdl2"
  },
  "runtimes": [
    { "rid": "win-x64", "triplet": "x64-windows-hybrid", "strategy": "hybrid-static", "runner": "windows-latest", "container_image": null }
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

### PreFlightCheckTask

Validates configuration consistency before builds.

**Checks**:

- manifest.json library versions match vcpkg.json override versions
- Port versions match
- All required fields present

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
