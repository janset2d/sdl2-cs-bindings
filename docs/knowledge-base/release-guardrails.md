# Release Guardrails

> Single map of every guardrail this project commits to land. Each entry has an owning stage and a current implementation status. When a guardrail moves from "planned" to "active," update its row here in the same change.

## 1. Guardrail Philosophy

1. **Strict by default.** Every guardrail hard-fails when violated. Bypasses (when they exist) require explicit operator action and loud logging.
2. **Defense-in-depth.** Each invariant is checked at multiple layers when feasible — structural (PreFlight), build-time (MSBuild target), pack-time (post-pack assertion), publish-time (CI gate). One missed check is rarely the only check.
3. **Catch as early as possible.** A guardrail that fires at PreFlight is preferable to one that fires at Pack, which is preferable to one that fires at Publish, which is preferable to one that fires at Consumer.
4. **No silent skips.** If a guardrail is conditionally bypassed (e.g., `AllowEmptyNativePayload=true` for G46 — the only documented pack-time bypass), the bypass is logged loudly with the override reason.
5. **Mirror the failure to its source.** Error messages name the file, line, property, and the canonical rule that was violated, so the operator can fix without re-reading the code.

## 2. Guardrail Inventory

### 2.0 Stage-Owned Validation Mapping

Every guardrail has one owning pipeline stage. This view organizes the same set listed in §2.1–§2.8 by the stage that owns enforcement; explicit mirrors are called out in the row text.

| Stage | Runs | Guardrails | Coverage |
| --- | --- | --- | --- |
| **PreFlight** | Single-runner, fail-fast before matrix work | G4, G6, G7, G14, G15, G16, G17, G18, G49, G54, G58 (mirror), G59 | Structural csproj contract + manifest↔vcpkg coherence + hybrid-static overlay coherence + core identity + manifest family-name invariant + upstream major.minor alignment + cross-family dependency scope reachability mirror |
| **Harvest** | Per-RID matrix | G19, G50 | Hybrid-static transitive leak detection + primary binary ≥ 1 post-deploy assertion |
| **NativeSmoke** | Per-RID matrix | — | C/C++ harness (`tests/smoke-tests/native-smoke/`); no G-series numbering yet. Proves native binaries load + initialize at OS level. |
| **Pack** | Single-runner after Consolidate | G13 (NuGet schema), G21, G22, G23, G25, G26, G27, G46 (MSBuild pre-pack), G47, G48, G51, G52, G53, G55, G56, G57, G58 | nupkg emission + post-pack shape (minimum-range + version-match + TFM consistency + symbols + metadata + `buildTransitive/` contract + per-RID payload shape + license payload + staged-replace invariants + native metadata file + cross-family upper bound + README mapping + cross-family dep resolvability) |
| **ConsumerSmoke** | Per-RID matrix re-entry | — | Restore + runtime TUnit pass per TFM; behavioral, not structural. |
| **Publish** | Single-runner per feed tier | G31, G32, G33, G34, G35 | Monotonicity + no-existing-version + smoke-gate + stage-ordering + cross-family resolvability at feed scope |
| **Full-Train (PD-7 scope)** | Meta-tag trigger | G37, G38, G39, G40, G41, G42 | Manifest-driven family selection + family-tag SemVer/G54 + no-dup + ordering + partial-train handling |
| **Manual Escape (PD-8 scope)** | Operator-driven | G43, G44, G45 | Version↔tag drift + explicit feed source + audit trail |

The subsystem view (§2.1–§2.8) remains authoritative for each guardrail's owner (validator class, task, MSBuild target, CI workflow step). This stage view is a routing map; it references the same guardrails, not duplicates.

### 2.1 csproj Structural (PreFlight)

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G4 | csproj `<MinVerTagPrefix>` equals `manifest.json package_families[].tag_prefix + "-"` | Active | `CsprojPackContractValidator` |
| G6 | csproj `<PackageId>` follows canonical pattern `Janset.SDL<Major>.<Role>` (managed) or `Janset.SDL<Major>.<Role>.Native` (native) | Active | `CsprojPackContractValidator` |
| G7 | Native `<ProjectReference>` path resolves to `manifest.json package_families[].native_project` | Active | `CsprojPackContractValidator` |

