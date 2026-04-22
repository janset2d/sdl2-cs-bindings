# ADR-003: Release Lifecycle Orchestration + Version Source Providers

**Status:** Draft / Proposal
**Date:** 2026-04-20
**Author:** Deniz Irgin (@denizirgin) + session collaboration
**Extends:** [ADR-001 (D-3seg versioning + Artifact Source Profile)](2026-04-18-versioning-d3seg.md)
**Extends:** [ADR-002 (DDD layering for build host)](2026-04-19-ddd-layering-build-host.md)
**Supersedes:** —

---

## Reading Note (Important)

This ADR is **a proposal and mental-model document**. Every code shape, `interface` signature, `record` definition, Cake target surface, and CI workflow YAML skeleton below is **pseudocode and a hypothetical suggestion**. Nothing here has been pinned against an implementation-level deep dive of the Cake code base.

**Shapes will change during implementation.** Struct names, method signatures, DI registration paths, task dependency graphs — all will be revised against the actual code state during the implementation pass. The job of this ADR is to lock the decisions, the mental model, and the stage ownership graph; not the exact mechanism.

This distinction matters because:

- Any implementation commit that cites this ADR should honestly note drift from it and amend the ADR when warranted.
- Do not expect the pseudocode to be implemented verbatim — if the semantics are preserved, the shape is free to evolve.
- The Cake deep dive (actual input/output shapes for Layer 2 targets, DI composition, test coverage needs) is a separate work item that will concretize this ADR.

## Design Principle — Vision First

This ADR is **not** a preserve-the-current-composition document. It exists to select the correct ownership graph, orchestration boundary, and contract model first; refactoring cost is secondary.

- Existing Cake tasks, services, seams, and helpers are reusable **lego pieces**, not fixed architectural truth.
- Reuse is preferred when it strengthens the chosen model, but current placement is not binding.
- If a component sits behind the wrong boundary, in the wrong task, or in the wrong orchestration layer, it may be **retained, relocated, narrowed, split, or retired**.
- Lower churn is desirable, but only after the target architecture is coherent. A cheaper wrong boundary is still the wrong boundary.

---

## 1. Context — Where we are and why this ADR exists

### 1.1 What prior ADRs settled

**ADR-001 (2026-04-18)** locked:

- D-3seg versioning (`<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`)
- Package-first consumer contract (Source Mode retired)
- Artifact Source Profile abstraction (`Local` / `RemoteInternal` / `ReleasePublic`)

**ADR-002 (2026-04-19)** locked:

- DDD layering for the build host: `Tasks / Application / Domain / Infrastructure`
- Layer discipline enforced by architecture tests

### 1.2 The gap ADR-001 and ADR-002 did not close

We currently carry three independent version-resolution paths that do not compose:

1. **Git tag + MinVer** — tag drives `$(Version)` (single family)
2. **Manifest + suffix** — `SetupLocalDev` auto-derivation (all families)
3. **CLI `--family-version`** — operator override (single family; G54 rejects multi-family)

And five release scenarios, each selecting one of those three paths with its own ad-hoc wiring:

- Local development
- CI behavioural-validation runs (including PA-2 witnesses)
- Targeted release (single family tag push)
- Full-train release (coordinated multi-family)
- Manual escape (CI broken, operator drives by hand)

**Consequence:** lower pipeline layers (`PackageTask`, `PackageConsumerSmokeRunner`, and a future `PublishTask`) carry defensive branches for three input shapes, and the CI/CD layer answers "which mechanism fits this trigger?" differently on each trigger type. PD-7 (full-train orchestration), PD-8 (manual escape), PD-13 (`--family-version` retirement), and A2 (PA-2 witness) are all symptoms of the same orchestration gap.

**Additionally:** the repo talks about "PreFlight" and "PostFlight" as if they were a two-stage validation split, but in practice every lifecycle stage owns its own validation suite (for example native smoke belongs to Harvest, post-pack guardrails belong to Pack, consumer smoke runs after Pack but on the re-entry matrix rather than the Pack runner). The "PostFlight" singular is misleading.

### 1.3 Research trail / prior attempts

- **An earlier proposal in this session:** a 5-profile enum (`Local` / `Witness` / `TargetedRelease` / `FullTrainRelease` / `ManualEscape`). Retired because the **axis is wrong**: "scenario" is an output of the question "where does the version come from", not the driver. The real axis is the version source; scenarios fall out of the provider + scope + trigger combination.
- **SetupLocalDev leaking into CI:** `SetupLocalDev` was designed as a local-dev convenience, but because it was the only working end-to-end flow, it acquired gravity toward CI usage. That pull is wrong — CI should select its own provider and call Cake with explicit version mappings.

---

## 2. Decision — Mental Model

### 2.1 Three axes

```text
RID               <- CI/CD matrix axis; library-agnostic; outermost umbrella
 |
 +- Family        <- deploy unit (managed + native pair, per ADR-001 "family as release unit")
     |
     +- Version   <- explicit input handed to Cake by operator/CI
```

Each layer has a single owner:

- **RID**: the CI/CD matrix decides (from trigger context + `manifest.runtimes[]`).
- **Family**: the orchestrator (CI workflow or operator) supplies the scope.
- **Version**: the orchestrator supplies a per-family mapping.

**Cake is scenario-blind but policy-thick.** Per [`plan.md` §Strategic Decisions April 2026](../plan.md) ("Cake as single orchestration surface"), every policy-bearing concern lives in the build host. This ADR sharpens that invariant:

- **CI/CD owns:** trigger semantics (which tag pattern fires what), job graph shape (matrix entry points, `needs:` dependencies, artifact upload/download), environment provisioning (secrets, vcpkg bootstrap).
- **Build host owns:** version resolution providers, pipeline execution policy, validation rules, guardrail enforcement, stage-request contracts.
- **YAML stays thin.** Every policy-bearing line belongs to Cake. A CI workflow reads as "orchestrate this Cake target with these inputs," not as "apply this version-resolution rule."

"Is this local, witness, targeted, full-train?" is not a question Cake is aware of; provider + scope + trigger combinations produce the scenario shape, and Cake just sees a resolved mapping.

### 2.2 Scope = versions.Keys

Scope and the versions mapping carry the same information (which families the operation touches). They are **not modelled as separate inputs** — the key set of the mapping is the scope. If you don't want to pack a family, don't put it in the mapping.

### 2.3 Validation is uniform — and PreFlight is version-aware by contract

No lifecycle stage skips its validation on any path. Local dev runs full validation too. Exceptions are code-level invariants (cycle detection on `depends_on`, and so on) — not scenario-driven skip logic.

