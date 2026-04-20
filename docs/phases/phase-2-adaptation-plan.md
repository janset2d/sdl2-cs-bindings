# Phase 2 Adaptation Plan — Release Lifecycle Implementation

**Date:** 2026-04-15 (last amended: 2026-04-18 — PA-1 closure + PA-2 mechanism landing)
**Status:** Approved — implementation in progress. Streams A-safe and B landed (#85 closed, #87 follow-up open). **Stream A0 RETIRED and Stream A-risky partially reverted on 2026-04-17 per S1 adoption — see "S1 Adoption Record" section below.**
**Prerequisite:** [Release Lifecycle Direction](../knowledge-base/release-lifecycle-direction.md) (locked; amended 2026-04-17 for S1)
**Issue context:** #54, #55, #63, #83, #85, #87

## Context

Release lifecycle direction is locked. Canonical docs and issues are cleaned up. This plan describes how to apply those decisions to the actual codebase: manifest.json, csproj files, Cake build host, CI workflows, and local dev model — holistically.

This is a multi-session project. The plan designs the full picture, identifies the implementation order, and what depends on what.

## S1 Adoption Record (2026-04-17)

After Stream A-risky landed (2026-04-16) and D-local integration started, production Cake `Package` invocations reproduced `NU5016` errors that blocked the managed pack step. The pre-S1 failure mode is that NuGet's pack-time sub-evaluation does not preserve the expected version inputs in this scenario: CLI globals (`Version`, `Sdl<Role>FamilyVersion`) are not visible inside the ProjectReference walk, the csproj's restore-safe sentinel fallback fires, and `PackageVersion Version="[$(Sdl<Role>FamilyVersion)]"` freezes at `[0.0.0-restore]`. Our best-diagnosed mechanical explanation is the `<MSBuild Projects="..." Properties="BuildProjectReferences=false;">` invocation at [`NuGet.Build.Tasks.Pack.targets` line 335](../../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md) — an explicit `Properties=` attribute that appears to replace the child evaluation's global property set rather than extend it. We consider this strong supporting evidence rather than the sole definitive root cause; the operational decision holds regardless of which exact internal mechanism is at fault. Research confirmed the code at that site has shipped unchanged for 8+ years ([NuGet/NuGet.Client#1915](https://github.com/NuGet/NuGet.Client/pull/1915), 2018-01-05) and is identical in .NET 10 SDK; no merged or in-flight upstream fix exists ([NuGet/Home#11617](https://github.com/NuGet/Home/issues/11617) open since 2022, [NuGet/Home#5556](https://github.com/NuGet/Home/issues/5556) open since 2017).

**Decision (S1):** Retire within-family exact-pin. Adopt SkiaSharp-style minimum range (`>=`) for the within-family dependency, matching the industry precedent for native-bundled .NET libraries (SkiaSharp, Avalonia, OpenTelemetry). Drift protection moves from consumer-side nuspec invariant to orchestration-time invariant: Cake `PackageTask` packs both managed and native at identical `--family-version` in a single invocation; post-pack validator asserts `<version>` match + minimum-range dependency shape before release.

**What changed:**

- Strategy: [`release-lifecycle-direction.md`](../knowledge-base/release-lifecycle-direction.md) §1 Dependency Contracts, §4 Dependency Contract Model (new "Drift Protection Model" subsection), §Tradeoffs (new item 5). Within-family `=` → `>=`. Amended 2026-04-17.
- Streams: Stream A0 RETIRED. Amendment 2 (BLOCKING: exact-pin spike required) SUPERSEDED. Stream A-risky partially reverted (Mechanism 3 csproj shape + sentinel + `_GuardAgainstShippingRestoreSentinel` target removed; MinVer + family identifier rename + G4/G6/G7/G17/G18 guardrails retained). Stream D-local simplified from 4-step pack orchestration to 2-step (`Pack native → Pack managed`, both with `$(NativePayloadSource)`).
- Guardrails: G1/G2/G3/G5/G8/G9/G10/G20/G24 deleted. G23 reframed as primary within-family coherence check. G11 flagged REVISIT.
- PDs: PD-2 Resolved → **Withdrawn**. PD-9 Frontier-confirmed → **Closed (not applicable)**. PD-11 opened to record S1 decision rationale.

**ADR-001 Adoption Addendum (2026-04-18):** D-3seg versioning (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`), package-first consumer contract, Artifact Source Profile abstraction. Source Mode (ProjectReference-based Content injection) retired. PD-4 (Source Mode mechanism) → **CLOSED (mechanism retired)**, PD-5 (non-host RID local acquisition) → direction reframed as RemoteInternal profile implementation, PD-6 (net462 source-mode visibility) → **CLOSED (not applicable — package-first contract via `Janset.SDL2.Native.Common.targets` handles net462 via buildTransitive)**. **PD-12 opened** to record D-3seg adoption rationale. Four new guardrails (G54–G57). See [ADR-001](../decisions/2026-04-18-versioning-d3seg.md).

**What didn't change:**

- Separation of managed + .Native packages (SkiaSharp topology).
- Fat `.Native` with all 7 RIDs (no per-platform split).
- MinVer 7.0.0 integration, `<MinVerTagPrefix>` per csproj, family identifier convention `sdl<major>-<role>`.
- Family as release unit (same version, same time).
- Release train ordering, full-train triggers, targeted release.
- Cross-family `>=` to Core (already minimum range).
- `$(NativePayloadSource)` contract (Stream F compatibility preserved).
- CI matrix shape, Two Version Planes, release promotion path.
- `_GuardAgainstEmptyNativePayload` (G46, post-S1) — unrelated to exact-pin.
- All 6 native csprojs and `src/native/Directory.Build.props`.

**Supporting evidence and research:** [`docs/research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (mechanism proven, now marked SUPERSEDED), [`artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md`](../../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md) (root-cause investigation, resolution recorded).

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

## Strategy State Audit (2026-04-17)

This section exists because the wiring-landed narrative around the Cake strategy layer (Stream B / #85) is easy to read as "the strategy pattern actively dispatches per-RID behavior." That would be misleading today. What follows is an honest snapshot of the brief design vs what actually lives in the code.

### Source of truth for the design

[`docs/research/cake-strategy-implementation-brief-2026-04-14.md`](../research/cake-strategy-implementation-brief-2026-04-14.md) — specifically the "Three New Interfaces (Reduced from Four)" section and the "Scanner Repurposing" paragraphs. Everything below is measured against that brief.

### Interface-level landing map

| Brief element | Design intent | Code status (2026-04-17) | Honest label |
| --- | --- | --- | --- |
| `IPackagingStrategy` | Named accessor: `Model` + `IsCoreLibrary(string vcpkgName)` | [`build/_build/Modules/Contracts/IPackagingStrategy.cs`](../../build/_build/Modules/Contracts/IPackagingStrategy.cs) + 2 × ~22-line implementations in `Modules/Strategy/{HybridStatic,PureDynamic}Strategy.cs`. `IsCoreLibrary` is a string-compare helper. Consumed in exactly one site (`HybridStaticValidator.Validate`). Packaging module does not consume it. | **Landed as designed** — the brief never asked for dispatcher logic here; it is a manifest-backed name lookup. |
| `IDependencyPolicyValidator` | Strategy-aware harvest-closure policy | `HybridStaticValidator` has real behavioral logic (transitive-dep leak detection via scanner output). `PureDynamicValidator` is a one-line pass-through (`return ValidationResult.Pass(_mode);`). Both wired via DI per-RID in `Program.cs`. `HarvestTask.ExecuteHarvestPipelineAsync` invokes the resolved validator. | **Landed as designed** — brief explicitly specified `PureDynamicValidator.cs ← NEW (passthrough — allows all deps)`. Pass-through is intentional legacy-compat, not a stub to fill in. |
| `INativeAcquisitionStrategy` | `NativeSource` enum (VcpkgBuild / Overrides / CiArtifact) + `GetBinaryDirectory(triplet)` | **Not implemented.** Zero matches for the symbol in `build/_build/`. | **Retired 2026-04-18 by ADR-001.** The brief's acquisition-strategy role is subsumed by the Artifact Source Profile abstraction (`IArtifactSourceResolver` + `ArtifactProfile { Local, RemoteInternal, ReleasePublic }` per ADR-001 §2.7). `NativeSource` enum's VcpkgBuild / Overrides / CiArtifact categories map roughly to the `ArtifactProfile` modes with different semantics (profile is about feed prep for downstream consumers, not about native binary acquisition routes). The interface is not re-added under its original name — the resolver abstraction replaces it cleanly. |
| `IPayloadLayoutPolicy` | Windows direct-copy vs Unix archive (deferred in the brief) | **Still deferred.** Brief said "Policy extraction can happen when PackageTask is implemented." PackageTask is implemented. The policy extraction did not follow. Packaging module hard-codes the layout today. | **Still deferred — triggering condition met but not acted on.** Either extract now, or promote the "deferred" status to "rejected" with rationale. |
| Scanner → validator repurposing | Scanners keep their original role and gain a second consumer (`HybridStaticValidator`) | `Modules/DependencyAnalysis/{WindowsDumpbinScanner,LinuxLddScanner,MacOtoolScanner}.cs` unchanged from their pre-strategy form. `BinaryClosureWalker` calls them; `HybridStaticValidator` reads the resulting `BinaryClosure` as a second consumer with zero scanner code changes. | **Landed as designed** — the specific architectural move the brief called out ("repurposed as guardrail input") is fully realized for the hybrid path. |

### What the strategy layer actually differentiates today

Exactly two things vary between `hybrid-static` and `pure-dynamic` in the current code:

1. **Harvest-closure validation.** Hybrid RIDs run `HybridStaticValidator` and fail on transitive-dep leak. Pure-dynamic RIDs run `PureDynamicValidator`, which always returns `Pass`.
2. **Declarative coherence.** `PreFlightCheckTask`'s `StrategyCoherenceValidator` asserts `manifest.runtimes[].triplet` and `manifest.runtimes[].strategy` agree (string-level check) for all 7 RIDs.

Everything else — `Package`, `PackageConsumerSmoke`, `PostFlight`, `DotNetPackInvoker`, `PackageOutputValidator`, `ArtifactPlanner`, `ArtifactDeployer`, the `buildTransitive/Janset.SDL2.Native.Common.targets` consumer-side logic — is strategy-agnostic. Pack output shape for a hybrid RID and a pure-dynamic RID is byte-identical (modulo the payload content that flowed through the RID-specific harvest, which is a payload concern, not a shape concern).

### What the playbook 3-platform validation actually exercised

The 2026-04-17 PostFlight sweep (`win-x64`, `linux-x64`, `osx-x64`) ran **only the original hybrid-static proof slice** end to end. The four additional runtime rows that moved onto hybrid overlay triplets during PA-2 on 2026-04-18 (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) have still never been:

- Harvested on a matching runner under the new overlay triplet allocation;
- Packed via `Package` under post-S1 guardrails (G21, G23, G47, G48 etc.);
- Consumer-smoked under `PackageConsumerSmoke`.

PreFlight coherence green for 7/7 RIDs is declarative only; it does not imply behavioral validation for the four newly-covered rows.

### Gaps worth naming so no one re-discovers them under deadline pressure

1. **Pure-dynamic path has no behavioral closure check.** After PA-2 landed on 2026-04-18, no live `manifest.runtimes[]` row uses `pure-dynamic`, so this is currently a dormant fallback path rather than an active release path. Before any future release reintroduces a pure-dynamic RID, decide whether `PureDynamicValidator` should gain an actual behavioral contract (e.g., "closure must contain SDL core + primary; transitive OS-provided libraries are permitted but satellite-embedded codec DLLs are not") or whether the project permanently retires pure-dynamic.
2. **Packaging module does not consume `IPackagingStrategy`.** If pack-time behavior ever needs to vary by strategy (e.g., "pure-dynamic nupkgs ship differently-shaped runtimes/ subtrees"), the seam needs to be added — it is not present today.
3. **`INativeAcquisitionStrategy` retired by ADR-001 (2026-04-18).** The role is subsumed by the Artifact Source Profile abstraction. See the row above and [ADR-001 §2.7](../decisions/2026-04-18-versioning-d3seg.md). No separate `INativeAcquisitionStrategy` interface is added.
4. **`IPayloadLayoutPolicy` deferral is stale.** The trigger condition ("when PackageTask is implemented") fired three weeks ago. The deferral deserves an up-or-down decision now that Packaging is real.

### Operational rule for future work

When someone says "strategy layer handles X" in a review or a PR description, treat that as a claim to verify against the code, not a given. The brief-vs-code delta above is the current reality. Update this section when any gap closes or any gap is explicitly accepted as permanent.

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
  └── Within-family constraint: minimum range `>=` (post-S1 2026-04-17); drift protection at orchestration time (Cake atomic pack + post-pack validator)

CI Workflows
  ├── preflight-gate job        → Cake PreFlightCheckTask + unit tests (MUST PASS)
  ├── generate-matrix job       → Cake GenerateMatrixTask → $GITHUB_OUTPUT
  ├── build-harvest jobs        → 7 RID jobs from dynamic matrix
  ├── consolidate job           → merge per-RID artifacts
  ├── package-publish job       → Cake PackageTask per family
  └── promote-to-public         → separate workflow, manual trigger

Local Dev (Execution Model — post-ADR-001)
  ├── SetupLocalDev --source=local   : repo pack → local folder feed → smoke.local.props
  ├── SetupLocalDev --source=remote  : internal feed download → local cache → smoke.local.props (Phase 2b)
  └── Release: tag push → CI → internal feed → manual promote to NuGet.org

Consumer contract is identical across all three: PackageReference + local folder feed.
Only feed preparation varies. Source Mode (ProjectReference chain + content injection) retired.
See ADR-001 §2.6–§2.8.
```

## Accepted Review Amendments (2026-04-15)

Six findings from independent review, all accepted. Amendments integrated into the stream descriptions below.

### Amendment 1 (High): PreFlight tag resolution — trigger-aware

PreFlight version resolution must distinguish between trigger types:

- **Tag push trigger** → resolve version from tag, tag not found = FAIL
- **Main push / manual trigger** → MinVer prerelease fallback allowed (`0.0.0-alpha.0.N`), PASS with info log

PreFlight always resolves a version and passes it downstream — the difference is whether "no tag" is an error or expected.

### Amendment 2 (High, BLOCKING): Within-family exact pin — A0 spike required — **SUPERSEDED 2026-04-17**

> **SUPERSEDED by S1 adoption (2026-04-17).** Within-family exact-pin requirement retired in favor of SkiaSharp-style minimum range (`>=`). See "S1 Adoption Record" above for rationale and cascading effects. The A0 spike (below) DID prove the mechanism, but the mechanism was retired because it hit upstream NuGet limitations that are not fixed in .NET 10 and have no in-flight upstream patch. Original amendment text preserved below for traceability.

**Historical problem (SUPERSEDED):** `dotnet pack` converts ProjectReference to PackageReference with `>=` minimum version constraint. The direction doc required exact pin (`[x.y.z]` in NuGet range format). These were incompatible — ProjectReference alone could not produce exact pin.

**Decision gate:** Before PackageTask or any publish flow is implemented, this must be resolved:

- **Historical acceptance target (SUPERSEDED): Image family (satellite), not Core.** Core-only is a useful calibration probe but cannot de-risk the full packaging model. The real risk was producing both a within-family exact pin AND a cross-family minimum range **in the same package**. Only a satellite package exercised both constraints simultaneously.
- **Success criteria:** A reproducible mechanism exists to produce `Janset.SDL2.Image.nupkg` where the `Janset.SDL2.Image.Native` dependency is emitted as `[1.0.3]` (within-family exact pin in NuGet range notation; historical exact-pin target, SUPERSEDED) and the `Janset.SDL2.Core` dependency is emitted as a minimum range (e.g., `1.2.0`, meaning `>=` in NuGet semantics — **not** bracketed).
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

### Stream A0: Exact Pin Spike — **RETIRED 2026-04-17** (mechanism proven 2026-04-16; approach retired by S1 adoption)

> **RETIRED 2026-04-17.** The mechanism (Mechanism 3: `PrivateAssets="all"` + bracket-notation CPM `PackageVersion` + paired `PackageReference`) was proven mechanically correct on 2026-04-16 but retired 2026-04-17 because production orchestration hit upstream NuGet limitations (`NuGet.Build.Tasks.Pack.targets:335` `Properties="BuildProjectReferences=false;"` strips CLI globals during pack-time sub-eval). S1 adoption replaced exact pin with SkiaSharp-style minimum range. Spike results preserved in [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (marked SUPERSEDED) as historical record. See "S1 Adoption Record" above for full decision rationale. Original stream text preserved below for traceability.

**Blocked Stream D and A-risky. Both are now unblocked.**

Historical A0 task: research and prove a mechanism to produce `.nupkg` files with both within-family exact pin AND cross-family minimum range dependencies, in the same package. See Amendment 2 above for the historical acceptance target (Image family) and success criteria.

**Historical mechanism (proven 2026-04-16, SUPERSEDED):** `PrivateAssets="all"` on the Native `ProjectReference` (suppresses it from pack output) + explicit `PackageReference` with bracket notation `[$(FamilyVersion)]` (injects exact pin into nuspec). Core `ProjectReference` remains default (emits `>=` minimum range). Empirically verified: 5 test scenarios on .NET SDK 9.0.309, 4 TFMs, parameterized versions. LibGit2Sharp production precedent. See the SUPERSEDED historical research note [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md).

**Sync checkpoint:** A0's chosen shape adds `PrivateAssets="all"` on existing Native ProjectReference + a new `PackageReference`/`PackageVersion` pair. This is additive — no ProjectReference removal. MinVer rollout (Stream A-risky) is compatible: MinVer sets `$(Version)` at build time, and the family version property feeds into `[$(FamilyVersion)]` at pack time.

**Regression guard placement:** The A0 acceptance criterion originally specified a TUnit test inside Build.Tests. After review, the regression guards land in their permanent homes instead of a throwaway spike test:

- **A-risky historical guard (SUPERSEDED):** PreFlight csproj structural validator — checks that every managed satellite's Native ProjectReference has `PrivateAssets="all"` and a matching bracket-notation `PackageReference`/`PackageVersion` exists. Lands in the same change that applies Mechanism 3 to real csproj files — no window where the shape exists without a guard.
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

### Stream A-risky Historical Record: MinVer + Exact-Pin csproj Rollout + Structural Lock (LANDED 2026-04-16, exact-pin subset **RETIRED 2026-04-17**)

> **PARTIAL REVERT 2026-04-17 (S1 adoption).** The exact-pin subset of A-risky is being rolled back in Phase 3 code changes following the S1 decision. Specifically: Mechanism 3 csproj shape (PrivateAssets="all" + bracket-notation PackageVersion + PackageReference pair) removed from 5 managed csprojs; sentinel PropertyGroup removed; `_GuardAgainstShippingRestoreSentinel` target removed from `src/Directory.Build.targets`; `CsprojPackContractValidator` invariants G1/G2/G3/G5/G8 removed. **What STAYS from A-risky:** MinVer 7.0.0 integration, `<MinVerTagPrefix>` per csproj (all 10), family identifier rename to `sdl<major>-<role>`, `CsprojPackContractValidator` invariants G4/G6/G7/G17/G18, `_GuardAgainstEmptyNativePayload` (G46 — unrelated to exact-pin). See "S1 Adoption Record" above for rationale. Original A-risky delivery record preserved below for traceability.

**Previously blocked by A0 (PD-2). Unblocked + landed in a single coordinated pass on 2026-04-16.**

**Delivered (as of 2026-04-16):**

- MinVer 7.0.0 wired through `src/Directory.Build.props` (chains to root, scopes to `src/`) + `Directory.Packages.props` central package version. Native csprojs inherit transparently via `src/native/Directory.Build.props` import chain.
- All 10 csprojs (5 managed + 5 native) carry canonical `<MinVerTagPrefix>sdl2-{role}-</MinVerTagPrefix>` per the `sdl<major>-<role>` family identifier convention adopted in the same change ([release-lifecycle-direction.md §1](../knowledge-base/release-lifecycle-direction.md)).
- **Historical pre-S1 rollout:** all 5 managed satellite csprojs apply Mechanism 3 from A0: `PrivateAssets="all"` on Native ProjectReference + bracket-notation `<PackageVersion>` + `<PackageReference>` + canonical `Sdl<Major><Role>FamilyVersion` property defaulting to `$(Version)` with `0.0.0-restore` restore-safe sentinel fallback. **[Reverted by S1 — Phase 3 code changes]**
- **Historical pre-S1 guard:** `src/Directory.Build.targets` MSBuild guard target `_GuardAgainstShippingRestoreSentinel` blocks pack of any `Janset.SDL2.*` managed package when family-version property is still the sentinel. Bypass `-p:AllowSentinelExactPin=true` for deliberate sentinel inspection. Pack invocations from production Cake orchestration (Stream D-local) supply the family version explicitly. **[Reverted by S1 — Phase 3 code changes]**
- PreFlight `CsprojPackContractValidator` checks guardrails G1-G8 + G17-G18 (see [release-guardrails.md](../knowledge-base/release-guardrails.md)) across every managed + native csproj declared in `manifest.json package_families[]`. 9 TUnit tests cover happy path + per-invariant violation. Wired into `PreFlightCheckTask` as the third validation step (after version consistency + strategy coherence). DI registration in `Program.cs`. **[G1/G2/G3/G5/G8 removed by S1; G4/G6/G7/G17/G18 retained — Phase 3 code changes]**
- Build-host test suite: 256/256 green (was 247, +9 new validator tests).
- PreFlight live run: 6 families × 10 csprojs × 8 invariants all green.
- PD-1 resolved empirically (MinVer + `<IncludeBuildOutput>false</IncludeBuildOutput>` interact cleanly).
- PD-9 opened to track ecosystem evolution around within-family exact-pin auto-derivation. **[Historical exact-pin frontier; Closed by S1 2026-04-17 as not-applicable.]**
- Family identifier rename `core/image/...` → `sdl2-core/sdl2-image/...` propagated through manifest, csproj properties, MinVerTagPrefix, test fixtures, and 6 canonical docs.

**Historical production-time version flow constraint (pre-S1):** standalone `dotnet pack` could not auto-derive within-family exact pin from MinVer due to MSBuild static-eval timing. Documented in the SUPERSEDED research note [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md` Part 3](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md#part-3-production-time-version-flow-constraint-empirical-finding-2026-04-16). Production path was Cake-driven two-step orchestration (Stream D-local). **[S1 2026-04-17: the two-step orchestration hit the deeper NuGet `Properties=` globals-strip issue in `NuGet.Build.Tasks.Pack.targets` line 335. Rather than carry a local workaround, S1 retired the exact-pin requirement entirely. See "S1 Adoption Record".]**

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

2. **Historical exact-pin csproj shape rollout (Mechanism 3 from A0, SUPERSEDED)**
   Apply the proven A0 mechanism to every managed satellite csproj. Per managed package:
   - Add `PrivateAssets="all"` to the existing Native `ProjectReference`
   - Add `<PackageVersion Include="Janset.SDL2.{Role}.Native" Version="[$(Sdl2{Role}FamilyVersion)]" />` (bracket notation, canonical `Sdl<Major><Role>FamilyVersion` property)
   - Add `<PackageReference Include="Janset.SDL2.{Role}.Native" />`
   - Core family: same pattern but no cross-family `ProjectReference`
   - **Historical pre-S1 detail:** the family version property defaults in each csproj to `$(Version)` (MinVer-set) with a restore-safe `0.0.0-restore` sentinel fallback. [`src/Directory.Build.targets`](../../src/Directory.Build.targets) rewrites `PackageVersion`'s `Version` metadata to `[$(Version)]` `BeforeTargets="GenerateNuspec"` so the nuspec carries the correct family version, never the sentinel.
   - **Validation:** `dotnet pack -p:Version=0.0.1-test` on each managed csproj, inspect nuspec: Native dep must be `[0.0.1-test]`, Core dep must be `0.0.1-test`
   - See the SUPERSEDED historical research note [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) for mechanism details and empirical proof

3. **Structural lock: PreFlight csproj pack contract validator**
   Historical structural lock: add a new validator to PreFlightCheckTask that verifies the exact-pin csproj shape and family-identifier coherence have not drifted. This is the permanent regression guard for Mechanism 3 + the canonical `sdl<major>-<role>` naming convention. Runs locally via `dotnet cake --target=PreFlightCheck` immediately; CI wiring comes with Stream C.
   - Reads `manifest.json` `package_families[]` to discover managed + native project pairs
   - For each managed satellite csproj, asserts (guardrails G1–G8 from [`knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md)):
     - Native `ProjectReference` exists with `PrivateAssets="all"` metadata (G1)
     - Matching `PackageReference` to the Native PackageId exists (G2)
     - Matching `PackageVersion` item uses bracket notation (`[...]`) (G3)
     - csproj `<MinVerTagPrefix>` equals `package_families[].tag_prefix + "-"` (G4)
     - Family-version property name matches canonical `Sdl<Major><Role>FamilyVersion` (G5)
     - csproj `<PackageId>` equals `Janset.SDL<Major>.<Role>` (G6)
     - Native `ProjectReference` path resolves to `package_families[].native_project` (G7)
   - Family-version property defaults to `$(Version)` with `0.0.0-restore` sentinel fallback (G8, historical pre-S1)
   - For each core managed csproj: same checks (minus cross-family ProjectReference; G6 with role = "Core")
   - For each native csproj: assert `<MinVerTagPrefix>` equals `package_families[].tag_prefix + "-"` and `<PackageId>` equals `Janset.SDL<Major>.<Role>.Native`
   - For `package_families[].depends_on` and `package_families[].library_ref` cross-section references: assert they exist (G17, G18)
   - On failure: PreFlight fails with clear message identifying which csproj, which invariant broke, and which canonical doc rule it violates
   - **Why PreFlight and not a unit test:** this is a structural config check (like version consistency and strategy coherence), not a code behavior test. PreFlight is the established home for "is the repo shape valid before we build?"
   - **Defense-in-depth note:** these guardrails are the FIRST layer. The MSBuild guard target (G9) catches sentinel leakage at build time. The post-pack nuspec assertion (G20–G27, Stream D-local) catches any drift in the produced nupkg itself. See [`knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md) for the full multi-layer design.

**Historical rationale:** MinVer adds `<MinVerTagPrefix>` to each csproj. Mechanism 3 adds `PrivateAssets` + `PackageReference` + `PackageVersion` to each managed csproj. Both touch the same ItemGroups and PropertyGroups. Doing them separately means two passes over the same files with merge risk. The structural lock follows immediately because the invariants it guards are created in the same change — there is no window where the shape exists but the guard does not.

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
   - **csproj pack contract validation (post-S1 subset):** for every managed + native csproj declared in `package_families[]`, verify `<PackageId>` matches canonical convention (G6), `<MinVerTagPrefix>` matches `package_families[].tag_prefix + "-"` (G4), Native `<ProjectReference>` path resolves to `package_families[].native_project` (G7), and manifest cross-references are consistent (G17, G18). *(Guardrails G1/G2/G3/G5/G8 — exact-pin csproj shape — were retired by S1 2026-04-17.)*
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

### Stream D-local: PackageTask + Local Validation (after A-safe + A-risky + B) — **Simplified by S1 2026-04-17**

> **S1 2026-04-17 simplification.** Dependency on A0 removed (A0 retired). 4-step pack orchestration collapsed to 2-step per family (`Pack native → Pack managed`), both with `$(NativePayloadSource)`. Within-family exact-pin mechanism removed. Post-pack nuspec assertion simplified to minimum-range + version-match checks. See "S1 Adoption Record" for rationale.

**Local validation:** Harvest + ConsolidateHarvest checkpoints from the smoke matrix must pass on all 3 platforms before PackageTask consumes their output. See [playbook/cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md).

**Depends on:**

- **A-safe** — `package_families` schema + NuGet.Versioning in Cake (pack task reads family metadata from manifest)
- **A-risky** — MinVer project rollout (pack time reads family version from git tags via MinVer), post-S1 subset
- **B** — strategy layer wired into harvest flow (pack task consumes strategy-validated harvest output)

1. **Cake PackageTask** — #54 (post-S1 shape)
   - Reads family version from MinVer (or CLI override via `--family-version`)
   - Reads harvest-manifest.json for successful RIDs
   - Passes native payload location via `-p:NativePayloadSource=<harvest-output-root>` — native csproj packs runtimes/licenses from that staging dir, src/ tree stays untouched. See Stream F mandatory control below for the contract Stream F must honour.
   - **Pack orchestration (post-S1, 2-step per family):** `Pack(native)` with `-p:Version=<family-version> -p:NativePayloadSource=<harvest-root>`, then `Pack(managed)` with the same properties. Managed's `ProjectReference` to native is plain (no PrivateAssets, no bracket PackageVersion). Emitted managed nuspec declares `<dependency id="Janset.SDL2.*.Native" version="X"/>` as minimum range (`>=`), matching SkiaSharp convention.
   - **Post-pack validator (post-S1 shape):** opens both emitted `.nupkg` files for the family, parses nuspecs, asserts:
     - (G23) managed and native `<version>` elements match (within-family coherence)
     - (G21) cross-family Core dependency uses minimum range (`x.y.z`, not bracketed) across all TFM dependency groups
     - (G22) TFM dependency groups are consistent
     - (G25-G27) symbols package, repo SHA, metadata checks
     - *(G20 exact-pin assertion deleted by S1; G24 sentinel-leak check deleted — sentinel no longer exists)*
   - **MSBuild build-time guard (G46, 2026-04-17):** `_GuardAgainstEmptyNativePayload` hard-fails any `dotnet pack` of a `Janset.SDL2.*.Native` project that runs without `$(NativePayloadSource)` set. Closes the "accidentally ship an empty nupkg" footgun. Bypass `-p:AllowEmptyNativePayload=true` for deliberate empty packs. *(Unrelated to exact-pin — stays post-S1.)*
   - Outputs to `artifacts/packages/`

2. **Package-consumer smoke test** — #83
   - Dedicated test project using PackageReference to local folder feed
   - Validates: restore works, native binaries land, SDL_Init succeeds
   - (PD-10 orthogonal: consumer-smoke `-r <rid>` vs default-resolver-path contract is tracked separately and unaffected by S1.)

3. **Local folder feed integration**
   - Cake task to publish to local folder feed after pack
   - Package Validation Mode flow: pack → local feed → consumer test

### Stream D-ci: CI Package + Publish (after A-safe + A-risky + B + C)

> **Note (S1 2026-04-17):** A0 dependency removed. Exact-pin mechanism retired. Pack orchestration simplified to 2-step (see Stream D-local).

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

### Stream F: Local-Dev Feed Preparation + Remote Feed Acquisition

**Scope rewritten 2026-04-18 per [ADR-001](../decisions/2026-04-18-versioning-d3seg.md) §2.6–§2.8.** Pre-ADR-001 scope was ProjectReference-based "Source Mode" with MSBuild content-injection for in-tree test/sample/sandbox csprojs; that mechanism is retired. Post-ADR-001 Stream F covers **feed preparation**: how the local folder feed gets populated so every consumer-facing csproj (smoke, example, sandbox, future samples) can consume packages uniformly via `PackageReference`.

**Why F is separate from D-local:** D-local validates the shipping graph (Cake `Package` → local feed → `PackageReference` consumer → smoke). Stream F owns the local-dev feed-prep flow (either repo-produces or remote-download) and writes the per-developer consumer override (`Janset.Smoke.local.props`). Same consumer contract, different feed origin.

**Local validation:** Feed prep + IDE-open smoke must be verified on all 3 platforms (Windows, WSL/Linux, macOS). The cross-platform smoke matrix ([playbook/cross-platform-smoke-validation.md](../playbook/cross-platform-smoke-validation.md)) provides the baseline environment setup and known gotchas for each platform.

**2a deliverables:**

1. **Cake `SetupLocalDev` task — two-source framework (Local-only impl in 2a)**
   - Interface: `dotnet cake --target=SetupLocalDev --source=local|remote` (per ADR-001 §2.7–§2.8)
   - Contributor never interacts with vcpkg directly in the `local` path
   - **`--source=local` (default, 2a fully implemented)**: bootstraps vcpkg submodule + installs host-triplet + runs Harvest + ConsolidateHarvest + Package for all families at an upstream-aligned prerelease per family (e.g. `sdl2-core-2.32.0-local.<timestamp>`, `sdl2-image-2.8.0-local.<timestamp>`). Populates `artifacts/packages/` local folder feed. Writes `build/msbuild/Janset.Smoke.local.props` with `<LocalPackageFeed>` + per-family `<JansetSdl<N><Role>PackageVersion>` values matching the freshly-packed set.
   - **`--source=remote` (2a: accepted, stubbed — 2b implementation)**: will fetch prebuilt nupkgs from the internal feed into a local cache folder, then write the same override file. Consumer-side contract is identical. Stream D-ci implements the remote side.
   - Abstraction: `IArtifactSourceResolver` + `ArtifactProfile { Local, RemoteInternal, ReleasePublic }`. `LocalArtifactSourceResolver` fully implemented in 2a; `RemoteInternalArtifactSourceResolver` + `ReleaseArtifactSourceResolver` **contract-only stubs** (no half-implementation that might mislead).

2. **`Janset.Smoke.local.props` mechanism**
   - Conditional import at tail of `build/msbuild/Janset.Smoke.props`:

     ```xml
     <Import Project="$(MSBuildThisFileDirectory)Janset.Smoke.local.props"
             Condition="Exists('$(MSBuildThisFileDirectory)Janset.Smoke.local.props')" />
     ```

   - File is **gitignored** — per-developer state.
   - Generated (and re-generated on every invocation) by `SetupLocalDev`.
   - Enables IDE-open smoke csprojs to restore + build without the Cake orchestrator wrapping each build.

3. **`.gitignore` update**
   - Add `build/msbuild/Janset.Smoke.local.props`.
   - Retain: `artifacts/` (already ignored); `artifacts/native-staging/` historical entry may be removed since Source Mode staging is no longer produced.

**What Stream F does NOT own (explicitly retired by ADR-001):**

- No more `Source-Mode-Prepare` task (replaced by `SetupLocalDev`).
- No more solution-root `Directory.Build.targets` MSBuild content-injection.
- No more `$(JansetSdl2SourceMode)` opt-in property.
- No more `test/Directory.Build.props` Source-Mode preset.
- No more `artifacts/native-staging/<rid>/native/*` consumer-side staging layout (pack-time `$(NativePayloadSource)` staging is a separate concern owned by D-local — see Mandatory Control below).

**Mechanism design reference:** [ADR-001 §2.6–§2.8](../decisions/2026-04-18-versioning-d3seg.md). Historical Source Mode research note [`source-mode-native-visibility-2026-04-15.md`](../research/source-mode-native-visibility-2026-04-15.md) is DEPRECATED; symlink-preservation findings remain informational for future remote-feed tar-extract cache work.

**Exit (2a):** A contributor on any of the 3 host platforms (Windows, WSL/Linux, macOS) can run `dotnet cake --target=SetupLocalDev --source=local`, then open the solution in their IDE, and smoke csprojs restore + build + execute end-to-end without further manual property plumbing. One playbook section documents the flow with per-platform command variants.

**Deferred to 2b:** `--source=remote` full implementation — internal feed URL convention chosen, auth pattern documented, cache strategy validated, end-to-end `SetupLocalDev --source=remote` operational on all 3 host platforms. Pairs with Stream D-ci's internal-feed publish pipeline. Tracked as reframed PD-5.

#### Stream F Mandatory Control — Native Payload Contract (opened 2026-04-17)

**Origin:** Tier 2 cleanup of Stream D-local (2026-04-17) switched the native csproj pack pipeline from "stage harvest output into `src/native/<Lib>.Native/runtimes/` before pack, restore after" to "pack from an external `$(NativePayloadSource)` directory, src/ tree never touched." The old source-tree-pollution approach had three structural problems (Ctrl-C leaves stale payload in src/, concurrent family packs clobber each other, would flatten Unix symlinks if harvest ever moves off tar.gz). The new mechanism is backed by MSBuild guard G46 ([release-guardrails.md §2.2](../knowledge-base/release-guardrails.md)) — direct `dotnet pack` of a `.Native` csproj without `$(NativePayloadSource)` hard-fails.

**What Stream F MUST honour:**

1. **Layout contract.** Whatever root path Stream F hands the native csproj (whether for shipping-graph pack via D-local, or for source-graph copy via its own mechanism) MUST contain:
   - `<root>/runtimes/<rid>/native/*` — the actual binaries
   - `<root>/licenses/**` — license / copyright files
   - Any other layout requires changing the include patterns in [`src/native/Directory.Build.props`](../../src/native/Directory.Build.props).

2. **Staging layout drift check.** Stream F's current 2a direction (per §7.2 of this doc) stages to `artifacts/native-staging/<rid>/native/*` — which is a **different shape** than what the native csproj expects. The source-graph mechanism can keep its own layout because it copies to `bin/`, not into a nupkg; but if Stream F ever needs to reuse D-local's pack pipeline for a non-host RID case (e.g., `--source=remote` producing a stand-in for harvest output), the layouts have to converge. Decide at Stream F implementation time whether to:
   - (a) Keep the layouts separate (Stream F stages to `artifacts/native-staging/<rid>/native/`, D-local reads from `artifacts/harvest_output/<LibraryRef>/runtimes/<rid>/native/`), or
   - (b) Unify under a single staging convention that both source-graph copy and shipping-graph pack can consume.

3. **Symlink preservation, cross-layer.** Stream F's Unix copy uses `<Exec cp -a>` to preserve symlink chains in `bin/`. The shipping-graph pack, when it eventually packs pre-extracted Unix payloads instead of today's tar.gz, will need an equivalent preservation step upstream of the native csproj's `<Content Include>` — MSBuild's default file-copy flattens symlinks. This is out of scope for 2a but must be flagged when tar.gz is dropped.

**Assumption being made (2026-04-17):** Stream F hasn't been implemented yet. The removal of the `$(MSBuildProjectDirectory)\runtimes` fallback in `src/native/Directory.Build.props` assumes Stream F will NOT rely on source-tree-populated runtimes. If that assumption breaks during Stream F design, revisit this note and decide whether to re-add a conditional source-tree fallback or adapt Stream F's staging layout.

**Verification checklist (run before declaring Stream F complete):**

- [ ] Confirm `src/native/Directory.Build.props` still pulls runtimes/licenses ONLY from `$(NativePayloadSource)` — no source-tree fallback added silently
- [ ] Confirm G46 (`_GuardAgainstEmptyNativePayload`) still fires when `$(NativePayloadSource)` is unset
- [ ] Confirm Stream F's source-graph copy to `bin/` works independently (does NOT set or rely on `$(NativePayloadSource)`)
- [ ] If unifying layouts (option 2b above), update both this control note and the native csproj's include patterns in the same change

## Implementation Notes (from Deniz)

1. **dotnet-affected via Cake** — preferred path: NuGet library integration. Fallback: CLI wrapper via `Cake.Process` runner if library integration proves infeasible (see Amendment 4). Both paths keep orchestration in Cake.
2. **NuGet.Versioning for SemVer** — use the NuGet package for SemVer parsing/comparison within Cake
3. **Cake as single orchestration surface** — all processes through Cake, CI workflows are triggers only
4. **PreFlightCheck is the gate** — must validate everything before any CI resources are spent

## Alignment Items (pre-Stream C)

These items gate Stream C (CI modernization). PA-1 and PA-2 are now resolved at the mechanism/policy level; the remaining work is execution of Stream C itself plus broader end-to-end validation on the newly-covered rows.

| # | Item | Status | Exit Criterion | Gates |
| --- | --- | --- | --- | --- |
| PA-1 | Matrix strategy review — RID-only vs `strategy × RID` vs parity-job | Resolved 2026-04-18 — keep the locked [release-lifecycle-direction.md §5](../knowledge-base/release-lifecycle-direction.md#5-ci-matrix-model) RID-only shape (7 jobs). `strategy` remains runtime-row metadata, not a top-level CI matrix axis. Supporting analysis: [ci-matrix-strategy-review-2026-04-17.md](../research/ci-matrix-strategy-review-2026-04-17.md). | Decision recorded canonically in the release-lifecycle doc and plan. | Stream C `GenerateMatrixTask` shape; Stream C CI workflow migration now has a fixed matrix contract. |
| PA-2 | Hybrid overlay triplet expansion to remaining 4 RIDs | Resolved 2026-04-18 — overlay triplet files now exist for `x86-windows-hybrid`, `arm64-windows-hybrid`, `arm64-linux-hybrid`, `arm64-osx-hybrid`; all 7 `manifest.runtimes[]` rows now map to `hybrid-static`; CI's shared vcpkg setup action and manual orchestrator workflow both use hybrid triplets. | Mechanism landed. Remaining follow-up is behavioral validation on the four newly-covered rows, not configuration wiring. | Stream C dynamic matrix can now target a 7/7 hybrid allocation without stock-triplet exceptions. |

### PA-1 discussion boundary

Three archetypes were on the table. PA-1 resolved in favor of A on 2026-04-18; the list remains as decision context so future reviews do not re-open the debate from scratch:

- **A — Keep RID-only.** After PA-2 lands, hybrid validator runs on 7/7 RIDs if the strategy allocation declares all 7 as hybrid. Pure-dynamic validator (pass-through today) runs only where manifest strategy is declared pure-dynamic. No additional CI cost.
- **B — Add parity axis.** Same RID runs both hybrid and pure-dynamic builds in parallel, catching cross-strategy regressions at the cost of doubled CI time per parity-covered RID.
- **C — Cross-strategy validation job.** A dedicated job re-validates a hybrid artifact under pure-dynamic expectations (or vice versa). Cheaper than B but introduces a new artifact contract.

### PA-2 scope

- Author overlay triplet files in `vcpkg-overlay-triplets/`: `x86-windows-hybrid`, `arm64-windows-hybrid`, `arm64-linux-hybrid`, `arm64-osx-hybrid`.
- Review `vcpkg-overlay-ports/` for per-architecture per-port overrides (e.g., SDL2_mixer codec bundling). Arch-specific adjustments may or may not be needed; review before authoring overlays.
- Update `manifest.json` `runtimes[].triplet` for the 4 RIDs from stock to the new hybrid overlay. The associated `strategy` field moves to `hybrid-static` where the target is to bring the RID under hybrid validation.
- Re-run `PreFlightCheckTask`; strategy coherence must be green across all 7 RIDs under the new allocation.
- On close: update canonical docs so they stop describing the remaining four rows as stock-triplet / pure-dynamic holdouts.

## Pending Decisions

| # | Decision | Owner | Status | Exit Criterion | Blocks |
| --- | --- | --- | --- | --- | --- |
| PD-1 | MinVer for native payload-only csproj: does `<IncludeBuildOutput>false</IncludeBuildOutput>` prevent MinVer from setting `Version`? | Stream A-risky implementer | **Resolved 2026-04-16** — MinVer 7.0.0 sets `$(Version)` correctly on native csprojs even with `<IncludeBuildOutput>false</IncludeBuildOutput>`. Empirically verified: `dotnet pack src/native/SDL2.Image.Native/SDL2.Image.Native.csproj` produces `Janset.SDL2.Image.Native.0.0.0-alpha.0.117.nupkg` with correct nuspec `<version>` (no tag → MinVer fallback). MinVer hooks `BeforeTargets="GenerateNuspec"` so the version is set in time for pack regardless of build output suppression. See empirical evidence in PD-1 probe artifacts and the SUPERSEDED historical research note [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md` Part 3](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md#part-3-production-time-version-flow-constraint-empirical-finding-2026-04-16). | Resolved. | No longer blocking — A-risky and D-local unblocked |
| PD-2 | Exact pin mechanism: which approach produces both within-family `[x.y.z]` and cross-family minimum range in the same `.nupkg`? | Stream A0 spike | **WITHDRAWN 2026-04-17 (S1 adoption).** Resolved 2026-04-16 as Mechanism 3 (`PrivateAssets="all"` + bracket-notation CPM `PackageVersion` + paired `PackageReference`), verified on .NET SDK 9.0.309 across 4 TFMs. Retired 2026-04-17 because the mechanism depended on CLI globals propagating into NuGet's pack-time sub-evaluation, which they do not in the cases we tested. Best-diagnosed mechanical explanation: the `<MSBuild Properties="BuildProjectReferences=false;">` invocation at `NuGet.Build.Tasks.Pack.targets` line 335 — an explicit `Properties=` attribute that appears to replace child-eval globals rather than extend them. That specific code path is unchanged since 2018 and identical in .NET 10 SDK; no in-flight upstream fix ([NuGet/Home#11617](https://github.com/NuGet/Home/issues/11617), [NuGet/Home#5556](https://github.com/NuGet/Home/issues/5556)). We treat the specific mechanism as supporting evidence rather than sole definitive cause; S1 adopted SkiaSharp-style minimum range regardless, which sidesteps the sub-eval concern entirely. See PD-11 and "S1 Adoption Record" at top of this doc. Historical artifacts preserved at [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (marked SUPERSEDED) and [`artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md`](../../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md). | Withdrawn. Decision superseded by S1. | No longer applicable |
| PD-3 | dotnet-affected: NuGet library or CLI wrapper? | Stream E 2a feasibility spike | Open — 2a feasibility only, full decision in 2b | ADR-style note committing to one path | Stream E full implementation (2b) |
| PD-4 | Source Mode native payload visibility mechanism | Stream F implementer | **CLOSED 2026-04-18 (ADR-001).** Mechanism was locked 2026-04-15 and PoC'd on three platforms (Windows worktree, WSL Ubuntu 2-level chain, Intel Mac 3-level dylib chain), but the entire Source Mode consumer contract is retired by ADR-001 in favour of package-first local dev. Symlink-preservation findings from the PoC remain useful reference for future remote-feed tar-extract caching. Research note [`source-mode-native-visibility-2026-04-15.md`](../research/source-mode-native-visibility-2026-04-15.md) is DEPRECATED. | Closed. Mechanism retired. | Not blocking. |
| PD-5 | Non-host RID local acquisition path | Reframed under ADR-001 as `RemoteInternal` Artifact Source Profile concrete implementation | **REFRAMED 2026-04-18 (ADR-001).** Pre-ADR-001 direction was `--source=remote --url=<url>` for Source Mode staging. Post-ADR-001, the same `--source=remote` concept survives but scoped to **feed-prep** (download prebuilt nupkgs from internal feed into local cache, then the package-first consumer contract handles everything else). Interface locked in ADR-001 §2.7 as `IArtifactSourceResolver` + `RemoteInternalArtifactSourceResolver`. Concrete implementation (internal feed URL convention, auth, cache strategy) still open — Phase 2b Stream D-ci scope. | Concrete Phase 2b deliverable: internal feed URL convention chosen, auth pattern documented, cache strategy validated, end-to-end `SetupLocalDev --source=remote` operational on all 3 host platforms. | `--source=remote` full implementation (Phase 2b, Stream D-ci) |
| PD-6 | `.NET Framework` (`net462`) source-mode visibility: how do `net462` in-tree tests see natives in Source Mode? | Future implementer | **CLOSED 2026-04-18 (ADR-001 — not applicable).** Under package-first consumer contract, net462 consumers use PackageReference and pick up natives via `buildTransitive/Janset.SDL2.Native.Common.targets` (already handles net462 AnyCPU explicit-copy path, including the C3 fix 2026-04-18). Source Mode is retired; there is no separate net462 source-mode path to design. | Closed. Not applicable. | Not blocking. |
| PD-7 | Full-train release orchestration mechanism: how does a coordinated multi-family release get invoked, ordered, surfaced, and recovered? | Stream D-ci research session | Open — placeholder doc published 2026-04-16 with scope + candidate paths (manual multi-tag / meta-tag + `release-set.json` / pack-time override / hybrid) + industry precedents to survey. Interim operational mechanism = Path A (manual multi-tag push). See [`research/full-train-release-orchestration-2026-04-16.md`](../research/full-train-release-orchestration-2026-04-16.md) | Path chosen with rationale; all six research questions answered (mechanism, release ordering, GitHub Release UX, notes aggregation, failure recovery, industry precedents surveyed); decision checked against criteria; `full-train-release.md` playbook drafted | Stream D-ci CI release pipeline (cannot land full-train automation without this) |
| PD-8 | Release recovery + manual escape hatch: how does an operator manually publish individual families and full trains when CI is broken or unavailable? | Stream D-local + D-ci research session | Open — placeholder doc published 2026-04-16 with scope, two escape-hatch categories (individual + full-train), seven research questions (Cake helper surface, API key provisioning, smoke-test-as-manual-gate, partial-train recovery, tag hygiene, auditability, industry precedents). Interim mechanism: operators replicate CI step-for-step by hand using documented `dotnet restore` + `dotnet pack --no-restore` + `dotnet nuget push` sequence. See [`research/release-recovery-and-manual-escape-hatch-2026-04-16.md`](../research/release-recovery-and-manual-escape-hatch-2026-04-16.md) | All seven questions answered; `playbook/release-recovery.md` exists with operator-executable step lists; Cake `Pack-Family` / `Smoke-Family` / `Push-Family` helpers implemented (or explicitly deferred); API key provisioning policy documented; industry precedent survey complete | Stream D-local Cake helper exposure, Stream D-ci CI publish pipeline (manual flow must mirror CI flow step-for-step) |
| PD-9 | Within-family exact-pin auto-derivation from MinVer for standalone `dotnet pack`: can the bracket-notation `PackageVersion` capture MinVer's resolved `$(Version)` without an explicit Cake-driven two-step orchestration? | Future SDK / NuGet evolution | **CLOSED 2026-04-17 — Not applicable (S1 adoption).** PD-9 tracked the frontier of making within-family exact-pin auto-derive from MinVer. S1 retired the exact-pin requirement itself, so the frontier is no longer relevant to this project. PD-9 is closed. The underlying ecosystem limitation (MSBuild static-eval timing + NuGet sub-eval globals-replace) remains unsolved upstream but no longer affects us. If we ever reconsider exact-pin, reopen with reference to "S1 Adoption Record" for context. | Closed. Not applicable post-S1. | — |
| PD-10 | D-local package-consumer smoke scope: does `-r <rid>` on restore/build/run validate the realistic consumer path, or just the "runtime assets copied to bin/" subset? | Stream D-local / D-ci review (before K checkpoint promotion to active) | **Opened 2026-04-17** during Tier 2 code-review cleanup. Today's [`PackageConsumerSmoke`](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs) always emits `-r <rid>` for restore, build, and run. This triggers runtime-specific restore and SDK-level native file copy into `bin/Release/net9.0/<rid>/`, then `dotnet run -r <rid> --no-build --no-restore` executes with assets already laid out. What it does NOT exercise: the default framework-dependent path (no `<RuntimeIdentifier>` in csproj), where the runtime's native library resolver walks the NuGet cache's `runtimes/<host-rid>/native/` subtree at load time via `runtimetargets` evaluation. The two subsystems are independent — a regression in one can hide behind success in the other. Contract scope is documented in [`playbook/cross-platform-smoke-validation.md` § "Consumer Invocation Contract (Checkpoint K)"](../playbook/cross-platform-smoke-validation.md#consumer-invocation-contract-checkpoint-k). **Orthogonal to S1 (2026-04-17): S1 changed packaging mechanism; PD-10 is about consumer-side resolver path coverage. Revisit independently.** | Decision recorded: either (a) keep `-r <rid>`-only contract with a doc note that the framework-dependent path is a D-ci concern, or (b) add a second invocation in the smoke runner that builds/runs without `-r <rid>` to cover the default resolver path. If (b), update `PackageConsumerSmokeRunner` + playbook. | Not blocking — current smoke still fails on any real regression in the file-copy path. This PD tracks whether the D-local contract is complete enough for 3-platform K promotion, or whether the smoke needs a second invocation pass first. Recommended resolution moment: when promoting checkpoint K from "planned" to "active" on all 3 platforms (currently Phase 2b scope). |
| PD-11 | S1 adoption decision record: within-family exact-pin retired in favor of SkiaSharp-style minimum range — does the decision hold under any new evidence? | Future review (watch-for-reconsideration) | **RECORDED 2026-04-17 (S1 adoption).** Retires within-family exact-pin. Adopts minimum-range contract. Drift protection moves to Cake orchestration invariant + post-pack validator assertion. Full rationale in "S1 Adoption Record" at top of this doc. Supporting research: [`exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (SUPERSEDED), [`nu5016-cake-restore-investigation-2026-04-17.md`](../../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md) (root-cause confirmed). Closes PD-2 (withdrawn) and PD-9 (not applicable). Cascading changes: strategy doc amendments (§1 Dependency Contracts, §4 Drift Protection Model, §Tradeoffs item 5), guardrails doc (9 rows deleted, G23 reframed, G11 marked REVISIT), Stream A0 retired, Amendment 2 superseded, Stream A-risky partially reverted in Phase 3 code changes, Stream D-local simplified to 2-step pack per family. | Reconsidered if: (a) upstream NuGet publishes a fix for the `Properties="BuildProjectReferences=false;"` globals-strip (NuGet/Home#11617 or equivalent) AND a community pattern emerges for auto-derived exact pin, OR (b) we encounter a real-world version-drift incident that orchestration-level protection couldn't catch, OR (c) our release policy changes such that within-family pairs become independently releasable at different versions. None of (a)(b)(c) are expected in Phase 2 timeframe. | Not blocking. Decision stands until further evidence. |
| PD-13 | `--family-version` CLI flag retirement review: does the flag justify its continued existence post-D-3seg + post-local.props alignment, or should it be retired? | Stream D-local / D-ci review (next release-lifecycle iteration) | **Opened 2026-04-19** during smoke-runner ↔ Cake ecosystem reconciliation (commits `0b95a98` + `e9356a5`). Pre-ADR-001 the flag was the primary bootstrap-override for `SetupLocalDev` and PD-8 manual-escape-hatch. Under current state (post-commit `e9356a5`): (a) SetupLocalDev auto-generates per-family versions from `manifest.json library_manifests[].vcpkg_version` + local timestamp, **does not read the flag**; (b) `PackageConsumerSmoke` was forcing the flag and overriding `Janset.Smoke.local.props` auto-import — reconciled in `e9356a5` to trust local.props as single source of version truth (runner treats the flag as optional, `PackageTask` auto-skips via `ShouldRun` when flag absent so the transitive DAG edge survives); (c) release-path version source is git family tag → MinVer (§3 release-lifecycle-direction.md), not the flag; (d) D-3seg G54 guardrail rejects multi-family packs at a single flag value because families carry different UpstreamMajor.UpstreamMinor (Core 2.32 vs Image 2.8 vs Gfx 1.0). The flag's surviving legitimate surface is PD-8 manual-escape-hatch for single-family packs when CI is broken. **Question to settle:** is that one surviving use case worth keeping the flag in public CLI surface + `PackageVersionResolver` code path + documentation, or should PD-8 escape use a different mechanism (env var, short-lived git tag, suffix-only input computed against manifest)? | Decision recorded with one of: (a) **retain flag** — keep `--family-version` only in PackageTaskRunner / bootstrap paths, documented as PD-8-only escape, removed from any non-escape invocation surface; (b) **retire flag** — replace PD-8 escape with tag-push-then-pack pattern or `--version-suffix` against manifest-derived UpstreamMajor.UpstreamMinor, delete `--family-version` from `PackageVersionResolver`, update ADR-001 §3 Source of truth + §Tradeoffs item 4. Document chosen path; update this row with RECORDED status + cascading changes list. | Not blocking. `e9356a5` reconciles `PackageConsumerSmokeRunner` with local.props (removes smoke-side dependency on the flag). The flag itself still exists in `PackageVersionResolver` and `PackageBuildConfiguration` for bootstrap/PD-8 paths until this PD resolves. Resolution moment: next release-lifecycle iteration or whenever PD-8 manual-escape-hatch mechanism is specified. |
| PD-14 | Linux end-user MIDI packaging strategy: how do consumers of `Janset.SDL2.Mixer.Native` get MIDI decoder registration on Linux without violating the LGPL-free policy? | Stream D-local / Stream F design session (post-PA-2 landing, before first public release) | **Opened 2026-04-20** after the Linux MIDI decoder investigation (commits `0b95a98` + parent smoke-runner reconciliation). Factual state: SDL_mixer's bundled internal Timidity (Artistic License, shipped in our `libSDL2_mixer` build via the overlay's `SDL2MIXER_MIDI_TIMIDITY=ON`) registers the `MIDI` decoder only when a GUS-format `.pat` patch set is found at init — the `freepats` apt package (GPL) provides `/etc/timidity/freepats.cfg` + GUS patches and SDL_mixer's auto-search picks it up via `TIMIDITY_CFG_FREEPATS`. The Debian `timidity` apt package installs an SF2-based cfg (`FluidR3_GM.sf2`) that bundled Timidity cannot parse, so `apt install timidity` alone does not register the decoder. Our CI + local test hosts install `freepats` as a build-tooling prereq (GPL test-host install does not affect distribution), but the end-user story is open. **Options:** (a) **Documentation only** — README + playbook tell Linux consumers to `apt install freepats` (or equivalent) themselves; no package changes. Simplest, mirrors how SDL2_mixer itself documents the dependency. (b) **Bundled SF2 soundfont + cfg via opt-in meta-package** — ship `TimGM6mb.sf2` (Creative Commons-licensed ~5 MB) or similar permissive SF2 in a separate `Janset.SDL2.Mixer.Native.Soundfonts` NuGet, generate a `timidity.cfg` at runtime referencing it, set `SDL_SOUNDFONTS`/`TIMIDITY_CFG` env vars. Technical caveat: bundled Timidity may not parse SF2 directly — would need a compatibility layer or the FluidSynth backend (which is LGPL and we've explicitly disabled). Needs feasibility confirmation before commitment. (c) **Bundled GUS patches via opt-in meta-package** — ship a trimmed `freepats` subset with our own redistribution clearance. GUS patches typically GPL, which means bundling them in a NuGet forces the NuGet itself under GPL — conflicts with the LGPL-free policy. Probably non-starter but documented for completeness. | ADR recorded with path chosen + licensing review checked + end-user README + optional `Janset.SDL2.Mixer.Native.Soundfonts` package implementation (if (b)) OR playbook docs finalised (if (a)). Cross-reference from `docs/knowledge-base/release-lifecycle-direction.md` Tradeoffs + `vcpkg-overlay-ports/README.md`. | Not blocking for Phase 2. Current Linux CI + WSL smoke validation is complete (test-host `freepats` install); end-user UX gap surfaces only when first public Mixer package release is prepared. Resolution moment: pre-first-public-release-of-Mixer-family, coordinated with licensing review. |
| PD-15 | SDL2_gfx Unix symbol-export regression guard: the overlay patch `003-fix-unix-visibility.patch` adds `__attribute__((visibility("default")))` to four public headers so the API surface survives `-fvisibility=hidden` on GCC/Clang. No automated guard exists today to catch a vcpkg baseline bump that silently undoes the patch. | Stream D-ci / Stream E next CI hardening pass | **Opened 2026-04-20** (low-severity review observation during commit `32fbed4` inspection). The patch itself is validated end-to-end on all three host platforms (Windows `dumpbin /exports` shows 102 SDL2_gfx functions, Linux `readelf -Ws` + macOS `nm -gU` show 5-of-5 critical symbols `GLOBAL DEFAULT` / `T`). But the next vcpkg-baseline bump that rewrites SDL2_gfx's upstream source tree could cause the patch to apply with partial offsets, fail silently under `git apply --3way`, or become a no-op if the upstream layout changes — and the gap would only surface later when a downstream C# P/Invoke consumer hits `EntryPointNotFoundException` on Unix at runtime. **Options:** (a) **Smoke-time check (cheap, high-signal)** — add a post-`Harvest` step in `prepare-native-assets-{linux,macos}.yml` that runs `readelf -Ws` / `nm -gU` against the harvested `libSDL2_gfx.*` and asserts a known set of canonical symbols (`filledCircleRGBA`, `pixelColor`, `rotozoomSurface`, `SDL_initFramerate`, `aacircleRGBA`) are `GLOBAL DEFAULT` / `T`. A failure would block the CI job with a clear reason. (b) **Post-pack guardrail (G-series)** — add a new guardrail to `PackageOutputValidator` that inspects the packed `.Native` nupkg's Unix binaries and asserts the same symbol set. Higher fidelity (runs in the release path) but adds binary inspection to a C# validator that today only inspects nuspec XML + layout. (c) **Both** — cheapest smoke-time check + G-series as defence in depth. Matches the project's "defense-in-depth across PreFlight/MSBuild/post-pack/CI" posture per `feedback_strong_guardrails` memory. | Decision recorded with chosen path + implementation commit. If (a): CI workflow patch + test that simulates missing symbol. If (b): new guardrail number + `PackageOutputValidator` extension + unit test. If (c): both, sequenced. Update `vcpkg-overlay-ports/README.md` sdl2-gfx entry to note the regression guard is in place. | Not blocking. The patch is working today and every CI job will exercise it. This PD ensures the protection survives a vcpkg baseline refresh silently losing the patch. Resolution moment: next CI hardening pass OR the first vcpkg baseline bump that touches sdl2-gfx ports. |
| PD-12 | ADR-001 adoption decision record: D-3seg versioning (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`), package-first consumer contract (Source Mode retired), Artifact Source Profile abstraction (Local/RemoteInternal/ReleasePublic) — does the decision hold under any new evidence? | Future review (watch-for-reconsideration) | **RECORDED 2026-04-18 (ADR-001 adoption).** Locks three coupled decisions: versioning shape, consumer-contract unification, feed-prep abstraction. Full rationale in [ADR-001](../decisions/2026-04-18-versioning-d3seg.md). New guardrails G54 (upstream Major.Minor coherence, PreFlight), G55 (native metadata file, post-pack), G56 (satellite cross-family upper bound, post-pack), G57 (README mapping table, post-pack). Closes PD-4 (Source Mode mechanism retired) and PD-6 (net462 source-mode visibility not applicable under package-first). Reframes PD-5 as RemoteInternal profile implementation. Cascading changes: [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md) §3 + §4 + §7 + Tradeoffs rewritten; [release-guardrails.md](../knowledge-base/release-guardrails.md) G54–G57 added; [plan.md](../plan.md) Strategic Decisions rows updated; Stream F scope rewritten (source-copy mechanism retired, feed-prep contract preserved); `execution-model-strategy-2026-04-13.md` three-mode framing amended to two-source; `source-mode-native-visibility-2026-04-15.md` DEPRECATED (symlink findings retained as reference). | Reconsidered if: (a) maintainer-load analysis shows binding-only release frequency is higher than expected (the primary rejected trade-off under D-3seg), OR (b) external tooling emerges that makes 4-segment SemVer idiomatic in NuGet (currently unfavoured), OR (c) a use case for ProjectReference-chain source-graph native visibility re-emerges that the throwaway-harness escape cannot cover. | Not blocking. Decision stands until further evidence. |