### 2.2 MSBuild Build-Time

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G46 | Pack of a `Janset.SDL2.*.Native` package fails if `$(NativePayloadSource)` is unset (native csproj has no source-tree fallback; empty payload would ship otherwise). Bypass `-p:AllowEmptyNativePayload=true` for deliberate empty packs. | Active | `_GuardAgainstEmptyNativePayload` in `src/Directory.Build.targets` |

### 2.3 Restore + Pack (NuGet built-in)

NuGet enforces some invariants we rely on but don't author. Tracked here so we know what NOT to re-implement.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G12 | CPM violation: every `PackageReference` must have a matching `PackageVersion` | Active | NuGet built-in |
| G13 | Manifest schema validity (JSON parseable, required fields present) | Active | `JsonSerializer.Deserialize<ManifestConfig>` |

### 2.4 Manifest + vcpkg Coherence (PreFlight)

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G14 | `manifest.json library_manifests[].vcpkg_version` equals `vcpkg.json` override | Active | `VersionConsistencyValidator` |
| G15 | `manifest.json library_manifests[].vcpkg_port_version` equals `vcpkg.json` port version | Active | `VersionConsistencyValidator` |
| G16 | `manifest.json runtimes[].triplet` is a hybrid overlay triplet (`-hybrid` suffix, overlay `.cmake` exists in `vcpkg-overlay-triplets/`) | Active | `HybridStaticOverlayValidator` |
| G17 | `package_families[].depends_on` references existing family identifiers | Active | `CsprojPackContractValidator` (cross-section check) |
| G18 | `package_families[].library_ref` references existing `library_manifests[].name` | Active | `CsprojPackContractValidator` (cross-section check) |
| G19 | Hybrid-static strategy: zero transitive dep leaks in harvest output | Active (Harvest stage) | `HybridStaticValidator` |
| G49 | Core-library identity coherence: `library_manifests[core_lib=true].vcpkg_name` equals `packaging_config.core_library` (case-insensitive); exactly one `core_lib=true` entry declared | Active | `CoreLibraryIdentityValidator` |
| G50 | Harvest must produce ≥1 primary binary per library+RID: `HarvestTask` post-deployment assertion fails fast if `DeploymentStatistics.PrimaryFiles.Count == 0`. Defends against silent feature-flag degradation / partial vcpkg install shapes that pass upstream guards. | Active (Harvest stage) | `HarvestTask` post-deploy assertion |
| G54 | **Family tag UpstreamMajor.UpstreamMinor ↔ manifest coherence (D-3seg anchor).** The family tag's first two SemVer segments MUST equal `manifest.json library_manifests[].vcpkg_version`'s first two segments for that family. Example: tag `sdl2-core-2.32.0` at a commit where `manifest.json` declares SDL2 at `2.32.10` → G54 passes (both anchor to `2.32`). A tag `sdl2-core-2.31.0` at the same commit fails. | Active | `UpstreamVersionAlignmentValidator` (PreFlight) |
| G59 | **Manifest family name invariant.** Every `package_families[].name` matches `^sdl[0-9]+-[a-z][a-z0-9-]*$` (lowercase kebab, e.g. `sdl2-core`, `sdl2-image`). Hand-edited mixed-case entries (`SDL2-Core`) silently bypass `PackageFamilyId` ordinal-exact lookups in downstream consumers; this invariant catches the drift at PreFlight time before any build operation runs. | Active | `ManifestFamilyNameInvariantValidator` (PreFlight) |

### 2.5 Post-Pack nuspec Assertion (Pack stage)

These guardrails open the produced `.nupkg` and assert the emitted nuspec is correct. Defense-in-depth layer that catches anything the structural + MSBuild layers missed.

