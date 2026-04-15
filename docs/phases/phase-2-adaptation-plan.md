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

- **Acceptance target: Image family (satellite), not Core.** Core-only is a useful calibration probe but cannot de-risk the full packaging model. The real risk is producing both a within-family exact pin AND a cross-family minimum range **in the same package**. Only a satellite package exercises both constraints simultaneously.
- **Success criteria:** A reproducible mechanism exists to produce `Janset.SDL2.Image.nupkg` where:
  - `Janset.SDL2.Image.Native` dependency is emitted as `[1.0.3]` (within-family exact pin in NuGet range notation)
  - `Janset.SDL2.Core` dependency is emitted as a minimum range (e.g., `1.2.0`, meaning `>=` in NuGet semantics — **not** bracketed)
- **Acceptance test (automated):** A TUnit test opens the produced `.nupkg`, parses the `.nuspec`, and asserts both dependency ranges are correct. Manual eyeballing does not count as "done." The test must survive as a regression guard beyond the spike.
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

**Must complete before Stream D. Can run in parallel with Stream A-safe (manifest + NuGet.Versioning) and Stream B. Stream A-risky (MinVer project rollout) waits until A0 resolves.**

Research and prove a mechanism to produce `.nupkg` files with both within-family exact pin AND cross-family minimum range dependencies, in the same package. See Amendment 2 above for acceptance target (Image family) and success criteria (automated TUnit assertion on nuspec).

**Sync checkpoint:** A0 findings may affect csproj structure (e.g., requiring ProjectReference → PackageReference switch during pack). MinVer rollout (Stream A-risky) must be reconciled with A0's chosen csproj shape before it starts.

**Exit:** Documented mechanism + working Image-family `.nupkg` + TUnit test asserting both dependency ranges are in the correct format (PD-2 resolved).

### Stream A-safe: Manifest Schema + Versioning Library (low-churn)

**No dependencies. Can run in parallel with A0 and B. Start here.**

1. **manifest.json v2.1: add `package_families`**
   - File: `build/manifest.json`
   - Add section with 6 families (core, image, mixer, ttf, gfx, net)
   - Fields (policy-level only — no mechanism-shaped knobs): name, tag_prefix, managed_project, native_project, library_ref, depends_on, change_paths
   - Update Cake model: `PackageFamilyConfig` class in BuildManifestModels
   - **Do not add packaging-mechanism fields here.** If A0 discovers a mechanism that appears to need family-specific packaging knobs inside `manifest.json`, stop and re-evaluate rather than encoding a workaround.

2. **NuGet.Versioning in Cake**
   - File: `build/_build/Build.csproj` — add PackageReference
   - File: `Directory.Packages.props` — add centralized version
   - No Cake task yet — just the library available for later use

### Stream A-risky: MinVer Project Rollout (held until A0 resolves)

**Blocked by A0 (PD-2). Must not start until the exact pin mechanism is chosen, because A0 may reshape csproj topology.**

1. **MinVer integration**
   - File: `Directory.Packages.props` — add MinVer package version
   - File: `Directory.Build.props` — add conditional MinVer reference (only for `src/` projects, not `build/_build`)
   - Each csproj: add `<MinVerTagPrefix>core-</MinVerTagPrefix>` etc.
   - Native csproj: ALSO needs MinVer (same family version) — subject to PD-1 (`<IncludeBuildOutput>false</IncludeBuildOutput>` interaction)
   - Test: `dotnet build` should give `0.0.0-alpha.0.N` versions (no tags exist yet)

**Note:** PreFlightCheckTask's version resolution (Amendment 1) does **not** depend on MinVer rollout. PreFlight reads git tags directly using NuGet.Versioning (delivered by A-safe). A-risky is strictly about project/pack-time versioning for `src/` csprojs.

### Stream B: Strategy Wiring (#85 closure)

**No dependency on A0 or A. Can run in parallel.**

B-done = the live pipeline respects the already-landed strategy layer. The 189 existing tests must stay green throughout; broad test rewrites are a smell (seam is in the wrong place).

