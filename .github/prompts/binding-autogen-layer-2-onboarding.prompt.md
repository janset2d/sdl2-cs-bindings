---
name: "Binding Autogen Layer 2 Onboarding"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after pre-Layer-2 doc consolidation landed at 1b32464 on 2026-05-29. SDL2 Layer 1 raw ABI closed across 5 families (Core/Image/GFX/TTF/Mixer); canonical/ tree is the policy home; binding-autogen workstream is ready for Layer 2 typed public API research → roadmap → implementation. Recommended flow: research peer bindings (ppy/Silk.NET/SDL2-CS) → brainstorm Layer 2 design → writing-plans → subagent-driven execution."
argument-hint: "Optional focus area, constraints, or sub-scope (e.g. 'focus on string overloads', 'start with handle adapters', 'compare ppy companion mechanism')"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You're an engineer entering the `janset2d/sdl2-cs-bindings` repo to begin Layer 2 typed public API work. The pre-Layer-2 doc consolidation just closed at commit `1b32464` (2026-05-29): SDL2 Layer 1 raw ABI is closed across all 5 SDL2 families (Core / Image / GFX / TTF / Mixer); canonical policy + roadmap + maintenance + testing + implementation notes live under `spikes/binding-generators/docs/canonical/`. **Most important state observation:** the Layer 2 next milestone is the first FORWARD layer-based work post-consolidation; design has not started yet and must go through brainstorm → spec → plan before implementation.

## First Principle

> Treat every claim in this prompt as **current-as-of-authoring (`2026-05-29` — pre-Layer-2 consolidation)** and verify against the live repo, git log, and canonical docs before acting. The doc tree just moved into `spikes/binding-generators/docs/canonical/`; pre-consolidation references (`docs/binding-autogen/`, `docs/parking-lot/binding-autogen-cake-implementation/`, retired spike top-level docs) no longer exist.

## What Just Happened

The pre-Layer-2 doc consolidation (one revertable squash commit `1b32464`):

**Topology shift**
- `docs/binding-autogen/` (4 canonical files) → `spikes/binding-generators/docs/canonical/`
- 7 satellite analyses moved from `spikes/binding-generators/docs/satellites/` → `canonical/satellites/`
- `ppy-reference-analysis-2026-05-25.md` moved → `canonical/references/`
- `docs/playbook/binding-output-oracle-validation.md` moved → `canonical/`
- Will restore to `docs/binding-autogen/` at Production Flip per roadmap §"Production Flip — SDL2.Core Reproducibility".

**Constitution Q3C aggressive purification** (645 → ~490 lines)
- Pure principles only. Mechanism + evidence moved to NEW `canonical/binding-generator-implementation-notes.md` sibling.
- 4 NET-NEW policy contracts added (M2/M3 audit fold targets): validator-id integrity, cleanup safety, disabled-family side-effect isolation, derivation-failure typing.

**Roadmap Q4D layer-based restructure** (523 → 209 lines)
- M0–M4 collapsed into "What's Done — SDL2 Layer 1 Raw ABI Closure (2026-05-28)".
- M5–M10 renamed to **layer-based forward milestones**: Layer 2 — Typed Public API Projection / Layer 3 — Friendly Overload Projection / Production Flip / SysWM Layout + Satellite Sweep Close / Smoke + Asset-Backed Testing Expansion / SDL3 Extension.
- M-numbering retired throughout.

**NEW docs**
- `canonical/binding-generator-implementation-notes.md` (~688 lines, 13 sections): mechanism, classification labels, hardcoding rules, config-surface evolution, postprocess pipeline order.
- `canonical/binding-generator-maintenance.md` (~647 lines, 11 sections): toolchain pinning quartet, configuration surface map, RSP file maintenance, postprocess pipeline maintenance, version-bump procedures, new-family addition, drift detection, Linux-canonical generation. ClangSharp + Roslyn adapted (sunset Cake playbook retired).

