# Release Lifecycle Direction

> **Status:** Canonical — locked **policy** decisions. Tool-specific implementation choices (marked as *current tooling* notes) are non-normative and may evolve without changing the policy.
>
> **Scope after 2026-04-21 narrowing:** this document owns **stable release policy** only — ubiquitous language, release governance, D-3seg versioning, dependency contracts, CI matrix model, promotion path, cross-referenced version planes, and long-lived tradeoffs. **Orchestration architecture** (version-source providers, pipeline stage ownership, stage-owned validation, invocation semantics, `--family-version` retirement under PD-13, `SetupLocalDev` resolver-centric composition) lives in [ADR-003](../decisions/2026-04-20-release-lifecycle-orchestration.md). Pointers below direct readers there for those topics rather than duplicating material that evolves on a faster cadence.
>
> **Last updated:** 2026-04-21 (ADR-003 narrowing pass — orchestration material devolved to ADR-003; policy material retained here)
>
> **Decision records binding this document:**
>
> - [ADR-001: D-3seg Versioning, Package-First Local Dev, Artifact Source Profile Abstraction](../decisions/2026-04-18-versioning-d3seg.md) — authoritative for §3, §4 cross-family upper bound, §7, and Tradeoffs #4/#5.
> - [ADR-003: Release Lifecycle Orchestration + Version Source Providers](../decisions/2026-04-20-release-lifecycle-orchestration.md) — authoritative for pipeline-stage ownership, version-source provider contracts, invocation semantics, `SetupLocalDev` composition model, and the PD-13 `--family-version` retirement direction.
>
> **Research basis:** Four independent research efforts converged on the prior (pre-D-3seg) conclusions; ADR-001 refined the versioning and consumer-contract decisions on 2026-04-18; ADR-003 formalized the orchestration ownership model on 2026-04-20:
>
> - `docs/research/release-lifecycle-patterns-2026-04-14-claude-opus.md`
> - `docs/research/release-lifecycle-strategy-research-2026-04-14-gpt-codex.md`
> - `docs/research/release-strategy-history-audit-2026-04-14-gpt-codex.md`
> - `docs/research/execution-model-strategy-2026-04-13.md` *(three-mode framing amended by ADR-001; see §7)*

## Relationship to Other Documents

- **Packaging topology** (hybrid static, per-library .Native split): locked in `docs/plan.md` Strategic Decisions. This document does not revisit that.
- **Execution model** (ADR-001 two-source feed preparation): consumer contract is always `PackageReference`; only feed preparation varies (`Local` / `Remote`). The historical three-mode framing lives in `docs/research/execution-model-strategy-2026-04-13.md`, amended by ADR-001. This document extends the current model with release governance, it does not replace it.
- **CI/CD pipeline architecture**: designed in `docs/knowledge-base/ci-cd-packaging-and-release-plan.md`. That document describes pipeline implementation. This document describes the policy it must enforce.
- **Implementation plan**: `docs/phases/phase-2-adaptation-plan.md` describes how these policy decisions are applied to manifest.json, Cake, csproj, CI, and local dev — the multi-stream implementation roadmap.

---

## 1. Ubiquitous Language

These terms are used consistently across all documentation, issues, code, and conversation in this repository.

### Release Unit

**Package Family** — A release unit consisting of one managed bindings package and its corresponding .Native package. Both packages in a family always share the same version and are always released together. A package family is the atomic unit of release.

> Example: The **core family** consists of `Janset.SDL2.Core` (managed bindings) and `Janset.SDL2.Core.Native` (pre-compiled native binaries for all RIDs).

**Family Identifier** — The canonical lowercase string that names a family across `manifest.json`, csproj `<MinVerTagPrefix>`, and git tags. Format: `sdl<major>-<role>`. Examples: `sdl2-core`, `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`, `sdl2-net`. Future SDL3 families parallel this convention: `sdl3-core`, `sdl3-image`, etc.

> The explicit `sdl<major>-` prefix is mandatory. It mirrors the `Janset.SDL2.*` PackageId convention, eliminates ambiguity once SDL3 families are introduced, and uses a product identifier (`sdl2`/`sdl3`) that cannot be confused with SemVer numbers.

**Core Family** — The SDL family's core package family (`sdl2-core` for SDL2, `sdl3-core` for SDL3). All satellite families in the same SDL major-version line depend on it. When a release set includes both core and satellite families, core releases first (see Release Ordering).

