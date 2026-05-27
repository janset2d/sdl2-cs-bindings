---
name: "Item 4 Closure + Item 5 SDL2_mixer Layer 1 — Handoff"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings after Item 4 SDL2_ttf Layer 1 raw ABI work completed on 2026-05-27. Recommended next: verify/commit Item 4 if still pending, then start Item 5 (SDL2_mixer Layer 1) spec/plan/research cycle using sdl2-mixer-header-analysis.md as the seed research artifact."
argument-hint: "Optional focus area: 'verify-item-4' | 'commit-item-4' | 'start-item-5' | 'write-spec' | 'write-plan' | 'onboard' | custom"
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

You are an engineer entering `janset2d/sdl2-cs-bindings` to continue the **ClangSharp + Roslyn postprocess binding-generator spike** under `spikes/binding-generators/clangsharp/`. The previous wave implemented **Item 4: SDL2_ttf Layer 1 Raw ABI** on top of Item 3 GFX. The next forward scope is **Item 4 verification/commit if still pending → Item 5 (SDL2_mixer Layer 1 raw ABI)**.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-27 — Item 4 implemented, commit may still be pending)** and verify against the live repo using `git log`, `git status`, generated output, and canonical docs before acting. This prompt is a handoff, not authority.

## What Just Happened — Item 4 Implemented

TTF was the first satellite to exercise both major cross-family mechanisms: a satellite-owned opaque handle and retained C `long` ABI surface. Key outcomes:

| Area | State |
|------|-------|
| **Generation** | TTF generated from one header (`SDL_ttf.h`) into Compat + Modern output. `Handles.g.cs` emitted in owner mode with `TTF_Font` in `namespace SDL2.Ttf`. |
| **Config** | `families.ttf.headers[]` populated with `{ "order": 10, "name": "SDL_ttf.h" }`; `selected_families("all")` now returns `['core', 'image', 'gfx', 'ttf']`. Mixer remains dormant. |
| **RSP** | `rsp/sdl2-ttf.rsp` created with 5 excludes: `TTF_SetError`, `TTF_GetError`, `TTF_GetFontKerningSize`, `TTF_SetDirection`, `TTF_SetScript`. No per-header `SDL_ttf.rsp` was needed. |
| **C# project** | `src/Janset.SDL2.Ttf/` created and added to `Janset.SDL2.ClangSharpSpike.slnx`; project follows the Image/GFX satellite pattern and references Core. |
| **C `long`** | Modern output uses `CLong`; Compat output uses Win32/Unix64 dual-dispatch helpers. Oracle initially flagged the private helper names as unresolved platform-sensitive C `long`, so `oracle.cs` was minimally fixed to ignore private `_Win32` / `_Unix64` helper imports for this category. |
| **No-SDLCALL functions** | `TTF_GetFontKerningSizeGlyphs`, `TTF_GetFontKerningSizeGlyphs32`, `TTF_SetFontSDF`, and `TTF_GetFontSDF` all emitted with Cdecl (`CallingConvention.Cdecl` in Compat, `CallConvCdecl` in Modern). This means no public surface was sacrificed here; Janset is more complete than SDL2-CS for the SDF functions. |
| **Peer evidence** | SDL2-CS uses `IntPtr` for `TTF_Font` and ABI-wrong `long` / `IntPtr` shortcuts for C `long`; Janset intentionally diverges. `Silk.NET.SDL` is real, but version 2.23.0 appears core-only for this purpose: its NuGet XML contains 0 `TTF_` / `SDL_ttf` / `TTF_Font` symbols and it depends on `Ultz.Native.SDL`, not an SDL_ttf satellite native package. |
| **Evidence** | Solution build succeeded 0 warnings / 0 errors; oracle report produced TTF raw ABI evidence with 0 findings after the helper false-positive fix; Python self-test PASS; C# postprocess self-test PASS; Slopwatch 0 issues; `git diff --check` clean. |
| **Deferred** | TTF runtime smoke is deferred until a redistributable font asset and native copy strategy are in scope. Do not invent a font-asset policy inside the Layer 1 raw ABI slice unless Deniz explicitly expands the scope. |

## Current Working-State Warning

At prompt authoring time, Item 4 may still be uncommitted. Before starting Item 5, run:

