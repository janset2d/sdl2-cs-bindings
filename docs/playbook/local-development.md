# Playbook: Local Development Setup

> How to clone, build, and develop Janset.SDL2 on your local machine.
>
> **Status 2026-04-26.** `SetupLocalDev --source=local` is the canonical fresh-clone path (package-first consumer contract per [ADR-001](../decisions/2026-04-18-versioning-d3seg.md) §2.8). `--source=remote` is also operational (PD-5 closure, 2026-04-26) — pulls latest published nupkgs from the GitHub Packages internal feed and exercises the same consumer surface. `--source=release` (public NuGet.org) stays stubbed pending Phase 2b PD-7. Manual vcpkg + Harvest + Package flow below stays as a fallback/debug route.

## Quick Start (recommended)

The fresh-clone flow is a single command:

```bash
# Clone with submodules
git clone --recursive https://github.com/janset2d/sdl2-cs-bindings.git
cd sdl2-cs-bindings

# One-shot: vcpkg bootstrap + install (host triplet) + Harvest + Consolidate + Package + write local override
dotnet run --project build/_build -- --target SetupLocalDev --source=local
```

Result: `artifacts/packages/` populated with D-3seg-versioned prerelease nupkgs (e.g. `Janset.SDL2.Core 2.32.0-local.<timestamp>`), and `build/msbuild/Janset.Local.props` + `artifacts/resolve-versions/versions.json` written (both gitignored). Opening any smoke / sample csproj in Rider / VS / VS Code then restores + builds directly from the local feed.

`--source=release` is accepted but intentionally fails with a "not implemented" error until Phase 2b PD-7 wires the public-feed promotion path.

If you need to debug the internals manually, follow the "Full Build" fallback sequence below.

### Alternative — `--source=remote` (test against published internal feed)

When you want to validate your consumer code against the **last published wave** on GitHub Packages without packing locally, use `--source=remote`. This skips vcpkg / Harvest / Pack entirely and downloads the latest published managed + native nupkg per family:

```bash
# One-time: set GH_TOKEN to a Classic PAT with read:packages scope.
# (See "Environment Variables" below for the full setup recipe.)

dotnet run --project build/_build -- --target SetupLocalDev --source=remote
```

Result: `artifacts/packages/` is wiped, then populated with the latest published version of every concrete family from `https://nuget.pkg.github.com/janset2d/index.json`. `Janset.Local.props` + `versions.json` get the same shape as `--source=local`, so smoke / sample csprojs restore from the pulled feed identically.

`--source=remote` is the right tool for "did the latest CI publish actually work end-to-end as a consumer would see it?" — exactly what `tests/scripts/smoke-witness.cs remote` automates (see [tests/scripts/README.md](../../tests/scripts/README.md)).

## Prerequisites

| Tool | Version | Purpose |
| --- | --- | --- |
| .NET SDK | 9.0.x | Building managed projects and Cake build system |
| Git | 2.30+ | Submodule support |
| Visual Studio / Rider / VS Code | Any | IDE (optional but recommended) |

### Platform-Specific

| Platform | Additional Requirements |
| --- | --- |
| Windows | Visual Studio Build Tools 2022 with C++ tools (`Microsoft.VisualStudio.Component.VC.Tools.x86.x64`). Since Slice CA (2026-04-21) the Cake host self-sources the MSVC environment via `IMsvcDevEnvironment` (VSWhere + `vcvarsall.bat`); plain PowerShell is sufficient. Developer PowerShell is still fine — the resolver's fast-path skips the extra `cmd /c` spawn when `VCToolsInstallDir` is already set. |
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

Important: smoke projects remain in `Janset.SDL2.sln` by design (package-first consumer contract). If `build/msbuild/Janset.Local.props` does not exist yet (or is stale), `dotnet restore/build Janset.SDL2.sln` can fail on smoke package restore (`NU1101`). Re-run `SetupLocalDev --source=local` to regenerate the local override.

### Getting Native Binaries Without Building

For local testing with native binaries, you have two options:

#### Option A: Download CI artifacts

1. Go to GitHub Actions → `Release` workflow → trigger via `workflow_dispatch` (or use a previously-completed run on a tag push).
2. Download the per-RID `harvest-output-<rid>` artifact for your platform (for example: `harvest-output-win-x64`). Each carries `artifacts/harvest_output/<Library>/` for every library that ran on that RID.
3. Extract under your local `artifacts/harvest_output/` so the per-library subtrees land at the expected path.
4. The packed `.nupkg` files are also available as the `nupkg-output` artifact from the same run; you can drop them into a local feed and consume via `Janset.Local.props` instead of harvesting + packing locally.

The retired `prepare-native-assets-*.yml` family was deleted 2026-04-25 (P8.1) — its harvest discipline lives inside `release.yml`'s `harvest` matrix job now. See [ci-cd-packaging-and-release-plan.md](../knowledge-base/ci-cd-packaging-and-release-plan.md) for the live job topology.

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
# From repo root
# Windows:
dotnet run --project build/_build -- --target Harvest --library SDL2 --library SDL2_image --library SDL2_mixer --library SDL2_ttf --library SDL2_gfx --rid win-x64

