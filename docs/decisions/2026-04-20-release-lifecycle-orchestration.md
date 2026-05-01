# ADR-003: Release Lifecycle Orchestration + Version Source Providers

**Status:** Accepted — implemented for the Phase 2b pipeline; public NuGet promotion remains pending
**Date:** 2026-04-20
**Last reviewed:** 2026-04-30
**Author:** Deniz Irgin (@denizirgin) + session collaboration
**Extends:** [ADR-001 (D-3seg versioning + Artifact Source Profile)](2026-04-18-versioning-d3seg.md)
**Extends:** [ADR-002 (DDD layering for build host)](2026-04-19-ddd-layering-build-host.md)
**Supersedes:** —

---

## Reading Note

This ADR started as the design record for the ADR-003 refactor and CI/CD rewrite. Its core decisions are now implemented in the Cake build host and the live `release.yml` workflow.

The decision sections below are authoritative for the ownership model: version sources resolve once, stages consume an immutable mapping, validation is stage-owned, and consumer smoke re-enters the RID matrix. Exact class names and file paths are implementation detail; use `build/_build/` and `.github/workflows/release.yml` as the live source for code shape.

## Current Implementation State

As of 2026-04-30:

- `ResolveVersions` is the build-host entrypoint for version resolution. It emits `artifacts/resolve-versions/versions.json` for CI and supports manifest-derived, explicit, family-tag, and train-tag sources.
- `release.yml` is the canonical release pipeline: build/test Cake host, resolve versions, PreFlight, dynamic RID matrix, Harvest + NativeSmoke, ConsolidateHarvest, Package, ConsumerSmoke matrix re-entry, PublishStaging, and disabled PublishPublic.
- `ManifestVersionProvider`, `ExplicitVersionProvider`, and `GitTagVersionProvider` are implemented under `Application/Versioning/`.
- Stage request records exist for PreFlight, Harvest, NativeSmoke, ConsolidateHarvest, Package, PackageConsumerSmoke, and Publish.
- `PostFlight` and `--family-version` are retired. Stage tasks use `--versions-file` in CI or repeated `--explicit-version family=semver` for operator-driven ad-hoc runs.
- `G58CrossFamilyDepResolvabilityValidator` is implemented as a Pack-stage gate and mirrored in PreFlight as defense in depth.
- `PublishStaging` is live for GitHub Packages. `PublishPublic` remains intentionally disabled pending PD-7 / nuget.org Trusted Publishing.

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

At adoption time, the repo carried three independent version-resolution paths that did not compose:

1. **Git tag + MinVer** — tag drives `$(Version)` (single family)
2. **Manifest + suffix** — `SetupLocalDev` auto-derivation (all families)
3. **CLI `--family-version`** — operator override (single family; G54 rejects multi-family; now retired)

And five release scenarios, each selecting one of those three paths with its own ad-hoc wiring:

- Local development
- CI behavioural-validation runs (including PA-2 witnesses)
- Targeted release (single family tag push)
- Full-train release (coordinated multi-family)
- Manual escape (CI broken, operator drives by hand)

**Consequence:** lower pipeline layers (`PackageTask`, `PackageConsumerSmokeRunner`, and `PublishTask`) had to defend against multiple input shapes, while CI/CD answered "which mechanism fits this trigger?" differently on each trigger type. PD-7 (full-train orchestration), PD-8 (manual escape), PD-13 (`--family-version` retirement), and A2 (PA-2 witness) were all symptoms of the same orchestration gap.

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

## 3. Decision — Architecture

### 3.1 Layer 1 — Version Source Providers (service-only, DI-scoped)

`IPackageVersionProvider` is the service boundary. The three implementations are:

- `ManifestVersionProvider`: derives `<UpstreamMajor>.<UpstreamMinor>.0-<suffix>` from `manifest.json` for local and CI manifest-derived runs.
- `ExplicitVersionProvider`: validates operator-supplied `family=semver` mappings for manual escape and explicit dispatch runs.
- `GitTagVersionProvider`: resolves targeted family tags and train tags, validates G54, and orders multi-family train mappings by `package_families[].depends_on`.

Providers are not public Cake targets. The public CI entrypoint is `ResolveVersions`; it selects the provider, writes `versions.json`, and downstream stages consume that immutable mapping.

