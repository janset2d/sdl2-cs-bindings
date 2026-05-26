---
name: "Iteration 2 Config Surface Unification — Spec + Plan Handoff"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings for Iteration 2 Config Surface Unification. As of 2026-05-26, the spec has been drafted at spikes/binding-generators/docs/items/iteration-2-config-surface-unification-spec.md. Recommended next: review the existing spec, verify the config surface inventory, discuss with Deniz, then write the implementation plan."
argument-hint: "Optional focus area: 'onboard' | 'write-spec' | 'write-plan' | 'review-inventory' | custom"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering `janset2d/sdl2-cs-bindings` to continue the **ClangSharp + Roslyn postprocess binding-generator spike** under `spikes/binding-generators/clangsharp/`. The previous wave closed **Item 1: Per-Library Generation Infrastructure**. The next wave is **Iteration 2: Config Surface Unification**: turn the roadmap placeholder into a reviewed spec and implementation plan before Items 2-5 activate more SDL2 satellite families.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-26 — Item 1 closed at `923c78d`)** and verify against the live repo, `git log`, `git status`, and canonical docs before acting. This prompt is a handoff, not authority. The authoritative Iteration 2 seed is `spikes/binding-generators/docs/satellite-expansion-roadmap.md` §"Iteration 2 — Config Surface Unification".

## What Just Happened

### Item 1 closed

Item 1 delivered the cross-family infrastructure that future satellite expansion depends on:

| Area | Current state |
| --- | --- |
| Per-family generation | `--family all`, `--family core`, and `--family image` regenerate byte-stable Core/Image output. `selected_families("all")` intentionally remains `['core', 'image']`; TTF/Mixer/GFX are dormant until their expansion items activate them. |
| Postprocess pipeline | 7 conceptual steps: `platform-delta`, `strip-varargs`, `libraryimport` (Modern only), `flags-detect`, `guid-substitute`, `clong-dispatch`, `uniform-opaque`. |
| Policy rosters | `opaque-handle-roster.json` and `flags-enum-roster.json` are family-keyed schema 2.0 inputs. |
| C `long` handling | `ClongDualDispatchRewriter` covers Core and dormant TTF C `long` method metadata, including return and parameter positions. |
| Flags handling | `FlagsAttributeRewriter` uses suffix + allow-list policy; `IMG_InitFlags` is already fixed as an Item 1 S1-6 side effect. |
| Oracle | Five-family oracle wiring exists. Core/Image are clean; TTF/Mixer/GFX generated rows are missing as expected. |
| Evidence | Solution build clean; AbiTests passed `792/792` across Windows `net462`, `net8.0`, `net9.0`, `net10.0`; Slopwatch clean at closure. |

### Why Iteration 2 exists

The post-Item-1 spike consumes configuration from **18 sources**: 9 external files and 9 code-internal data structures. The dispersion is partly intentional, because the Constitution separates manifest-owned configuration from code-owned ABI/API policy. The problem is that the current split grew incrementally; it has not yet been deliberately classified against that policy.

Three risks are already named in the roadmap:

- **Drift between source types.** Family identity facts such as `namespace`, `raw_class`, and `library_dir` live in `generate_bindings.py` while related identity exists in `build/manifest.json` `package_families[]` / `library_manifests[]`.
- **Mixed config-vs-policy footprint.** Some family-varying facts live in code, some in external roster/scope/RSP files, and the boundary is not consistently justified.
- **Discoverability for new agents.** Answering "where does family X config live?" requires inspecting multiple files plus code dictionaries.

Iteration 2 is not a cleanup-for-cleanup's-sake pass. Its job is to make the config surface intentional before GFX/TTF/Mixer add more family-specific data and make the mess expensive. Classic "clean the kitchen before cooking for five people" energy.

## Iteration 2 Scope

### Binding constraints

These are fixed unless Deniz explicitly reopens them:

- **Do not touch `build/manifest.json` in Iteration 2.** Manifest reintegration belongs to Roadmap M7 production flip, not this spike-local unification.
- **Preserve Constitution §"Generation Determinism Contract".** No timestamps, machine paths, network inputs, host-derived behavior, or cross-family generated-file reads.
- **Preserve family isolation.** `--family X --execute` must continue to write only that family's `Generated/` root.
- **Preserve selected-family dormancy.** `selected_families("all")` remains Core+Image until Items 3/4/5 intentionally activate more families.
- **Do not start Items 2-5 during Iteration 2.** The next wave is spec + plan first, then config unification implementation, then satellite expansion.
- **Do not introduce a broad "configuration framework" because it feels enterprise-y.** Consolidate only where the inventory analysis proves a source-of-truth improvement.

