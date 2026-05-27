---
name: "Item 2 Closure + Item 3 SDL2_gfx Layer 1 — Handoff"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after Iteration 2 config surface unification closed at 4851de0 on 2026-05-27. Recommended next: formally close Item 2 (verification-only — all evidence already collected), then start the Item 3 (SDL2_gfx Layer 1) spec/plan/research cycle using sdl2-gfx-header-analysis.md as the seed research artifact."
argument-hint: "Optional focus area: 'close-item-2' | 'start-item-3' | 'write-spec' | 'write-plan' | 'onboard' | custom"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering `janset2d/sdl2-cs-bindings` to continue the **ClangSharp + Roslyn postprocess binding-generator spike** under `spikes/binding-generators/clangsharp/`. The previous wave closed **Iteration 2: Config Surface Unification** (commit `4851de0` on `spike/binding-autogen-sdl2-gfx`). The next work is **Item 2 (SDL_image verification — formal close) → Item 3 (SDL2_gfx Layer 1 raw ABI)**.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-27 — Iteration 2 closed at `4851de0`)** and verify against the live repo using `git log`, `git status`, and canonical docs before acting. This prompt is a handoff, not authority.

## What Just Happened — Iteration 2 Closed

Iteration 2 consolidated the spike's ~25-source config surface into a single `config/family-config.json` (743 lines, schema 1.0, 5 families) read by both `generate_bindings.py` and the C# postprocess. Key outcomes:

| Area | State |
|------|-------|
| **Config file** | `config/family-config.json` — mechanically seeded, parity-verified, all 5 families populated (ttf/mixer/gfx dormant: `headers: []`, `required_surface: null`) |
| **Python orchestrator** | `FAMILY_CONFIG`, `ALL_PLATFORM_MACROS`, `SDL2_PLATFORM_VIEWS`, `PLATFORM_SENSITIVE_HEADERS` constants removed. All per-family facts derived from config accessors. Schema version guard in `load_config()`. |
| **RSP identity** | `--methodClassName` and `--libraryPath` removed from `sdl2-core.rsp` + `sdl2-image.rsp`. Orchestrator derives them from config `raw_class` / `library_name`. |
| **C# postprocess** | Hardcoded family switches eliminated from `Program.cs`, `UniformOpaqueFamilyIdentity.cs`, `UniformOpaqueOwnerMode.cs`. `AffectedMethodNames` + `PlatformOrder`/`SupportedOsByPlatform` derived from config. `FlagsEnumRosterLoader.cs` deleted — rosters read from `FamilyConfig.cs`. Native type classification (`"SDL_threadID"`, `"unsigned long"`, `"long"`) stays in code per Constitution. |
| **Retired sources** | Deleted: `scope/sdl2-core.headers.txt`, `scope/sdl2-image.headers.txt`, `scope/sdl2-core-sdlh-required.json`, `policy/opaque-handle-roster.json`, `policy/flags-enum-roster.json`, `config/config_migration.py`, `postprocess/FlagsEnumRosterLoader.cs`. |
| **Evidence** | Generated output byte-identical to pre-Iteration-2 baseline. Build 0/0 all 5 TFMs. AbiTests 792/792. Oracle Core/Image clean. Slopwatch 0 issues. Self-tests PASS × 2. |
| **Deferred** | `oracle.cs FamilyConfigs` / `KnownFamilies` — retains own hardcoded per-family records this iteration (Deniz decision, 2026-05-27). |
| **Branch** | `spike/binding-autogen-sdl2-gfx` at `4851de0` — single squash-landed commit. Working tree clean. |

## Next Forward Scope

Per the roadmap at `spikes/binding-generators/docs/satellite-expansion-roadmap.md`:

```
Iteration 2 (config unification) → ✅ closed
  └── Item 2: SDL_image [Flags] verification  → ⏭ formal close (evidence ready)
       └── Item 3: SDL2_gfx Layer 1            → ⏭ next real work
            └── Item 4: SDL2_ttf Layer 1        → queued
                 └── Item 5: SDL2_mixer Layer 1  → queued
```

### Item 2 — Formal Close Only

Item 2's success criteria were all satisfied as a side effect of prior work:

| Criterion | Evidence |
|-----------|----------|
| `IMG_InitFlags` has `[Flags]` in Modern + Compat | Item 1 S1-6 delivered the `flags-detect` postprocess step |
| `--family image` regen correct | Iteration 2 evidence: image-only regen byte-identical |
| Oracle sdl2-image: 0 new findings | Oracle report clean at Iteration 2 closure |
| Multi-TFM build clean (5 TFMs) | Build 0/0 at every evidence gate |

**No code work remains for Item 2.** The task is purely documentation: mark Item 2 closed in `satellite-expansion-roadmap.md` and `next-iteration-plan.md`, and note the closure in `llm-handoff.md`. The next agent should do this as its first commit.

### Item 3 — SDL2_gfx Layer 1 Raw ABI

GFX is the **lowest-risk satellite** per the header analysis at `spikes/binding-generators/docs/satellites/sdl2-gfx-header-analysis.md`. Key facts:

- **102 functions** across 4 headers (5 `SDL_`-prefixed + 59 bare-name + 30 `SDL_`-prefixed + 8 bare-name)
- **1 struct**: `FPSmanager` (20 bytes, fully blittable, pointer-only)
- **No opaque handles**, no C `long`, no callbacks, no unions, no platform-conditioned code
- **Per-header export macros** (`SDL2_GFXPRIMITIVES_SCOPE` etc.) — clean resolution via insurance `--define-macro` entries in family RSP
- **`M_PI` duplicate risk** — appears in two headers, needs `--exclude`
- **Consumer mode** for `uniform-opaque` — no `Handles.g.cs` to emit
- **4 `--define-macro` insurance entries** in `rsp/sdl2-gfx.rsp` (one per scope macro)
- **Config scaffold exists**: `families.gfx` in `family-config.json` has identity, `owner_mode: false`, empty `headers[]` awaiting population, empty `clong_methods[]`, empty `opaque_handles.auto_detect_well_known[]`

