# Release Guardrails Roadmap

> **Status:** Canonical — this doc is the single map of every guardrail the project commits to land. Every entry has an owning stream and a current implementation status. When a guardrail moves from "planned" to "active," update its row here in the same change.
>
> **Last updated:** 2026-04-16
>
> **Why a dedicated roadmap:** Defense-in-depth is a hard requirement for this project. A single missed inconsistency between manifest, csproj, vcpkg, family tag, and produced nupkg can ship a broken package family. Guardrails are scattered across multiple subsystems (PreFlight, MSBuild, Cake, CI workflows, post-pack assertions, feed checks). This doc enumerates them all, deduplicates, and maps each to its owning stream so nothing is forgotten in the gap between A-risky and the public release of v1.0.

## 1. Guardrail Philosophy

1. **Strict by default.** Every guardrail hard-fails when violated. Bypasses (when they exist) require explicit operator action and loud logging.
2. **Defense-in-depth.** Each invariant is checked at multiple layers when feasible — structural (PreFlight), build-time (MSBuild target), pack-time (post-pack assertion), publish-time (CI gate). One missed check is rarely the only check.
3. **Catch as early as possible.** A guardrail that fires at PreFlight is preferable to one that fires at Pack, which is preferable to one that fires at Publish, which is preferable to one that fires at Consumer.
4. **No silent skips.** If a guardrail is conditionally bypassed (e.g., `AllowSentinelExactPin=true`), the bypass is logged loudly with the override reason.
5. **Mirror the failure to its source.** Error messages name the file, line, property, and the canonical rule that was violated, so the operator can fix without re-reading the code.

## 2. Guardrail Inventory

### 2.1 csproj Structural (PreFlight, A-risky scope)

These guardrails run as part of `PreFlightCheckTask` and are the first line of defense. They check that csproj files conform to the canonical shape established by [release-lifecycle-direction.md §1](release-lifecycle-direction.md) and the A0 spike mechanism.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G1 | Each managed satellite csproj's Native `<ProjectReference>` carries `PrivateAssets="all"` | Planned (A-risky step 3) | `CsprojPackContractValidator` |
| G2 | Each managed satellite csproj has matching `<PackageReference>` to the Native PackageId | Planned (A-risky step 3) | `CsprojPackContractValidator` |
| G3 | Each managed satellite csproj has matching `<PackageVersion>` for the Native dep with bracket notation `[...]` | Planned (A-risky step 3) | `CsprojPackContractValidator` |
| G4 | csproj `<MinVerTagPrefix>` equals `manifest.json package_families[].tag_prefix + "-"` | Planned (A-risky step 3) | `CsprojPackContractValidator` |
| G5 | csproj family-version property name follows canonical pattern `Sdl<Major><Role>FamilyVersion` | Planned (A-risky step 3) | `CsprojPackContractValidator` |
| G6 | csproj `<PackageId>` follows canonical pattern `Janset.SDL<Major>.<Role>` (managed) or `Janset.SDL<Major>.<Role>.Native` (native) | Planned (A-risky step 3) | `CsprojPackContractValidator` |
| G7 | Native `<ProjectReference>` path resolves to `manifest.json package_families[].native_project` | Planned (A-risky step 3) | `CsprojPackContractValidator` |
| G8 | Each managed csproj's family-version property defaults to `$(Version)` with `0.0.0-restore` sentinel fallback | Planned (A-risky step 3) | `CsprojPackContractValidator` |

### 2.2 MSBuild Build-Time (Directory.Build.targets, A-risky scope)

These guardrails run as MSBuild targets during `dotnet build` / `dotnet pack` invocation. They catch issues that PreFlight cannot see (runtime-resolved property values).

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G9 | Pack of a `Janset.SDL2.*` managed package fails if any family-version property still equals `0.0.0-restore` sentinel at pack time | Active (A-risky step 2) | `_GuardAgainstShippingRestoreSentinel` in [src/Directory.Build.targets](../../src/Directory.Build.targets) |
| G10 | Bypass `-p:AllowSentinelExactPin=true` produces loud warning banner | Planned (A-risky polish) | `_GuardAgainstShippingRestoreSentinel` |

### 2.3 Restore + Pack (NuGet built-in)

NuGet itself enforces some invariants we rely on but don't author. Tracked here for completeness so we know what NOT to re-implement.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G11 | Restore-time and pack-time version mismatch between bracket-notation `PackageVersion` and resolved project version produces NU5016 ("empty version range") | Active (NuGet built-in) | NuGet `Pack.targets` |
| G12 | CPM violation: every `PackageReference` must have a matching `PackageVersion` | Active (NuGet built-in) | NuGet `CPM` validation |
| G13 | Manifest schema validity (JSON parseable, required fields present) | Active (build host) | `JsonSerializer.Deserialize<ManifestConfig>` |

### 2.4 Manifest + vcpkg Coherence (PreFlight, B closed)