**Retired** (25 files, audit-and-fold first per Rule 1; nothing blind):
- `docs/parking-lot/binding-autogen-cake-implementation/` (4 archived M1/M2/M3 plans)
- `docs/playbook/binding-generator-maintenance.md` (sunset)
- 6 spike top-level docs (`llm-handoff`, `priority-c-closure-summary`, `generator-spike-goals`, `oracle-evidence-design`, `oracle-evidence-implementation-plan`, `satellite-expansion-roadmap`)
- `docs/items/` folder (5 plans + 5 specs)
- `docs/testing/` folder (3 files folded into canonical/testing-strategy.md)
- 3 spike-specific priming prompts under `.github/prompts/`
- 2 legacy oracle-comparison reports

**6 multi-agent review checkpoints passed** (Checkpoints 1-6, PASS or PASS WITH CONCERNS with fixes inline). ADR-004 remains **Reopened** pending Layer 2 closure (formal toolchain amendment deferred).

## Current State You Should Assume Until Verified

- **HEAD**: `1b32464` — `docs(binding-autogen): pre-Layer-2 doc consolidation` (squash; 60 files changed, +7290 / -19863 = -12573 net lines).
- **Branch**: `spike/binding-autogen-sdl2-gfx`, **1 commit ahead** of `origin/spike/binding-autogen-sdl2-gfx`. Push pending Deniz approval per AGENTS.md §Approval Gate.
- **Working tree**: clean.
- **SDL2 Layer 1 raw ABI**: closed across Core / Image / GFX / TTF / Mixer. Generator at `spikes/binding-generators/clangsharp/`. Multi-TFM clean across `net10.0` / `net9.0` / `net8.0` / `netstandard2.0` / `net462`. Oracle clean (5-family report at `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`).
- **ADR-004**: Reopened; formal amendment deferred to post-Layer-2.
- **Active iteration plan**: `spikes/binding-generators/docs/next-iteration-plan.md` (Layer 2 framing + Forward Backlog: Production Flip Gates / Layer 2-Layer 3 Follow-ups / Accepted Tradeoffs).

## Three-Phase Approach (RESEARCH → ROADMAP → IMPLEMENT)

This is the explicit shape Deniz asked for. Do not skip phases or compress them.

### Phase A — Research peer bindings (Layer 2/3 ergonomic patterns)

Goal: catalog *how* peers solve the Layer 2 typed public API + Layer 3 friendly overload problems. Output: research findings doc (likely `docs/superpowers/research/2026-MM-DD-layer-2-peer-research.md` or under `spikes/binding-generators/docs/canonical/references/` — your judgment, see grounding).

**Peer targets** (in priority order):

1. **ppy/SDL3-CS — source generation approach**. The Roslyn source generator at `references/ppy-SDL3-CS/SDL3-CS.SourceGeneration/FriendlyOverloadGenerator.cs` (~192 lines) is the densest evidence. Understand: (a) how it consumes `[NativeTypeName("const char *")]` annotations to emit string overloads, (b) the `Unsafe_` prefix convention contract between Python orchestrator (which remaps `const char*`-returning functions to `Unsafe_X`) and the SG (which looks for `Unsafe_`-prefixed methods), (c) the `Utf8String` ref-struct mechanism (`SDL3-CS/Utf8String.cs`, ~57 lines) for zero-alloc UTF-8 string overloads. **Important: ppy uses Roslyn SG at consumer-build time; our Constitution requires committed `.g.cs` + reproducibility stamp (M7 Production Flip), so we adapt the same transforms as POSTPROCESS rewriter steps, not SG.** See `canonical/binding-generator-constitution.md` §"Generator Home" + `canonical/binding-generator-implementation-notes.md` §13.

2. **ppy/SDL3-CS — companion class files**. ppy has ~25 hand-written companion `.cs` files (e.g. `SDL_video.cs`, `SDL_events.cs`, `SDL_pixels.cs`, `SDL_version.cs`) that augment generated structs with: typed property accessors (`SDL_CommonEvent.Type` returning `SDL_EventType` enum over raw `uint type` field), string helpers (`SDL_TextInputEvent.GetText()` calling `SDL3.PtrToStringUTF8`), array-returning friendly wrappers (`SDL_GetDisplays()` returning `SDLArray<SDL_DisplayID>` with `IDisposable` + auto-`SDL_free`), and `[Typedef]`/`[Macro]` markers for the Python orchestrator to consume as `--remap` / `--exclude` feedback. **Deniz has rejected "magic companion" patterns** (per memory + spike charter); we systematically GENERATE these transforms via Layer 2 projection rather than hand-write. Catalog every shape of ergonomic transform ppy adds and decide: which generate cleanly from `[NativeTypeName]` + structural inference vs. which need explicit policy config? See `canonical/references/ppy-reference-analysis-2026-05-25.md` Category 1-5 for the prior analysis baseline.

