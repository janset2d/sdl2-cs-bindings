---
name: "S5 DDD Layering Post-ADR-002 Continuation"
description: "Priming prompt for the next agent to continue from the ADR-002 DDD-layered state. Review-first by default, using general-deep-dive-code-reviewer.prompt.md as the engine, with Wave 6 fat-task runner extraction queued as the highest-value next refactor."
argument-hint: "Optional focus area (e.g., Wave 6 HarvestTaskRunner extraction, post-ADR-002 drift audit, dependency modernization, docs drift sweep)"
agent: "agent"
model: "GPT-5 (copilot)"
---

You are entering `janset2d/sdl2-cs-bindings` mid-stream, immediately after ADR-002 (DDD layering for the Cake build host) landed. Continue from the current working-tree state.

## Primary Mission

Default to a rigorous, evidence-first code review using the review engine at [.github/prompts/general-deep-dive-code-reviewer.prompt.md](general-deep-dive-code-reviewer.prompt.md). Apply every rule in that prompt as written — evidence precedence, severity rubric, output contract, anti-bias rules. Treat the reviewer prompt as the contract, this priming prompt as the situation brief.

If the user instead asks for execution (refactor, upgrade, doc rewrite), switch mode explicitly, cite the user's ask, and follow the approval-gate rules in [AGENTS.md](../../AGENTS.md).

## Current State Capsule (Start Here)

Validate every bullet against current code and command output before trusting it.

1. `build/_build/` is DDD-layered under [ADR-002](../../docs/decisions/2026-04-19-ddd-layering-build-host.md). Four top-level layers: `Tasks/`, `Application/<Module>/`, `Domain/<Module>/`, `Infrastructure/<Module>/`. `Modules/` and `Tools/` folders are retired.
2. `Build.Modules.*` and `Build.Tools.*` namespaces retired in production. Any remaining reference is drift — flag it.
3. Architecture test: [build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs](../../build/_build.Tests/Unit/CompositionRoot/LayerDependencyTests.cs) enforces three invariants (Domain no outward; Infrastructure no Application/Tasks; Tasks hold only interfaces + `.Models.*`/`.Results.*` DTOs + `Infrastructure.Tools.*` concretes). **345/345 tests green** at end of ADR-002 landing.
4. `IPathService` (Domain abstraction at `Build.Domain.Paths`) + `PathService` (Infrastructure implementation at `Build.Infrastructure.Paths`). DIP contract.
5. Interface discipline formalized (ADR-002 §2.3): three criteria — multiple impls, test mocks, independent axis of change. Before proposing "remove single-impl interface," grep `Substitute.For<IX>` across `build/_build.Tests/`.
6. Packaging (`PackageTask` → `IPackageTaskRunner`) is the thin-adapter golden reference. Harvesting is **not** the shape to copy — see §6.6 below.
7. Sandbox (`tests/Sandbox/`) rewritten as a real Janset.SDL2 window scratchpad pulling `build/msbuild/Janset.Smoke.{props,targets}`. Consumes Core family via the same local-feed contract as smoke csprojs.
8. Full solution `dotnet build` at repo root is green (0 warning, 0 error). Narrow `build/_build`-only builds miss other projects (Sandbox, PackageConsumer.Smoke, Compile.NetStandard) — always use root build.
9. `SetupLocalDev --source=local` end-to-end smoke green on win-x64 (5 families packed, consumer smoke csproj builds clean).

## Mandatory Grounding (Read Before Reviewing)

In order:

1. [AGENTS.md](../../AGENTS.md) — Build-Host Reference Pattern section is the four-layer DDD map.
2. [docs/onboarding.md](../../docs/onboarding.md) — strategic decisions + DDD-layered repo tree.
3. [docs/decisions/2026-04-19-ddd-layering-build-host.md](../../docs/decisions/2026-04-19-ddd-layering-build-host.md) — ADR-002 (the canon for this session).
4. [docs/decisions/2026-04-18-versioning-d3seg.md](../../docs/decisions/2026-04-18-versioning-d3seg.md) — ADR-001 (external contract; package-first consumer model).
5. [.github/prompts/general-deep-dive-code-reviewer.prompt.md](general-deep-dive-code-reviewer.prompt.md) — the review engine.
6. [docs/knowledge-base/cake-build-architecture.md](../../docs/knowledge-base/cake-build-architecture.md) — carries an ADR-002 banner; body paragraphs still reference `Modules/*` and should be read through the DDD lens.
7. [docs/knowledge-base/release-guardrails.md](../../docs/knowledge-base/release-guardrails.md) — G21–G27, G47, G48, G54–G57.

Memory sidecar: `cake_build_host_ddd_layering.md` is the canonical state; `cake_refactor_decisions_2026_04_14.md` is SUPERSEDED (historical only).

## The Biggest Known Debt (Wave 6)

