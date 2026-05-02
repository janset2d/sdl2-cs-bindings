# Knowledge Base: Cake Frosting Build Architecture

> Deep technical reference for the Cake Frosting build system in `build/_build/`.
>
> **Architecture status (2026-05-02 + Adım 13):** the build host implements [ADR-004 — Cake-native feature-oriented architecture](../decisions/2026-05-02-cake-native-feature-architecture.md), which supersedes ADR-002 (DDD layering, 2026-04-19). Five top-level folders own all production code:
>
> - **`Host/`** — Cake/Frosting runtime, CLI parsing, `BuildContext`, composition root, paths, Cake extensions.
> - **`Features/<X>/`** — operational vertical slices (Ci, Coverage, DependencyAnalysis, Diagnostics, Harvesting, Info, **LocalDev**, Maintenance, Packaging, Preflight, Publishing, Vcpkg, Versioning). Each feature owns its Cake `Task`, size-triggered `Pipeline` (or `Flow` in LocalDev's case), validators, generators, `Request` DTOs, and a per-feature `ServiceCollectionExtensions.AddXFeature()`.
> - **`Shared/`** — build-domain vocabulary (manifest models, runtime types, version mapping, package family conventions, cross-feature result primitives — `Shared/Harvesting`, `Shared/Coverage`, `Shared/Packaging`, `Shared/Versioning`, `Shared/Strategy`, `Shared/Manifest`, `Shared/Runtime`, `Shared/Results`). **No Cake dependencies, no I/O.**
> - **`Tools/`** — Cake `Tool<TSettings>` wrappers ONLY (vcpkg, dumpbin, ldd, otool, tar, native-smoke).
> - **`Integrations/`** — non-Cake-Tool external adapters (NuGet protocol client, dotnet pack invoker, project metadata reader, coverage XML readers, vcpkg manifest reader, MSVC env resolver, dependency-analysis scanners).
>
> Direction-of-dependency invariants are asserted by [`ArchitectureTests`](../../build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs) (renamed from `LayerDependencyTests` at the P2 wave); the five invariants are listed in [ADR-004 §2.13](../decisions/2026-05-02-cake-native-feature-architecture.md#213-architecturetests-direction-of-dependency-invariants). One permanent named exception is documented inline: `Integrations/{DotNet,Vcpkg} → Host.Paths.IPathService` — `IPathService` is the canonical Host-tier path abstraction that Integrations adapters may consume (see phase-x §14.5). Contributor on-ramp: [AGENTS.md § Build-Host Reference Pattern](../../AGENTS.md) + [docs/onboarding.md repo tree](../onboarding.md).

## Overview

The build system is a .NET 9.0 console application using **Cake Frosting v6.1.0**. It orchestrates the native binary harvesting pipeline — collecting compiled SDL2/SDL3 libraries and their transitive dependencies from vcpkg output and organizing them for NuGet packaging.

## Current Implementation Notes

- The build host follows the Cake-native feature-oriented shape per ADR-004. Active harvest logic lives under `Features/Harvesting/`; packaging under `Features/Packaging/`; publishing under `Features/Publishing/`; and so on for the 13 features enumerated in the header.
- **Task layer is thin** by ADR-004 §2.4: each `<X>Task` in a feature folder is a Cake `FrostingTask<BuildContext>` adapter that builds a `Request` DTO from `BuildContext` + the feature's configuration sub-record and delegates to a co-located `<X>Pipeline`. Reference example: `Features/Packaging/PackageTask.cs`. Pipeline classes are extracted from the Task only when they exceed ~200 LOC (smell threshold, not a hard rule per §2.4).
- **Harvesting remains the build-host reference pattern** for service boundaries: pipeline takes `Request` DTOs only (ADR-004 §2.11.1, closed at P4-A), validators / planners / deployers take explicit inputs, Cake capabilities are injected via constructor DI. Tests mirror the split (whitebox unit tests for the services, task-shape tests for behavior contracts).
- **`BuildContext` is invocation state, not a service locator** (ADR-004 §2.11). The four properties it carries are `Paths`, `Runtime`, `Manifest`, `Options`. Pipelines accept `(BuildContext context, TRequest request, CancellationToken ct)` today; the cut-over to `RunAsync(TRequest, CancellationToken)` is a P4 deliverable. Pure services (validators, generators, planners, readers) take explicit inputs only — never a full `BuildContext`. Tools and Integrations may take narrow Cake abstractions (`ICakeContext`, `ICakeLog`, `IFileSystem`) but never `BuildContext` itself.
- **LocalDev is the designated multi-feature orchestration slice** per ADR-004 §2.5 + §2.13 invariant #4 allowlist. `Features/LocalDev/SetupLocalDevFlow` composes `EnsureVcpkgDependenciesPipeline`, `PreflightPipeline`, `HarvestPipeline`, `ConsolidateHarvestPipeline`, `IPackagePipeline`, `IPackageConsumerSmokePipeline`, `ManifestVersionProvider`, and `IArtifactSourceResolver` for the `--source=local` profile. Adding a second orchestration feature requires editing the allowlist in [`ArchitectureTests`](../../build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs) **and** ADR-004.
- **Per-feature `ServiceCollectionExtensions.AddXFeature()`** (ADR-004 §2.12 composition-root architectural index): each `Features/<X>/` folder ships a `ServiceCollectionExtensions` static class whose extension method registers that feature's pipelines, validators, generators, and factory closures on `IServiceCollection`. `Program.cs ConfigureBuildServices` reads as 3 cross-cutting group calls (`AddHostBuildingBlocks`, `AddIntegrations`, `AddToolWrappers`) plus a 13-call feature roster. LocalDev is registered last so every sibling pipeline it composes is already in the container.
- **DI smoke per feature**: [`Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs`](../../build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs) seeds a `ServiceCollection` via [`Fixtures/TestHostFixture.AddTestHostBuildingBlocks`](../../build/_build.Tests/Fixtures/TestHostFixture.cs), invokes a single `AddXFeature()` (plus its declared cross-feature dependencies), and asserts every descriptor the feature added resolves through `provider.GetService(...)` without throwing. Catches missing-transitive-dep / mistyped-factory regressions at CI gate time without bootstrapping Cake.
- **`OneOf` result discipline**: every pipeline / validator / reader / planner returns a typed `Result<TError, TSuccess>` from the `OneOf.Monads` surface (implicit/explicit operators + `From*`/`To*` factories) instead of throwing. Cross-feature void-marker `Unit` lives in `Shared/Results/`; per-feature error hierarchies derive from `Shared/Results/BuildError`.
- `PreflightPipeline` is the pre-matrix fail-fast stage (`release.yml preflight` job). `CoverageCheckPipeline` keeps Cake-side path resolution + failure policy in the Task; the pass/fail decision lives in the concrete `CoverageThresholdValidator`. `PathService` already exposes `harvest-staging` helpers for future distributed CI; current tasks and workflows still write to `artifacts/harvest_output/`.
- **Packaging flow** spans `Features/Packaging/`, `Features/Publishing/`, `Shared/Packaging/`, and `Integrations/DotNet/`: thin tasks (`PackageTask`, `PackageConsumerSmokeTask`, `SetupLocalDevTask`, `PublishStagingTask`, `PublishPublicTask`) + size-triggered pipelines (`PackagePipeline`, `PackageConsumerSmokePipeline`, `SetupLocalDevFlow`, `PublishPipeline`) + narrow services (`DotNetPackInvoker`, `ProjectMetadataReader`, `PackageOutputValidator`, `G58CrossFamilyDepResolvabilityValidator`, `FamilyTopologyHelpers`). `PackageOutputValidator` accumulates all post-pack observations (G21–G23, G25–G27, G46–G48, G55–G58) into a single `PackageValidation` aggregate so operators see the complete failure set, not first-throw-wins. **All 7 manifest runtime rows** green end-to-end via `release.yml` — Pack ✓ + ConsumerSmoke ✓ on master `8ec85c5` (CI run 24938451364, 2026-04-26). `PostFlightTask` retired in Slice B2 (2026-04-21) — semantics absorbed into `SetupLocalDevFlow` (Flow 1) + standalone `Package` + `PackageConsumerSmoke` (Flow 2) per ADR-003 §3.3 + cross-platform-smoke-validation.md §K. `PublishStagingTask` is live for the internal GitHub Packages staging feed; dispatch is checkbox-gated and release tags publish after trigger-aware `ResolveVersions` routing. `PublishPublicTask` remains a Phase 2b PD-7 stub pending nuget.org Trusted Publishing OIDC.

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

`HybridStaticValidator` (`build/_build/Shared/Strategy/HybridStaticValidator.cs`) consumes the same `BinaryClosure` (now under `build/_build/Shared/Harvesting/`), filters nodes by the `IRuntimeProfile.IsSystemFile` + `IPackagingStrategy.IsCoreLibrary` + `closure.IsPrimaryFile` rules, and fails on any remaining node — that is a transitive dependency leak (the static bake missed a library). Runs per-RID during `HarvestPipeline`.

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

[ADR-003](../decisions/2026-04-20-release-lifecycle-orchestration.md) formalizes the release pipeline as a sequence of stage-owned targets, each with an explicit input shape and an owned set of validators. **Implementation landed across Slices A→C (pass-1 merged at `bfc6713` 2026-04-22) + Slice E follow-up pass (2026-04-25 master `d190b5b`).** All seven per-stage request records (`PreflightRequest`, `HarvestRequest`, `NativeSmokeRequest`, `ConsolidateHarvestRequest`, `PackRequest`, `PackageConsumerSmokeRequest`, `PublishRequest`) live co-located with their owning feature under `Features/<X>/`; three `IPackageVersionProvider` implementations (`ManifestVersionProvider`, `GitTagVersionProvider`, `ExplicitVersionProvider`) live under `Features/Versioning/`.

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
PublishStaging → PublishPublic (public promotion disabled pending PD-7)
```

### Per-stage request shapes (ADR-003 §3.2, implemented)

Each stage owns its own request record co-located with its feature under `Features/<X>/<X>Request.cs`; there is no monolithic `PipelineRequest`. Live shape:

- `PreflightRequest` — manifest + resolved version mapping (always resolved per ADR-003 §2.3)
- `HarvestRequest` — rid + library set + vcpkg config
- `NativeSmokeRequest` — rid + harvest output location
- `ConsolidateHarvestRequest` — successful rid list + output root
- `PackRequest` — `IReadOnlyDictionary<string, NuGetVersion> Versions` (resolved version mapping; scope = mapping keys per §2.2)
- `PackageConsumerSmokeRequest` — rid + Versions mapping + FeedPath
- `PublishRequest` — `DirectoryPath PackagesDir` + `string FeedUrl` + `string AuthToken` (live for `PublishStaging`; `PublishPublic` remains disabled pending public promotion work)

### Version source providers (ADR-003 §3.1, implemented)

Version resolution is abstracted behind `IPackageVersionProvider` with three implementations under `Features/Versioning/`:

- `ManifestVersionProvider` — reads `manifest.json library_manifests[].vcpkg_version` per family + injected suffix (used by local-dev + CI workflow_dispatch `mode=manifest-derived` via `ResolveVersionsTask`)
- `GitTagVersionProvider` — reads `sdl<major>-<role>-<semver>` family tags at the invocation commit; supports `GitTagScope.Targeted` (single-family) + `GitTagScope.Train` (multi-family meta-tag) sum-type modes
- `ExplicitVersionProvider` — operator-supplied per-family mapping. Direct stage-target `--explicit-version <family>=<semver>` remains the PD-8/manual escape-hatch path. CI `workflow_dispatch mode=explicit` normalizes through `ResolveVersions --version-source=explicit`, so explicit releases emit the shared `versions.json` artifact before downstream stages run.

Providers are **service-only, not Cake CLI targets** (only `ResolveVersionsTask` exposes them publicly via the build-host entrypoint). Resolution happens exactly once per invocation; the mapping is immutable downstream (see ADR-003 §2.4). Caveat: the invariant scopes to CI job-chain runs and composite Cake targets; operator-driven ad-hoc target sequencing is each-invocation-resolves-independently.

CI flows (`release.yml`) emit the resolved mapping as a `versions.json` artifact via the `ResolveVersions` job; downstream stage tasks consume it via `--versions-file artifacts/resolve-versions/versions.json` (mutually exclusive with direct `--explicit-version` per CLI guard in `Program.cs`). The live workflow routes `workflow_dispatch mode=manifest-derived` through manifest + CI suffix, `workflow_dispatch mode=explicit` through `ResolveVersions --version-source=explicit`, family tags through targeted git-tag mode, and train tags through meta-tag mode.

### Pipeline / Flow vocabulary (ADR-004 §2.4 + §2.5)

Stage runners use one of two suffixes, chosen by intent:

- **`<X>Pipeline`** — operational vertical slice for a single feature. Examples: `HarvestPipeline`, `PackagePipeline`, `PackageConsumerSmokePipeline`, `PreflightPipeline`, `ConsolidateHarvestPipeline`, `NativeSmokePipeline`, `PublishPipeline`, `ResolveVersionsPipeline`, `EnsureVcpkgDependenciesPipeline`, `CoverageCheckPipeline`, `OtoolAnalyzePipeline`, `InspectHarvestedDependenciesPipeline`, `GenerateMatrixPipeline`, `CleanArtifactsPipeline`, `CompileSolutionPipeline`, `InfoPipeline`. Each lives in `Features/<X>/<X>Pipeline.cs` next to its task.
- **`<X>Flow`** — multi-feature orchestration slice. Today's only instance: `SetupLocalDevFlow` (`Features/LocalDev/SetupLocalDevFlow.cs`) for the local-dev composition path. Adding a second `Flow` requires extending the `OrchestrationFeatureAllowlist` in `ArchitectureTests` and ADR-004 §2.5.

Pipelines target `RunAsync(TRequest, CancellationToken)` — the ADR-004 §2.11.1 migration exception is closed at P4-A. Pipelines receive only their request DTO; Cake abstractions (`ICakeContext`, `ICakeLog`, `IPathService`) are constructor-injected. Two known exceptions remain documented inline: `EnsureVcpkgDependenciesPipeline.Run(BuildContext)` (P4-deferred) and `SetupLocalDevFlow.RunAsync(BuildContext, CT)` (P4-deferred — awaits sub-pipeline cut-overs). Cake `Tool<TSettings>` wrappers under `Tools/` retain the `<X>Runner` suffix native to Cake (`LddRunner`, `OtoolRunner`, etc.) — see ADR-004 §2.10.

### Stage-owned validation

Each guardrail has one owning stage; there is no monolithic "PostFlight" validator suite. Explicit mirrors, such as G58 in PreFlight, are called out where they exist. Canonical mapping:

- **PreFlight:** G14/G15 (version consistency), G16 (strategy coherence), G4/G6/G7/G17/G18 (csproj pack contract), G49 (core library identity), G54 (upstream major.minor alignment).
- **Harvest:** G19 (hybrid leak), G50 (primary ≥ 1).
- **NativeSmoke:** native binaries load + initialize at OS level (C/C++ harness today; ADR-003 extracts as its own stage).
- **Pack:** G21–G23, G25–G27, G46 (empty-payload guard), G47 (`buildTransitive/` contract), G48 (per-RID native payload shape), G55 (native metadata file), G56 (satellite cross-family upper bound), G57 (README mapping table), G58 (cross-family dependency scope reachability; Pack-owned and mirrored in PreFlight as defense in depth).
- **ConsumerSmoke:** restore + runtime TUnit per-TFM pass.
- **Publish:** feed auth + deduplication.

See [release-guardrails.md](release-guardrails.md) for the full guardrail registry.

### SetupLocalDev composition (ADR-003 §3.3 Option A + ADR-004 §2.5)

`SetupLocalDevTask` is a thin Cake task over `SetupLocalDevFlow` (`Features/LocalDev/SetupLocalDevFlow.cs`). The flow is the build host's only multi-feature orchestrator and consumes sibling-feature pipelines (Vcpkg, Preflight, Harvesting, Packaging, Versioning) directly per the architecture-test invariant #4 allowlist exception. For the `Local` profile, the flow internally composes `ManifestVersionProvider` + `EnsureVcpkgDependenciesPipeline` + `PreflightPipeline` + `HarvestPipeline` + `ConsolidateHarvestPipeline` + `IPackagePipeline` + `IArtifactSourceResolver.PrepareFeedAsync` — a single public seam (`SetupLocalDevTask`) over a privately-composed flow. `RemoteInternal` / `ReleasePublic` profiles retain the same public seam even if their internal mechanisms differ; the resolver factory dispatches to `LocalArtifactSourceResolver` / `RemoteArtifactSourceResolver` based on the operator's `--source` CLI argument.

See [ADR-001 §2.7–§2.8](../decisions/2026-04-18-versioning-d3seg.md) for the resolver contract, [ADR-003 §3.3](../decisions/2026-04-20-release-lifecycle-orchestration.md) for the Option A selection rationale, and [ADR-004 §2.5](../decisions/2026-05-02-cake-native-feature-architecture.md#25-features-localdev-orchestration-feature-exception) for the orchestration-feature exception design.

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

The build host follows [ADR-004](../decisions/2026-05-02-cake-native-feature-architecture.md) Cake-native feature-oriented architecture: five top-level folders, no DDD layering. The ADR-002 layered shape (`Tasks/Application/Domain/Infrastructure`) is fully retired in the production tree.

```text
build/_build/
├── Program.cs              ← Entry point: CLI parsing, DI composition root, repo-root detection
├── Host/                   ← Cake/Frosting runtime + composition site (free dependency direction)
│   ├── BuildContext.cs     ← Slim 4-property carrier (Paths, Runtime, Manifest, Options) per ADR-004 §2.11
│   ├── Cake/               ← CakeJsonExtensions, CakePlatformExtensions, CakeFileSystemExtensions
│   ├── Cli/                ← CakeOptions, RepositoryOptions, VcpkgOptions, DotNetOptions, PackageOptions, VersioningOptions, DumpbinOptions
│   ├── Configuration/      ← BuildOptions aggregate + per-axis records (VcpkgConfiguration, PackageBuildConfiguration, VersioningConfiguration, RepositoryConfiguration, DotNetBuildConfiguration, DumpbinConfiguration)
│   └── Paths/              ← IPathService + PathService (Host-tier path abstraction)
├── Features/               ← Operational vertical slices (13 features post-Adım 13)
│   ├── Ci/                 ← GenerateMatrixTask + GenerateMatrixPipeline + ServiceCollectionExtensions
│   ├── Coverage/           ← CoverageCheckTask + CoverageCheckPipeline + CoverageThresholdValidator
│   ├── DependencyAnalysis/ ← OtoolAnalyzeTask + OtoolAnalyzePipeline (diagnostic alias)
│   ├── Diagnostics/        ← InspectHarvestedDependenciesTask + InspectHarvestedDependenciesPipeline
│   ├── Harvesting/         ← HarvestTask + HarvestPipeline + ConsolidateHarvestTask/Pipeline + NativeSmokeTask/Pipeline + ArtifactPlanner + ArtifactDeployer + BinaryClosureWalker + IArtifactPlanner / IArtifactDeployer / IBinaryClosureWalker
│   ├── Info/               ← InfoTask + InfoPipeline
│   ├── LocalDev/           ← SetupLocalDevTask + SetupLocalDevFlow (multi-feature orchestrator; ADR-004 §2.5 + §2.13 invariant #4 allowlist exception)
│   ├── Maintenance/        ← CleanArtifactsTask/Pipeline + CompileSolutionTask/Pipeline
│   ├── Packaging/          ← PackageTask + PackagePipeline + PackageConsumerSmokeTask/Pipeline + SetupLocalDevTask (registered here, consumed via LocalDev) + PackageOutputValidator + NativePackageMetadataGenerator + ReadmeMappingTableGenerator + SatelliteUpperBoundValidator + LocalArtifactSourceResolver + RemoteArtifactSourceResolver + ArtifactSourceResolverFactory + PackagingStrategyFactory + DependencyPolicyValidatorFactory + FamilyTopologyHelpers + JansetLocalPropsWriter + VersionsFileWriter + SmokeScopeComparator
│   ├── Preflight/          ← PreFlightCheckTask + PreflightPipeline + PreflightReporter + VersionConsistencyValidator + StrategyCoherenceValidator + CoreLibraryIdentityValidator + CsprojPackContractValidator + FamilyIdentifierConventions
│   ├── Publishing/         ← PublishStagingTask (internal feed live) + PublishPublicTask (PD-7 stub) + PublishPipeline
│   ├── Vcpkg/              ← EnsureVcpkgDependenciesTask + EnsureVcpkgDependenciesPipeline
│   └── Versioning/         ← ResolveVersionsTask + ResolveVersionsPipeline + ManifestVersionProvider + ExplicitVersionProvider + GitTagVersionProvider + ExplicitVersionParser + IPackageVersionProvider + GitTagScope (sum-type Targeted | Train)
├── Shared/                 ← Build-domain vocabulary (no Cake deps, no I/O — invariant #1)
│   ├── Coverage/           ← CoverageMetrics, CoverageBaseline, CoverageCheckResult/Success/Error (Adım 13.2)
│   ├── Harvesting/         ← BinaryClosure, BinaryNode, HarvestManifest cluster (RidHarvestStatus, HarvestStatistics, HarvestSummary, ConsolidationState, DivergentLicense), PackageInfo, PackageInfoResult/Error, HarvestingError (Adım 13.1; path-typed members are string per Cake-decoupling)
│   ├── Manifest/           ← ManifestConfig, LibraryManifest, RuntimeInfo, PackageFamily, RuntimeConfig, SystemArtefactsConfig, VcpkgManifest
│   ├── Packaging/          ← DotNetPackResult/Error, ProjectMetadata, ProjectMetadataResult/Error, G58CrossFamilyCheckModels, IG58CrossFamilyDepResolvabilityValidator + impl, PackagingError (Adım 13.3)
│   ├── Results/            ← BuildError, BuildResultExtensions, AsyncResultChaining helpers, Unit (cross-feature OneOf void-marker)
│   ├── Runtime/            ← IRuntimeProfile, RuntimeProfile, RuntimeFamily (Cake-decoupled enum)
│   ├── Strategy/           ← HybridStaticStrategy/Validator, PureDynamicStrategy/Validator, StrategyResolver, IPackagingStrategy, IDependencyPolicyValidator, IStrategyResolver, ValidationResult/Success/Error, PackagingModel
│   └── Versioning/         ← IUpstreamVersionAlignmentValidator + impl + UpstreamVersionAlignmentResult/Success/Error + UpstreamVersionAlignmentValidation (Adım 13.4)
├── Tools/                  ← Cake Tool<TSettings> wrappers ONLY (invariant #2: no Features deps)
│   ├── Dumpbin/            ← DumpbinTool, DumpbinSettings, DumpbinAliases, DumpbinDependentsRunner
│   ├── Ldd/                ← LddTool, LddSettings, LddAliases, LddDependentsRunner
│   ├── NativeSmoke/        ← CMake invocation wrapper for the C/C++ smoke harness (Slice D)
│   ├── Otool/              ← OtoolTool, OtoolSettings, OtoolAliases, OtoolAnalyzeRunner
│   ├── Tar/                ← Tar wrapper (symlink-preserving extract, Slice D)
│   └── Vcpkg/              ← VcpkgTool, VcpkgInstallTool, VcpkgPackageInfoTool, VcpkgSettings, VcpkgAliases
└── Integrations/           ← Non-Cake-Tool external adapters (invariant #3: no Features deps; IPathService Host-coupling permanently tolerated — see ArchitectureTests invariant #3)
    ├── Coverage/           ← CoberturaReader + ICoberturaReader; CoverageBaselineReader + ICoverageBaselineReader
    ├── DependencyAnalysis/ ← WindowsDumpbinScanner, LinuxLddScanner, MacOtoolScanner, IRuntimeScanner
    ├── DotNet/             ← DotNetPackInvoker + IDotNetPackInvoker; ProjectMetadataReader + IProjectMetadataReader; DotNetRuntimeEnvironment + IDotNetRuntimeEnvironment (win-x86 child runtime bootstrap, P4d)
    ├── Msvc/               ← MsvcDevEnvironment + IMsvcDevEnvironment (vswhere + vcvarsall + env-delta merge per MsvcTargetArch, Slice CA)
    ├── NuGet/              ← NuGetProtocolFeedClient + INuGetFeedClient (read+write to GitHub Packages staging feed; PD-7 nuget.org public path TBD)
    └── Vcpkg/              ← VcpkgCliProvider (IPackageInfoProvider impl) + VcpkgManifestReader (IVcpkgManifestReader impl) + VcpkgBootstrapTool (bootstrap-vcpkg.bat/.sh dispatch, sealed concrete)
```

Direction-of-dependency invariants are asserted by [`build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs`](../../build/_build.Tests/Unit/CompositionRoot/ArchitectureTests.cs):

1. **`Shared_Should_Have_No_Outward_Or_Cake_Dependencies`** — `Build.Shared.*` may reference pure-domain libraries only (`NuGet.Versioning`, `OneOf`, `System.Text.Json`, etc.). No Cake.*, no `Build.Host.*`, no `Build.Features.*`, no `Build.Tools.*`, no `Build.Integrations.*`.
2. **`Tools_Should_Have_No_Feature_Dependencies`** — `Build.Tools.*` may depend on Cake framework + `Build.Shared.*` only.
3. **`Integrations_Should_Have_No_Feature_Dependencies`** — `Build.Integrations.*` may depend on Cake framework + `Build.Shared.*` only. **Inline named exception**: 2 IPathService Host-couplings (`Integrations.DotNet.DotNetPackInvoker → Build.Host.Paths.IPathService`; `Integrations.Vcpkg.VcpkgCliProvider → Build.Host.Paths.IPathService`) are permanently tolerated via a `permanentIntegrationsAllowlist` HashSet in the test method. `IPathService` is the canonical Host-tier path abstraction that Integrations adapters may consume; the BuildPaths fluent split originally scoped to P4 §8.3 was discarded on 2026-05-02.
4. **`Features_Should_Not_Cross_Reference_Except_From_LocalDev`** — `Build.Features.X.*` may not reference types in `Build.Features.Y.*`. Cross-feature data sharing flows through `Build.Shared.*`. `Build.Features.LocalDev.*` is the sole orchestration-feature exception (see ADR-004 §2.5).
5. **`Host_Is_Free`** — `Build.Host.*` may reference any layer; documented as a dual-direction sanity check, asserts only that the namespace is non-empty.

See [ADR-004 §2.13](../decisions/2026-05-02-cake-native-feature-architecture.md) for the invariant rationale.

## Service Architecture (DI)

`Program.cs ConfigureBuildServices` reads as the architectural index per ADR-004 §2.12: 3 cross-cutting group calls (`AddHostBuildingBlocks(parsedArgs)`, `AddIntegrations()`, `AddToolWrappers()`) followed by 13 `services.AddXFeature()` calls (Info → Maintenance → Ci → Coverage → Versioning → Vcpkg → Diagnostics → DependencyAnalysis → Preflight → Harvesting → Publishing → Packaging → LocalDev). LocalDev is registered last so every sibling pipeline it composes is already in the container per ADR-004 §2.5.

| Service Interface | Implementation | Purpose |
| --- | --- | --- |
| `IPathService` | `PathService` (Host) | Resolves paths to manifest.json, artifact dirs, vcpkg layout |
| `IRuntimeProfile` | `RuntimeProfile` (Shared/Runtime) | Maps RID ↔ vcpkg triplet, detects current platform via `RuntimeFamily` enum |
| `IPackageInfoProvider` | `VcpkgCliProvider` (Integrations/Vcpkg) | Queries vcpkg for installed package metadata; emits `Shared/Harvesting/PackageInfoResult` |
| `IBinaryClosureWalker` | `BinaryClosureWalker` (Features/Harvesting) | Two-stage graph walk: vcpkg metadata + runtime scan (dumpbin/ldd/otool); emits `Shared/Harvesting/BinaryClosure` |
| `IArtifactPlanner` | `ArtifactPlanner` (Features/Harvesting) | Determines which binaries to include and how to deploy them |
| `IArtifactDeployer` | `ArtifactDeployer` (Features/Harvesting) | Copies binaries to output, creates tar.gz for Unix |
| `IRuntimeScanner` | Platform-specific (Integrations/DependencyAnalysis) | dumpbin (Windows), ldd (Linux), otool (macOS); resolved per-RID by Program.cs factory closure |
| `IPackagingStrategy` | `HybridStaticStrategy` / `PureDynamicStrategy` (Shared/Strategy) | Packaging model and core-library interpretation; resolved by `PackagingStrategyFactory` from `VcpkgConfiguration` triplet |
| `IDependencyPolicyValidator` | `HybridStaticValidator` / `PureDynamicValidator` (Shared/Strategy) | Strategy-aware closure validation (hybrid leak enforcement, pure-dynamic pass-through); resolved by `DependencyPolicyValidatorFactory` |
| `IStrategyResolver` | `StrategyResolver` (Shared/Strategy) | Maps `runtimes[].triplet` ↔ packaging model; consumed by `StrategyCoherenceValidator` + factory closures |
| `ICoberturaReader` | `CoberturaReader` (Integrations/Coverage) | Parses cobertura XML into `Shared/Coverage/CoverageMetrics` |
| `ICoverageBaselineReader` | `CoverageBaselineReader` (Integrations/Coverage) | Loads `build/coverage-baseline.json` into `Shared/Coverage/CoverageBaseline` |
| `CoverageThresholdValidator` | concrete (Features/Coverage) | Applies the ratchet rule to parsed metrics; emits `Shared/Coverage/CoverageCheckResult` |
| `IVcpkgManifestReader` | `VcpkgManifestReader` (Integrations/Vcpkg) | Loads `vcpkg.json` into `Shared/Manifest/VcpkgManifest` for Preflight + future consumers |
| `IArtifactSourceResolver` | `LocalArtifactSourceResolver` for `--source=local`; `RemoteArtifactSourceResolver` for `--source=remote`/`--source=remote-internal` (Features/Packaging/ArtifactSourceResolvers) | Feed-prep profile selection per ADR-001 §2.7. `RemoteInternal` pulls latest coherent managed/native family packages from GitHub Packages into the local feed; `ReleasePublic` remains stubbed pending PD-7. Subsumes the retired `INativeAcquisitionStrategy` design from the strategy brief — feed-prep abstraction replaces native-acquisition abstraction. |
| `IPackageVersionProvider` | `ExplicitVersionProvider` registered as singleton (the only stage-task-visible provider per ADR-003 §3.1); `ManifestVersionProvider` + `GitTagVersionProvider` reach the CLI only via `ResolveVersionsPipeline` | Version-source provider abstraction; emits `versions.json` consumed downstream via `--versions-file` |
| `IDotNetPackInvoker` | `DotNetPackInvoker` (Integrations/DotNet) | Wraps `dotnet pack` CLI invocation with Cake `IFileSystem` / `ICakeContext` integration; emits `Shared/Packaging/DotNetPackResult` |
| `IProjectMetadataReader` | `ProjectMetadataReader` (Integrations/DotNet) | MSBuild `-getProperty` evaluation to `Shared/Packaging/ProjectMetadata` |
| `IDotNetRuntimeEnvironment` | `DotNetRuntimeEnvironment` (Integrations/DotNet) | Win-x86 child-runtime bootstrap (downloads + injects `DOTNET_ROOT_X86` / `DOTNET_ROOT(x86)` only into child `dotnet test` invocations, P4d) |
| `IMsvcDevEnvironment` | `MsvcDevEnvironment` (Integrations/Msvc) | Self-sources MSVC env via vswhere + `vcvarsall.bat` + env-delta merge per `MsvcTargetArch` (host + target cross-compile combinations cached in `ConcurrentDictionary`) |
| `INuGetFeedClient` | `NuGetProtocolFeedClient` (Integrations/NuGet) | Reads + pushes nupkgs against GitHub Packages staging feed (PD-5); nuget.org public path PD-7 |
| `IPackagePipeline` | `PackagePipeline` (Features/Packaging) | Per-family pack orchestration (renamed from `PackageTaskRunner` at P2); consumed by `PackageTask` + `SetupLocalDevFlow` |
| `IPackageConsumerSmokePipeline` | `PackageConsumerSmokePipeline` (Features/Packaging) | Per-RID consumer smoke orchestration (renamed from `PackageConsumerSmokeRunner` at P2); runner-strict on `--explicit-version` mapping |
| `IPackageOutputValidator` | `PackageOutputValidator` (Features/Packaging) | Post-pack guardrail aggregator (G21–G23, G25–G27, G46–G48, G55–G58 into a single `PackageValidation`) |
| `IG58CrossFamilyDepResolvabilityValidator` | `G58CrossFamilyDepResolvabilityValidator` (Shared/Packaging) | Cross-family minimum-version reachability check; defense-in-depth in Preflight + Pack stages (interface + impl moved to Shared at Adım 13.3) |
| `IUpstreamVersionAlignmentValidator` | `UpstreamVersionAlignmentValidator` (Shared/Versioning) | G54 upstream major.minor alignment for resolved family-version mappings (interface + impl moved to Shared at Adım 13.4; consumed by Preflight + all 3 version providers) |

## Reference Pattern: Harvesting First

When a build-host refactor needs precedent, compare the shape of the Harvesting module before inventing a new seam.

- `HarvestTask` keeps `BuildContext` in its `FrostingTask<BuildContext>.RunAsync()` override (Cake contract), but the Pipeline receives only `HarvestRequest`.
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
    { "rid": "win-x64", "triplet": "x64-windows-hybrid", "strategy": "hybrid-static", "runner": "windows-2025", "container_image": null }
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

var pipeline = new PreflightPipeline(
  manifest,
  vcpkgManifestReader,
  new StrategyCoherenceValidator(new StrategyResolver()),
  upstreamVersionAlignmentValidator,
  csprojPackContractValidator,
  g58CrossFamilyValidator,
  new PreflightReporter(repo.CakeContext),
  repo.CakeContext,
  repo.CakeContext.Log,
  repo.Paths);

var task = new PreFlightCheckTask(pipeline, packageBuildConfiguration);
task.Run(repo.BuildContext); // BuildContext → FrostingTask override (Cake contract)

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

Current canonical helpers live under `build/_build/Host/Cake/`:

- `CakeJsonExtensions.ToJson<TModel>()` / `ToJsonAsync<TModel>()` / `WriteJsonAsync` / `SerializeJson` / `DeserializeJson`
- `CakeFileSystemExtensions.ReadAllTextAsync()` / `WriteAllTextAsync()`
- `CakePlatformExtensions.Rid()`

If new production code reaches for `System.IO.File.*` directly, it will likely bypass `FakeFileSystem` and force tests back onto real disk. That is considered regression territory for the build-host test infra.

### PreFlightCheckTask

`Features/Preflight/PreFlightCheckTask` is a thin Cake task over `PreflightPipeline`. Validates configuration consistency before any matrix work runs.

**Checks**:

- manifest.json library versions match vcpkg.json override versions (G14/G15)
- Port versions match (G14/G15)
- Runtime strategy coherence (`runtimes[].strategy` vs triplet-derived model — G16)
- Csproj pack contract (G4/G6/G7/G17/G18)
- Core library identity (G49)
- G54 upstream major/minor alignment (`Shared/Versioning/IUpstreamVersionAlignmentValidator`)
- G58 cross-family dependency resolvability defense-in-depth (`Shared/Packaging/IG58CrossFamilyDepResolvabilityValidator`)

PreFlight is version-aware by ADR-003 §2.3 contract — both structural and version-aware validators run on every invocation. CI dispatches `PreFlightCheck` as a single-runner fail-fast gate before the harvest matrix opens (see [`release.yml`](../../.github/workflows/release.yml) `preflight` job).

## Binary Closure Walking

The most complex part of the build system. Each platform uses different tools:

### Windows (dumpbin)

```text
dumpbin /dependents SDL2.dll
→ Lists: SDL2.dll depends on kernel32.dll, user32.dll, vcruntime140.dll, ...
→ Filter `build/manifest.json` system exclusions
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
→ Filter `build/manifest.json` system exclusions
→ Handle symlink chains (libSDL2.so → libSDL2-2.0.so.0 → libSDL2-2.0.so.0.3200.4)
→ Recursively scan remaining dependencies
```

### macOS (otool)

```text
otool -L libSDL2.dylib
→ Lists: @rpath/libSDL2.dylib, /usr/lib/libSystem.B.dylib, ...
→ Filter `build/manifest.json` system exclusions and framework references
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

1. Pick the feature folder (`Features/<X>/`) the task belongs to. If no existing feature fits, create one — see "Adding a New Feature" below.
2. Create `Features/<X>/<X>Task.cs` inheriting from `FrostingTask<BuildContext>`.
3. Add `[TaskName("YourTask")]` attribute.
4. Add `[IsDependentOn(typeof(...))]` for dependencies.
5. Build a `<X>Request` DTO from `BuildContext` + the feature's configuration sub-record.
6. Delegate to `<X>Pipeline.RunAsync(context, request, cancellationToken)`. Keep the Task body to one orchestration delegation call per ADR-004 §2.4. Extract a `<X>Pipeline.cs` only if the inline body grows past ~200 LOC.
7. Register the pipeline + any new validators / generators / factories in the feature's `ServiceCollectionExtensions.AddXFeature()`.

### Adding a New Feature

1. Create `Features/<X>/` with at minimum: `<X>Task.cs`, `<X>Pipeline.cs` (or inline in Task), `<X>Request.cs`, and `ServiceCollectionExtensions.cs`.
2. Wire the feature into `Program.cs ConfigureBuildServices` via `services.AddXFeature()`.
3. Add a `[Test] public async Task AddXFeature_Should_Register_All_Pipeline_And_Validator_Types()` smoke in `build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs` (use the existing per-feature smokes as the template).
4. If the feature consumes types from sibling features (cross-feature DI), explicitly pre-register the upstream features in the smoke (matches production composition order — see Versioning / Preflight / Harvesting / Packaging smokes).

### Adding a New Platform

1. Update `manifest.json` (schema v2.1): add a new `runtimes[]` entry with RID → triplet → strategy → runner → container_image mapping.
2. Ensure `IRuntimeScanner` / `BinaryClosureWalker` handles the new platform's dependency scanning tool (extend the DI-time RID switch in `Program.cs` if a new OS family appears).
3. Update `manifest.json system_exclusions[]` with the platform's system libraries.
4. Author a matching vcpkg overlay triplet under `vcpkg-overlay-triplets/` if the platform is a new hybrid-static target.
5. Add CI workflow (or expand dynamic matrix — see ADR-003 §3.4) for the new platform.

## Historical Note

The original build plan has been retired after migration into the current docs set. The current implementation still follows that earlier harvest-pipeline direction, but CI/CD and packaging details have evolved and should now be read from the active docs instead.

For repo-specific tradeoffs and architecture-review carry-over, see [design-decisions-and-tradeoffs.md](design-decisions-and-tradeoffs.md). For general Cake Frosting working patterns trimmed from the deep reference, see [cake-frosting-patterns.md](../playbook/cake-frosting-patterns.md), [cake-frosting-host-organization.md](../playbook/cake-frosting-host-organization.md), and [cake-frosting-build-expertise.md](../reference/cake-frosting-build-expertise.md).
