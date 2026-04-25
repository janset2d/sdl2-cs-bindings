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
| **external/sdl2-cs removal** | flibitijibibo/SDL2-CS submodule is transitional — will be removed when CppAst generator (Phase 4) produces bindings. Not trusted for production testing. | Unmaintained import-style bindings; don't scale for SDL2+SDL3 |
| **C++ native smoke test** | CMake/vcpkg C++ project for testing hybrid-built natives directly, IDE-debuggable (Phase 2b) | Needed for format coverage testing without P/Invoke layer dependency |

## Satellite Library Coverage

### SDL2 (Priority — Finish First)

| Library | vcpkg Port | Bindings | Native Build | Status |
| --- | --- | --- | --- | --- |
| SDL2 | `sdl2` (2.32.10) | SDL2-CS import | Cake Harvest | Functional |
| SDL2_image | `sdl2-image` (2.8.8) | SDL2-CS import | Cake Harvest | Functional |
| SDL2_mixer | `sdl2-mixer` (2.8.1) | SDL2-CS import | Declared in vcpkg + Cake Harvest | In validation |
| SDL2_ttf | `sdl2-ttf` (2.24.0) | SDL2-CS import | Declared in vcpkg + Cake Harvest | In validation |
| SDL2_gfx | `sdl2-gfx` (1.0.4) | SDL2-CS import | Declared in vcpkg + Cake Harvest | In validation |
| SDL2_net | `sdl2-net` (2.2.0) | Not yet added | Declared in vcpkg/manifest + Harvest path wired | Partial (managed binding pending) |

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
│   ├── manifest.json          ← Single source of truth: packaging config, runtimes, system exclusions, library manifests
│   └── _build/                ← Cake Frosting build system (DDD-layered per ADR-002)
│       ├── Program.cs         ← Entry point + DI composition root
│       ├── Context/           ← BuildContext binding (Cake task boundary)
│       ├── Tasks/             ← Presentation: Cake Frosting task classes (Harvest, Package, PreFlight, etc.)
│       ├── Application/       ← Use-case orchestrators (TaskRunners, Resolvers, SmokeRunner)
│       │   ├── Packaging/     ← PackageTaskRunner, SmokeRunner, ArtifactSourceResolvers
│       │   ├── Harvesting/    ← ArtifactPlanner, ArtifactDeployer, BinaryClosureWalker
│       │   └── Preflight/     ← PreflightReporter
│       ├── Domain/            ← Models, value objects, domain services, result types
│       │   ├── Packaging/     ← PackageVersion, NativePackageMetadata, PackageOutputValidator, etc.
│       │   ├── Harvesting/    ← PackageInfo, DeploymentPlan, BinaryClosure, HarvestJsonContract
│       │   ├── Preflight/     ← FamilyIdentifierConventions + guardrail validators (G21–G27, G54, G56)
│       │   ├── Coverage/      ← CoverageThresholdValidator + metrics/baseline models
│       │   ├── Strategy/      ← HybridStatic/PureDynamic strategies + validators + resolver
│       │   ├── Runtime/       ← RuntimeProfile (RID + triplet + platform detection)
│       │   ├── Paths/         ← IPathService (abstraction; implementation in Infrastructure)
│       │   └── Results/       ← BuildError, BuildResultExtensions, AsyncResultChaining helpers
│       └── Infrastructure/    ← External-system adapters: filesystem, process, CLI
│           ├── Paths/         ← PathService implementation
│           ├── Json/          ← (placeholder — JSON helpers currently live on CakeExtensions)
│           ├── DotNet/        ← DotNetPackInvoker, ProjectMetadataReader (wrap dotnet CLI / MSBuild)
│           ├── Vcpkg/         ← VcpkgCliProvider, VcpkgManifestReader (consume manifest + vcpkg CLI)
│           ├── Coverage/      ← CoberturaReader, CoverageBaselineReader (XML/JSON readers)
│           ├── DependencyAnalysis/ ← Windows/Linux/macOS binary scanners (dumpbin/ldd/otool)
│           └── Tools/         ← Cake-native Tool<T>/Aliases/Settings wrappers (Vcpkg, Dumpbin, Ldd, Otool)
│
├── external/
│   ├── sdl2-cs/               ← Git submodule: flibitijibibo/SDL2-CS (binding source)
│   └── vcpkg/                 ← Git submodule: microsoft/vcpkg (native build toolchain)
│
├── .github/
│   ├── actions/
│   │   ├── vcpkg-setup/                    ← vcpkg submodule + container digest cache identity + bootstrap + install
│   │   ├── nuget-cache/                    ← cross-OS workspace NuGet cache keyed on lock files + CPM + csproj hash
│   │   └── platform-build-prereqs/         ← macOS brew autotools (idempotent); Linux/Windows no-ops
│   └── workflows/
│       ├── release.yml                     ← End-to-end release pipeline (10-job topology, tag-push + workflow_dispatch triggers)
│       └── build-linux-container.yml       ← Multi-arch GHCR builder image (focal-<yyyymmdd>-<sha> + focal-latest tags)
│
├── artifacts/                 ← Build output (gitignored)
├── samples/                   ← Example projects (empty — to be created)
├── tests/                     ← Test projects
│   ├── Sandbox/               ← Throwaway exploration sandbox (ignore in review)
│   └── smoke-tests/           ← Post-pipeline integrity checks — see tests/smoke-tests/README.md
│       ├── native-smoke/      ← C++/CMake runtime test for hybrid-built natives
│       └── package-smoke/     ← .NET consumer smoke (PackageReference → SDL_Init)
├── build/
│   ├── _build/                ← Cake Frosting build host (production code)
│   └── _build.Tests/          ← Cake build-host unit tests (TUnit, white-box)
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
Cake PackageTask → NuGet .nupkg files (full 7-RID matrix green via release.yml)
    ↓