**Satellite Family** — Any non-core package family within an SDL major-version line: e.g. `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`, `sdl2-net`. Each satellite family depends on the core family of the same SDL major-version line but is versioned and released independently from other satellite families.

### Versioning

**Family Version** — The single shared version number for both packages within a family. There is no separate "binding version" and "native version" — one version per family, always.

**Family Tag** — A git tag in the format `<family-identifier>-<semver>` that marks a release point for a specific family. The tag is the source of truth for versioning. The `<semver>` portion follows §3 D-3seg. Examples: `sdl2-core-2.32.0`, `sdl2-image-2.8.0`, `sdl2-mixer-2.8.1-beta.1`. Future SDL3 examples: `sdl3-core-3.0.0`, `sdl3-image-3.0.0`.

### Release Governance

**Targeted Release** — Release of one or more specific families without touching others. This is the default release mode. Most releases are targeted.

**Full-Train Release** — Coordinated release of all families together. Triggered by cross-cutting changes or milestone checkpoints. Full-train ensures coherence across the entire package ecosystem.

**Full-Train Trigger** — A category of change that mandates a full-train release instead of a targeted release. Defined exhaustively:

- vcpkg baseline update (changes native library versions across all families)
- Triplet or strategy changes (affects build output for all families)
- Shared dependency baseline changes (e.g., .NET SDK version, Cake version)
- Build-host validation or guardrail changes (affects what passes/fails for all families)
- Milestone or periodic coherence checkpoints

**Release Ordering** — When the release set includes both core and satellite families (whether full-train or multi-family targeted): core releases first, then satellites. This is required because satellites declare a minimum dependency (`>=`) on core, so core must be available on the feed before satellite packages can resolve their dependencies. When only satellite families are in the release set, no ordering constraint applies — they can release independently or in parallel.

**Release Promotion** — The staged path a package follows from build to public availability:

1. **Local folder feed** — produced by every local Cake pack run. For developer validation.
2. **Internal feed** — CI-produced packages (both pre-release and stable candidates). Staging area for integration testing and cross-family coherence validation.
3. **Public NuGet.org** — manual promotion of validated stable packages from internal feed. Deliberate, gated, audited.

Each stage is a separate, conscious step. Packages never skip stages on the CI/public promotion path: all public releases must pass through the internal feed first. The local folder feed is a developer validation stage and is not a required prerequisite for CI promotion. The internal feed holds both pre-release versions (from main branch pushes) and stable candidates (from family tag pushes). Only validated stable packages are promoted to public.

### Dependency Contracts

**Within-Family Constraint** — The NuGet dependency between a managed package and its own native package within the same family. This is a **minimum version constraint** (`>=`), matching the cross-family convention. Both packages share a version and always ship together (see §2 Release Governance), so in practice a consumer resolves the exact same version. Using minimum range instead of exact pin under S1 aligns with the industry standard for native-bundled .NET libraries (SkiaSharp, Avalonia, OpenTelemetry). Drift protection between managed and native is enforced at orchestration time, not at consumer-side resolution (see §4 Drift Protection Model).

> Example: `Janset.SDL2.Core 2.32.0` depends on `Janset.SDL2.Core.Native (>= 2.32.0)`.

**Cross-Family Constraint** — The NuGet dependency from a satellite family's managed package to the core family's managed package. This is a **minimum version constraint with an explicit upper bound** at the next SDL upstream major: `>= x.y.z, < (UpstreamMajor + 1).0.0`. The upper bound is SemVer-idiomatic "don't accept next major" hygiene and cheap insurance against accidental resolution into any future SDL major. It does NOT tighten to `< (UpstreamMinor + 1).0.0` — that would make each SDL minor bump cascade satellite re-releases and defeat version independence. Upper bound enforcement is guardrail **G56** at post-pack time.

> Example: `Janset.SDL2.Image 2.8.0` depends on `Janset.SDL2.Core (>= 2.32.4, < 3.0.0)`. The minimum is the Core version the Image family was packed against (recorded in `janset-native-metadata.json` for audit); the upper bound is the next SDL major boundary.

### CI and Build

**Build Axis** — The primary CI matrix dimension: one job per supported RID. Each job runs the full vcpkg install for that triplet (all libraries), then harvests per-library within the same job. The build matrix has one entry per supported RID (currently 7), not library × RID.