Already-active guardrails from Stream B. Listed for completeness.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G14 | `manifest.json library_manifests[].vcpkg_version` equals `vcpkg.json` override | Active | `VersionConsistencyValidator` |
| G15 | `manifest.json library_manifests[].vcpkg_port_version` equals `vcpkg.json` port version | Active | `VersionConsistencyValidator` |
| G16 | `manifest.json runtimes[].strategy` is coherent with the declared triplet | Active | `StrategyCoherenceValidator` |
| G17 | `package_families[].depends_on` references existing family identifiers | Planned (A-risky step 3 polish) | `CsprojPackContractValidator` (cross-section check) |
| G18 | `package_families[].library_ref` references existing `library_manifests[].name` | Planned (A-risky step 3 polish) | `CsprojPackContractValidator` (cross-section check) |
| G19 | Hybrid-static strategy: zero transitive dep leaks in harvest output | Active (B) | `HybridStaticValidator` |

### 2.5 Post-Pack nuspec Assertion (Stream D-local scope)

These guardrails run AFTER `dotnet pack` completes, opening the produced `.nupkg` and asserting the emitted nuspec is correct. This is the defense-in-depth layer that catches anything the structural + MSBuild layers missed.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G20 | Within-family Native dependency emitted as exact range `[x.y.z]` (bracket notation) | Planned (D-local) | Cake `PackageTask` post-pack assertion |
| G21 | Cross-family Core dependency emitted as bare minimum range `x.y.z` (no brackets) | Planned (D-local) | Cake `PackageTask` post-pack assertion |
| G22 | All TFM dependency groups (net9.0, net8.0, netstandard2.0, net462) are consistent with each other | Planned (D-local) | Cake `PackageTask` post-pack assertion |
| G23 | Native package `<version>` matches managed package `<version>` (within-family pair coherence) | Planned (D-local) | Cake `PackageTask` post-pack assertion |
| G24 | Nuspec contains no `0.0.0-restore` sentinel anywhere (catch leak past G9) | Planned (D-local) | Cake `PackageTask` post-pack assertion |
| G25 | Symbol package (.snupkg) is present and valid | Planned (D-local) | Cake `PackageTask` post-pack assertion |
| G26 | Nuspec `<repository>` element points at expected commit SHA | Planned (D-local) | Cake `PackageTask` post-pack assertion |
| G27 | Nuspec metadata fields (id, authors, license, icon) match expected values | Planned (D-local) | Cake `PackageTask` post-pack assertion |

### 2.6 CI Pipeline (Stream C + D-ci scope)

These guardrails are CI-workflow-level checks that run before any publish action.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G28 | Tag pushed matches format `sdl<major>-<role>-<semver>` | Planned (D-ci) | CI workflow regex |
| G29 | Tag's family identifier exists in `manifest.json package_families[]` | Planned (D-ci) | CI / Cake `ValidateTask` |
| G30 | Tag's SemVer parses cleanly (no malformed pre-release suffix etc.) | Planned (D-ci) | CI / Cake (NuGet.Versioning) |
| G31 | Tag's version is strictly greater than the latest published version on internal feed (monotonicity) | Planned (D-ci) | Cake `ValidateTask` (queries internal feed) |
| G32 | NuGet.org "version already exists" check before public push (prevents accidental re-publish) | Planned (D-ci) | CI workflow / Cake |
| G33 | Smoke test must pass before publish (explicit `needs:` gate) | Planned (D-ci) | CI workflow |
| G34 | Internal feed publish completes before public promote (no skipping stages) | Planned (D-ci) | Promotion workflow |
| G35 | Cross-family coherence: satellite family's Core minimum version is `<= currently-published Core version` (satellite cannot demand unreleased Core) | Planned (D-ci) | Cake `ValidateTask` |
| G36 | Coverage ratchet floor maintained (`build/coverage-baseline.json`) | Active locally (#86); CI wiring planned (Stream C) | `Coverage-Check` Cake task |

### 2.7 Full-Train Release-Set Validation (PD-7 scope)

When PD-7 lands, the meta-tag + `release-set.json` workflow needs its own guardrails.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G37 | `release-set.json` JSON schema validity | Planned (PD-7 → D-ci) | Cake `TrainValidateTask` |
| G38 | Every family identifier in `release-set.json` exists in `manifest.json` | Planned (PD-7 → D-ci) | Cake `TrainValidateTask` |
| G39 | Every version in `release-set.json` parses as SemVer | Planned (PD-7 → D-ci) | Cake `TrainValidateTask` |
| G40 | No duplicate family entries in `release-set.json` | Planned (PD-7 → D-ci) | Cake `TrainValidateTask` |
| G41 | Release ordering enforced: core completes before satellites start | Planned (PD-7 → D-ci) | CI workflow `needs:` graph |
| G42 | All families in `release-set.json` either succeed end-to-end or train is marked partial | Planned (PD-7 → D-ci) | CI workflow + recovery playbook |

### 2.8 Manual Escape Hatch (PD-8 scope)

When PD-8 lands, the manual operator flow needs its own guardrails.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G43 | Manual pack operator-typed family version matches the family tag (no version drift between hand-typed CLI and intended tag) | Planned (PD-8 → D-local) | Cake `Pack-Family` validates against git tag if present |
| G44 | Manual push requires explicit feed source identification (no implicit defaults that ship to public) | Planned (PD-8 → D-local) | Cake `Push-Family --source=` is required |
| G45 | Manual release records audit trail (who, what, when, hash) | Planned (PD-8 → D-local) | Cake `Push-Family` writes audit JSON |

## 3. Stream Mapping (At-a-Glance)

| Stream | Guardrails delivered | Cumulative active |
| --- | --- | --- |
| B (closed) | G14, G15, G16, G19, G36 (local) | 5 |
| A-risky (current) | G1–G8 (PreFlight csproj), G9, G10 (MSBuild guard), G17, G18 (cross-section) | 17 |
| C (CI modernization) | G36 (CI gate wiring) | 17 |
| D-local | G20–G27 (post-pack assertion) | 25 |
| D-ci | G28–G35 (CI publish pipeline) | 33 |
| PD-7 (full-train) | G37–G42 (release-set validation) | 39 |
| PD-8 (manual escape) | G43–G45 (manual operator validation) | 42 |

## 4. Failure Mode Catalog

For each known failure mode, list the guardrails that catch it. If no guardrail catches it, that's a gap to fill.

| Failure mode | Guardrails | Gap? |
| --- | --- | --- |
| Operator removes `PrivateAssets="all"` from Native ProjectReference | G1 | No |
| Operator adds new managed satellite csproj without bracket-notation PackageVersion | G3 | No |
| MinVer tag prefix in csproj drifts from manifest (e.g., csproj says `core-` but manifest says `sdl2-core-`) | G4 | No |
| Renaming family in manifest without updating csproj property names | G5 | No |
| Operator creates new family in manifest but doesn't add csprojs | G7 (path doesn't resolve) | No |
| Standalone `dotnet pack` ships sentinel `0.0.0-restore` as native dep | G9 (build-time block), G24 (post-pack catch) | No |
| Operator passes wrong family version flag at CLI | G11 (NU5016 NuGet built-in), G23 (post-pack version match) | No |
| Cross-family dep accidentally emitted as exact pin instead of minimum range | G21 | No |
| Missing TFM group in dependency emission | G22 | No |
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
| Strategy / triplet incoherent | G16 | No |
| Hybrid-static build leaks transitive deps | G19 | No |
| Coverage drops below floor | G36 | No |
| Operator generates package with wrong nuspec metadata (e.g., wrong author) | G27 | No |
| Symbol package missing | G25 | No |
| Build artifact contains wrong commit SHA | G26 | No |