### What Iteration 2 must decide

The spec/planning cycle owns these decisions:

1. Classify all 18 sources against Constitution §"Manifest Configuration Vs Code-Owned Policy".
2. Decide which sources remain code-owned policy, which become external auditable inputs, and which should be derived from existing sources.
3. Decide whether `FAMILY_CONFIG`, `PLATFORM_SENSITIVE_HEADERS`, `CODEGEN_CONFIG`, or `selected_families("all")` should be split, moved, derived, or left alone with better documentation.
4. Decide whether any external sources can merge without losing per-family auditability, especially the two roster JSONs and RSP tiers.
5. Define source-of-truth ordering for overlapping concepts, especially family identity in `generate_bindings.py` versus manifest package/library facts.
6. Define exact verification gates that prove determinism and family isolation survived the reshuffle.

### 18-source inventory seed

Start from the roadmap inventory, then verify each path and line number in the live repo.

External files:

| # | Source | Path / location | Notes |
| --- | --- | --- | --- |
| 1 | Header lists | `spikes/binding-generators/scope/sdl2-<family>.headers.txt` | Per-family native headers. Only active families have files today. |
| 2 | Core required surface | `spikes/binding-generators/scope/sdl2-core-sdlh-required.json` | Core `SDL.h` required functions/constants. |
| 3 | Platform shims | `spikes/binding-generators/clangsharp/shims/platform-headers/*.h` | Windows-local synthetic parse aids. |
| 4 | Opaque roster | `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json` | Family-keyed Pattern B handle policy. |
| 5 | Flags roster | `spikes/binding-generators/clangsharp/policy/flags-enum-roster.json` | Family-keyed `[Flags]` allow-list. |
| 6 | RSP tiers | `spikes/binding-generators/clangsharp/rsp/base.rsp`, `sdl2-<family>.rsp`, `per-header/<header>.rsp` | ClangSharp remap/exclude/define inputs. |
| 7 | Manifest | `build/manifest.json library_manifests[]` | Pin source and future production config source. Do not mutate in Iteration 2. |
| 8 | Tool pinning | `.config/dotnet-tools.json` / spike tool manifest as applicable | ClangSharp tool version. Verify exact active path. |
| 9 | Native pins | `vcpkg.json` + overlay triplets | Native header content input. Do not mutate unless the user explicitly reopens scope. |

Code-internal structures:

| # | Source | Location to verify | Notes |
| --- | --- | --- | --- |
| 10 | `FAMILY_CONFIG` | `spikes/binding-generators/clangsharp/generate_bindings.py` | Per-family namespace, raw class, RSP, scope filename, library directory. |
| 11 | `PLATFORM_SENSITIVE_HEADERS` | `generate_bindings.py` | Per-family platform-view parse lists. |
| 12 | `ALL_PLATFORM_MACROS` | `generate_bindings.py` | Platform define inventory. |
| 13 | `SDL2_PLATFORM_VIEWS` | `generate_bindings.py` | Seven platform view definitions. |
| 14 | `CODEGEN_CONFIG` | `generate_bindings.py` | Compat/Modern ClangSharp config flags. |
| 15 | `selected_families("all")` | `generate_bindings.py` | Dormant activation set. |
| 16 | `AffectedMethodNames` | `postprocess/ClongDualDispatchRewriter.cs` | Core + dormant TTF C `long` symbol set. |
| 17 | Flags suffix rule | `postprocess/FlagsAttributeRewriter.cs` | `EndsWith("Flags")` policy. |
| 18 | GUID substitution | `postprocess/GuidSubstitutionRewriter.cs` | `SDL_GUID` -> `System.Guid` policy. |

## Required Workflow For The Next Agent

### 1. Onboard before proposing anything

Read in this order:

1. `docs/onboarding.md`
2. `AGENTS.md`
3. `docs/plan.md`
4. `docs/phases/README.md`
5. `docs/README.md`
6. `spikes/binding-generators/README.md`
7. `spikes/binding-generators/docs/llm-handoff.md`
8. `spikes/binding-generators/docs/next-iteration-plan.md`
9. `spikes/binding-generators/docs/satellite-expansion-roadmap.md` §"Iteration 2 — Config Surface Unification"
10. `docs/binding-autogen/binding-generator-constitution.md` §"Generation Determinism Contract", §"Manifest Configuration Vs Code-Owned Policy", and §"Family Identity"
11. `spikes/binding-generators/docs/items/item-1-per-library-generation-spec.md` §5.1-§5.7 and §6
12. `spikes/binding-generators/docs/priority-c-closure-summary.md`