**Harvest Axis** — The secondary dimension within each build axis job: per-library harvesting, validation, and artifact production. Sequential within the job, driven by Cake.

---

## 2. Release Governance Model

### Default: Targeted Release

When a change affects only one family (e.g., a binding bug fix in SDL2.Image, or a native codec update for SDL2.Mixer), only that family releases. Other families are untouched. The family tag triggers the release.

### Exception: Full-Train Release

When a change crosses family boundaries, all families release together. The full-train trigger list is authoritative — if a change matches any trigger, full-train is mandatory.

A full-train release follows release ordering: core first, then all satellites (in parallel after core completes).

### Cross-SDL-Major-Version Boundary

A release of an `sdl2-*` family is independent of any `sdl3-*` family. The two SDL major-version lines coexist in the monorepo but never share a release set. Cross-major-version coordination, when it eventually matters, is a separate concern outside the family-tag mechanism.

### Cadence

There is no fixed release schedule. Releases are event-driven (family tag push). However, periodic full-train coherence checkpoints are recommended at milestones to verify that all families still work together, even if no individual family has changed.

---

## 3. Versioning Model (D-3seg)

Each package family is versioned using a three-segment scheme that anchors to its upstream library. Authoritative decision record: [ADR-001: D-3seg Versioning](../decisions/2026-04-18-versioning-d3seg.md).

**Format:** `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`

- **UpstreamMajor.UpstreamMinor** is drawn from the family's native upstream library version as declared in `manifest.json library_manifests[].vcpkg_version`. Each family follows its own upstream library, so Major.Minor values differ across families (e.g., Core `2.32.x`, Image `2.8.x`, Gfx `1.0.x`). Post-pack guardrail **G54** asserts the match.
- **FamilyPatch** is the repo's own iteration counter within a given UpstreamMajor.UpstreamMinor line. Monotonically increasing. Human-set at release time. Resets to `0` when UpstreamMajor or UpstreamMinor changes.
- **No build segment.** Three-part SemVer 2.0 only. Keeps MinVer compatible, matches NuGet SemVer 2.0 convention ([reference](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning?tabs=semver20sort)).
- **Prerelease suffixes permitted** (e.g., `2.32.0-rc.1`, `2.32.0-local.<timestamp>`). Prereleases MUST preserve the UpstreamMajor.UpstreamMinor prefix — no generic `0.1.0-*` shortcuts, including bootstrap flows.

### Examples

| Scenario | Family version |
| --- | --- |
| Initial release wrapping SDL2 `2.32.10` | `Janset.SDL2.Core 2.32.0` |
| Binding marshal fix, SDL2 still `2.32.10` | `Janset.SDL2.Core 2.32.1` |
| vcpkg SDL2 patch bump (`2.32.12`) + re-pack | `Janset.SDL2.Core 2.32.2` |
| vcpkg port_version bump + re-pack | `Janset.SDL2.Core 2.32.3` |
| SDL2 minor bump (`2.33.0`) | `Janset.SDL2.Core 2.33.0` (FamilyPatch resets) |
| SDL3 transition | `Janset.SDL3.Core 3.x.y` under `sdl3-core` family identifier |

### Source of truth + tooling

- The family version is derived from the family tag (e.g., `sdl2-core-2.32.0` → version `2.32.0`) via **MinVer at pack time**. MinVer functions as a tag reader, not a version inventor.
- Untagged commits receive automatic pre-release versions from MinVer (e.g., `2.32.1-alpha.0.3`).
- Operator-supplied version input (for manual escape and CI workflow_dispatch paths) flows through an explicit provider contract. The three-provider architecture (Manifest / GitTag / Explicit), the per-invocation resolve-once-immutable semantics, and the PD-13 `--family-version` retirement direction live in [ADR-003 §3.1 + §6](../decisions/2026-04-20-release-lifecycle-orchestration.md). Tag remains the source of truth for stable releases.
- All versioning logic is driven through the build system, not manual project file edits. (*Current tooling: NuGet.Versioning package via Cake for SemVer parsing; MinVer with `<MinVerTagPrefix>sdl<N>-<role>-</MinVerTagPrefix>` per csproj.*)

### Upstream version metadata (outside the version string)