**Net:** with the full guardrail roadmap landed, every cataloged failure mode is caught by at least one guardrail before the package reaches public consumers. If a future failure mode is discovered without a covering guardrail, add a new row here AND a new guardrail to fill the gap.

## 5. Operational Principles

1. **PreFlight is the single CI gate** that runs before any matrix work. If PreFlight fails, no resources are spent on builds that would fail downstream anyway.
2. **Multi-layer for the critical invariants.** Within-family exact pin (the hardest-won A0 mechanism) is checked at G1+G2+G3 (structural), G9 (build-time), G20+G24 (post-pack). Three independent layers, each catches a different failure class.
3. **Bypass requires explicit, loud opt-in.** `-p:AllowSentinelExactPin=true` is the only documented bypass and it produces banner-level warning. No silent escape hatches.
4. **New invariants land WITH their guardrail** — never as "we'll add the check later." The gap between "rule exists" and "rule enforced" is the rot zone.
5. **Cross-cutting checks live in PreFlight.** Per-package or per-pack-output checks live in their respective stream's task. Don't mix layers.

## 6. Adding a New Guardrail

When a new failure mode emerges:

1. **Catalog the failure mode** in §4 with current state ("Gap?" = Yes).
2. **Pick the layer** — the earliest layer that has visibility into the inputs needed.
3. **Pick the owner** — which task / target / workflow runs the check.
4. **Add a row** to the appropriate §2 subsection with status = "Planned" and owner.
5. **Implement the check.** PR includes the test that demonstrates the failure mode is caught.
6. **Promote to "Active"** in §2 + flip the §4 row to "No (Gap closed)".
7. **Update the §3 Cumulative count.**

## 7. Cross-References

- [release-lifecycle-direction.md](release-lifecycle-direction.md) — canonical policy that the guardrails enforce
- [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md) — stream-by-stream implementation roadmap
- [exact-pin-spike-and-nugetizer-eval-2026-04-16.md](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) — within-family exact-pin mechanism + the production-time version flow constraint
- [full-train-release-orchestration-2026-04-16.md](../research/full-train-release-orchestration-2026-04-16.md) — PD-7 scope
- [release-recovery-and-manual-escape-hatch-2026-04-16.md](../research/release-recovery-and-manual-escape-hatch-2026-04-16.md) — PD-8 scope
- [src/Directory.Build.targets](../../src/Directory.Build.targets) — current MSBuild guard implementation
