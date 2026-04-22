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
| [phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) | CI/CD & Packaging (active execution ledger) | IN PROGRESS |
| [phase-4-binding-autogen.md](phases/phase-4-binding-autogen.md) | Binding Auto-Generation | PLANNED (design brief) |
| [phase-5-sdl3-support.md](phases/phase-5-sdl3-support.md) | SDL3 Support | PLANNED (design brief) |

> **Retired phase documents** (redirect stubs kept for backlink integrity — not canonical navigation targets):
>
> - `phases/phase-2-cicd-packaging.md` — superseded by `phases/phase-2-adaptation-plan.md` and `plan.md` roadmap (retired 2026-04-21; redirects).
> - `phases/phase-3-sdl2-complete.md` — superseded by `plan.md` roadmap (retired 2026-04-21; sample projects spec absorbed into [issue #60](https://github.com/janset2d/sdl2-cs-bindings/issues/60)).
>
> See [phases/README.md](phases/README.md) for the Planned Phase Doc Retention Test that governs when a phase doc earns its place vs retires to stub.

### Research (Dated Findings — Verify Before Acting)

| Document | Topic | Date |
| --- | --- | --- |
| [native-packaging-patterns.md](research/native-packaging-patterns.md) | SkiaSharp/LibGit2Sharp/SQLitePCLRaw NuGet patterns | 2026-04-11 |
| [binding-autogen-approaches.md](research/binding-autogen-approaches.md) | CppAst vs ClangSharp vs c2ffi comparison | 2026-04-11 |
| [sdl3-ecosystem-analysis.md](research/sdl3-ecosystem-analysis.md) | SDL3 status, vcpkg support, C# binding ecosystem | 2026-04-11 |
| [symlink-handling.md](research/symlink-handling.md) | NuGet symlink problem, tar.gz rationale | 2026-04-11 |
| [tunit-testing-framework-2026-04-14.md](research/tunit-testing-framework-2026-04-14.md) | TUnit adoption plan, patterns, naming convention, project setup | 2026-04-14 |
| [cake-testing-strategy-2026-04-14.md](research/cake-testing-strategy-2026-04-14.md) | Cake.Testing package, ICakeContext mocking, test layers, coverage plan | 2026-04-14 |
| [source-mode-native-visibility-2026-04-15.md](research/source-mode-native-visibility-2026-04-15.md) | Deprecated historical Source Mode research note; symlink-preservation findings retained as reference for future remote-feed tar-extract caching under ADR-001 | 2026-04-15 |
| [unix-native-identity-strategy-2026-04-22.md](research/unix-native-identity-strategy-2026-04-22.md) | Detailed analysis of single-file Unix `.so` / `.dylib` goals in the SDL2 family: triplets vs overlay ports, maintainability vs reliability tradeoffs, validation requirements, and SDL3 implications | 2026-04-22 |

### Playbook (How-To Recipes)

| Document | Question It Answers |
| --- | --- |
| [local-development.md](playbook/local-development.md) | How do I set up and build locally? |
| [adding-new-library.md](playbook/adding-new-library.md) | How do I add a new SDL satellite library? |
| [vcpkg-update.md](playbook/vcpkg-update.md) | How do I update vcpkg baseline and library versions? |
| [ci-troubleshooting.md](playbook/ci-troubleshooting.md) | How do I debug CI failures? |
| [cake-frosting-patterns.md](playbook/cake-frosting-patterns.md) | What Cake Frosting patterns should I follow in this repo? |
| [cake-frosting-host-organization.md](playbook/cake-frosting-host-organization.md) | How should I structure and scale the Cake build host? |
| [cross-platform-smoke-validation.md](playbook/cross-platform-smoke-validation.md) | How do I verify cross-platform correctness after a refactor? |

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

### Reviews (Dated Assessments)

> **Policy (2026-04-18).** External code reviews are stored as dated, authored snapshots. They reflect the reviewer's read at one point in time and are NOT canonical status — [`plan.md`](plan.md), the active phase doc, and the ADR directory under [`decisions/`](decisions/) remain the sources of truth. When a review's findings are addressed, the resolution is tracked in the consolidated index under [`reviews/`](reviews/); reviews older than ~3 months may be moved to an `archive/` subfolder if no longer actionable. The consolidated index (first row below) is the recommended entry point for human or agent readers landing here.

| Document | Scope | Date |
| --- | --- | --- |
| **[2026-04-18-consolidated-review-index.md](reviews/2026-04-18-consolidated-review-index.md)** | **Entry point.** Consolidates and de-duplicates the seven 2026-04-18 reviews, records spot-verification state per finding, and maps each item to a severity + status column updated as waves complete | 2026-04-18 |
| [2026-04-18-build-tests-deep-dive-codex.md](reviews/2026-04-18-build-tests-deep-dive-codex.md) | Companion wide-lens deep-dive on `build/_build.Tests`: fixtures, composition-root coupling, task-layer platform bias | 2026-04-18 |
| [2026-04-18-build-tests-review.md](reviews/2026-04-18-build-tests-review.md) | Deep-dive review of the `build/_build.Tests` suite: fixtures, assertion quality, and packaging-path coverage gaps | 2026-04-18 |
| [2026-04-18-general-deep-dive-review-claude-opus.md](reviews/2026-04-18-general-deep-dive-review-claude-opus.md) | Full-repo deep-dive (Claude Opus 4.7) — packaging guardrails, CI drift, doc-vs-code coherence | 2026-04-18 |
| [2026-04-18-general-deep-dive-review-codex.md](reviews/2026-04-18-general-deep-dive-review-codex.md) | Session-baseline general review covering solution entrypoints, smoke build contracts, and contributor docs drift | 2026-04-18 |
| [2026-04-18-general-deep-dive-review.md](reviews/2026-04-18-general-deep-dive-review.md) | Repository-wide multi-round deep-dive covering build-host reliability, packaging guardrails, and docs/code drift | 2026-04-18 |
| [2026-04-18-packaging-consumer-review.md](reviews/2026-04-18-packaging-consumer-review.md) | Deep-dive review of Phase 2 packaging, consumer delivery, and native `buildTransitive` behavior | 2026-04-18 |
| [2026-04-18-smoke-orchestration-review.md](reviews/2026-04-18-smoke-orchestration-review.md) | Deep-dive review of smoke orchestration: guard targets, runner inputs, and restore determinism gaps | 2026-04-18 |

### Decisions (Architecture Decision Records)

| Document | Decision | Date | Status |
| --- | --- | --- | --- |
| [2026-04-18-versioning-d3seg.md](decisions/2026-04-18-versioning-d3seg.md) | ADR-001 — D-3seg versioning, package-first local dev, artifact source profile abstraction | 2026-04-18 | Accepted |
| [2026-04-19-ddd-layering-build-host.md](decisions/2026-04-19-ddd-layering-build-host.md) | ADR-002 — DDD layering for the Cake build host (Tasks / Application / Domain / Infrastructure) | 2026-04-19 | Accepted |
| [2026-04-20-release-lifecycle-orchestration.md](decisions/2026-04-20-release-lifecycle-orchestration.md) | ADR-003 — Release lifecycle orchestration + version source providers (Manifest / GitTag / Explicit) | 2026-04-20 | Draft (v1.5 post-sweep) |

New ADRs land under [`decisions/`](decisions/) with a dated filename (`YYYY-MM-DD-<slug>.md`) and carry an embedded Impact Checklist that tracks implementation completion.

### Archive (Historical Snapshots)

Pre-rewrite bodies of canonical documents are preserved under [`_archive/`](_archive/) when a rewrite substantially changes the shape but historical rationale has no other canonical home. See [`_archive/README.md`](_archive/README.md) for the convention.

| Document | Original | Archived on | Reason |
| --- | --- | --- | --- |
| [_archive/phase-2-adaptation-plan-2026-04-15.md](_archive/phase-2-adaptation-plan-2026-04-15.md) | `phases/phase-2-adaptation-plan.md` | 2026-04-21 | Pre-ADR-003 amendment archaeology (S1 Adoption Record, retired Stream A0, A-risky historical record, ADR-001 addendum, Strategy State Audit, closed PDs). Rewrite retains only current execution state + open PDs. |

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
