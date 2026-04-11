# Phase 3: SDL2 Complete

**Status**: PLANNED
**Depends on**: Phase 2 (CI/CD & Packaging)

## Objective

Bring all 6 SDL2 satellite libraries to production quality across all 7+ target RIDs, with tests, samples, and published NuGet packages.

## Scope

### 3.1 Full RID Population

Ensure all 6 libraries have successfully harvested native binaries for all 7 RIDs:

| Library | win-x64 | win-x86 | win-arm64 | linux-x64 | linux-arm64 | osx-x64 | osx-arm64 |
|---------|:-------:|:-------:|:---------:|:---------:|:-----------:|:-------:|:---------:|
| SDL2.Core | Target | Target | Target | Target | Target | Target | Target |
| SDL2.Image | Target | Target | Target | Target | Target | Target | Target |
| SDL2.Mixer | Target | Target | Target | Target | Target | Target | Target |
| SDL2.Ttf | Target | Target | Target | Target | Target | Target | Target |
| SDL2.Gfx | Target | Target | Target | Target | Target | Target | Target |
| SDL2.Net | Target | Target | Target | Target | Target | Target | Target |

### 3.2 Smoke Tests

Create a test project (`test/SDL2.SmokeTests/`) that verifies for each library:
- Native library loads successfully (`SDL_Init` / equivalent init call)
- Basic function calls work (e.g., `SDL_GetVersion`, `IMG_Init`, `Mix_Init`, `TTF_Init`)
- Clean shutdown works (`SDL_Quit` / equivalent cleanup)

These should run on CI for at least the platform the CI runner is on (e.g., linux-x64 on Ubuntu runners).

### 3.3 Sample Projects

Create sample applications in `samples/`:

| Sample | Libraries Used | Demonstrates |
|--------|---------------|-------------|
| `HelloWindow` | Core | Window creation, event loop, basic rendering |
| `ImageViewer` | Core, Image | Loading and displaying various image formats |
| `AudioPlayer` | Core, Mixer | Playing music and sound effects |
| `TextRenderer` | Core, Ttf | TrueType font rendering with Harfbuzz |
| `DrawingPrimitives` | Core, Gfx | Lines, circles, polygons, rotozoom |
| `SimpleChat` | Core, Net | Basic TCP/UDP networking |

### 3.4 Meta-Package

Create `Janset.SDL2` meta-package that depends on all 6 library packages. Users can `dotnet add package Janset.SDL2` to get everything.

### 3.5 NuGet Publishing

- Set up GitHub Packages or NuGet.org as package feed
- Publish pre-release versions (e.g., `2.32.10-preview.1`)
- Establish versioning scheme: native library version + build metadata
- Configure API keys and publish workflow

### 3.6 Documentation Polish

- README.md with badges, getting started, API overview
- Per-library README files in each src project
- Contributing guide (CONTRIBUTING.md)
- Changelog (CHANGELOG.md)

## Exit Criteria

- [ ] All 7 RIDs produce valid native packages for all 6 libraries
- [ ] Smoke tests pass on CI for at least one platform per OS
- [ ] At least 3 sample projects demonstrating different libraries
- [ ] Meta-package `Janset.SDL2` published
- [ ] Pre-release packages available on NuGet.org or GitHub Packages
- [ ] README.md polished for public consumption
- [ ] CONTRIBUTING.md written

## Open Questions

1. **Versioning scheme**: Should packages use SDL2's version (e.g., 2.32.10.x) or independent semantic versioning?
2. **Linux distro support**: Should we provide distro-specific RIDs (e.g., `ubuntu.20.04-x64`) or rely on generic `linux-x64`?
3. **buildTransitive targets**: Do all `.Native` packages need them, or only `SDL2.Core.Native`?