**PreFlight is version-aware by contract.** Because version resolution happens before any stage executes (§2.4), PreFlight always receives a resolved mapping, and both structural and version-aware validators run on every invocation. This aligns with [`phase-2-adaptation-plan.md` Amendment 1](../phases/phase-2-adaptation-plan.md) ("PreFlight always resolves a version and passes it downstream").

If a genuinely version-free pre-check ever proves necessary (for example, manifest-structural sanity without a release anchor), it earns a distinct stage name (`RepoSanity`, `ConfigValidate`, etc.) — it does not re-enter PreFlight as a nullable-version escape hatch.

### 2.4 Version resolution is a pre-stage concern

An "invocation" is either:

- a **CI workflow run** — the `resolve-versions` job emits a mapping; downstream jobs consume it via `needs:` outputs (or equivalent artifact), or
- a **local Cake composite run** — a provider resolves into memory; composite stages consume that in-process instance.

Within an invocation, **the resolved version mapping is immutable**. Stages consume the mapping; stages do not re-resolve, re-derive, or shadow it. Drift between PreFlight, Pack, and ConsumerSmoke inside the same invocation is a contract violation, not a degraded mode.

This invariant means:

- Version source selection (which provider, which suffix, which tag) varies by trigger.
- Version resolution happens **exactly once per invocation**, before any stage executes.
- Every downstream stage receives the same mapping instance (or wire-equal JSON copy across job boundaries).

The resolution step can physically live either as a dedicated CI `resolve-versions` job that invokes the build host, or as the first step inside a local composite target (for example, `SetupLocalDev` invoking the selected artifact-source resolver, which then performs profile-owned resolution/orchestration). Placement and CLI surface name are implementation details; the invariant — resolve once, distribute immutably, consume everywhere — is the contract.

This invariant applies to **CI job-chain runs** and **composite Cake targets**. Operator-driven ad-hoc sequencing of standalone targets is outside its scope: each such target invocation is its own invocation and may resolve independently. In that mode, each invocation accepts its inputs independently; cross-invocation consistency is the operator's responsibility, supplemented (not replaced) by stage-level validators such as G54.

### 2.5 `depends_on` does not auto-expand scope

`manifest.package_families[].depends_on` is **ordering and dependency-consistency metadata**, not automatic scope expansion. Releasing `sdl2-image` alone does not require packing `sdl2-core` in the same invocation. The operator chooses the scope; `depends_on` only drives:

- **Ordering** within a multi-family invocation (core before satellites).
- **Consistency** validation: G58 (cross-family dependency resolvability) ensures that when a satellite is packed, its declared minimum Core version is reachable — either in the current scope or on the target feed.

Scope expansion is never implicit.

### 2.6 Terminology guard — "strategy" scope

This ADR redefines release orchestration and version-source ownership. It does **not** revisit runtime packaging strategy — `hybrid-static`, `pure-dynamic`, and the strategy layer described in [`plan.md` §Strategic Decisions April 2026](../plan.md) retain their current semantics unchanged. "Strategy" in Cake code continues to refer to the hybrid/pure-dynamic packaging model; "release orchestration" (targeted vs full-train vs manual escape) is not a Cake concept — it is a CI/CD concern satisfied by trigger semantics + provider selection.

---

## 3. Decision — Architecture (pseudocode)

> Every C# and YAML snippet in this section is **hypothetical**. Names and shapes are subject to revision during the Cake deep dive.

### 3.1 Layer 1 — Version Source Providers (service-only, DI-scoped)

```csharp
namespace Build.Application.Versioning;  // proposed placement; revisit at impl

public interface IPackageVersionProvider
{
    Task<IReadOnlyDictionary<FamilyId, SemanticVersion>> ResolveAsync(
        IReadOnlySet<FamilyId> requestedScope,
        CancellationToken ct);
}

// 3 implementations (pseudocode):

public sealed class ManifestVersionProvider : IPackageVersionProvider
{
    // Reads manifest.json library_manifests[].vcpkg_version for each family in scope,
    // composes <UpstreamMajor>.<UpstreamMinor>.0-<suffix> per family.
    // Suffix is ctor-injected (e.g., "local.1713582400" or "ci.run-id-12345").
    public ManifestVersionProvider(ManifestConfig manifest, string suffix) { ... }
}

public sealed class GitTagVersionProvider : IPackageVersionProvider
{
    // Mode A (single family): reads the family's git tag, derives version through a
    //   MinVer-shaped pathway, validates UpstreamMajor.Minor against manifest (G54).
    // Mode B (multi-family from meta-tag): reads manifest.package_families[], for
    //   each family discovers the sdl<major>-<role>-<semver> tag at the meta-tag commit,
    //   topologically orders using package_families[].depends_on.
    public GitTagVersionProvider(ManifestConfig manifest, GitRepository repo, GitTagScope scope) { ... }
}

public sealed class ExplicitVersionProvider : IPackageVersionProvider
{
    // Takes operator-supplied per-family SemVer mapping (sourced from
    // `--explicit-version sdl2-image=2.8.1-hotfix.1` or equivalent).
    // Validates each entry against manifest (G54).
    public ExplicitVersionProvider(ManifestConfig manifest, IReadOnlyDictionary<FamilyId, SemanticVersion> overrides) { ... }
}
```

**Important:** Providers are **not Cake CLI targets**. They live as services in the DI container. Public orchestration surfaces may consume them internally, but `SetupLocalDev` does **not** expose raw provider composition as its public boundary.

The tag-based provider shape above is also **illustrative, not binding**. The implementation may keep one `GitTagVersionProvider` with a scope parameter or split targeted/full-train tag resolution behind a shared helper. ADR-003 locks the capability and ownership boundary, not the final class decomposition.

**Ownership invariant — build host resolves, CI consumes.** Version resolution logic lives in the build host; it is not reimplemented in CI workflow scripts. A CI workflow invokes a build-host entrypoint (the exact target name is pinned at implementation time) and consumes the resolved mapping. Workflow-native provider logic — parsing tags in YAML/bash, reading manifest fragments from action steps, re-implementing G54 checks in workflow code — is **not** an accepted path. It would violate [`plan.md` §Strategic Decisions April 2026](../plan.md) ("Cake as single orchestration surface"). The exact CLI surface shape for version resolution is open for the implementation pass; the ownership boundary is not.

### 3.2 Layer 2 — Pipeline Stages (Cake targets)

Each stage owns its own input/output shape. There is no monolithic `PipelineRequest`.

