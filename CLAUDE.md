# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Read These First

This repo already has a comprehensive agent contract — do not duplicate it here:

1. [AGENTS.md](AGENTS.md) — operating rules, **approval gate**, communication style, settled strategic decisions, build-host reference pattern, configuration-file relationships
2. [docs/onboarding.md](docs/onboarding.md) — project overview, repo layout, glossary, native build pipeline diagram
3. [docs/plan.md](docs/plan.md) — current status, active phase, roadmap

The approval gate in AGENTS.md is binding: do not start coding new features, modify the build system / CI / vcpkg.json / manifest.json / .csproj / .sln, run deployments, or commit without an explicit "go / apply / proceed / başla / yap". Doc-only edits, broken-link fixes, and minor comment improvements are exempt.

## Common Commands

The Cake Frosting build host is invoked as a regular `dotnet run` — `build.ps1` / `build.sh` are thin shims that forward args.

```pwsh
# Discover lifecycle targets
dotnet run --project build/_build -- --tree
dotnet run --project build/_build -- --target Info

# Fresh-clone canonical setup (vcpkg + Harvest + Pack + writes build/msbuild/Janset.Local.props)
dotnet run --project build/_build -- --target SetupLocalDev --source=local
# --source=remote pulls latest published nupkgs from the GitHub Packages internal feed
#   (requires GH_TOKEN with read:packages — Classic PAT, fine-grained PATs unsupported by GH Packages NuGet)
# --source=release is intentionally stubbed until Phase 2b PD-7

# Managed-only build (skip native pipeline)
dotnet build src/SDL2.Core/SDL2.Core.csproj
# Solution-level build can fail on smoke restore (NU1101) if Janset.Local.props is stale —
# re-run SetupLocalDev to regenerate before `dotnet build Janset.SDL2.sln`.

# Build-host regression suite (TUnit on Microsoft.Testing.Platform)
dotnet test build/_build.Tests/Build.Tests.csproj -c Release --nologo
# Single test class: append --filter "FullyQualifiedName~PackageVersionResolverTests"

# Pipeline targets (run individually for debugging)
dotnet run --project build/_build -- --target PreFlightCheck
dotnet run --project build/_build -- --target Harvest --library SDL2 --library SDL2_image --rid win-x64
dotnet run --project build/_build -- --target ConsolidateHarvest
dotnet run --project build/_build -- --target Package --explicit-version sdl2-core=2.32.0-local.1 ...
dotnet run --project build/_build -- --target CoverageCheck   # ratchet against build/coverage-baseline.json
```

Versioning notes:

