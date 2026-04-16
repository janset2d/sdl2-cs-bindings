# Parking Lot — Preserved Ideas and Partial Threads

> This file is the deliberate landing zone for ideas that are valid, useful, or partially implemented, but not active work today.
> The goal is simple: no important idea should survive only because it is buried in retired notes or half-expressed in code.

## How To Use This File

- Keep items here when they are worth preserving but are not in the active phase.
- Promote items into `plan.md`, a phase doc, a playbook, or a knowledge-base doc when work actually starts.
- Remove items only when they are clearly rejected or superseded, and record why.

## Status Legend

| Status | Meaning |
| --- | --- |
| `partially-implemented` | There is real code, config, or workflow scaffolding in the repo, but it is not fully wired or production-ready |
| `planned` | The idea is intentionally kept, but no working implementation exists in the repo |
| `parked` | Valuable, but not on the current roadmap |
| `hardening-backlog` | Quality, reliability, or maintenance work that should be revisited even if it is not feature work |

## Partially Implemented In Code

### Harvest Staging Path Model For Distributed CI

- Status: `partially-implemented` — CI matrix shape now locked
- **Direction reference:** CI matrix is one job per RID (build axis), with per-library harvesting inside each job. Matrix generated dynamically from `manifest.json`. See [`knowledge-base/release-lifecycle-direction.md`](../knowledge-base/release-lifecycle-direction.md) §5. Staging path implementation is a Phase 2 adaptation task.
- Why it matters: local harvest consolidation works, but distributed CI needs staging paths separate from final consolidated output.
- Current repo evidence:
  - `PathService` exposes `harvest-staging` helpers
  - active tasks and workflows still emit to `artifacts/harvest_output/`
- AI review convergence: multiple independent reviews all landed on this as the main CI blocker, so it should be treated as a prerequisite for the real release pipeline.
- Preserve with this thread:
  - per-RID staging artifact layout
  - consolidation into final `harvest_output/{library}/`
  - workflow artifact naming conventions such as `harvest-staging-{library}-{rid}`

### Result Pattern Modernization

- Status: `partially-implemented`
- Why it matters: the build host uses a rich `OneOf` result pattern, but the manual wrappers are verbose and were already identified as maintenance debt.
- Current repo evidence:
  - manual result wrapper types still exist in `build/_build/Modules/Harvesting/Results/`
  - `OneOf.SourceGenerator` is referenced by the build host project
- Preserve with this thread:
  - attribute-driven generation toggles for factory methods, conversions, monadic extensions, and async variants
  - custom generator option
  - phased implementation idea: basic generation first, monadic operations second, async helpers third
  - before/after examples from the archived design should be kept as evaluation material, not treated as already-proven outcome
  - simplification options if source generation is not worth the complexity
  - async chaining helpers and cancellation-aware variants already present in the build host
  - make an explicit style choice between named success accessors (`ValidationSuccess`, `CheckSuccess`, `Closure`, `DeploymentPlan`) and the generic `SuccessValue()` API so new code does not keep mixing both idioms
  - audit wrapper-local `FromXxx` and `ToXxx` conversion helpers before adding more result families; trim dead ceremony before introducing any heavier abstraction

### PreFlight Validator Growth Guardrails

- Status: `hardening-backlog`
- Why it matters: `PreFlightCheckTask` is back to being a thin orchestrator, but the validator layer can bloat again if Stream C folds package-family or CI-flow policy into the same classes without discipline.
- Preserve with this thread:
  - keep follow-through narrow unless the rule surface actually grows
  - prefer splitting loader/parser concerns from rule evaluation only when the added behavior justifies the extra seams
  - avoid reintroducing `BuildContext` leakage or task-level policy into validator internals

### Harvesting Component Refactors

- Status: `parked`
- Why it matters: the archived architecture reviews identified likely refactor seams that are still useful to remember even though they are not active work.
- Preserve with this thread:
  - splitting `BinaryClosureWalker` into primary-binary resolution, package-dependency walking, binary scanning, and file classification roles
  - extracting `SystemFileFilter` behavior if `RuntimeProfile` keeps accumulating non-profile logic
  - using these as pressure-tested refactor directions only when real change volume justifies them

