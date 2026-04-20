# Release Lifecycle Patterns for .NET Monorepos with Native Packages

**Date:** 2026-04-14
**Author:** Claude Opus (research agent synthesis)
**Status:** Research — pending alignment decisions. **Note (2026-04-17):** post-dated by S1 adoption. The LibGit2Sharp exact-pin pattern described in this doc's industry survey (e.g., "Managed depends on its own Native with exact pin (`=`)", "completely different version schemes but exact-pinned: managed depends on `NativeBinaries (= 2.0.323)`") is **no longer the chosen pattern for Janset.SDL2**. S1 adoption (2026-04-17) aligned with the SkiaSharp `>=` pattern instead. The survey data remains accurate; the subsequent design inference has changed. See [phase-2-adaptation-plan.md "S1 Adoption Record"](../phases/phase-2-adaptation-plan.md). **Note (2026-04-18):** [ADR-001](../decisions/2026-04-18-versioning-d3seg.md) later locked D-3seg family versioning (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`) and the package-first consumer contract, so treat any pre-ADR version-shape or Source Mode implications here as historical context only.
**Issue context:** #85 (strategy awareness), #83 (packaging spike), #54 (PackageTask)

## Purpose

Comprehensive survey of how .NET monorepos with inter-dependent packages (especially those shipping native binaries) manage versioning, CI/CD matrix generation, and release flows. Conducted to inform Janset.SDL2's release lifecycle design before implementing PackageTask and CI dynamic matrix.

## Projects Surveyed

| Project | Packages | Native? | Team Size | Pattern |
|---|---|---|---|---|
| Azure SDK for .NET | 200+ | No | 100+ | Fully independent per-package |
| SkiaSharp | 49 (incl. 9 NativeAssets) | Yes | 5-10 | All-at-once locked |
| ppy/SDL3-CS | 4 | Yes (committed to git) | 3-5 | All-at-once locked, calendar version |
| LibGit2Sharp | 2 | Yes | 2-3 | Independent versions, historical exact pin |
| Magick.NET | 13 | Yes | 1-2 | Matrix split (quantum×arch), locked |
| Microsoft.Data.SqlClient | 3 | Yes (Windows SNI only) | 5+ | Independent versions, `>=` |
| NSec/libsodium | 2 | Yes (upstream-owned) | 1-2 | Independent, bounded range |
| OpenTK/GLFW | 6+ | Yes (GLFW only) | 3-5 | Independent, transitive |
| Avalonia UI | 30+ | Partial | 10-20 | Shared version, NUKE build |
| SixLabors/ImageSharp | 1 per repo | No | 2-3 | Per-repo, tag-based |
| aspnetcore (Microsoft.Extensions.*) | 50+ | No | 100+ | Shared version, Arcade/Darc |

## Release Pattern Taxonomy

### Pattern A: Fully Independent Per-Package (Azure SDK)

**How it works:**

- Each package declares its own `<Version>` in its `.csproj` file
- Per-service `ci.yml` with path-based triggers — only changes under `sdk/{service}/` trigger builds
- Dynamic matrix via JSON config + PowerShell generator (`Create-JobMatrix.ps1`)
- Release pipeline separate from CI, gated on `System.TeamProject = 'internal'`
- `eng/scripts/Update-PkgVersion.ps1` automates version bumps

**Version management:**

- Dev builds: automatic `alpha.yyyyMMdd.r` suffix (unique per CI build)
- Release builds: `SkipDevBuildNumber=true` preserves declared version
- `ApiCompatVersion` property tracks backward compatibility

**Strengths:** Maximum flexibility, per-package release cadence, mature tooling.
**Weaknesses:** Massive infrastructure overhead (custom PS scripts, matrix generator, dedicated release pipelines per service). Not viable for 1-2 person teams.

### Pattern B: All-at-Once Locked (SkiaSharp, ppy/SDL3-CS, Avalonia)

**How it works (SkiaSharp):**

- All 49 packages share the same version number (e.g., `3.119.2`)
- Version scheme: `major.skia-milestone.patch`
- Azure Pipelines with sequential stages: Prepare → Native Builds (parallel) → Native Merge → Managed → Package
- Native artifacts consolidated before managed build
- All packages always released together

**How it works (ppy/SDL3-CS):**

- 4 packages, all same version (`YYYY.MMDD.patch` calendar scheme)
- Version from `git describe --exact-match --tags HEAD` — no config file
- Native binaries committed to git (repo = 614 MB)
- `deploy.yml` packs all 4 packages on tag push, publishes all with glob
- Cannot release one library independently

**How it works (Avalonia):**

- ~30 packages, shared version via NUKE `BuildParameters`
- `numerge.json` for combining intermediate packages into final NuGet outputs
- API compatibility validation against baseline packages