```csharp
// Pseudocode — each stage's request type (final shape TBD at impl):

record PreflightRequest(
    ManifestPath Manifest,
    IReadOnlyDictionary<FamilyId, SemanticVersion> Versions);  // always resolved; see §2.3 + §2.4

record HarvestRequest(
    Rid Rid,
    IReadOnlySet<LibraryId> Libraries,
    VcpkgConfig Vcpkg);

record NativeSmokeRequest(
    Rid Rid,
    ArtifactLocation HarvestOutput);

record ConsolidateHarvestRequest(
    IReadOnlyList<Rid> SuccessfulRids,
    ArtifactLocation RootOutput);

record PackRequest(
    IReadOnlyDictionary<FamilyId, SemanticVersion> Versions,
    ArtifactLocation ConsolidatedHarvest,
    OutputLocation PackagesDir);

record PackageConsumerSmokeRequest(
    Rid Rid,
    IReadOnlyDictionary<FamilyId, SemanticVersion> Versions,
    FeedLocation NupkgSource);

record PublishRequest(
    ArtifactLocation Packages,
    FeedTarget Feed,
    AuthToken Token);
```

Cake target surface (hypothetical CLI):

```text
--target=PreFlight              --versions sdl2-core=2.32.0,...
--target=Harvest                --rid <rid> --library ...
--target=NativeSmoke            --rid <rid>
--target=ConsolidateHarvest
--target=Pack                   --versions sdl2-core=2.32.0,...
--target=PackageConsumerSmoke   --rid <rid> --versions ...
--target=PublishStaging         --feed <url>
--target=PublishPublic          --feed <url>
```

### 3.3 Layer 3 — Convenience Target (Cake, composition)

`SetupLocalDev` is a convenience target for local-dev ergonomics. This ADR explicitly selects **Option A (resolver-centric composition)**: the public orchestration boundary for profile-specific feed preparation remains ADR-001's `IArtifactSourceResolver`, and `SetupLocalDev` stays a thin entry point over that seam.

```csharp
// Pseudocode — Option A (resolver-centric public boundary):
class SetupLocalDevTask(
    IArtifactSourceResolver artifactSourceResolver,
    ICakeLog log)
{
    public async Task RunAsync(BuildContext context, CancellationToken ct)
    {
        log.Information("SetupLocalDev started with source profile '{0}'.", artifactSourceResolver.Profile);

        await artifactSourceResolver.PrepareFeedAsync(context, ct);
        await artifactSourceResolver.WriteConsumerOverrideAsync(context, ct);
    }
}
```

> **Option A note.** `SetupLocalDev` is **not** a second first-class orchestration surface beside `IArtifactSourceResolver`. The resolver seam remains the public profile boundary. For the `Local` profile, `PrepareFeedAsync` may internally compose version providers, pack loops, and any stage-runner calls needed to materialize the feed, but that composition stays private to the resolver implementation. Internal composition goes through Application-layer runners injected via DI, not nested Cake target invocations. Remote/Internal/Public profiles retain the same public seam even if their internal mechanisms differ.
>
> **Amendment (v1.6, 2026-04-21, Slice B2 implementation discovery).** Direct resolver-owned composition violated two invariants in practice: (a) the `LocalArtifactSourceResolver` class name implies consumption of an existing feed, not production from scratch — collapsing the full pipeline into the resolver's ctor pushed it to 11 dependencies and conflicted with its framing; (b) dragging `NativeSmokeTaskRunner` into the Local feed-prep flow imposed CMake + a platform C/C++ toolchain (MSVC Developer shell on Windows) as a prereq for managed-binding iteration, which is orthogonal to feed materialisation. NativeSmoke is per-RID native payload validation under the Harvest stage (see §4 validation-ownership table); it is not a Pack prerequisite. The composition therefore moves to a dedicated `SetupLocalDevTaskRunner` in the Application layer: the runner resolves the mapping, drives `Preflight → EnsureVcpkg → Harvest → ConsolidateHarvest → Pack`, then hands the resolved mapping to `IArtifactSourceResolver.PrepareFeedAsync` + `WriteConsumerOverrideAsync` for verify + override stamping. The resolver's responsibility narrows to "given a feed location and an expected mapping, does the feed resolve?" — genuine resolver behaviour. `IArtifactSourceResolver.PrepareFeedAsync` and `WriteConsumerOverrideAsync` both take the mapping explicitly rather than pulling it from DI state; the resolver becomes stateless across the two calls. NativeSmoke stays reachable as a standalone `--target NativeSmoke --rid <rid>` invocation and runs per-RID as part of the CI harvest matrix. The ADR's §2–§4 decisions (three axes, three providers, stage-owned validation, matrix re-entry for ConsumerSmoke) are unaffected; this amendment only re-homes the Local-profile composition one layer above the resolver. Detail ledger: `docs/phases/phase-2-release-cycle-orchestration-implementation-plan.md` §6.4 post-review amendment block + §14 change log v2.7.

**Critical:** CI/CD does **not** call this target. CI invokes the build host's version-resolution entrypoint in its own `resolve-versions` job, then calls the pipeline stage targets with the resolved mapping. `SetupLocalDev`'s `Janset.Local.props` side effect is meaningless for CI.

**Relationship to Artifact Source Profile (ADR-001).** `SetupLocalDev` is the orchestrating convenience layer for the **`Local` Artifact Source Profile** defined in [ADR-001 §2.7–§2.8](2026-04-18-versioning-d3seg.md). It composes the `Local` profile's feed-preparation flow (repo pack → local folder feed → `Janset.Local.props`) with the Cake pipeline; it does not bypass or replace the `IArtifactSourceResolver` seam. The `RemoteInternal` and `ReleasePublic` profiles retain their own orchestration paths (CI-driven, no local composite target); all three profiles continue to meet at the `PackageReference + local folder feed` consumer contract locked in ADR-001.

### 3.4 Layer 4 — CI/CD Orchestration

CI workflow behaviour is trigger-driven. Pseudocode YAML skeleton (the real workflow takes shape during the implementation pass):

