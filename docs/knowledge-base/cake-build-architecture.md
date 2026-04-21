# Knowledge Base: Cake Frosting Build Architecture

> Deep technical reference for the Cake Frosting build system in `build/_build/`.
>
> **Layering update (2026-04-19):** The build host is now DDD-layered per [ADR-002](../decisions/2026-04-19-ddd-layering-build-host.md). The historical `Modules/` + `Tools/` directory layout described below has been superseded by `Application/<Module>/` + `Domain/<Module>/` + `Infrastructure/<Module>/` with `Tasks/` retained as the Cake-native presentation layer and `Infrastructure/Tools/` hosting Cake `Tool<T>` / `Aliases` / `Settings` wrappers. Interface discipline is formalized (three criteria) and enforced by `LayerDependencyTests`. Where this document still refers to `Modules/*` paths for legacy examples, the mental mapping is: "Models and domain services → `Domain/<Module>/`, use-case orchestrators → `Application/<Module>/`, external-system adapters → `Infrastructure/<Module>/`." Contributor on-ramp: [AGENTS.md § Build-Host Reference Pattern](../../AGENTS.md) + [docs/onboarding.md repo tree](../onboarding.md).

## Overview

The build system is a .NET 9.0 console application using **Cake Frosting v6.1.0**. It orchestrates the native binary harvesting pipeline — collecting compiled SDL2/SDL3 libraries and their transitive dependencies from vcpkg output and organizing them for NuGet packaging.

## Current Implementation Notes