# Linux:
dotnet run --project build/_build -- --target Harvest --library SDL2 --library SDL2_image --library SDL2_mixer --library SDL2_ttf --library SDL2_gfx --rid linux-x64

# macOS:
dotnet run --project build/_build -- --target Harvest --library SDL2 --library SDL2_image --library SDL2_mixer --library SDL2_ttf --library SDL2_gfx --rid osx-arm64
```

> SDL2_net was removed from `manifest.json` 2026-04-22 (commit `bc652d1`) — re-add when the binding + native csproj skeleton lands per [#58](https://github.com/janset2d/sdl2-cs-bindings/issues/58). Until then, `--library SDL2_net` fails with `library not found in manifest`.

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
  --explicit-version sdl2-core=2.32.0-local.1 \
  --explicit-version sdl2-image=2.8.0-local.1 \
  --explicit-version sdl2-mixer=2.8.0-local.1 \
  --explicit-version sdl2-ttf=2.24.0-local.1 \
  --explicit-version sdl2-gfx=1.0.0-local.1
```

Each `--explicit-version <family>=<semver>` entry must follow D-3seg (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>` per family — anchored to the family's `library_manifests[].vcpkg_version` via G54). The `--family` / `--family-version` legacy flags retired in Slice B1 (PD-13 closure 2026-04-22); CLI surface is now provider-strict via `ExplicitVersionProvider`. For a stable release you would tag the commit (`git tag sdl2-core-2.32.0`) and let `GitTagVersionProvider` resolve the mapping from the tag — see [ADR-003 §3.1](../decisions/2026-04-20-release-lifecycle-orchestration.md) for the version-source provider architecture.

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
| `GH_TOKEN` | GitHub Packages auth for `SetupLocalDev --source=remote`. Falls back to `GITHUB_TOKEN` if unset. | Not set (see "GitHub Packages auth" below) |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | Disable .NET telemetry | Not set |

### GitHub Packages auth (`--source=remote`)

The `RemoteArtifactSourceResolver` reads `GH_TOKEN` (then `GITHUB_TOKEN`) from the environment. GitHub Packages NuGet feed **always requires authentication**, even for public packages — anonymous read is not supported on the NuGet/npm/Maven registries (only `ghcr.io` containers allow anonymous public pulls). This is by-design GitHub behavior, not a misconfiguration; making packages public only changes the ACL applied after authentication, not the auth requirement itself.

**Token requirements:**

- **Classic PAT** (fine-grained PATs are not supported by GH Packages NuGet — documented limitation).
- Scope: `read:packages` (for `--source=remote`); add `write:packages` if you ever need the PD-8 manual escape hatch (operator-driven publish).
- SSO: if your org enforces SAML SSO, authorize the PAT for the `janset2d` org after creating it.

**Setup recipes:**

```bash
# Recipe A — gh CLI scope refresh (interactive browser auth, one-time)
gh auth refresh -h github.com -s read:packages
export GH_TOKEN=$(gh auth token)
dotnet run --project build/_build -- --target SetupLocalDev --source=remote

# Recipe B — dedicated PAT (explicit, easier to rotate)
# 1. github.com/settings/tokens/new → Classic → scope: read:packages → Generate
# 2. Persist via shell rc or per-platform env.

# zsh (default on WSL Ubuntu + recent macOS — prefer this on WSL):
export GH_TOKEN=ghp_yourtokenhere
echo 'export GH_TOKEN=ghp_yourtokenhere' >> ~/.zshrc

# bash (older distros / users who haven't switched):
export GH_TOKEN=ghp_yourtokenhere
echo 'export GH_TOKEN=ghp_yourtokenhere' >> ~/.bashrc

# Windows (PowerShell, persistent User-scope):
[Environment]::SetEnvironmentVariable('GH_TOKEN', 'ghp_yourtokenhere', 'User')
# Then restart shells so the new env is inherited.
```

**CI does not need this** — `release.yml`'s `publish-staging` job maps `${{ secrets.GITHUB_TOKEN }}` into `GH_TOKEN` automatically; that token is scoped + short-lived per workflow run.

**External-consumer note**: this auth is for **internal feed access** (CI-staged prereleases). External consumers of Janset.SDL2 packages will go through nuget.org once the public-feed promotion path lands (Phase 2b PD-7); they will not need a PAT at all. The `--source=remote` flow is a developer/maintainer-side dogfooding tool, not the external-consumer path.

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
- Plain PowerShell is fine post-Slice-CA — `DumpbinTool` + `IMsvcDevEnvironment` locate `dumpbin.exe` + source `vcvarsall.bat` via VSWhere automatically. Developer PowerShell is still supported (resolver's `VCToolsInstallDir` fast path).
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