3. **Silk.NET binding patterns**. Read `references/silk-net/` (clone via `docs/reference-clones.md` if not present). Silk.NET has broad generated-interop scale; useful for span/ref overload conventions, low-level binding ergonomics, and how a large-scale generator handles cross-cutting friendly-overload generation. Note Silk.NET drops `[Flags]` entirely (rejected by our Constitution §"Enums") — don't copy that pattern, but study how their handle / typed-overload projection composes.

4. **SDL2-CS — compatibility surface**. `external/sdl2-cs/src/SDL2.cs` (and the satellite files). SDL2-CS is the de-facto legacy public API for SDL2; our Constitution §"Authority Order" treats it as **compatibility signal, not target API truth** (`IntPtr` everywhere is not our target). Goal: catalog where the **shape** of SDL2-CS Layer 2 mirrors what we should generate (function names, parameter order, overload sets) vs. where it diverges (typed handles vs `IntPtr`, friendly string overloads vs `byte*`). This anchors the SDL2-CS-compatibility-as-best-effort posture in v1.

**Discipline (all 4 peers):**
- Constitution citations are how decisions get authority — every research finding should ideally trace to a section in `canonical/binding-generator-constitution.md` (`§"Layer Contract"`, `§"Opaque Handles"`, `§"BCL-Replaceable Helper Exclusion Policy"`, `§"Foreign Type Boundary Policy"`, etc.).
- Peer bindings are evidence, **never authority** (Constitution §"Authority Order" rank 6). If a peer pattern conflicts with our policy, the policy wins.
- Layer 2 work happens on the SAME mechanism backbone as Layer 1 — postprocess rewriter steps over committed `.g.cs`, NOT Roslyn source generators (Constitution §"Generator Home"). Reframe every peer SG mechanism as "what would the equivalent postprocess rewriter do?"

### Phase B — Roadmap (brainstorm → design spec)

Once research findings are landed, invoke `superpowers:brainstorming` to design Layer 2 scope + exit evidence + non-goals. The brainstorm should produce a design spec at `docs/superpowers/specs/YYYY-MM-DD-binding-autogen-layer-2-typed-public-api-design.md`.

Brainstorm targets:

- **Public API surface shape** — public methods on the manifest-driven public class (e.g., `SDL2.SDL`) calling internal raw ABI. Constitution §"Layer Contract" Layer 2 + Layer Contract `Why/How/What` (L34-50) is the framing.
- **Typed handle ergonomics** — Pattern B handles (`SDL_Window`, `SDL_Renderer`, etc.) are already emitted by Layer 1's `OpaqueHandleEmitRewriter` (per `canonical/binding-generator-implementation-notes.md` §3). Layer 2 needs handle-typed method signatures + `out`/`ref` ergonomics. Decide which transforms are mechanical (postprocess rewriter) vs. policy-driven.
- **Enum projection** — typed `enum` parameters/returns over raw `int`/`uint`. Cross-check `canonical/binding-generator-implementation-notes.md` §8 (`[Flags]` Detection).
- **Struct field ergonomics** — typed property accessors over raw fields (ppy Category 3 pattern). Decide: emit alongside the generated struct via postprocess rewriter? Cross-check ABI safety (no layout drift).
- **String overloads** — Layer 3 territory but research informs Layer 2 design. `string` / `ReadOnlySpan<byte>` / `Utf8String`-equivalent.
- **Array-returning friendly wrappers** — ppy `SDLArray<T>` pattern; decide infrastructure types we generate vs. hand-write.
- **Callback registration ergonomics** — typed `delegate*`/`Action`/`Func` over function-pointer signatures; lifetime/rooting policy (Mixer 4-element deferred policy from `next-iteration-plan.md` Forward Backlog).
- **Layer 2 vs Layer 3 split** — Constitution Layer Contract puts typed low-level in Layer 2 and friendly ergonomics in Layer 3. Find the seam: when does a typed method become "friendly"?

