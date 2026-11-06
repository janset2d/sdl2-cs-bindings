# Binding Auto-Generation Workstream

> **Status:** Canonical binding-generator workstream index. Pure-principles policy + mechanism + maintenance + testing + satellite analyses for the active spike implementation under `spikes/binding-generators/clangsharp/`. Restores to `docs/binding-autogen/` at Production Flip (see roadmap §"Production Flip — SDL2.Core Reproducibility").

## Folder Layout

| Path | Purpose |
| --- | --- |
| [`README.md`](README.md) | Workstream index and reading order. |
| [`binding-abi-policy.md`](../../../../docs/architecture/binding-abi-policy.md) | Canonical ABI/API policy: internal raw ABI, public typed low-level API, friendly overloads, manifest config vs code-owned policy, evidence gates. |
| [`binding-generator-roadmap.md`](binding-generator-roadmap.md) | Canonical roadmap: SDL2 Layer 1 closure record + Layer 2 / Layer 3 / Production Flip / SysWM + Satellite Sweep / Smoke Expansion / SDL3 Extension. |
| [`binding-testing.md`](../../../../docs/engineering/binding-testing.md) | Canonical testing strategy: layer model, fixture policy, raw ABI runtime tests, smoke expansion backlog, raw ABI upstream port backlog. |
| [`binding-generation.md`](../../../../docs/architecture/binding-generation.md) | Mechanism + evidence: postprocess rewriter design, oracle classification labels, hardcoding rules, config surface evolution, postprocess pipeline order. |
| [`binding-generator-maintenance.md`](../../../../docs/engineering/binding-generator-maintenance.md) | Maintenance playbook: version-bump procedures, RSP file maintenance, family-config.json schema, postprocess pipeline maintenance, drift detection, new-family addition checklist. |
| [`binding-output-review.md`](../../../../docs/engineering/binding-output-review.md) | Multi-oracle review workflow for validating generated output. |
| [`satellites/`](satellites/) | Per-family header analyses + cross-family consolidation analyses. |
| [`references/`](references/) | Peer reference analyses (ppy/SDL3-CS, etc.). |

## Reading Order

| # | Document | Purpose |
| --- | --- | --- |
| 1 | [`binding-abi-policy.md`](../../../../docs/architecture/binding-abi-policy.md) | Read first. ABI/API law and evidence gates. |
| 2 | [`binding-generator-roadmap.md`](binding-generator-roadmap.md) | Read second. Layer-based forward sequencing. |
| 3 | [`binding-testing.md`](../../../../docs/engineering/binding-testing.md) | Read before changing tests, snapshots, smoke tests, fixtures. |
| 4 | [`binding-generation.md`](../../../../docs/architecture/binding-generation.md) | Read for mechanism rationale (rewriter design, hardcoding rules, classification labels). |
| 5 | [`binding-generator-maintenance.md`](../../../../docs/engineering/binding-generator-maintenance.md) | Read for operational procedures (version bumps, RSP edits, family-config schema). |
| 6 | [`binding-output-review.md`](../../../../docs/engineering/binding-output-review.md) | Read before promoting output toward production source. |
| 7 | [`satellites/`](satellites/) | Read when designing Layer 2/3 projection for a specific SDL2 family. |
| 8 | [`references/ppy-reference-analysis-2026-05-25.md`](references/ppy-reference-analysis-2026-05-25.md) | Read for peer reference (orchestrator architecture, companion class layer mapping). |

## Current Decision Posture

- **Toolchain:** ClangSharp + Roslyn postprocess is the active implementation under [`spikes/binding-generators/clangsharp/`](../../clangsharp/) and the direction-of-record for Layer 2+ work. Formal ADR-004 amendment deferred to post-Layer-2 closure.
- **Output contract** per Layer Contract in the constitution: internal raw ABI externs + public typed low-level API + friendly overloads. Internal raw container blocks public package API exposure. Public raw `IntPtr` externs not part of v1 preview.
- **Family identity** is manifest-driven; namespace + public class per Constitution Family Identity table.
- **Generator is build infrastructure** — committed `.g.cs` source, per-family `.generated-stamp` with reproducibility metadata at Production Flip, validators reachable from PreFlight and Pack stages.
- **Linux-canonical generation** for ABI correctness across the 7-RID surface.
- **SDL3** is gated on PD-7 (SDL2 real-public-release) per release-strategy.

## Related Canonical Docs

- [`../../../../docs/phases/phase-4-binding-autogen.md`](../../../../docs/phases/phase-4-binding-autogen.md) — Phase 4 design brief.
- [`../../../../docs/release-strategy.md`](../../../../docs/release-strategy.md) — AST-first sequencing and v1.0 release strategy.
- [`../../../../docs/ROADMAP.md`](../../../../docs/ROADMAP.md) — tactical roadmap.
- [`../../../../docs/adr/0002-target-centric-build-host.md`](../../../../docs/adr/0002-target-centric-build-host.md) — ADR-002 target-centric build-host pattern.
- [`../../../../docs/adr/0003-build-host-data-layer.md`](../../../../docs/adr/0003-build-host-data-layer.md) — ADR-003 contract-centric data layer pattern.
- [`../../../../docs/adr/0004-binding-autogen-toolchain.md`](../../../../docs/adr/0004-binding-autogen-toolchain.md) — ADR-004 binding-autogen toolchain (status: Reopened; amendment deferred).
- [`../../../../docs/engineering/coding-style.md`](../../../../docs/engineering/coding-style.md) — collaborator extraction discipline.
- [`../../../../docs/engineering/testing-style.md`](../../../../docs/engineering/testing-style.md) — TUnit/MTP test infrastructure.
- [`../../../../AGENTS.md`](../../../../AGENTS.md) — operating rules and settled project decisions.
