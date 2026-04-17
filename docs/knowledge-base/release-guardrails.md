# Release Guardrails Roadmap

> **Status:** Canonical — this doc is the single map of every guardrail the project commits to land. Every entry has an owning stream and a current implementation status. When a guardrail moves from "planned" to "active," update its row here in the same change.
>
> **Last updated:** 2026-04-17 (S1 adoption — 9 exact-pin-related guardrails retired; G23 reframed; G11 marked REVISIT. See [phase-2-adaptation-plan.md "S1 Adoption Record"](../phases/phase-2-adaptation-plan.md).)
>
> **Why a dedicated roadmap:** Defense-in-depth is a hard requirement for this project. A single missed inconsistency between manifest, csproj, vcpkg, family tag, and produced nupkg can ship a broken package family. Guardrails are scattered across multiple subsystems (PreFlight, MSBuild, Cake, CI workflows, post-pack assertions, feed checks). This doc enumerates them all, deduplicates, and maps each to its owning stream so nothing is forgotten in the gap between A-risky and the public release of v1.0.
>
> **S1 adoption note (2026-04-17):** Within-family exact-pin requirement retired in favor of SkiaSharp-style minimum range (`>=`). Guardrails **G1, G2, G3, G5, G8, G9, G10, G20, G24 removed** from active scope (exact-pin-specific). **G23 reframed** as the primary within-family coherence check (was derivative of G20). **G11 marked REVISIT** (NuGet built-in NU5016; relevance under minimum-range contract uncertain). **G4, G6, G7, G17, G18, G21, G22, G25–G27, G46** retained. Drift protection moves from consumer-side nuspec invariant to orchestration-time invariant (Cake atomic pack + post-pack version-match). See [release-lifecycle-direction.md §4 Drift Protection Model](release-lifecycle-direction.md) and [phase-2-adaptation-plan.md "S1 Adoption Record"](../phases/phase-2-adaptation-plan.md).

## 1. Guardrail Philosophy

1. **Strict by default.** Every guardrail hard-fails when violated. Bypasses (when they exist) require explicit operator action and loud logging.
2. **Defense-in-depth.** Each invariant is checked at multiple layers when feasible — structural (PreFlight), build-time (MSBuild target), pack-time (post-pack assertion), publish-time (CI gate). One missed check is rarely the only check.
3. **Catch as early as possible.** A guardrail that fires at PreFlight is preferable to one that fires at Pack, which is preferable to one that fires at Publish, which is preferable to one that fires at Consumer.
4. **No silent skips.** If a guardrail is conditionally bypassed (e.g., `AllowEmptyNativePayload=true` for G46 — the only documented pack-time bypass post-S1), the bypass is logged loudly with the override reason.
5. **Mirror the failure to its source.** Error messages name the file, line, property, and the canonical rule that was violated, so the operator can fix without re-reading the code.

## 2. Guardrail Inventory

### 2.1 csproj Structural (PreFlight, A-risky scope — post-S1 subset)

These guardrails run as part of `PreFlightCheckTask` and are the first line of defense. They check that csproj files conform to the canonical shape established by [release-lifecycle-direction.md §1](release-lifecycle-direction.md).

> **S1 2026-04-17:** Guardrails G1 (PrivateAssets="all"), G2 (paired PackageReference), G3 (bracket notation PackageVersion), G5 (family-version property name convention), G8 (sentinel fallback) **RETIRED**. They enforced the exact-pin csproj shape (Mechanism 3) which is no longer used. Retained: G4, G6, G7. Cross-section checks G17/G18 remain in §2.4.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G4 | csproj `<MinVerTagPrefix>` equals `manifest.json package_families[].tag_prefix + "-"` | Active (A-risky step 3, post-S1 subset) | `CsprojPackContractValidator` |
| G6 | csproj `<PackageId>` follows canonical pattern `Janset.SDL<Major>.<Role>` (managed) or `Janset.SDL<Major>.<Role>.Native` (native) | Active (A-risky step 3, post-S1 subset) | `CsprojPackContractValidator` |
| G7 | Native `<ProjectReference>` path resolves to `manifest.json package_families[].native_project` | Active (A-risky step 3, post-S1 subset) | `CsprojPackContractValidator` |

### 2.2 MSBuild Build-Time (Directory.Build.targets, post-S1 scope)

These guardrails run as MSBuild targets during `dotnet build` / `dotnet pack` invocation. They catch issues that PreFlight cannot see (runtime-resolved property values).

> **S1 2026-04-17:** Guardrails G9 (`_GuardAgainstShippingRestoreSentinel`) and G10 (`AllowSentinelExactPin` bypass banner) **RETIRED**. They enforced the `0.0.0-restore` sentinel mechanism which no longer exists (sentinel PropertyGroup removed from managed csprojs). G46 unrelated to exact-pin — stays.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G46 | Pack of a `Janset.SDL2.*.Native` package fails if `$(NativePayloadSource)` is unset (native csproj has no source-tree fallback; empty payload would ship otherwise). Bypass `-p:AllowEmptyNativePayload=true` for deliberate empty packs. | Active (D-local Tier 2 cleanup, 2026-04-17) | `_GuardAgainstEmptyNativePayload` in [src/Directory.Build.targets](../../src/Directory.Build.targets) |

