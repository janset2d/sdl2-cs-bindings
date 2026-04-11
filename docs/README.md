# Documentation Map — Janset.SDL2 / Janset.SDL3

> Quick navigation guide for all project documentation. Start with [onboarding.md](onboarding.md) if this is your first time.

## Loading Rules

### For LLM/Code Agents
1. Load `onboarding.md` and `AGENTS.md` (repo root) before doing anything else.
2. Load `plan.md` to understand current status and active phase.
3. Load `phases/README.md` to know which phase is active.
4. Then load docs relevant to your specific task (see table below).
5. Do **not** load `archive/` by default — treat it as dated reference material.

### For Human Contributors
1. Start with [onboarding.md](onboarding.md) for project overview.
2. Check [plan.md](plan.md) for current status and roadmap.
3. Use [playbook/](playbook/) for "how do I...?" questions.

## Document Index

### Core (Always-Current)

| Document | Purpose | When to Read |
|----------|---------|-------------|
| [onboarding.md](onboarding.md) | Project overview, strategic decisions, repo layout | First visit, new contributor onboarding |
| [plan.md](plan.md) | Canonical status, phase roll-up, version tracking, roadmap | Before any work session |
| [phases/README.md](phases/README.md) | Phase workflow, active vs completed phases | When you need to know what's in scope |

### Phases (Execution Details)

| Document | Phase | Status |
|----------|-------|--------|
| [phase-1-sdl2-bindings.md](phases/phase-1-sdl2-bindings.md) | SDL2 Core Bindings + Harvesting | DONE |
| [phase-2-cicd-packaging.md](phases/phase-2-cicd-packaging.md) | CI/CD & Packaging | IN PROGRESS |
| [phase-3-sdl2-complete.md](phases/phase-3-sdl2-complete.md) | SDL2 Complete (all libs, all RIDs, tests, samples) | PLANNED |
| [phase-4-binding-autogen.md](phases/phase-4-binding-autogen.md) | Binding Auto-Generation | PLANNED |
| [phase-5-sdl3-support.md](phases/phase-5-sdl3-support.md) | SDL3 Support | PLANNED |

### Research (Dated Findings — Verify Before Acting)

| Document | Topic | Date |
|----------|-------|------|
| [native-packaging-patterns.md](research/native-packaging-patterns.md) | SkiaSharp/LibGit2Sharp/SQLitePCLRaw NuGet patterns | 2026-04-11 |
| [binding-autogen-approaches.md](research/binding-autogen-approaches.md) | CppAst vs ClangSharp vs c2ffi comparison | 2026-04-11 |
| [sdl3-ecosystem-analysis.md](research/sdl3-ecosystem-analysis.md) | SDL3 status, vcpkg support, C# binding ecosystem | 2026-04-11 |
| [symlink-handling.md](research/symlink-handling.md) | NuGet symlink problem, tar.gz rationale | 2026-04-11 |

### Playbook (How-To Recipes)

| Document | Question It Answers |
|----------|-------------------|
| [local-development.md](playbook/local-development.md) | How do I set up and build locally? |
| [adding-new-library.md](playbook/adding-new-library.md) | How do I add a new SDL satellite library? |
| [vcpkg-update.md](playbook/vcpkg-update.md) | How do I update vcpkg baseline and library versions? |
| [ci-troubleshooting.md](playbook/ci-troubleshooting.md) | How do I debug CI failures? |

### Knowledge Base (Deep Technical References)

| Document | Topic |
|----------|-------|
| [harvesting-process.md](knowledge-base/harvesting-process.md) | Native binary harvesting pipeline (how it works end-to-end) |
| [ci-cd-packaging-and-release-plan.md](knowledge-base/ci-cd-packaging-and-release-plan.md) | CI/CD pipeline design and packaging strategy |
| [cake-build-architecture.md](knowledge-base/cake-build-architecture.md) | Cake Frosting build system structure and patterns |

### Archive (Historical — Read-Only)

| Document | Why Archived |
|----------|-------------|
| [cake-build-plan.md](archive/cake-build-plan.md) | Original blueprint; superseded by knowledge-base docs |
| [architectural-review.md](archive/architectural-review.md) | Point-in-time review (May 2025) |
| [architectural-review-core-components.md](archive/architectural-review-core-components.md) | Deep component analysis (May 2025) |
| [source-generator-design.md](archive/source-generator-design.md) | OneOf/Results generator — parked, may revisit |
| [source-generator-implementation-summary.md](archive/source-generator-implementation-summary.md) | Implementation report for above |
| [Cake Frosting Build Expertise_.md](archive/Cake%20Frosting%20Build%20Expertise_.md) | Educational reference (69KB) |
| [ai-reviews/](archive/ai-reviews/) | 5 AI code reviews from Jan 2026 session |

## Conflict Resolution

When docs disagree with each other:
1. **Code wins over docs** for runtime behavior questions.
2. **`plan.md` wins** for current status and phase information.
3. **`onboarding.md` wins** for strategic decisions.
4. **Knowledge-base docs win over archive docs** for technical details.
5. **Archive docs are read-only** — do not update them, create new docs instead.

## Change Hygiene

- Prefer consolidation over new files.
- If you create a new doc, add it to this index.
- If you move or rename a doc, update all references.
- Never duplicate tables/registries — state which copy is authoritative.
- Research docs should always carry a date.