**Ownership invariant — build host resolves, CI consumes.** Version resolution logic lives in the build host. Workflow-native provider logic — parsing tags in YAML/bash, reading manifest fragments from action steps, or re-implementing G54 checks in workflow code — is not accepted.

### 3.2 Layer 2 — Pipeline Stages (Cake targets)

Each stage owns its own input/output shape. There is no monolithic `PipelineRequest`.

Implemented stage request records:

- `PreflightRequest` — manifest + resolved versions.
- `HarvestRequest` — RID + library set + vcpkg config.
- `NativeSmokeRequest` — RID + harvest output.
- `ConsolidateHarvestRequest` — successful RID list + output root.
- `PackRequest` — resolved versions + consolidated harvest + package output.
- `PackageConsumerSmokeRequest` — RID + resolved versions + feed path.
- `PublishRequest` — package directory + feed target + auth material.

Current Cake target surface:

```text
--target=ResolveVersions         --version-source manifest|explicit|git-tag|meta-tag
--target=PreFlightCheck          --versions-file artifacts/resolve-versions/versions.json
--target=Harvest                --rid <rid> --library ...
--target=NativeSmoke            --rid <rid>
--target=ConsolidateHarvest
--target=Package                --versions-file artifacts/resolve-versions/versions.json
--target=PackageConsumerSmoke   --rid <rid> --versions-file artifacts/resolve-versions/versions.json
--target=PublishStaging         --versions-file artifacts/resolve-versions/versions.json
--target=PublishPublic          (disabled until PD-7)
```

### 3.3 Layer 3 — Convenience Target (Cake, composition)

`SetupLocalDev` is a convenience target for local-dev ergonomics. It is implemented as a thin Cake task over `SetupLocalDevTaskRunner`.

Current shape:

- `SetupLocalDevTaskRunner` owns the local composite flow: resolve manifest-derived local versions, run PreFlight, ensure vcpkg, harvest, consolidate, package, then stamp the local consumer override.
- `IArtifactSourceResolver` remains the profile boundary for feed preparation and override writing.
- `LocalArtifactSourceResolver` prepares the repo-produced feed.
- `RemoteArtifactSourceResolver` downloads the latest matching managed/native pairs from GitHub Packages into the same local feed layout.
- `ReleasePublic` is intentionally unsupported until public NuGet promotion lands.

NativeSmoke is not part of the local feed-prep loop. It remains a standalone target and runs in the CI harvest matrix.

**Critical:** CI/CD does **not** call this target. CI invokes the build host's version-resolution entrypoint in its own `resolve-versions` job, then calls the pipeline stage targets with the resolved mapping. `SetupLocalDev`'s `Janset.Local.props` side effect is meaningless for CI.

**Relationship to Artifact Source Profile (ADR-001).** `SetupLocalDev` is the orchestrating convenience layer for the **`Local` Artifact Source Profile** defined in [ADR-001 §2.7–§2.8](2026-04-18-versioning-d3seg.md). It composes the `Local` profile's feed-preparation flow (repo pack → local folder feed → `Janset.Local.props`) with the Cake pipeline; it does not bypass or replace the `IArtifactSourceResolver` seam. The `RemoteInternal` and `ReleasePublic` profiles retain their own orchestration paths (CI-driven, no local composite target); all three profiles continue to meet at the `PackageReference + local folder feed` consumer contract locked in ADR-001.

### 3.4 Layer 4 — CI/CD Orchestration

CI workflow behaviour is trigger-driven and lives in `.github/workflows/release.yml`.

Current job topology:

```text
build-cake-host
  -> resolve-versions
  -> preflight
  -> generate-matrix
  -> harvest (7-RID matrix, runs NativeSmoke inline after Harvest)
  -> consolidate-harvest
  -> pack
  -> consumer-smoke (same 7-RID matrix re-entry)
  -> publish-staging
  -> publish-public (disabled until PD-7)
```

Supported trigger routing:

- `workflow_dispatch mode=manifest-derived` uses `ManifestVersionProvider` with `ci.<run-id>.<attempt>` suffix.
- `workflow_dispatch mode=explicit` uses `ExplicitVersionProvider` after parsing repeated `--explicit-version family=semver` entries.
- `sdl2-*` / future `sdl3-*` tag pushes use targeted `GitTagVersionProvider` mode.
- `train-*` tag pushes use meta-tag `GitTagVersionProvider` mode.

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
| G58 cross-family dep resolvability | **Pack** plus PreFlight mirror | pack step / whole pipeline mirror |
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

