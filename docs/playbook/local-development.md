# Playbook: Local Development Setup

> How to clone, build, and develop Janset.SDL2 on your local machine.
>
> **Status 2026-04-19 (ADR-001 transition).** `SetupLocalDev --source=local` is now the canonical fresh-clone path (package-first consumer contract per [ADR-001](../decisions/2026-04-18-versioning-d3seg.md) §2.8). `--source=remote` / `--source=release` are accepted profile stubs for Phase 2b. The manual vcpkg + Harvest + Package flow below stays as a fallback/debug route.

## Quick Start (recommended)

The fresh-clone flow is a single command:

```bash
# Clone with submodules
git clone --recursive https://github.com/janset2d/sdl2-cs-bindings.git
cd sdl2-cs-bindings

# One-shot: vcpkg bootstrap + install (host triplet) + Harvest + Consolidate + Package + write local override
dotnet run --project build/_build -- --target SetupLocalDev --source=local
```

Result: `artifacts/packages/` populated with D-3seg-versioned prerelease nupkgs (e.g. `Janset.SDL2.Core 2.32.0-local.<timestamp>`), and `build/msbuild/Janset.Smoke.local.props` written (gitignored) with the matching `LocalPackageFeed` + per-family version properties. Opening any smoke / sample csproj in Rider / VS / VS Code then restores + builds directly from the local feed.

`--source=remote` / `--source=release` are accepted now but intentionally fail with a "not implemented in Phase 2a" error until feed-download profile work lands.

If you need to debug the internals manually, follow the "Full Build" fallback sequence below.

## Prerequisites

| Tool | Version | Purpose |
| --- | --- | --- |
| .NET SDK | 9.0.x | Building managed projects and Cake build system |
| Git | 2.30+ | Submodule support |
| Visual Studio / Rider / VS Code | Any | IDE (optional but recommended) |

### Platform-Specific

| Platform | Additional Requirements |
| --- | --- |
| Windows | Visual Studio Build Tools 2022 with C++ tools (`Microsoft.VisualStudio.Component.VC.Tools.x86.x64`); Developer PowerShell/Developer Command Prompt recommended |
| Linux | `build-essential`, `cmake`, `pkg-config`, `ldd`, plus SDL2 dev dependencies (see CI workflow for full list) |
| macOS | Xcode Command Line Tools, `autoconf`, `automake`, `libtool` (via Homebrew) |

## Quick Start — Managed Only (No Native Builds)

If you just want to work on C# bindings without building native libraries:

```bash
# Clone with submodules
git clone --recursive https://github.com/janset2d/sdl2-cs-bindings.git
cd sdl2-cs-bindings

# Build managed binding projects directly
dotnet build src/SDL2.Core/SDL2.Core.csproj
dotnet build src/SDL2.Image/SDL2.Image.csproj
dotnet build src/SDL2.Mixer/SDL2.Mixer.csproj
dotnet build src/SDL2.Ttf/SDL2.Ttf.csproj
dotnet build src/SDL2.Gfx/SDL2.Gfx.csproj
```

This builds the C# binding projects without requiring local package-feed injection.

Important: smoke projects remain in `Janset.SDL2.sln` by design (package-first consumer contract). If `build/msbuild/Janset.Smoke.local.props` does not exist yet (or is stale), `dotnet restore/build Janset.SDL2.sln` can fail on smoke package restore (`NU1101`). Re-run `SetupLocalDev --source=local` to regenerate the local override.

### Getting Native Binaries Without Building

For local testing with native binaries, you have two options:

#### Option A: Download CI artifacts

1. Go to GitHub Actions → `prepare-native-assets-main` workflow
2. Download the artifact for your platform. Current artifact names follow `harvested-assets-{platform}-{rid}` (for example: `harvested-assets-windows-win-x64`).
3. Extract and merge the artifact so the harvested library directories land under `artifacts/harvest_output/`.
4. Copy the harvested native payload from `artifacts/harvest_output/{Library}/runtimes/{rid}/native/` into the matching `src/native/{Library}.Native/runtimes/{rid}/native/` directory.

Current status: the `prepare-native-assets-*` workflows now target the full SDL2 satellite set with explicit RID. Expanded matrix validation is still pending.

#### Option B: Install SDL2 system-wide