The exact upstream library version (`2.32.10` vs `2.32.12`) and `vcpkg_port_version` are **not** encoded in the family version string. They are preserved as package-shipped metadata in two complementary forms:

1. **`janset-native-metadata.json`** at the root of each `.Native` nupkg (machine-readable; asserted by G55). Contains: `janset_family_version`, `family_identifier`, `upstream_library`, `upstream_version`, `vcpkg_port_version`, `triplet_set`, `build_commit`.
2. **README mapping table** delimited by `<!-- JANSET:MAPPING-TABLE-START -->` / `<!-- JANSET:MAPPING-TABLE-END -->` markers (human-readable; Cake-generated; asserted by G57).

### Version Independence (Cross-Family)

Family versions are fully independent across families because each tracks its own upstream. Core at `2.32.0` while Image is at `2.8.0` and Gfx at `1.0.0` is the expected shape, not an oddity. Within-family, the managed and native packages share a single version per family-lock (see §2 Release Governance).

### What Changes a Version

- **FamilyPatch** (`2.32.0 → 2.32.1`): binding bug fixes, native rebuild at same upstream version, vcpkg patch bump roll-up, vcpkg port_version bump roll-up, RID set change, documentation fix in binding attributes.
- **UpstreamMinor** (`2.32.x → 2.33.0`): upstream native minor version bump (new SDL API surface available to expose). FamilyPatch resets to `0`.
- **UpstreamMajor** (`2.x.y → 3.x.y`, hypothetical): upstream native major version bump. In practice handled via new family identifier (`sdl2-core` → `sdl3-core`), not in-place bump.

**Breaking managed API changes** without an upstream bump are not a normal-flow occurrence under D-3seg. If required, they require a separate Architecture Decision Record documenting the break, the affected consumer surface, and the transition plan. See ADR-001 §2.1 and §5 for discussion.

---

## 4. Dependency Contract Model

### Within a Family

```text
Janset.SDL2.Image (managed, v2.8.0)
  └── depends on: Janset.SDL2.Image.Native (>= 2.8.0)
```

Minimum range. The two packages share a version and always release together (§2), so consumers in practice resolve the exact same version within a family. The contract is consumer-side flexible, orchestration-side strict — drift protection lives in §4 Drift Protection Model, not in the consumer-side dependency expression.

### Across Families

```text
Janset.SDL2.Image (managed, v2.8.0)
  ├── depends on: Janset.SDL2.Image.Native (>= 2.8.0)                  ← within-family (minimum)
  └── depends on: Janset.SDL2.Core (>= 2.32.4, < 3.0.0)                  ← cross-family (ranged)
```

Within-family is minimum range (see above). **Cross-family is minimum PLUS an explicit upper bound at `< (UpstreamMajor + 1).0.0`.** The minimum is the Core version Image was packed against (recorded in `janset-native-metadata.json`). The upper bound rejects any hypothetical `Janset.SDL2.Core 3.x` (which should not exist under the `sdl2-core`/`sdl3-core` family identifier split, but the range is explicit hygiene anyway). A consumer on Core `2.32.5` or Core `2.33.0` resolves successfully; a consumer on Core `3.0.0` is rejected.

Post-pack guardrail **G56** asserts that every satellite nuspec declares the upper bound. The ranged dependency is emitted by Cake at pack time from the UpstreamMajor recorded in `manifest.json library_manifests[].vcpkg_version` for the core family.

### Drift Protection Model

Within-family consistency is guaranteed at **orchestration time**, not at consumer-side resolution time. Three mechanisms combine to make mismatched within-family distribution practically impossible:

1. **Cake `PackageTask` is atomic per family.** Both managed and native nupkg for a family are emitted from a single task invocation at the same family version. There is no code path where one package is emitted at version `X` and its sibling at version `Y`. (The specific CLI input surface — `--family-version` today, `--explicit-version key=value,...` post-PD-13 — does not change this atomicity invariant.)
2. **Post-pack validator** (Stream D-local) asserts that both emitted `.nupkg` files declare the same `<version>` element and that the managed nuspec contains the expected `>=` dependency on the native. Runs before any artifact leaves the build host. **State 2026-04-18 (post-ADR-001):** Validator is `PackageOutputValidator` in `Modules/Packaging/`, returns a Result-pattern `PackageValidationResult` that accumulates every guardrail observation into a single `PackageValidation` aggregate — operators see the full failure set on a failed pack, not first-throw-wins. Current guardrail set: G21 minimum-range, G22 per-TFM consistency, G23 within-family version match, G25 symbol package validity, G26 repository commit, G27 canonical metadata, G47 `buildTransitive/` contract presence, G48 per-RID native payload shape, **G54 UpstreamMajor.UpstreamMinor ↔ manifest coherence (PreFlight)**, **G55 `janset-native-metadata.json` presence + schema + content match (post-pack)**, **G56 satellite cross-family upper-bound declared (post-pack)**, **G57 README mapping table ≡ manifest (post-pack)**.
3. **Release train ordering** (§2) ensures that whenever both members of a family reach the internal feed, they do so as a coherent set within the same release wave.

