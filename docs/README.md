# Documentation Map — Janset.SDL2 / Janset.SDL3

> Quick navigation guide for all project documentation. Start with [onboarding.md](onboarding.md) if this is your first time.

## Loading Rules

### For LLM/Code Agents

1. Load `onboarding.md` and `AGENTS.md` (repo root) before doing anything else.
2. Load `plan.md` to understand current status and active phase.
3. Load `phases/README.md` to know which phase is active.
4. Then load docs relevant to your specific task (see table below).
5. Load `reference/` only when you need broader tool/framework context beyond repo-specific docs.

### For Human Contributors

1. Start with [onboarding.md](onboarding.md) for project overview.
2. Check [plan.md](plan.md) for current status and roadmap.
3. Use [playbook/](playbook/) for "how do I...?" questions.
4. Use [reference/](reference/) only when you need deeper general background that is useful but not canonical repo behavior.

## Document Index

### Core (Always-Current)

| Document | Purpose | When to Read |
| --- | --- | --- |
| [onboarding.md](onboarding.md) | Project overview, strategic decisions, repo layout | First visit, new contributor onboarding |
| [plan.md](plan.md) | Canonical status, phase roll-up, version tracking, roadmap | Before any work session |
| [phases/README.md](phases/README.md) | Phase workflow, active vs completed phases | When you need to know what's in scope |

### Phases (Execution Details)

| Document | Phase | Status |
| --- | --- | --- |
| [phase-1-sdl2-bindings.md](phases/phase-1-sdl2-bindings.md) | SDL2 Core Bindings + Harvesting | DONE |
| [phase-2-cicd-packaging.md](phases/phase-2-cicd-packaging.md) | CI/CD & Packaging | IN PROGRESS |
| [phase-3-sdl2-complete.md](phases/phase-3-sdl2-complete.md) | SDL2 Complete (all libs, all RIDs, tests, samples) | PLANNED |
| [phase-4-binding-autogen.md](phases/phase-4-binding-autogen.md) | Binding Auto-Generation | PLANNED |
| [phase-5-sdl3-support.md](phases/phase-5-sdl3-support.md) | SDL3 Support | PLANNED |

### Research (Dated Findings — Verify Before Acting)

| Document | Topic | Date |
| --- | --- | --- |
| [native-packaging-patterns.md](research/native-packaging-patterns.md) | SkiaSharp/LibGit2Sharp/SQLitePCLRaw NuGet patterns | 2026-04-11 |
| [binding-autogen-approaches.md](research/binding-autogen-approaches.md) | CppAst vs ClangSharp vs c2ffi comparison | 2026-04-11 |
| [sdl3-ecosystem-analysis.md](research/sdl3-ecosystem-analysis.md) | SDL3 status, vcpkg support, C# binding ecosystem | 2026-04-11 |
| [symlink-handling.md](research/symlink-handling.md) | NuGet symlink problem, tar.gz rationale | 2026-04-11 |
| [tunit-testing-framework-2026-04-14.md](research/tunit-testing-framework-2026-04-14.md) | TUnit adoption plan, patterns, naming convention, project setup | 2026-04-14 |
| [cake-testing-strategy-2026-04-14.md](research/cake-testing-strategy-2026-04-14.md) | Cake.Testing package, ICakeContext mocking, test layers, coverage plan | 2026-04-14 |
| [source-mode-native-visibility-2026-04-15.md](research/source-mode-native-visibility-2026-04-15.md) | Source Mode native payload visibility mechanism (platform-branched `Directory.Build.targets` + Cake two-source framework); PD-4 resolved, PD-5 direction locked | 2026-04-15 |

### Playbook (How-To Recipes)

| Document | Question It Answers |
| --- | --- |
| [local-development.md](playbook/local-development.md) | How do I set up and build locally? |
| [adding-new-library.md](playbook/adding-new-library.md) | How do I add a new SDL satellite library? |
| [vcpkg-update.md](playbook/vcpkg-update.md) | How do I update vcpkg baseline and library versions? |
| [ci-troubleshooting.md](playbook/ci-troubleshooting.md) | How do I debug CI failures? |
| [cake-frosting-patterns.md](playbook/cake-frosting-patterns.md) | What Cake Frosting patterns should I follow in this repo? |
| [cake-frosting-host-organization.md](playbook/cake-frosting-host-organization.md) | How should I structure and scale the Cake build host? |

### Knowledge Base (Deep Technical References)

| Document | Topic |
| --- | --- |
| [harvesting-process.md](knowledge-base/harvesting-process.md) | Native binary harvesting pipeline (how it works end-to-end) |
| [ci-cd-packaging-and-release-plan.md](knowledge-base/ci-cd-packaging-and-release-plan.md) | CI/CD pipeline design and packaging strategy |
| [cake-build-architecture.md](knowledge-base/cake-build-architecture.md) | Cake Frosting build system structure and patterns |
| [design-decisions-and-tradeoffs.md](knowledge-base/design-decisions-and-tradeoffs.md) | Preserved architecture review findings, tradeoffs, and refactor pressure points |

### Parking Lot (Intentionally Preserved Futures)

| Document | Purpose |
| --- | --- |
| [parking-lot.md](parking-lot.md) | Deferred ideas, partially implemented threads, and hardening backlog that should not be lost during archive cleanup |

### Reference (Deep On-Demand)

| Document | Purpose |
| --- | --- |
| [cake-frosting-build-expertise.md](reference/cake-frosting-build-expertise.md) | Long-form Cake Frosting reference for deeper general context when trimmed playbooks are not enough |

### Legacy Status

Legacy archive content was retired on 2026-04-12 after migration review.
Useful material was either folded into current docs, promoted into [reference/](reference/), or intentionally discarded as obsolete.
If older historical detail is ever needed again, use git history rather than expecting a standing docs archive.

## Conflict Resolution

When docs disagree with each other:

1. **Code wins over docs** for runtime behavior questions.
2. **`plan.md` wins** for current status and phase information.
3. **`onboarding.md` wins** for strategic decisions.
4. **Knowledge-base and playbook docs win over reference docs** for repo-specific technical details.
5. **Reference docs provide broader context** — useful, but not canonical repo behavior.

## Change Hygiene

- Prefer consolidation over new files.
- If you create a new doc, add it to this index.
- If you move or rename a doc, update all references.
- Never duplicate tables/registries — state which copy is authoritative.
- Research docs should always carry a date.
- Do not leave active ideas only in retired notes or one-off references; move them into current docs or [parking-lot.md](parking-lot.md).
