# Parking Lot — Preserved Ideas and Partial Threads

> Deliberate landing zone for ideas that are valid or partially implemented but not active work today. The goal: no important idea should survive only because it is buried in retired notes or half-expressed in code.

## How To Use This File

- Items here are worth preserving but not in the active phase.
- Promote items into [`plan.md`](plan.md), a phase doc, a playbook, or a knowledge-base doc when work actually starts.
- Remove items only when they are clearly rejected or superseded — record why.

## Status Legend

| Status | Meaning |
| --- | --- |
| `partially-implemented` | Real code/config/workflow scaffolding exists but isn't fully wired or production-ready |
| `planned` | Idea is intentionally kept; no working implementation yet |
| `parked` | Valuable, but not on the current roadmap |
| `hardening-backlog` | Quality / reliability / maintenance work to revisit |

## Partially Implemented

### Result Pattern Discipline

- Status: `partially-implemented`
- The build host uses `OneOf` (with `OneOf.SourceGenerator`, `OneOf.Monads`). Some manual wrappers and `FromXxx` / `ToXxx` ceremony still exist.
- Preserve:
  - Audit wrapper-local conversion helpers; trim dead ceremony before adding heavier abstraction.
  - Decide an explicit style between named success accessors (`ValidationSuccess`, `Closure`, `DeploymentPlan`) and the generic `SuccessValue()` API so new code stops mixing both idioms.
  - Async chaining + cancellation-aware result variants — already in build host; keep usage consistent.

### PreFlight Validator Growth Guardrails

- Status: `hardening-backlog`
- `PreFlightCheckTask` is a thin orchestrator over per-rule validators; that shape can bloat again if package-family or CI-flow policy folds into the same classes without discipline.
- Preserve:
  - Keep follow-through narrow unless the rule surface actually grows.
  - Prefer splitting loader/parser concerns from rule evaluation only when the added behavior justifies the seams.
  - Avoid reintroducing `BuildContext` leakage or task-level policy into validator internals.

### Harvesting Component Refactors

- Status: `parked`
- Refactor seams identified in earlier reviews; not active work.
- Preserve as pressure-tested directions only when real change volume justifies them:
  - Splitting `BinaryClosureWalker` into primary-binary resolution / package-dep walking / binary scanning / file classification roles.
  - Extracting `SystemFileFilter` if `RuntimeProfile` keeps accumulating non-profile logic.
  - `HarvestPipeline` orchestration extraction ([#87](https://github.com/janset2d/sdl2-cs-bindings/issues/87), deferred).

## Planned Operational Features

### Known-Issues Skip List

- Status: `planned`
- Skip known-bad `library/version/RID` combos in CI instead of repeatedly burning time on predictable failures.
- Intended artifact: `build/known-issues.json`.
- Preserve:
  - Shape for keyed entries (`library/version/RID` or equivalent).
  - CI behavior: skip vs warn vs hard-fail.
  - Expiry / revalidation policy so the list does not become permanent sediment.
  - Guidance for proving an item can be removed after upstream fixes.

### Recovery Procedures (beyond PD-8 manual escape)

- Status: `planned`
- Package yanking strategy (internal + public feeds).
- Internal / public feed rollback procedures.
- Temporary artifact backups during publish.

### Maintenance Mode / Health Checks

- Status: `planned`
- Scheduled feed health checks.
- Stale package validation.
- `known-issues.json` drift detection.
- Recovery time benchmarking.

## Hardening Backlog

### Invariant-Culture Logging Hygiene

- Status: `hardening-backlog`
- Preserve:
  - Keep numeric / date logging culture-invariant anywhere the build host prints metrics or timestamps.
  - If invariant-format logging appears in multiple tasks, factor a tiny helper instead of repeating ad hoc `string.Create(CultureInfo.InvariantCulture, ...)` shapes.

### Optional Coverage Signal

- Status: `parked`
- ADR-002 §14 left the door open for coverage to return later as an optional non-blocking signal (not a build-host target concern). The Cake-owned `Coverage-Check` gate retired in S10 (2026-05-08); coverage instrumentation came out of CI entirely.
- Preserve:
  - Re-introduce only when there is concrete motivation (PR-comment summary, dashboard surface, external SaaS, etc.) and a deliberate design.
  - Do NOT reintroduce as a build-host Cake target — that shape was rejected. If it returns, it returns as a CI-side signal owned by the workflow, not a build-host gate.
  - Re-add must include the rationale and the surface it serves; otherwise it stays parked.

### OneOf-Shaped Result Types (Surviving Post-S11)

- Status: `parked`
- ADR-002 §11 retires "OneOf-style result hierarchies for expected build failures" in favor of `Result<T, TError>` (binary) or `ValidationReport`/`ValidationCheck` (multi-check). S11 retired 5 strategy-related OneOf result types and demonstrated the collapse pattern (`ValidationResult`/`ValidationError`/`ValidationSuccess` → `ValidationReport`).
- Preserve:
  - Eleven OneOf-shaped result types survive: `PackageInfoResult`, `UpstreamVersionAlignmentResult`, `DotNetPackResult`, `ProjectMetadataResult`, `ArtifactPlannerResult`, `ClosureResult`, `CopierResult`, `PackageValidationResult`, `CsprojPackContractResult`, `CoreLibraryIdentityResult`, `VersionConsistencyResult`.
  - Each retires within its respective target migration (P6/P7/P8) when that target's pipeline gets reshaped.
  - The OneOf package dependency stays in `Build.csproj` and `Directory.Packages.props` until all 11 types retire (likely P10 final cleanup).
  - The collapse pattern: OneOf-shaped `Result<TError, TSuccess>` → either `ValidationReport` (multi-check with severities) OR a homegrown `Result<T, TError>` record struct (binary success/failure) per ADR-002 §11.

### Performance And Caching

- Status: `hardening-backlog`
- Caching for expensive package-info queries; smarter reuse of repeated filesystem or dependency-analysis work.

### Tool Reproducibility

- Status: `hardening-backlog`
- Explicit tool-path selection where environment drift matters; reproducible CI vs local tool resolution guidance.

## Packaging, Supply Chain, And Release Detail

### SBOM Generation

- Status: `planned`
- Software bill of materials generation for native and managed artifacts.

### Native Symbol Handling

- Status: `planned`
- Managed `.snupkg` publication is live. Deferred: native symbol handling strategy (per-platform `.pdb` / `.dSYM` / `.debug` payloads + symbol server publish).

## Retention Rule

Retired material disappears only after the useful parts are either:

- moved into canonical docs ([`plan.md`](plan.md), phase docs, playbook, knowledge-base), or
- preserved here as an intentionally parked thread, or
- explicitly rejected (with the why captured).
