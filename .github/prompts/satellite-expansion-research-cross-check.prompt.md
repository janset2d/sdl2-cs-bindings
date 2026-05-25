---
name: "Cross-Check Review — Janset.SDL2 Satellite Family Expansion Research (2026-05-25)"
description: "Independent cross-check prompt for a fresh LLM with multi-agent dispatch capability. Review all four satellite-family research artifacts produced on 2026-05-25 (TTF, Mixer, GFX header analyses + Image cross-check) for factual accuracy, completeness, and logical soundness. Verify claims against live headers and existing generated code. Identify blind spots, wrong assumptions, and missing risks. Recommend implementation ordering."
argument-hint: "Optional focus: 'ttf-only' | 'mixer-only' | 'gfx-only' | 'image-only' | 'ordering-only' | 'full' (default)"
agent: "agent"
model: "Any model with multi-agent dispatch capability"
---

You are an independent reviewer entering `janset2d/sdl2-cs-bindings`. Your job is NOT to continue work, NOT to implement anything, and NOT to agree with prior analysis. Your job is to **cross-check four research artifacts** produced on 2026-05-25 against the live repository and identify every error, omission, assumption, and blind spot.

## First Principle

> Every claim in the research artifacts is **alleged**, not proven. Verify against the live headers in `vcpkg_installed/x64-windows-hybrid/include/SDL2/`, the existing generated bindings in `spikes/binding-generators/clangsharp/src/`, and the binding infrastructure in `spikes/binding-generators/clangsharp/`. The repo is at branch `spike/binding-autogen-sdl2-gfx`, ~43 commits ahead of origin.

---

## What You're Reviewing

On 2026-05-25, four research agents independently analyzed SDL2 satellite library headers to prepare for Layer 1 raw ABI binding generation. Their findings were persisted to:

| Artifact | What it covers |
|---|---|
| `spikes/binding-generators/docs/satellites/sdl2-ttf-header-analysis.md` | SDL2_ttf v2.24.0 — 87 functions, C `long` risk (5 functions), `TTF_Font` opaque handle, no callbacks, no platform-conditioned code |
| `spikes/binding-generators/docs/satellites/sdl2-mixer-header-analysis.md` | SDL2_mixer v2.8.1 — 97 functions, 6 callback typedefs, `Mix_Music` opaque handle, `Mix_Chunk` struct (NO union found), 3 enums |
| `spikes/binding-generators/docs/satellites/sdl2-gfx-header-analysis.md` | SDL2_gfx — ~96 functions across 4 headers, per-header export macro risk, `FPSmanager` struct, no opaque handles, no callbacks |
| `spikes/binding-generators/docs/satellites/sdl2-image-cross-check.md` | SDL2_image v2.8.8 — cross-check of existing generated bindings, found 1 bug (`[Flags]` missing on `IMG_InitFlags`) |

These agents also previously produced a ppy/SDL3-CS reference analysis at `spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md`.

## Mandatory Grounding (read in this order before dispatching)

1. `AGENTS.md` — project context, approval gates, settled decisions
2. `docs/binding-autogen/binding-generator-constitution.md` — **canonical policy authority**. Key sections for this review:
   - Layer Contract L34-50 (three-layer API)
   - C `long` policy L246-283 (TTF has 5 C `long` functions)
   - Opaque Handles L325-368 (Pattern B spec, auto-detect criterion, roster JSON, cross-assembly contract)
   - Structs And Unions L406-435
   - Foreign Type Boundary Policy L369-400
   - Function Surface L151-167 (includes GFX export macro note at L160-161)
   - BCL-Replaceable Helper Exclusion Policy L169-208
   - Family Identity L136-148 (TTF/Mixer/GFX namespace + raw class names)
3. `spikes/binding-generators/docs/priority-c-closure-summary.md` — what was already resolved (C `long` hybrid pattern, Pattern B handles, foreign types)
4. `spikes/binding-generators/docs/next-iteration-plan.md` — current slice plan, Review Follow-up Backlog
5. `spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md` — ppy satellite pattern analysis
6. `spikes/binding-generators/clangsharp/generate_bindings.py` — the orchestrator (understand FAMILY_CONFIG, command_for_header, extend_rsp_arguments, postprocess pipeline)
7. `spikes/binding-generators/clangsharp/rsp/base.rsp` — cross-cutting remaps
8. `spikes/binding-generators/clangsharp/rsp/sdl2-image.rsp` — reference family RSP pattern
9. `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/` — reference csproj + Generated output + Support files

