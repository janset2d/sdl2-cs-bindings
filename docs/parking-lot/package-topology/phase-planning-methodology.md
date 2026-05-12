# Package Topology Refactor — Implementation Plan Methodology

> Methodology + roadmap for the role-metapackage refactor's implementation phase. Companion to [`strategy-brief.md`](strategy-brief.md) (strategic decisions) and [`impact-map.md`](impact-map.md) (mechanical surface). All three working drafts retire in Phase 6.

## Why Phase-by-Phase Planning

We deliberately rejected a single monolithic implementation plan in favor of one plan document per phase. Three reasons:

1. **Speculative drift.** Each phase ships artifacts (skip attributes, helper renames, model fields, validator dispatch paths) that downstream phases reference concretely. Writing Phase 4 step-by-step before Phase 1 ships means hand-coding flip syntax against an attribute that doesn't exist yet, exact-pin assertions against a normalizer mode that isn't implemented yet, csproj references to a `RoleMetaPackageId` helper that hasn't been renamed yet. Stale plans = wasted writing + execution friction.
2. **Phase boundaries are real.** Each phase ends green-build + green-test + AGENTS.md commit-approval gate (impact-map.md §10). That's a natural plan boundary — the phase deliverable is a coherent, shippable change.
3. **Cadence over completionism.** Project convention favors skip-micro-checkpoints during execution + return to "what's next?" at each significant boundary. Per-phase plans match that cadence: write Phase N → execute Phase N → commit Phase N → write Phase N+1 against the now-current codebase. Speculation pushed to the latest possible moment; concrete steps anchored in real artifacts.