```pwsh
git branch --show-current
git log --oneline -5
git status --short
```

If Item 4 changes are still pending, first verify the existing work and ask Deniz before committing. Do **not** silently mix Item 5 changes into the Item 4 commit.

Expected Item 4 changed areas if uncommitted:

- `spikes/binding-generators/clangsharp/config/family-config.json`
- `spikes/binding-generators/clangsharp/rsp/sdl2-ttf.rsp`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/`
- `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx`
- `spikes/binding-generators/clangsharp/generate_bindings.py`
- `spikes/binding-generators/clangsharp/oracle.cs`
- `spikes/binding-generators/docs/items/item-4-sdl2-ttf-layer-1-spec.md`
- `spikes/binding-generators/docs/items/item-4-sdl2-ttf-layer-1-plan.md`
- `spikes/binding-generators/docs/llm-handoff.md`
- `spikes/binding-generators/docs/next-iteration-plan.md`
- `spikes/binding-generators/docs/satellite-expansion-roadmap.md`
- `spikes/binding-generators/output/reports/clangsharp-production.md`
- `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`

Recommended Item 4 commit message if Deniz approves:

```text
feat(bindings): add SDL2_ttf Layer 1 raw ABI generation

- enable SDL2_ttf in the ClangSharp family configuration and aggregate generation
- add the TTF spike project, RSP excludes, typed TTF_Font handle, and generated output
- verify C long handling, no-SDLCALL Cdecl bindings, deprecated exclusions, and error macro exclusions
- update oracle evidence, generator reports, roadmap notes, and Item 4 planning docs
- record SDL2-CS and Silk.NET peer comparison findings for TTF surface decisions
```

## Next Forward Scope

Per `spikes/binding-generators/docs/satellite-expansion-roadmap.md`:

```text
Item 3: SDL2_gfx Layer 1      → closed
  └── Item 4: SDL2_ttf Layer 1  → implemented / verify-commit if pending
       └── Item 5: SDL2_mixer Layer 1 → next real work