The next agent should: brainstorm the Item 3 design with Deniz, write a spec, write an implementation plan, then execute. GFX is simple enough that spec+plan may be compact. The header analysis doc serves as the seed research artifact — no new research dives needed unless unknowns surface during ClangSharp dry-run.

## Required Onboarding (Read In Order)

1. `AGENTS.md` — operating rules, approval gate, settled decisions
2. `docs/onboarding.md` — project overview, glossary
3. `docs/plan.md` — current roadmap
4. `spikes/binding-generators/README.md` — spike layout, quick commands, endgame vision
5. `spikes/binding-generators/docs/llm-handoff.md` — LLM-to-LLM handoff (pre-Iteration-2 — some paths stale, marked)
6. `spikes/binding-generators/docs/next-iteration-plan.md` — active spike plan + review backlog
7. `spikes/binding-generators/docs/satellite-expansion-roadmap.md` §"Item 2" + §"Item 3" — roadmap context
8. `spikes/binding-generators/docs/satellites/sdl2-gfx-header-analysis.md` — **★ seed research artifact for Item 3**
9. `spikes/binding-generators/docs/items/iteration-2-config-surface-unification-spec.md` — Iteration 2 design (for context on the new config surface)
10. `docs/binding-autogen/binding-generator-constitution.md` — canonical ABI/API policy authority
11. `spikes/binding-generators/docs/priority-c-closure-summary.md` — Layer 1 evidence baseline

## Working State

```pwsh
git branch --show-current   # expected: spike/binding-autogen-sdl2-gfx
git log --oneline -3        # expected: 4851de0 feat(bindings): unify config...
git status --short          # expected: (empty — clean working tree)
```

**Active config file:** `spikes/binding-generators/clangsharp/config/family-config.json` (single source of truth for all per-family facts).

**Dormant families guard:** `selected_families("all")` returns `["core", "image"]`. TTF/Mixer/GFX are in the config but not activated. Item 3 adds GFX to the activation set.

**Config population for Item 3:** The agent must populate `families.gfx.headers[]` with the 4 functional headers (ordered: `SDL2_framerate.h`, `SDL2_gfxPrimitives.h`, `SDL2_imageFilter.h`, `SDL2_rotozoom.h`), create `rsp/sdl2-gfx.rsp`, create `src/Janset.SDL2.Gfx/` with csproj (pattern: copy `Janset.SDL2.Image.csproj`, change names), create `Support/DisableRuntimeMarshalling.cs`, and add GFX to `selected_families("all")` once generation is verified.

## GFX-Specific Notes The Next Agent Must Internalize

From the header analysis (§7, now outdated — Iteration 2 changed the RSP format):

**Family RSP `rsp/sdl2-gfx.rsp` must contain (no `--libraryPath` / `--methodClassName` — those come from config):**
```
# SDL2_gfx uses per-header export macros instead of extern DECLSPEC.
# Insurance: explicitly define each scope macro to extern.
--define-macro
SDL2_GFXPRIMITIVES_SCOPE=extern
SDL2_ROTOZOOM_SCOPE=extern
SDL2_FRAMERATE_SCOPE=extern
SDL2_IMAGEFILTER_SCOPE=extern

--exclude
M_PI
```

**Postprocess:** All 7 steps are GFX-safe with zero changes needed. Consumer `uniform-opaque` runs as normal (no opaque handles to rewrite).

**No new rewriters, no new postprocess modes, no include directory changes.**

## First Action For The Next Agent

1. **Onboard** by reading the docs above and verifying branch state.
2. **Close Item 2** — update `satellite-expansion-roadmap.md` §"Item 2" status to "Closed 2026-05-27", note evidence already collected, update `next-iteration-plan.md` slice table, note in `llm-handoff.md`. Commit as `docs(bindings): close Item 2 — SDL_image verification complete`.
3. **Start Item 3** — use the `brainstorming` skill. The header analysis is already done; the spec can go straight to design. Key questions for Deniz: "Should GFX use consumer mode only (no `Handles.g.cs` emit), and is the `M_PI --exclude` approach acceptable, or do we want it emitted as a constant in one canonical header?" Then write spec + plan per the established workflow (brainstorming → writing-plans → approval gate).

## Files Most Likely To Matter For Item 3

- `config/family-config.json` → `families.gfx` section (populate `headers[]`)
- `generate_bindings.py` → add `"gfx"` to `selected_families("all")` return
- `rsp/sdl2-gfx.rsp` → create with 4 `--define-macro` + `--exclude M_PI`
- `src/Janset.SDL2.Gfx/` → create csproj + Support/ + Generated/
- `Janset.SDL2.ClangSharpSpike.slnx` → add GFX project
- `spikes/binding-generators/BindingGeneratorSpikes.slnx` → add GFX project if applicable

## Guardrails

- Do not touch `build/manifest.json`, `vcpkg.json`, `tools.cs`, CI/CD.
- Do not activate TTF or Mixer — only GFX in Item 3.
- Do not change `oracle.cs` — deferred per Iteration 2.
- Use the `brainstorming` skill before any design or implementation work.
- Wait for explicit approval (`go` / `başla` / `yap`) before writing code.
- Regen + `git diff --ignore-cr-at-eol` empty is the determinism gate after every code change.
- Generated output must remain byte-identical for Core + Image when `--family all` runs.
- `config/family-config.json` is the single source of truth — no new scope files, no new roster JSONs, no hardcoded family data in code.
