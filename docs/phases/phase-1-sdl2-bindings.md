# Phase 1: SDL2 Core Bindings + Harvesting

**Status**: DONE
**Period**: April 27 — June 1, 2025 (~5 weeks, 80 commits)

## Objective

Establish the foundational C# binding libraries for SDL2 and its satellites, build a cross-platform native binary harvesting pipeline, and prove the concept works end-to-end.

## What Was Accomplished

### C# Bindings

All five SDL2 satellite libraries were set up as modular .NET projects importing source files from the [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS) submodule:

| Project | Source File | Lines | Package ID |
|---------|-----------|-------|-----------|
| `src/SDL2.Core/` | `external/sdl2-cs/src/SDL2.cs` | 8,966 | Janset.SDL2.Core |
| `src/SDL2.Image/` | `external/sdl2-cs/src/SDL2_image.cs` | 316 | Janset.SDL2.Image |
| `src/SDL2.Mixer/` | `external/sdl2-cs/src/SDL2_mixer.cs` | 665 | Janset.SDL2.Mixer |
| `src/SDL2.Ttf/` | `external/sdl2-cs/src/SDL2_ttf.cs` | 768 | Janset.SDL2.Ttf |
| `src/SDL2.Gfx/` | `external/sdl2-cs/src/SDL2_gfx.cs` | 390 | Janset.SDL2.Gfx |

All target: `net9.0`, `net8.0`, `netstandard2.0`, `net462`.

### Native Package Projects

Five corresponding `.Native` projects were created under `src/native/`:

- `SDL2.Core.Native` — with `buildTransitive` MSBuild targets for .NET Framework
- `SDL2.Image.Native`
- `SDL2.Mixer.Native`
- `SDL2.Ttf.Native` — placeholder only (no binaries harvested)
- `SDL2.Gfx.Native`

Each follows the `runtimes/{rid}/native/` NuGet convention.

### Cake Frosting Build System

A comprehensive build system was created in `build/_build/`:

- **DI-based architecture**: Services registered via Cake modules (IPathService, IRuntimeProfile, IPackageInfoProvider, IBinaryClosureWalker, IArtifactPlanner, IArtifactDeployer)
- **HarvestTask**: Orchestrates the binary closure walk → artifact planning → deployment pipeline for each library
- **ConsolidateHarvestTask**: Merges per-RID status files into library-wide manifests
- **PreFlightCheckTask**: Validates version consistency between `manifest.json` and `vcpkg.json`
- **Platform-specific tools**: dumpbin (Windows), ldd (Linux), otool (macOS)

### Configuration Files

| File | Purpose |
|------|---------|
| `build/manifest.json` | Library definitions: 5 libraries with vcpkg names, versions, native package names, binary patterns |
| `build/runtimes.json` | 7 RIDs mapped to vcpkg triplets and CI runners |
| `build/system_artefacts.json` | OS library exclusion whitelist (kernel32, libc, Cocoa, etc.) |
| `vcpkg.json` | vcpkg dependencies with feature flags (SDL2 + SDL2_image only) |

### CI/CD Workflows

Four GitHub Actions workflows were created and tested:

1. **`prepare-native-assets-main.yml`** — Orchestrator workflow (manual trigger)
2. **`prepare-native-assets-windows.yml`** — Reusable workflow for Windows (x64, x86, arm64)
3. **`prepare-native-assets-linux.yml`** — Reusable workflow for Linux in containers (x64: ubuntu:20.04, arm64: ubuntu:24.04)
4. **`prepare-native-assets-macos.yml`** — Reusable workflow for macOS (x64: macos-15-intel, arm64: macos-latest)

A reusable composite action `vcpkg-setup` handles vcpkg bootstrap, binary caching, and dependency installation.

### Project Infrastructure

- `Directory.Build.props`: Multi-TFM, C# 13, 7 static analyzers, AOT/trimming support, Source Link
- `Directory.Packages.props`: Central package version management
- `.editorconfig`: Comprehensive code style rules
- `Janset.SDL2.sln`: Solution with all projects organized in folders

## Key Technical Decisions Made

1. **SDL2-CS import, not fork**: Binding source files are `<Compile Include="...">` from the submodule, not copied. This preserves upstream attribution and makes updates easier.

2. **Separate .Native packages**: Following SkiaSharp/LibGit2Sharp pattern. Managed bindings reference their native counterpart via `ProjectReference`.

3. **Three-tier dependency resolution**: Runtime analysis (binary closure walking) → package metadata (vcpkg queries) → overrides (system_artefacts.json exclusions).

4. **tar.gz for Unix symlinks**: Linux/macOS natives are archived to preserve symlink chains that NuGet's ZIP format destroys. Extracted at build time via MSBuild targets.

5. **Per-RID status files**: Each harvest run produces a `{rid}.json` status file, enabling distributed CI where each platform job runs independently and results are consolidated later.

## Exit Criteria (All Met)

- [x] C# bindings compile for all 5 libraries across all target frameworks
- [x] Cake Frosting build system functional with DI architecture
- [x] HarvestTask produces correct per-RID output for at least Windows and Linux
- [x] CI workflows successfully build native assets on all 3 platforms
- [x] ConsolidateHarvestTask merges results correctly
- [x] Project structure and metadata ready for NuGet packaging

## Lessons Learned

1. **Linux dependency scanning is complex**: `ldd` output varies by distro and container. The `system_artefacts.json` whitelist was essential to avoid bundling OS libraries.
2. **macOS universal binaries**: otool analysis needed special handling for fat/universal Mach-O binaries.
3. **vcpkg binary caching**: Critical for CI performance. Without it, each run rebuilds everything from source (~15-30 minutes per triplet).
4. **Symlink chains are real**: A typical Linux SDL2 install has `libSDL2.so → libSDL2-2.0.so.0 → libSDL2-2.0.so.0.3200.4`. All three must be preserved for runtime linking to work.

## What Was NOT Done (Deferred to Later Phases)

- vcpkg.json only covers SDL2 + SDL2_image (mixer, ttf, gfx, net deferred)
- No Cake PackageTask (harvest → .nupkg)
- No NuGet publishing
- No tests beyond build verification
- No sample projects
- Release candidate pipeline only stubbed out
