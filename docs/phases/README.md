# Phases — Janset.SDL2 / Janset.SDL3

Phase workflow + active phase navigation.

## Status Overview

| Phase | Name | Status | Doc |
| --- | --- | --- | --- |
| 1 | SDL2 Core Bindings + Harvesting | DONE | retired (code + git history are canonical) |
| 2 | CI/CD & Packaging | **IN PROGRESS** | [phase-2-adaptation-plan.md](phase-2-adaptation-plan.md) |
| X | Build-Host Modernization (ADR-004 closed; ADR-002 target-centric refactor) | **IN PROGRESS** | [target-centric-build-host-refactor-plan.md](../refactoring/target-centric-build-host-refactor-plan.md) |
| 3 | SDL2 Complete (samples, meta-package, first prerelease) | PLANNED | [plan.md](../plan.md) roadmap |
| 4 | Binding Auto-Generation | PLANNED | [phase-4-binding-autogen.md](phase-4-binding-autogen.md) |
| 5 | SDL3 Support | PLANNED | [phase-5-sdl3-support.md](phase-5-sdl3-support.md) |

## Active Phases

Two phases are active in parallel:

**Phase 2: CI/CD & Packaging.** Core surface is landed (`release.yml` + Cake build host + `tools.cs`). The remaining tail lives in [phase-2-adaptation-plan.md](phase-2-adaptation-plan.md): nuget.org promotion (PD-7), release-recovery playbook (PD-8), and the four scope-assumption gaps surfaced in the 2026-05-01 tag-push rehearsals.

**Phase X: Build-Host Modernization — ADR-002 target-centric refactor.** ADR-004 migration is closed (P0 → P4-A on master). ADR-002 is the active continuation; it absorbs residual ADR-004 cleanup. Canonical docs: [`../decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md), [`../refactoring/target-centric-build-host-refactor-plan.md`](../refactoring/target-centric-build-host-refactor-plan.md), [`../refactoring/target-centric-build-host-review-checklist.md`](../refactoring/target-centric-build-host-review-checklist.md). P0 → P6 closed (S00 → S12, all 2026-05-08): docs/guardrails landed, V2 test infra + foundation primitives shipped, repositories + named `BuildContext` properties wired, `Info` + `ResolveVersions{FromManifest,FromExplicit}` + four diagnostic targets migrated, coverage gate + strategy abstraction retired, **PreFlight migrated to `Targets/PreFlightCheck/` with root-level `Validation/` named concept and full `PackageFamilyVersionSet` typed boundary (S12)**. Cross-platform validated 8/8 PASS on Windows + WSL/linux-x64 + macOS/osx-x64. Test count: 525. Next: P7 (Package boss-fight migration).

## Phase Lifecycle

```text
PLANNED → IN PROGRESS → DONE
                ↓
          (can pause → resume)
```

A phase is DONE when its exit criteria are met. Phases can pause and resume (Phase 2 did, after a ~10-month hiatus).

## Planned Phase Doc Retention Test

A planned phase document stays in `phases/` only if it carries **design-level content that `plan.md` cannot absorb**:

- Alternative analysis tables (e.g., "CppAst vs ClangSharp vs c2ffi" in Phase 4)
- Pipeline / topology diagrams specific to the phase
- Vendor availability matrices (e.g., SDL3 per-library vcpkg availability + features + blockers in Phase 5)
- Known-blocker catalogs that `plan.md` rotation cannot hold

If the doc drifts into a roadmap-brief copy of `plan.md`, it retires. The test re-applies at each phase activation.

## Reading Order

- **Catching up:** start with [phase-2-adaptation-plan.md](phase-2-adaptation-plan.md) for the Phase 2 tail.
- **Day-to-day:** active phase doc + [`plan.md`](../plan.md).
- **Planning ahead:** [phase-4-binding-autogen.md](phase-4-binding-autogen.md), [phase-5-sdl3-support.md](phase-5-sdl3-support.md). Don't start detailed design work until the phase activates.
