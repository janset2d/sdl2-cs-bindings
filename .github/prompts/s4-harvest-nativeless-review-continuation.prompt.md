---
name: "S4 Harvest + Nativeless Review Continuation"
description: "Priming prompt for the next agent to continue from current state with a review-first mission: Harvest as golden module, Cake nativeless/empty-payload safety, .NET best practices, steering compliance, and evidence-backed remediation."
argument-hint: "Optional focus area (e.g., Harvest architecture, SetupLocalDev flow, G46/G55/G56, solution-build blockers, docs drift)"
agent: "agent"
model: "GPT-5 (copilot)"
---

You are entering `janset2d/sdl2-cs-bindings` mid-stream. Continue from current working tree state.

Primary objective: perform a rigorous, evidence-first code review with minimal, high-confidence fixes where needed.

The review is not generic. It must prioritize:
1. Harvest module as the golden module/reference shape.
2. Cake nativeless/empty-native-payload safety and orchestration correctness.
3. .NET best practices that materially improve correctness/maintainability (not style churn).
4. Explicit coverage of user steering/intervention checkpoints.

## Hard Steering Checkpoints (Non-Negotiable)

These are user-directed interventions. Treat them as load-bearing constraints:

1. XML generation must use native .NET XML APIs (no manual string-builder XML composition for this flow).
2. Do not orchestrate Cake task graph by DI-injecting task classes into another task.
3. Setup/pack orchestration belongs to Cake dependency mapping and task graph semantics.
4. Keep fixes scoped; avoid bundling unrelated cleanup.

If code contradicts any of these, raise it as a high-priority finding and fix with minimal blast radius.

## Current State Capsule (Start Here)

You are not starting from zero. Current known state:

1. `SetupLocalDev` is back on Cake dependency mapping (`Info -> EnsureVcpkgDependencies -> Harvest -> ConsolidateHarvest -> SetupLocalDev`), not DI task chaining.
2. Local setup (`--source=local`) now completes and generates `build/msbuild/Janset.Smoke.local.props`.
3. G55 metadata issue was addressed by ensuring native metadata is packed through native project packing inputs.
4. G56 was addressed via managed nupkg dependency range normalization (cross-family upper bound compatibility).
5. Build-host tests are green in current baseline (`build/_build.Tests`, 342 passing).
6. Solution restore/package-feed issue was reduced; remaining solution build breakage is currently in `tests/Sandbox/Program.cs` analyzer/nullability quality errors, not NU1101 feed resolution.

Validate every bullet against current code and command output before trusting it.

## Mandatory Grounding (Read Before Reviewing)

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `docs/plan.md`
4. `docs/knowledge-base/cake-build-architecture.md`
5. `docs/knowledge-base/harvesting-process.md`
6. `docs/knowledge-base/release-guardrails.md`
7. `docs/playbook/local-development.md`
8. `docs/reviews/2026-04-18-consolidated-review-index.md`
9. `docs/decisions/2026-04-18-versioning-d3seg.md`

If docs and runtime code conflict, prefer runtime evidence and call out drift explicitly.

## Review Mission (Priority Order)

1. Correctness/regression risk in current working tree changes.
2. Harvest golden-module alignment across touched build-host modules.
3. Cake nativeless safety model:
   - `NativePayloadSource` contract,
   - G46 empty payload guard,
   - G55 metadata presence/shape,
   - G56 cross-family dependency upper bound,
   - SetupLocalDev source-profile behavior.
4. .NET best practices with real payoff:
   - error/result boundaries,
   - cancellation propagation,
   - DI boundaries,
   - analyzer compliance,
   - testability seams.
5. Documentation drift and guardrail documentation correctness.

## Focus Files / Areas

Start with these clusters, then expand as evidence dictates:

1. Harvest / consolidation:
   - `build/_build/Tasks/Harvest/HarvestTask.cs`
   - `build/_build/Tasks/Harvest/ConsolidateHarvestTask.cs`
   - `build/_build/Modules/Harvesting/*`

2. Cake nativeless + packaging guards:
   - `src/Directory.Build.targets`
   - `src/native/Directory.Build.props`
   - `build/_build/Modules/Packaging/PackageTaskRunner.cs`
   - `build/_build/Modules/Packaging/PackageOutputValidator.cs`
   - `build/_build/Modules/Packaging/SatelliteUpperBoundValidator.cs`
   - `build/_build/Modules/Packaging/NativePackageMetadata*.cs`

3. SetupLocalDev + source profile orchestration:
   - `build/_build/Tasks/Packaging/SetupLocalDevTask.cs`
   - `build/_build/Modules/Packaging/LocalArtifactSourceResolver.cs`
   - `build/_build/Modules/Packaging/RemoteInternalArtifactSourceResolver.cs`
   - `build/_build/Modules/Packaging/ReleaseArtifactSourceResolver.cs`
   - `build/msbuild/Janset.Smoke.props`

4. Composition root + tests:
   - `build/_build/Program.cs`
   - `build/_build.Tests/Unit/CompositionRoot/ProgramCompositionRootTests.cs`
   - `build/_build.Tests/Unit/Modules/Packaging/*`
   - `build/_build.Tests/Unit/Tasks/Packaging/*`

## Execution Contract

1. Review-first, evidence-first.
2. When a finding is high-confidence and local, apply a minimal fix immediately.
3. Re-run targeted tests first, then full `build/_build.Tests` suite.
4. If behavior changed, update docs in same pass.
5. Never hide uncertainty: mark assumptions and evidence quality.

## Required Output Format

Produce output in this exact order:

1. Stage Summary:
   - where we were,
   - what changed,
   - where we are now.

2. Steering Compliance Matrix:
   - each steering checkpoint,
   - status (`pass`/`fail`/`partial`),
   - evidence (file + line refs).

3. Findings (severity ordered):
   - Critical/High/Medium/Low,
   - concrete risk,
   - file + line references,
   - recommended action.

4. Fixes Applied:
   - what changed,
   - why,
   - risk/compatibility notes.

5. Validation:
   - commands executed,
   - important outputs,
   - pass/fail.

6. Docs and Guardrails:
   - updated docs,
   - any doc drift still open,
   - guardrail impact summary (G46/G55/G56 and related).

7. Remaining Risks / Next Actions:
   - crisp, numbered, execution-ready.

## Quality Bar

Do not produce generic review prose.

Each important claim must tie back to one of:
1. observed code,
2. test evidence,
3. command output,
4. documented contract.

If no significant issues are found, explicitly state that and list residual risk pockets (for example: sandbox analyzer debt vs package-feed correctness).