PackageConsumerSmoke (matrix re-entry, per-TFM TUnit) → validates the produced nupkgs end-to-end
    ↓
[Phase 2b] PublishStaging / PublishPublic → real feed transfer (Cake stubs landed P6; release.yml jobs gated `if: false` until impl)
```

### Key Configuration Files

| File | Purpose | Authoritative For |
| --- | --- | --- |
| `build/manifest.json` | Single source of truth (schema v2): library definitions, runtimes, system exclusions, packaging config | What we ship, platform targeting, OS exclusions, strategy mapping |
| `vcpkg.json` | vcpkg dependency declarations + feature flags | What gets compiled by vcpkg |
| `Directory.Build.props` | Target frameworks, analyzers, packaging metadata | .NET project-wide settings |

> **Note:** `build/manifest.json` is the canonical merged configuration file. Legacy references to separate `runtimes.json` / `system_artefacts.json` files should be treated as stale historical context unless a doc explicitly says otherwise.

### Target Platforms

RID and runner mappings are sourced from `manifest.runtimes[]` (single source of truth). Current state (2026-04-25):

| RID | vcpkg Triplet | Strategy | CI Runner | Container |
| --- | --- | --- | --- | --- |
| win-x64 | x64-windows-hybrid | hybrid-static | windows-2025 | — |
| win-x86 | x86-windows-hybrid | hybrid-static | windows-2025 | — |
| win-arm64 | arm64-windows-hybrid | hybrid-static | windows-11-arm | — |
| linux-x64 | x64-linux-hybrid | hybrid-static | ubuntu-24.04 | ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest |
| linux-arm64 | arm64-linux-hybrid | hybrid-static | ubuntu-24.04-arm | ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest |
| osx-x64 | x64-osx-hybrid | hybrid-static | macos-15-intel | — |
| osx-arm64 | arm64-osx-hybrid | hybrid-static | macos-26 | — |

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
└── Janset.SDL2.Net                      ← Phase 3 (to be created — see #58; manifest entry retired 2026-04-22 awaiting binding + native csproj skeleton)
    └── Janset.SDL2.Net.Native
```

Users reference `Janset.SDL2.Core` (or the meta-package `Janset.SDL2`). The `.Native` dependency is pulled in transitively — users never reference it directly.

## What Works Today (as of 2026-04-26, master `8ec85c5`)