---

## Review Mission — Dispatch Sub-Agents

Use multi-agent dispatch to verify each artifact INDEPENDENTLY against the live headers and code. Each sub-agent must re-read the actual header from `vcpkg_installed/x64-windows-hybrid/include/SDL2/` — never trust the research artifact's claims without verification.

### Agent 1: Verify SDL2_ttf claims

**Input:** `spikes/binding-generators/docs/satellites/sdl2-ttf-header-analysis.md`
**Live header:** `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_ttf.h`

Verify every claim:

1. **Function count and inventory:** Re-count all `extern DECLSPEC` declarations. Does the artifact miss any functions? Does it include any non-existent functions? Are all function signatures correct?
2. **C `long` surface:** Independently identify every function with `long` or `unsigned long` in its signature. Compare against the artifact's list of 5. Are any missed? Are any incorrectly flagged?
3. **Deprecated functions:** Verify the artifact's claim that `TTF_GetFontKerningSize`, `TTF_SetDirection`, `TTF_SetScript` are deprecated and the latter two miss `SDLCALL`. Is this correct?
4. **Macro exclusions:** Are `TTF_SetError`/`TTF_GetError` correctly identified as cross-family macros? Are there any other macros that should be excluded?
5. **Type inventory:** Verify `TTF_Font` is forward-declared-only (opaque). Are there any transparent structs the artifact missed? Any enums missed?
6. **Platform-conditioned code:** The artifact claims "no platform-conditioned code." Verify by searching for `#ifdef`, `#if defined(` blocks that affect function availability or struct layout.
7. **RSP recommendations:** Are the proposed `--exclude` entries correct and complete? Any missing?
8. **Postprocess assessment:** Is extending `threadid-dispatch` → `clong-dispatch` the right approach for the 5 C `long` functions? Are there alternatives?

**Return:** A table of verified claims, corrected claims, and missed claims. Every disagreement must cite the header line number.

### Agent 2: Verify SDL2_mixer claims

**Input:** `spikes/binding-generators/docs/satellites/sdl2-mixer-header-analysis.md`
**Live header:** `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_mixer.h`

Verify every claim:

