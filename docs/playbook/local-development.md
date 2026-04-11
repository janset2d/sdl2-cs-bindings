# Playbook: Local Development Setup

> How to clone, build, and develop Janset.SDL2 on your local machine.

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 9.0.x | Building managed projects and Cake build system |
| Git | 2.30+ | Submodule support |
| Visual Studio / Rider / VS Code | Any | IDE (optional but recommended) |

### Platform-Specific

| Platform | Additional Requirements |
|----------|----------------------|
| Windows | Visual Studio Build Tools (for `dumpbin.exe`), or Windows SDK |
| Linux | `build-essential`, `cmake`, `pkg-config`, `ldd`, plus SDL2 dev dependencies (see CI workflow for full list) |
| macOS | Xcode Command Line Tools, `autoconf`, `automake`, `libtool` (via Homebrew) |

## Quick Start — Managed Only (No Native Builds)

If you just want to work on C# bindings without building native libraries:

```bash
# Clone with submodules
git clone --recursive https://github.com/janset2d/sdl2-cs-bindings.git
cd sdl2-cs-bindings

# Restore and build managed projects
dotnet restore Janset.SDL2.sln
dotnet build Janset.SDL2.sln
```

This builds all C# binding projects. Native packages will be empty (no binary files), but the managed code compiles fine.

### Getting Native Binaries Without Building

For local testing with native binaries, you have two options:

**Option A: Download CI artifacts**
1. Go to GitHub Actions → `prepare-native-assets-main` workflow
2. Download the artifact for your platform (e.g., `harvest-output-win-x64`)
3. Extract into `artifacts/harvest_output/`
4. Copy binaries to the appropriate `src/native/{Library}.Native/runtimes/{rid}/native/` directory

**Option B: Install SDL2 system-wide**
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

```bash
# Install for your platform's triplet
# Windows x64:
./external/vcpkg/vcpkg install --triplet x64-windows-release

# Linux x64:
./external/vcpkg/vcpkg install --triplet x64-linux-dynamic

# macOS arm64 (Apple Silicon):
./external/vcpkg/vcpkg install --triplet arm64-osx-dynamic
```

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
dotnet run -- --target Harvest --library SDL2 --library SDL2_image --rid win-x64

# Linux:
dotnet run -- --target Harvest --library SDL2 --library SDL2_image --rid linux-x64

# macOS:
dotnet run -- --target Harvest --library SDL2 --library SDL2_image --rid osx-arm64
```

Harvested binaries appear in `artifacts/harvest_output/{Library}/rid-status/{rid}.json`.

### Step 5: Copy to Native Projects (Manual)

Until the PackageTask is implemented, copy harvested binaries manually:

```bash
# Example for SDL2 Core on Windows x64
cp artifacts/harvest_output/SDL2/win-x64/native/* src/native/SDL2.Core.Native/runtimes/win-x64/native/
```

## Project Structure for Development

```
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
dotnet run -- --showtree

# Run a specific task
dotnet run -- --target InfoTask          # Show environment info
dotnet run -- --target PreFlightCheck    # Validate version consistency
dotnet run -- --target Harvest --library SDL2 --rid win-x64
dotnet run -- --target ConsolidateHarvest

# Control verbosity
dotnet run -- --target Harvest --verbosity Diagnostic
```

## Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `VCPKG_ROOT` | vcpkg installation path | `external/vcpkg/` |
| `VCPKG_DEFAULT_TRIPLET` | Default build triplet | Auto-detected from OS |
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

Until we have proper tests, the quickest way to verify changes:

```bash
# Build everything
dotnet build Janset.SDL2.sln

# Or build a specific project
dotnet build src/SDL2.Core/SDL2.Core.csproj
```

### Creating a Local NuGet Package (Manual)

```bash
# Pack a specific project
dotnet pack src/SDL2.Core/SDL2.Core.csproj -o ./artifacts/packages/

# Pack with version
dotnet pack src/SDL2.Core/SDL2.Core.csproj -o ./artifacts/packages/ /p:PackageVersion=2.32.4-local.1
```

## Troubleshooting

### "DllNotFoundException: SDL2"
Native binaries are not in the runtime path. Either:
- Install SDL2 system-wide (see Quick Start above)
- Copy binaries to your project's output directory
- Download CI artifacts

### vcpkg install fails
- Ensure submodules are initialized: `git submodule update --init --recursive`
- On Linux, install build dependencies: `sudo apt install build-essential cmake pkg-config`
- Check disk space — vcpkg builds can use several GB

### Cake build fails
- Ensure .NET 9.0 SDK is installed: `dotnet --version`
- Try cleaning: `cd build/_build && dotnet clean && dotnet build`
- Check `--verbosity Diagnostic` output for details
