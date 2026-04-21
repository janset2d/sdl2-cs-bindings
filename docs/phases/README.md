# Phases — Janset.SDL2 / Janset.SDL3

> Phase workflow, current status, and navigation for all phase documents.

## Phase Status Overview

| Phase | Name | Status | Document |
| --- | --- | --- | --- |
| 1 | SDL2 Core Bindings + Harvesting | **DONE** | [phase-1-sdl2-bindings.md](phase-1-sdl2-bindings.md) |
| 2 | CI/CD & Packaging | **IN PROGRESS** | [phase-2-adaptation-plan.md](phase-2-adaptation-plan.md) |
| 3 | SDL2 Complete | PLANNED | [plan.md roadmap](../plan.md) |
| 4 | Binding Auto-Generation | PLANNED | [phase-4-binding-autogen.md](phase-4-binding-autogen.md) |
| 5 | SDL3 Support | PLANNED | [phase-5-sdl3-support.md](phase-5-sdl3-support.md) |

> **Retired phase documents** (kept as redirect stubs for backlink integrity — not canonical navigation targets):
>
> - `phase-2-cicd-packaging.md` — superseded by `phase-2-adaptation-plan.md` and `plan.md` roadmap.
> - `phase-3-sdl2-complete.md` — superseded by `plan.md` roadmap.

## Active Phase

**Phase 2: CI/CD & Packaging** — resumed 2026-04-11 after ~10 month hiatus; currently in the ADR-003 orchestration rewrite pass.

Post-[ADR-003](../decisions/2026-04-20-release-lifecycle-orchestration.md) priority items for Phase 2:

1. **Canonical documentation sweep** — align all canonical docs with ADR-003 baseline (vision-first, stage-owned validation, version-source providers, consumer smoke matrix re-entry). *(Active)*
2. **Cake refactor** — introduce `IPackageVersionProvider` + 3 impls (Manifest / GitTag / Explicit); extract `NativeSmokeTask` from Harvest; adopt per-stage request records; retire `--family-version` in favor of `--explicit-version`.
3. **CI/CD workflow rewrite** — new `release.yml` with dynamic matrix from `manifest.runtimes[]` + consumer smoke matrix re-entry; deprecate or reuse existing `prepare-native-assets-*.yml` in harvest-only shape.
4. **PA-2 behavioral validation** — end-to-end pack + consumer-smoke witness runs on the four newly-hybridized RIDs (`win-arm64`, `win-x86`, `linux-arm64`, `osx-arm64`) via the new pipeline.
5. **G58 cross-family dependency resolvability** — implement scope-contains check + optional feed probe in Pack stage.
6. **Remote artifact source profile** — implement `RemoteArtifactSourceResolver` so `SetupLocalDev --source=remote` becomes operational (Phase 2b, Stream D-ci).

Historical Phase 2a scope items (vcpkg.json coverage, vcpkg baseline, Cake `PackageTask`, packaging strategy) already landed; see [plan.md](../plan.md) "What Works Today" and Strategic Decisions April 2026 for the current factual state. For the detailed stream-level ledger, see [phase-2-adaptation-plan.md](phase-2-adaptation-plan.md).

## Phase Lifecycle

```text
PLANNED → IN PROGRESS → DONE
                ↓
          (can pause → resume)
```

- A phase moves to IN PROGRESS when active work begins.
- A phase is DONE when all its exit criteria (defined in the phase doc or in `plan.md`) are met.
- Phases can be paused and resumed (as happened with Phase 2 after the hiatus).
- Phase boundaries are not strict walls — some overlap is expected (e.g., Phase 3 work may start before Phase 2 is 100% complete if the remaining items are non-blocking).

## Planned Phase Doc Retention Test

A planned phase document stays in `phases/` only if it carries **design-level content that `plan.md` cannot absorb** — for example:

- Alternative analysis tables (e.g., "CppAst vs ClangSharp vs c2ffi" matrix in Phase 4)
- Pipeline / topology diagrams specific to the phase
- Vendor availability matrices (e.g., SDL3 per-library vcpkg availability + features + blockers in Phase 5)
- Known-blocker catalogs that `plan.md` rotation cannot hold
- Canonical open questions that are genuinely unresolved (not already closed by an ADR or issue)

If the doc drifts into being a roadmap-brief copy of `plan.md` — i.e., its content is fully absorbable into the `plan.md` roadmap section — it retires via retire-to-stub. The test is **re-applied at each phase activation**: when a phase moves from PLANNED to IN PROGRESS, the doc is checked against this rule and either elevated (live content + exit criteria) or retired (stub with roadmap pointer).

This invariant exists because `phase-3-sdl2-complete.md` accumulated drift between 2025-05 and 2026-04 and became a copy of `plan.md` Phase 3 rotation with stale open questions. Retention test prevents recurrence for `phase-4-binding-autogen.md` and `phase-5-sdl3-support.md` as those phases activate.

## Reading Order

For **catching up** (after absence): Read Phase 1 (what was accomplished) → Phase 2 adaptation plan (what's in progress).

For **day-to-day work**: Read the active phase adaptation plan. Reference other phases for context when needed.

For **planning ahead**: Read the PLANNED phase docs (4, 5) to understand the design-level roadmap. Phase 3 scope lives in `plan.md` roadmap. Do not start detailed design work until a phase is activated.