**Strengths:** Simple mental model, guaranteed consistency, no version matrix to manage.
**Weaknesses:** Cannot release one library independently. All-or-nothing deployment.

### Pattern C: Per-Family Locked, Cross-Family Independent (LibGit2Sharp)

**How it works:**

- Two packages: `LibGit2Sharp` (managed, v0.31.0) + `LibGit2Sharp.NativeBinaries` (native, v2.0.323)
- Historical comparison: completely different version schemes but exact-pinned; managed depends on `NativeBinaries (= 2.0.323)`
- Native and managed in separate repositories
- Native released first when libgit2 upstream changes, managed released independently

**NSec/libsodium variant:**

- Bounded range constraint: `libsodium (>= 1.0.18 && < 1.0.19)`
- Allows patch-level native updates without touching managed package
- Different ownership entirely (upstream maintains native NuGet)

**SqlClient/SNI variant:**

- Independent versions with `>=` minimum constraint
- Native package marked "internal implementation — not for direct consumption"
- Conditional dependency per TFM (native only on Windows)

**Strengths:** Each "family" (managed + native) releases independently. Honest versioning reflects different change cadences.
**Weaknesses:** Must sequence releases (native first, then managed). More coordination overhead than all-at-once.

### Pattern D: Multi-Repo (SixLabors)

Not applicable for monorepo design. Included for completeness.

## Native + Managed Package Topology

### Fat Package (All RIDs in One nupkg)

Used by: LibGit2Sharp, NSec/libsodium, OpenTK/GLFW, ppy/SDL3-CS

```
runtimes/
  win-x64/native/libfoo.dll
  win-x86/native/libfoo.dll
  linux-x64/native/libfoo.so
  osx-arm64/native/libfoo.dylib
```

**Pro:** Fewest packages to manage. Users add one reference, get all platforms.
**Con:** Download size includes all platforms (~25-40 MB for SDL2-class libraries).

### Per-OS Split

Used by: SkiaSharp (9 NativeAssets packages)

```
SkiaSharp.NativeAssets.Win32   → win-x64, win-x86, win-arm64
SkiaSharp.NativeAssets.macOS   → osx (universal binary)
SkiaSharp.NativeAssets.Linux   → linux-x64, linux-arm64, etc.
```

**Pro:** Users only download their OS family. Linux is opt-in (deliberate — too many distro variants).
**Con:** 9 native packages for one library. For 6 libraries = 54 native packages. Too many.

### Per-RID Split

Used by: Microsoft.NETCore.App.Runtime (runtime itself)

**Pro:** Minimal download per consumer.
**Con:** Explosion in package count. Only viable for the .NET runtime team.

### Matrix Split

Used by: Magick.NET (quantum depth × architecture = 12 packages)

**Pro:** Fine-grained selection.
**Con:** Users must choose the right variant. Confusing.

## Version Constraint Patterns

| Pattern | Example | When To Use |
|---|---|---|
| **Historical exact pin** `=` | `LibGit2Sharp.NativeBinaries (= 2.0.323)` | Managed + native tightly coupled, tested together |
| **Minimum** `>=` | `SkiaSharp.NativeAssets.Win32 (>= 3.119.2)` | Forward-compatible native API |
| **Bounded range** `>= && <` | `libsodium (>= 1.0.18 && < 1.0.19)` | Patch-safe but ABI-break-aware |

## Versioning Tools for .NET Monorepos

### MinVer

- Tag-based: zero configuration beyond `<MinVerTagPrefix>`
- Monorepo support via per-project tag prefixes: `core-1.0.0`, `image-1.0.3`
- Automatic pre-release for untagged commits: `1.0.1-alpha.0.3`
- Simple, no external services

### Nerdbank.GitVersioning (NBGV)

- `version.json` per project with path-based version height filtering
- Branch-specific version policies
- More powerful but more complex than MinVer

### Manual `<Version>` in csproj (Azure SDK pattern)

- Explicit, no magic
- Requires scripted version bump automation
- Full control over version lifecycle

## CI Matrix Generation Patterns

### Hardcoded YAML (ppy, current Janset)

```yaml
matrix:
  include:
    - { triplet: x64-windows-release, rid: win-x64, runner: windows-latest }
```

**Problem:** Drifts from config files. Our orchestrator already has stale triplets.

### Dynamic from JSON (Azure SDK, recommended)

```yaml
jobs:
  generate:
    outputs:
      matrix: ${{ steps.gen.outputs.matrix }}
    steps:
      - run: # script reads manifest.json, outputs JSON matrix

  build:
    needs: generate
    strategy:
      matrix: ${{ fromJson(needs.generate.outputs.matrix) }}
```

**Best for:** Config-driven builds where manifest.json is source of truth.

### Template-based (SkiaSharp)