- **C# bindings**: all 5 SDL2 libraries (`Core`/`Image`/`Mixer`/`Ttf`/`Gfx`) compile against `net9.0`/`net8.0`/`netstandard2.0`/`net462`.
- **Cake Frosting build host**: 20 lifecycle + diagnostic targets (Info, CleanArtifacts, CompileSolution, GenerateMatrix, ResolveVersions, PreFlightCheck, EnsureVcpkgDependencies, Harvest, NativeSmoke, ConsolidateHarvest, Inspect-HarvestedDependencies, Package, PackageConsumerSmoke, SetupLocalDev, Coverage-Check, PublishStaging, PublishPublic, plus dependency-analysis aliases). DDD-layered per ADR-002 (Tasks/Application/Domain/Infrastructure + LayerDependencyTests catchnet).
- **Build-host test suite**: 460 TUnit tests covering Domain, Application, Infrastructure, Tasks, Context, CompositionRoot. Run via `dotnet test build/_build.Tests/Build.Tests.csproj -c Release`. Coverage ratchet gate `Coverage-Check` enforces the floor in `build/coverage-baseline.json`.
- **Version-source providers** (ADR-003): `ManifestVersionProvider` (manifest-derived), `GitTagVersionProvider` (tag-driven targeted/full-train), `ExplicitVersionProvider` (operator override) — `ResolveVersions` Cake target emits canonical `versions.json` consumed by every downstream stage via `--versions-file`.
- **CI pipeline** (`release.yml`, 10 jobs): tag-push + `workflow_dispatch` triggers; dynamic 7-RID matrix from `manifest.runtimes[]`; consumer smoke matrix re-entry; Cake host built once + distributed as FDD artifact; GHCR-hosted Linux builder image.
- **Cross-platform validation**: A-K checkpoints green on Windows + WSL Linux at master `d190b5b` (P7 witness 2026-04-25); all 7 RIDs green again via post-push CI run 24938451364 on master `8ec85c5` (Pack ✓, ConsumerSmoke ✓ across `win-{x64,x86,arm64}`/`linux-{x64,arm64}`/`osx-{x64,arm64}`).
- **PreFlight validation**: manifest.json ↔ vcpkg.json consistency, strategy coherence, csproj pack contract (G4/G6/G7/G17/G18), G54 upstream version alignment, G58 cross-family dependency resolvability (defense-in-depth in PreFlight + Pack).
- **Lock-file discipline** (P5): `<RestorePackagesWithLockFile>true</...>` on `build/_build` + `build/_build.Tests` + 10 `src/**` csprojs; strict mode (`<RestoreLockedMode Condition="'$(GITHUB_ACTIONS)'='true'"/>`) only on the build host; `src/` lenient mode absorbs SDK-implicit-package drift (ILLink.Tasks per runtime patch, NETFramework.ReferenceAssemblies per host OS).
- **Native packaging**: `PackageTask` + `PackageConsumerSmoke` produce + validate 5 family × 3 nupkg per release (managed + native + snupkg) at D-3seg-shaped versions (per family `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`); `buildTransitive/Janset.SDL2.Native.Common.targets` handles consumer-side native-asset placement (Unix tar.gz extraction + .NETFramework AnyCPU DLL copy).
- **vcpkg native ports** for SDL2 + 4 satellites (`sdl2`, `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`) all declared in `vcpkg.json` + custom hybrid overlay triplets at `vcpkg-overlay-triplets/`.
- **NativeSmoke**: 29-test C/C++ harness (CMake/vcpkg) validates hybrid-built natives at OS level (PNG/JPEG/WebP/TIFF/AVIF loading, FLAC/MIDI/WavPack/Opus/OGG/MP3/MOD decoder discovery, TTF_Init, SDL2_gfx draw); IDE-debuggable via `CMakePresets.json` (Release/Debug × 7 RIDs) + `CMakeUserPresets.json.example` for interactive variants.

## What Doesn't Work Yet