G25 is intentionally scoped to the managed package's `.snupkg`. Payload-only `.Native` projects currently disable symbol-package generation by design.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G21 | Within-family Native dependency emitted as bare minimum range `x.y.z` (no brackets). Cross-family dependencies preserve `>= x.y.z` lower-bound semantics (upper-bound enforcement owned by G56). Within-family Native dependency must not exclude build assets, else `.NET Framework` consumers lose the native `buildTransitive` copy targets. | Active | Cake `PackageTask` post-pack assertion |
| G22 | All TFM dependency groups (net10.0, net9.0, net8.0, netstandard2.0, net462) are consistent with each other | Active | Cake `PackageTask` post-pack assertion |
| G23 | **Primary within-family coherence check.** Native package's `<version>` matches the managed package's `<version>` byte-for-byte. Detects drift between family members that would make the minimum-range contract misleading. | Active | Cake `PackageTask` post-pack assertion |
| G25 | Managed symbol package (.snupkg) is present and valid | Active | Cake `PackageTask` post-pack assertion |
| G26 | Nuspec `<repository>` element points at expected commit SHA | Active | Cake `PackageTask` post-pack assertion |
| G27 | Nuspec metadata fields (id, authors, license, icon) match expected values | Active | Cake `PackageTask` post-pack assertion |
| G47 | Native package ships the consumer-side buildTransitive contract — both `buildTransitive/$(PackageId).targets` (thin wrapper) and `buildTransitive/Janset.SDL2.Native.Common.targets` (shared extraction + .NETFramework AnyCPU copy). Missing either entry leaves Linux/macOS consumers without the `tar -xzf` extraction step (DllNotFoundException at first P/Invoke) and .NETFramework AnyCPU consumers without the per-RID DLL copy. | Active | Cake `PackageTask` post-pack assertion |
| G48 | For every `runtimes/<rid>/native/` subtree: Windows RIDs ship one or more `*.dll` files with no tarball; Unix RIDs ship exactly one `$(PackageId).tar.gz` (per-package rename prevents filename collision when the SDK flattens RID subtrees into the consumer's `$(OutDir)`). | Active | Cake `PackageTask` post-pack assertion |
| G51 | Native `.nupkg` ships at least one entry under `licenses/`. Compliance surface — a nupkg missing license attribution is a release-blocking defect. | Active | Cake `PackageTask` post-pack assertion (`PackageOutputValidator.EvaluateLicensePayloadPresence`) |
| G52 | Pack pre-pack payload gate checks `runtimes/` AND `licenses/_consolidated/`, not the top-level `licenses/` parent. Tightens against false-green where receipt + per-RID evidence under `licenses/<rid>/` would pass while the pack-time include pattern (`licenses/_consolidated/**/*`) shipped an empty payload. | Active | Cake `PackageTask` pre-pack assertion (`PackageTaskRunner.EnsureHarvestOutputReadyAsync` via `PayloadDirectories`) |
| G53 | ConsolidateHarvest staged-replace invariant — Phase 1 writes to `_consolidated.tmp/` + `harvest-manifest.tmp.json` + `harvest-summary.tmp.json`; Phase 2 deletes old artifacts and moves tmp → final. If Phase 1 fails the old valid state survives. Per-library exceptions aggregated and the task fails fatally — silent license drops would create false-green compliance. Malformed `rid-status/*.json` is also fatal, never warning-only skip. | Active | Cake `ConsolidateHarvestTask` + `HarvestTask.InvalidateCrossRidReceipts` tmp orphan cleanup |
| G55 | **Native package ships `janset-native-metadata.json` at root.** Schema: `{ janset_family_version, family_identifier, upstream_library, upstream_version, vcpkg_port_version, triplet_set, build_commit }`. Validator opens the nupkg, asserts file exists + parses + schema-matches + `upstream_version` equals vcpkg-resolved version + `build_commit` equals HEAD SHA. Carries the exact upstream patch + port_version that the D-3seg version string drops. | Active | `NativePackageMetadataValidator` (post-pack) |
| G56 | **Satellite cross-family dep upper bound declared.** Every satellite cross-family dependency on Core declares both lower bound (`>= x.y.z`) AND upper bound (`< (UpstreamMajor + 1).0.0`). Parses the exact range expression — not just "dependency present." Prevents accidental consumer-side resolution across SDL upstream majors. | Active | `SatelliteUpperBoundValidator` (post-pack) |
| G57 | **README mapping table current.** `README.md` mapping block delimited by `<!-- JANSET:MAPPING-TABLE-START -->` / `<!-- JANSET:MAPPING-TABLE-END -->` markers matches the current `manifest.json library_manifests[]` set byte-equivalent to the Cake-generator output. | Active | `ReadmeMappingTableValidator` (post-pack) |
| G58 | **Cross-family dependency resolvability.** If the resolved version mapping contains a satellite family, each declared cross-family dependency must also be present in the same mapping. Active scope-contains check in Pack and mirrored in PreFlight, so a satellite pack cannot emit a lower-bound Core dependency that the current invocation cannot resolve. Feed probing remains a deferred relaxation surface (gap #2 in §4.1). The deferred feed-probe queries the **publish target feed** (GH Packages for tag-push staging, nuget.org for the future PD-7 path). Complements G56 (upper bound declared in nuspec) + G35 (Publish-stage cross-family monotonicity). | Active | `G58CrossFamilyDepResolvabilityValidator` via `PreflightTaskRunner` + `PackageTaskRunner` |

### 2.6 CI Pipeline (planned)

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G28 | Tag pushed matches format `sdl<major>-<role>-<semver>` | Planned | CI workflow regex |
| G29 | Tag's family identifier exists in `manifest.json package_families[]` | Planned | CI / Cake `ValidateTask` |
| G30 | Tag's SemVer parses cleanly (no malformed pre-release suffix etc.) | Planned | CI / Cake (NuGet.Versioning) |
| G31 | Tag's version is strictly greater than the latest published version on internal feed (monotonicity) | Planned | Cake `ValidateTask` (queries internal feed) |
| G32 | NuGet.org "version already exists" check before public push | Planned | CI workflow / Cake |
| G33 | Smoke test must pass before publish (explicit `needs:` gate) | Planned | CI workflow |
| G34 | Internal feed publish completes before public promote | Planned | Promotion workflow |
| G35 | Cross-family coherence: satellite family's Core minimum version is `<= currently-published Core version` (satellite cannot demand unreleased Core) | Planned | Cake `ValidateTask` |

### 2.7 Full-Train Meta-Tag Validation (PD-7 scope)

The current PD-7 full-train design selects manifest-driven train composition: `manifest.json package_families[].depends_on` supplies ordering, family tags at the invocation commit supply versions, and no separate `release-set.json` is planned.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G37 | Train/meta tag resolves only concrete package families from `manifest.json` | Planned | `GitTagVersionProvider` + PreFlight |
| G38 | Required family tags exist at the invocation commit | Planned | `GitTagVersionProvider` |
| G39 | Every family-tag version parses as SemVer and passes G54 upstream alignment | Planned | `GitTagVersionProvider` + G54 |
| G40 | Manifest family identifiers and tag prefixes are unique before train resolution | Planned | PreFlight |
| G41 | Release ordering follows `manifest.json package_families[].depends_on` | Planned | `FamilyTopologyHelpers` |
| G42 | All train families either succeed end-to-end or the train is marked partial with recovery guidance | Planned | CI workflow + recovery playbook |

### 2.8 Manual Escape Hatch (PD-8 scope)

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G43 | Manual pack operator-typed family version matches the family tag (no version drift between hand-typed CLI and intended tag) | Planned | Cake `Pack-Family` validates against git tag if present |
| G44 | Manual push requires explicit feed source identification (no implicit defaults that ship to public) | Planned | Cake `Push-Family --source=` is required |
| G45 | Manual release records audit trail (who, what, when, hash) | Planned | Cake `Push-Family` writes audit JSON |

## 3. Failure Mode Catalog

For each known failure mode, list the guardrails that catch it. If no guardrail catches it, that's a gap to fill.

| Failure mode | Guardrails | Gap? |
| --- | --- | --- |
| MinVer tag prefix in csproj drifts from manifest | G4 | No |
| Operator creates new family in manifest but doesn't add csprojs | G7 (path doesn't resolve) | No |
| Csproj `<PackageId>` deviates from canonical convention | G6 | No |
| Operator passes wrong family version flag at CLI (managed and native end up at different versions) | G23 | No |
| Family dependency accidentally emitted as exact-pin bracket notation | G21 | No |
| Missing TFM group in dependency emission | G22 | No |
| Managed and native shipped at mismatched versions (Cake orchestration bug) | G23 | No |
| Republishing same version (overwrite attempt) | G31 (monotonicity), G32 (existing-version check) | No |
| Satellite published referencing unreleased Core version | G35 | No |
| Tag pushed in wrong format (e.g., `sdl2-image-1.3` missing patch) | G28, G30 | No |
| Tag pushed for non-existent family (typo) | G29 | No |
| Smoke test fails silently and publish proceeds | G33 (explicit gate) | No |
| Public-feed push without internal staging | G34 | No |
| Manual escape: operator typos version in CLI | G43 | No (after PD-8) |
| Manual escape: operator publishes to wrong feed | G44 | No (after PD-8) |
| Full-train: core fails but satellites continue | G41 | No (after PD-7) |
| vcpkg.json drifts from manifest | G14 | No |
| Runtime triplet is not a hybrid overlay triplet | G16 | No |
| Hybrid-static build leaks transitive deps | G19 | No |
| Operator generates package with wrong nuspec metadata | G27 | No |
| Managed symbol package missing | G25 | No |
| Build artifact contains wrong commit SHA | G26 | No |
| Direct `dotnet pack` of a `.Native` csproj without Cake ships empty runtimes/licenses payload | G46 | No |
| Family tag's Major.Minor drifts from upstream `vcpkg_version` Major.Minor | G54 | No |
| Native nupkg ships without machine-readable upstream-version metadata | G55 | No |
| Satellite nuspec missing cross-family upper bound | G56 | No |
| README mapping table drifts from manifest | G57 | No |
| Satellite pack invocation emits nuspec declaring `>= Core x.y.z` while Core is missing from the resolved invocation scope (consumer-side restore would fail unless a later feed-probe relaxation proves it already exists on the target feed) | G58 | No |
| Hand-edited manifest family name with uppercase / underscore / non-canonical kebab (`SDL2-Core`, `sdl2_core`, `sdlx-core`) silently bypasses `PackageFamilyId` ordinal-exact lookups downstream | G59 | No |

**Net:** every cataloged failure mode is caught by at least one guardrail before the package reaches public consumers. Operational gaps (not structural) are documented in §4.1.

## 4. Operational Principles

1. **PreFlight is the single CI gate** that runs before any matrix work. If PreFlight fails, no resources are spent on builds that would fail downstream anyway.
2. **Multi-layer for the critical invariants.** Within-family version coherence is checked at the orchestration layer (Cake atomic `PackageTask` packs both family members at the same per-family entry of the `--explicit-version <family>=<semver>` mapping in one invocation) AND at the post-pack layer (G23 asserts the emitted `<version>` elements match byte-for-byte). Two independent layers; either alone would be sufficient, together they are defense-in-depth.
3. **Bypass requires explicit, loud opt-in.** `-p:AllowEmptyNativePayload=true` (G46 bypass) is the only documented pack-time bypass. It produces a banner-level warning. No silent escape hatches.
4. **New invariants land WITH their guardrail** — never as "we'll add the check later." The gap between "rule exists" and "rule enforced" is the rot zone.
5. **Cross-cutting checks live in PreFlight.** Per-package or per-pack-output checks live in their respective stage's task. Don't mix layers.

### 4.1 Pipeline Scope-Assumption Gaps — Operational Implications

The 2026-05-01 tag-push rehearsals traced trigger-aware routing end-to-end and surfaced four gaps where the pipeline assumed full ecosystem coverage rather than honoring the resolved scope. One was fixed in `437edff` and stays here only as the historical anchor for the pattern.

| # | Stage | Gap | Status |
| --- | --- | --- | --- |
| 1 | `Resolve Versions` `--scope` filter | Provider's `FilterByRequestedScope` rejected full-tag scope values like `sdl2-image-2.8.0` because it expected family-id keys only | **Fixed** in `437edff` (`EmptyRequestedScope` from `ResolveFromGitTagAsync`) |
| 2 | `PreFlight` + `Pack` G58 cross-family resolvability | Scope-contains check only; satellite without core in same scope blocks at G58 even when core is published on the target feed | **Open** — feed-probe deferred relaxation surface |
| 3 | `PackageConsumerSmoke` runner | `EnsureSelectionSupportsCurrentSmokeScope` enforces "all manifest-concrete families OR none" against the `--explicit-version` mapping; partial scope rejected before any `dotnet restore` runs | **Open** — partial-scope smoke not yet supported |
| 4 | `release.yml` tag-trigger fan-out | `on.push.tags` filter fires one workflow run per pushed tag. A train release requires N+1 tags atomically (N family tags + the `train-*` tag); GitHub creates N+1 separate workflow runs of which only `train-*` is the desired release. | **Open** — trigger mechanism under reconsideration |

**Operational consequence today.** The only release shapes that pass the full pipeline are those that include every concrete family in the resolved scope — train tag, multi-family explicit dispatch with all 5 families, and manifest-derived dispatch. Targeted single-family or arbitrary-subset releases block at gap #2 (satellites) or gap #3 (any partial scope including core-only).

Until gaps #2 and #3 close, the canonical "core first, then satellites independently" release ordering is operationally enforced as "all families together." Gap #4 is **separate**: closing #2 + #3 enables partial-scope content-wise, but #4 is about the **trigger mechanism**. Even with #2 + #3 closed, per-tag workflow fan-out makes train release via tag-push impractical.

#### Candidate directions (research + decision required, not final)

For gap #3, current leaning (2026-05-01) is **per-library / per-family smoke csprojs** rather than the existing single multi-family csproj — each family carries its own consumer smoke project; the runner invokes only the smoke projects whose families are in scope. Same direction may apply to native smoke. Tradeoffs to evaluate: file count vs scope isolation; manifest-csproj drift catchnet shape; whether per-family smoke covers the cross-family interaction surface that the current 5-family csproj exercises.

For gap #2, the deferred slice queries the **publish target feed** for the satellite's invocation — GH Packages staging for tag-push, nuget.org for the future PD-7 public promotion path. NuGet semver ordering matters: a CI prerelease `2.32.0-ci.<run-id>` does NOT satisfy `>= 2.32.0` because prerelease versions come before their base release in NuGet ordering, so the satellite's lower bound effectively requires a stable (or stable-comparable) core version on the target feed.

For gap #4, two candidate replacements: (a) **manual `workflow_dispatch` as canonical**, with tags becoming audit-trail-only records created after a successful release; (b) **GitHub Releases as trigger source**, with the release body parsed for family-version mapping and `release: published` firing one workflow run regardless of tag count. Both preserve the existing governance policy while breaking per-tag fan-out.

All four gaps are PD-7 adjacent. The first nuget.org prerelease publish ([#63](https://github.com/janset2d/sdl2-cs-bindings/issues/63)) — likely targeting a single family at first — needs at least gaps #2 and #3 closed for the release shape to be operationally complete; gap #4 needs a deliberate decision before the public-promotion path is locked.

## 5. Adding a New Guardrail

When a new failure mode emerges:

1. **Catalog the failure mode** in §3 with current state ("Gap?" = Yes).
2. **Pick the layer** — the earliest layer that has visibility into the inputs needed.
3. **Pick the owner** — which task / target / workflow runs the check.
4. **Add a row** to the appropriate §2 subsection with status = "Planned" and owner.
5. **Implement the check.** PR includes the test that demonstrates the failure mode is caught.
6. **Promote to "Active"** in §2 + flip the §3 row to "No (Gap closed)".

## 6. Cross-References

- [`plan.md`](../plan.md) — current roadmap; PD-7 / PD-8 work tracked there.
- [`phases/phase-2-adaptation-plan.md`](../phases/phase-2-adaptation-plan.md) — Phase 2b execution ledger; gap detail + candidate directions.
- `.github/workflows/release.yml` — live CI pipeline.
- `build/_build/Targets/{PreFlightCheck,Package}/` and `build/_build/Validation/` — guardrail implementations.
- `src/Directory.Build.targets` — current MSBuild guard implementation.
