# Janset.SDL2 / Janset.SDL3 — Modular C# Bindings with Native Libraries

**Modern, modular C# bindings for SDL2 and SDL3, bundled with cross-platform native libraries built from source via vcpkg.**

This project provides comprehensive C# bindings for SDL2 (and upcoming SDL3) along with their satellite libraries (SDL_image, SDL_mixer, SDL_ttf, SDL_gfx, SDL_net), distributed as NuGet packages with pre-compiled native binaries for Windows, Linux, and macOS.

[![Release](https://github.com/janset2d/sdl2-cs-bindings/actions/workflows/release.yml/badge.svg?branch=master)](https://github.com/janset2d/sdl2-cs-bindings/actions/workflows/release.yml)

## Why This Project?

The .NET SDL ecosystem has a gap. Existing binding projects provide C# P/Invoke declarations but expect you to source native libraries yourself. This project is different:

1. **C# Bindings** — Currently based on [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS), with auto-generated bindings planned
2. **Native libraries built from source** — Reproducible builds via [vcpkg](https://github.com/microsoft/vcpkg) with explicit feature flags
3. **Cross-platform NuGet packages** — Proper `runtimes/{rid}/native/` layout for 7+ platforms
4. **Symlink preservation** — tar.gz archives for Linux/macOS with MSBuild extraction targets

**No other project in the ecosystem does all four.**

## Packages

### SDL2 (Available Now)

| Package | Description | Status |
| --- | --- | --- |
| `Janset.SDL2.Core` | Core SDL2 bindings (windowing, input, events, rendering) | Functional |
| `Janset.SDL2.Image` | SDL2_image bindings (JPEG, PNG, WebP, AVIF, TIFF) | Functional |
| `Janset.SDL2.Mixer` | SDL2_mixer bindings (MP3, FLAC, OGG, Opus, MOD, MIDI) | In Progress |
| `Janset.SDL2.Ttf` | SDL2_ttf bindings (TrueType + Harfbuzz text shaping) | In Progress |
| `Janset.SDL2.Gfx` | SDL2_gfx bindings (primitives, rotozoom) | Functional |
| `Janset.SDL2.Net` | SDL2_net bindings (TCP/UDP networking) | Planned |
| `Janset.SDL2` | Meta-package (pulls everything) | Planned |

### SDL3 (Planned)

| Package | Description | Status |
| --- | --- | --- |
| `Janset.SDL3.Core` | SDL3 bindings (GPU API, new audio, camera, dialogs) | Planned |
| `Janset.SDL3.Image` | SDL3_image bindings | Planned |
| `Janset.SDL3.Mixer` | SDL3_mixer bindings | Planned |
| `Janset.SDL3.Ttf` | SDL3_ttf bindings (+ SVG/emoji support) | Planned |
| `Janset.SDL3` | Meta-package | Planned |

## Platform Support

Native binaries are built for all major desktop platforms:

| Platform | Architectures | Status |
| --- | --- | --- |
| Windows | x64, x86, ARM64 | Functional (7-RID green via `release.yml`) |
| Linux | x64, ARM64 | Functional (GHCR-hosted `linux-builder:focal-latest` image) |
| macOS | x64 (Intel), ARM64 (Apple Silicon) | Functional |

All native libraries are compiled from source using vcpkg via custom `*-hybrid` overlay triplets — transitive deps are statically baked into satellite shared libs while the SDL2 core stays dynamic (single source-of-truth instance across the package set). Active feature set:

- **SDL2**: Vulkan, X11, Wayland, ALSA, D-Bus, IBus, libsamplerate
- **SDL2_image**: AVIF, JPEG (libjpeg-turbo), WebP, TIFF, PNG
- **SDL2_mixer**: MP3 (minimp3 — LGPL-free), FLAC (drflac), Opus, WavPack, OGG Vorbis, MOD (libmodplug), MIDI (bundled Timidity + freepats on Linux/CI)
- **SDL2_ttf**: FreeType, Harfbuzz (advanced text shaping)
- **SDL2_gfx**: primitives, rotozoom

LGPL-licensed codecs (mpg123, libxmp, FluidSynth) are deliberately not in the active feature set per the project's LGPL-free codec policy — see [`AGENTS.md` Settled Strategic Decisions](AGENTS.md).

## Getting Started

Once packages are published:

```bash
# Everything at once
dotnet add package Janset.SDL2

# Or pick what you need
dotnet add package Janset.SDL2.Core
dotnet add package Janset.SDL2.Image
```

The native packages are pulled in transitively — you never need to reference `.Native` packages directly.

**Target Frameworks**: net9.0, net8.0, netstandard2.0, net462

## Project Status

**Actively under development.** Resuming after a hiatus — core infrastructure is solid, now completing the packaging pipeline.

| Area | Status |
| --- | --- |
| C# bindings (5 SDL2 libraries) | Done |
| Cake Frosting build host (DDD-layered, ADR-002) | Done |
| Native binary harvesting pipeline (7-RID hybrid-static) | Done |
| Cross-platform CI workflow (`release.yml`, 10 jobs) | Done |
| NuGet package creation (5 family × 3 nupkg) | Done |
| Build-host test suite (TUnit) | Done — 460/460 |
| Cross-platform smoke validation (A-K checkpoints) | Done — Windows + WSL Linux local, 7 RIDs CI |
| Release pipeline publish stubs (PublishStaging / PublishPublic) | Phase 2b — Cake stubs landed, real feed transfer pending |
| SDL2_net support | Phase 3 — manifest entry retired pending binding skeleton (#58) |
| Binding auto-generation (CppAst) | Phase 4 |
| SDL3 support | Phase 5 |
| Samples | Phase 3 (#60) |

See [docs/plan.md](docs/plan.md) for detailed status and roadmap.

## Building from Source

```bash
# Clone with submodules (vcpkg + SDL2-CS)
git clone --recursive https://github.com/janset2d/sdl2-cs-bindings.git
cd sdl2-cs-bindings

# Canonical local setup (V5): prepare local feed + smoke override in one step
dotnet run --project build/_build -- --target SetupLocalDev --source=local

# Build managed bindings directly (fast managed-only path)
dotnet build src/SDL2.Core/SDL2.Core.csproj
dotnet build src/SDL2.Image/SDL2.Image.csproj

# Optional: build-host regression suite
dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo

# Note: the root solution intentionally keeps smoke projects.
# If build/msbuild/Janset.Local.props is missing or stale,
# solution-level build can fail on smoke package restore:
# dotnet build Janset.SDL2.sln

# To build native libraries locally:
# 1. Bootstrap vcpkg
./external/vcpkg/bootstrap-vcpkg.sh  # or .bat on Windows

# 2. Install native dependencies for your platform
./external/vcpkg/vcpkg install --triplet x64-linux-hybrid --overlay-triplets=vcpkg-overlay-triplets

# 3. Run the harvest pipeline
cd build/_build
dotnet run -- --target Harvest --library SDL2 --library SDL2_image --rid linux-x64
```

For detailed build instructions, see [docs/playbook/local-development.md](docs/playbook/local-development.md).

## Documentation

| Document | Purpose |
| --- | --- |
| [docs/onboarding.md](docs/onboarding.md) | Project overview, decisions, repo layout |
| [docs/plan.md](docs/plan.md) | Current status and roadmap |
| [docs/playbook/](docs/playbook/) | How-to recipes (local dev, adding libraries, vcpkg updates) |
| [docs/knowledge-base/](docs/knowledge-base/) | Deep technical references (harvesting, CI/CD, Cake architecture) |
| [docs/research/](docs/research/) | Design rationale (packaging patterns, autogen approaches, SDL3 analysis) |
| [docs/phases/](docs/phases/) | Phase-by-phase execution details |

## Architecture

```text
User's .csproj
  └── references Janset.SDL2.Core (managed bindings)
        └── depends on Janset.SDL2.Core.Native (native binaries)
              └── runtimes/win-x64/native/SDL2.dll
              └── runtimes/linux-x64/native/libSDL2-2.0.so.0
              └── runtimes/osx-arm64/native/libSDL2.dylib
```

Native libraries are built via a Cake Frosting pipeline that:

1. Compiles SDL2 from source using vcpkg
2. Walks the binary dependency closure (dumpbin/ldd/otool)
3. Filters out OS-provided libraries
4. Packages binaries into NuGet-compatible `runtimes/{rid}/native/` layout
5. Preserves Linux/macOS symlinks via tar.gz archives

## Motivation

This project is the foundation for **Janset2D**, a cross-platform 2D game framework (named after my daughter, Janset). While these bindings are integral to Janset2D (which will be open-sourced separately), they are designed as a standalone, community-facing contribution.

## Acknowledgements

- C# bindings based on [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS) by Ethan Lee
- Native libraries built using [vcpkg](https://github.com/microsoft/vcpkg) by Microsoft
- Build automation powered by [Cake Frosting](https://cakebuild.net/)
- Inspired by the packaging patterns of [SkiaSharp](https://github.com/mono/SkiaSharp) and [LibGit2Sharp](https://github.com/libgit2/libgit2sharp)

## Contributing

Contributions will be welcome once the initial release and CI/CD pipeline are stabilized. A `CONTRIBUTING.md` will be added at that time.

## License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.

## Version Mapping

<!-- JANSET:MAPPING-TABLE-START -->
| Family | Version | Upstream | vcpkg Port |
| --- | --- | --- | --- |
| Janset.SDL2.Core | 2.32.0 | SDL 2.32.10 | 0 |
| Janset.SDL2.Image | 2.8.0 | SDL2_image 2.8.8 | 2 |
| Janset.SDL2.Mixer | 2.8.0 | SDL2_mixer 2.8.1 | 2 |
| Janset.SDL2.Ttf | 2.24.0 | SDL2_ttf 2.24.0 | 0 |
| Janset.SDL2.Gfx | 1.0.0 | SDL2_gfx 1.0.4 | 11 |
<!-- JANSET:MAPPING-TABLE-END -->
