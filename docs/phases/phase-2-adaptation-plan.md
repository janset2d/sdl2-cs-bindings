# Phase 2 Adaptation Plan — Release Lifecycle Implementation

**Date:** 2026-04-15
**Status:** Approved — implementation not yet started
**Prerequisite:** [Release Lifecycle Direction](../knowledge-base/release-lifecycle-direction.md) (locked)
**Issue context:** #54, #55, #63, #83, #85

## Context

Release lifecycle direction is locked. Canonical docs and issues are cleaned up. This plan describes how to apply those decisions to the actual codebase: manifest.json, csproj files, Cake build host, CI workflows, and local dev model — holistically.

This is a multi-session project. The plan designs the full picture, identifies the implementation order, and what depends on what.

## Current State

**What's ready:**

- csproj files have NO explicit `<Version>` — clean slate for MinVer
- `Directory.Build.props` has CPM enabled, Source Link, symbol packages
- Managed packages already reference their native counterparts (ProjectReference)
- Native packages are payload-only (no compiled assemblies, pack runtimes + .targets)
- Manifest v2 schema with runtimes, library_manifests, system_exclusions
- Strategy layer implemented + tested (189 tests) but not wired (#85)
- release-candidate-pipeline has `fromJson()` pattern prototyped (but with wrong matrix shape)

**What's missing:**

- `package_families` in manifest.json
- MinVer integration
- NuGet.Versioning in Cake
- dotnet-affected in Cake
- Family-aware Cake tasks (matrix generation, PackageTask)
- Dynamic CI matrix from manifest
- #85 strategy wiring completion

## Holistic Design — How Everything Fits Together

```
manifest.json (schema v2.1)
  ├── runtimes[]               → CI matrix generation (build axis)
  ├── library_manifests[]      → Cake harvest + validate
  ├── package_families[] (NEW) → Release governance, change detection, version resolution
  └── packaging_config         → Strategy validation

Cake Build Host
  ├── Existing: Harvest → Validate → Deploy → Consolidate
  ├── NEW: GenerateMatrixTask     → reads runtimes, outputs GHA JSON
  ├── NEW: ResolveVersionTask     → reads family tags via NuGet.Versioning
  ├── NEW: DetectChangesTask      → dotnet-affected as library
  ├── NEW: PackageTask            → family-aware pack (managed + native at family version)
  └── WIRE: Strategy DI (#85)    → IPackagingStrategy + IDependencyPolicyValidator in pipeline

csproj (all src/ projects)
  ├── MinVer package reference (via Directory.Build.props or Directory.Packages.props)
  ├── <MinVerTagPrefix> per family (in each csproj)
  └── Within-family constraint: exact pin [x.y.z] enforced at package output level (mechanism TBD by A0 spike)

CI Workflows
  ├── preflight-gate job        → Cake PreFlightCheckTask + unit tests (MUST PASS)
  ├── generate-matrix job       → Cake GenerateMatrixTask → $GITHUB_OUTPUT
  ├── build-harvest jobs        → 7 RID jobs from dynamic matrix
  ├── consolidate job           → merge per-RID artifacts
  ├── package-publish job       → Cake PackageTask per family
  └── promote-to-public         → separate workflow, manual trigger

Local Dev (Execution Model)
  ├── Source Mode: ProjectReference, vcpkg native for current RID
  ├── Package Validation Mode: Cake pack → local folder feed → PackageReference consumer test
  └── Release Mode: tag push → CI → internal feed → manual promote
```

## Accepted Review Amendments (2026-04-15)

Six findings from independent review, all accepted. Amendments integrated into the stream descriptions below.

### Amendment 1 (High): PreFlight tag resolution — trigger-aware

PreFlight version resolution must distinguish between trigger types:

- **Tag push trigger** → resolve version from tag, tag not found = FAIL
- **Main push / manual trigger** → MinVer prerelease fallback allowed (`0.0.0-alpha.0.N`), PASS with info log

PreFlight always resolves a version and passes it downstream — the difference is whether "no tag" is an error or expected.

### Amendment 2 (High, BLOCKING): Within-family exact pin — A0 spike required

**Problem:** `dotnet pack` converts ProjectReference to PackageReference with `>=` minimum version constraint. The direction doc requires exact pin (`[x.y.z]` in NuGet range format). These are incompatible — ProjectReference alone cannot produce exact pin.

**Decision gate:** Before PackageTask or any publish flow is implemented, this must be resolved:

- **Success criteria:** A reproducible mechanism exists to produce `.nupkg` files where `Janset.SDL2.Core` depends on `Janset.SDL2.Core.Native` with dependency version range `[1.2.0]` (NuGet exact pin notation, not `>= 1.2.0`).
- **Acceptance test:** Parse the `.nuspec` inside the produced `.nupkg`, assert that the within-family dependency uses `[x.y.z]` range format.
- **Where this lives:** Stream A0 spike (blocking, before Stream D).

Possible approaches to investigate in the spike:

1. `.nuspec` override in csproj (`<NuspecProperties>` or custom `.nuspec` template)
2. Post-pack nuspec patching via Cake
3. Pack-time MSBuild property injection (`VersionOverride` + `ExactVersion=true` if supported)
4. Separate native PackageReference instead of ProjectReference during pack

### Amendment 3 (Medium): Affected family filtering in harvest axis, not matrix

CI matrix stays RID-only (7 jobs). Affected family filtering happens inside each RID job at the harvest axis level: Cake receives `--affected-families core,image` and only harvests those libraries. Matrix shape does not change.

### Amendment 4 (Medium): dotnet-affected feasibility spike + fallback

Stream E starts with a short feasibility spike: can dotnet-affected run as an in-process NuGet library within Cake? If not, fallback to CLI wrapper via `Cake.Process` runner. Both paths orchestrated through Cake. Spike before full implementation.

### Amendment 5 (Medium): Stream D split — local vs CI

Stream D is split:

- **D-local** (after A + B): Cake PackageTask, local folder feed, consumer smoke test. No CI dependency.
- **D-ci** (after A + B + C): CI package-publish job consuming harvest artifacts from CI pipeline. Depends on Stream C delivering dynamic matrix + artifact flow.

### Amendment 6 (Low): Smoke test as explicit publish gate

Package-consumer smoke test is a required gate for publish. CI pipeline shape becomes:

```text
... → Package → Smoke Test (gate) → Publish
```

If smoke test fails, publish is blocked. This is an explicit `needs:` dependency in the CI workflow, not optional.

## Implementation Streams

### Stream A0: Exact Pin Spike (BLOCKING)

**Must complete before Stream D. Can run in parallel with Stream A (manifest + MinVer).**

Research and prove a mechanism to produce `.nupkg` files with within-family exact pin dependencies in NuGet `[x.y.z]` range format. See Amendment 2 above for success criteria and investigation approaches.

**Sync checkpoint:** A0 findings may affect MinVer integration or csproj structure from Stream A. After A0 completes, review Stream A artifacts for compatibility before proceeding to Stream D. If A0 discovers that the mechanism requires csproj changes (e.g., switching ProjectReference to PackageReference during pack), those changes must be reconciled with Stream A's MinVer setup.

**Exit:** Documented mechanism + working proof-of-concept `.nupkg` with verified `.nuspec` dependency (PD-2).

### Stream A: Data Foundation (manifest schema + versioning tooling)

**No dependencies. Start here.**

1. **manifest.json v2.1: add `package_families`**
   - File: `build/manifest.json`
   - Add section with 6 families (core, image, mixer, ttf, gfx, net)
   - Fields: name, tag_prefix, managed_project, native_project, library_ref, depends_on, change_paths
   - Update Cake model: `PackageFamilyConfig` class in BuildManifestModels

2. **NuGet.Versioning in Cake**
   - File: `build/_build/Build.csproj` — add PackageReference
   - File: `Directory.Packages.props` — add centralized version
   - No Cake task yet — just the library available for later use

3. **MinVer integration**
   - File: `Directory.Packages.props` — add MinVer package version
   - File: `Directory.Build.props` — add conditional MinVer reference (only for src/ projects, not build/_build)
   - Each csproj: add `<MinVerTagPrefix>core-</MinVerTagPrefix>` etc.
   - Native csproj: ALSO needs MinVer (same family version)
   - Test: `dotnet build` should give `0.0.0-alpha.0.N` versions (no tags exist yet)

### Stream B: Strategy Wiring (#85 completion)

**No dependency on Stream A. Can run in parallel.**

Already planned in `docs/research/cake-strategy-implementation-brief-2026-04-14.md`:

1. Program.cs DI wiring (register IPackagingStrategy + IDependencyPolicyValidator)
2. HarvestTask validation step (call validator after closure walk)
3. PreFlightCheck coherence (call StrategyResolver for all runtimes)
4. HarvestPipeline extraction (thin out HarvestTask)

This stream has existing tests and design. Execute as designed.

### Stream C: CI Modernization (after Stream A)

**Depends on Stream A (manifest schema for matrix generation).**

**Key principle: PreFlightCheck is the gate.** Before ANY matrix job runs, PreFlightCheck must validate everything: manifest consistency, triplet↔strategy coherence, family version resolution, package_families integrity, and Cake unit tests. If PreFlight fails, no CI resources are spent on builds.

1. **Expand PreFlightCheckTask as the CI gate**
   - Runs BEFORE matrix generation
   - Validates: manifest.json schema integrity, vcpkg.json↔manifest version consistency, triplet↔strategy coherence (#85), package_families↔library_manifests cross-reference
   - **Trigger-aware version resolution** (Amendment 1): tag push → resolve from tag, fail if missing. Main push / manual → prerelease fallback allowed, info log.
   - Runs: `dotnet test build/_build.Tests/` — Cake unit tests must pass before any build
   - On failure: entire pipeline stops, no matrix jobs spawn

2. **Cake GenerateMatrixTask**
   - New task: reads `manifest.json` runtimes, outputs GHA-compatible JSON
   - Runs AFTER PreFlightCheck passes
   - Groups by OS family (windows/linux/macos) for reusable workflow routing
   - Output: three JSON matrices (one per OS family)

3. **CI pipeline shape**

   ```
   PreFlightCheck (gate)
     → unit tests pass
     → manifest consistency validated
     → family versions resolved
     ↓ (only if gate passes)
   GenerateMatrix
     → dynamic JSON from manifest
     ↓
   Build/Harvest (7 RID jobs from matrix)
     ↓
   Consolidate → Package → Publish
   ```

4. **Pure-dynamic first validation**
   - Run GenerateMatrixTask with current manifest
   - Validate that the dynamic matrix produces the same 7 RID jobs as the hardcoded YAML
   - Don't update workflows yet — just prove the matrix generation works

5. **CI workflow migration** (after validation)
   - Add `preflight-gate` job that runs Cake PreFlightCheckTask + unit tests
   - Add `generate-matrix` job that runs Cake GenerateMatrixTask (needs: preflight-gate)
   - Replace hardcoded matrix with `fromJson()`
   - Keep reusable workflows (windows/linux/macos) — they handle OS-specific setup

### Stream D-local: PackageTask + Local Validation (after Stream A + A0 + B)

**Depends on A (versioning), A0 (exact pin mechanism), and B (strategy validation).**

1. **Cake PackageTask** — #54
   - Reads family version from MinVer (or CLI override)
   - Reads harvest-manifest.json for successful RIDs
   - Stages native content into `runtimes/{rid}/native/` layout
   - Runs `dotnet pack` for both managed and native at family version
   - **Applies exact pin mechanism** from A0 spike for within-family dependencies (`[x.y.z]` NuGet range)
   - **Post-pack assertion:** parse produced `.nuspec`, verify within-family dep uses exact range
   - Outputs to `artifacts/packages/`

2. **Package-consumer smoke test** — #83
   - Dedicated test project using PackageReference to local folder feed
   - Validates: restore works, native binaries land, SDL_Init succeeds

3. **Local folder feed integration**
   - Cake task to publish to local folder feed after pack
   - Package Validation Mode flow: pack → local feed → consumer test

### Stream D-ci: CI Package + Publish (after Stream A + A0 + B + C)

**Depends on all prior streams. This is the CI-level packaging and promotion flow.**

1. **CI package-publish job**
   - Consumes harvest artifacts from CI build-harvest jobs (Stream C)
   - Runs Cake PackageTask per family
   - Uploads `.nupkg` artifacts

2. **Smoke test as publish gate** (Amendment 6)
   - Package-consumer smoke test runs AFTER pack, BEFORE publish
   - Explicit `needs:` dependency — publish is blocked if smoke test fails

3. **Publish to internal feed**
   - Pushes validated packages to internal feed
   - Tag push → stable candidate. Main push → prerelease.

### Stream E: Change Detection (parallel with C/D)

**Depends on Stream A (manifest schema for change_paths).**

1. **Feasibility spike** (Amendment 4)
   - Can dotnet-affected run as in-process NuGet library within Cake?
   - If yes: proceed with library integration
   - If no: fallback to CLI wrapper via `Cake.Process` runner
   - Both paths orchestrated through Cake

2. **Cake DetectChangesTask**
   - Compares HEAD vs base branch, outputs affected family list
   - Uses `package_families[].change_paths` as hint, MSBuild graph as ground truth

3. **CI integration** (Amendment 3)
   - Affected family filtering happens at **harvest axis** level, not matrix level
   - Matrix stays RID-only (7 jobs). Each RID job receives `--affected-families core,image` parameter
   - Only affected families are harvested within each job. Unaffected families are skipped.
   - Full-train triggers bypass filtering — all families harvested

## Implementation Notes (from Deniz)

1. **dotnet-affected via Cake** — preferred path: NuGet library integration. Fallback: CLI wrapper via `Cake.Process` runner if library integration proves infeasible (see Amendment 4). Both paths keep orchestration in Cake.
2. **NuGet.Versioning for SemVer** — use the NuGet package for SemVer parsing/comparison within Cake
3. **Cake as single orchestration surface** — all processes through Cake, CI workflows are triggers only
4. **PreFlightCheck is the gate** — must validate everything before any CI resources are spent

## Pending Decisions

| # | Decision | Owner | Exit Criterion | Blocks |
| --- | --- | --- | --- | --- |
| PD-1 | MinVer for native payload-only csproj: does `<IncludeBuildOutput>false</IncludeBuildOutput>` prevent MinVer from setting `Version`? | Stream A implementer | `dotnet build` on native csproj produces versioned assembly metadata or pack output with correct version | Stream D-local |
| PD-2 | Exact pin mechanism: which approach produces within-family `[x.y.z]` NuGet range in `.nupkg`? | Stream A0 spike | Proof-of-concept `.nupkg` with verified `.nuspec` exact pin dependency | Stream D-local, Stream D-ci |
| PD-3 | dotnet-affected: NuGet library or CLI wrapper? | Stream E spike | Working Cake task that outputs affected family list from git diff | Stream E full implementation |
