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

### External Native Overrides

- Status: `partially-implemented`
- Why it matters: supports consuming prebuilt native binaries instead of building via vcpkg.
- Current repo evidence:
  - The build host exposes `--use-overrides`.
  - The rest of the task pipeline does not yet consume that option.
- Preserve with this thread:
  - explicit override precedence rules
  - potential `--overridesPath` support
  - external storage ideas such as S3
  - LocalStack-backed testing if cloud-backed overrides are implemented

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
