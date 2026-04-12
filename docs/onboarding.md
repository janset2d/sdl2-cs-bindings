# Onboarding — Janset.SDL2 / Janset.SDL3

> **If you are an LLM/code agent entering this repo for the first time, start here.**
> If you are a human contributor, this is also your best starting point.

## What Is This Project?

Janset.SDL2 (and its upcoming sibling Janset.SDL3) provides **modular C# bindings for SDL2/SDL3 and their satellite libraries**, bundled with **cross-platform native libraries built via vcpkg** and distributed as **NuGet packages**.

This is the foundation layer for **Janset2D**, a cross-platform 2D game framework (named after the maintainer's daughter). However, these bindings are designed to be a **fully independent, community-facing open-source project** — not just internal tooling.

## Why Does This Project Exist?

The .NET SDL ecosystem has a gap: existing binding projects (SDL2-CS, ppy/SDL3-CS, Alimer.Bindings.SDL, etc.) provide C# P/Invoke declarations but **none of them ship cross-platform native binaries built from source via a reproducible pipeline**. Users are expected to source their own native SDL2/SDL3 libraries.

Janset.SDL2/SDL3 fills this gap by:

1. Providing C# bindings (currently based on [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS), with plans for auto-generated bindings)
2. Building native libraries from source using **vcpkg** across 7+ Runtime Identifiers (RIDs)
3. Packaging everything into modular NuGet packages with proper `runtimes/{rid}/native/` layout
4. Handling Linux/macOS symlink preservation via tar.gz archives with MSBuild extraction targets

**No other project in the ecosystem does all four.**

## Who Maintains This?

**Deniz Irgin** (@denizirgin) — senior .NET developer based in Istanbul. Communication preferences:

- Conversational, practical humor
- Prefers comprehensive solutions over quick hacks
- Expects challenge and reasoning, not yes-person behavior
- Bilingual: Turkish and English

## Strategic Decisions (Canonical)

These are settled decisions. Do not re-debate them unless new evidence surfaces.

| Decision | Detail | Rationale |
| --- | --- | --- |
| **Dual SDL support** | SDL2 AND SDL3 in the same monorepo | Community service; SDL2 is priority (finish first), SDL3 follows |
| **Full RID coverage** | 7+ targets including win-x86, linux-arm64, macOS | Maximum platform coverage is a project differentiator |
| **vcpkg-based builds** | All native binaries built from source via vcpkg | Reproducibility, version pinning, feature flag control |
| **Separate .Native packages** | `Janset.SDL2.Core` + `Janset.SDL2.Core.Native` split | Industry standard (SkiaSharp, LibGit2Sharp pattern) |
| **tar.gz for Unix symlinks** | Linux/macOS natives archived to preserve symlink chains | NuGet doesn't support symlinks; renaming breaks transitive deps |
| **Binding autogeneration** | CppAst-based generator (Alimer approach) planned | Manual bindings don't scale for SDL2+SDL3 dual support |
| **Nx rejected** | .NET-native tooling instead (dotnet-affected, .slnx, Cake) | Nx .NET plugin is experimental; adds Node.js dependency for no gain |
| **Maximum feature coverage** | Both X11 + Wayland, all image codecs, all audio codecs, Harfbuzz | Game framework needs broad backend support |

## Satellite Library Coverage

### SDL2 (Priority — Finish First)

| Library | vcpkg Port | Bindings | Native Build | Status |
| --- | --- | --- | --- | --- |
| SDL2 | `sdl2` (2.32.4, latest: 2.32.10) | SDL2-CS import | Cake Harvest | Functional |
| SDL2_image | `sdl2-image` (2.8.8) | SDL2-CS import | Cake Harvest | Functional |
| SDL2_mixer | `sdl2-mixer` (2.8.1) | SDL2-CS import | vcpkg.json missing | Incomplete |
| SDL2_ttf | `sdl2-ttf` (2.24.0) | SDL2-CS import | vcpkg.json missing | Placeholder |
| SDL2_gfx | `sdl2-gfx` (1.0.4) | SDL2-CS import | Cake Harvest | Functional |
| SDL2_net | `sdl2-net` (2.2.0) | Not yet added | Not yet added | Not started |

### SDL3 (Future — After SDL2 Complete)

| Library | vcpkg Port | Bindings Source | Status |
| --- | --- | --- | --- |
| SDL3 | `sdl3` (3.4.4) | flibitijibibo/SDL3-CS or auto-gen | Not started |
| SDL3_image | `sdl3-image` (3.4.2) | Auto-gen planned | Not started |
| SDL3_mixer | `sdl3-mixer` (3.2.0) | Auto-gen planned | Not started |
| SDL3_ttf | `sdl3-ttf` (3.2.2) | Auto-gen planned | Not started |
| SDL3_net | N/A | Upstream WIP, no vcpkg port | Blocked |

## Repository Layout

```text
janset2d/sdl2-cs-bindings/
├── AGENTS.md                  ← LLM/agent operating rules
├── README.md                  ← Public-facing project overview
├── Janset.SDL2.sln            ← Main solution (all projects)
├── Directory.Build.props      ← Centralized build config (TFMs, analyzers, packaging)
├── Directory.Packages.props   ← Centralized NuGet version management
├── vcpkg.json                 ← Native dependency declarations + feature flags
├── global.json                ← .NET SDK version pin
│
├── src/
│   ├── SDL2.Core/             ← C# bindings for SDL2 (imports external/sdl2-cs/src/SDL2.cs)
│   ├── SDL2.Image/            ← C# bindings for SDL2_image
│   ├── SDL2.Mixer/            ← C# bindings for SDL2_mixer
│   ├── SDL2.Ttf/              ← C# bindings for SDL2_ttf
│   ├── SDL2.Gfx/              ← C# bindings for SDL2_gfx
│   └── native/                ← Native NuGet package projects
│       ├── Directory.Build.props  ← Shared native package config
│       ├── SDL2.Core.Native/  ← Pre-compiled SDL2 binaries per RID
│       ├── SDL2.Image.Native/
│       ├── SDL2.Mixer.Native/
│       ├── SDL2.Ttf.Native/   ← Currently placeholder (no binaries)
│       └── SDL2.Gfx.Native/
│
├── build/
│   ├── manifest.json          ← Source of truth: library names, versions, binary patterns
│   ├── runtimes.json          ← RID → vcpkg triplet → CI runner mapping
│   ├── system_artefacts.json  ← OS library exclusion whitelist
│   └── _build/                ← Cake Frosting build system
│       ├── Program.cs         ← Entry point + DI configuration
│       ├── Context/           ← Build context and state
│       ├── Models/            ← Data models (DeploymentPlan, RuntimeProfile, etc.)
│       ├── Modules/           ← DI modules (harvesting, packaging services)
│       ├── Tasks/             ← Build tasks (Harvest, Consolidate, Preflight, etc.)
│       └── Tools/             ← Utility services (BinaryClosureWalker, ArtifactDeployer)
│
├── external/
│   ├── sdl2-cs/               ← Git submodule: flibitijibibo/SDL2-CS (binding source)
│   └── vcpkg/                 ← Git submodule: microsoft/vcpkg (native build toolchain)
│
├── .github/
│   ├── actions/
│   │   └── vcpkg-setup/       ← Reusable composite action for vcpkg bootstrap + caching
│   └── workflows/
│       ├── prepare-native-assets-main.yml     ← Orchestrator (calls platform workflows)
│       ├── prepare-native-assets-windows.yml  ← Windows matrix: x64, x86, arm64
│       ├── prepare-native-assets-linux.yml    ← Linux matrix: x64, arm64 (containers)
│       ├── prepare-native-assets-macos.yml    ← macOS matrix: x64, arm64
│       └── release-candidate-pipeline.yml     ← End-to-end release (STUB/INCOMPLETE)
│
├── artifacts/                 ← Build output (gitignored)
├── samples/                   ← Example projects (empty — to be created)
├── test/                      ← Test projects (only Sandboc sandbox exists)
├── scripts/                   ← Packaging scripts (PowerShell/Bash)
│
└── docs/                      ← You are here
    ├── README.md              ← Documentation map and navigation guide
    ├── onboarding.md          ← THIS FILE — start here
    ├── plan.md                ← Canonical status, phase roll-up, roadmap
    ├── phases/                ← Phase-by-phase execution details
    ├── research/              ← Dated research, design rationale, comparisons
    ├── playbook/              ← "How do I...?" recipes
    ├── knowledge-base/        ← Deep technical references
    └── reference/             ← Deep on-demand general references
```

## Build System Overview

### How Native Libraries Get Built

```text
vcpkg.json (dependency + feature declarations)
    ↓
vcpkg install --triplet {triplet}  (via CI or local)
    ↓
vcpkg_installed/{triplet}/  (compiled native binaries)
    ↓
Cake Frosting HarvestTask  (binary closure walk + dependency scanning)
    ↓
artifacts/harvest_output/{Library}/rid-status/{rid}.json
    ↓
Cake Frosting ConsolidateHarvestTask  (merge per-RID results)
    ↓
harvest-manifest.json + harvest-summary.json
    ↓
[PLANNED] Cake PackageTask → NuGet .nupkg files
    ↓
[PLANNED] Publish to NuGet feed
```

### Key Configuration Files

| File | Purpose | Authoritative For |
| --- | --- | --- |
| `build/manifest.json` | Library definitions (name, version, binary patterns) | What libraries we ship and their versions |
| `build/runtimes.json` | RID ↔ vcpkg triplet ↔ CI runner mapping | Platform targeting |
| `build/system_artefacts.json` | OS library exclusion list | What NOT to bundle |
| `vcpkg.json` | vcpkg dependency declarations + feature flags | What gets compiled by vcpkg |
| `Directory.Build.props` | Target frameworks, analyzers, packaging metadata | .NET project-wide settings |

### Target Platforms

| RID | vcpkg Triplet | CI Runner | Container |
| --- | --- | --- | --- |
| win-x64 | x64-windows-release | windows-latest | — |
| win-x86 | x86-windows | windows-latest | — |
| win-arm64 | arm64-windows | windows-latest | — |
| linux-x64 | x64-linux-dynamic | ubuntu-24.04 | ubuntu:20.04 |
| linux-arm64 | arm64-linux-dynamic | ubuntu-24.04-arm | ubuntu:24.04 |
| osx-x64 | x64-osx-dynamic | macos-13 | — |
| osx-arm64 | arm64-osx-dynamic | macos-latest | — |

## NuGet Package Topology

```text
Janset.SDL2                              ← Meta-package (pulls everything)
├── Janset.SDL2.Core                     ← Managed bindings
│   └── Janset.SDL2.Core.Native          ← Native SDL2 binaries (all RIDs)
├── Janset.SDL2.Image
│   └── Janset.SDL2.Image.Native
├── Janset.SDL2.Mixer
│   └── Janset.SDL2.Mixer.Native
├── Janset.SDL2.Ttf
│   └── Janset.SDL2.Ttf.Native
├── Janset.SDL2.Gfx
│   └── Janset.SDL2.Gfx.Native
└── Janset.SDL2.Net                      ← To be created
    └── Janset.SDL2.Net.Native
```

Users reference `Janset.SDL2.Core` (or the meta-package `Janset.SDL2`). The `.Native` dependency is pulled in transitively — users never reference it directly.

## What Works Today (as of 2026-04-11)

- C# bindings for all 5 SDL2 libraries compile and target net9.0/net8.0/netstandard2.0/net462
- Cake Frosting Harvest pipeline: binary closure walking, dependency scanning, per-RID status files, consolidation
- GitHub Actions: Cross-platform native builds for Windows/Linux/macOS (manual trigger)
- vcpkg: SDL2 + SDL2_image with full feature flags (Vulkan, X11, Wayland, ALSA, D-Bus, all image codecs)
- Native packaging: `runtimes/{rid}/native/` structure for win-x64, win-arm64, linux-x64 (partial for other RIDs)
- `buildTransitive` MSBuild targets for .NET Framework compatibility

## What Doesn't Work Yet

- vcpkg.json missing: SDL2_mixer, SDL2_ttf, SDL2_gfx, SDL2_net dependencies
- SDL2.Ttf.Native: Placeholder only (no binaries)
- SDL2.Mixer.Native: Only win-x64 has full codec dependencies
- Release Candidate Pipeline: Largely stub/placeholder
- Cake PackageTask: Not implemented (harvest → NuGet .nupkg step missing)
- NuGet publishing: Neither internal nor public feed configured
- Tests: Only a Sandboc sandbox exists, no real test suite
- Samples: Empty directory
- Binding autogeneration: Not yet started
- SDL3 support: Not yet started

## Reading Order for LLM Agents

1. **This file** (`docs/onboarding.md`) — you are here
2. **`AGENTS.md`** (repo root) — operating rules, approval gates, communication preferences
3. **`docs/plan.md`** — current status, active phase, roadmap
4. **`docs/phases/README.md`** — which phases are active vs completed vs future
5. Then branch to the relevant area:
   Build/CI work → `docs/knowledge-base/harvesting-process.md`, `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`
   Adding a new library → `docs/playbook/adding-new-library.md`
   Understanding architecture decisions → `docs/research/*`
   Local development → `docs/playbook/local-development.md`
   Broader framework/tooling context → `docs/reference/*`

## Non-Goals

- **Not a game engine**: This is a binding/packaging layer. Game engine logic belongs in Janset2D (separate future repo).
- **Not a high-level SDL wrapper**: We provide raw P/Invoke bindings, not an OOP abstraction layer (that's also Janset2D's job).
- **Not a tutorial project**: While samples will exist, this is production infrastructure, not a learning resource.
- **Not cross-language**: C#/.NET only. No C++, Rust, or Python bindings.

## Glossary

| Term | Meaning |
| --- | --- |
| **RID** | Runtime Identifier — .NET's platform descriptor (e.g., `win-x64`, `linux-arm64`) |
| **Triplet** | vcpkg's platform descriptor (e.g., `x64-windows-release`, `arm64-linux-dynamic`) |
| **Harvest** | The process of collecting compiled native binaries + their transitive dependencies from vcpkg output |
| **Binary Closure Walk** | Recursively scanning a binary's dependencies (dumpbin on Windows, ldd on Linux, otool on macOS) |
| **Satellite Library** | SDL companion libraries: SDL_image, SDL_mixer, SDL_ttf, SDL_gfx, SDL_net |
| **SONAME** | Shared Object Name — the versioned name Linux shared libraries link against (e.g., `libSDL2-2.0.so.0`) |
| **buildTransitive** | NuGet targets that apply to consuming projects (not just direct references) |
| **Cake Frosting** | C#-based build automation framework (strongly-typed, DI-enabled alternative to MSBuild scripts) |