Then verify live state:

```pwsh
git branch --show-current
git log --oneline -5
git status --short
```

At prompt authoring, expected branch was `spike/binding-autogen-sdl2-gfx`, latest commit was `923c78d docs(bindings): close item 1 generation handoff`, and the worktree was clean. Verify; don't trust stale pickup text.

### 2. Review the existing spec

The Iteration 2 spec exists at `spikes/binding-generators/docs/items/iteration-2-config-surface-unification-spec.md` (status: draft/proposed). Read it. Then ask Deniz:

> For Iteration 2, should the default bias be **minimal consolidation** (keep most sources, document and derive only obvious duplicates), **moderate unification** (move family identity/config into one spike-local config file), or **aggressive unification** (larger schema consolidation), assuming `build/manifest.json` remains untouched?

Do not ask five questions at once. One question, then adapt.

### 3. Verify inventory, then write plan

After Deniz approves the spec, use the `writing-plans` skill.

### 3. Write the implementation plan after spec approval

Use the `writing-plans` skill. Suggested plan path:

`spikes/binding-generators/docs/items/iteration-2-config-surface-unification-plan.md`

Plan expectations:

- TDD/characterization-first for any behavior-moving code changes.
- Bite-sized tasks with exact paths, commands, expected outputs.
- Per-slice verification including `generate_bindings.py --self-test`, postprocess self-test if touched, full regen if generation behavior changes, solution build, oracle where relevant, `git diff --check`, and Slopwatch for C#/project/test changes.
- Explicit commit approval gates. Do not commit without Deniz's approval.

## Files And Concepts Most Likely To Matter

Read these when preparing the inventory and spec:

- `spikes/binding-generators/clangsharp/generate_bindings.py`
- `spikes/binding-generators/clangsharp/oracle.cs`
- `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json`
- `spikes/binding-generators/clangsharp/policy/flags-enum-roster.json`
- `spikes/binding-generators/clangsharp/rsp/base.rsp`
- `spikes/binding-generators/clangsharp/rsp/sdl2-core.rsp`
- `spikes/binding-generators/clangsharp/rsp/sdl2-image.rsp`
- `spikes/binding-generators/clangsharp/rsp/per-header/`
- `spikes/binding-generators/scope/sdl2-core.headers.txt`
- `spikes/binding-generators/scope/sdl2-image.headers.txt`
- `spikes/binding-generators/scope/sdl2-core-sdlh-required.json`
- `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs`
- `spikes/binding-generators/clangsharp/postprocess/FlagsAttributeRewriter.cs`
- `spikes/binding-generators/clangsharp/postprocess/GuidSubstitutionRewriter.cs`
- `build/manifest.json` (read-only for Iteration 2)
- `vcpkg.json` and overlay triplets (read-only unless explicitly reopened)

## Guardrails To Keep In Your Head

- Documentation-only edits are allowed, but code/build/project/manifest changes require explicit user approval per `AGENTS.md`.
- Do not modify `build/manifest.json`, `vcpkg.json`, project files, build system, or CI unless Deniz explicitly says to reopen that scope.
- Do not create backward-compatibility shims for spike-local config unless there is persisted/shipped data or Deniz asks. This spike is pre-production.
- Do not hide ABI/API behavior in JSON knobs. Constitution says behavior belongs in code/tests unless it genuinely varies by family or is a named exception.
- Do not collapse policy rosters into a generic blob unless the spec proves auditability improves. Boring explicit files are often the good kind of boring.
- Do not make `--family all` activate TTF/Mixer/GFX in Iteration 2.
- Do not use generated `.g.cs` from one family as input to another family's generation/postprocess.
- Do not claim completion without fresh verification evidence.

## Acceptance Criteria For The Next Session

A good next session:

1. **Preferred:** Review existing spec, verify inventory, get Deniz approval, write implementation plan.
2. **Acceptable stop point:** Live inventory verified and approach options discussed, with open questions captured clearly.

If the next agent starts coding before the spec is approved, that's a process bug. Specs before scalpels, abi.