1. **Function count and inventory:** Re-count all `extern DECLSPEC` declarations. Does the artifact miss any functions? Include non-existent ones?
2. **Mix_Chunk struct — NO UNION:** This is the artifact's most surprising claim (contradicting earlier ppy analysis). Independently verify: does `Mix_Chunk` have a union or not? Read the struct definition carefully. Check for `union` keyword, `#ifdef`-conditioned alternative layouts, or `_hidden` members. Also check SDL2_mixer documentation/comments above the struct.
3. **Callback typedefs:** Verify all 6 callback signatures. Are the parameter types and calling conventions correct? Are any callbacks missed?
4. **Enum inventory:** Verify `MIX_InitFlags` (bitmask?), `Mix_Fading`, `Mix_MusicType`. Are member values correct? Any enums missed?
5. **Macro exclusions:** Verify `Mix_SetError`/`Mix_GetError`/`Mix_ClearError`/`Mix_OutOfMemory` are cross-family macros. Verify `MIX_MAJOR_VERSION`/`MIX_MINOR_VERSION`/`MIX_PATCHLEVEL`/`MIX_VERSION` are legacy aliases that should be excluded. Any others?
6. **Platform-conditioned code:** Verify the claim of "no platform-conditioned code."
7. **`double` returns:** Verify the 5 `double`-returning functions and 2 `double`-param functions.
8. **RSP recommendations:** Complete and correct?
9. **Satellite-owned opaque handle gap:** The artifact identifies that `Mix_Music` is a satellite-owned opaque handle NOT in Core's roster, and the current `uniform-opaque` consumer mode doesn't handle this. Is this analysis correct? Are there really no satellite-owned handles in Image (verification: check Image's headers for any `typedef struct X X;` patterns)?

**Return:** A table of verified claims, corrected claims, and missed claims with line number citations.

### Agent 3: Verify SDL2_gfx claims and export macro resolution

**Input:** `spikes/binding-generators/docs/satellites/sdl2-gfx-header-analysis.md`
**Live headers:** All `SDL2_*` headers in `vcpkg_installed/x64-windows-hybrid/include/SDL2/` that belong to GFX

First, identify which headers belong to GFX (exclude `SDL_`-prefixed core headers, `SDL_image.h`, `SDL_mixer.h`, `SDL_ttf.h`). Then for each:

1. **Export macro analysis:** For EACH functional GFX header, verify:
   - The scope macro name and definition
   - Whether the `#ifndef` fallback resolves to `extern` in a consuming scenario (no `DLL_EXPORT`, no `LIBSDL2_GFX_DLL_IMPORT`)
   - Whether the artifact's insurance `--define-macro` entries are correct and complete
   - **Critical question:** Is the artifact's claim that the natural fallback works correct? Are there any headers where the preprocessor path is different?
2. **Function count and inventory:** Re-count per-header. Verify all signatures.
3. **Mixed naming surface:** Verify the claim that SDL2_gfxPrimitives.h and SDL2_rotozoom.h use bare names while SDL2_framerate.h and SDL2_imageFilter.h use `SDL_` prefix. Is this categorization complete and correct?
4. **FPSmanager struct:** Verify field layout and sizes.
5. **SDL2_gfxPrimitives_font.h:** Verify the claim that this is data-only and should be excluded from scope.
6. **Constants:** Verify all constant values.
7. **No opaque handles:** Verify — does GFX really have zero `typedef struct X X;` patterns?
8. **RSP recommendations:** Are the 4 `--define-macro` entries complete? Any missing excludes?
9. **Postprocess assessment:** The artifact claims no postprocess changes needed. Verify: does the existing 6-step pipeline handle all GFX function signatures correctly? Are there any `delegate*` or callback patterns that need postprocess attention?

**Return:** Verified/corrected/missed claims table with line numbers.

### Agent 4: Cross-check SDL2_image and identify systemic gaps

**Input:** `spikes/binding-generators/docs/satellites/sdl2-image-cross-check.md`
**Live header:** `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_image.h`
**Existing generated code:** `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern/SDL_image.g.cs` and `Compat/SDL_image.g.cs`

1. **Function coverage:** Independently re-count and compare header functions vs generated functions. Confirm 59/59 or find gaps.
2. **`[Flags]` bug:** Verify `IMG_InitFlags` should be `[Flags]`. Check the header documentation (L112). Also check: does the Constitution's `[Flags]` rule apply?
3. **Signature audit:** Spot-check at least 10 function signatures across Modern and Compat output. Verify parameter types and return types match the header after applying `base.rsp` remaps and `uniform-opaque` postprocess.
4. **Pattern B handle usage:** Verify `SDL_RWops`, `SDL_Renderer`, `SDL_Texture` are used by-value (not as pointers) in the generated output. Verify `SDL_Surface` correctly stays as pointer.
5. **Constants:** Verify all version constants have correct values.
6. **Namespace and csproj:** Verify the namespace is `SDL2.Image`, the ProjectReference to Core exists, the TFM split is correct, and `DisableRuntimeMarshalling.cs` is present.
7. **Systemic `[Flags]` gap:** The cross-check report warns that Mixer's `MIX_InitFlags` (and potentially others) may have the same missing `[Flags]` bug. Check:
   - Does `MIX_InitFlags` in `SDL_mixer.h` have power-of-two bitmask values?
   - Does `IMG_InitFlags` in the generated output actually lack `[Flags]`? (Read the actual .g.cs file)
   - Are there any other enums across all four families that should be `[Flags]`?
8. **Cross-family `--exclude` pattern:** Verify that all four families follow the same pattern for error macros (`X_SetError`/`X_GetError` → exclude). Is there a consistent naming convention we can document?

**Return:** Verification table with any corrections and the systemic `[Flags]` gap assessment.

### Agent 5: Verify infrastructure assumptions and ordering recommendation

This agent does NOT read headers — it reviews the architectural claims across all four artifacts:

1. **Include directory assumption:** All four artifacts claim that satellite headers live in the same `include/SDL2/` directory as core headers, so "no new include directories needed." Verify: list the actual files in `vcpkg_installed/x64-windows-hybrid/include/SDL2/` that match `SDL_image*`, `SDL_mixer*`, `SDL_ttf*`, `SDL2_*`. Are they all in one flat directory? Or are there subdirectories?
2. **`FAMILY_CONFIG` proposals:** Review the proposed FAMILY_CONFIG entries for TTF, Mixer, GFX. Do they follow the existing Image pattern? Are `namespace`, `raw_class`, `rsp`, `library_dir` values consistent with the Constitution's Family Identity table?
3. **`generate_bindings.py` changes:** Review the proposed changes (add family entries, update `selected_families`, `--family` choices, `stats` dict, `PLATFORM_SENSITIVE_HEADERS`). Are any code locations missed? Any logic changes needed beyond adding entries?
4. **Postprocess pipeline impact:** For each family, verify which of the 6 postprocess steps apply and which are no-ops. Are there any conflicts between families? Does the `owner_mode` logic in `uniform-opaque` need updating for TTF?
5. **Satellite-owned opaque handle gap:** This is the most important architectural gap. Verify:
   - TTF has `TTF_Font` (satellite-owned opaque)
   - Mixer has `Mix_Music` (satellite-owned opaque)
   - GFX has none
   - Image has none
   - The current `uniform-opaque` consumer mode does NOT handle satellite-owned opaques
   - The `opaque-handle-roster.json` only covers Core types
   Is this gap assessment accurate? What's the minimum change to fix it? Is the recommendation (consumer mode emits local Handles.g.cs) sound?
6. **Ordering recommendation:** The GFX agent recommended GFX → TTF → Mixer. The TTF agent recommended TTF first (different opinion). Independently assess:
   - Which family has the lowest implementation risk?
   - Which family validates the most infrastructure (include dirs, FAMILY_CONFIG, csproj, postprocess)?
   - Which family's unique challenge (export macros vs C `long` vs callbacks) is best addressed first to build confidence?
   - **Recommend an ordering and justify it.**

**Return:** Infrastructure verification, ordering recommendation with justification, and any missed implementation concerns.

---

## Synthesis Required

After all sub-agents return, compile a single review report with these sections:

### 1. Factual Errors Found
Every claim in the artifacts that was disproven by live header/code inspection. Cite header line numbers.

### 2. Omissions
Functions, types, constants, or risks present in the headers but not mentioned in the artifacts.

### 3. Wrong Assumptions
Architectural or infrastructure assumptions in the artifacts that don't hold up. Include the include-directory assumption, if wrong.

### 4. Systemic Gaps
Issues that span multiple families (e.g., `[Flags]` attribute gap, satellite-owned opaque handle handling, cross-family error macro pattern).

### 5. Implementation Ordering
Your recommended order with justification. Must address:
- Risk profile per family
- Infrastructure validation value per family
- Dependency relationships between families
- Whether any family should be bundled with another

### 6. Blind Spots
Risks or concerns that NONE of the four artifacts addressed. Think about:
- What happens when multiple satellites reference the same Core handle type?
- Build ordering — do we need a specific build sequence?
- What test assets (fonts, audio files) do we need committed?
- Any vcpkg triplet or platform considerations the artifacts ignored?
- The `AbiTests.csproj` pattern — does it scale to 5 families?

### 7. Overall Assessment
One-paragraph verdict: are these artifacts trustworthy enough to proceed to implementation, or should specific artifacts be redone?

---

## Working Rules

- **Read-only review.** Do not modify any files. Do not commit anything.
- **Verify, don't trust.** Every claim in the artifacts is alleged until you read the live header/code yourself.
- **Cite evidence.** Every correction MUST include the header line number or file path + line number.
- **Disagree openly.** If sub-agents disagree with each other, surface the disagreement. Don't reconcile silently.
- **If blocked by ambiguity**, state what's ambiguous and what clarification is needed from Deniz.
- **Respect AGENTS.md §Approval Gate.** No commits, no file modifications, no build system changes.
- **Bilingual TR/EN communication style** (per AGENTS.md) is Deniz's preference but doesn't constrain your review output.

---

## Files at Issue (do not modify)

```
vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_ttf.h
vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_mixer.h
vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_image.h
vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL2_framerate.h
vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL2_gfxPrimitives.h
vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL2_imageFilter.h
vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL2_rotozoom.h
vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL2_gfxPrimitives_font.h

spikes/binding-generators/docs/satellites/sdl2-ttf-header-analysis.md
spikes/binding-generators/docs/satellites/sdl2-mixer-header-analysis.md
spikes/binding-generators/docs/satellites/sdl2-gfx-header-analysis.md
spikes/binding-generators/docs/satellites/sdl2-image-cross-check.md
spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md

spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern/SDL_image.g.cs
spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat/SDL_image.g.cs
spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj

spikes/binding-generators/clangsharp/rsp/sdl2-image.rsp
spikes/binding-generators/clangsharp/rsp/base.rsp
spikes/binding-generators/clangsharp/generate_bindings.py
spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json

docs/binding-autogen/binding-generator-constitution.md
spikes/binding-generators/docs/next-iteration-plan.md
spikes/binding-generators/docs/priority-c-closure-summary.md
```