- Versions follow **D-3seg** (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`, see [ADR-001](docs/decisions/2026-04-18-versioning-d3seg.md)). UpstreamMajor.Minor is anchored to `manifest.library_manifests[].vcpkg_version` and enforced by guardrail G54.
- `--explicit-version` accepts `<family>=<semver>` repeated; mutually exclusive with `--versions-file`.
- **G58 cross-family scope**: pack a satellite (e.g. `sdl2-image`) without also supplying a satisfying `sdl2-core` version → Pack stops. For local rehearsal, pack all 5 concrete families together or pack `sdl2-core` alone.
- Direct `dotnet pack` on a `*.Native.csproj` hard-fails G46 (empty native payload). Always go through the `Package` task.

## High-Level Architecture

### What ships

Two parallel artifact lines per SDL family (e.g. `sdl2-core` family = `Janset.SDL2.Core` managed + `Janset.SDL2.Core.Native` natives). Users reference the managed package; the `.Native` package is transitive.

```text
vcpkg.json              → vcpkg install --triplet <name>-hybrid
build/manifest.json     → single source of truth (schema v2.1):
  packaging_config • runtimes[] • package_families[] • system_exclusions • library_manifests[]
       │
       ▼
Harvest (binary closure walk: dumpbin/ldd/otool, OS-lib filter, symlink preserve)
       │
       ▼  artifacts/harvest_output/<Library>/runtimes/<rid>/native/ + rid-status/<rid>.json
       ▼
ConsolidateHarvest → harvest-manifest.json + harvest-summary.json
       │
       ▼
Package → 5 family × 3 nupkg per release (managed + native + snupkg, D-3seg)
       │
       ▼
PackageConsumerSmoke (matrix re-entry, per-TFM TUnit)
       │
       ▼
PublishStaging (GitHub Packages internal feed) → PublishPublic (nuget.org via Trusted Publishing OIDC, PD-7 stub)
```

`PreFlightCheck` validates manifest ↔ vcpkg.json consistency, triplet ↔ strategy coherence, csproj pack contract (G4/G6/G7/G17/G18), G54 upstream alignment, and G58 cross-family resolvability before anything else runs.

### Native packaging strategy (settled — do not re-debate)

- **Hybrid-static / dynamic-core** across all 7 RIDs: transitive deps are statically baked into satellite shared libs while the SDL2 core library stays dynamic (single source-of-truth instance across the package set). Encoded in custom vcpkg overlay triplets at `vcpkg-overlay-triplets/` (`x64-windows-hybrid`, `arm64-linux-hybrid`, etc.).
- **Triplet name = strategy** — there is no `--strategy` CLI flag. `manifest.runtimes[].strategy` is the formal mapping enforced by `IStrategyResolver` + `IStrategyCoherenceValidator` (G14/G15/G16).
- **LGPL-free codec stack**: drop mpg123 / libxmp / fluidsynth; use bundled minimp3 / drflac / libmodplug / Timidity / native MIDI. Adding LGPL codecs requires explicit reopening of the strategic decision.
- **tar.gz for Unix symlinks**: NuGet can't preserve symlinks; `buildTransitive/Janset.SDL2.Native.Common.targets` extracts at consumer build time.

### Cake build host layout (Cake-native feature-oriented per [ADR-004](docs/decisions/2026-05-02-cake-native-feature-architecture.md))

`build/_build/` is organized as five top-level folders per ADR-004 (which supersedes ADR-002 DDD layering, 2026-04-19). Direction-of-dependency invariants are enforced by `build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs` (renamed from `LayerDependencyTests.cs` at the P2 wave).

| Folder | Role |
| --- | --- |
| `Host/` | Cake/Frosting runtime, CLI parsing, `BuildContext`, composition root, paths, Cake extensions |
| `Features/<X>/` | Operational vertical slice — Cake `Task` + `Pipeline` (size-triggered) + validators + generators + `Request` DTOs + `ServiceCollectionExtensions.cs`. Examples: `Features/Packaging/`, `Features/Harvesting/`, `Features/Preflight/`, `Features/LocalDev/` (the designated orchestration feature). |
| `Shared/` | Build-domain vocabulary — manifest models, runtime types, version mapping, package family conventions, cross-feature result primitives. No Cake dependencies, no I/O. |
| `Tools/` | Cake `Tool<TSettings>` wrappers ONLY (vcpkg, dumpbin, ldd, otool, tar, cmake, native-smoke) |
| `Integrations/` | Non-Cake-Tool external adapters: NuGet protocol client, dotnet pack invoker, project metadata reader, coverage XML readers, vcpkg manifest reader, MSVC environment resolver |

> **Status note (mid-migration).** Code currently carries the ADR-002 layered shape (`Application/<Module>/`, `Domain/<Module>/`, `Infrastructure/<Module>/`, `Tasks/<Module>/`, `Context/`) until P1/P2 waves complete. See [`docs/phases/phase-x-build-host-modernization-2026-05-02.md`](docs/phases/phase-x-build-host-modernization-2026-05-02.md) for current wave status. Reference shapes below describe the **target state**.

Reference shapes for new build-host work:

- **Task layer is thin**: build a feature-specific `Request` DTO from `BuildContext` + configuration, delegate to `pipeline.RunAsync(request)`. The Task body is one line of orchestration delegation. Golden example: `PackageTask`.
- **Pipeline classes are size-triggered.** Below ~200 LOC the logic stays in the Task with private methods. Above it, extract to `<X>Pipeline.cs` co-located in the feature folder (smell threshold, not hard rule — ADR-004 §2.4).
- **`BuildContext` is invocation state, not service locator.** Pipelines target `RunAsync(TRequest)`; pure services take explicit inputs only; Tools and Integrations may take narrow Cake abstractions (`ICakeContext`, `ICakeLog`, `IFileSystem`) but never the full `BuildContext`. See ADR-004 §2.11.
- **Interface discipline (ADR-004 §2.9):** keep an interface only if (1) multiple production implementations exist, (2) it formalizes an independent axis of change, or (3) it backs a high-cost test seam (transitional). Mocks alone don't justify a seam — prefer `internal sealed class` registered concrete via `Features/<X>/ServiceCollectionExtensions.cs`.
- **Cross-feature data sharing flows through `Shared/`.** Code-level cross-feature references are forbidden by `ArchitectureTests` invariant #4, with one allowlist exception for `Features/LocalDev/`.
- **`Shared/Results/` is for cross-feature primitives only.** Feature-specific `*Error` / `*Result` types stay in their feature folder (ADR-004 §2.6.1).
- **Typed result boundaries**: services return `OneOf`-shaped results; tasks translate them into Cake logging, `CakeException`, RID-status persistence, or cancellation.
- **When in doubt**, compare against ADR-004 §2.3 reference layout (Packaging) or §2.5 (LocalDev).

### Configuration topology

```text
vcpkg.json                    ← What vcpkg builds (deps + features)
    ↕ must match
build/manifest.json           ← Single source of truth, schema v2.1
    ├── packaging_config      ← validation mode, core library
    ├── runtimes[]            ← RID ↔ triplet ↔ strategy ↔ CI runner ↔ container image
    ├── package_families[]    ← family identity (managed_project, native_project, library_ref, depends_on, change_paths)
    ├── system_exclusions     ← OS libraries excluded from packages
    └── library_manifests[]   ← library versions, binary patterns
    ↕ validated by
PreFlightCheckTask            ← G14/G15/G16/G49/G54/G58 + family-scope guardrails
```

Legacy `runtimes.json` and `system_artefacts.json` were merged into `manifest.json` schema v2.1 — treat any reference to them as stale.

### Target platforms (sourced from `manifest.runtimes[]`)

7 RIDs, all hybrid-static: `win-{x64,x86,arm64}`, `linux-{x64,arm64}`, `osx-{x64,arm64}`. Linux jobs use the GHCR-hosted `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest` builder image.

## Project Conventions

- **TFMs are centralized in [Directory.Build.props](Directory.Build.props)**: `$(LibraryTargetFrameworks) = net9.0;net8.0;netstandard2.0;net462`, `$(ExecutableTargetFrameworks) = net9.0;net8.0;net462`. net462 builds on Linux via `Microsoft.NETFramework.ReferenceAssemblies` (no Mono needed for compile).
- **Central package management** via [Directory.Packages.props](Directory.Packages.props) (`<ManagePackageVersionsCentrally>true`). Lock files are enabled on the build host (strict mode under CI) and on `src/**` projects (lenient — absorbs SDK-implicit-package drift).
- **Analysis is strict**: `TreatWarningsAsErrors=true`, `AnalysisMode=All`, latest-feature SDK pin in [global.json](global.json) (9.0.200). Suppressions go in csproj or with file-scoped `#pragma`, not by lowering the global bar.
- **Test naming** (TUnit): `<MethodName>_Should_<Verb>_<optional When/If/Given>` — PascalCase method name, underscores between every other word segment, `Should` always present (e.g. `IsSystemFile_Should_Return_True_When_Windows_System_Dll`).
- **Test folders mirror production**: `Unit/Domain/Packaging/PackageVersionResolverTests.cs` asserts the contract of `Domain/Packaging/PackageVersionResolver.cs`. Integration tests live under `Integration/<Scenario>/` and are not mirrored.
- **Living docs rule**: if a code change shifts behavior / topology / infrastructure, update `docs/plan.md` or the active phase doc in the same change. Docs are first-class artifacts.

## Branch Out To

| Task | Doc |
| --- | --- |
| Build / harvest internals | [docs/knowledge-base/harvesting-process.md](docs/knowledge-base/harvesting-process.md), [docs/knowledge-base/cake-build-architecture.md](docs/knowledge-base/cake-build-architecture.md) |
| CI/CD pipeline | [docs/knowledge-base/ci-cd-packaging-and-release-plan.md](docs/knowledge-base/ci-cd-packaging-and-release-plan.md) |
| Release lifecycle (provider/scope/version axes) | [docs/decisions/2026-04-20-release-lifecycle-orchestration.md](docs/decisions/2026-04-20-release-lifecycle-orchestration.md) |
| Local dev recipes | [docs/playbook/local-development.md](docs/playbook/local-development.md) |
| Adding a new SDL library | [docs/playbook/adding-new-library.md](docs/playbook/adding-new-library.md) |
| vcpkg overlay triplets / ports (critical for build work) | [docs/playbook/overlay-management.md](docs/playbook/overlay-management.md) |
| Native smoke (C++ runtime validation) | [tests/smoke-tests/native-smoke/README.md](tests/smoke-tests/native-smoke/README.md) |