## Planned Operational Features

### Known-Issues Skip List

- Status: `planned`
- Purpose: skip known-bad `library/version/RID` combinations in CI instead of repeatedly burning time on predictable failures.
- Intended artifact: `build/known-issues.json`
- Preserve with this thread:
  - file shape for `library/version/RID` or equivalent keyed entries
  - CI behavior for skip vs warn vs hard-fail
  - expiry or revalidation policy so the list does not become permanent sediment
  - guidance for proving an item can be removed after upstream fixes

### Internal Feed, Pack-Only, And Public Promotion Flow

- Status: `planned` — direction now locked
- **Direction reference:** Three-stage release promotion (local → internal → public) is defined in [`knowledge-base/release-lifecycle-direction.md`](../knowledge-base/release-lifecycle-direction.md) §6. Internal feed acts as staging for both pre-release and stable candidates. Implementation remains planned.
- Preserve with this thread:
  - `internal-feed` vs `pack-only`
  - manual public promotion workflow
  - force-build strategies such as `auto-detect`, `force-buildable`, and `force-everything`
  - deployment environment or status-page reporting

### Oops / Recovery Procedures

- Status: `planned`
- Preserve with this thread:
  - package yanking strategy
  - internal/public feed rollback procedures
  - temporary artifact backups during publish

### Maintenance Mode / Health Checks

- Status: `planned`
- Preserve with this thread:
  - scheduled feed health checks
  - stale package validation
  - `known-issues.json` drift detection
  - recovery time benchmarking

## Hardening Backlog

### Dependency-Resolution Resilience

- Status: `hardening-backlog`
- Preserve with this thread:
  - task-level failure-handling rules such as when to stop immediately vs continue with diagnostics
  - retry/backoff policy for transient tooling or environment failures
  - retry policies for transient failures
  - graceful degradation for partial dependency resolution
  - clearer error recovery paths
  - explicit rules for when lower-level harvest or preflight operations should convert failures into typed domain errors versus surfacing them as hard failures

### Logging And Coverage Metadata Hygiene

- Status: `hardening-backlog`
- Preserve with this thread:
  - keep human-readable numeric and date logging culture-invariant anywhere the build host prints metrics or timestamps
  - if invariant-format logging appears in multiple tasks, factor a tiny helper instead of repeating ad hoc `string.Create(CultureInfo.InvariantCulture, ...)` shapes
  - decide whether `measured_*` fields in `build/coverage-baseline.json` are intentional snapshots or metadata that should be auto-rewritten by a dedicated ratchet-raise flow

### Performance And Caching

- Status: `hardening-backlog`
- Preserve with this thread:
  - caching for expensive package-info queries
  - smarter reuse of repeated filesystem or dependency-analysis work

### Test Coverage For Build Infrastructure

- Status: `hardening-backlog`
- Preserve with this thread:
  - unit tests for dependency resolution
  - unit tests for system-file filtering
  - tests around consolidation and packaging manifests

## Packaging, Supply Chain, And Release Detail

### SBOM Generation

- Status: `planned`
- Preserve with this thread:
  - software bill of materials generation for native and managed artifacts

### Symbol Handling

- Status: `planned`
- Preserve with this thread:
  - managed `.snupkg` publication
  - deferred native symbol handling strategy

### Tool Reproducibility

- Status: `hardening-backlog`
- Preserve with this thread:
  - explicit tool-path selection where environment drift matters
  - reproducible CI vs local tool resolution guidance

## Legacy Cleanup Checkpoint (2026-04-12)

- Current-doc coverage now exists for the retired build-plan, architecture-review, source-generator, and AI-review material.
- `cake-frosting-build-expertise.md` has been promoted to `docs/reference/` as the long-form reference source behind the trimmed playbooks.
- The staging-vs-consolidated CI path split was the only AI-review finding that still needed stronger wording in current docs; that wording is now carried in the active docs set.

## Retention Rule

Retired material should only disappear after the useful parts are either:

- moved into canonical docs
- preserved here as an intentionally parked thread
- or explicitly rejected
