# Phase 2 Adaptation Plan — Release Lifecycle Implementation

**Date:** 2026-04-15 (last amended: 2026-04-17 — S1 adoption)
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
| `INativeAcquisitionStrategy` | `NativeSource` enum (VcpkgBuild / Overrides / CiArtifact) + `GetBinaryDirectory(triplet)` | **Not implemented.** Zero matches for the symbol in `build/_build/`. | **Design not landed.** Source Mode (Stream F, `--source=local\|remote`) covers an adjacent problem space along a different axis and may implicitly subsume this interface's role; the relationship has never been documented. Re-decide before Stream F implementation closes. |
| `IPayloadLayoutPolicy` | Windows direct-copy vs Unix archive (deferred in the brief) | **Still deferred.** Brief said "Policy extraction can happen when PackageTask is implemented." PackageTask is implemented. The policy extraction did not follow. Packaging module hard-codes the layout today. | **Still deferred — triggering condition met but not acted on.** Either extract now, or promote the "deferred" status to "rejected" with rationale. |
| Scanner → validator repurposing | Scanners keep their original role and gain a second consumer (`HybridStaticValidator`) | `Modules/DependencyAnalysis/{WindowsDumpbinScanner,LinuxLddScanner,MacOtoolScanner}.cs` unchanged from their pre-strategy form. `BinaryClosureWalker` calls them; `HybridStaticValidator` reads the resulting `BinaryClosure` as a second consumer with zero scanner code changes. | **Landed as designed** — the specific architectural move the brief called out ("repurposed as guardrail input") is fully realized for the hybrid path. |

### What the strategy layer actually differentiates today

Exactly two things vary between `hybrid-static` and `pure-dynamic` in the current code:

1. **Harvest-closure validation.** Hybrid RIDs run `HybridStaticValidator` and fail on transitive-dep leak. Pure-dynamic RIDs run `PureDynamicValidator`, which always returns `Pass`.
2. **Declarative coherence.** `PreFlightCheckTask`'s `StrategyCoherenceValidator` asserts `manifest.runtimes[].triplet` and `manifest.runtimes[].strategy` agree (string-level check) for all 7 RIDs.

Everything else — `Package`, `PackageConsumerSmoke`, `PostFlight`, `DotNetPackInvoker`, `PackageOutputValidator`, `ArtifactPlanner`, `ArtifactDeployer`, the `buildTransitive/Janset.SDL2.Native.Common.targets` consumer-side logic — is strategy-agnostic. Pack output shape for a hybrid RID and a pure-dynamic RID is byte-identical (modulo the payload content that flowed through the RID-specific harvest, which is a payload concern, not a shape concern).

### What the playbook 3-platform validation actually exercised

The 2026-04-17 PostFlight sweep (`win-x64`, `linux-x64`, `osx-x64`) ran **only the hybrid-static path** end to end. The four pure-dynamic RIDs in `manifest.runtimes[]` (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) have never been:

- Harvested on a matching runner since S1 landed;
- Packed via `Package` under post-S1 guardrails (G21, G23, G47, G48 etc.);
- Consumer-smoked under `PackageConsumerSmoke`.

PreFlight coherence green for 7/7 RIDs is declarative only; it does not imply behavioral validation.

### Gaps worth naming so no one re-discovers them under deadline pressure

1. **Pure-dynamic path has no behavioral closure check.** Today that is fine because no pure-dynamic RID is release-scoped. Before the first release that includes a pure-dynamic RID, decide whether `PureDynamicValidator` should gain an actual behavioral contract (e.g., "closure must contain SDL core + primary; transitive OS-provided libraries are permitted but satellite-embedded codec DLLs are not") or whether PA-2 retires pure-dynamic entirely (overlay hybrid triplets for the remaining 4 RIDs).
2. **Packaging module does not consume `IPackagingStrategy`.** If pack-time behavior ever needs to vary by strategy (e.g., "pure-dynamic nupkgs ship differently-shaped runtimes/ subtrees"), the seam needs to be added — it is not present today.
3. **`INativeAcquisitionStrategy` is a design ghost.** Either document that Source Mode subsumed it and retire the interface from the plan, or implement it before Stream F locks in without it.
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