```

Item 5 is the last SDL2 satellite Layer 1 slice in this expansion wave before planning Layer 2 typed public projection.

## Item 5 — SDL2_mixer Layer 1 Raw ABI

Mixer is the **highest runtime-risk satellite** in this wave because it introduces callback typedefs and callback-accepting functions. Layer 1 still only needs correct raw signatures; callback lifetime/rooting policy belongs to higher-level APIs and runtime smoke design.

Key facts from `spikes/binding-generators/docs/satellites/sdl2-mixer-header-analysis.md`:

- **97 public functions**, single header: `SDL_mixer.h`.
- **All functions use `SDLCALL`**, so no no-SDLCALL calling-convention anomaly like TTF.
- **No C `long`**, no `wchar_t`, no variadics, no unions, no platform-conditioned API/layout.
- **Satellite-owned opaque handle:** `Mix_Music`; owner mode should emit `Handles.g.cs` in `namespace SDL2.Mixer`.
- **Transparent struct:** `Mix_Chunk` is a plain 4-field POD struct, not a union: `int allocated`, `Uint8* abuf`, `Uint32 alen`, `Uint8 volume`.
- **Enums:** `MIX_InitFlags` should receive `[Flags]`; `Mix_Fading` and `Mix_MusicType` should not.
- **Callback typedefs:** 6 total, all Cdecl. Compat should emit delegates with `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]`; Modern should emit `delegate* unmanaged[Cdecl]<...>`.
- **Callback-typed function parameters:** 7 functions / 8 callback-typed parameters, especially `Mix_RegisterEffect` with two callbacks.
- **Error macros:** exclude `Mix_SetError`, `Mix_GetError`, `Mix_ClearError`, `Mix_OutOfMemory` from Layer 1 raw ABI.
- **Legacy compatibility aliases:** exclude `MIX_MAJOR_VERSION`, `MIX_MINOR_VERSION`, `MIX_PATCHLEVEL`, `MIX_VERSION`.
- **Value macros:** keep/verify `SDL_MIXER_MAJOR_VERSION`, `SDL_MIXER_MINOR_VERSION`, `SDL_MIXER_PATCHLEVEL`, `MIX_CHANNELS`, `MIX_DEFAULT_FREQUENCY`, `MIX_DEFAULT_CHANNELS`, `MIX_MAX_VOLUME`, `MIX_CHANNEL_POST`, and investigate token/string macros such as `MIX_DEFAULT_FORMAT`, `MIX_EFFECTSMAXSPEED`.
- **Version helper macros:** function-like `SDL_MIXER_VERSION(X)` and `SDL_MIXER_VERSION_ATLEAST(X,Y,Z)` are not Layer 1 generated raw bindings; record if naturally skipped.

## Required Onboarding — Read In Order

1. `AGENTS.md` — operating rules, approval gate, settled decisions.
2. `docs/onboarding.md` — project overview, glossary.
3. `docs/plan.md` — current roadmap; Phase 4 binding autogen remains critical path.
4. `spikes/binding-generators/README.md` — spike layout, quick commands, endgame vision.
5. `spikes/binding-generators/docs/llm-handoff.md` — current spike handoff and known platform caveats.
6. `spikes/binding-generators/docs/next-iteration-plan.md` — active spike plan and latest evidence snapshot.
7. `spikes/binding-generators/docs/satellite-expansion-roadmap.md` §"Item 4" and §"Item 5" — closure context and Mixer target.
8. `spikes/binding-generators/docs/items/item-4-sdl2-ttf-layer-1-spec.md` — latest satellite owner-mode + C `long` design precedent.
9. `spikes/binding-generators/docs/items/item-4-sdl2-ttf-layer-1-plan.md` — latest implementation-plan style and verification gate structure.
10. `spikes/binding-generators/docs/satellites/sdl2-mixer-header-analysis.md` — **seed research artifact for Item 5**.
11. `spikes/binding-generators/docs/satellites/sdl2-satellite-error-function-consolidation.md` — Mixer error macro excludes and future redirect policy.
12. `spikes/binding-generators/docs/satellites/sdl2-function-like-macro-consolidation.md` — function-like macro policy context.
13. `docs/binding-autogen/binding-generator-constitution.md` — canonical ABI/API policy authority, especially opaque handles, callbacks, macros, and generated raw ABI constraints.

## Lessons To Carry From Item 4 Into Item 5

- **Trust generated evidence, not assumptions.** TTF no-SDLCALL functions looked risky on paper, but generated Cdecl output was correct. For Mixer callbacks, verify actual generated Compat/Modern signatures before adding any postprocess logic.
- **Do not create per-header RSPs preemptively.** TTF did not need one. Mixer's header analysis says none is needed; only add one if generated output proves a concrete ABI gap.
- **Owner-mode handles work for non-`SDL_` names.** `TTF_Font` validated the family-blind opaque detection path. Mixer should use the same path for `Mix_Music`; do not add `Mix_Music` to Core.
- **Every satellite project needs local support files.** `NativeTypeNameAttribute` is internal per assembly; `DisableRuntimeMarshalling` keeps Pattern B structs honest on modern TFMs.
- **Oracle false positives can happen around generated helper mechanics.** Item 4 required a small oracle correction for private C `long` helper imports. For Mixer, expect potential evidence-tool gaps around callback typedefs before assuming generated ABI is wrong.
- **Peer evidence is compatibility evidence, not authority.** SDL2-CS may omit modern functions or use weaker ABI types. Janset should follow the pinned headers + Constitution first.
- **Runtime smoke can be scoped out when assets/lifecycle policy are not ready.** For Mixer, callback runtime smoke is valuable, but Layer 1 closure should first prove generated signatures. If runtime smoke needs audio device setup, chunk assets, or delegate-rooting policy, call that out explicitly instead of smuggling it into raw generation.

## Files Most Likely To Matter For Item 5

- `spikes/binding-generators/clangsharp/config/family-config.json` → populate `families.mixer.headers[]` with `SDL_mixer.h`; verify owner mode and flags roster entries.
- `spikes/binding-generators/clangsharp/rsp/sdl2-mixer.rsp` → create with 8 excludes: 4 error macros + 4 legacy aliases.
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/` → create project, support files, and generated output from the Image/GFX/TTF satellite pattern.
- `spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx` → add Mixer project.
- `spikes/binding-generators/clangsharp/generate_bindings.py` → add `"mixer"` to `selected_families("all")` only after explicit Mixer generation is verified.
- `spikes/binding-generators/clangsharp/oracle.cs` → verify Mixer paths produce a real evidence row; make only minimal evidence-tool fixes if needed.
- `external/sdl2-cs/src/SDL2_mixer.cs` → peer visual comparison for callbacks, `Mix_Music`, `Mix_Chunk`, error redirects, and omitted/generated macros.
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_mixer.h` → pinned header authority.

## Suggested Item 5 Spec Questions For Deniz

Ask one question at a time per the `brainstorming` skill. Good first questions:

1. Should Item 5 stay strictly Layer 1 raw ABI like TTF, with runtime callback/audio smoke deferred unless generated signatures look suspicious?
2. For `MIX_EFFECTSMAXSPEED` and cross-header token macros like `MIX_DEFAULT_FORMAT`, should the spec treat absence as a Layer 1 reportable macro gap or require explicit generation/exclusion?
3. Should the Item 5 peer comparison include downloading/checking `Silk.NET.SDL` again, or is the Item 4 finding enough to treat Silk.NET as core-only unless new Mixer evidence appears?

Recommended stance: keep Item 5 raw-ABI focused. Verify callback signatures thoroughly, but do not design delegate-rooting or audio-device runtime policy inside this slice unless Deniz explicitly says `go` for that scope.

## Guardrails

- Do not touch `build/manifest.json`, `vcpkg.json`, `tools.cs`, Cake build tasks, CI/CD, or package projects.
- Do not activate SDL2_net or SDL3.
- Do not start Layer 2 typed public API design in the same commit as Mixer Layer 1.
- Do not add callback-rooting abstractions to Layer 1 raw bindings; raw ABI should stay close to generated ClangSharp output plus existing postprocess policy.
- Do not add new postprocess rewriters unless generated Mixer output proves a concrete correctness gap.
- Use the `brainstorming` skill before any Item 5 design or implementation work.
- Use the `writing-plans` skill after the Item 5 spec is approved.
- Wait for explicit approval (`go` / `başla` / `yap`) before writing production-like code or changing generator behavior.
- Keep `config/family-config.json` as the single source of truth; no new scope files or roster JSONs.
- Generated output must remain byte-identical for Core + Image + GFX + TTF when `--family all` runs, modulo line-ending noise.
- Run Slopwatch after code/project/test changes with this repo's exclude set.

## Minimum Item 5 Verification Shape

Start from the Item 4 plan style and adapt it. Expected gates:

1. JSON parse check after `family-config.json` edit.
2. `python spikes/binding-generators/clangsharp/generate_bindings.py --family mixer --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` exits 0 and produces Compat + Modern output plus `Handles.g.cs`.
3. Generated output contains `Mix_Music` owner-mode Pattern B handle in `SDL2.Mixer`.
4. Generated output contains `Mix_Chunk` as a plain sequential POD struct with the four expected fields.
5. Generated output decorates `MIX_InitFlags` with `[Flags]` and does not decorate non-bitmask Mixer enums.
6. All 6 callback typedefs and the 7 callback-consuming functions are manually checked in Compat and Modern output for Cdecl shape.
7. Excluded error macros and legacy aliases are absent.
8. Core/Image/GFX/TTF generated roots remain regress-free after aggregate generation.
9. `dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release` succeeds with 0 warnings / 0 errors.
10. `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-gfx --family sdl2-ttf --family sdl2-mixer --write-report` writes a real Mixer row.
11. `python spikes/binding-generators/clangsharp/generate_bindings.py --self-test` passes.
12. `dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- --self-test` passes.
13. `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"` reports 0 issues.
14. `git diff --check` is clean.

## First Action For The Next Agent

1. Onboard by reading the docs above and checking branch/status/log.
2. If Item 4 is still uncommitted, verify it and ask Deniz before committing.
3. Start Item 5 with `brainstorming`; use `sdl2-mixer-header-analysis.md` as the seed artifact and avoid re-litigating settled Item 4 decisions.
4. Write `spikes/binding-generators/docs/items/item-5-sdl2-mixer-layer-1-spec.md`.
5. After Deniz approves the spec, use `writing-plans` and write `spikes/binding-generators/docs/items/item-5-sdl2-mixer-layer-1-plan.md`.