```yaml
# release.yml (hypothetical — name and shape TBD)

on:
  push:
    tags:
      - 'sdl2-*-*.*.*'    # targeted release per-family
      - 'train-*'         # full-train coordinated release
  workflow_dispatch:
    inputs:
      mode:
        description: 'manifest-derived | explicit'
        type: choice
        options: [manifest-derived, explicit]
      versions:
        description: 'For mode=explicit: sdl2-core=2.32.0,sdl2-image=2.8.0,...'
        required: false

jobs:
  resolve-versions:
    # CI step supplies only the trigger context (ref name, event type, dispatch inputs);
    # the actual resolution happens inside the build host (§3.1 ownership invariant).
    # This job invokes a build-host entrypoint whose exact CLI surface is pinned at
    # implementation time. Supported contexts:
    #   tag push (per-family): build host reads sdl<N>-<role>-<semver> tag -> single-family mapping.
    #   meta-tag push: build host reads manifest.package_families[], discovers per-family tags
    #     at the meta-tag commit, returns a topologically ordered multi-family mapping.
    #   workflow_dispatch (manifest-derived): build host reads manifest + applies suffix=ci.<run-id>.
    #   workflow_dispatch (explicit): build host validates operator-supplied versions input.
    runs-on: ubuntu-24.04
    outputs:
      versions-json: ${{ steps.resolve.outputs.versions }}
      families: ${{ steps.resolve.outputs.families }}
    steps:
      - id: resolve
        run: cake <build-host-version-resolution-entrypoint>

  generate-matrix:
    runs-on: ubuntu-24.04
    outputs:
      matrix: ${{ steps.matrix.outputs.matrix }}
    steps:
      - id: matrix
        run: cake --target=GenerateMatrix

  preflight:
    needs: resolve-versions
    runs-on: ubuntu-24.04
    steps:
      - cake --target=PreFlight --versions ${{ fromJson(needs.resolve-versions.outputs.versions-json) }}
      - cake --target=Coverage-Check

  harvest:
    needs: [preflight, generate-matrix]
    strategy:
      fail-fast: false
      matrix: ${{ fromJson(needs.generate-matrix.outputs.matrix) }}  # dynamic from manifest.runtimes
    runs-on: ${{ matrix.runner }}
    steps:
      - cake --target=Harvest --rid ${{ matrix.rid }} --library ...
      - cake --target=NativeSmoke --rid ${{ matrix.rid }}
      - upload harvest artifact

  pack:
    needs: [harvest, resolve-versions]
    runs-on: ubuntu-24.04
    steps:
      - download all harvest artifacts
      - cake --target=ConsolidateHarvest
      - cake --target=Pack --versions ${{ fromJson(needs.resolve-versions.outputs.versions-json) }}
      - upload nupkg artifact

  consumer-smoke:
    needs: [pack, generate-matrix]
    strategy:
      fail-fast: false
      matrix: ${{ fromJson(needs.generate-matrix.outputs.matrix) }}  # SAME matrix, re-entry
    runs-on: ${{ matrix.runner }}
    steps:
      - download nupkg artifact
      - cake --target=PackageConsumerSmoke --rid ${{ matrix.rid }} --versions ...

  publish-staging:
    needs: consumer-smoke
    if: startsWith(github.ref, 'refs/tags/')
    runs-on: ubuntu-24.04
    steps:
      - cake --target=PublishStaging --feed $GH_PACKAGES
```

**Four critical points:**

1. **Matrix dynamic from `manifest.runtimes`** — there is no hard-coded YAML matrix. `GenerateMatrixTask` resolves it.
2. **Consumer smoke re-entry** — Pack runs on a single runner, but Smoke re-enters the matrix. All 7 RIDs validate the consumer-side path (Windows DLL lookup, macOS dyld, arm64 P/Invoke, and so on).
3. **The `resolve-versions` job invokes a build-host entrypoint** — tag parsing, manifest reads, and G54 validation happen inside the build host (§3.1 ownership invariant). The CI step supplies only trigger-context inputs (ref name, event type, dispatch inputs) and consumes the resolved mapping.
4. **Matrix shape is always the full `manifest.runtimes[]`** for this canonical release workflow — there is no trigger-level input to reduce scope. RID subsets are a local / debug-workflow concern, never a canonical release concern. A non-promotable debug workflow may accept a RID subset input; `release.yml` does not.

### 3.5 Lifecycle diagram

```text
                  +------------------------+
                  |  resolve-versions job  |     <- invokes build-host entrypoint;
                  |  (CI supplies trigger  |     resolution + validation live in
                  |   context only)        |     the build host (§3.1)
                  +-----------+------------+
                              |
                              v
                  +------------------------+
                  |     PreFlight          |     <- single runner, fail-fast
                  |  (manifest+vcpkg, G54, |
                  |   csproj, G49)         |
                  +-----------+------------+
                              |
                              v
                +-----------------------------+
                |    Harvest (N RID matrix)   |    <- native runners
                |  - binary closure scan      |
                |  - hybrid leak check        |
                |  - NativeSmoke per-RID      |
                +-----------+-----------------+
                              |
                              v
                  +------------------------+
                  |  ConsolidateHarvest +   |     <- aggregation runner
                  |  Pack (G21-27, G46-48, |
                  |        G55-57, G58)    |
                  +-----------+------------+
                              |
                              v
                +-----------------------------+
                |  ConsumerSmoke (N RID      |     <- RE-ENTRY matrix
                |  matrix, restore+runtime)  |
                +-----------+-----------------+
                              |
                              v
                  +------------------------+
                  |  PublishStaging -> (gate) -> PublishPublic  (single runner each)
                  +------------------------+
```

**Stage semantics (normative).**

- **PreFlight** is the pre-matrix fail-fast gate; version-aware by contract (§2.3 + §2.4). Structural and version-aware validators both run on every invocation.
- **Harvest** is intentionally version-blind native evidence collection; per-RID matrix; strategy-aware guardrails (hybrid leak detection, primary=1 / runtime=0, license collection) run here.
- **NativeSmoke** is the distinct per-RID stage that proves native binaries load and initialize at the OS level. It is extracted from Harvest so that Harvest stays focused on asset collection and NativeSmoke can evolve its own fail semantics independently.
- **Pack** is single-runner package construction; consumes consolidated harvest artifacts and the resolved version mapping; owns post-pack guardrails including G58.
- **ConsumerSmoke** is the **first cross-RID managed/runtime truth stage** — this is why it is a matrix re-entry rather than a single-runner substitute. Per-RID consumer paths (Windows P/Invoke lookup, macOS dyld two-level namespace, arm64 runtime resolver behaviour) regress independently; only a per-RID run catches them.
- **Publish** is single-runner staging/promotion of validated artifacts; it owns transfer-side checks such as feed auth and deduplication, but no package-structure or compatibility validation.

---

## 4. Validation ownership

Every guardrail belongs to exactly one stage. There is no monolithic "PostFlight" suite; each stage fails on its own validator output.

| Validation / Guardrail | Stage | Fails |
| --- | --- | --- |
| manifest ↔ vcpkg.json version consistency | **PreFlight** | whole pipeline |
| csproj pack contract (G4/G6/G7/G17/G18) | **PreFlight** | whole pipeline |
| Strategy coherence (G49) | **PreFlight** | whole pipeline |
| Tag format + tag↔manifest G54 upstream coherence | **PreFlight** (tag format on release invocations; G54 manifest coherence on every invocation) | whole pipeline |
| G58 cross-family dep resolvability (new) | **Pack** (see §8 — open for scope/placement) | pack step |
| Binary closure hybrid-leak detection | **Harvest** (per-RID) | that RID |
| Primary=1 / runtime=0 harvest shape | **Harvest** (per-RID) | that RID |
| Licenses collected | **Harvest** (per-RID) | that RID |
| Native smoke (C++ SDL_Init, codec discovery) | **NativeSmoke** (per-RID) | that RID |
| Post-pack shape (G21/G22/G23/G25/G26/G27) | **Pack** | pack step |
| Native payload shape (G46/G47/G48) | **Pack** | pack step |
| `janset-native-metadata.json` + README (G55/G56/G57) | **Pack** | pack step |
| Consumer restore + runtime TUnit smoke | **PackageConsumerSmoke** (per-RID) | that RID's smoke |
| Feed auth + deduplication | **Publish** | publish step |