**B closure criteria (all three must land to close #85):**

1. Program.cs DI wiring (register IPackagingStrategy + IDependencyPolicyValidator)
2. HarvestTask validation step (call validator after closure walk)
3. PreFlightCheck strategy coherence (call StrategyResolver for all runtimes). **If B touches PreFlightCheck code, leave a clean version-resolution seam so Amendment 1 (Stream C) can plug in without a second invasive rewrite.** PreFlight version resolution itself is still Stream C's work; B just makes it easy to add.

**HarvestPipeline extraction is explicitly OUT of #85 closure.** Track as a separate follow-up issue. This prevents the "98% done forever" trap. Extraction is cleanliness work; it is not the same as landing the policy seam into runtime behavior.

Design reference: `docs/research/cake-strategy-implementation-brief-2026-04-14.md`.

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

### Stream D-local: PackageTask + Local Validation (after A-safe + A-risky + A0 + B)

**Depends on:**

- **A-safe** — `package_families` schema + NuGet.Versioning in Cake (pack task reads family metadata from manifest)
- **A-risky** — MinVer project rollout (pack time reads family version from git tags via MinVer)
- **A0** — chosen exact-pin mechanism (`[x.y.z]` within-family + cross-family minimum range applied during pack)
- **B** — strategy layer wired into harvest flow (pack task consumes strategy-validated harvest output)

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

### Stream D-ci: CI Package + Publish (after A-safe + A-risky + A0 + B + C)

**Depends on everything D-local depends on, plus Stream C (PreFlight gate + dynamic matrix + CI build-harvest artifact flow). This is the CI-level packaging and promotion flow.**

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

### Stream E: Change Detection — scope-reduced in 2a

**2a scope: feasibility spike only, and only if cheap. Full DetectChangesTask + CI filtering defer to Phase 2b.**

**Rationale:** Without change detection, CI is wasteful (every push does a full rebuild) but not incorrect. Without B, A0, A-safe, C, and D-local, packaging is blocked or directionless. That is the difference in criticality. 2a stays focused on correctness, version model, pack shape, and publish gating.

**2a deliverable (feasibility spike, Amendment 4 — depends on Stream A-safe):**

- Can dotnet-affected run as an in-process NuGet library within Cake?
- If yes: short ADR-style note in `docs/research/` committing to library integration for 2b.
- If no: short ADR-style note committing to CLI wrapper via `Cake.Process` runner for 2b.
- Output: a one-line decision with rationale. **No task implementation in 2a.**

**2b scope (deferred, listed here for traceability):**

1. Cake `DetectChangesTask` — compares HEAD vs base branch, outputs affected family list. Uses `package_families[].change_paths` as hint, MSBuild graph as ground truth.
2. CI integration (Amendment 3) — affected family filtering at **harvest axis** level (not matrix level). Matrix stays RID-only (7 jobs). Each RID job receives `--affected-families core,image` parameter. Full-train triggers bypass filtering.

### Stream F: Local-Dev Native Acquisition + Source Mode Payload Visibility

**Source-truth counterpart to Stream D-local. Independent of A0 and B. Depends on Stream A-safe (manifest schema for library identities).**

Stream F covers the source-graph side of local development: how contributors acquire native payloads locally and how those payloads become visible to test, sandbox, and sample csprojs that reach `.Native` via `ProjectReference`.

**Why F is separate from D-local:** D-local validates the shipping graph (`pack` → local feed → `PackageReference` consumer → smoke test). F validates the source graph (Cake populate → `Directory.Build.targets` inject → `bin/`). Related but structurally different mechanisms. Per [`docs/research/execution-model-strategy-2026-04-13.md §9`](../research/execution-model-strategy-2026-04-13.md), source graph and shipping graph are explicitly separate realities.

**2a deliverables:**

1. **Cake `Source-Mode-Prepare` task — two-source framework**
   - Stable interface: `dotnet cake --target=Source-Mode-Prepare --rid=<rid> [--source=local|remote] [--url=<url>]`
   - Contributor never interacts with vcpkg directly; vcpkg is one of two hidden backends
   - **Option 2 (default, 2a scope): `--source=local`** — invokes existing vcpkg + harvest pipeline for the RID's triplet; host-RID only
   - **Option 1 (direction locked, mechanism open; implementation 2b): `--source=remote --url=<url>`** — downloads an archive from the URL and places its contents under `artifacts/native-staging/<rid>/native/`. The archive format itself (tar.gz, something else), the producer workflow, and the extract semantics are **all unresolved 2b decisions**. The only locked constraint is that whatever the contract ends up as, it must preserve Linux/macOS symlink chains end-to-end — otherwise Option 1 regresses to the §5.1/§5.2 symlink-destruction case. Intended to work for any RID, including non-host.
   - Both options produce identical staging layout: `artifacts/native-staging/<rid>/native/*`
   - On Unix, `--source=local` uses `cp -a` (or equivalent) when copying vcpkg output to staging so that symlink chains remain intact
   - Emits a sanity report (files staged, total size, RIDs present)

2. **Solution-root `Directory.Build.targets` — platform-branched**
   - Opt-in via `$(JansetSdl2SourceMode)` property
   - Windows path: `<Content>` + `CopyToOutputDirectory="PreserveNewest"`, uses `%(RecursiveDir)` to preserve RID segment (avoids `NETSDK1047` restore gotcha)
   - Linux/macOS path: `<Target AfterTargets="CopyFilesToOutputDirectory">` with `<Exec Command="cp -a ...">` — preserves symlink chains at 1× size (WSL-verified; `<Content>`-only would produce 3× duplication and flatten chains)
   - Platform gating: `$([MSBuild]::IsOsPlatform('Windows'))`
   - Sanity check target emits MSBuild `Warning` codes `JANSET0001` (staging missing) and `JANSET0002` (staging present but empty for requested RID), mutually exclusive by construction

3. **`test/Directory.Build.props`** (Phase 2a preset — not the entire scope)
   - Sets `JansetSdl2SourceMode=true` for everything under `test/`
   - Imports `../Directory.Build.props` for centralized settings inheritance
   - Future subtrees (`samples/`, `sandbox/`, any other non-`src/` tree of csprojs) follow the same 5-line props pattern when they come online; they are not part of Phase 2a because they currently do not exist

4. **`.gitignore` update**
   - Add `artifacts/native-staging/` to gitignore rules

**Mechanism design and empirical evidence:** See [`docs/research/source-mode-native-visibility-2026-04-15.md`](../research/source-mode-native-visibility-2026-04-15.md) (PD-4 resolution; PD-5 direction-locked, mechanism open). The **copy mechanism** (staging → `bin/`) is empirically verified on Windows (worktree PoC), Linux (WSL Ubuntu, 2-level symlink chain), and macOS (SSH Intel Mac / Darwin 24.6, 3-level dylib chain) — all on 2026-04-15. "Verified" here is scoped to MSBuild-level file placement + platform gating; runtime loadability under dyld / ld.so with real SDL2 natives and code-signing is end-to-end validation, pending Stream F execution.

**Exit (2a):** A contributor on Windows can run `dotnet cake --target=Source-Mode-Prepare --rid=win-x64 --source=local`, then `dotnet test test/<SomeTest>/`, and native payloads resolve at runtime. A contributor on Linux or macOS performs the symmetric flow with their host RID; symlink chains arrive intact in `bin/` (the copy mechanism is PoC-verified on both kernels). One test project demonstrates end-to-end flow. The `--source=remote` path is wired in as an interface only in 2a — its producer contract (URL convention, which GHA workflow publishes artifacts, auth, caching) is unresolved and 2b scope.

**Deferred to 2b (PD-5 direction locked; concrete mechanism still open):** `--source=remote` full implementation — producer workflow, URL/artifact convention, archive format and extract semantics, authentication, caching. Direction is locked (non-host RIDs acquire via a remote download-and-stage path) but the archive contract and producer contract are both unresolved, and none of the mechanism pieces are validated end-to-end. This is not fill-in-the-blanks work; it is a proper 2b deliverable that pairs with D-ci.

**Deferred and explicit (tracked as PD-6):** `.NET Framework` (`net462`) source-mode visibility. The current mechanism targets modern .NET. Any future `net462` in-tree test project must resolve PD-6 first — an `AfterTargets="Build"` copy hook similar to today's `buildTransitive/*.targets` but activated by `$(JansetSdl2SourceMode)`. **Must land before any `net462` in-tree test project is added.**

## Implementation Notes (from Deniz)

1. **dotnet-affected via Cake** — preferred path: NuGet library integration. Fallback: CLI wrapper via `Cake.Process` runner if library integration proves infeasible (see Amendment 4). Both paths keep orchestration in Cake.
2. **NuGet.Versioning for SemVer** — use the NuGet package for SemVer parsing/comparison within Cake
3. **Cake as single orchestration surface** — all processes through Cake, CI workflows are triggers only
4. **PreFlightCheck is the gate** — must validate everything before any CI resources are spent

## Pending Decisions

| # | Decision | Owner | Status | Exit Criterion | Blocks |
| --- | --- | --- | --- | --- | --- |
| PD-1 | MinVer for native payload-only csproj: does `<IncludeBuildOutput>false</IncludeBuildOutput>` prevent MinVer from setting `Version`? | Stream A-risky implementer | Open | `dotnet build` on native csproj produces versioned assembly metadata or pack output with correct version | Stream A-risky, Stream D-local |
| PD-2 | Exact pin mechanism: which approach produces both within-family `[x.y.z]` and cross-family minimum range in the same `.nupkg`? | Stream A0 spike | Open | TUnit test on Image-family `.nupkg` asserts both dependency ranges are in the correct format | Stream D-local, Stream D-ci, Stream A-risky start |
| PD-3 | dotnet-affected: NuGet library or CLI wrapper? | Stream E 2a feasibility spike | Open — 2a feasibility only, full decision in 2b | ADR-style note committing to one path | Stream E full implementation (2b) |
| PD-4 | Source Mode native payload visibility mechanism | Stream F implementer | **Mechanism locked 2026-04-15** — verified on Windows (worktree), Linux (WSL Ubuntu, 2-level chain), and macOS (SSH Intel Mac, Darwin 24.6, 3-level dylib chain). End-to-end validation with real SDL2 natives pending Stream F execution. See [`source-mode-native-visibility-2026-04-15.md`](../research/source-mode-native-visibility-2026-04-15.md) | Locked: platform-branched — Windows `<Content>` + `CopyToOutputDirectory`, Linux/macOS `<Target>` + `<Exec cp -a>` (preserves symlink chains at 1× size). Opt-in `Directory.Build.targets` at solution root, flag via `test/Directory.Build.props` (Phase 2a preset), staging at `artifacts/native-staging/<rid>/native/`. Tar.gz is NOT used in Source Mode (only shipping graph) | — (informs Stream F shape) |
| PD-5 | Non-host RID local acquisition path | Stream F extension | **Direction locked 2026-04-15** via two-source framework in [`source-mode-native-visibility-2026-04-15.md`](../research/source-mode-native-visibility-2026-04-15.md) §7.2 (`--source=remote --url=<url>`). Concrete mechanism (URL convention, producer workflow, artifact granularity, auth, caching) still open — 2b scope | Producer workflow chosen, URL/artifact convention defined, auth story settled, end-to-end validated by downloading real natives + extracting to staging on a non-host RID | `--source=remote` full implementation (2b) |
| PD-6 | `.NET Framework` (`net462`) source-mode visibility: how do `net462` in-tree tests see natives in Source Mode? | Future implementer | Open — **must resolve before any `net462` in-tree test is added** | Mechanism documented and tested; analogous to today's `buildTransitive` .NET Framework copy hook but activated by `$(JansetSdl2SourceMode)` | Any future `net462` in-tree test project |