- **Windows**: Download SDL2 development libraries from [libsdl.org](https://github.com/libsdl-org/SDL/releases) and put DLLs in your PATH
- **Linux**: `sudo apt install libsdl2-dev libsdl2-image-dev libsdl2-mixer-dev libsdl2-ttf-dev libsdl2-gfx-dev libsdl2-net-dev`
- **macOS**: `brew install sdl2 sdl2_image sdl2_mixer sdl2_ttf sdl2_gfx sdl2_net`

With system-wide install, `DllImport("SDL2")` will find the library via standard OS search paths.

## Full Build — With Native Libraries

### Step 1: Bootstrap vcpkg

```bash
# vcpkg is a git submodule
git submodule update --init --recursive

# Bootstrap vcpkg
# Windows:
external/vcpkg/bootstrap-vcpkg.bat

# Linux/macOS:
./external/vcpkg/bootstrap-vcpkg.sh
```

### Step 2: Install Native Dependencies via vcpkg

Use the hybrid-static overlay triplet for your platform (all 7 RIDs now ship under hybrid-static per PA-2, 2026-04-18):

```bash
# Windows x64:
./external/vcpkg/vcpkg install --triplet x64-windows-hybrid --overlay-triplets=vcpkg-overlay-triplets

# Linux x64:
./external/vcpkg/vcpkg install --triplet x64-linux-hybrid --overlay-triplets=vcpkg-overlay-triplets

# macOS arm64 (Apple Silicon):
./external/vcpkg/vcpkg install --triplet arm64-osx-hybrid --overlay-triplets=vcpkg-overlay-triplets
```

> **Retired triplets (pre-2026-04-14):** `x64-windows-release`, `x64-linux-dynamic`, `arm64-osx-dynamic`, etc. Do not use — the hybrid overlay triplets replace them. See [overlay-management.md](overlay-management.md) and [vcpkg-update.md](vcpkg-update.md).

This compiles SDL2 and its dependencies from source. First run takes 15-30 minutes; subsequent runs use binary caching.

### Step 3: Build Cake Frosting Host

```bash
cd build/_build
dotnet build
```

### Step 4: Run Harvest

```bash
# From build/_build directory
# Windows:
dotnet run -- --target Harvest --library SDL2 --library SDL2_image --library SDL2_mixer --library SDL2_ttf --library SDL2_gfx --library SDL2_net --rid win-x64

# Linux:
dotnet run -- --target Harvest --library SDL2 --library SDL2_image --library SDL2_mixer --library SDL2_ttf --library SDL2_gfx --library SDL2_net --rid linux-x64

# macOS:
dotnet run -- --target Harvest --library SDL2 --library SDL2_image --library SDL2_mixer --library SDL2_ttf --library SDL2_gfx --library SDL2_net --rid osx-arm64
```

Harvest writes two kinds of output:

- Status JSON: `artifacts/harvest_output/{Library}/rid-status/{rid}.json`
- Native payload: `artifacts/harvest_output/{Library}/runtimes/{rid}/native/`

If you also want the consolidated `harvest-manifest.json` and `harvest-summary.json` files, run:

```bash
dotnet run -- --target ConsolidateHarvest
```

### Step 5: Pack Families (or let `Package` task handle it)

With Harvest + ConsolidateHarvest green, produce the per-family nupkgs via the Cake `Package` task. Version string MUST follow [D-3seg](../decisions/2026-04-18-versioning-d3seg.md) (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>` with prerelease suffix for local iterations):

```bash
dotnet run --project build/_build -- --target Package \
  --family sdl2-core --family sdl2-image --family sdl2-mixer --family sdl2-ttf --family sdl2-gfx \
  --family-version 2.32.0-local.1
```

The `--family-version` flag is a **bootstrap override** accepted by `PackageVersionResolver` — it bypasses MinVer when no git tag exists locally. For a stable release you would instead tag the commit (`git tag sdl2-core-2.32.0`) and let MinVer resolve the version from the tag.

> **Manual per-satellite copy is retired.** The pre-2026-04-18 "copy `artifacts/harvest_output/.../runtimes/<rid>/native/*` into `src/native/<Lib>.Native/runtimes/<rid>/native/`" step no longer applies — the native csproj packs from `$(NativePayloadSource)` (handed in by Cake, `artifacts/harvest_output/<Lib>/` root), never from the `src/` tree. Guardrail G46 hard-fails direct `dotnet pack` of a `.Native` csproj without `$(NativePayloadSource)`.

## Project Structure for Development

```text
Janset.SDL2.sln
├── src/SDL2.Core         ← Work on bindings here
├── src/SDL2.Image        ← Each imports from external/sdl2-cs/src/
├── src/SDL2.Mixer
├── src/SDL2.Ttf
├── src/SDL2.Gfx
├── src/native/*          ← Native package projects (binary containers)
├── build/_build          ← Cake Frosting (build system development)
├── test/                 ← Test projects
└── samples/              ← Sample applications
```

## Working with the Cake Build System

The build system lives in `build/_build/` and is a regular .NET console application using Cake Frosting.

```bash
# Show available tasks
cd build/_build
dotnet run -- --tree

# Run a specific task
dotnet run -- --target Info              # Show environment info
dotnet run -- --target PreFlightCheck    # Partial gate: version + runtime strategy coherence
dotnet run -- --target Harvest --library SDL2 --rid win-x64
dotnet run -- --target ConsolidateHarvest

# Control verbosity
dotnet run -- --target Harvest --verbosity Diagnostic
```

## Environment Variables

| Variable | Purpose | Default |
| --- | --- | --- |
| `VCPKG_ROOT` | vcpkg installation path | `external/vcpkg/` |
| `VCPKG_DEFAULT_TRIPLET` | Default build triplet | Auto-detected from OS |
| `DOTNET_ROOT` | .NET runtime root (required on WSL/macOS when dotnet is not in standard system paths) | Not set (see [cross-platform smoke validation](cross-platform-smoke-validation.md#per-platform-environment-setup)) |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | Disable .NET telemetry | Not set |

## Common Tasks

### Updating a Binding Source

The binding source files come from `external/sdl2-cs/`. If you need to update:

```bash
cd external/sdl2-cs
git pull origin master
cd ../..
git add external/sdl2-cs
git commit -m "chore: update SDL2-CS submodule"
```

### Testing a Change

Fast verification loop:

```bash
# Build-host test suite (TUnit)
dotnet test build/_build.Tests/Build.Tests.csproj -c Release

# Build everything
dotnet build Janset.SDL2.sln

# Or build a specific project
dotnet build src/SDL2.Core/SDL2.Core.csproj
```

### Creating a Local NuGet Package (Manual)

**Prefer the Cake `Package` task** (see Step 5 in the Full Build flow above) — it handles version injection, `$(NativePayloadSource)` staging, and post-pack validation (G21–G27, G46–G48, G51–G57) in one invocation. Direct `dotnet pack` on the native csproj will hard-fail G46 (empty native payload guard).

Direct `dotnet pack` on a managed csproj (e.g. for a quick isolated pack test) is allowed but requires a D-3seg-shaped version. Example:

```bash
# D-3seg-shaped version: <UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>[-suffix]
dotnet pack src/SDL2.Core/SDL2.Core.csproj -o ./artifacts/packages/ -p:PackageVersion=2.32.0-local.1
```

> Versions like `0.1.0-local.1` or `1.0.0-local.1` violate D-3seg (UpstreamMajor.UpstreamMinor not anchored to SDL2 2.32.x). G54 will reject them at PreFlight. See [ADR-001 §2.1](../decisions/2026-04-18-versioning-d3seg.md).

## Troubleshooting

### "DllNotFoundException: SDL2"

Native binaries are not in the runtime path. Either:

- Install SDL2 system-wide (see Quick Start above)
- Copy binaries to your project's output directory
- Download CI artifacts

### "dumpbin.exe not found" on Windows

The build host's Windows dependency scanner expects Visual Studio C++ tooling.

- Install Visual Studio Build Tools 2022 with `Microsoft.VisualStudio.Component.VC.Tools.x86.x64`
- Prefer running local checks from Developer PowerShell (this sets `VCToolsInstallDir`)
- If needed, verify `VCToolsInstallDir` and confirm `dumpbin.exe` exists under `%VCToolsInstallDir%\bin\Hostx64\x64`
- If `vswhere.exe` is missing, install/repair Visual Studio Installer components before retrying

### vcpkg install fails

- Ensure submodules are initialized: `git submodule update --init --recursive`
- On Linux, install build dependencies: `sudo apt install build-essential cmake pkg-config`
- Check disk space — vcpkg builds can use several GB

### Cake build fails

- Ensure .NET 9.0 SDK is installed: `dotnet --version`
- Try cleaning: `cd build/_build && dotnet clean && dotnet build`
- Check `--verbosity Diagnostic` output for details

## Cross-Platform Validation

After significant build-host changes, run the full smoke matrix across Windows, WSL/Linux, and macOS to verify cross-platform correctness. See [cross-platform-smoke-validation.md](cross-platform-smoke-validation.md) for the complete matrix definition, per-platform environment setup, and command reference.
