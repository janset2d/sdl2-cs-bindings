---
name: "Item 3 Closure + Item 4 SDL2_ttf Layer 1 — Handoff"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after Item 3 SDL2_gfx Layer 1 closed on 2026-05-27. Recommended next: formally close Item 3 (verification-only — all evidence collected), then start Item 4 (SDL2_ttf Layer 1) spec/plan/research cycle using sdl2-ttf-header-analysis.md as the seed research artifact."
argument-hint: "Optional focus area: 'close-item-3' | 'start-item-4' | 'write-spec' | 'write-plan' | 'onboard' | custom"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering `janset2d/sdl2-cs-bindings` to continue the **ClangSharp + Roslyn postprocess binding-generator spike** under `spikes/binding-generators/clangsharp/`. The previous wave closed **Item 3: SDL2_gfx Layer 1 Raw ABI** on `spike/binding-autogen-sdl2-gfx`. The next work is **Item 3 (SDL2_gfx verification — formal close) → Item 4 (SDL2_ttf Layer 1 raw ABI)**.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-27 — Item 3 closed)** and verify against the live repo using `git log`, `git status`, and canonical docs before acting. This prompt is a handoff, not authority.

## What Just Happened — Item 3 Closed

GFX was the lowest-risk satellite and proved the multi-family pipeline works end-to-end. Key outcomes:

| Area | State |
|------|-------|
| **Generation** | 8 `.g.cs` files (4 Compat + 4 Modern across 4 headers), no `Handles.g.cs` (consumer mode — no satellite-owned opaque handles) |
| **Config** | `families.gfx.headers[]` populated with 4 ordered headers; `selected_families("all")` now returns `["core", "image", "gfx"]` |
| **RSP** | `rsp/sdl2-gfx.rsp` created: 4 `--define-macro` insurance entries (SDL2_GFXPRIMITIVES_SCOPE/ROTOZOOM/FRAMERATE/IMAGEFILTER=extern) + `--exclude M_PI` |
| **C# project** | `src/Janset.SDL2.Gfx/` created (csproj copied from Image, `Support/NativeTypeNameAttribute.cs`, `Support/DisableRuntimeMarshalling.cs`), added to slnx |
| **Postprocess** | Zero changes — all 7 steps GFX-safe (platform-delta no-op, strip-varargs no-op, libraryimport 4/4, flags-detect 0 transforms, guid-substitute no-op, clong-dispatch 0 methods, uniform-opaque consumer) |
| **Evidence** | Build 0/0 all 5 TFMs across 4 projects. Oracle: 102 functions, 8 constants, 5 types, 0 ABI findings, 0 evidence gaps. SDL2-CS cross-check: 102/102 function match, constant gap is `M_PI` exclusion (policy) + SDL2-CS's outdated `SDL2_GFXPRIMITIVES_MICRO=1` (header says 4). AbiTests 792/792. Python --self-test PASS. C# --self-test PASS. Slopwatch 0 issues. |
| **Deferred** | `[StructLayout(LayoutKind.Sequential)]` not emitted by ClangSharp for sequential data structs (`FPSmanager`, `SDL_Color`, etc.). With `[assembly: DisableRuntimeMarshalling]` there is no runtime risk, but explicit layout is interop hygiene per ECMA-335. Recorded in `next-iteration-plan.md` §"Production Flip Gates" as postprocess item. |
| **Branch** | `spike/binding-autogen-sdl2-gfx` — commit pending push. Working tree changes include GFX project + generated files. |

## Next Forward Scope

Per the roadmap at `spikes/binding-generators/docs/satellite-expansion-roadmap.md`:

```
Item 3: SDL2_gfx Layer 1            → ⏭ formal close (evidence ready)
  └── Item 4: SDL2_ttf Layer 1      → ⏭ next real work
       └── Item 5: SDL2_mixer Layer 1 → queued
```

### Item 3 — Formal Close Only

Item 3's success criteria were all satisfied:

| Criterion | Evidence |
|-----------|----------|
| `--family gfx --execute` produces `.g.cs` | 4 Compat + 4 Modern, non-empty, correct namespace |
| Build 0/0 across 5 TFMs | All projects clean |
| Core/Image no regression | `git diff --ignore-cr-at-eol` empty |
| Oracle sdl2-gfx: evidence report | 102 functions, 0 findings, 0 gaps |
| AbiTests | 792/792 |