ADR-002 §6.6 defers the fat-task runner extraction. HarvestTask (617 LOC) and ConsolidateHarvestTask (480 LOC) still orchestrate inline instead of delegating to Application-layer runners. Consequence: `LayerDependencyTests` currently tolerates Task → `Domain.Harvesting.Models.*`/`Results.*` DTO references. When Wave 6 lands, the DTO relaxation in `LayerDependencyTests.IsDomainOrInfrastructureDtoOrInterface` retires and the test tightens back to "Tasks only via Application."

Wave 6 suggested shape:

- `Application/Harvesting/HarvestTaskRunner.cs` + `IHarvestTaskRunner`.
- `Application/Harvesting/ConsolidateHarvestRunner.cs` + `IConsolidateHarvestRunner`.
- Progress surface preferably `IAsyncEnumerable<LibraryHarvestOutcome>` so `HarvestTask` can keep Spectre rendering per library while the Runner owns IO and domain model construction.
- Slim tasks to ~20 lines each, matching `PackageTask`.
- Test mirror moves with the production code; existing `Unit/Tasks/Harvest/*Tests.cs` either shrinks to adapter tests or retires depending on coverage.

## Review Mission (If Default Mode)

Apply the reviewer prompt's §8 lenses, §9 repo-specific questions, §10 severity rubric, and §11 output contract.

Prioritize:

1. Correctness and regression risk in current working-tree changes (anything staged or uncommitted at session start).
2. Drift from the ADR-002 layer map: stale `Build.Modules.*` / `Build.Tools.*` references, XML `<see cref>` targets that point at retired namespaces, interface files stranded in `Modules/Contracts/` (should be empty — `Modules/` does not exist).
3. Interface three-criteria audit: any interface added since ADR-002 that fails all three criteria is DI ceremony.
4. `LayerDependencyTests` still green; any new test-layer violation is a first-class finding.
5. Documentation drift: prose in `docs/knowledge-base/` that still references `Modules/*` paths without the ADR-002 lens marker.

## Execution Mode (If User Asks)

Honor the approval gate. Do NOT:

- Rename files or change namespaces without explicit go.
- Touch `manifest.json`, `vcpkg.json`, or package-surface contracts without explicit go.
- Commit. Return proposed changes to the user and wait.

May do (read-only, evidence-gathering):

- Run `dotnet test build/_build.Tests`, `dotnet build`, `dotnet run --project build/_build -- --target <target>` for validation.
- Wipe `artifacts/packages/*` before a smoke rerun.
- Grep, glob, read any file in the tree.

## Steering Checkpoints (Non-Negotiable)

1. XML generation must use native .NET XML APIs (no manual string-builder XML composition).
2. Do not orchestrate Cake task graph by DI-injecting **task classes** into another task. Service-to-service composition within Application is permitted (ADR-002 §2.2 clarification).
3. Setup / pack orchestration belongs to Cake dependency mapping and task graph semantics.
4. Every on-disk path in the Cake build host goes through `IPathService` (memory-locked — "Always Use PathService"). No `Combine` chains, no hardcoded relative-path arrays.
5. Keep fixes scoped; no bundled adjacent cleanup on critical findings (memory: "No Scope Creep On Critical Findings").
6. Narrate findings → propose → wait for ack before commit/push (memory: "Honest Progress Narration").

## Validation Commands Ready At Hand

```bash
# Full solution build (includes Sandbox, smoke csprojs, native + managed src)
dotnet build

# Build-host test suite
dotnet test build/_build.Tests

# Architecture invariants only
dotnet test build/_build.Tests --filter "FullyQualifiedName~LayerDependency"

# Full SetupLocalDev smoke
rm -rf artifacts/packages/*
dotnet run --project build/_build -- --target SetupLocalDev --source=local
dotnet build tests/smoke-tests/package-smoke/PackageConsumer.Smoke/PackageConsumer.Smoke.csproj -c Release

# Drift detection for retired namespaces
rg "Build\.Modules\." build/_build build/_build.Tests
rg "Build\.Tools\." build/_build build/_build.Tests
```

## Required Output Format

Use the reviewer prompt's §11 output contract:

1. Scope And Assumptions.
2. Findings First (severity-ordered, strict format).
3. Broader Systemic Observations.
4. Open Questions / Confidence Limiters.
5. What Was Not Verified.
6. Brief Summary.

For any documentation finding include the exact markdown/XML doc rewrite needed. For any interface-extraction recommendation include the minimal proposed API surface.

## Quality Bar

Do not produce generic review prose.

Each important claim ties back to one of:

1. observed code,
2. test evidence,
3. command output,
4. documented contract (ADR-001, ADR-002, AGENTS.md, release-guardrails.md).

If no significant issues are found, explicitly state that and list residual risk pockets — specifically the Wave 6 fat-task debt, `cake-build-architecture.md` body-prose drift, and pre-existing markdown lint backlog in the ADR-002 file.

If the user pivots to a dependency-modernization ask instead of a review, switch the engine to [.github/prompts/dependency_modernization_and_net_10_updater_prompt_v_1.prompt.md](dependency_modernization_and_net_10_updater_prompt_v_1.prompt.md) and use its §5 "Key upgrade-sensitive locations" list as the start-point.