## 5. Implementation Outcome

The ADR-003 refactor kept the policy model from this ADR but adjusted class boundaries against the real Cake host. Current outcome:

| Status | Component | Note |
| --- | --- | --- |
| **Retain** | Strategy layer (`HybridStaticStrategy`, `PureDynamicStrategy`, `HybridStaticValidator`) | Same role within the Harvest stage |
| **Retain** | `PackageOutputValidator` + G21-G48 + G54-G58 | Invoked inside the Pack stage |
| **Retain** | `CoverageCheckTask` + ratchet policy | Chained into the preflight gate |
| **Retain** | `ManifestConfig` + schema v2.1 | SSoT role strengthened |
| **Retain** | DDD layering (ADR-002) | New additions do not violate ADR-002 |
| **Retain** | D-3seg versioning (ADR-001) | Version shape is unchanged |
| **Retain** | Artifact Source Profile (ADR-001) | `Local` / `RemoteInternal` / `ReleasePublic` — preserved on the feed-prep axis |
| **Refactor** | `IPackageVersionProvider` | Three implementations: Manifest, GitTag, Explicit |
| **Refactor** | `PreFlightCheckTask` | Version-aware and consumes resolved mappings |
| **Refactor** | `HarvestTask` | Delegates orchestration to `HarvestTaskRunner`; NativeSmoke is its own stage/target |
| **Refactor** | `PackageTask` | Input is a per-family version mapping; G58 added |
| **Refactor** | `PackageConsumerSmokeTask` | Matrix re-entry stage: RID + feed + versions |
| **Refactor** | `SetupLocalDev` | `SetupLocalDevTaskRunner` owns local composition; resolvers prepare feeds and write overrides |
| **Retire** | `--family-version` CLI flag (single-valued, G54-incompatible with multi-family) | Replaced by `--explicit-version key=value,...` (ExplicitVersionProvider input). PD-13 closes. |
| **Retire** | Monolithic "PostFlight" naming | Each stage owns its validation; `PostFlight` target is retired. |
| **New** | `IPackageVersionProvider` + 3 impls | Service-only, not a Cake target |
| **New** | `NativeSmokeTask` | Extracted from Harvest |
| **New** | `G58CrossFamilyDepResolvabilityValidator` | Pack stage plus PreFlight mirror |
| **New** | `GenerateMatrixTask` | Dynamic CI matrix from manifest |
| **New** | CI `release.yml` workflow | Supersedes the retired `prepare-native-assets-*.yml` and `release-candidate-pipeline.yml` workflows |

---

## 6. PD Outcomes

| PD | Current outcome |
| --- | --- |
| **PD-7 (full-train orchestration)** | Mostly implemented through `GitTagVersionProvider`, train-tag routing, manifest dependency ordering, and `release.yml`. Public NuGet promotion remains open. |
| **PD-8 (manual escape hatch)** | Direction selected but playbook validation remains open. Operators use the same stage targets with explicit mappings or a generated `versions.json`; no separate release path exists. |
| **PD-13 (`--family-version` retirement)** | Closed. CI resolves versions once via `ResolveVersions` and downstream stages consume `--versions-file`; ad-hoc recovery uses repeated `--explicit-version family=semver`. |

---

## 7. Relationship to prior ADRs

| ADR | Relationship |
| --- | --- |
| **ADR-001 (D-3seg + Artifact Source Profile)** | **Extends.** ADR-001 locked version shape and consumer contract. ADR-003 adds version **sources** on top (3 providers) and draws the release-lifecycle ownership graph. Artifact Source Profile (`Local/RemoteInternal/ReleasePublic`) is preserved as the **feed-prep** axis; version source is an orthogonal axis. |
| **ADR-002 (DDD layering)** | **Consistent.** New components (version providers → Application, pipeline request records → Domain, Cake targets → Tasks) respect ADR-002 invariants. `LayerDependencyTests` gates the additions. |

Neither ADR-001 nor ADR-002 is superseded. ADR-003 fills the orchestration gap they left open.

---

## 8. Resolved Implementation Choices