**Spec output:** must include scope, exit evidence, non-goals, multi-agent review checkpoints (mirror the consolidation's 6-checkpoint discipline at smaller scale). Reference ADR-002 (target-centric build host) + ADR-003 (data layer) + ADR-004 (binding-autogen toolchain — Reopened) per repo memory "Plan-doc reference discipline".

### Phase C — Implement (writing-plans → subagent-driven-development)

After Layer 2 spec lands and Deniz approves, invoke `superpowers:writing-plans` to produce an implementation plan at `docs/superpowers/plans/YYYY-MM-DD-binding-autogen-layer-2-typed-public-api-plan.md`. Then execute via `superpowers:subagent-driven-development` per the consolidation's pattern (fresh subagent per task; controller-side rigorous review; multi-agent review checkpoints at gates; WIP commits → squash via `git reset --soft` at the end per memory "Single changeset via branch+squash").

**Mechanism prediction (not policy — re-verify during Phase A research):** Layer 2 likely adds 1-3 new postprocess rewriters to the existing 7-step pipeline:
- A `public-typed-method-projection` rewriter emitting public methods over internal `SDLNative` extern declarations.
- A `handle-parameter-rewrite` rewriter applying Pattern B handle types at public-method parameter positions.
- A `typed-enum-projection` rewriter projecting raw int/uint return/parameter positions to typed enum types where the `[NativeTypeName]` annotation marks the enum.

These predictions feed the brainstorm; don't lock them in before research validates.

## Mandatory Grounding (read in this order)

1. `docs/onboarding.md` (project overview, repo layout, glossary)
2. `AGENTS.md` (operating rules, approval gates, settled strategic decisions)
3. `CLAUDE.md` (relay to AGENTS.md)
4. `docs/plan.md` Phase 4 section (current tactical state — paths point at canonical/ post-consolidation)
5. `docs/phases/phase-4-binding-autogen.md` (Phase 4 scope brief — Stage 1/2/3 sequencing)
6. `docs/release-strategy.md` §Sequencing + §Promotion Gates (PD-7 + AST-first context)
7. **`spikes/binding-generators/docs/canonical/README.md` (canonical workstream index — start here for the binding-autogen tree)**
8. `spikes/binding-generators/docs/canonical/binding-generator-constitution.md` (ABI/API/translation policy — pure principles)
9. `spikes/binding-generators/docs/canonical/binding-generator-roadmap.md` (forward layer-based milestones — focus on Layer 2 Typed Public API Projection)
10. `spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md` (mechanism + classification labels + hardcoding rules — focus on §10 Oracle Classification Labels and §13 Postprocess Pipeline Mechanism Detail)
11. `spikes/binding-generators/docs/canonical/testing-strategy.md` (Test Categories + Coverage Strategy — Layer 2 work needs new public-API snapshot tests)
12. `spikes/binding-generators/docs/canonical/satellites/` (per-family analyses — read the family you're prototyping Layer 2 on first; Core is the natural starting family)
13. `spikes/binding-generators/docs/canonical/references/ppy-reference-analysis-2026-05-25.md` (prior peer analysis baseline — Phase A research extends this)
14. `spikes/binding-generators/docs/next-iteration-plan.md` (active iteration tracking + Forward Backlog: Production Flip Gates / Layer 2-Layer 3 Follow-ups / Accepted Tradeoffs)
15. `docs/decisions/2026-05-14-binding-autogen-toolchain.md` (ADR-004 Reopened — toolchain history; do not amend this ADR during Layer 2 work; formal amendment happens at Layer 2 closure)
16. `docs/knowledge-base/extraction-guidelines.md` + `docs/knowledge-base/testing-guidelines.md` (collaborator-extraction discipline + test-infrastructure canon)
17. `docs/reference-clones.md` (how to set up `references/` clones for Phase A — gitignored)

Skip anything not load-bearing for your current focus. Don't pad reading with everything.

## Layer 2 Specific Anchors

These are the policy + mechanism sections most likely to govern Layer 2 design decisions:

- Constitution §"Layer Contract" — three-layer API shape (internal raw ABI / public typed low-level / friendly overloads).
- Constitution §"Internal Raw ABI: Why / How / What" — why raw stays internal even with lexically-public generated methods.
- Constitution §"Family Identity" table — namespace + public class per family.
- Constitution §"Opaque Handles" — Pattern B handle contract; cross-family handle name resolution; cross-assembly `[assembly: DisableRuntimeMarshalling]`.
- Constitution §"Foreign Type Boundary Policy" — IntPtr-at-foreign-boundary discipline.
- Constitution §"BCL-Replaceable Helper Exclusion Policy" — 3-condition exclusion rule.
- Constitution §"Evidence Gates" — what Layer 2 exit evidence must satisfy.
- Implementation-notes §3 (Opaque Handles Implementation Mechanism), §4 (C `long` Hybrid), §10 (Classification Labels), §13 (Postprocess Pipeline).
- Maintenance §3 (Configuration Surface Map), §5 (Postprocess Pipeline Maintenance), §7 (Adding A New SDL Satellite Family — for the per-family Layer 2 rollout pattern).
- next-iteration-plan §Forward Backlog — Production Flip Gates list Layer 2 prerequisites; Layer 2/Layer 3 Follow-ups list the Mixer-callback-policy and similar deferred decisions.

## Locked Policy Recap

Most-likely-to-be-tempting-to-violate rules in upcoming Layer 2 work:

- **Approval Gate** — no commits without explicit `go / yap / apply / proceed / başla`. Documentation-only edits are exceptions per AGENTS.md but the gate still requires presenting summary + proposed commit message first.
- **Single changeset via branch+squash** — Layer 2 implementation lands as ONE revertable squash commit; work branch + WIP checkpoints + `git reset --soft` at the end. No worktrees per user memory.
- **Master-direct commits acceptable** — PRs are optional in this repo; direct commits OK; reference issues with `refs #N` / `closes #N`.
- **Internal Raw ABI stays internal** — Layer 2 public methods call the internal `SDLNative` container; never lift the raw ABI class to public. Constitution §"Layer Contract".
- **No Roslyn source generators for committed output** — Layer 2 transforms land as POSTPROCESS rewriter steps producing committed `.g.cs`. Roslyn SG runs at consumer-build time and can't be committed (Constitution §"Generator Home").
- **No `magic` companion files** — Deniz has explicitly rejected hand-written companion-class augmentation patterns. Layer 2 ergonomic transforms are GENERATED via postprocess.
- **No JSON ABI policy knobs** — Constitution §"Manifest Configuration Vs Code-Owned Policy" forbids putting ABI-shape decisions in JSON; manifest holds *facts*, code holds *policy*. Don't introduce a new `family-config.json` field that encodes Layer 2 typed-method emit shape; that's policy and stays in code.
- **`docs/binding-autogen/` paths are stale** — post-consolidation, every binding-autogen canonical doc lives at `spikes/binding-generators/docs/canonical/`. References in new docs must use the canonical path (will restore to `docs/binding-autogen/` at Production Flip per roadmap).
- **Audit-fold gate (Rule 1) + CppAst-independent fold (Rule 2) + section-name cross-refs (Rule 3)** — the three rules from the consolidation apply to any subsequent doc work in this area. Carry forward.
- **Multi-agent review at critical stages** — Deniz emphasized this in the consolidation. Layer 2 spec + plan should include review checkpoints at post-research / post-design / post-implementation gates.

## Final Steering Note

Layer 2 is the first forward layer-based work after the Layer 1 raw ABI + consolidation closure. The canonical/ tree is the new home; the spike implementation under `spikes/binding-generators/clangsharp/` is the production-track surface. Take Phase A research seriously — Deniz called it out explicitly. ppy's source generation approach is the densest peer evidence we have, but the adaptation discipline (postprocess rewriter vs Roslyn SG; generated vs companion) is what separates a clean Layer 2 from a Silk.NET-style sprawl. Brainstorm only after research is complete; spec only after brainstorm. Hold the policy line.

The Layer 1 raw ABI surface is stable enough to project onto. The path forward is clear; the discipline is yours to keep.