- Active harvest logic lives under `Tasks/Harvest/`.
- Harvesting is the current build-host reference standard for task/service boundaries: tasks keep `BuildContext`, services take explicit inputs, and Cake capabilities are injected where they are actually used. Thin-task target remains `Tasks -> Application`; the current architecture test still tolerates direct Task -> Domain / Infrastructure interfaces for legacy holdovers, but that allowance is transitional and should not be copied into new work.
- `PreFlightCheckTask` is implemented in the build host, but the release-candidate workflow does not invoke it yet.
- `PreFlightCheckTask` has since been aligned closer to the Harvesting pattern: DI-loaded `ManifestConfig`, explicit validators, typed validator results, `IVcpkgManifestReader`, `IStrategyResolver`, and a reporter that owns Cake context instead of taking `ICakeLog` through every public method.
- `CoverageCheckTask` keeps path resolution and task-level failure policy, but the pass/fail decision now lives behind an injectable `ICoverageThresholdValidator` instead of a static helper call.
- `PathService` already exposes `harvest-staging` helpers for future distributed CI, but current tasks and workflows still write to `artifacts/harvest_output/`.
- Native-source acquisition mode selection is intentionally deferred from the active CLI surface.
- The build host still uses hand-written `OneOf` result wrappers. Source-generator-based cleanup remains a parked follow-up, not active build-system behavior.
- **Packaging flow (Stream D-local, S1 shape, 2026-04-17)** spans `Tasks/Packaging/`, `Application/Packaging/`, `Domain/Packaging/`, and `Infrastructure/DotNet/`. It follows the Harvesting reference pattern: thin tasks (`PackageTask`, `PackageConsumerSmokeTask`, `PostFlightTask`) + narrow services (`PackageTaskRunner`, `DotNetPackInvoker`, `PackageFamilySelector`, `PackageVersionResolver`, `ProjectMetadataReader`, `PackageOutputValidator`, `PackageConsumerSmokeRunner`) + typed Results with the full `OneOf.Monads` surface (implicit/explicit operators + `From*`/`To*` factories). Every service returns a typed `Result<PackagingError, T>` instead of throwing. `PackageOutputValidator` accumulates all guardrail observations (G21–G23, G25–G27, G47, G48) into a single `PackageValidation` aggregate so operators see the complete failure set, not first-throw-wins. 3-platform validated for the `sdl2-core` + `sdl2-image` slice on `win-x64` / `linux-x64` / `osx-x64`; PA-2 later moved all 7 manifest runtime rows onto hybrid triplets, but the four newly-covered rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) remain unexercised on the pack / consumer path (see [phase-2-adaptation-plan.md "Strategy & Tool Landing State"](../phases/phase-2-adaptation-plan.md#strategy--tool-landing-state)).

## Strategy Layer Reality Check

The strategy seam landed with Stream B (#85 closed) and the #85 handoff note in [plan.md](../plan.md) describes it as "strategy primitives + runtime wiring landed." That is technically correct but easy to misread. Before assuming the strategy layer does anything more than it does, read [`phases/phase-2-adaptation-plan.md` "Strategy & Tool Landing State"](../phases/phase-2-adaptation-plan.md#strategy--tool-landing-state) for the interface-level landing state. Quick summary:

- `IPackagingStrategy` is a one-method lookup helper (`IsCoreLibrary`), not a dispatcher. The Packaging module does not consume it.
- `IDependencyPolicyValidator` has one real implementation (`HybridStaticValidator`) and one intentional pass-through (`PureDynamicValidator` — by design per [`research/cake-strategy-implementation-brief-2026-04-14.md`](../research/cake-strategy-implementation-brief-2026-04-14.md)).
- `INativeAcquisitionStrategy` was designed in the brief but never implemented; ADR-001's Artifact Source Profile abstraction (`SetupLocalDev --source=local|remote`) now covers that problem space from the feed-preparation side.
- `IPayloadLayoutPolicy` was deferred in the brief "until PackageTask lands"; PackageTask landed, the policy extraction did not follow.

Behavioral dispatch between `hybrid-static` and `pure-dynamic` in the current code is limited to: (1) which `IDependencyPolicyValidator` instance DI resolves at harvest time; (2) `PreFlightCheckTask`'s declarative triplet↔strategy coherence. Everything downstream (pack, smoke, validator, deployer) is strategy-agnostic. After PA-2 (2026-04-18), no live `manifest.runtimes[]` row currently uses `pure-dynamic`, but the fallback code path still exists.

## Scanner Repurposing + Strategy-Aware Guardrails

The existing runtime scanners (`WindowsDumpbinScanner`, `LinuxLddScanner`, `MacOtoolScanner`) were built for a single purpose — **binary dependency discovery** consumed by `BinaryClosureWalker` during harvest. Post-strategy wiring they gained a second role as **packaging guardrail input** with zero scanner-code changes. This "same scanner, second consumer" move is the thesis of [`research/cake-strategy-implementation-brief-2026-04-14.md`](../research/cake-strategy-implementation-brief-2026-04-14.md) §"Scanner Repurposing" and is fully realized end to end.

### Before (pure-dynamic era)

```text
Scanner (dumpbin/ldd/otool)
    └─> BinaryClosure
            └─> ArtifactPlanner → ArtifactDeployer
```

One producer, one consumer. The scanner output described "what this binary depends on at runtime"; nothing validated whether the closure should be acceptable.

### After (hybrid-static era)

```text
Scanner (dumpbin/ldd/otool)
    └─> BinaryClosure ─┬─> ArtifactPlanner → ArtifactDeployer          (original role, preserved)
                       └─> HybridStaticValidator → G19 leak detection   (new second consumer)
```

`HybridStaticValidator` (`build/_build/Domain/Strategy/HybridStaticValidator.cs`) consumes the same `BinaryClosure`, filters nodes by the `IRuntimeProfile.IsSystemFile` + `IPackagingStrategy.IsCoreLibrary` + `closure.IsPrimaryFile` rules, and fails on any remaining node — that is a transitive dependency leak (the static bake missed a library). Runs per-RID during `HarvestTask`.

### Complementary harvest-shape assertion

Strategy-agnostic but same "post-deployment sanity check" discipline: `HarvestTask` asserts `DeploymentStatistics.PrimaryFiles.Count >= 1` after the deployer runs. If the closure walker and planner returned success shapes but zero primary binaries landed, the task fails loud. This catches silent feature-flag degradation / partial vcpkg install shapes that the closure walker's success code wouldn't otherwise surface. Tracked as **G50** in `release-guardrails.md`.

### Why this matters architecturally

The strategy pattern did not require rewriting the scanners. The scanners expose `BinaryClosure` as domain data; the strategy layer attached a new rule engine to that data. This is the "open-closed" move — adding behavior via new consumers of stable producers rather than by modifying producers. When reviewing code that claims "strategy-aware" behavior, verify the mechanism fits this shape: is the new behavior a rule engine attached to existing domain data, or is it a fork inside the producer? If the latter, that's a smell; the former is the pattern.

### Guardrail anchors

- **G19** — Hybrid-static strategy: zero transitive dep leaks in harvest output. Owned by `HybridStaticValidator`. Fires per-RID during Harvest stage.
- **G50** — Harvest must produce ≥1 primary binary per library+RID. Owned by `HarvestTask` post-deploy assertion. Strategy-agnostic.
- **G16** — Runtime strategy coherence: `manifest.runtimes[].strategy` agrees with the declared triplet. Owned by `StrategyCoherenceValidator`. Fires during PreFlight.

Under ADR-003 §4 stage-owned validation model, G19 and G50 are Harvest-stage guardrails; G16 is a PreFlight-stage guardrail.

## Pipeline Stage Architecture (ADR-003)

[ADR-003](../decisions/2026-04-20-release-lifecycle-orchestration.md) formalizes the release pipeline as a sequence of stage-owned targets, each with an explicit input shape and an owned set of validators. **This section describes the target shape; the implementation is pseudocode at ADR level and lands in the post-sweep Cake refactor pass.**

### Stage sequence

```text
Resolve versions (single-runner, pre-pipeline) ─ build-host entrypoint
      ↓
PreFlight (single-runner, fail-fast) ─────────── structural + version-aware validation
      ↓
Harvest (N-RID matrix) ─────────────────────────── per-RID binary closure + hybrid leak guardrails
      ↓
NativeSmoke (N-RID matrix) ─────────────────────── per-RID native binaries load / init
      ↓
ConsolidateHarvest (single-runner) ─────────────── aggregation across RID statuses
      ↓
Pack (single-runner) ───────────────────────────── nupkg emission + post-pack guardrails
      ↓
ConsumerSmoke (N-RID matrix RE-ENTRY) ──────────── per-RID managed/runtime restore + init
      ↓
PublishStaging → PublishPublic (single-runner each)
```

### Per-stage request shapes (ADR-003 §3.2, implementation pending)

Each stage owns its own request record; there is no monolithic `PipelineRequest`. Shape examples (subject to impl-pass revision):

- `PreflightRequest` — manifest + resolved version mapping (always resolved; see §2.3 of ADR-003)
- `HarvestRequest` — rid + library set + vcpkg config
- `NativeSmokeRequest` — rid + harvest output location
- `ConsolidateHarvestRequest` — successful rid list + output root
- `PackRequest` — resolved version mapping + consolidated harvest + packages dir
- `PackageConsumerSmokeRequest` — rid + resolved version mapping + feed location
- `PublishRequest` — packages + feed target + auth

### Version source providers (ADR-003 §3.1, implementation pending)

Version resolution is abstracted behind `IPackageVersionProvider` with three implementations:

- `ManifestVersionProvider` — reads `manifest.json library_manifests[].vcpkg_version` per family + injected suffix (used by local-dev + CI workflow_dispatch `mode=manifest-derived`)
- `GitTagVersionProvider` — reads `sdl<major>-<role>-<semver>` family tags at the invocation commit; supports single-family (targeted release) and multi-family (full-train meta-tag) modes
- `ExplicitVersionProvider` — operator-supplied per-family mapping (CI workflow_dispatch `mode=explicit` or PD-8 manual escape)

Providers are **service-only, not Cake CLI targets**. Resolution happens exactly once per invocation; the mapping is immutable downstream (see ADR-003 §2.4). Caveat: the invariant scopes to CI job-chain runs and composite Cake targets; operator-driven ad-hoc target sequencing is each-invocation-resolves-independently.

### Stage-owned validation

Each guardrail belongs to exactly one stage; there is no monolithic "PostFlight" validator suite. Canonical mapping:

- **PreFlight:** G14/G15 (version consistency), G16 (strategy coherence), G4/G6/G7/G17/G18 (csproj pack contract), G49 (core library identity), G54 (upstream major.minor alignment).
- **Harvest:** G19 (hybrid leak), G50 (primary ≥ 1).
- **NativeSmoke:** native binaries load + initialize at OS level (C/C++ harness today; ADR-003 extracts as its own stage).
- **Pack:** G21–G23, G25–G27, G46 (empty-payload guard), G47 (`buildTransitive/` contract), G48 (per-RID native payload shape), G55 (native metadata file), G56 (satellite cross-family upper bound), G57 (README mapping table), G58 (cross-family dep resolvability — planned, placement in Pack per ADR-003 §8 Open Question 1).
- **ConsumerSmoke:** restore + runtime TUnit per-TFM pass.
- **Publish:** feed auth + deduplication.

See [release-guardrails.md](release-guardrails.md) for the full guardrail registry.

### SetupLocalDev composition (ADR-003 §3.3 Option A)

Post-sweep target retains its current Option A shape: `SetupLocalDevTask` is a thin Cake task over `IArtifactSourceResolver` (the public profile boundary). For the `Local` profile, `LocalArtifactSourceResolver.PrepareFeedAsync` may internally compose version providers + pack loops + stage-runner calls needed to materialize the feed, but that composition stays private to the resolver implementation. Internal composition goes through Application-layer runners injected via DI, not nested Cake target invocations. `RemoteInternal` / `ReleasePublic` profiles retain the same public seam even if their internal mechanisms differ.

See [ADR-001 §2.7–§2.8](../decisions/2026-04-18-versioning-d3seg.md) for the resolver contract and [ADR-003 §3.3](../decisions/2026-04-20-release-lifecycle-orchestration.md) for the Option A selection rationale.

## SDL2-CS Submodule Boundary (Transitional)

`external/sdl2-cs` is a git submodule pointing at a fork-compatible commit of `flibitijibibo/SDL2-CS`. It is **transitional and untrusted long-term** — the project will retire it in favour of an AST-driven binding generator (tracked under [`docs/plan.md` Phase 3 / 4 roadmap](../plan.md)). Until that retirement happens, two rules apply:

1. **Never patch the submodule working tree.** Even if an upstream wrapper bug bites a smoke test, the correct response is to write repo-local code (in the smoke test, in a wrapper, in a helper) that avoids the broken surface — not to carry local edits inside `external/sdl2-cs/`. Submodule patches rot under every upstream bump and blur the ownership boundary.
2. **Document broken upstream wrappers here, cross-reference from code.** When a smoke test scopes around a specific upstream defect, it should cite this section so future contributors don't re-discover the defect under deadline pressure.

### Known upstream defects (as of 2026-04-18)

Confirmed by direct inspection of the submodule worktree:

- `external/sdl2-cs/src/SDL2_mixer.cs:148` declares `[DllImport(nativeLibName, EntryPoint = "MIX_Linked_Version", ...)]`. The actual SDL2_mixer native export is `Mix_Linked_Version` (lowercase `ix`, matching every other `Mix_*` symbol). Calling the wrapper throws `EntryPointNotFoundException` against a correctly-built `SDL2_mixer.dll` / `libSDL2_mixer.so`.
- `external/sdl2-cs/src/SDL2_ttf.cs:77` declares `[DllImport(nativeLibName, EntryPoint = "TTF_LinkedVersion", ...)]` — missing the underscore between `Linked` and `Version`. The actual native export is `TTF_Linked_Version` per the SDL2_ttf header. Same `EntryPointNotFoundException` at call time.

Neither defect is tracked upstream at `flibitijibibo/SDL2-CS` (searched 2026-04-18). Two possible paths — both deferred by project decision:

- File a PR upstream. Low-risk community contribution; retired naturally when the AST generator replaces SDL2-CS.
- Wait for the AST generator to retire the whole submodule. Preferred per `docs/plan.md` roadmap direction.

**Repo-local impact:** [`tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageSmokeTests.cs`](../../tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageSmokeTests.cs) `Core_And_Image_Linked_Versions_Report_Expected_Majors` intentionally asserts only the wrapper methods that call correctly-named native symbols (`SDL.SDL_GetVersion`, `SDL_image.IMG_Linked_Version`). Mixer and TTF linked-version coverage is intentionally absent at the managed layer; the native-smoke (C) harness exercises the correct `Mix_Linked_Version` / `TTF_Linked_Version` symbols directly.

## Architecture

The build host follows ADR-002 DDD layering (`Tasks/Application/Domain/Infrastructure`).

```text
build/_build/
├── Program.cs              ← Entry point: CLI parsing, DI composition root, repo-root detection
├── Context/                ← BuildContext (Cake task boundary state)
├── Tasks/                  ← Presentation: Cake Frosting task classes
│   ├── Common/             ← InfoTask
│   ├── Coverage/           ← CoverageCheckTask
│   ├── Dependency/         ← Dumpbin / Ldd / Otool diagnostic wrappers
│   ├── Harvest/            ← HarvestTask, ConsolidateHarvestTask
│   ├── Packaging/          ← PackageTask, PackageConsumerSmokeTask, SetupLocalDevTask
│   ├── PostFlight/         ← PostFlightTask (PreFlight → Package → Smoke chain)
│   ├── Preflight/          ← PreFlightCheckTask
│   └── Vcpkg/              ← EnsureVcpkgDependenciesTask
├── Application/            ← Use-case orchestrators (TaskRunners, Resolvers, SmokeRunner)
│   ├── Common/             ← InfoTaskRunner
│   ├── Coverage/           ← CoverageCheckTaskRunner
│   ├── DependencyAnalysis/ ← OtoolAnalyzeTaskRunner
│   ├── Harvesting/         ← HarvestTaskRunner, ConsolidateHarvestTaskRunner, ArtifactPlanner, ArtifactDeployer, BinaryClosureWalker
│   ├── Packaging/          ← PackageTaskRunner, PackageConsumerSmokeRunner, LocalArtifactSourceResolver, resolver/strategy/validator factories
│   ├── Preflight/          ← PreflightTaskRunner, PreflightReporter
│   └── Vcpkg/              ← EnsureVcpkgDependenciesTaskRunner
├── Domain/                 ← Models, value objects, result types, domain services (no outward deps)
│   ├── Coverage/           ← CoverageThresholdValidator, metrics, baseline
│   ├── Harvesting/         ← PackageInfo, DeploymentPlan, BinaryClosure, HarvestJsonContract
│   ├── Packaging/          ← PackageOutputValidator, NativePackageMetadata, version/family resolvers, SmokeScopeComparator, SatelliteUpperBoundValidator
│   ├── Paths/              ← IPathService abstraction
│   ├── Preflight/          ← FamilyIdentifierConventions + guardrail validators (G4/G6/G7/G17/G18/G49/G54)
│   ├── Results/            ← BuildError, BuildResultExtensions, AsyncResultChaining helpers
│   ├── Runtime/            ← RuntimeProfile (RID + triplet + platform detection)
│   └── Strategy/           ← HybridStatic / PureDynamic strategies + validators + StrategyResolver
└── Infrastructure/         ← External-system adapters
    ├── Coverage/           ← CoberturaReader, CoverageBaselineReader
    ├── DependencyAnalysis/ ← WindowsDumpbinScanner, LinuxLddScanner, MacOtoolScanner
    ├── DotNet/             ← DotNetPackInvoker, ProjectMetadataReader
    ├── Paths/              ← PathService implementation
    ├── Tools/              ← Cake-native Tool<T> / Aliases / Settings wrappers (Vcpkg, Dumpbin, Ldd, Otool)
    └── Vcpkg/              ← VcpkgCliProvider, VcpkgManifestReader
```

Layer discipline (Tasks → Application → Domain, Infrastructure → Domain; Domain has no outward deps) is asserted by `build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs`. See [ADR-002](../decisions/2026-04-19-ddd-layering-build-host.md) for rationale and the temporary catchnet allowance for fat-task holdovers (HarvestTask, PreFlight) being reduced under Wave 6.

## Service Architecture (DI)

All services are registered via dependency injection in `Program.cs`:

| Service Interface | Implementation | Purpose |
| --- | --- | --- |
| `IPathService` | `PathService` | Resolves paths to manifest.json, output dirs |
| `IRuntimeProfile` | `RuntimeProfile` | Maps RID ↔ vcpkg triplet, detects current platform |
| `IPackageInfoProvider` | `VcpkgCliProvider` | Queries vcpkg for installed package metadata |
| `IBinaryClosureWalker` | `BinaryClosureWalker` | Two-stage graph walk: vcpkg metadata + runtime scan (dumpbin/ldd/otool) |
| `IArtifactPlanner` | `ArtifactPlanner` | Determines which binaries to include and how to deploy them |
| `IArtifactDeployer` | `ArtifactDeployer` | Copies binaries to output, creates tar.gz for Unix |
| `IRuntimeScanner` | Platform-specific | dumpbin (Windows), ldd (Linux), otool (macOS) |
| `IPackagingStrategy` | `HybridStaticStrategy` / `PureDynamicStrategy` | Packaging model and core-library interpretation, resolved per runtime strategy in DI |
| `IDependencyPolicyValidator` | `HybridStaticValidator` / `PureDynamicValidator` | Strategy-aware closure validation (hybrid leak enforcement, pure-dynamic pass-through) |
| `ICoberturaReader` | `CoberturaReader` | Parses cobertura XML (MTP `--coverage --coverage-output-format cobertura`) into aggregate `CoverageMetrics` |
| `ICoverageBaselineReader` | `CoverageBaselineReader` | Loads `build/coverage-baseline.json` into `CoverageBaseline` (line / branch floor + optional metadata) |
| `ICoverageThresholdValidator` | `CoverageThresholdValidator` | Applies the ratchet rule to parsed metrics and returns a typed coverage result |
| `IVcpkgManifestReader` | `VcpkgManifestReader` | Loads `vcpkg.json` into `VcpkgManifest` for PreFlight and future build-host consumers |
| `IArtifactSourceResolver` | `LocalArtifactSourceResolver` (RemoteInternal / ReleasePublic stubbed) | Feed-prep profile selection per ADR-001 §2.7. Subsumes the retired `INativeAcquisitionStrategy` design from the strategy brief — feed-prep abstraction replaces native-acquisition abstraction. |

## Reference Pattern: Harvesting First

When a build-host refactor needs precedent, compare the shape of the Harvesting module before inventing a new seam.

- `HarvestTask` keeps `BuildContext`, task-only policy, and user-facing failure behavior.
- `BinaryClosureWalker`, `ArtifactPlanner`, and `ArtifactDeployer` take narrower dependencies and explicit domain inputs.
- Service boundaries return typed domain results/errors instead of forcing exception-only flow everywhere.
- Rich domain models (`BinaryClosure`, `DeploymentPlan`, `DeploymentStatistics`) carry intent better than raw path collections.
- Tests mirror this split: whitebox module tests for the services, task tests for behavior and output contracts.

This is a reference pattern, not a claim that every line in Harvesting is perfect. The point is to copy the boundary discipline before copying any implementation detail.

Recent alignment examples:

- Coverage keeps file-path resolution in the task, parsing in readers, and the threshold rule in an injectable validator. The module stays intentionally small without letting the task own the core policy decision.
- PreFlight now uses typed validator result boundaries and a dedicated `IVcpkgManifestReader`, while the task retains user-facing reporting and Cake-facing failure policy.
- “Golden standard” in this repo means: copy Harvesting's architecture shape first, not its exact implementation details.

## Configuration Files

### manifest.json — Single Source of Truth (Schema v2.1)

All build configuration lives in a single file. Previously split across `manifest.json`, `runtimes.json`, and `system_artefacts.json` — now merged.

```json
{
  "schema_version": "2.1",
  "packaging_config": {
    "validation_mode": "strict",
    "core_library": "sdl2"
  },
  "runtimes": [
    { "rid": "win-x64", "triplet": "x64-windows-hybrid", "strategy": "hybrid-static", "runner": "windows-latest", "container_image": null }
  ],
  "package_families": [
    { "name": "core", "library_ref": "SDL2", "depends_on": [], "change_paths": ["src/SDL2.Core/**"] }
  ],
  "system_exclusions": {
    "windows": { "system_dlls": ["kernel32.dll", "user32.dll", "..."] },
    "linux": { "system_libraries": ["libc.so*", "libstdc++.so*", "..."] },
    "osx": { "system_libraries": ["libSystem.B.dylib", "Cocoa.framework", "..."] }
  },
  "library_manifests": [
    {
      "name": "SDL2",
      "vcpkg_name": "sdl2",
      "vcpkg_version": "2.32.10",
      "vcpkg_port_version": 0,
      "native_lib_name": "SDL2.Core.Native",
      "core_lib": true,
      "primary_binaries": [
        { "os": "Windows", "patterns": ["SDL2.dll"] },
        { "os": "Linux", "patterns": ["libSDL2*"] },
        { "os": "OSX", "patterns": ["libSDL2*.dylib"] }
      ]
    }
  ]
}
```

> **Schema change 2026-04-18 (ADR-001):** the `native_lib_version` field was removed from `library_manifests[]`. Under D-3seg, family version is git-tag-derived (MinVer), not manifest-declared. Exact upstream patch + port_version are recorded in the packed `janset-native-metadata.json` (G55) and README mapping table (G57). See [ADR-001 §2.5](../decisions/2026-04-18-versioning-d3seg.md).

Key sections:

- `packaging_config`: Validation mode and core library identification
- `runtimes[]`: RID ↔ triplet ↔ strategy ↔ CI runner mapping. Triplet = strategy authority; the `strategy` field is a formal declaration validated by PreFlightCheck
- `package_families[]`: family metadata for packaging/release orchestration
- `system_exclusions`: OS-level libraries that must NOT be bundled (used by `RuntimeProfile.IsSystemFile()`)
- `library_manifests[]`: Library definitions with vcpkg name/version and binary patterns
- `core_lib`: If true, this library's binary appears in other packages too (SDL2.dll in Image, Mixer, etc.)

For the full merged schema, see [research/cake-strategy-implementation-brief-2026-04-14.md](../research/cake-strategy-implementation-brief-2026-04-14.md).

## Task Pipeline

### HarvestTask

The core task that collects native binaries for a specific library and RID.

**Arguments**:

- `--library`: Library name(s) from manifest.json (e.g., `SDL2`, `SDL2_image`)
- `--rid`: Runtime identifier (e.g., `win-x64`, `linux-x64`)

**Pipeline per library**:

```text
1. Load manifest.json entry for library
2. Resolve vcpkg install path for triplet
3. Find primary binary using manifest patterns
4. IBinaryClosureWalker: Recursively scan dependencies
   ├── Windows: dumpbin /dependents → filter system_artefacts
   ├── Linux: ldd → filter system_artefacts
   └── macOS: otool -L → filter system_artefacts
5. IArtifactPlanner: Build deployment plan
   ├── Classify each file (primary, dependency, system-excluded)
   ├── Determine deployment strategy (direct copy vs archive)
   └── Generate DeploymentPlan model
6. IArtifactDeployer: Execute deployment
   ├── Windows: Direct file copy to runtimes/{rid}/native/
   ├── Linux/macOS: Create tar.gz preserving symlinks
   └── Generate per-RID status JSON
```

**Output**:

- Status JSON: `artifacts/harvest_output/{Library}/rid-status/{rid}.json`
- Native payload: `artifacts/harvest_output/{Library}/runtimes/{rid}/native/`

### ConsolidateHarvestTask

Merges per-RID status files into library-wide manifests.

**Input**: All `{rid}.json` files from `artifacts/harvest_output/{Library}/rid-status/`
**Output**: `harvest-manifest.json` + `harvest-summary.json`

## Writing Build-Host Tests

### Hermetic Task Tests: `FakeRepoBuilder`

Task-level tests in `build/_build.Tests/` should model repo state through the Cake-native fake filesystem, not temp directories or `System.IO.File.*` calls.

Canonical entry point: `build/_build.Tests/Fixtures/FakeRepoBuilder.cs`

What it provides:

- Fake repo root + `FakeFileSystem` + `FakeEnvironment`
- Real `PathService` topology over fake paths, so tests exercise the same semantic path model as production code
- Fluent writers for common repo artifacts: `manifest.json`, `vcpkg.json`, coverage baseline, cobertura report, harvest RID status files
- Async read helpers on the returned handles for output assertions (`ReadAllTextAsync`)
- Optional `VcpkgInstalledFake` layout builder for future tests that need a fake `vcpkg_installed/<triplet>/...` tree

Pattern:

```csharp
var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
  .WithManifest(manifest)
  .WithVcpkgJson(vcpkgManifest)
  .BuildContextWithHandles();

var task = new PreFlightCheckTask(
  manifest,
  new VersionConsistencyValidator(),
  new StrategyCoherenceValidator(new StrategyResolver()),
  new PreflightReporter(repo.BuildContext));
task.Run(repo.BuildContext);

await Assert.That(repo.Exists("artifacts/harvest_output/SDL2/rid-status/win-x64.json")).IsTrue();
```

### Real-Repo Characterization Tests: `WorkspaceFiles`

Characterization tests that intentionally inspect committed repo files (`build/manifest.json`, `vcpkg.json`) should still avoid `System.IO.File.*` and `System.IO.Directory.*` directly.

Canonical entry point: `build/_build.Tests/Fixtures/WorkspaceFiles.cs`

Use it to:

- Resolve the workspace repo root from `AppContext.BaseDirectory`
- Read committed files through Cake's physical `FileSystem`
- Keep the "real repo contract" intent while preserving the build host's Cake-native I/O discipline

### Production I/O Rule For Testability

If a build-host task or helper must read or write repo files that task tests need to fake, route that I/O through Cake abstractions.

Current canonical helpers live in `build/_build/Context/CakeExtensions.cs`:

- `ToJson<TModel>()` / `ToJsonAsync<TModel>()`
- `ReadAllTextAsync()`
- `WriteAllTextAsync()`

If new production code reaches for `System.IO.File.*` directly, it will likely bypass `FakeFileSystem` and force tests back onto real disk. That is considered regression territory for the build-host test infra.

### PreFlightCheckTask

Validates configuration consistency before builds (partial gate).

**Checks**:

- manifest.json library versions match vcpkg.json override versions
- Port versions match
- Runtime strategy coherence (`runtimes[].strategy` vs triplet-derived model)

**Out of scope (deferred to Stream C):** package-family integrity, dynamic CI matrix gating, and CI artifact-flow checks.

## Binary Closure Walking

The most complex part of the build system. Each platform uses different tools:

### Windows (dumpbin)

```text
dumpbin /dependents SDL2.dll
→ Lists: SDL2.dll depends on kernel32.dll, user32.dll, vcruntime140.dll, ...
→ Filter system_artefacts.json
→ Recursively scan remaining dependencies
```

Resolution note (current implementation):

- `DumpbinTool` first checks `VCToolsInstallDir` (Developer PowerShell/Developer Command Prompt scenario)
- then falls back to `vswhere` with `Microsoft.VisualStudio.Component.VC.Tools.x86.x64`
- then probes MSVC Host/Target bin combinations for `dumpbin.exe`

### Linux (ldd)

```text
ldd libSDL2-2.0.so.0
→ Lists: libSDL2-2.0.so.0 => /usr/lib/x86_64-linux-gnu/libSDL2-2.0.so.0
→ Filter system_artefacts.json
→ Handle symlink chains (libSDL2.so → libSDL2-2.0.so.0 → libSDL2-2.0.so.0.3200.4)
→ Recursively scan remaining dependencies
```

### macOS (otool)

```text
otool -L libSDL2.dylib
→ Lists: @rpath/libSDL2.dylib, /usr/lib/libSystem.B.dylib, ...
→ Filter system_artefacts.json and framework references
→ Handle @rpath, @loader_path references
→ Recursively scan remaining dependencies
```

## Output Structure

```text
artifacts/
├── harvest_output/
│   ├── SDL2/
│   │   ├── rid-status/
│   │   │   ├── win-x64.json
│   │   │   ├── linux-x64.json
│   │   │   └── osx-arm64.json
│   │   ├── harvest-manifest.json    ← Generated by ConsolidateHarvestTask
│   │   └── harvest-summary.json     ← Human-readable summary
│   ├── SDL2_image/
│   │   └── rid-status/
│   │       └── ...
│   └── ...
└── packages/                         ← Future: PackageTask output
    └── ...
```

## Extending the Build System

### Adding a New Task

1. Create a new class in `Tasks/` inheriting from `FrostingTask<BuildContext>`
2. Add `[TaskName("YourTask")]` attribute
3. Add `[IsDependentOn(typeof(...))]` for dependencies
4. Register any new services in the DI module

### Adding a New Platform

1. Update `manifest.json` (schema v2.1): add a new `runtimes[]` entry with RID → triplet → strategy → runner → container_image mapping.
2. Ensure `IRuntimeScanner` / `BinaryClosureWalker` handles the new platform's dependency scanning tool (extend the DI-time RID switch in `Program.cs` if a new OS family appears).
3. Update `manifest.json system_exclusions[]` with the platform's system libraries.
4. Author a matching vcpkg overlay triplet under `vcpkg-overlay-triplets/` if the platform is a new hybrid-static target.
5. Add CI workflow (or expand dynamic matrix — see ADR-003 §3.4) for the new platform.

## Historical Note

The original build plan has been retired after migration into the current docs set. The current implementation still follows that earlier harvest-pipeline direction, but CI/CD and packaging details have evolved and should now be read from the active docs instead.

For repo-specific tradeoffs and architecture-review carry-over, see [design-decisions-and-tradeoffs.md](design-decisions-and-tradeoffs.md). For general Cake Frosting working patterns trimmed from the deep reference, see [cake-frosting-patterns.md](../playbook/cake-frosting-patterns.md), [cake-frosting-host-organization.md](../playbook/cake-frosting-host-organization.md), and [cake-frosting-build-expertise.md](../reference/cake-frosting-build-expertise.md).