- G58 runs in Pack and is mirrored in PreFlight as defense in depth.
- `PostFlight` is retired.
- `--explicit-version` is repeatable (`--explicit-version family=semver`) and mutually exclusive with `--versions-file`.
- `ManifestVersionProvider` takes a caller-supplied suffix.
- `GitTagVersionProvider` supports targeted and train scopes through `GitTagScope`.
- The matrix task is `GenerateMatrix`.
- The version-resolution entrypoint is `ResolveVersions`.

---

## 9. Out of scope (orthogonal concerns)

This ADR does not touch the following open PDs; they continue on their own tracks:

- **PD-3** (dotnet-affected as NuGet library vs CLI wrapper) — change-detection axis, orthogonal.
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

- **More moving parts in the build host** — version providers, stage request records, and publishing runners add concepts contributors must learn.
- **Longer CI path** — consumer smoke matrix re-entry adds post-Pack work, but buys real per-RID consumer validation.
- **Publishing still has a tail** — internal staging is live; public NuGet promotion remains a separate PD-7 delivery item.

### Risks

- **CI workflow bypass of the provider abstraction is trivially easy.** If a CI engineer or operator wires up the `resolve-versions` job incorrectly and emits a wrong versions mapping, Cake won't catch it — it only validates at G54 (manifest coherence) and other post-input guardrails. Mitigation: explicit validation of the `resolve-versions` output inside PreFlight (G54 + manifest coherence + format check).
- **`GitTagVersionProvider` topological ordering errors** — if `manifest.package_families[].depends_on` ever contains a cycle (it shouldn't, but the invariant is worth defending), the resolver could loop. Mitigation: a cycle-detection guardrail (candidate G59).
- **Consumer smoke matrix re-entry artifact size** — the Pack stage produces a 7-RID nupkg set (~100-200 MB total); 7 runners download that during smoke = 700 MB–1.4 GB of transfer. Cheap inside GitHub Actions but noticeable locally if a contributor reproduces the matrix. Mitigation: measure during impl; split into per-RID artifact upload/download if needed.
- **Tag-push trigger fan-out for train release** — surfaced empirically during the 2026-05-01 rehearsal. GitHub Actions `on.push.tags` semantics fire one workflow run per pushed tag; a train release requires every family tag at HEAD (so `GitTagVersionProvider.Train` can find it) plus the `train-*` tag, so an atomic N+1-tag push creates N+1 workflow runs of which only 1 is the desired train run. The other N also functionally fail at the partial-scope gaps (G58 / `ConsumerSmoke`). No native git/Actions mechanism collapses this fan-out. Mitigation: see §15 Empirical Operational Reality for the reconsideration direction.

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

## 12. Implementation Summary

Implemented through the ADR-003 Cake refactor and CI/CD rewrite:

- Version provider architecture and `ResolveVersions` entrypoint.
- Per-stage request records and stage-owned validation.
- `NativeSmoke` extraction and consumer-smoke matrix re-entry.
- `--family-version` retirement in favor of explicit per-family mappings and `versions.json`.
- Dynamic matrix generation from `manifest.runtimes[]`.
- `release.yml` as the single canonical release workflow.

Remaining outside this ADR's completed implementation: public NuGet promotion (`PublishPublic`, Trusted Publishing, and the first nuget.org prerelease) plus the PD-8 release-recovery playbook.

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
| 2026-04-20 | v1 | Initial decision: version-source providers, stage-owned validation, and single release workflow direction. |
| 2026-04-21 | v1.1 | Implementation discovery moved local feed composition into `SetupLocalDevTaskRunner` and kept NativeSmoke out of the local feed-prep loop. |
| 2026-04-30 | v2 | Updated from proposal record to current implementation state; removed stale pseudocode, open implementation questions, and historical checklist clutter. |
| 2026-05-01 | v2.1 | Added §15 Empirical Operational Reality capturing rehearsal evidence + four open gaps. The §3.4 trigger model (tag-push as canonical release trigger) is now under reconsideration; final direction pending PD-7 design pass. |

---

## 15. Empirical Operational Reality (2026-05-01)

The trigger-aware version routing landed in commit `437edff` (April 29, 2026 baseline) was rehearsed end-to-end across four CI runs on master `0ffaa7a`. The rehearsals validated the `Resolve Versions` routing layer but exposed **four operational gaps** between the policy this ADR locked and the tooling that backs it. Two of the four gaps (the satellite-only and partial-scope guardrail behaviors) are within the design envelope this ADR anticipated as deferred; the third revealed a CI-workflow design issue that this ADR did not contemplate; the fourth was a fix-now blocker that has already landed.

| # | Stage | Surfaced via | Status |
| --- | --- | --- | --- |
| 1 | `Resolve Versions` `--scope` filter rejected full-tag scope | Run 25212911868 (initial diagnosis pre-commit; fixed before tag push) | Fixed in `437edff` |
| 2 | `PreFlight` + `Pack` G58 cross-family resolvability is scope-contains only; satellite tag without core in scope blocks | Run 25212911868 (`sdl2-image-2.8.0-rehearsal.1` halted at G58) | **Open** — feed-probe deferred (this ADR §8 Q1 era; resolved-state in §8 currently silent) |
| 3 | `PackageConsumerSmoke` runner enforces all-or-nothing manifest-concrete scope | Run 25213284985 (`sdl2-core-2.32.0-rehearsal.1` halted at `EnsureSelectionSupportsCurrentSmokeScope`) | **Open** — partial-scope smoke not yet supported |
| 4 | `release.yml` `on.push.tags` fans out one workflow run per pushed tag; train release atomic-pushes N+1 tags so N+1 workflow runs queue, of which only 1 is the desired train run | Train rehearsal preparation (no run executed; design wart blocked the rehearsal command sequence) | **Open** — trigger mechanism under reconsideration |

**Implication for §3.4 (CI/CD Orchestration).** §3.4 sketched a single `release.yml` triggering on `tags: ['sdl2-*-*.*.*', 'sdl3-*-*.*.*', 'train-*']`. The fan-out semantic of GitHub Actions tag triggers makes the train arm of this trigger filter operationally impractical: train release requires N+1 tags at HEAD (per-family + the train tag), which fans out into N+1 workflow runs. The N family-tag runs are unwanted noise that also functionally fail at gaps #2 / #3, requiring the operator to manually cancel them via the Actions UI. This was rejected by Deniz (2026-05-01) as not a tenable operator UX.

**Direction (decision pending; PD-7 adjacent).** Two leading candidates for the trigger mechanism, neither finalized:

1. **Manual `workflow_dispatch` as canonical trigger.** Releases are operator-driven via GitHub Actions UI or `gh workflow run`, supplying scope through `mode=explicit explicit-versions=...`. Tag pushes drop from the trigger filter; tags become audit-trail-only records created after a successful release. Targeted release of a single family is a dispatch with a single `--explicit-version` entry. Gaps #2 + #3 still apply for sub-5-family scopes but the operation is deliberate and controlled rather than fanning out into noise.
2. **GitHub Releases as trigger source.** A GitHub Release object (creating tags as a side-effect) carries a body / release notes that encodes the family-version mapping. The workflow triggers on `release: published`, parses the body, routes through `ResolveVersions --version-source=explicit`. One Release event = one workflow run regardless of how many family tags the release object creates. Audit trail and trigger combine.

Both options preserve the §2 governance policy (targeted vs full-train, core-first ordering, family-version coherence) documented in [release-lifecycle-direction.md](../knowledge-base/release-lifecycle-direction.md). Both also leave gaps #2 and #3 as **separate** open work — closing the trigger-mechanism question does not subsume the partial-scope gaps.

**This ADR is not superseded.** The §2 mental model (RID → Family → Version axes; three providers; pre-stage version resolution; immutable mapping; stage-owned validation) and §3.1–§3.3 (provider design, stage request records, resolver-centric `SetupLocalDev`) remain canonical. §3.4's specific tag-push trigger filter is the only piece under active reconsideration; the rest of the orchestration architecture stands.

For the operational rule today: **prefer `workflow_dispatch mode=explicit publish-staging=true` for any actual release work.** The tag-push path remains in `release.yml` as research surface but is not the canonical operator path until either (a) gap #4 is addressed via a workflow-design slice, or (b) tag-push is dropped from the trigger filter altogether in favor of one of the candidates above.

Tracking moves to the [Phase 2b adaptation plan](../phases/phase-2-adaptation-plan.md) and [release-guardrails.md §5.1](../knowledge-base/release-guardrails.md). The PD-7 design pass that finalizes the public-promotion path will pin the trigger mechanism for both internal staging and nuget.org publish.