**Validation state as of 2026-04-18:** D-local has been 3-platform validated end to end for the Phase 2a proof slice (`sdl2-core` + `sdl2-image`) on the three original hybrid-static RIDs (`win-x64`, `linux-x64`, `osx-x64`). PA-2 landed the missing four overlay triplets on 2026-04-18 and moved all 7 manifest runtime rows to `hybrid-static`, so the config is now coherent for a 7/7 hybrid allocation. That does **not** mean the four newly-covered rows (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) have been packed or consumer-smoked under the post-S1 guardrail set yet — that behavioral coverage remains Phase 2b work. See [`playbook/cross-platform-smoke-validation.md`](../playbook/cross-platform-smoke-validation.md) for the exact checkpoint results and command reproducibility, and see [phases/phase-2-adaptation-plan.md "Strategy & Tool Landing State"](../phases/phase-2-adaptation-plan.md#strategy--tool-landing-state) for the interface-level landing state.

Consumer-side, the `>=` contract means a consumer who manually pulled mismatched family members would succeed (within SemVer compatibility) — matching industry expectations and the SkiaSharp precedent. Mismatched distribution is prevented at release time, not at consumer-side resolution.

> **Why not consumer-side exact pin?** It was investigated (2026-04-16 A0 spike) and empirically proven feasible with `PrivateAssets="all"` + bracket-notation CPM + `PackageReference`. The mechanism was retired on 2026-04-17 because it hit upstream NuGet limitations around MSBuild global-property propagation through `_GetProjectVersion` sub-evaluations (see [NuGet/Home#11617](https://github.com/NuGet/Home/issues/11617), open since 2022; [NuGet/Home#5556](https://github.com/NuGet/Home/issues/5556), open since 2017). Rather than carry a local workaround, the project adopted the industry-standard minimum-range contract. The guarantee that was surrendered (consumer-side exact pin) was already belt-and-suspenders against a scenario Cake orchestration prevents by construction.

### The Meta-Package

```text
Janset.SDL2 (meta-package, v?.?.?)
  ├── Janset.SDL2.Core (= 2.32.0)
  ├── Janset.SDL2.Image (= 2.8.0)
  ├── Janset.SDL2.Mixer (= 2.8.0)
  ├── Janset.SDL2.Ttf (= 2.24.0)
  ├── Janset.SDL2.Gfx (= 1.0.0)
  └── Janset.SDL2.Net (= 2.2.0)
```

Each family carries its own UpstreamMajor.UpstreamMinor (see §3 D-3seg Version Independence). Cross-family visual heterogeneity is intentional and documented via the README mapping table.

The meta-package pins a **deterministic known-good combination** using exact version pins for every family. It releases only as part of a full-train release — never for a targeted single-family release.

> **Meta-package own versioning scheme is an OPEN decision** (sub-item of PD-7 full-train orchestration). The meta-package has no single upstream library to anchor D-3seg to. Candidate schemes: independent SemVer (`1.0.0`, `2.0.0`, …) or date-versioned (`2026.04.0`, `2026.07.0`, …). ADR-001 defers this to the full-train ADR whenever PD-7 lands.

---

## 5. CI Matrix Model

### Build Matrix Shape

```text
7 RID jobs (build axis)
  Each job:
    1. vcpkg install for this triplet (all libraries, all-or-nothing)
    2. Cake harvest for each library (harvest axis, sequential)
    3. Cake validate per strategy (hybrid-static: check for transitive dep leaks)
    4. Upload per-library harvest artifacts
```

The matrix is **not** library × RID (that would be 42 jobs duplicating vcpkg installs). It is RID-only, with per-library work inside each job.

PA-1 closed on 2026-04-18: Stream C keeps the RID-only model. `strategy` remains metadata on each runtime row in `manifest.json`; it is not promoted to a separate matrix axis. Supporting analysis: [`ci-matrix-strategy-review-2026-04-17.md`](../research/ci-matrix-strategy-review-2026-04-17.md).

### Matrix Generation

The matrix is generated dynamically from `manifest.json` runtimes section. No hardcoded YAML matrices. This eliminates the triplet drift problem (where YAML and manifest diverge).

### Change Detection

- Coarse (CI level): path-based triggers determine which families need rebuilding.
- Precise (build system level): MSBuild-aware change detection identifies transitive dependency changes that path filters miss. (*Current tooling consideration: dotnet-affected integrated as a Cake tool.*)
- Override: full-train triggers bypass all change detection — everything builds.

---

## 6. Release Promotion Path

### Stage 1: Local Folder Feed

- Produced by the build system's package task locally.
- Used for `SetupLocalDev`-driven local package validation, IDE-ready smoke restore/build, and other local package-consumer development.
- No external infrastructure required.

### Stage 2: Internal Feed (Staging)

- CI-produced packages. (*Current tooling consideration: GitHub Packages or equivalent.*)
- Holds **both** pre-release versions (from main branch pushes) and stable candidates (from family tag pushes).
- Acts as a staging area — packages are validated here before public promotion.
- Used for integration testing and cross-family coherence validation.

### Stage 3: Public NuGet.org

- Manual promotion of validated stable packages from internal feed via a separate, deliberate workflow.
- Only stable versions that passed internal validation are promoted. Pre-release packages may be published to public if explicitly intended (e.g., public beta programs).
- Release metadata (release notes, git SHA, build fingerprint) attached.

---

## 7. Cross-Referenced Version Planes

Two version concepts coexist in this repository. Under pre-D-3seg canon they were described as "orthogonal." Under ADR-001 they are **cross-referenced, not orthogonal**: the family version's UpstreamMajor.UpstreamMinor segment mirrors the upstream library's Major.Minor (see §3). Confusing their roles still leads to policy drift, so the distinction is preserved for clarity.

### Upstream Library Version Plane

The version of the native library being shipped — SDL2 `2.32.10`, SDL2_image `2.8.8`, and so on. Tracked in `manifest.json` under `library_manifests[].vcpkg_version` (with an associated `vcpkg_port_version` for port revisions). Must match the version installed by vcpkg for the corresponding triplet. `PreFlightCheckTask` enforces this match.

The exact upstream patch version and port_version are **not** encoded in the family version string (see §3). They live in:

- `janset-native-metadata.json` packed into each `.Native` nupkg (machine-readable; G55).
- README mapping table delimited by comment markers (human-readable, Cake-generated; G57).

### Family Version Plane

The version of a `Janset.SDL<N>` package family — `Janset.SDL2.Core 2.32.0` — at the shape defined by §3 D-3seg. Derived at pack time from git family tags (e.g., `sdl2-core-2.32.0`) via MinVer. The UpstreamMajor.UpstreamMinor segment is anchored to the upstream library (§3); FamilyPatch is the repo's release-iteration counter within that anchor.

### Coherence Between the Two Planes

The two planes are **structurally coupled** at the UpstreamMajor.UpstreamMinor level:

- The family version's first two segments MUST match `manifest.json library_manifests[].vcpkg_version`'s first two segments for the same family.
- **Guardrail G54** (PreFlight) enforces this match. Example: a tag `sdl2-core-2.31.0` at a commit where `manifest.json` declares SDL2 at `2.32.10` fails PreFlight.

The FamilyPatch segment (third position) is **independent** of the upstream patch. This means `Janset.SDL2.Core 2.32.0`, `2.32.1`, and `2.32.2` are valid successive releases that may wrap identical or different upstream patches (`2.32.10`, `2.32.12`, …). The mapping table records which exact upstream each family version corresponds to.

### Example

| Plane | Value | Source of truth | Coupling |
| --- | --- | --- | --- |
| Upstream library version | SDL2 `2.32.10` | `manifest.json library_manifests[].vcpkg_version` + `vcpkg.json overrides[].version` | Major.Minor = `2.32` |
| Family version (core) | `Janset.SDL2.Core 2.32.0` | git tag `sdl2-core-2.32.0` → MinVer | First two segments = `2.32` |

Both planes carry `2.32` in their first two segments; their third segments are independent (upstream patch `10` vs FamilyPatch `0`).

### Cross-family plane independence

Each family tracks its own upstream library's Major.Minor independently. `Janset.SDL2.Core` follows SDL2's Major.Minor (`2.32.x`). `Janset.SDL2.Image` follows SDL2_image's Major.Minor (`2.8.x`). `Janset.SDL2.Gfx` follows SDL2_gfx's Major.Minor (`1.0.x`). The families share the `sdl2-*` release-line identifier but their version numbers differ visually. The README mapping table (G57-enforced currency) documents this expected heterogeneity.

---

## Tradeoffs Explicitly Accepted

1. **Family-lock means no managed-only release.** If only binding code changed but native binaries are identical, the native package still re-releases at the new family version with unchanged binaries. This is the SkiaSharp/Avalonia tradeoff: simplicity over flexibility.

2. **Fat native package means download overhead.** Each .Native package contains all 7 RIDs (~25 MB). Consumers download all platforms even when targeting one. This is acceptable for SDL2-class library sizes and avoids the SkiaSharp 9-package explosion.

3. **One job per RID, not per library×RID.** Per-library parallelism within a RID job is sacrificed for vcpkg cache efficiency. Sequential per-library harvest within a job is fast enough (harvest is I/O, not compute).

4. **Tag-derived versions as the primary source.** Family tags drive build-time `$(Version)` via MinVer at pack time. Operator-supplied version input (manual escape, CI workflow_dispatch) is carried as an explicit provider input, not as a shipped CLI-flag surface. Version numbers are never manually edited in project files. The tradeoff: stable releases require git tag discipline. Provider architecture, invocation semantics, and the PD-13 `--family-version` retirement direction: [ADR-003 §3.1 + §6](../decisions/2026-04-20-release-lifecycle-orchestration.md). (*Current tooling: MinVer with `<MinVerTagPrefix>sdl<N>-<role>-</MinVerTagPrefix>` per family.*)

5. **Minimum range within family + cross-family upper bound, drift-prevented at orchestration time.** Within-family uses `>=`. Cross-family uses `>= x.y.z, < (UpstreamMajor + 1).0.0` — minimum plus explicit upper bound at next SDL major. Matches SkiaSharp / Avalonia / OpenTelemetry conventions for the lower side; upper bound added under ADR-001 (2026-04-18) as defence against accidental cross-SDL-major resolution. Within-family exact pin was investigated pre-S1 (2026-04-16 A0 spike) and proven mechanically feasible, but it hit MSBuild global-property propagation limitations through NuGet's pack-time `_GetProjectVersion` sub-evaluation (`NuGet.Build.Tasks.Pack.targets` uses `Properties=` globals-replace rather than `AdditionalProperties=` globals-extend; unchanged for 8+ years, still unchanged in .NET 10 SDK; no merged or in-flight fix upstream). Drift protection moved to the orchestration layer (§4 Drift Protection Model).

6. **No build segment, no 4-part SemVer.** D-3seg uses three-part SemVer only (`UpstreamMajor.UpstreamMinor.FamilyPatch`). Considered alternative: append a build counter as fourth segment (`2.32.10.1`) that would preserve exact upstream patch visibility. Rejected because MinVer supports three-part SemVer 2.0 + prerelease + metadata natively, and four-part would require bypass mechanisms or custom pipeline. Exact upstream patch visibility is preserved via `janset-native-metadata.json` (G55) and README mapping table (G57) instead — metadata-based signal rather than version-string encoding. See ADR-001 §3 (Rationale) for comparison with pure upstream-tracked and 4-segment alternatives.

7. **Package-first consumer contract across the board.** Smoke, examples, sandbox, and future sample csprojs all consume Janset artifacts via `PackageReference` against a local folder feed. ProjectReference-based Source Mode (Content injection from `src/native/<lib>/runtimes/` into consumer `bin/`) is retired. Single consumer model → smoke regressions reflect external-consumer regressions 1:1. The fast-loop binding-debug path (live source changes reflected immediately) is no longer mainline; if required, handled via separate opt-in throwaway harness. See ADR-001 §2.6.
