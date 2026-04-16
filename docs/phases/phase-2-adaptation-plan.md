# Phase 2 Adaptation Plan — Release Lifecycle Implementation

**Date:** 2026-04-15
**Status:** Approved — implementation in progress (Streams A-safe and B landed; #85 closed, #87 follow-up open)
**Prerequisite:** [Release Lifecycle Direction](../knowledge-base/release-lifecycle-direction.md) (locked)
**Issue context:** #54, #55, #63, #83, #85, #87

## Context

Release lifecycle direction is locked. Canonical docs and issues are cleaned up. This plan describes how to apply those decisions to the actual codebase: manifest.json, csproj files, Cake build host, CI workflows, and local dev model — holistically.

This is a multi-session project. The plan designs the full picture, identifies the implementation order, and what depends on what.

## Current State

**What's ready:**

- csproj files have NO explicit `<Version>` — clean slate for MinVer
- `Directory.Build.props` has CPM enabled, Source Link, symbol packages
- Managed packages already reference their native counterparts (ProjectReference)
- Native packages are payload-only (no compiled assemblies, pack runtimes + .targets)
- Manifest v2.1 schema with runtimes, library_manifests, system_exclusions, package_families
- Strategy layer implemented + wired (Program.cs DI + HarvestTask validation + PreFlight coherence), with subsequent PreFlight cleanup aligned closer to Harvesting-style service boundaries; 241 tests currently passing (#85)
- Coverage ratchet infrastructure landed (#86): `ICoberturaReader` + `ICoverageBaselineReader` + `CoverageThresholdValidator` + `CoverageCheckTask`, OneOf result monad, Cake-native `IFileSystem` I/O, `build/coverage-baseline.json` floor (60.0% line / 49.0% branch vs measured 62.62% / 50.96%). Runs locally via `dotnet cake --target=Coverage-Check`; CI gate wiring deferred to Stream C PreflightGate.
- NuGet.Versioning is available in Cake build host
- release-candidate-pipeline has `fromJson()` pattern prototyped (but with wrong matrix shape)

**What's missing:**

- MinVer integration
- dotnet-affected in Cake
- Family-aware Cake tasks (matrix generation, PackageTask)
- Dynamic CI matrix from manifest

## Holistic Design — How Everything Fits Together

```text
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

### Stream A0: Exact Pin Spike — CLOSED (mechanism proven 2026-04-16)

**Blocked Stream D and A-risky. Both are now unblocked.**

Research and prove a mechanism to produce `.nupkg` files with both within-family exact pin AND cross-family minimum range dependencies, in the same package. See Amendment 2 above for acceptance target (Image family) and success criteria.

**Mechanism (proven 2026-04-16):** `PrivateAssets="all"` on the Native `ProjectReference` (suppresses it from pack output) + explicit `PackageReference` with bracket notation `[$(FamilyVersion)]` (injects exact pin into nuspec). Core `ProjectReference` remains default (emits `>=` minimum range). Empirically verified: 5 test scenarios on .NET SDK 9.0.309, 4 TFMs, parameterized versions. LibGit2Sharp production precedent. See [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md).

**Sync checkpoint:** A0's chosen shape adds `PrivateAssets="all"` on existing Native ProjectReference + a new `PackageReference`/`PackageVersion` pair. This is additive — no ProjectReference removal. MinVer rollout (Stream A-risky) is compatible: MinVer sets `$(Version)` at build time, and the family version property feeds into `[$(FamilyVersion)]` at pack time.

**Regression guard placement:** The A0 acceptance criterion originally specified a TUnit test inside Build.Tests. After review, the regression guards land in their permanent homes instead of a throwaway spike test:

- **A-risky (first automated guard):** PreFlight csproj structural validator — checks that every managed satellite's Native ProjectReference has `PrivateAssets="all"` and a matching bracket-notation `PackageReference`/`PackageVersion` exists. Lands in the same change that applies Mechanism 3 to real csproj files — no window where the shape exists without a guard.
- **D-local (second automated guard, defense-in-depth):** post-pack nuspec assertion inside PackageTask — opens the produced `.nupkg`, parses the nuspec, asserts within-family `[x.y.z]` and cross-family `x.y.z` ranges per TFM group. Catches actual pack output regression.

**Exit:** Documented mechanism + empirical proof artifacts (preserved at `artifacts/temp/a0-mechanism3/`) + PD-2 resolved + research note published. Spike is closed; automated guards land with A-risky (immediate next work) and D-local (downstream).

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

### Stream A-risky: MinVer + Exact-Pin csproj Rollout + Structural Lock (LANDED 2026-04-16)

**Previously blocked by A0 (PD-2). Unblocked + landed in a single coordinated pass on 2026-04-16.**

**Delivered:**

- MinVer 7.0.0 wired through `src/Directory.Build.props` (chains to root, scopes to `src/`) + `Directory.Packages.props` central package version. Native csprojs inherit transparently via `src/native/Directory.Build.props` import chain.
- All 10 csprojs (5 managed + 5 native) carry canonical `<MinVerTagPrefix>sdl2-{role}-</MinVerTagPrefix>` per the `sdl<major>-<role>` family identifier convention adopted in the same change ([release-lifecycle-direction.md §1](../knowledge-base/release-lifecycle-direction.md)).
- All 5 managed satellite csprojs apply Mechanism 3 from A0: `PrivateAssets="all"` on Native ProjectReference + bracket-notation `<PackageVersion>` + `<PackageReference>` + canonical `Sdl<Major><Role>FamilyVersion` property defaulting to `$(Version)` with `0.0.0-restore` restore-safe sentinel fallback.
- `src/Directory.Build.targets` MSBuild guard target `_GuardAgainstShippingRestoreSentinel` blocks pack of any `Janset.SDL2.*` managed package when family-version property is still the sentinel. Bypass `-p:AllowSentinelExactPin=true` for deliberate sentinel inspection. Pack invocations from production Cake orchestration (Stream D-local) supply the family version explicitly.
- PreFlight `CsprojPackContractValidator` checks guardrails G1-G8 + G17-G18 (see [release-guardrails.md](../knowledge-base/release-guardrails.md)) across every managed + native csproj declared in `manifest.json package_families[]`. 9 TUnit tests cover happy path + per-invariant violation. Wired into `PreFlightCheckTask` as the third validation step (after version consistency + strategy coherence). DI registration in `Program.cs`.
- Build-host test suite: 256/256 green (was 247, +9 new validator tests).
- PreFlight live run: 6 families × 10 csprojs × 8 invariants all green.
- PD-1 resolved empirically (MinVer + `<IncludeBuildOutput>false</IncludeBuildOutput>` interact cleanly).
- PD-9 opened to track ecosystem evolution around within-family exact-pin auto-derivation.
- Family identifier rename `core/image/...` → `sdl2-core/sdl2-image/...` propagated through manifest, csproj properties, MinVerTagPrefix, test fixtures, and 6 canonical docs.

**Production-time version flow constraint discovered:** standalone `dotnet pack` cannot auto-derive within-family exact pin from MinVer due to MSBuild static-eval timing. Documented in [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md` Part 3](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md#part-3-production-time-version-flow-constraint-empirical-finding-2026-04-16). Production path is Cake-driven two-step orchestration (Stream D-local). Industry survey confirms this is the frontier — no major .NET multi-package monorepo solves it.

---

#### Original A-risky Plan (preserved for traceability)

This stream applies three coordinated changes to `src/` csproj files in a single pass, because all three touch the same files and are interdependent:

1. **MinVer integration**
   - File: `Directory.Packages.props` — add MinVer package version
   - File: `src/Directory.Build.props` — chain through to root + add MinVer `PackageReference` scoped to `src/` only (not `build/_build`)
   - File: `src/native/Directory.Build.props` — already imports `../Directory.Build.props` so natives inherit MinVer transparently
   - Each managed and native csproj: add `<MinVerTagPrefix>sdl2-{role}-</MinVerTagPrefix>` (e.g., `sdl2-core-`, `sdl2-image-`). Family identifier convention is `sdl<major>-<role>` per [knowledge-base/release-lifecycle-direction.md §1](../knowledge-base/release-lifecycle-direction.md#1-ubiquitous-language).
   - Native csproj: ALSO needs MinVer (same family version) — subject to PD-1 (`<IncludeBuildOutput>false</IncludeBuildOutput>` interaction)
   - Test: `dotnet build` should give `0.0.0-alpha.0.N` versions (no tags exist yet)

2. **Exact-pin csproj shape rollout (Mechanism 3 from A0)**
   Apply the proven A0 mechanism to every managed satellite csproj. Per managed package:
   - Add `PrivateAssets="all"` to the existing Native `ProjectReference`
   - Add `<PackageVersion Include="Janset.SDL2.{Role}.Native" Version="[$(Sdl2{Role}FamilyVersion)]" />` (bracket notation, canonical `Sdl<Major><Role>FamilyVersion` property)
   - Add `<PackageReference Include="Janset.SDL2.{Role}.Native" />`
   - Core family: same pattern but no cross-family `ProjectReference`
   - The family version property defaults in each csproj to `$(Version)` (MinVer-set) with a restore-safe `0.0.0-restore` sentinel fallback. [`src/Directory.Build.targets`](../../src/Directory.Build.targets) rewrites `PackageVersion`'s `Version` metadata to `[$(Version)]` `BeforeTargets="GenerateNuspec"` so the nuspec carries the correct family version, never the sentinel.
   - **Validation:** `dotnet pack -p:Version=0.0.1-test` on each managed csproj, inspect nuspec: Native dep must be `[0.0.1-test]`, Core dep must be `0.0.1-test`
   - See [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) for mechanism details and empirical proof

3. **Structural lock: PreFlight csproj pack contract validator**
   Add a new validator to PreFlightCheckTask that verifies the exact-pin csproj shape and family-identifier coherence have not drifted. This is the permanent regression guard for Mechanism 3 + the canonical `sdl<major>-<role>` naming convention. Runs locally via `dotnet cake --target=PreFlightCheck` immediately; CI wiring comes with Stream C.
   - Reads `manifest.json` `package_families[]` to discover managed + native project pairs
   - For each managed satellite csproj, asserts (guardrails G1–G8 from [`knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md)):
     - Native `ProjectReference` exists with `PrivateAssets="all"` metadata (G1)
     - Matching `PackageReference` to the Native PackageId exists (G2)
     - Matching `PackageVersion` item uses bracket notation (`[...]`) (G3)
     - csproj `<MinVerTagPrefix>` equals `package_families[].tag_prefix + "-"` (G4)
     - Family-version property name matches canonical `Sdl<Major><Role>FamilyVersion` (G5)
     - csproj `<PackageId>` equals `Janset.SDL<Major>.<Role>` (G6)
     - Native `ProjectReference` path resolves to `package_families[].native_project` (G7)
     - Family-version property defaults to `$(Version)` with `0.0.0-restore` sentinel fallback (G8)
   - For each core managed csproj: same checks (minus cross-family ProjectReference; G6 with role = "Core")
   - For each native csproj: assert `<MinVerTagPrefix>` equals `package_families[].tag_prefix + "-"` and `<PackageId>` equals `Janset.SDL<Major>.<Role>.Native`
   - For `package_families[].depends_on` and `package_families[].library_ref` cross-section references: assert they exist (G17, G18)
   - On failure: PreFlight fails with clear message identifying which csproj, which invariant broke, and which canonical doc rule it violates
   - **Why PreFlight and not a unit test:** this is a structural config check (like version consistency and strategy coherence), not a code behavior test. PreFlight is the established home for "is the repo shape valid before we build?"
   - **Defense-in-depth note:** these guardrails are the FIRST layer. The MSBuild guard target (G9) catches sentinel leakage at build time. The post-pack nuspec assertion (G20–G27, Stream D-local) catches any drift in the produced nupkg itself. See [`knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md) for the full multi-layer design.

**Why these three go together:** MinVer adds `<MinVerTagPrefix>` to each csproj. Mechanism 3 adds `PrivateAssets` + `PackageReference` + `PackageVersion` to each managed csproj. Both touch the same ItemGroups and PropertyGroups. Doing them separately means two passes over the same files with merge risk. The structural lock follows immediately because the invariants it guards are created in the same change — there is no window where the shape exists but the guard does not.

**Note:** PreFlightCheckTask's version resolution (Amendment 1) does **not** depend on MinVer rollout. PreFlight reads git tags directly using NuGet.Versioning (delivered by A-safe). A-risky is strictly about project/pack-time versioning for `src/` csprojs.

### Stream B: Strategy Wiring (#85 closure)

**No dependency on A0 or A. Can run in parallel.**

B-done = the live pipeline respects the already-landed strategy layer. The 196 existing tests must stay green throughout; broad test rewrites are a smell (seam is in the wrong place).

**B closure criteria (all three must land to close #85):**

1. Program.cs DI wiring (register IPackagingStrategy + IDependencyPolicyValidator)
2. HarvestTask validation step (call validator after closure walk)
3. PreFlightCheck strategy coherence (call StrategyResolver for all runtimes). **If B touches PreFlightCheck code, leave a clean version-resolution seam so Amendment 1 (Stream C) can plug in without a second invasive rewrite.** PreFlight version resolution itself is still Stream C's work; B just makes it easy to add.

**HarvestPipeline extraction is explicitly OUT of #85 closure.** It is tracked separately in #87. This prevents the "98% done forever" trap. Extraction is cleanliness work; it is not the same as landing the policy seam into runtime behavior.

Design reference: `docs/research/cake-strategy-implementation-brief-2026-04-14.md`.

### Stream C: CI Modernization (after Stream A)

**Depends on Stream A (manifest schema for matrix generation).**

**Local validation:** Run the cross-platform smoke matrix after landing CI workflow changes. See [playbook/cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md).

**Key principle: the preflight-gate job is the single checkpoint before CI resources are spent.** It validates three concerns in sequence: structural integrity (PreFlightCheck), code quality (unit tests + coverage ratchet), and — once landed — packaging contract shape. If any concern fails, no matrix jobs spawn.

1. **Expand PreFlightCheckTask as the CI gate**
   - Runs BEFORE matrix generation
   - Validates: manifest.json schema integrity, vcpkg.json↔manifest version consistency, triplet↔strategy coherence (#85), package_families↔library_manifests cross-reference
   - **csproj pack contract validation (from A0):** for every managed satellite package, verify that the Native ProjectReference carries `PrivateAssets="all"` and a matching bracket-notation `PackageReference`/`PackageVersion` pair exists. Catches accidental removal of the exact-pin shape before pack runs.
   - **Trigger-aware version resolution** (Amendment 1): tag push → resolve from tag, fail if missing. Main push / manual → prerelease fallback allowed, info log.
   - On failure: entire pipeline stops, no matrix jobs spawn

2. **Code quality gate (same preflight-gate job, after PreFlightCheck)**
   - Runs: `dotnet test build/_build.Tests/` — Cake unit tests must pass before any build
   - Runs: `dotnet cake --target=Coverage-Check` — coverage ratchet against `build/coverage-baseline.json`. Already implemented (#86), currently local-only. Stream C wires it into CI.
   - On failure: entire pipeline stops, no matrix jobs spawn

3. **Cake GenerateMatrixTask**
   - New task: reads `manifest.json` runtimes, outputs GHA-compatible JSON
   - Runs AFTER PreFlightCheck passes
   - Groups by OS family (windows/linux/macos) for reusable workflow routing
   - Output: three JSON matrices (one per OS family)

4. **CI pipeline shape**

   ```text
   preflight-gate job:
     PreFlightCheck        → structural integrity (manifest, strategy, csproj pack shape)
     dotnet test           → unit tests
     Coverage-Check        → coverage ratchet
     ↓ (only if all pass)
   GenerateMatrix
     → dynamic JSON from manifest
     ↓
   Build/Harvest (7 RID jobs from matrix)
     ↓
   Consolidate → Package (+ post-pack nuspec assertion) → Smoke Test → Publish
   ```

5. **Pure-dynamic first validation**
   - Run GenerateMatrixTask with current manifest
   - Validate that the dynamic matrix produces the same 7 RID jobs as the hardcoded YAML
   - Don't update workflows yet — just prove the matrix generation works

6. **CI workflow migration** (after validation)
   - Add `preflight-gate` job that runs Cake PreFlightCheckTask + unit tests + Coverage-Check
   - Add `generate-matrix` job that runs Cake GenerateMatrixTask (needs: preflight-gate)
   - Replace hardcoded matrix with `fromJson()`
   - Keep reusable workflows (windows/linux/macos) — they handle OS-specific setup

### Stream D-local: PackageTask + Local Validation (after A-safe + A-risky + A0 + B)

**Local validation:** Harvest + ConsolidateHarvest checkpoints from the smoke matrix must pass on all 3 platforms before PackageTask consumes their output. See [playbook/cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md).

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
   - Passes family version to both restore and pack (`-p:ImageFamilyVersion=x.y.z` etc.) — version mismatch between restore and pack causes NuGet `NU5016`, which is a safety net
   - **Applies exact pin mechanism** from A0 spike: csproj shape uses `PrivateAssets="all"` on Native ProjectReference + explicit `PackageReference Version="[$(FamilyVersion)]"`. See [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md)
   - **Post-pack nuspec assertion (A0 regression guard):** opens each produced `.nupkg`, parses the embedded `.nuspec`, asserts that within-family Native dependency uses exact range `[x.y.z]` and cross-family Core dependency uses minimum range `x.y.z`, across all TFM dependency groups. This is the permanent home of the A0 acceptance criterion — defense-in-depth beyond Stream C's csproj structural check
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

**Local validation:** Source Mode visibility must be verified on all 3 platforms (Windows, WSL/Linux, macOS). The cross-platform smoke matrix ([playbook/cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md)) provides the baseline environment setup and known gotchas for each platform.

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

## Open Alignment Items (pre-Stream C)

These items must be aligned on before Stream C (CI modernization) starts. They do not block the current coverage ratchet (#86) or the HarvestPipeline extraction follow-up. They do gate Stream C's CI workflow migration and dynamic matrix rollout.

| # | Item | Status | Exit Criterion | Gates |
| --- | --- | --- | --- | --- |
| PA-1 | Matrix strategy review — RID-only vs `strategy × RID` vs parity-job | Open — alignment discussion pending. Current locked decision in [release-lifecycle-direction.md §5](../knowledge-base/release-lifecycle-direction.md#5-ci-matrix-model) is RID-only (7 jobs). | Explicit decision recorded: keep current (and close PA-1) or amend `release-lifecycle-direction.md §5` with the chosen variant. | Stream C `GenerateMatrixTask` shape; Stream C CI workflow migration. |
| PA-2 | Hybrid overlay triplet expansion to remaining 4 RIDs | Open — 3/7 RIDs covered today (`x64-windows-hybrid`, `x64-linux-hybrid`, `x64-osx-hybrid`). 4 remaining use stock triplets. Pulled into Phase 2a scope from [plan.md Known Issue #7](../plan.md#known-issues). | Overlay triplet files exist for `x86-windows-hybrid`, `arm64-windows-hybrid`, `arm64-linux-hybrid`, `arm64-osx-hybrid`. `manifest.runtimes[].triplet` + `runtimes[].strategy` updated per the intended strategy allocation. PreFlightCheck strategy coherence green across all 7 RIDs. | Stream C dynamic matrix — without this, hybrid validator is exercised in only 3/7 jobs, diluting the value of 7-job coverage. |

### PA-1 discussion boundary

Three arketypes on the table. This list is the agreed scope of discussion, not a resolution — closure is required before Stream C CI workflow migration starts, not mid-migration:

- **A — Keep RID-only.** After PA-2 lands, hybrid validator runs on 7/7 RIDs if the strategy allocation declares all 7 as hybrid. Pure-dynamic validator (pass-through today) runs only where manifest strategy is declared pure-dynamic. No additional CI cost.
- **B — Add parity axis.** Same RID runs both hybrid and pure-dynamic builds in parallel, catching cross-strategy regressions at the cost of doubled CI time per parity-covered RID.
- **C — Cross-strategy validation job.** A dedicated job re-validates a hybrid artifact under pure-dynamic expectations (or vice versa). Cheaper than B but introduces a new artifact contract.

### PA-2 scope

- Author overlay triplet files in `vcpkg-overlay-triplets/`: `x86-windows-hybrid`, `arm64-windows-hybrid`, `arm64-linux-hybrid`, `arm64-osx-hybrid`.
- Review `vcpkg-overlay-ports/` for per-architecture per-port overrides (e.g., SDL2_mixer codec bundling). Arch-specific adjustments may or may not be needed; review before authoring overlays.
- Update `manifest.json` `runtimes[].triplet` for the 4 RIDs from stock to the new hybrid overlay. The associated `strategy` field moves to `HybridStatic` where the target is to bring the RID under hybrid validation. Whether any of the 4 intentionally stays pure-dynamic (e.g., arm64 cross-compile constraints) is a strategy allocation choice made during this work — PA-2 delivers the mechanism; strategy allocation is a sub-decision captured alongside the overlay files.
- Re-run `PreFlightCheckTask`; strategy coherence must be green across all 7 RIDs under the new allocation.
- On close: update [plan.md Known Issue #7](../plan.md#known-issues) to reflect completion and remove the Phase 2b deferral note.

## Pending Decisions

| # | Decision | Owner | Status | Exit Criterion | Blocks |
| --- | --- | --- | --- | --- | --- |
| PD-1 | MinVer for native payload-only csproj: does `<IncludeBuildOutput>false</IncludeBuildOutput>` prevent MinVer from setting `Version`? | Stream A-risky implementer | **Resolved 2026-04-16** — MinVer 7.0.0 sets `$(Version)` correctly on native csprojs even with `<IncludeBuildOutput>false</IncludeBuildOutput>`. Empirically verified: `dotnet pack src/native/SDL2.Image.Native/SDL2.Image.Native.csproj` produces `Janset.SDL2.Image.Native.0.0.0-alpha.0.117.nupkg` with correct nuspec `<version>` (no tag → MinVer fallback). MinVer hooks `BeforeTargets="GenerateNuspec"` so the version is set in time for pack regardless of build output suppression. See empirical evidence in PD-1 probe artifacts and [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md` Part 3](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md#part-3-production-time-version-flow-constraint-empirical-finding-2026-04-16). | Resolved. | No longer blocking — A-risky and D-local unblocked |
| PD-2 | Exact pin mechanism: which approach produces both within-family `[x.y.z]` and cross-family minimum range in the same `.nupkg`? | Stream A0 spike | **Resolved 2026-04-16** — `PrivateAssets="all"` on Native ProjectReference + explicit `PackageReference` with bracket notation. Empirically verified on .NET SDK 9.0.309 across 4 TFMs with parameterized versions. LibGit2Sharp production precedent. First automated guard lands in A-risky (PreFlight csproj structural validator); second guard lands in D-local (post-pack nuspec assertion). See [`exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) | Resolved. Mechanism proven and documented. Automated regression guards land in A-risky and D-local. | No longer blocking — A-risky and D-local unblocked |
| PD-3 | dotnet-affected: NuGet library or CLI wrapper? | Stream E 2a feasibility spike | Open — 2a feasibility only, full decision in 2b | ADR-style note committing to one path | Stream E full implementation (2b) |
| PD-4 | Source Mode native payload visibility mechanism | Stream F implementer | **Mechanism locked 2026-04-15** — verified on Windows (worktree), Linux (WSL Ubuntu, 2-level chain), and macOS (SSH Intel Mac, Darwin 24.6, 3-level dylib chain). End-to-end validation with real SDL2 natives pending Stream F execution. See [`source-mode-native-visibility-2026-04-15.md`](../research/source-mode-native-visibility-2026-04-15.md) | Locked: platform-branched — Windows `<Content>` + `CopyToOutputDirectory`, Linux/macOS `<Target>` + `<Exec cp -a>` (preserves symlink chains at 1× size). Opt-in `Directory.Build.targets` at solution root, flag via `test/Directory.Build.props` (Phase 2a preset), staging at `artifacts/native-staging/<rid>/native/`. Tar.gz is NOT used in Source Mode (only shipping graph) | — (informs Stream F shape) |
| PD-5 | Non-host RID local acquisition path | Stream F extension | **Direction locked 2026-04-15** via two-source framework in [`source-mode-native-visibility-2026-04-15.md`](../research/source-mode-native-visibility-2026-04-15.md) §7.2 (`--source=remote --url=<url>`). Concrete mechanism (URL convention, producer workflow, artifact granularity, auth, caching) still open — 2b scope | Producer workflow chosen, URL/artifact convention defined, auth story settled, end-to-end validated by downloading real natives + extracting to staging on a non-host RID | `--source=remote` full implementation (2b) |
| PD-6 | `.NET Framework` (`net462`) source-mode visibility: how do `net462` in-tree tests see natives in Source Mode? | Future implementer | Open — **must resolve before any `net462` in-tree test is added** | Mechanism documented and tested; analogous to today's `buildTransitive` .NET Framework copy hook but activated by `$(JansetSdl2SourceMode)` | Any future `net462` in-tree test project |
| PD-7 | Full-train release orchestration mechanism: how does a coordinated multi-family release get invoked, ordered, surfaced, and recovered? | Stream D-ci research session | Open — placeholder doc published 2026-04-16 with scope + candidate paths (manual multi-tag / meta-tag + `release-set.json` / pack-time override / hybrid) + industry precedents to survey. Interim operational mechanism = Path A (manual multi-tag push). See [`research/full-train-release-orchestration-2026-04-16.md`](../research/full-train-release-orchestration-2026-04-16.md) | Path chosen with rationale; all six research questions answered (mechanism, release ordering, GitHub Release UX, notes aggregation, failure recovery, industry precedents surveyed); decision checked against criteria; `full-train-release.md` playbook drafted | Stream D-ci CI release pipeline (cannot land full-train automation without this) |
| PD-8 | Release recovery + manual escape hatch: how does an operator manually publish individual families and full trains when CI is broken or unavailable? | Stream D-local + D-ci research session | Open — placeholder doc published 2026-04-16 with scope, two escape-hatch categories (individual + full-train), seven research questions (Cake helper surface, API key provisioning, smoke-test-as-manual-gate, partial-train recovery, tag hygiene, auditability, industry precedents). Interim mechanism: operators replicate CI step-for-step by hand using documented `dotnet restore` + `dotnet pack --no-restore` + `dotnet nuget push` sequence. See [`research/release-recovery-and-manual-escape-hatch-2026-04-16.md`](../research/release-recovery-and-manual-escape-hatch-2026-04-16.md) | All seven questions answered; `playbook/release-recovery.md` exists with operator-executable step lists; Cake `Pack-Family` / `Smoke-Family` / `Push-Family` helpers implemented (or explicitly deferred); API key provisioning policy documented; industry precedent survey complete | Stream D-local Cake helper exposure, Stream D-ci CI publish pipeline (manual flow must mirror CI flow step-for-step) |
| PD-9 | Within-family exact-pin auto-derivation from MinVer for standalone `dotnet pack`: can the bracket-notation `PackageVersion` capture MinVer's resolved `$(Version)` without an explicit Cake-driven two-step orchestration? | Future SDK / NuGet evolution | **Frontier confirmed 2026-04-16** — no major .NET multi-package monorepo solves this (LibGit2Sharp hardcodes literal, SkiaSharp uses minimum range, Avalonia uses minimum range, Magick.NET hardcodes, SDL3-CS bundles). MSBuild item static-evaluation timing locks `PackageVersion.Version` metadata before MinVer's targets fire; restore-time hook chicken-and-egg with MinVer package targets loading. Standalone `dotnet pack` produces sentinel `[0.0.0-restore]`, blocked by `_GuardAgainstShippingRestoreSentinel` MSBuild guard. **Production path** = Cake-driven two-step orchestration (Stream D-local). See [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md` Part 3](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md#part-3-production-time-version-flow-constraint-empirical-finding-2026-04-16). | Either MSBuild / NuGet / MinVer evolves to expose a restore-time hook that runs after package targets load but before item static-eval lock-in, OR a community pattern emerges (e.g., NuGetizer adopts a solution). At that point we re-evaluate whether the MSBuild guard + Cake orchestration can be simplified. | Not blocking — current approach (Cake two-step + MSBuild guard) works. This PD tracks "watch for ecosystem evolution," not active work. |