Platform-specific YAML templates imported into main pipeline. Matrix defined in templates.

## Change Detection

### dotnet-affected

- MSBuild-aware, respects `Directory.Packages.props`
- Exit code 166 = no changes (skip build)
- `dotnet affected --from origin/main --to HEAD --format json`

### Path-based (GitHub Actions)

```yaml
on:
  push:
    paths: ['src/SDL2.Core/**', 'src/native/SDL2.Core.Native/**']
```

Simple but coarse-grained. Does not understand transitive MSBuild dependencies.

### Nx `affected` (via @nx/dotnet)

- Experimental, requires Node.js
- No NuGet release support
- `dotnet-affected` covers the same ground without Nx overhead
- **Not recommended** for .NET-only monorepos

## Verdict: Recommended Pattern for Janset.SDL2

### Versioning: Pattern C — Per-Family Locked

- Each SDL2 library is a "family": managed + native packages, always same version
- Families version independently: Core at v1.2.0, Image at v1.0.3
- Historical pre-S1 pattern: managed depends on its own Native with exact pin (`=`)
- Satellites depend on Core with minimum constraint (`>=`)

### Native topology: Fat package (all RIDs in one nupkg)

- 6 managed + 6 native = 12 packages total
- Each `.Native` package contains 7 RIDs (~25 MB per package)
- Users reference managed package, native pulled transitively

### Versioning tool: MinVer with tag prefixes

- `core-1.0.0`, `image-1.0.3`, `mixer-1.1.0` tags
- `<MinVerTagPrefix>core-</MinVerTagPrefix>` in csproj
- Zero infrastructure beyond tags + one NuGet reference per project

### CI matrix: Dynamic from manifest.json

- Cake task or jq script reads `manifest.json`, emits GHA-compatible JSON
- Cross-product of `package_families × runtimes`
- Per-OS reusable workflows retained (prerequisites differ per OS)

### Release flow: Tag-triggered, sequential stages

```
Tag push (e.g., core-1.2.0)
  → Detect family from tag prefix
  → Native build per-RID (parallel)
  → Consolidate native artifacts
  → Build managed bindings
  → Pack NuGet (managed + native)
  → Publish to feed
```

### Change detection: Hybrid

- CI: path-based triggers from manifest `change_paths`
- Local/PR: `dotnet-affected` for precise MSBuild-aware detection
- Future: both feeding into matrix to skip unchanged families

### What NOT to use

| Tool | Why Not |
|---|---|
| Nx / @nx/dotnet | Experimental, Node.js dep, no NuGet release support |
| Arcade / Darc / Maestro | Designed for Microsoft's 50+ repo ecosystem |
| Release-Please | No native .NET/NuGet release type |
| NBGV | Overkill for tag-based releases |
| All-at-once versioning | Blocks per-library independent releases |
| Per-RID or per-OS native split | Package explosion (up to 54 packages) |

## Open Questions for Alignment

1. **Family versioning tool**: MinVer + tag prefix vs manual `<Version>` in csproj?
2. **Historical alignment question**: Exact pin (`=`) vs minimum (`>=`) between managed and its own native?
3. **manifest.json evolution**: Add `package_families` section (v2.1) vs full library-centric rewrite (v3.0)?
4. **Dev feed**: GitHub Packages vs Feedz.io vs local folder (for now)?
5. **CI migration order**: Pure-dynamic first (validate existing pipeline) → hybrid overlay → release pipeline?
6. **Inter-family dependency constraint**: `Janset.SDL2.Image` depends on `Janset.SDL2.Core (>= x.y.z)` — always latest or bounded range?

## Sources

- Azure SDK versioning: `doc/dev/Versioning.md`, `eng/Versioning.targets`, `eng/scripts/Update-PkgVersion.ps1`
- Azure SDK matrix generator: `eng/pipelines/templates/stages/platform-matrix.json`, `Create-JobMatrix.ps1`
- SkiaSharp versioning: github.com/mono/SkiaSharp/wiki/Versioning
- SkiaSharp pipeline: `azure-templates-stages.yml`, `azure-templates-merger.yml`
- ppy/SDL3-CS: `build.yml`, `deploy.yml`, `External/build.sh`
- LibGit2Sharp: NuGet dependency chain, github.com/libgit2/libgit2sharp.nativebinaries
- Magick.NET: NuGet package topology on nuget.org
- NSec/libsodium: NuGet bounded range constraint
- SqlClient/SNI: conditional TFM-based native dependency
- Avalonia: `nukebuild/Build.cs`, `numerge.json`
- aspnetcore: `eng/Versions.props`, `eng/Version.Details.xml`
- dotnet-affected: github.com/leonardochaia/dotnet-affected
- MinVer: github.com/adamralph/minver
- Nx .NET: nx.dev/docs/technologies/dotnet/introduction
