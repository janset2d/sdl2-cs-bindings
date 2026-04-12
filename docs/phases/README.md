# Phases — Janset.SDL2 / Janset.SDL3

> Phase workflow, current status, and navigation for all phase documents.

## Phase Status Overview

| Phase | Name | Status | Document |
| --- | --- | --- | --- |
| 1 | SDL2 Core Bindings + Harvesting | **DONE** | [phase-1-sdl2-bindings.md](phase-1-sdl2-bindings.md) |
| 2 | CI/CD & Packaging | **IN PROGRESS** | [phase-2-cicd-packaging.md](phase-2-cicd-packaging.md) |
| 3 | SDL2 Complete | PLANNED | [phase-3-sdl2-complete.md](phase-3-sdl2-complete.md) |
| 4 | Binding Auto-Generation | PLANNED | [phase-4-binding-autogen.md](phase-4-binding-autogen.md) |
| 5 | SDL3 Support | PLANNED | [phase-5-sdl3-support.md](phase-5-sdl3-support.md) |

## Active Phase

**Phase 2: CI/CD & Packaging** — Resumed 2026-04-11 after ~10 month hiatus.

Priority items for Phase 2:

1. Complete `vcpkg.json` (add all missing satellite libraries with feature flags)
2. Update vcpkg baseline (SDL2 2.32.4 → 2.32.10)
3. Implement Cake PackageTask (harvest output → .nupkg)
4. Make release-candidate-pipeline.yml functional
5. Clean up native binaries from git
6. Correct and validate the local development playbook

## Phase Lifecycle

```text
PLANNED → IN PROGRESS → DONE
                ↓
          (can pause → resume)
```

- A phase moves to IN PROGRESS when active work begins.
- A phase is DONE when all its exit criteria (defined in the phase doc) are met.
- Phases can be paused and resumed (as happened with Phase 2 after the hiatus).
- Phase boundaries are not strict walls — some overlap is expected (e.g., Phase 3 work may start before Phase 2 is 100% complete if the remaining items are non-blocking).

## Reading Order

For **catching up** (after absence): Read Phase 1 (what was accomplished) → Phase 2 (what's in progress).

For **day-to-day work**: Read only the active phase document. Reference other phases for context when needed.

For **planning ahead**: Read the PLANNED phases to understand the roadmap, but do not start detailed design work until a phase is activated.
