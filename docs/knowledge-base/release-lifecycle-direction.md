# Release Lifecycle Direction

> **Status:** Canonical — locked policy decisions. Tool-specific implementation choices (marked as *current tooling* notes) are non-normative and may evolve without changing the policy.
>
> **Last updated:** 2026-04-17 (S1 adoption — within-family exact-pin retired in favor of SkiaSharp-style minimum range; see §4 Drift Protection Model and Tradeoff 5)
>
> **Research basis:** Four independent research efforts converged on these conclusions:
>
> - `docs/research/release-lifecycle-patterns-2026-04-14-claude-opus.md`
> - `docs/research/release-lifecycle-strategy-research-2026-04-14-gpt-codex.md`
> - `docs/research/release-strategy-history-audit-2026-04-14-gpt-codex.md`
> - `docs/research/execution-model-strategy-2026-04-13.md`

## Relationship to Other Documents

- **Packaging topology** (hybrid static, per-library .Native split): locked in `docs/plan.md` Strategic Decisions. This document does not revisit that.
- **Execution model** (Source Mode / Package Validation Mode / Release Mode): established in `docs/research/execution-model-strategy-2026-04-13.md`. This document extends it with release governance, it does not replace it.
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

**Family Tag** — A git tag in the format `<family-identifier>-<semver>` that marks a release point for a specific family. The tag is the source of truth for versioning. Examples: `sdl2-core-1.0.0`, `sdl2-image-1.0.3`, `sdl2-mixer-1.1.0-beta.1`. Future SDL3 examples: `sdl3-core-1.0.0`, `sdl3-image-1.0.0`.

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

> Example: `Janset.SDL2.Core 1.2.0` depends on `Janset.SDL2.Core.Native (>= 1.2.0)`.

**Cross-Family Constraint** — The NuGet dependency from a satellite family's managed package to the core family's managed package. This is a **minimum version constraint** (`>=`). A satellite can release independently as long as the core version on the feed is at least the declared minimum.

> Example: `Janset.SDL2.Image 1.0.3` depends on `Janset.SDL2.Core (>= 1.2.0)`.

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

## 3. Versioning Model

Each package family is versioned independently using Semantic Versioning 2.0 per the [NuGet SemVer 2.0 convention](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning?tabs=semver20sort).

- The version is derived from the family tag (e.g., `sdl2-core-1.2.0` → version `1.2.0`).
- Untagged commits receive automatic pre-release versions (e.g., `1.2.1-alpha.0.3`).
- All versioning logic is driven through the build system, not manual project file edits.
- SemVer parsing and comparison within the build system uses a dedicated NuGet-compatible library. (*Current tooling consideration: NuGet.Versioning package via Cake.*)

### Version Independence

Family versions are fully independent. Core might be at `2.0.0` while Image is at `1.3.1` and Mixer is at `1.0.0-beta.2`. Each family evolves at its own pace.

### What Changes a Version

- **Patch** (1.0.0 → 1.0.1): bug fixes, native rebuild without ABI change, documentation fixes in binding attributes.
- **Minor** (1.0.0 → 1.1.0): new API surface (new SDL functions exposed), upstream native minor version bump.
- **Major** (1.0.0 → 2.0.0): breaking API changes, upstream native major version bump, RID support removal.

---

## 4. Dependency Contract Model

### Within a Family

```text
Janset.SDL2.Image (managed, v1.0.3)
  └── depends on: Janset.SDL2.Image.Native (>= 1.0.3)
```

Minimum range. The two packages share a version and always release together (§2), so consumers in practice resolve the exact same version within a family. The contract is consumer-side flexible, orchestration-side strict — drift protection lives in §4 Drift Protection Model, not in the consumer-side dependency expression.

### Across Families

```text
Janset.SDL2.Image (managed, v1.0.3)
  ├── depends on: Janset.SDL2.Image.Native (>= 1.0.3)     ← within-family
  └── depends on: Janset.SDL2.Core (>= 1.2.0)              ← cross-family
```

Minimum version on both axes. Image 1.0.3 was tested with Core 1.2.0, but Core 1.3.0 is also acceptable (new features, no breaking changes).

### Drift Protection Model

Within-family consistency is guaranteed at **orchestration time**, not at consumer-side resolution time. Three mechanisms combine to make mismatched within-family distribution practically impossible:

1. **Cake `PackageTask` is atomic per family.** Both managed and native nupkg for a family are emitted from a single task invocation at the same `--family-version`. There is no code path where one package is emitted at version `X` and its sibling at version `Y`.
2. **Post-pack validator** (Stream D-local) asserts that both emitted `.nupkg` files declare the same `<version>` element and that the managed nuspec contains the expected `>=` dependency on the native. Runs before any artifact leaves the build host.
3. **Release train ordering** (§2) ensures that whenever both members of a family reach the internal feed, they do so as a coherent set within the same release wave.

Consumer-side, the `>=` contract means a consumer who manually pulled mismatched family members would succeed (within SemVer compatibility) — matching industry expectations and the SkiaSharp precedent. Mismatched distribution is prevented at release time, not at consumer-side resolution.