**No code work remains for Item 3.** The task is purely documentation: mark Item 3 closed in `satellite-expansion-roadmap.md` and `next-iteration-plan.md`, and note the closure in `llm-handoff.md`. The next agent should do this as its first commit.

### Item 4 — SDL2_ttf Layer 1 Raw ABI

TTF is the **first family with significant infrastructure dependencies** — satellite-owned opaque handle (`TTF_Font`) and C `long` surface — exercising the two most significant generator features:

- **88 functions** across a single header (`SDL_ttf.h`)
- **Satellite-owned opaque handle:** `TTF_Font` — no `SDL_` prefix. `OpaqueHandleEmitRewriter` auto-detection is family-blind (structural: empty partial struct + pointer use), so it should detect `TTF_Font` when TTF activates. Owner mode emits `Handles.g.cs` in `namespace SDL2.Ttf`.
- **C `long` surface:** 5 functions (`TTF_OpenFontIndex`, `TTF_OpenFontIndexRW`, `TTF_OpenFontIndexDPI`, `TTF_OpenFontIndexDPIRW` — `long index` parameter; `TTF_FontFaces` — `long` return). `ClongDualDispatchRewriter` already has these 5 in `AffectedMethodNames` (read from `clong_methods[]` in config). The rewriter handles parameter-position C `long` (not just return-position like Core's `SDL_ThreadID`).
- **No-SDLCALL functions:** 4 functions lack the `SDLCALL` / `__cdecl` annotation (`TTF_GetFontKerningSizeGlyphs`, `TTF_GetFontKerningSizeGlyphs32`, `TTF_SetFontSDF`, `TTF_GetFontSDF`). ClangSharp may or may not handle this — deep-dive needed per the roadmap. Per-header RSP `--with-callconv` or postprocess may be needed.
- **Deprecated functions to exclude:** 3 (`TTF_GetFontKerningSize`, `TTF_SetDirection`, `TTF_SetScript`)
- **Error macros to exclude:** `TTF_SetError` / `TTF_GetError` (cross-family aliases to Core's `SDL_SetError` / `SDL_GetError`)
- **Owner mode** (emits `Handles.g.cs` with `TTF_Font` Pattern B struct)
- **Config scaffold exists:** `families.ttf` in `family-config.json` has identity, `owner_mode: true`, `headers` currently `[]`, `clong_methods[]` pre-populated with 5 TTF methods

### Key Risks for Item 4

| Risk | Severity | Notes |
|------|----------|-------|
| **No-SDLCALL calling convention** | **MEDIUM** | 4 functions lack `SDLCALL`. On Windows they may compile as `__cdecl` (default), but this needs verification. May need `--with-callconv` per-header RSP or postprocess fix. This is the only TTF-specific unknown. |
| **C `long` parameter-position rewrite** | LOW | Already wired in `ClongDualDispatchRewriter`. Already tested with Core's return-position `SDL_ThreadID`. Parameter-position is the same mechanism — just confirms it works for `long index`. |
| **`TTF_Font` auto-detection** | LOW | Family-blind structural detection. Same mechanism that detects Core's `SDL_Window`, `SDL_Thread`, etc. |
| **32 `SDL_Surface*`-returning render functions** | LOW | Satellite → Core type boundary. Same pattern as Image's `IMG_LoadTexture_RW(SDL_Renderer*)`. Consumer-side uniform-opaque handles this. |

## Required Onboarding (Read In Order)

1. `AGENTS.md` — operating rules, approval gate, settled decisions
2. `docs/onboarding.md` — project overview, glossary
3. `docs/plan.md` — current roadmap
4. `spikes/binding-generators/README.md` — spike layout, quick commands, endgame vision
5. `spikes/binding-generators/docs/llm-handoff.md` — LLM-to-LLM handoff (updated to Item 3 closure)
6. `spikes/binding-generators/docs/next-iteration-plan.md` — active spike plan + review backlog
7. `spikes/binding-generators/docs/satellite-expansion-roadmap.md` §"Item 3" + §"Item 4" — roadmap context
8. `spikes/binding-generators/docs/satellites/sdl2-ttf-header-analysis.md` — **★ seed research artifact for Item 4**
9. `spikes/binding-generators/docs/satellites/sdl2-satellite-error-function-consolidation.md` — error macro excludes
10. `docs/binding-autogen/binding-generator-constitution.md` L246-283 — C `long` policy
11. `spikes/binding-generators/docs/items/item-3-sdl2-gfx-layer-1-spec.md` — GFX design (for reference on satellite expansion pattern)
12. `spikes/binding-generators/docs/items/item-3-sdl2-gfx-layer-1-plan.md` — GFX plan (for reference on implementation pattern)

## Working State

```pwsh
git branch --show-current   # expected: spike/binding-autogen-sdl2-gfx
git log --oneline -5        # shows Item 3 closure commit
git status --short          # expected: clean working tree after Item 3 commit
```

**Active config file:** `spikes/binding-generators/clangsharp/config/family-config.json` (single source of truth).

**Dormant families guard:** `selected_families("all")` returns `["core", "image", "gfx"]`. TTF and Mixer are in the config but not activated. Item 4 adds TTF to the activation set.

**Config population for Item 4:** The agent must populate `families.ttf.headers[]` with `[{ "order": 10, "name": "SDL_ttf.h" }]`, create `rsp/sdl2-ttf.rsp`, evaluate whether `rsp/per-header/SDL_ttf.rsp` is needed for no-SDLCALL functions, create `src/Janset.SDL2.Ttf/` with csproj (pattern: copy `Janset.SDL2.Image.csproj`, change names, but also add `Handles.g.cs` support since TTF is owner mode), create `Support/DisableRuntimeMarshalling.cs` + `Support/NativeTypeNameAttribute.cs`, and add TTF to `selected_families("all")` once generation is verified.

## GFX-Carried Lessons for TTF

From the GFX implementation (Item 3), the next agent should internalize:

- **`Support/NativeTypeNameAttribute.cs` is required** in every satellite project. GFX build failed until this was added (2410 CS0122 errors). The attribute is `internal`, so each assembly needs its own copy.
- **satellite csproj pattern:** Copy Image's, change names. Consumer vs owner mode only changes whether `Handles.g.cs` is emitted by postprocess — csproj is identical.
- **Family RSP format:** No `--libraryPath` / `--methodClassName` — those come from config per Iteration 2.
- **Per-header RSP:** Only create if needed. GFX didn't need any. TTF may need one for no-SDLCALL calling convention.
- **`ClongDualDispatchRewriter`:** Already wired for TTF's 5 C `long` methods. The config (`families.ttf.clong_methods[]`) is pre-populated. No rewriter code changes needed.
- **Oracle:** Already has `Sdl2Ttf` in `FamilyConfigs` + `KnownFamilies`. No oracle.cs change needed.

## Files Most Likely To Matter For Item 4

- `config/family-config.json` → `families.ttf` section (populate `headers[]`)
- `generate_bindings.py` → add `"ttf"` to `selected_families("all")` return
- `rsp/sdl2-ttf.rsp` → create with 5 `--exclude` entries (3 deprecated + 2 error macros)
- `rsp/per-header/SDL_ttf.rsp` → create if no-SDLCALL calling convention fix needed
- `src/Janset.SDL2.Ttf/` → create csproj + Support/ + Generated/
- `Janset.SDL2.ClangSharpSpike.slnx` → add TTF project

## Guardrails

- Do not touch `build/manifest.json`, `vcpkg.json`, `tools.cs`, CI/CD.
- Do not activate Mixer — only TTF in Item 4.
- Do not change `oracle.cs` — already has `Sdl2Ttf`.
- Use the `brainstorming` skill before any design or implementation work.
- Wait for explicit approval (`go` / `başla` / `yap`) before writing code.
- Regen + `git diff --ignore-cr-at-eol` empty for Core + Image + GFX is the determinism gate after every code change.
- Generated output must remain byte-identical for Core + Image + GFX when `--family all` runs.
- `config/family-config.json` is the single source of truth.
- Every satellite project MUST include `Support/NativeTypeNameAttribute.cs` (internal, namespace-matched to family).

## First Action For The Next Agent

1. **Onboard** by reading the docs above and verifying branch state.
2. **Close Item 3** — update `satellite-expansion-roadmap.md` §"Item 3" status to "Closed 2026-05-27", note evidence already collected, update `next-iteration-plan.md` slice table, note in `llm-handoff.md`. Commit as `docs(bindings): close Item 3 — SDL2_gfx verification complete`.
3. **Start Item 4** — use the `brainstorming` skill. Key questions for Deniz: "Should we trust ClangSharp to emit the correct calling convention for no-SDLCALL TTF functions, or do we need a per-header RSP `--with-callconv`? And should we verify TTF_Font auto-detection first via dry-run before committing to config changes?" Then write spec + plan per the established workflow (brainstorming → writing-plans → approval gate).