The cost: re-planning effort at each phase transition (~15-30 min to author the next phase's plan against the shipped artifacts). The benefit: every step the implementing agent executes references a real symbol, a real file, a real assertion — no placeholders, no "TBD", no speculative APIs.

## Phase Status Table

| Phase | Plan document | Status | Slice scope |
| --- | --- | --- | --- |
| 1 — Characterization tests | [implementation-plan-phase-1.md](implementation-plan-phase-1.md) | **Active** — written 2026-05-12, ready to execute | S1 |
| 2a — Conventions + helpers rename | TBD — written after Phase 1 ships | Speculative outline in [impact-map.md §10](impact-map.md) | S2, S3 |
| 2b — Manifest schema + validator dual-mode | TBD — written after Phase 2a ships | Speculative outline in impact-map.md §10 | S4 |
| 3 — Project topology | TBD | Speculative outline | S5, S6, S7 |
| 4 — Packaging + guardrails + publish + smoke | TBD | Speculative outline. Largest phase. | S8, S9, S10, S11, S12, S13 |
| 5 — Consumer validation | TBD | Speculative outline. Includes `learning-sdl2` real-consumer check per impact-map.md §15. | S14, S15, S16 |
| 6 — Canonical documentation | TBD | Speculative outline. Retires brief + impact-map + this plan + every phase plan. | S17, S18 |

## What's Concrete Now vs Speculative

**Concrete and stable (locked through the refactor):**

- 3-tier topology shape: `Janset.SDL<M>.<Role>` meta + `.Bindings` + `.Native` (impact-map.md §1, strategy-brief.md §HOW Package layers).
- V1 family-lock: all three packages of a family share one version (strategy-brief.md §Versioning stance).
- Manifest schema `2.2` field shape: `bindings_project` + `native_project` + `meta_project` (impact-map.md §2).
- Normalizer modes: `CrossFamilyRewrite` / `CrossFamilyAndWithinFamilyRewrite` / `CrossFamilySynthesize` (impact-map.md §4.C).
- Cross-family lower-bound bug fix: dep family version as lower bound, not current family version (impact-map.md §4.C).
- Phase ordering: 2a → 2b → 3, with validator dual-mode landing before csproj changes (impact-map.md §10).
- TUnit `SkipAttribute`-derived pattern for target characterization tests (impact-map.md §9.H + `build/_build.Tests/Fixtures/WindowsOnlyAttribute.cs` as exemplar).

**Speculative until written or shipped:**

- Skip attribute name and predicate mechanism. `[TopologyRefactorTarget]` is the working candidate; Phase 1 plan finalizes it.
- Exact PackageOutputValidator integration shape under dual-mode (depends on Phase 2b shipping).
- Bindings-only smoke csproj layout — Gap #3 co-design slice owns the final choice (impact-map.md §6.B).
- Final guardrail ID assignments (impact-map.md §5 uses behavior-first names; knowledge-base re-numbering in Phase 6).
- README mapping table column shape (impact-map.md §8.A recommended; finalized when `ReadmeMappingTableGenerator` lands).

## Outstanding Corrections to impact-map.md

These were caught while authoring this plan + the Phase 1 plan, after the impact-map commit (`d5cfc56`). They land in a separate corrections commit before Phase 1 execution starts. Recording here for traceability.

| Section | Correction | Source |
| --- | --- | --- |
| §4.C "PackageFamilyVersionSet (NEW immutable record)" row in artifacts table | `PackageFamilyVersionSet` **already exists** at `build/_build/Data/Versions/PackageFamilyVersionSet.cs`, alongside `PackageFamilyId` and `PackageFamilyVersion`. It is consumed by `ResolveVersionsFromManifestTask`, `ExplicitVersionParser`, and `FakeCakeWorld.WithFamilyVersions` (see `PackageTaskScenarioTests.cs:215-222`). The refactor's job is to **plumb the existing type into `PackageFamilyPacker.PackAsync` + `DependencyRangeNormalizer.NormalizeAsync`** — not to create a new abstraction. API: `RequireVersion(PackageFamilyId) → NuGetVersion`, `TryGetVersion`, immutable record. | Authored Phase 1 plan, spotted while reading `PackageTaskScenarioTests.cs` for the canonical scenario pattern. |

After Phase 1 ships, the correction commit lands as a doc-only edit; impact-map.md §4.C "NEW abstraction" framing rewrites to "existing abstraction; plumb to downstream callers."

## Re-Planning Cadence

When a phase ships (green-build + green-test + AGENTS.md commit-approval gate + actual commit), the next phase's plan is authored from these inputs:

1. [`strategy-brief.md`](strategy-brief.md) — immutable strategic anchor through the refactor.
2. [`impact-map.md`](impact-map.md) — mechanical surface map; treat as immutable through the refactor (corrections land via dedicated review pass + commit, not silently during implementation).
3. The just-shipped phase's artifacts — the new code, tests, manifest fields, helper APIs. Plans for downstream phases reference these by exact path and exact symbol name.
4. Review notes captured during the just-shipped phase — recorded in the "Phase Status" table above as a one-line addendum to the row (e.g., "S2 surfaced `Foo.Bar` rename collision in `Build.Tests.X`; Phase 2b plan adapts.").

Author responsibility: the brainstorming/writing-plans agent (the role you and I have shared in this conversation) writes the next phase's plan. The implementing agent (subagent-driven-development or inline executing-plans) does NOT author plans — they execute the active plan and surface friction. The separation keeps execution focused; planning keeps the head lifted for re-scoping.

## Execution Approach

For each phase plan, the writing-plans skill offers two execution modes:

| Mode | When | Cadence |
| --- | --- | --- |
| **Subagent-driven** ([`superpowers:subagent-driven-development`](https://github.com/anthropics/claude-code-skills)) | Default. Fresh subagent per task; main agent reviews between tasks. Best for medium-large slices. | Per-task review checkpoints. |
| **Inline executing-plans** ([`superpowers:executing-plans`](https://github.com/anthropics/claude-code-skills)) | Tiny slices (1-3 tasks) where context-switching cost exceeds review value. | Batch execution with checkpoint commits. |

Mode selection happens at the end of each phase plan's authoring, not pre-committed here.

## Lifecycle

`phase-planning-methodology.md`, `strategy-brief.md`, `impact-map.md`, and every `implementation-plan-phase-N.md` share one lifecycle:

- **Active** through Phase 6.
- Phase 6 step 8 (impact-map.md §10) promotes durable rules to ADRs + knowledge-base + playbook + README + AGENTS.md.
- Phase 6 step 9 commit removes the entire `docs/parking-lot/package-topology/` directory.

This matches the project's refactoring-doc convention: durable architecture rules live in ADRs and knowledge-base; per-slice working drafts disappear after promotion. Future readers learn from canonical docs, not retired plans.

## Approval Gate Reminder

Per AGENTS.md §Approval Gate, code changes (Cake tasks, MSBuild targets, csprojs, manifest, validators, generators) require explicit "go / apply / proceed / başla / yap" before the implementing agent makes them. Phase-plan documents themselves are documentation-only edits (exception per AGENTS.md), so this `phase-planning-methodology.md` and individual phase plans can be authored without per-document approval. But:

1. **Plan authoring** = no approval needed (docs only).
2. **Plan commit** = explicit approval required.
3. **Plan execution** = explicit approval required, and the implementing agent re-confirms at each task boundary if uncertainty surfaces.

## Authoring Convention: Reference Discipline

**Every phase plan and every plan-shaped document in `docs/parking-lot/package-topology/` MUST cross-reference the project's canonical ADRs and knowledge-base guidelines it touches.** This is non-negotiable. Plans without these references encourage the implementing agent to invent conventions that already exist; references keep execution aligned with settled decisions and durable guidance.

The mandatory reference matrix per phase:

| Document | When to reference | Why |
| --- | --- | --- |
| [`AGENTS.md`](../../../AGENTS.md) | **Always** | Operating rules, approval gate ("Do not start coding new features without explicit go"), settled strategic decisions, test naming convention, build-host reference pattern. |
| [`decisions/2026-05-05-target-centric-build-host.md`](../../decisions/2026-05-05-target-centric-build-host.md) (ADR-002) | **Always** when touching `build/_build/` | Target-centric architecture is the build-host's canonical shape. New code lives under `Targets/<TargetName>/`, `Validation/`, `Data/`, `Host/`, `Results/`, `Tools/` — not legacy `Features/`, `Shared/`, `Integrations/`. |
| [`decisions/2026-05-12-build-host-data-layer.md`](../../decisions/2026-05-12-build-host-data-layer.md) (ADR-003) | When touching `build/_build/Data/` (manifest models, repositories, version files) | Contract-centric data layer — adapters live under `Data/<Contract>/`. |
| [`decisions/2026-05-05-d3seg-and-package-first.md`](../../decisions/2026-05-05-d3seg-and-package-first.md) (ADR-001) | Phase 4+ (versioning + packaging surface) and Phase 6 (supersede) | D-3seg + family-lock + package-first consumer contract. This refactor preserves D-3seg + family-lock; Phase 6 supersedes ADR-001 with a successor encoding the role-metapackage topology. |
| [`knowledge-base/release-guardrails.md`](../../knowledge-base/release-guardrails.md) | Whenever touching a guardrail (G-numbered behavior) | Single map of every guardrail this project commits to. Use behavior-first language; final ID assignments happen in Phase 6 against this catalog. |
| [`knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) | **Always** when writing tests | Canonical test infra (`FakeCakeWorld`, `TargetTestHost`, `FixtureLoader`), filesystem rule (no `System.IO`, no `AppContext.BaseDirectory` traversal), test data policy (embedded fixtures vs centralized inline), test taxonomy, TUnit rules. |
| [`knowledge-base/extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) | Whenever extracting a collaborator or making interface-vs-class decisions | Private-method decision tree, interface discipline, anti-patterns. |
| [`AGENTS.md` §Build-Host Reference Pattern](../../../AGENTS.md) | Whenever modifying Cake task surface | Tasks are not pass-through shells, requests earned but standard, no catch-all Shared, Cake nativeness at build boundaries, behavior-first names not guardrail IDs in code. |

A phase plan that touches both build-host code and tests references ADR-002 + testing-guidelines.md + AGENTS.md as a minimum. Implementing agents are expected to **read those documents before coding**, not after.

This convention applies retroactively to [`strategy-brief.md`](strategy-brief.md) + [`impact-map.md`](impact-map.md): both already cross-reference the canonical set. Future Phase N plans inherit the same discipline.

## Cross-Reference

- [`strategy-brief.md`](strategy-brief.md) — strategic decisions (V1 family-lock, 3-tier topology, deferred ultimate meta)
- [`impact-map.md`](impact-map.md) — mechanical surface map (file paths, classes, methods, schemas, slice sequence)
- [`implementation-plan-phase-1.md`](implementation-plan-phase-1.md) — currently active phase plan
- [`../../decisions/2026-05-05-target-centric-build-host.md`](../../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 build-host architecture
- [`../../decisions/2026-05-12-build-host-data-layer.md`](../../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 data layer
- [`../../decisions/2026-05-05-d3seg-and-package-first.md`](../../decisions/2026-05-05-d3seg-and-package-first.md) — ADR-001 D-3seg + package-first (to be superseded in Phase 6)
- [`../../knowledge-base/release-guardrails.md`](../../knowledge-base/release-guardrails.md) — guardrail catalog
- [`../../knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — canonical test infra and fixture data policy
- [`../../knowledge-base/extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) — collaborator extraction policy
- [`../../../AGENTS.md`](../../../AGENTS.md) — operating rules, approval gates, settled decisions, build-host reference pattern