The build-host test suite (`dotnet test build/_build.Tests/...`) and `Coverage-Check` are invoked alongside PreFlight in the preflight-gate CI job — as CI policy, not as Cake target dependencies.

---

## 5. Retain / Refactor / Retire / New

This is not a greenfield rewrite mandate, but neither is it a preserve-the-current-shape exercise. Existing pieces are candidates for reuse, relocation, narrowing, splitting, or retirement if that better matches the chosen ownership model. The table below is **hypothetical** — the actual classification is revised against real code state during the implementation pass.

| Status | Component | Note |
| --- | --- | --- |
| **Retain** | Strategy layer (`HybridStaticStrategy`, `PureDynamicStrategy`, `HybridStaticValidator`) | Same role within the Harvest stage |
| **Retain** | `PackageOutputValidator` + G21-G48 + G54-G57 | Invoked the same way inside the Pack stage |
| **Retain** | `CoverageCheckTask` + ratchet policy | Chained into the preflight gate |
| **Retain** | `ManifestConfig` + schema v2.1 | SSoT role strengthened |
| **Retain** | DDD layering (ADR-002) | New additions do not violate ADR-002 |
| **Retain** | D-3seg versioning (ADR-001) | Version shape is unchanged |
| **Retain** | Artifact Source Profile (ADR-001) | `Local` / `RemoteInternal` / `ReleasePublic` — preserved on the feed-prep axis |
| **Refactor** | `IPackageVersionResolver` (existing) | Generalises into `IPackageVersionProvider` with 3 implementations; current resolver collapses into one of them or splits |
| **Refactor** | `PreFlightCheckTask` | Version-aware validation takes explicit input; shape simplifies |
| **Refactor** | `HarvestTask` | NativeSmoke is extracted; Harvest focuses on asset gathering |
| **Refactor** | `PackageTask` | Input is a per-family version mapping (replacing the single `--family-version`); G58 added |
| **Refactor** | `PackageConsumerSmokeTask` | Becomes stateless-callable for matrix re-entry (input: RID + feed + versions) |
| **Refactor** | `SetupLocalDev` | Explicitly settles on Option A: thin task over resolver-centric profile orchestration; resolver internals may still be reworked |
| **Retire** | `--family-version` CLI flag (single-valued, G54-incompatible with multi-family) | Replaced by `--explicit-version key=value,...` (ExplicitVersionProvider input). PD-13 closes. |
| **Retire** | Monolithic "PostFlight" naming | Each stage owns its validation. Fate of the `PostFlight` target itself — remove entirely, or keep as a local-dev convenience wrapper of `PreFlight → Harvest → Pack → Smoke` — is decided at impl. |
| **New** | `IPackageVersionProvider` + 3 impls | Service-only, not a Cake target |
| **New** | `NativeSmokeTask` | Extracted from Harvest |
| **New** | `G58CrossFamilyDepResolvabilityValidator` | Pack stage (scope-contains check, optional feed probe) |
| **New** | `GenerateMatrixTask` | Dynamic CI matrix from manifest (already planned under Stream C; this ADR rephrases it) |
| **New** | CI `release.yml` workflow | Supersedes or subsets the current `prepare-native-assets-*.yml` workflows |

---

## 6. PD closures

| PD | Outcome under this ADR |
| --- | --- |
| **PD-7 (full-train orchestration)** | **Direction selected.** `GitTagVersionProvider` multi-mode + manifest-driven topological ordering formalises the mechanism: meta-tag trigger + `manifest.package_families[].depends_on` supply the ordering; no separate `release-set.json`; the manifest remains SSoT. **Formal closure** happens during canonical doc sweep + implementation. Sub-items that stay open outside this ADR: meta-package versioning (tracked in [`release-lifecycle-direction.md`](../knowledge-base/release-lifecycle-direction.md) §Meta-Package) is not covered here. |
| **PD-8 (manual escape hatch)** | **Direction selected.** An operator runs the same pipeline Cake provides (for example `dotnet cake --target=Pack --explicit-version sdl2-image=2.8.1-hotfix.1`) without CI as the orchestrator. The `--explicit-version` mapping carries scope via its key set (§2.2); no separate `--family` argument is required. Audit trail rides the existing git-tag + CI-log discipline. **Formal closure** lands when `playbook/release-recovery.md` is written, the Cake helper surface is enumerated, and a real recovery scenario is validated end-to-end during the implementation pass. |
| **PD-13 (`--family-version` retirement)** | **Closed (2026-04-22).** Legacy `--family-version` flag retired; replaced by `--explicit-version family=version,...` (ExplicitVersionProvider input) — type-safe, multi-family safe, G54-validated per entry. `ExplicitVersionProvider` wired into `ResolveVersionsTaskRunner` (Slice B1); `PackageConsumerSmokeRunner` enforces non-empty mapping (Slice C.8); `PackageConsumerSmokeTask.ShouldRun` skips silently when no mapping supplied. All legacy `--family-version` call sites removed across Cake tasks, CLI parsing, and smoke-witness.cs. |

Implementation commits formalise PD-7/8/13 closure as their respective deliverables land; cross-document references in `release-lifecycle-direction.md`, `release-guardrails.md`, and `plan.md` are updated during the doc sweep pass.

---

## 7. Relationship to prior ADRs

| ADR | Relationship |
| --- | --- |
| **ADR-001 (D-3seg + Artifact Source Profile)** | **Extends.** ADR-001 locked version shape and consumer contract. ADR-003 adds version **sources** on top (3 providers) and draws the release-lifecycle ownership graph. Artifact Source Profile (`Local/RemoteInternal/ReleasePublic`) is preserved as the **feed-prep** axis; version source is an orthogonal axis. |
| **ADR-002 (DDD layering)** | **Consistent.** New components (version providers → Application, pipeline request records → Domain, Cake targets → Tasks) respect ADR-002 invariants. `LayerDependencyTests` gates the additions. |

Neither ADR-001 nor ADR-002 is superseded. ADR-003 fills the orchestration gap they left open.

---

## 8. Open questions / risks (to resolve at impl)