> **Why not consumer-side exact pin?** It was investigated (2026-04-16 A0 spike) and empirically proven feasible with `PrivateAssets="all"` + bracket-notation CPM + `PackageReference`. The mechanism was retired on 2026-04-17 because it hit upstream NuGet limitations around MSBuild global-property propagation through `_GetProjectVersion` sub-evaluations (see [NuGet/Home#11617](https://github.com/NuGet/Home/issues/11617), open since 2022; [NuGet/Home#5556](https://github.com/NuGet/Home/issues/5556), open since 2017). Rather than carry a local workaround, the project adopted the industry-standard minimum-range contract. The guarantee that was surrendered (consumer-side exact pin) was already belt-and-suspenders against a scenario Cake orchestration prevents by construction.

### The Meta-Package

```text
Janset.SDL2 (meta-package, v1.0.0)
  ├── Janset.SDL2.Core (= 1.2.0)
  ├── Janset.SDL2.Image (= 1.0.3)
  ├── Janset.SDL2.Mixer (= 1.1.0)
  ├── Janset.SDL2.Ttf (= 1.0.1)
  ├── Janset.SDL2.Gfx (= 1.0.0)
  └── Janset.SDL2.Net (= 1.0.0)
```

The meta-package pins a **deterministic known-good combination** using exact version pins for every family. It has its own independent version (SemVer, monotonically increasing). The meta-package releases only as part of a full-train release — never for a targeted single-family release.

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
- Used for Source Mode and Package Validation Mode development.
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

## 7. Two Version Planes

Two distinct "version" concepts coexist in this repository. They are orthogonal, and confusing them leads to policy drift.

### Upstream Library Version Plane

The version of the native library being shipped — SDL2 2.32.10, SDL2_image 2.8.8, and so on. Tracked in `manifest.json` under `library_manifests[].vcpkg_version` (with an associated `vcpkg_port_version` for port revisions). Must match the version installed by vcpkg for the corresponding triplet. `PreFlightCheckTask` enforces this match.

### Family Version Plane

The version of a Janset.SDL2 package family — `Janset.SDL2.Core` + `Janset.SDL2.Core.Native` at version `1.2.0`, for example. Derived at pack time from git family tags (e.g., `sdl2-core-1.2.0`) via MinVer. Increments independently of the upstream library version.

### Coherence Is Cross-Validation, Not Semantic Implication

PreFlightCheck today enforces **structural coherence** between the two planes — `manifest.json library_manifests[].vcpkg_version` must agree with `vcpkg.json` for each triplet, and `manifest.json runtimes[].strategy` must be coherent with the declared triplet mapping. PreFlightCheck does **not** today enforce semantic coherence rules such as "if the upstream library major version bumped, the family major version must also bump." Such a rule may be adopted later, but this document does not commit to it. Any future adoption must be landed as an explicit amendment here.

### Example

| Plane | Value | Source of truth |
| --- | --- | --- |
| Upstream library version | SDL2 2.32.10 | `manifest.json` + `vcpkg.json` |
| Family version (core) | Janset.SDL2.Core 1.2.0 | git tag `sdl2-core-1.2.0` → MinVer |

Both are valid simultaneously. Upstream is 2.x, family is 1.x. Neither implies anything about the other beyond the structural coherence check above.

---

## Tradeoffs Explicitly Accepted

1. **Family-lock means no managed-only release.** If only binding code changed but native binaries are identical, the native package still re-releases at the new family version with unchanged binaries. This is the SkiaSharp/Avalonia tradeoff: simplicity over flexibility.

2. **Fat native package means download overhead.** Each .Native package contains all 7 RIDs (~25 MB). Consumers download all platforms even when targeting one. This is acceptable for SDL2-class library sizes and avoids the SkiaSharp 9-package explosion.

3. **One job per RID, not per library×RID.** Per-library parallelism within a RID job is sacrificed for vcpkg cache efficiency. Sequential per-library harvest within a job is fast enough (harvest is I/O, not compute).

4. **Tag-derived versions over manual versions.** Family tags are the version source. This means version numbers are never manually edited in project files. The tradeoff: version bumps require git tag discipline, not file edits. Build system orchestration ensures this is reliable. (*Current tooling consideration: MinVer with `<MinVerTagPrefix>` per family.*)

5. **Minimum range within family, drift-prevented at orchestration time.** Within-family and cross-family dependency contracts both use `>=`, matching SkiaSharp / Avalonia / OpenTelemetry conventions. Within-family exact pin was investigated pre-S1 (2026-04-16 A0 spike) and proven mechanically feasible, but the approach depended on MSBuild global-property propagation through NuGet's pack-time `_GetProjectVersion` sub-evaluation — which `NuGet.Build.Tasks.Pack.targets` implements with `Properties=` (globals-replace) rather than `AdditionalProperties=` (globals-extend). That behavior has been unchanged for 8+ years and is unchanged in .NET 10 SDK; no merged or in-flight fix exists upstream. Rather than carry a local workaround, drift protection moved to the orchestration layer (§4 Drift Protection Model). The theoretical consumer-side exact-pin guarantee is traded for operational simplicity that every major .NET native-bundled library has already accepted.
