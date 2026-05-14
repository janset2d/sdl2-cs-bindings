# Documentation Map — Janset.SDL2 / Janset.SDL3

> Quick navigation guide for all project documentation. New here? Start with [onboarding.md](onboarding.md).

## Loading Rules

### For LLM/Code Agents

1. Load [`../AGENTS.md`](../AGENTS.md) — operating rules, approval gate, settled decisions.
2. Load [`onboarding.md`](onboarding.md) — what the project is, glossary, where-to-go pointers.
3. Load [`plan.md`](plan.md) — current status, active phase, roadmap.
4. Load [`phases/README.md`](phases/README.md) — which phase is active.
5. Then load docs relevant to the specific task (see Document Index below).

### For Human Contributors

1. Start with [onboarding.md](onboarding.md).
2. Check [plan.md](plan.md) for current status and roadmap.
3. Use [playbook/](playbook/) for "how do I...?" recipes.
4. Use [research/](research/) for design rationale and historical comparisons.

## Document Index

### Core (Always-Current)

| Document | Purpose | When to Read |
| --- | --- | --- |
| [onboarding.md](onboarding.md) | Project overview, glossary, where-to-go pointers | First visit |
| [release-strategy.md](release-strategy.md) | Strategic anchor — end state at v1.0, AST-first sequencing, NuGet labeling, promotion gates, maintenance commitment | Before making any decision that affects how the project ships |
| [plan.md](plan.md) | Canonical roadmap, current phase, hardening backlog | Before any work session |
| [post-refactor-cleanup-plan.md](post-refactor-cleanup-plan.md) | Completion record for the post-ADR-002 cleanup and build-host test infrastructure consolidation | When auditing why the migration-era notes were retired |
| [parking-lot.md](parking-lot.md) | Preserved ideas + partial threads not on the active roadmap | When a deferred concern surfaces |
| [parking-lot/](parking-lot/) | Deferred multi-file research (e.g., package-topology refactor) — directory siblings to `parking-lot.md` | When investigating an unpark trigger or surveying parked workstreams |
| [phases/README.md](phases/README.md) | Phase workflow, active vs planned phases | When the phase question matters |

### Phases

| Document | Phase | Status |
| --- | --- | --- |
| [phases/phase-2-adaptation-plan.md](phases/phase-2-adaptation-plan.md) | CI/CD & Packaging | IN PROGRESS — Phase 2b tail |
| [phases/phase-4-binding-autogen.md](phases/phase-4-binding-autogen.md) | Binding Auto-Generation | PLANNED (design brief) |
| [phases/phase-5-sdl3-support.md](phases/phase-5-sdl3-support.md) | SDL3 Support | PLANNED (design brief) |

Phase 3 (SDL2 Complete — samples, meta-package, first prerelease) lives directly in [plan.md](plan.md) per the [phases/README.md](phases/README.md) retention test.

### Binding Auto-Generation Workstream

| Document | Purpose |
| --- | --- |
| [binding-autogen/README.md](binding-autogen/README.md) | Workstream index and reading order for Phase 4 binding generator research, feasibility, and spike findings |
| [binding-autogen/binding-autogen-onboarding.md](binding-autogen/binding-autogen-onboarding.md) | Fast onboarding for a fresh contributor or agent picking up the binding-autogen workstream |
| [binding-autogen/binding-autogen-feasibility.md](binding-autogen/binding-autogen-feasibility.md) | Emit rules, platform-conditioned parsing model, validation layers, open decisions |
| [binding-autogen/binding-autogen-spike-findings.md](binding-autogen/binding-autogen-spike-findings.md) | Hands-on SDL2_gfx spike findings for ClangSharp and CppAst |

### Playbook (How-To Recipes)

| Document | Question It Answers |
| --- | --- |
| [playbook/local-development.md](playbook/local-development.md) | How do I clone, build, and develop locally? |
| [playbook/local-validation.md](playbook/local-validation.md) | How do I validate the pipeline end-to-end on my host (`tools ci-sim` / `tools setup`)? |
| [playbook/adding-new-library.md](playbook/adding-new-library.md) | How do I add a new SDL satellite library? |
| [playbook/overlay-management.md](playbook/overlay-management.md) | How do I work with vcpkg overlay triplets and ports? |
| [playbook/vcpkg-update.md](playbook/vcpkg-update.md) | How do I bump the vcpkg baseline and library versions? |

### Knowledge Base (Deep Technical References)

| Document | Topic |
| --- | --- |
| [knowledge-base/release-guardrails.md](knowledge-base/release-guardrails.md) | Every G-numbered guardrail, owning stage, and failure-mode catalog |
| [knowledge-base/testing-guidelines.md](knowledge-base/testing-guidelines.md) | Durable build-host testing policy: canonical infra, fake filesystem rule, fixture data policy, taxonomy |
| [knowledge-base/extraction-guidelines.md](knowledge-base/extraction-guidelines.md) | Durable collaborator/extraction policy: private-method decision tree, interface discipline, anti-patterns |

### Decisions (Architecture Decision Records)

| Document | Decision | Status |
| --- | --- | --- |
| [decisions/2026-05-05-d3seg-and-package-first.md](decisions/2026-05-05-d3seg-and-package-first.md) | ADR-001 — D-3seg versioning + package-first consumer contract | Accepted |
| [decisions/2026-05-05-target-centric-build-host.md](decisions/2026-05-05-target-centric-build-host.md) | ADR-002 — Target-centric Cake build host architecture | Accepted |
| [decisions/2026-05-12-build-host-data-layer.md](decisions/2026-05-12-build-host-data-layer.md) | ADR-003 — Contract-centric build host data layer | Accepted |

See [decisions/README.md](decisions/README.md) for the index.

### Research (Dated Findings)

[`research/`](research/) holds dated design rationale, comparative analyses, and historical research notes. Active binding-generation workstream material lives in [`binding-autogen/`](binding-autogen/). Verify against current code before acting on any individual note.

## Conflict Resolution

When docs disagree:

1. **Code wins** for runtime behavior questions.
2. **`plan.md` wins** for current status and phase information.
3. **`onboarding.md` wins** for strategic project framing.
4. **`AGENTS.md` wins** for operating rules and settled decisions.
5. **Knowledge-base / playbook wins over research** for repo-specific operational truth.

## Change Hygiene

- Prefer consolidation over new files. Add a doc only when it reduces complexity.
- If you create a new doc, add it to this index.
- If you move or rename a doc, update all internal references.
- Never duplicate tables / registries — state which copy is authoritative.
- Research docs should always carry a date.
- Don't leave active ideas only in retired notes; promote them into current docs or [parking-lot.md](parking-lot.md).