- **NuGet publishing**: `PublishStaging` / `PublishPublic` Cake targets are scaffolded stubs (P6 — throws `NotImplementedException` with Phase-2b pointer); `release.yml` jobs gated `if: false`. Real implementation arrives with Phase 2b's first prerelease publication wave.
- **`SDL2.Mixer.Native`**: full codec dependencies in the hybrid bake validated on `win-x64`; per-RID codec parity audit pending.
- **`SDL2.Ttf.Native`**: harvest + pack pipeline live, but per-RID font-rendering smoke beyond `TTF_Init` is still pending.
- **`SDL2.Net` family**: manifest entry retired 2026-04-22 (`bc652d1`); will re-land with the full skeleton (binding csproj + native csproj + overlay port + manifest entries + harvest validation) per [#58](https://github.com/janset2d/sdl2-cs-bindings/issues/58).
- **`RemoteArtifactSourceResolver`** (PD-5): `SetupLocalDev --source=remote` accepted but stubbed (`UnsupportedArtifactSourceResolver`); remote-internal feed download is Phase 2b.
- **Samples**: `samples/` directory empty; targeted to land alongside the first prerelease publication ([#60](https://github.com/janset2d/sdl2-cs-bindings/issues/60)).
- **Binding autogeneration**: CppAst-based generator (Phase 4) is the long-term plan to replace the `external/sdl2-cs/` submodule import. Not yet started.
- **SDL3 support**: scoped for after SDL2 line is fully shipped.

## Work Tracking Model

This repo treats docs, issues, and commits as one delivery system:

- Canonical docs define current reality and roadmap direction.
- GitHub issues should represent concrete deliverables, cleanup threads, or deferred work that still matters.
- Issues should link back to the relevant canonical docs and use current milestone and label metadata.
- The canonical label model is `type:*` plus `area:*`, with optional `platform:*` labels for OS-specific work.
- Retired label families such as `phase:*`, `component:*`, `topic:*`, `meta:*`, and `process:*` are historical only and should not be used for new work.
- PRs are optional; direct commits are fine when the work does not need a PR workflow.
- When possible, commits should reference the issue they belong to so the implementation trail is easy to reconstruct.
- If something is worth remembering but not worth doing now, keep it in a backlog issue or the parking-lot docs instead of leaving it only in chat history.

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
   **Overlay triplets & ports (CRITICAL for build system work)** → `docs/playbook/overlay-management.md`
   Symbol visibility & cross-platform safety → `docs/research/symbol-visibility-analysis-2026-04-14.md`
   **Cake build host strategy refactor (NEXT WORK ITEM)** → `docs/research/cake-strategy-implementation-brief-2026-04-14.md`
   Native smoke test (C++ runtime validation) → `tests/smoke-tests/native-smoke/README.md`
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
| **Triplet** | vcpkg's platform descriptor (e.g., `x64-windows-hybrid`, `arm64-linux-dynamic`). Encodes the packaging strategy in the name. |
| **Harvest** | The process of collecting compiled native binaries + their transitive dependencies from vcpkg output |
| **Binary Closure Walk** | Recursively scanning a binary's dependencies (dumpbin on Windows, ldd on Linux, otool on macOS) |
| **Satellite Library** | SDL companion libraries: SDL_image, SDL_mixer, SDL_ttf, SDL_gfx, SDL_net |
| **Package Family** | A release unit: one managed bindings package + its .Native package, always versioned and released together (e.g., `sdl2-core` family = `Janset.SDL2.Core` + `Janset.SDL2.Core.Native`) |
| **Family Identifier** | Canonical `sdl<major>-<role>` string used in manifest.json, `<MinVerTagPrefix>`, and git tags. Examples: `sdl2-core`, `sdl2-image`, `sdl3-core` (future). Mandatory `sdl<major>-` prefix mirrors `Janset.SDL2.*` PackageId convention and disambiguates SDL2 from SDL3. |
| **Core Family** | The SDL family's core package family — `sdl2-core` for SDL2, `sdl3-core` (future) for SDL3. All satellite families in the same SDL major-version line depend on it. Released first when multiple families release together. |
| **Satellite Family** | Any non-core package family within an SDL major-version line: `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`, `sdl2-net`. Depends on the core family of the same line, but versioned independently. |
| **Family Version** | The single shared version number for both packages within a family. Shape = `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>` (D-3seg, see [ADR-001](decisions/2026-04-18-versioning-d3seg.md)). Derived from a family tag, e.g., `sdl2-core-2.32.0`. UpstreamMajor.Minor anchored to `manifest.json library_manifests[].vcpkg_version` (enforced by G54); FamilyPatch is the repo's own release-iteration counter. |
| **Targeted Release** | Release of specific families without touching others. The default release mode. |
| **Full-Train Release** | Coordinated release of all families together, triggered by cross-cutting changes. |
| **SONAME** | Shared Object Name — the versioned name Linux shared libraries link against (e.g., `libSDL2-2.0.so.0`) |
| **buildTransitive** | NuGet targets that apply to consuming projects (not just direct references) |
| **Cake Frosting** | C#-based build automation framework (strongly-typed, DI-enabled alternative to MSBuild scripts) |

> For the full release lifecycle glossary, see [knowledge-base/release-lifecycle-direction.md](knowledge-base/release-lifecycle-direction.md).