### 2.3 Restore + Pack (NuGet built-in)

NuGet itself enforces some invariants we rely on but don't author. Tracked here for completeness so we know what NOT to re-implement.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G11 | Restore-time and pack-time version mismatch between bracket-notation `PackageVersion` and resolved project version produces NU5016 ("empty version range") | **REVISIT (S1 2026-04-17)** — effectively dead-letter under minimum-range contract; no bracket-notation `PackageVersion` items remain in managed csprojs post-S1. Kept listed as informational tripwire: if anyone reintroduces bracket notation, NuGet still enforces. Revisit whether to keep tracking or drop. | NuGet `Pack.targets` |
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

### 2.5 Post-Pack nuspec Assertion (Stream D-local scope — post-S1 subset)

These guardrails run AFTER `dotnet pack` completes, opening the produced `.nupkg` and asserting the emitted nuspec is correct. This is the defense-in-depth layer that catches anything the structural + MSBuild layers missed.

G25 is intentionally scoped to the managed package's `.snupkg`. Payload-only `.Native` projects currently disable symbol-package generation by design, so D-local validates the symbols that should exist instead of pretending every package emits a `.snupkg`.

> **S1 2026-04-17:** G20 (exact-pin `[x.y.z]` nuspec assertion) **RETIRED** — exact-pin no longer used. G24 (sentinel leak check) **RETIRED** — sentinel no longer exists. G21 **REFRAMED** to cover all family dependencies (within and cross), since both are now minimum range. G23 **REFRAMED and PROMOTED** to primary within-family coherence check (was derivative of G20). G22, G25–G27 unchanged.

| # | Invariant | Status | Owner |
| --- | --- | --- | --- |
| G21 | All family dependencies (within-family Native AND cross-family Core) emitted as minimum range `x.y.z` (no brackets). Post-S1: unified check; within-family no longer bracketed. Additional 2026-04-17 consumer-safety invariant: within-family Native dependency must not exclude build assets, otherwise `.NET Framework` consumers lose the native `buildTransitive` copy targets. | Active (D-local; reframed by S1 2026-04-17, expanded 2026-04-17) | Cake `PackageTask` post-pack assertion |
| G22 | All TFM dependency groups (net9.0, net8.0, netstandard2.0, net462) are consistent with each other | Active (D-local, 2026-04-16) | Cake `PackageTask` post-pack assertion |
| G23 | **Primary within-family coherence check (post-S1).** Native package's `<version>` matches the managed package's `<version>` byte-for-byte. Detects drift between family members that would make the minimum-range contract misleading (consumer resolves compatible versions but publisher intended them pinned together). | Active (D-local; promoted to primary by S1 2026-04-17) | Cake `PackageTask` post-pack assertion |
| G25 | Managed symbol package (.snupkg) is present and valid | Active (D-local, 2026-04-16) | Cake `PackageTask` post-pack assertion |
| G26 | Nuspec `<repository>` element points at expected commit SHA | Active (D-local, 2026-04-16) | Cake `PackageTask` post-pack assertion |
| G27 | Nuspec metadata fields (id, authors, license, icon) match expected values | Active (D-local, 2026-04-16) | Cake `PackageTask` post-pack assertion |
| G47 | Native package ships the consumer-side buildTransitive contract — both `buildTransitive/$(PackageId).targets` (thin wrapper) and `buildTransitive/Janset.SDL2.Native.Common.targets` (shared extraction + .NETFramework AnyCPU copy). Missing either entry leaves Linux/macOS consumers without the `tar -xzf` extraction step (DllNotFoundException at first P/Invoke) and .NETFramework AnyCPU consumers without the per-RID DLL copy. | Active (D-local, 2026-04-17) | Cake `PackageTask` post-pack assertion |
| G48 | For every `runtimes/<rid>/native/` subtree in the native `.nupkg`: Windows RIDs ship one or more `*.dll` files with no tarball; Unix RIDs ship exactly one `$(PackageId).tar.gz` (the per-package rename that prevents filename collision with sibling `.Native` packages when the .NET SDK flattens the RID subtree into the consumer's `$(OutDir)`). | Active (D-local, 2026-04-17) | Cake `PackageTask` post-pack assertion |

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

Counts reflect post-S1 (2026-04-17) scope. 9 guardrails retired by S1 (G1/G2/G3/G5/G8/G9/G10/G20/G24). Historical pre-S1 cumulative was 43; post-S1 is 34.

| Stream | Guardrails delivered (post-S1) | Cumulative active |
| --- | --- | --- |
| B (closed) | G14, G15, G16, G19, G36 (local) | 5 |
| A-risky (partially reverted 2026-04-17) | G4, G6, G7 (PreFlight csproj — post-S1 subset), G17, G18 (cross-section) | 10 |
| C (CI modernization) | G36 (CI gate wiring — same guardrail as B-local; count unchanged) | 10 |
| D-local (post-S1) | G21, G22, G23 (post-pack assertions — minimum range + version match), G25, G26, G27 (symbols, repo, metadata), G46 (MSBuild payload guard) | 17 |
| D-ci | G28–G35 (CI publish pipeline) | 25 |
| PD-7 (full-train) | G37–G42 (release-set validation) | 31 |
| PD-8 (manual escape) | G43–G45 (manual operator validation) | 34 |
| — | G11, G12, G13 are NuGet/build-host built-ins (not delivered by any stream; G11 marked REVISIT) | — |

## 4. Failure Mode Catalog

For each known failure mode, list the guardrails that catch it. If no guardrail catches it, that's a gap to fill. (Post-S1 2026-04-17: rows specific to exact-pin mechanism removed; rows for minimum-range contract added.)

| Failure mode | Guardrails | Gap? |
| --- | --- | --- |
| MinVer tag prefix in csproj drifts from manifest (e.g., csproj says `core-` but manifest says `sdl2-core-`) | G4 | No |
| Operator creates new family in manifest but doesn't add csprojs | G7 (path doesn't resolve) | No |
| Csproj `<PackageId>` deviates from canonical `Janset.SDL<Major>.<Role>` convention | G6 | No |
| Operator passes wrong family version flag at CLI (managed and native end up at different versions) | G23 (post-pack version match) | No |
| Family dependency accidentally emitted as exact-pin bracket notation instead of minimum range | G21 | No |
| Missing TFM group in dependency emission | G22 | No |
| Managed and native shipped at mismatched versions (e.g., Cake orchestration bug) | G23 (within-family version coherence check — primary defense post-S1) | No |
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
| Managed symbol package missing | G25 | No |
| Build artifact contains wrong commit SHA | G26 | No |
| Direct `dotnet pack` of a `.Native` csproj without Cake ships empty runtimes/licenses payload | G46 | No |

