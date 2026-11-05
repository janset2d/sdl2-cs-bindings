# Validator Output-Shape Standardization — DEFERRED

**Status:** Audit complete. Migration deferred.
**Date:** 2026-05-16 (audited during PSTH-H refactor course-correction).
**Unpark policy:** Promote during a future design-cleanup slice (e.g. PreFlight reporter refresh, ADR-002 validator-contract sweep), or when a new validator surfaces a third bespoke shape and tips the cost balance.

## What This Is

Validator return types in `build/_build/Validation/**` are split between two coexisting shapes — every new validator inherits whichever pattern its closest neighbour uses, with no explicit rule on which to pick.

### Camp A — `ValidationReport` (11 validators)

Canonical home: [`build/_build/Results/ValidationReport.cs`](../../../build/_build/Results/ValidationReport.cs).
Shape: `ValidationReport : IReadOnlyCollection<ValidationCheck>` with `IsValid`, `HasWarnings`, `Errors`, `Warnings`, `Combine(params ValidationReport[])`.
`ValidationCheck` carries `Name`, `Severity` (`Warning`/`Error`), `Message`, optional `Code` (guardrail ID).

Members:

- `BindingPublicApiCoherenceValidator`
- `HybridStaticLeakValidator`
- `HarvestPreconditionsValidator`
- `NativePackageMetadataValidator`
- `PackageOutputValidator`
- `SatelliteUpperBoundValidator`
- `ReadmeMappingTableValidator`
- `HybridStaticOverlayValidator`
- `ManifestFamilyNameInvariantValidator`
- `PackageConsumerSmokePreconditionsValidator`
- `NativeSmokePreconditionsValidator`

### Camp B — Custom typed validation/check/status (6 validators)

Each validator owns a dedicated `<Topic>Validation` record + `<Topic>Check` record + `<Topic>CheckStatus` enum under `build/_build/Validation/Models/`.

Members:

- `VersionConsistencyValidator`
- `UpstreamVersionAlignmentValidator` (G54)
- `CrossFamilyDependencyResolvabilityValidator` (G58)
- `CoreLibraryIdentityValidator`
- `CsprojPackContractValidator`
- `HarvestReadinessValidator`
- `OverlayPortVersionCoherenceValidator` (G60) — most recent addition (2026-05-16)

## Why Two Camps Exist

The split is driven by **reporter rendering needs**, not by any explicit design decision:

1. **Per-check structured success rendering.** `PreflightReporter` renders Camp B validators with typed field interpolation, e.g.

   ```text
   ✅ sdl2-mixer: overlay 2.8.1#2 matches upstream 2.8.1#2
   ✅ sdl2-image: family version '2.8.0' aligns with upstream '2.8.0'
   ```

   These pulls (`check.OverlayVersion`, `check.UpstreamPortVersion`, `check.FamilyVersion`, etc.) require typed access. `ValidationReport.ValidationCheck` exposes only `Name`/`Code`/`Message`/`Severity` — to keep the same render, the structure would have to be baked into the `Message` string, which sacrifices both grep-ability and the possibility of structured downstream consumption.

2. **Typed status enums.** Camp B validators carry domain-specific status enums (e.g. `OverlayPortVersionCheckStatus.{Match, VersionDrift, OverlayManifestMissing, UpstreamPortMissing, InvalidJson}`). The reporter `switch`es on these to choose icons, log levels, and tail messages. `ValidationReport` only models a binary severity (`Warning`/`Error`) plus implicit success via absence.

Camp A validators have no per-check success-info to render — they only emit errors. That's why they tolerate the flatter shape.

## Why Deferred

- **G60 is consistent with its immediate neighbours** (G54, G58). The asymmetry is not within the version/identity-invariant subcategory — it's between subcategories.
- **No user-facing pain.** Reporter rendering works; tests work; new contributors copy whichever neighbour they see first. The cost is conceptual ("which shape do I pick?") more than operational.
- **A proper cleanup is a multi-validator change.** Migrating only G60 to `ValidationReport` would *worsen* the symmetry within its subcategory (G54 + G58 still bespoke). Migrating all three together is the right slice, and it requires deciding whether to extend `ValidationReport` to support typed payloads or to accept the rendering downgrade.
- **The decision touches reporter contract.** `PreflightReporter` would need either:
  - Severity-only rendering (lose per-port `Match` line, lose typed status icons), or
  - A `ValidationCheck` extension carrying structured payload (`Tags : IReadOnlyDictionary<string,string>`? A generic payload field? An attached `IRenderable`?), which is a non-trivial design choice in itself.

## Migration Options (For The Unpark Decision)

| Option | Effort | Tradeoff |
| --- | --- | --- |
| **1. Status quo** | 0 | Two camps stay. Add `// Camp A`/`// Camp B` doc comments + ADR note explaining the split so new validators pick deliberately. |
| **2. Migrate G60 only to `ValidationReport`** | ~1 day | Lose per-port Match line (only PASS/FAIL summary). G60 leaves G54/G58 subcategory pattern → asymmetric within "version/identity invariants". |
| **3. Migrate G54 + G58 + G60 (Camp B subcategory) to `ValidationReport`** | ~2–3 days | Lose all per-check success rendering; reporter shrinks to severity-only pass/fail. Largest payoff for consistency. Requires reporter tests update. |
| **4. Extend `ValidationReport` with a typed-payload extension point** | ~3–4 days | Camp A stays valid; Camp B migrates with payload preserved. Most invasive but preserves both behaviours. Risks bloating `ValidationCheck` for marginal benefit. |

## Unpark Triggers

Resume the decision when **any** of:

1. A new validator slice (anything beyond G60) needs to pick Camp A vs Camp B and the choice is genuinely ambiguous — i.e. the slice author would have to consult both for precedent.
2. PreFlight reporter is touched for unrelated reasons (e.g. tabular rendering, structured-log export, JSON output mode) — fold the standardization in.
3. ADR-002 validator-contract sweep is formally opened (currently scoped to task-orchestrator concerns, not validator output shape).
4. A consumer of the validation surface (a non-PreFlight reporter, a CI gate, an external tool) needs structured access — only Camp A's flat shape is grep-friendly; only Camp B preserves the comparison fields.

## File Index

| File | Contents |
| --- | --- |
| [`README.md`](README.md) | This document. |

## Cross-Reference

- [`../../../build/_build/Results/ValidationReport.cs`](../../../build/_build/Results/ValidationReport.cs) — Camp A canonical type
- [`../../../build/_build/Validation/Models/`](../../../build/_build/Validation/Models/) — Camp B per-validator types
- [`../../../build/_build/Validation/Vcpkg/OverlayPortVersionCoherenceValidator.cs`](../../../build/_build/Validation/Vcpkg/OverlayPortVersionCoherenceValidator.cs) — most recent Camp B addition that surfaced the question (G60, 2026-05-16)
- [`../../../build/_build/Targets/PreFlightCheck/Reporting/PreflightReporter.cs`](../../../build/_build/Targets/PreFlightCheck/Reporting/PreflightReporter.cs) — reporter that drives both camps' rendering today
- [`../../parking-lot.md`](../../parking-lot.md) — index of deferred ideas