These questions do not block this ADR but must be resolved during the implementation pass:

1. **G58 exact semantic + placement.**
   - Full phrasing: "if scope contains a satellite family, the satellite's cross-family dependency on Core must either have the Core version present in the same invocation scope OR have the declared minimum Core version already available on the target feed."
   - Placement: PreFlight or Pack? Pack is later, but a feed probe there avoids introducing auth dependency earlier (auth is already a Publish-stage concern). Alternative: PreFlight does a scope-contains check always; feed probe runs in Pack only when a feed URL is configured.
   - **Proposed:** In Pack. Feed probe optional (runs only if feed URL is passed). Scope-contains check always runs.

2. **Fate of the `PostFlight` umbrella target.**
   - Today `PostFlight` = PreFlight → Package → ConsumerSmoke (single RID). Convenient in local dev.
   - In the new lifecycle the `SetupLocalDev` composition covers that role. `PostFlight` either retires entirely or survives as a smoke-but-no-local.props variant of `SetupLocalDev`.
   - **Proposed:** Decide at impl based on whether anyone still uses `PostFlight` standalone.

3. **`--explicit-version` CLI parse shape.**
   - Multi-value flag: `--explicit-version sdl2-core=2.32.0 --explicit-version sdl2-image=2.8.0`
   - Or comma-separated: `--explicit-version sdl2-core=2.32.0,sdl2-image=2.8.0`
   - Which is more idiomatic under System.CommandLine 2.0 GA — pick during the deep dive.

4. **`ManifestVersionProvider` suffix strategy.**
   - Local: `local.<unix-timestamp>` (non-deterministic but unique per invocation)
   - CI manifest-derived: `ci.<github-run-id>.<github-run-attempt>` (deterministic, reproducible)
   - One provider with a ctor-injected suffix string, or two providers? **Proposed:** one provider; caller supplies suffix.

5. **`GitTagVersionProvider` single vs multi-family.**
   - Single family (targeted release) vs multi-family (full-train).
   - Modelled as one provider with a `GitTagScope.Single(FamilyId) | GitTagScope.Multi(IReadOnlySet<FamilyId>)` parameter, or as two providers over a shared helper.
   - **Implementation-time choice:** ADR-003 does not lock this decomposition now. Choose the variant that minimizes internal branching and yields the clearer test and code boundary shape during the Cake deep dive.

6. **Location / naming of the matrix-generation task.**
   - Planned under Stream C as `GenerateMatrixTask`. ADR-003 does not redefine it, only acknowledges its existence. Shape firms up at impl.

7. **CLI surface shape for the build-host version-resolution entrypoint.**
   - The ownership invariant is locked (§3.1): the build host resolves, CI consumes; workflow-native provider logic is not an accepted path.
   - The open detail is only the entrypoint shape: a dedicated Cake target named `ResolveVersions`, a sub-command of an existing target (e.g., `PreFlight --emit-versions`), or a separate light-weight CLI tool in `build/_build` reachable without a full Cake host spin-up.
   - **Proposed:** pick during the Cake deep dive based on reuse patterns across local composite targets and CI; favour the option that minimises cold-start overhead for the CI `resolve-versions` job.

---

## 9. Out of scope (orthogonal concerns)

This ADR does not touch the following open PDs; they continue on their own tracks:

- **PD-3** (dotnet-affected as NuGet library vs CLI wrapper) — change-detection axis, orthogonal.
- **PD-5** (RemoteInternal profile concrete implementation) — feed acquisition axis, already opened by ADR-001.
- **PD-10** (`PackageConsumerSmoke` `-r <rid>` contract and default framework-dependent resolver path coverage) — a consumer-side MSBuild resolver concern, orthogonal to version orchestration.
- **PD-14** (Linux end-user MIDI packaging strategy) — licensing + distribution, orthogonal.
- **PD-15** (sdl2-gfx Unix visibility regression guard) — regression-invariant concern, orthogonal.

These PDs resolve in their own time via separate commits / research notes.

---

## 10. Consequences

### Positive

- **Single contract** — 5 scenarios reduced to 3 providers + one pipeline shape.
- **Manifest authority is reinforced** — every provider validates against manifest via G54.
- **CI workflows stay DRY** — one `release.yml` behaves profile-aware via triggers; no `pa2-witness.yml` / `targeted-release.yml` / `full-train.yml` file proliferation.
- **PD-7, PD-8, PD-13 close** in a single design pass.
- **Validation ownership is explicit** — the "what does PostFlight cover" ambiguity disappears; each stage defends itself.
- **Consumer smoke matrix re-entry** — per-RID consumer paths (DLL lookup, dyld, arm64 P/Invoke) finally get real coverage.
- **Testability** — providers get their own unit tests; stage targets get their own; integration tests run top-down.

### Negative

- **New code volume** — 3 providers + per-stage request records + `NativeSmokeTask` + `G58` validator + `release.yml`. Estimated ~800-1200 lines of new code (~10% of current code base). Real figure lands at impl.
- **CI workflow rewrite** — the current `prepare-native-assets-*.yml` either deprecate or are reused in a harvest-only shape. During the transition two workflow sets may live in parallel; the right path is decided at impl.
- **Learning curve** — new layers (provider, per-stage request records) feel like extra complexity on top of ADR-002 at first glance; canonical doc sweep must explain the separation clearly.
- **Matrix cost growth** — consumer smoke matrix re-entry roughly doubles post-Pack CI wall-clock. But Harvest stays the dominant cost (vcpkg builds vs nupkg restore + smoke test) — the increase is modest in absolute terms.

### Risks