**Retired failure modes (S1 2026-04-17):** "Operator removes PrivateAssets=all" (no longer a mechanism), "Operator adds new satellite without bracket-notation PackageVersion" (no longer required), "Standalone dotnet pack ships 0.0.0-restore sentinel" (sentinel removed), "Renaming family in manifest without updating csproj property names" (`Sdl<Role>FamilyVersion` property no longer required). These failure modes cannot occur under the S1 shape.

**Net:** with the full guardrail roadmap landed, every currently-cataloged failure mode is caught by at least one guardrail before the package reaches public consumers. If a future failure mode is discovered without a covering guardrail, add a new row here AND a new guardrail to fill the gap.

## 5. Operational Principles

1. **PreFlight is the single CI gate** that runs before any matrix work. If PreFlight fails, no resources are spent on builds that would fail downstream anyway.
2. **Multi-layer for the critical invariants.** Within-family version coherence (post-S1 2026-04-17: the critical release invariant, since drift is otherwise invisible under minimum-range semantics) is checked at the orchestration layer (Cake atomic `PackageTask` packs both family members at identical `--family-version` in one invocation) AND at the post-pack layer (G23 asserts the emitted `<version>` elements match byte-for-byte). Two independent layers; either alone would be sufficient, together they are defense-in-depth.
3. **Bypass requires explicit, loud opt-in.** `-p:AllowEmptyNativePayload=true` (G46 bypass) is the only documented pack-time bypass in post-S1 scope. It produces banner-level warning. No silent escape hatches.
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
- [phase-2-adaptation-plan.md](../phases/phase-2-adaptation-plan.md) — stream-by-stream implementation roadmap; see "S1 Adoption Record" for the 2026-04-17 amendments
- [exact-pin-spike-and-nugetizer-eval-2026-04-16.md](../research/exact-pin-spike-and-nugetizer-eval-2026-04-16.md) — **SUPERSEDED 2026-04-17.** Within-family exact-pin mechanism research. Kept as history; no longer binding. The production-time version flow constraint documented there is what motivated S1 adoption.
- [nu5016-cake-restore-investigation-2026-04-17.md](../../artifacts/temp/nu5016-cake-restore-investigation-2026-04-17.md) — investigation artifact: traced NU5016 root cause to `NuGet.Build.Tasks.Pack.targets:335` globals-replace; resolution via S1 adoption
- [full-train-release-orchestration-2026-04-16.md](../research/full-train-release-orchestration-2026-04-16.md) — PD-7 scope
- [release-recovery-and-manual-escape-hatch-2026-04-16.md](../research/release-recovery-and-manual-escape-hatch-2026-04-16.md) — PD-8 scope
- [src/Directory.Build.targets](../../src/Directory.Build.targets) — current MSBuild guard implementation