### Amendment 2 (High, BLOCKING): Within-family exact pin — A0 spike required — **SUPERSEDED 2026-04-17**

> **SUPERSEDED by S1 adoption (2026-04-17).** Within-family exact-pin requirement retired in favor of SkiaSharp-style minimum range (`>=`). See "S1 Adoption Record" above for rationale and cascading effects. The A0 spike (below) DID prove the mechanism, but the mechanism was retired because it hit upstream NuGet limitations that are not fixed in .NET 10 and have no in-flight upstream patch. Original amendment text preserved below for traceability.

**Historical problem (SUPERSEDED):** `dotnet pack` converts ProjectReference to PackageReference with `>=` minimum version constraint. The direction doc required exact pin (`[x.y.z]` in NuGet range format). These were incompatible — ProjectReference alone could not produce exact pin.

**Decision gate:** Before PackageTask or any publish flow is implemented, this must be resolved:

- **Historical acceptance target (SUPERSEDED): Image family (satellite), not Core.** Core-only is a useful calibration probe but cannot de-risk the full packaging model. The real risk was producing both a within-family exact pin AND a cross-family minimum range **in the same package**. Only a satellite package exercised both constraints simultaneously.
- **Success criteria:** A reproducible mechanism exists to produce `Janset.SDL2.Image.nupkg` where:
   - `Janset.SDL2.Image.Native` dependency is emitted as `[1.0.3]` (within-family exact pin in NuGet range notation; historical exact-pin target, SUPERSEDED)
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
| PD-1 | MinVer for native payload-only csproj: does `<IncludeBuildOutput>false</IncludeBuildOutput>` prevent MinVer from setting `Version`? | Stream A-risky implementer | **Resolved 2026-04-16** — MinVer 7.0.0 sets `$(Version)` correctly on native csprojs even with `<IncludeBuildOutput>false</IncludeBuildOutput>`. Empirically verified: `dotnet pack src/native/SDL2.Image.Native/SDL2.Image.Native.csproj` produces `Janset.SDL2.Image.Native.0.0.0-alpha.0.117.nupkg` with correct nuspec `<version>` (no tag → MinVer fallback). MinVer hooks `BeforeTargets="GenerateNuspec"` so the version is set in time for pack regardless of build output suppression. See empirical evidence in PD-1 probe artifacts and the SUPERSEDED historical research note [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md` Part 3](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md#part-3-production-time-version-flow-constraint-empirical-finding-2026-04-16). | Resolved. | No longer blocking — A-risky and D-local unblocked |
| PD-2 | Exact pin mechanism: which approach produces both within-family `[x.y.z]` and cross-family minimum range in the same `.nupkg`? | Stream A0 spike | **WITHDRAWN 2026-04-17 (S1 adoption).** Resolved 2026-04-16 as Mechanism 3 (`PrivateAssets="all"` + bracket-notation CPM `PackageVersion` + paired `PackageReference`), verified on .NET SDK 9.0.309 across 4 TFMs. Retired 2026-04-17 because the mechanism depended on CLI globals propagating into NuGet's pack-time sub-evaluation, which they do not in the cases we tested. Best-diagnosed mechanical explanation: the `<MSBuild Properties="BuildProjectReferences=false;">` invocation at `NuGet.Build.Tasks.Pack.targets` line 335 — an explicit `Properties=` attribute that appears to replace child-eval globals rather than extend them. That specific code path is unchanged since 2018 and identical in .NET 10 SDK; no in-flight upstream fix ([NuGet/Home#11617](https://github.com/NuGet/Home/issues/11617), [NuGet/Home#5556](https://github.com/NuGet/Home/issues/5556)). We treat the specific mechanism as supporting evidence rather than sole definitive cause; S1 adopted SkiaSharp-style minimum range regardless, which sidesteps the sub-eval concern entirely. See PD-11 and "S1 Adoption Record" at top of this doc. Historical artifacts preserved at [`research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (marked SUPERSEDED) and [`artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md`](../../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md). | Withdrawn. Decision superseded by S1. | No longer applicable |
| PD-3 | dotnet-affected: NuGet library or CLI wrapper? | Stream E 2a feasibility spike | Open — 2a feasibility only, full decision in 2b | ADR-style note committing to one path | Stream E full implementation (2b) |
| PD-4 | Source Mode native payload visibility mechanism | Stream F implementer | **Mechanism locked 2026-04-15** — verified on Windows (worktree), Linux (WSL Ubuntu, 2-level chain), and macOS (SSH Intel Mac, Darwin 24.6, 3-level dylib chain). End-to-end validation with real SDL2 natives pending Stream F execution. See [`source-mode-native-visibility-2026-04-15.md`](../research/source-mode-native-visibility-2026-04-15.md) | Locked: platform-branched — Windows `<Content>` + `CopyToOutputDirectory`, Linux/macOS `<Target>` + `<Exec cp -a>` (preserves symlink chains at 1× size). Opt-in `Directory.Build.targets` at solution root, flag via `test/Directory.Build.props` (Phase 2a preset), staging at `artifacts/native-staging/<rid>/native/`. Tar.gz is NOT used in Source Mode (only shipping graph) | — (informs Stream F shape) |
| PD-5 | Non-host RID local acquisition path | Stream F extension | **Direction locked 2026-04-15** via two-source framework in [`source-mode-native-visibility-2026-04-15.md`](../research/source-mode-native-visibility-2026-04-15.md) §7.2 (`--source=remote --url=<url>`). Concrete mechanism (URL convention, producer workflow, artifact granularity, auth, caching) still open — 2b scope | Producer workflow chosen, URL/artifact convention defined, auth story settled, end-to-end validated by downloading real natives + extracting to staging on a non-host RID | `--source=remote` full implementation (2b) |
| PD-6 | `.NET Framework` (`net462`) source-mode visibility: how do `net462` in-tree tests see natives in Source Mode? | Future implementer | Open — **must resolve before any `net462` in-tree test is added** | Mechanism documented and tested; analogous to today's `buildTransitive` .NET Framework copy hook but activated by `$(JansetSdl2SourceMode)` | Any future `net462` in-tree test project |
| PD-7 | Full-train release orchestration mechanism: how does a coordinated multi-family release get invoked, ordered, surfaced, and recovered? | Stream D-ci research session | Open — placeholder doc published 2026-04-16 with scope + candidate paths (manual multi-tag / meta-tag + `release-set.json` / pack-time override / hybrid) + industry precedents to survey. Interim operational mechanism = Path A (manual multi-tag push). See [`research/full-train-release-orchestration-2026-04-16.md`](../research/full-train-release-orchestration-2026-04-16.md) | Path chosen with rationale; all six research questions answered (mechanism, release ordering, GitHub Release UX, notes aggregation, failure recovery, industry precedents surveyed); decision checked against criteria; `full-train-release.md` playbook drafted | Stream D-ci CI release pipeline (cannot land full-train automation without this) |
| PD-8 | Release recovery + manual escape hatch: how does an operator manually publish individual families and full trains when CI is broken or unavailable? | Stream D-local + D-ci research session | Open — placeholder doc published 2026-04-16 with scope, two escape-hatch categories (individual + full-train), seven research questions (Cake helper surface, API key provisioning, smoke-test-as-manual-gate, partial-train recovery, tag hygiene, auditability, industry precedents). Interim mechanism: operators replicate CI step-for-step by hand using documented `dotnet restore` + `dotnet pack --no-restore` + `dotnet nuget push` sequence. See [`research/release-recovery-and-manual-escape-hatch-2026-04-16.md`](../research/release-recovery-and-manual-escape-hatch-2026-04-16.md) | All seven questions answered; `playbook/release-recovery.md` exists with operator-executable step lists; Cake `Pack-Family` / `Smoke-Family` / `Push-Family` helpers implemented (or explicitly deferred); API key provisioning policy documented; industry precedent survey complete | Stream D-local Cake helper exposure, Stream D-ci CI publish pipeline (manual flow must mirror CI flow step-for-step) |
| PD-9 | Within-family exact-pin auto-derivation from MinVer for standalone `dotnet pack`: can the bracket-notation `PackageVersion` capture MinVer's resolved `$(Version)` without an explicit Cake-driven two-step orchestration? | Future SDK / NuGet evolution | **CLOSED 2026-04-17 — Not applicable (S1 adoption).** PD-9 tracked the frontier of making within-family exact-pin auto-derive from MinVer. S1 retired the exact-pin requirement itself, so the frontier is no longer relevant to this project. PD-9 is closed. The underlying ecosystem limitation (MSBuild static-eval timing + NuGet sub-eval globals-replace) remains unsolved upstream but no longer affects us. If we ever reconsider exact-pin, reopen with reference to "S1 Adoption Record" for context. | Closed. Not applicable post-S1. | — |
| PD-10 | D-local package-consumer smoke scope: does `-r <rid>` on restore/build/run validate the realistic consumer path, or just the "runtime assets copied to bin/" subset? | Stream D-local / D-ci review (before K checkpoint promotion to active) | **Opened 2026-04-17** during Tier 2 code-review cleanup. Today's [`PackageConsumerSmoke`](../../build/_build/Modules/Packaging/PackageConsumerSmokeRunner.cs) always emits `-r <rid>` for restore, build, and run. This triggers runtime-specific restore and SDK-level native file copy into `bin/Release/net9.0/<rid>/`, then `dotnet run -r <rid> --no-build --no-restore` executes with assets already laid out. What it does NOT exercise: the default framework-dependent path (no `<RuntimeIdentifier>` in csproj), where the runtime's native library resolver walks the NuGet cache's `runtimes/<host-rid>/native/` subtree at load time via `runtimetargets` evaluation. The two subsystems are independent — a regression in one can hide behind success in the other. Contract scope is documented in [`playbook/cross-platform-smoke-validation.md` § "Consumer Invocation Contract (Checkpoint K)"](../playbook/cross-platform-smoke-validation.md#consumer-invocation-contract-checkpoint-k). **Orthogonal to S1 (2026-04-17): S1 changed packaging mechanism; PD-10 is about consumer-side resolver path coverage. Revisit independently.** | Decision recorded: either (a) keep `-r <rid>`-only contract with a doc note that the framework-dependent path is a D-ci concern, or (b) add a second invocation in the smoke runner that builds/runs without `-r <rid>` to cover the default resolver path. If (b), update `PackageConsumerSmokeRunner` + playbook. | Not blocking — current smoke still fails on any real regression in the file-copy path. This PD tracks whether the D-local contract is complete enough for 3-platform K promotion, or whether the smoke needs a second invocation pass first. Recommended resolution moment: when promoting checkpoint K from "planned" to "active" on all 3 platforms (currently Phase 2b scope). |
| PD-11 | S1 adoption decision record: within-family exact-pin retired in favor of SkiaSharp-style minimum range — does the decision hold under any new evidence? | Future review (watch-for-reconsideration) | **RECORDED 2026-04-17 (S1 adoption).** Retires within-family exact-pin. Adopts minimum-range contract. Drift protection moves to Cake orchestration invariant + post-pack validator assertion. Full rationale in "S1 Adoption Record" at top of this doc. Supporting research: [`exact-pin-spike-and-nugetizer-eval-2026-04-16.md`](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) (SUPERSEDED), [`nu5016-cake-restore-investigation-2026-04-17.md`](../../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md) (root-cause confirmed). Closes PD-2 (withdrawn) and PD-9 (not applicable). Cascading changes: strategy doc amendments (§1 Dependency Contracts, §4 Drift Protection Model, §Tradeoffs item 5), guardrails doc (9 rows deleted, G23 reframed, G11 marked REVISIT), Stream A0 retired, Amendment 2 superseded, Stream A-risky partially reverted in Phase 3 code changes, Stream D-local simplified to 2-step pack per family. | Reconsidered if: (a) upstream NuGet publishes a fix for the `Properties="BuildProjectReferences=false;"` globals-strip (NuGet/Home#11617 or equivalent) AND a community pattern emerges for auto-derived exact pin, OR (b) we encounter a real-world version-drift incident that orchestration-level protection couldn't catch, OR (c) our release policy changes such that within-family pairs become independently releasable at different versions. None of (a)(b)(c) are expected in Phase 2 timeframe. | Not blocking. Decision stands until further evidence. |