- **CI workflow bypass of the provider abstraction is trivially easy.** If a CI engineer or operator wires up the `resolve-versions` job incorrectly and emits a wrong versions mapping, Cake won't catch it — it only validates at G54 (manifest coherence) and other post-input guardrails. Mitigation: explicit validation of the `resolve-versions` output inside PreFlight (G54 + manifest coherence + format check).
- **`GitTagVersionProvider` topological ordering errors** — if `manifest.package_families[].depends_on` ever contains a cycle (it shouldn't, but the invariant is worth defending), the resolver could loop. Mitigation: a cycle-detection guardrail (candidate G59).
- **Consumer smoke matrix re-entry artifact size** — the Pack stage produces a 7-RID nupkg set (~100-200 MB total); 7 runners download that during smoke = 700 MB–1.4 GB of transfer. Cheap inside GitHub Actions but noticeable locally if a contributor reproduces the matrix. Mitigation: measure during impl; split into per-RID artifact upload/download if needed.

---

## 11. Alternatives considered

### Alt-1: 5-profile enum (earlier proposal, discarded)

A `ReleaseProfile` enum (`Local` / `Witness` / `TargetedRelease` / `FullTrainRelease` / `ManualEscape`) driving a single `IPackageVersionResolver`.

**Why rejected:** "scenario" was the wrong axis. The real axis is the version source; scenarios are outputs of `(provider, scope, trigger)` combinations. The enum forces a profile × strategy cross-product where each profile needs its own branch, which is not DRY. The adopted model (3 providers + 1 pipeline + CI orchestrator) has fewer moving parts and explains more.

### Alt-2: SetupLocalDev Option B (no convenience target, everything explicit)

The user runs 7 separate Cake commands and writes `local.props` manually.

**Why rejected:** ergonomically painful. Nobody would use it; it would inevitably be wrapped in a local script and end up reinventing `SetupLocalDev` without the domain-layer benefits.

### Alt-3: SetupLocalDev Option C (providers exposed as Cake targets, SetupLocalDev chains them)

Three separate `Resolve*Versions` Cake targets, with `SetupLocalDev` chaining them.

**Why rejected:** widens the Cake CLI surface without offsetting benefit. Per §3.1 the build host owns a single version-resolution entrypoint; three public `Resolve*` targets would fragment that surface for no orchestration gain. The three targets would exist mostly to serve one user — `SetupLocalDev` — and would still leave its local-dev ergonomics problem unsolved.

### Alt-4: SetupLocalDev Option D (providers as DI services, CI can invoke if desired)

The Option A shape with the added caveat that CI may call providers via a dedicated Cake service target.

**Why folded into A:** effectively a superset of A, but the extra opt-in surface is redundant — per §3.1 CI invokes the single build-host version-resolution entrypoint anyway; there is no separate "CI chooses provider" decision to make. No net value over A.

### Alt-5: Scenario-aware Cake (rejected)

Cake targets take a `--scenario=release|local|witness` parameter and behave accordingly.

**Why rejected:** scenarios are not Cake's business (again). Switch cases inside every target pollute the code, inflate the test matrix, and break the symmetry between CI and local invocations.

### Alt-6: No matrix re-entry for consumer smoke

Consumer smoke runs only on the Pack runner (Linux, 1 RID) to save CI cost.

**Why rejected:** per-RID consumer paths (Windows P/Invoke search order, macOS dyld two-level namespace, arm64-specific runtime resolver behaviour) escape a Linux-only smoke. SkiaSharp / LibGit2Sharp / Magick.NET all do matrix re-entry. The cost is modest; the coverage is substantial.

---

## 12. Implementation outline

After this ADR locks, the following sequence — each step is a separate commit group, independently revertable:

```text
1. Canonical doc sweep (ADR-003 baseline) — ACTIVE 2026-04-21
 - phases/README.md: refresh + planned phase retention test invariant — DONE
 - phase-2-cicd-packaging.md: retire-to-stub — DONE
 - phase-3-sdl2-complete.md: retire-to-stub — DONE
 - phase-2-adaptation-plan.md: rewrite (historical body archived at
   docs/_archive/phase-2-adaptation-plan-2026-04-15.md); PD-7/8/13 "Direction
   selected" + formal closure criteria; new PD-16 (shared native dep duplicate
   policy, dormant under hybrid-static, absorbed from retired phase-2-cicd §2.8) — DONE
 - phase-5-sdl3-support.md: minor drift fix (runtimes.json → manifest.json
   schema v2.1) — DONE
 - release-lifecycle-direction.md: narrow to policy-only; orchestration
   material devolved to this ADR; --family-version / PD-13 / provider
   ownership collapsed to pointer — DONE
 - cake-build-architecture.md: pipeline stages + target surface + version
   provider architecture section; scanner repurposing expanded into own
   subsection — DONE
 - release-guardrails.md: stage-owned guardrail table (§2.0 added);
   G58 added in Pack stage (ADR-003 §8 Q1 placement) — DONE
 - ci-cd-packaging-and-release-plan.md: ADR-003 direction note; release.yml
   supersession flagged for §4.A — DONE
 - cross-platform-smoke-validation.md: consumer smoke matrix re-entry note;
   per-stage checkpoint mapping; PA-2 witness note for new pipeline — DONE
 - plan.md: strategic decisions + roadmap + known issues refresh; ADR-003
   row added to Strategic Decisions table — DONE
 - docs/README.md: retired phase docs + archive pointer — DONE
 - docs/_archive/ directory + archive convention README — DONE

2. Cake refactor (2-3 sessions, iterative)
 - IPackageVersionProvider interface + ManifestVersionProvider impl
   (existing IPackageVersionResolver logic consolidates into it)
 - GitTagVersionProvider + ExplicitVersionProvider impls
 - Per-stage request records (PreflightRequest, HarvestRequest, ...)
 - Extract NativeSmokeTask from HarvestTask
 - PackageTask input refactor: per-family version mapping
 - G58 validator in the Pack stage
 - --family-version CLI retirement + --explicit-version introduction
 - SetupLocalDev composition shape (Option A)
 - Test suite adjust + new provider tests + G58 tests

3. CI/CD workflow rewrite (1-2 sessions)
 - release.yml (dispatch + tag triggers, dynamic matrix)
 - prepare-native-assets-*.yml: deprecate or reuse in harvest-only shape
 - GenerateMatrixTask wiring

4. PA-2 behavioural validation via the new pipeline
 - workflow_dispatch on the 4 new RIDs (win-arm64, win-x86, linux-arm64, osx-arm64)
 - mode=manifest-derived, suffix=pa2.<run-id>
 - fail triage per the playbook PA-2 section (which is updated to the current
   command shape during the doc sweep pass)
```

Each step references this ADR. Places where implementation drifts from the ADR are reflected back here as an amendment commit.

---

## 13. References

### Prior ADRs

- [ADR-001: D-3seg Versioning + Artifact Source Profile](2026-04-18-versioning-d3seg.md)
- [ADR-002: DDD Layering for Build Host](2026-04-19-ddd-layering-build-host.md)

### Canonical repo docs

- [`AGENTS.md`](../../AGENTS.md) — operating rules
- [`docs/onboarding.md`](../onboarding.md)
- [`docs/plan.md`](../plan.md)
- [`docs/knowledge-base/release-lifecycle-direction.md`](../knowledge-base/release-lifecycle-direction.md)
- [`docs/knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md)
- [`docs/knowledge-base/cake-build-architecture.md`](../knowledge-base/cake-build-architecture.md)
- [`docs/phases/phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md)
- [`docs/playbook/cross-platform-smoke-validation.md`](../playbook/cross-platform-smoke-validation.md)

### Research and historical context

- [`docs/research/release-lifecycle-patterns-2026-04-14-claude-opus.md`](../research/release-lifecycle-patterns-2026-04-14-claude-opus.md)
- [`docs/research/execution-model-strategy-2026-04-13.md`](../research/execution-model-strategy-2026-04-13.md) — three-mode framing amended by ADR-001; further clarified here
- [`docs/research/full-train-release-orchestration-2026-04-16.md`](../research/full-train-release-orchestration-2026-04-16.md) — PD-7 candidate paths; this ADR picks manifest-driven meta-tag
- [`docs/research/release-recovery-and-manual-escape-hatch-2026-04-16.md`](../research/release-recovery-and-manual-escape-hatch-2026-04-16.md) — PD-8 research scope; this ADR picks "same pipeline, operator as driver"

---

## 14. Revision history

| Date | Revision | Change |
| --- | --- | --- |
| 2026-04-20 | v1 (draft) | Initial proposal. Pseudocode level; shapes subject to deep-dive revision. Direction selected for PD-7/PD-8/PD-13. Extends ADR-001 and ADR-002. |
| 2026-04-20 | v1.1 (draft, boundary-tightening pass) | Revision following peer-review notes and session discussion: (a) §2.1 "policy-thick" framing added with thin-CI / thick-build-host ownership table; (b) §2.3 PreFlight declared version-aware by contract; (c) §2.4 new — invocation scope + immutable resolved-mapping invariant; (d) §2.5 new — `depends_on` is ordering and consistency metadata, never automatic scope expansion; (e) §2.6 new — terminology guard separating release orchestration from runtime packaging strategy; (f) §3.1 ownership invariant locks version resolution to build host; workflow-native provider alternative rejected; (g) §3.2 `PreflightRequest.Versions` made non-nullable; (h) §3.3 SetupLocalDev explicitly composes the `Local` Artifact Source Profile from ADR-001 §2.7–§2.8 (does not bypass `IArtifactSourceResolver`); (i) §3.4 `release.yml` removes the `rids` trigger-level input — matrix is always full `manifest.runtimes[]` for the canonical release workflow; RID subsets reserved for local / debug flows; (j) §3.5 stage semantics normative paragraph added; (k) §6 PD closures softened from "Closed" to "Direction selected" with explicit formal-closure criteria. |
| 2026-04-20 | v1.2 (draft, alignment pass) | Second-round peer review of v1.1 caught residual CI-native-resolver wording drift that contradicted the §3.1 ownership invariant. Realignment: (a) §3.3 SetupLocalDev pseudocode flagged as a deliberately flattened sketch — actual wiring goes through the ADR-001 `IArtifactSourceResolver` seam, with a prose note beneath the code block; (b) §3.4 `resolve-versions` job comments rewritten to "CI supplies trigger context; the build host resolves and validates"; (c) §3.5 lifecycle diagram label corrected from "workflow-native provider" to "invokes build-host entrypoint"; (d) §8 open question 7 reframed from a workflow-native-vs-Cake-target dichotomy to an entrypoint CLI-surface-shape question only (ownership is already locked); (e) §6 PD-8 example dropped the redundant `--family` argument — scope is carried by the `--explicit-version` key set per §2.2. |
| 2026-04-20 | v1.3 (draft, consistency pass) | Follow-up cleanup after the final review: (a) §3.2 CLI sketch now makes `PreFlight` version input mandatory, matching the non-nullable request contract; (b) §3.4 adds the missing `generate-matrix` job and completes the job-output wiring in the workflow skeleton; (c) §3.5 lifecycle diagram removes stale `G58?` ambiguity from PreFlight and aligns Publish wording with transfer-side checks only; (d) §12 implementation outline no longer implies PD-7/8/13 are formally closed during the doc-sweep step. |
| 2026-04-20 | v1.4 (draft, vision-first + ownership pass) | Revision following direction-first review: (a) new Vision-First principle states that current build-host composition is reusable input, not binding architecture; (b) §2.4 now scopes the immutable-within-invocation invariant to CI job-chain runs and composite Cake targets, explicitly excluding ad-hoc standalone target sequencing; (c) §3.1 clarifies that raw providers are internal building blocks, not SetupLocalDev's public boundary, and softens GitTag provider decomposition from prescription to implementation-time choice; (d) §3.3 explicitly selects Option A — `IArtifactSourceResolver` remains the public profile-orchestration seam and `SetupLocalDev` stays a thin task over it; (e) §5 now frames retain/refactor decisions as ownership-driven rather than status-quo preserving. |
| 2026-04-21 | v1.5 (draft, post-sweep cross-doc cleanup) | Canonical doc sweep executed against ADR-003 baseline. §8 Open Question 5 markdown indent fixed; §3.3 Option A note extended with "Internal composition goes through Application-layer runners injected via DI, not nested Cake target invocations" clarification; §2.4 scope clause last sentence softened ("cross-invocation consistency is the operator's responsibility, supplemented (not replaced) by stage-level validators such as G54"); §12 implementation outline updated with sweep-step completion markers and retire-to-stub / archive additions that were not in the original v1.1–v1.4 drafts. No decision-level changes; all edits are polish + status tracking. Sweep scope executed: 14-step pass across `phases/README.md`, `phase-2-cicd-packaging.md` (retire-to-stub), `phase-3-sdl2-complete.md` (retire-to-stub), `phase-2-adaptation-plan.md` (rewrite + archive), `phase-5-sdl3-support.md` (minor drift), `release-lifecycle-direction.md` (narrow), `cake-build-architecture.md`, `release-guardrails.md`, `ci-cd-packaging-and-release-plan.md`, `cross-platform-smoke-validation.md`, `plan.md`, this ADR (cross-doc cleanup), `docs/README.md`. New `docs/_archive/` directory introduced with convention README. |
| 2026-04-21 | v1.6 (draft, Slice B2 implementation amendment) | §3.3 Option A note gains an Amendment block documenting the resolver-narrowing split uncovered during Slice B2 implementation. Direct resolver-owned pipeline composition (the literal reading of the original Option A wording) was rolled back after peer review flagged the 11-ctor-dependency load on `LocalArtifactSourceResolver` and the CMake + MSVC Developer shell prereq that `NativeSmokeTaskRunner` imposed on managed-only contributors. Composition moves one layer up into a dedicated `Application/Packaging/SetupLocalDevTaskRunner`; the resolver retains its public-seam role but narrows to verify-feed + stamp-props only, with `PrepareFeedAsync` + `WriteConsumerOverrideAsync` both accepting an explicit mapping. NativeSmoke exits the Local feed-prep flow; remains reachable via standalone `--target NativeSmoke` and the CI harvest matrix. §2 and §4 decisions unchanged. Implementation-side ledger: `phase-2-release-cycle-orchestration-implementation-plan.md` §6.4 post-review amendment block + §14 change log v2.7. |
