# Satellite Expansion & Per-Library Generation — Grand Plan Roadmap

**Date:** 2026-05-25
**Status:** Roadmap — directional, not prescriptive. Each item gets its own deep-dive spec + plan before implementation.
**Branch:** `spike/binding-autogen-sdl2-gfx`
**North star:** ppy/SDL3-CS binding generation pattern, adapted for multi-OS pass + multi-TFM + internal raw ABI + Pattern B handles.

---

## Principle: Assumptions Are Explicit, Docs Are First-Class

Every item below describes the **current best understanding** and the **preferred direction** — not a binding design. Deep-dive iterations own the final decisions. When a deep-dive contradicts an assumption in this document, the assumption is updated in-place (docs are first-class artifacts). Inline code comments are self-contained; this document does not point to docs as a substitute for explanation. Phase numbers, ADR references, and migration timing belong here and in canonical docs, not logic-bearing code comments (AGENTS.md).

---

## Cross-Cutting: Configuration, Policy, Shims, Oracle Standardization

Iteration 2 consolidated all family header lists, required surfaces, and policy rosters into a single config file:

### `spikes/binding-generators/clangsharp/config/family-config.json`

- **Unified Configuration Surface:** Single source of truth for the entire binding-generator scope. Holds per-family identities, header lists, opaque-handle and flags-enum rosters, clong methods lists, and global platform views.
- **Removed files:** Stale roster JSONs, required-surface JSONs, and `.headers.txt` scope files are retired and deleted.

### `spikes/binding-generators/clangsharp/shims/`

- `platform-headers/`: synthetic platform parse shims for Windows-local iteration. Rename or restructure only if needed for clarity; no functional change assumed.

### `spikes/binding-generators/clangsharp/compare_oracle.py`

- **Remove.** The Cake oracle comparison approach is retired; `oracle.cs` is the active evidence reporter. The sunset Cake-hosted CppAst implementation under `build/_build/Targets/GenerateBindings/` is not the comparison target going forward.

### `spikes/binding-generators/clangsharp/oracle.cs`

- **Multi-family alignment.** `FamilyConfigs` currently hardcoded to `Sdl2Core` + `Sdl2Image`. Extend with `FamilyConfigs.Sdl2Ttf`, `Sdl2Mixer`, `Sdl2Gfx`. The `RawAbiChecks` engine is already family-parameterized — adding families is a data change, not a logic change.
- Success: `dotnet run --file oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report` produces a 5-family evidence report.

---

## Item 1: Per-Library Generation Infrastructure

**Status:** Closed 2026-05-26. Items 2–5 are unblocked, with Iteration 2 config surface unification intentionally scheduled before expansion work.

**Goal:** `generate_bindings.py` + postprocess pipeline supports per-family invocation deterministically. `--family all` output is byte-identical (CRLF aside) to `--family core` + `--family image` run separately. Existing Core + Image output is REGRESSION-FREE.

### Success Criteria

1. `generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` produces identical Core/Image `.g.cs` output to current HEAD except audited `[Flags]` additions required by Constitution §"Enums" (diff with `--ignore-cr-at-eol` contains only those additions).
2. `generate_bindings.py --family core --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` + `generate_bindings.py --family image --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` run sequentially produce identical output to `--family all`.
3. Multi-TFM build: `dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release` — 0 warnings, 0 errors across the spike solution.
4. `oracle.cs --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report` — 0 findings across Priority C categories for Core + Image; dormant TTF/Mixer/GFX generated rows report missing without hard bugs.
5. GFX, TTF, Mixer FAMILY_CONFIG entries exist in `generate_bindings.py` but are NOT yet wired in `selected_families("all")` — they're dormant until their respective expansion items activate them.

### Current Understanding (assumptions flagged)

**FAMILY_CONFIG routing:** Item 1 extends `FAMILY_CONFIG` with `ttf`, `mixer`, `gfx` entries and keeps `selected_families("all")` dormant at `['core', 'image']` until each expansion item activates its family. `PLATFORM_SENSITIVE_HEADERS`, `stats`, `write_report()`, and `--family` choices are family-aware.

**Include directory wiring:** All satellite headers live flat under `vcpkg_installed/<triplet>/include/SDL2/` alongside Core headers. The existing `--include-directory` already points there — **Assumption:** no new include directories are needed. Verified by header listing; deep-dive reconfirms per family.

**Postprocess pipeline — per-family independence:** Each of the 7 conceptual postprocess steps (`platform-delta`, `strip-varargs`, `libraryimport` for Modern only, `flags-detect`, `guid-substitute`, `clong-dispatch`, `uniform-opaque`) operates on one family's `Generated/{Compat,Modern}/` tree. The `generate_bindings.py` orchestrator invokes postprocess per-family via `run_postprocess(family, ...)`; the only cross-family flow is the data-only roster pull defined by the Constitution.

**OpaqueHandleEmitRewriter — family-keyed owner/consumer mode (landed in Item 1):**

- Auto-detection is structural: empty partial struct + pointer use at any ABI signature position. It does not require the `SDL_` prefix, so satellite-owned `TTF_Font` and `Mix_Music` are covered when their families activate.
- The rewriter reads the namespace from the `.g.cs` file being processed and emits `Handles.g.cs` into that namespace.
- `force_opaque_exceptions` (`SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg`) remain under `families.core` in `family-config.json`. Satellite-owned handles (`TTF_Font`, `Mix_Music`) are listed under their owning family sections and are still structurally drift-checked by owner-mode runs.
- `--owner-mode` determines which families emit their own `Handles.g.cs`: owner mode for `core`, `ttf`, and `mixer`; consumer mode for `image` and `gfx`.
- Structural auto-detection without prefix matching is the accepted Item 1 policy for known SDL2 opaque handle patterns.

**ClongDualDispatchRewriter:**

- Current mode key is `clong-dispatch`.
- `AffectedMethodNames` covers `SDL_ThreadID`, `SDL_GetThreadID`, and TTF's 5 C `long` functions (4 index params + 1 FontFaces return).
- Parameter-position rewrite logic is present. `ClongDualDispatchRewriter` inspects return and parameter native type names so TTF's dormant `long index` parameters receive the same platform-sensitive dispatch as Core's return-position C `long` surface.
- The rewriter remains applied per-family — it skips families with no matching function names (no-op for Image, GFX, Mixer).

**`[Flags]` detection:**

- `IMG_InitFlags` and `MIX_InitFlags` are confirmed bitmask enums (power-of-two values, documentation says "OR'd together"). Pre-Item 1 generated output lacked `[Flags]`; S1-6 fixes Image and the same suffix rule should cover Mixer when it activates.
- **Resolved direction:** Item 1 adds a `flags-detect` postprocess step using **name-suffix + family-keyed roster allow-list** per Constitution §"Enums" auto-decoration policy. Power-of-two value heuristic was considered and rejected: Constitution L448 friction; `SDL_bool` (`SDL_FALSE=0`, `SDL_TRUE=1`) would false-positive under a naive heuristic; no peer validation — only alimer-bindings-sdl auto-detects in production, and it uses the same name-suffix + hardcoded allow-list approach we adopt. ppy/SDL3-CS uses manual companion files (incompatible with our "no magic companion" philosophy); Silk.NET 2.X drops `[Flags]` entirely.
- **Item 1 landed policy:** name-suffix detection (`EndsWith("Flags")` — catches `IMG_InitFlags`, `MIX_InitFlags`, `SDL_RendererFlags`, etc.) + family-keyed allow-list for composite-alias-bearing enums (`SDL_Keymod`, `SDL_BlendMode`, `SDL_GLcontextFlag`, `SDL_RendererFlip`, `SDL_TextureModulate`). See Item 1 spec §5.4.

### Reference Docs

- `spikes/binding-generators/docs/priority-c-closure-summary.md` — current Layer 1 evidence baseline
- `spikes/binding-generators/docs/ppy-reference-analysis-2026-05-25.md` — ppy satellite architecture analysis
- `spikes/binding-generators/docs/next-iteration-plan.md` — current slice plan + review follow-up backlog
- `docs/binding-autogen/binding-generator-constitution.md` — canonical policy authority
- `spikes/binding-generators/docs/satellites/sdl2-image-cross-check.md` — existing Image binding cross-check
- `spikes/binding-generators/docs/satellites/sdl2-ttf-header-analysis.md` — TTF header analysis
- `spikes/binding-generators/docs/satellites/sdl2-mixer-header-analysis.md` — Mixer header analysis
- `spikes/binding-generators/docs/satellites/sdl2-gfx-header-analysis.md` — GFX header analysis

### Reference Code

- `spikes/binding-generators/clangsharp/generate_bindings.py` — orchestrator (FAMILY_CONFIG, command_for_header, extend_rsp_arguments, run_postprocess, main)
- `spikes/binding-generators/clangsharp/postprocess/OpaqueHandleEmitRewriter.cs:190-210, 330-340` — current auto-detect gate + namespace emit
- `spikes/binding-generators/clangsharp/postprocess/ClongDualDispatchRewriter.cs` — current Core + dormant TTF C `long` method set and return/parameter rewrite logic
- `spikes/binding-generators/clangsharp/postprocess/UniformOpaqueOwnerMode.cs` — CLI flag parsing
- `spikes/binding-generators/clangsharp/postprocess/Program.cs` — postprocess CLI dispatch
- `spikes/binding-generators/clangsharp/rsp/base.rsp` — cross-cutting remaps
- `spikes/binding-generators/clangsharp/rsp/sdl2-image.rsp` — reference family RSP pattern
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/` — reference satellite csproj + Generated + Support
- `spikes/binding-generators/clangsharp/oracle.cs:160-183` — FamilyConfigs hardcoding
- `spikes/binding-generators/clangsharp/config/family-config.json` — unified family configuration (family identity, handle rosters, flags allow-list, etc.)

---

## Iteration 2 — Config Surface Unification

**Status:** Closed 2026-05-27. Config surface consolidated into single `config/family-config.json`. Full spec + plan at `docs/items/iteration-2-config-surface-unification-spec.md` and `-plan.md`. See §"What changed" below for migration summary.
**Branch:** `spike/binding-autogen-sdl2-gfx` (landing commit TBD).
**Position:** Immediately after Item 1, before Item 2 onwards. Iteration 1 = Item 1 (per-library infra); Iteration 2 = this config unification; subsequent iterations = expansion items (2–5).

### Binding constraints (set now, not in the spec/plan iteration)

- **`build/manifest.json` is not touched in Iteration 2.** The Cake-host'lu CppAst implementation under `build/_build/Targets/GenerateBindings/` is sunset (ADR-004 Reopened). Manifest re-integration belongs to Roadmap M7 production flip, not here.
- **No determinism regression.** All Iteration 2 changes must satisfy Constitution §"Generation Determinism Contract" — pin-set inputs, family isolation, dependency direction, cross-family handle name resolution all preserved.
- **Ships before any expansion item.** Items 2–5 (Image bug fix, GFX, TTF, Mixer) start after Iteration 2 closes.

### Problem

The generation + postprocess pipeline (post-Item 1) consumes configuration from **18 sources** — 9 external files + 9 code-internal data structures. This dispersion is not inherently wrong (Constitution §"Manifest Configuration Vs Code-Owned Policy" L88-135 sets a clean policy: manifest owns reviewable facts that vary by family; code owns ABI/API policy), but the current spike's distribution between external and code-internal is not the result of applying that policy deliberately — it's the result of incremental growth. Three concrete risks:

- **Drift between source types.** Family identity fields (`namespace`, `raw_class`, `library_dir`) live in `generate_bindings.py`'s `FAMILY_CONFIG` dict; the same concept exists at the manifest level (`package_families[]`) but the spike doesn't read manifest. Two parallel surfaces for the same fact.
- **Mixed config-vs-policy footprint.** The Constitution split between manifest-owned and code-owned isn't enforced today — some family-varying facts live in code (`FAMILY_CONFIG`, `PLATFORM_SENSITIVE_HEADERS`), some live in external roster/scope/RSP files.
- **Discoverability for new agents.** Answering "where does the config for family X live?" requires inspecting ≥6 different external files plus 4 code-internal data structures.

### Configuration inventory (Item 1 baseline — 18 sources)

External config files (9):

| # | Source type | Path | Content | Family-keyed |
|---|---|---|---|---|
| 1 | Header list `.txt` | `scope/sdl2-<family>.headers.txt` | Native headers parsed per family | per-family |
| 2 | Required-surface `.json` | `scope/sdl2-core-sdlh-required.json` | Core SDL.h required functions + SDL_INIT_* constants (manifest parity check) | Core-only |
| 3 | Platform shim `.h` | `clangsharp/shims/platform-headers/{endian,AvailabilityMacros,TargetConditionals}.h` | Windows-local synthetic Linux/macOS/iOS parse shims | Cross-family |
| 4 | Opaque-handle roster `.json` | `clangsharp/policy/opaque-handle-roster.json` | Pattern B auto-detect + force-opaque + excluded-candidates | per-family schema 2.0 |
| 5 | Flags-enum roster `.json` | `clangsharp/policy/flags-enum-roster.json` | `[Flags]` allow-list | per-family schema 2.0 |
| 6 | RSP `.rsp` (3-tier) | `clangsharp/rsp/base.rsp` + `sdl2-<family>.rsp` + `per-header/<header>.rsp` | ClangSharp remap/exclude/define-macro directives | Tier 2+3 family-aware |
| 7 | Manifest `.json` | `build/manifest.json library_manifests[]` | `vcpkg_version` (determinism anchor) + Core required-surface entries | per-family entries |
| 8 | Tool pinning | `dotnet-tools.json` | ClangSharp tool version | Cross-cutting |
| 9 | Vcpkg pinning | `vcpkg.json` + overlay triplets | Native header content (input) | Cross-cutting |

Code-internal data structures (9):

| # | Location | Content |
|---|---|---|
| 10 | `generate_bindings.py:327-344` `FAMILY_CONFIG` dict | Per-family identity (`namespace`, `raw_class`, `rsp`, scope filenames, `library_dir`) |
| 11 | `generate_bindings.py:449-455` `PLATFORM_SENSITIVE_HEADERS` dict | Per-family platform-sensitive header lists |
| 12 | `generate_bindings.py:367-395` `ALL_PLATFORM_MACROS` list | 26 platform define names |
| 13 | `generate_bindings.py:404-441` `SDL2_PLATFORM_VIEWS` list | 7 platform view definitions |
| 14 | `generate_bindings.py:355-358` `CODEGEN_CONFIG` dict | `compat`/`modern` ClangSharp flag combos |
| 15 | `generate_bindings.py:776-779` `selected_families("all")` return value | Dormant activation set |
| 16 | `postprocess/ClongDualDispatchRewriter.cs` `AffectedMethodNames` HashSet | C `long` symbols (Core + TTF — post-Item 1) |
| 17 | `postprocess/FlagsAttributeRewriter.cs` suffix rule | Hardcoded `EndsWith("Flags")` |
| 18 | `postprocess/GuidSubstitutionRewriter.cs` | `SDL_GUID` → `System.Guid` hardcoded substitution |

### What this iteration does NOT decide (deferred to its own spec + plan)

The following are open questions that belong to Iteration 2's spec + plan, not to this roadmap placeholder:

- Classification of the 18-source inventory against Constitution §"Manifest Configuration Vs Code-Owned Policy" L88-135.
- Whether/how to align `FAMILY_CONFIG` (#10) with manifest `package_families[]` while honoring the "manifest not touched" constraint above.
- Whether `PLATFORM_SENSITIVE_HEADERS` (#11), `selected_families("all")` (#15), or other code-internals should migrate to external surfaces.
- Fate of spike-only artifacts (`sdl2-core-sdlh-required.json`, platform shim `.h` files) at production flip (M7).
- Whether the 9 external sources can collapse (e.g. roster JSONs unified, RSP tiers reorganized) without losing per-family auditability.
- Source-of-truth ordering when concepts overlap (e.g. family identity in `FAMILY_CONFIG` vs `package_families[]`).

**No solution is proposed here.** The Iteration 2 spec + plan iteration owns those answers under its own approval gate.

### Reference docs

- Constitution §"Manifest Configuration Vs Code-Owned Policy" — policy authority for any reorganization.
- Constitution §"Generation Determinism Contract" — binding determinism invariants Iteration 2 cannot violate.
- ADR-004 (Reopened) — toolchain re-evaluation context (Cake-host'lu CppAst sunset).
- [`item-1-per-library-generation-spec.md`](items/item-1-per-library-generation-spec.md) §4.7 — Item 1 cross-cutting status (current external config snapshot).

---

## Item 2: SDL_image Bug Fixes

**Goal:** Fix the single confirmed bug in existing SDL_image bindings. Prove the per-library infrastructure can make targeted fixes without regressing Core.

### Success Criteria

1. `IMG_InitFlags` in both Modern and Compat output has `[Flags]` attribute.
2. Regeneration with `--family image` (or `--family all`) produces correct output.
3. `oracle.cs` for sdl2-image: 0 new findings (no regression).
4. Multi-TFM build clean for Image (5 TFMs).

### Current Understanding

- `IMG_InitFlags` at `SDL_image.h:95-103` has power-of-two values (JPG=0x01, PNG=0x02, TIF=0x04, WEBP=0x08, JXL=0x10, AVIF=0x20). Documentation at L112 says "OR'd together." Constitution §"Enums" mandates `[Flags]`.
- Item 1 S1-6 fixes generated Modern and Compat output through the `flags-detect` postprocess step. Item 2 remains useful as an explicit Image-targeted regression/closure check, not as the first implementation of the attribute.
- `Enum.HasFlag()` does NOT require `[Flags]` — the concrete impact is `ToString()` formatting and debugging display only. Severity: low.
- Same bug confirmed in Mixer's `MIX_InitFlags` — handled under Item 5.
- Item 2 can close as a targeted verification slice because S1-7 preserved the S1-6 `IMG_InitFlags` output.

### Reference Docs

- `spikes/binding-generators/docs/satellites/sdl2-image-cross-check.md` §5 — bug description
- `docs/binding-autogen/binding-generator-constitution.md` L437-451 — Enums policy

### Reference Code

- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern/SDL_image.g.cs` — `IMG_InitFlags` `[System.Flags]`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat/SDL_image.g.cs` — `IMG_InitFlags` `[System.Flags]`
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_image.h:95-103, 112`

---

## Item 3: Expansion — SDL2_gfx

**Goal:** Full Layer 1 raw ABI for SDL2_gfx. Consumer mode (GFX has no satellite-owned opaque handles). First non-Image satellite — validates the entire multi-family pipeline.

### Success Criteria

1. `generate_bindings.py --family gfx --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` produces `.g.cs` files under `src/Janset.SDL2.Gfx/Generated/{Compat,Modern}/`.
2. `dotnet build Janset.SDL2.Gfx.csproj -c Release` — 0 warnings, 0 errors across 5 TFMs.
3. `dotnet build Janset.SDL2.Image.csproj -c Release` — still 0/0 across 5 TFMs (no regression from adding a new family to the solution).
4. `oracle.cs --family sdl2-gfx --write-report` — family evidence report produced.
5. AbiTests or characterization tests: `FPSmanager` struct layout verified (sizeof == 20).

### Current Understanding

- **102 functions** across 4 headers: `SDL2_framerate.h` (5, `SDL_`-prefixed), `SDL2_gfxPrimitives.h` (59, bare names), `SDL2_imageFilter.h` (30, `SDL_`-prefixed), `SDL2_rotozoom.h` (8, bare names). `SDL2_gfxPrimitives_font.h` excluded (data-only).
- **SDL2-CS compatibility baseline exists:** `external/sdl2-cs/src/SDL2_gfx.cs` gives us a real GFX oracle comparison source for validating generated Layer 1 output. This improves compatibility evidence only; it does not change GFX's ABI risk profile.
- **Export macro risk:** Per-header scope macros (`SDL2_GFXPRIMITIVES_SCOPE` etc.). Each header's `#ifndef` fallback resolves to `extern` in consuming scenarios. Insurance `--define-macro` entries in `rsp/sdl2-gfx.rsp` guarantee ClangSharp recognition.
- **No satellite-owned opaque handles.** Consumer mode. No `uniform-opaque` owner-mode changes needed for GFX.
- **No C `long`, no callbacks, no unions, no platform-conditioned code.** FPSmanager is the only struct — transparent, 20 bytes, fully blittable.
- **`M_PI` duplicate risk:** Appears conditionally in two headers (`SDL2_gfxPrimitives.h:34-35`, `SDL2_rotozoom.h:40-41`) — needs `--exclude` to prevent duplicate emission.
- **Mixed naming:** 67 bare-name + 35 `SDL_`-prefixed. Layer 1 raw ABI is unaffected. Layer 3 ergonomic concern only.
- **Assumption:** the 4 `--define-macro` insurance entries are sufficient; additional per-header RSP may be needed for edge cases discovered during generation.

### Family Identity (per Constitution L147)

- Namespace: `SDL2.Gfx`
- Internal raw ABI class: `SDL2_gfxNative`
- Native library: `SDL2_gfx`
- Managed project: `Janset.SDL2.Gfx`

### Reference Docs

- `spikes/binding-generators/docs/satellites/sdl2-gfx-header-analysis.md` — full header analysis

### Reference Code

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL2_framerate.h`
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL2_gfxPrimitives.h`
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL2_imageFilter.h`
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL2_rotozoom.h`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/` — reference satellite csproj pattern

### New Files to Create

- `rsp/sdl2-gfx.rsp` — family RSP with 4 `--define-macro` entries + `--exclude M_PI`
- `src/Janset.SDL2.Gfx/Janset.SDL2.Gfx.csproj` — multi-TFM, ProjectReference→Core
- `src/Janset.SDL2.Gfx/Support/DisableRuntimeMarshalling.cs` — cross-assembly Pattern B contract
- Update `config/family-config.json` `families.gfx.headers[]` with the 4 functional headers (Iteration 2 retired `scope/sdl2-gfx.headers.txt`)

---

## Item 4: Expansion — SDL2_ttf

**Goal:** Full Layer 1 raw ABI for SDL2_ttf. First family with satellite-owned opaque handle (TTF_Font) and C `long` surface — exercises the two most significant infrastructure gaps.

### Success Criteria

1. `generate_bindings.py --family ttf --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` produces `.g.cs` files including `Handles.g.cs` with `TTF_Font` Pattern B struct in `namespace SDL2.Ttf` (owner mode).
2. `dotnet build Janset.SDL2.Ttf.csproj -c Release` — 0/0 across 5 TFMs.
3. C `long` functions (`TTF_OpenFontIndex*`, `TTF_FontFaces`) use CLong/dual-dispatch pattern in generated output.
4. No-SDLCALL functions (`TTF_GetFontKerningSizeGlyphs*`, `TTF_SetFontSDF`, `TTF_GetFontSDF`) correctly bound with explicit `CallingConvention.Cdecl`.
5. `dotnet build Janset.SDL2.{Core,Image,Gfx}.csproj` — still 0/0 (no regression from adding TTF).
6. AbiTests: `TTF_Init`/`TTF_Quit` lifecycle + `TTF_OpenFont` handle roundtrip + `TTF_OpenFontIndex(0)` C `long` smoke.

### Current Understanding

- **88 functions**, single header. 5 C `long` surface (4 index params + 1 FontFaces return).
- **Satellite-owned opaque:** `TTF_Font` — no `SDL_` prefix. Depends on Item 1's family-blind auto-detection. Owner mode emits `Handles.g.cs` in `namespace SDL2.Ttf`.
- **C `long` dispatch:** Depends on Item 1's `ClongDualDispatchRewriter` extension (add 5 TTF function names + parameter-position rewrite logic).
- **No-SDLCALL functions:** 4 non-deprecated functions lack `SDLCALL` (`TTF_GetFontKerningSizeGlyphs`, `TTF_GetFontKerningSizeGlyphs32`, `TTF_SetFontSDF`, `TTF_GetFontSDF`). SDL's `SDLCALL` maps to `__cdecl` on Windows at `begin_code.h:77-80`; without it, the native declaration carries no explicit annotation. Generated P/Invoke must force `CallingConvention.Cdecl`. **Assumption:** a per-header RSP or postprocess step can add the calling convention. Deep-dive determines mechanism.
- **Deprecated functions:** 3 to exclude (`TTF_GetFontKerningSize`, `TTF_SetDirection`, `TTF_SetScript`). Only the first lacks `SDLCALL`.
- **Error macros:** `TTF_SetError`/`TTF_GetError` excluded (cross-family aliases to Core). See [satellites/sdl2-satellite-error-function-consolidation.md](satellites/sdl2-satellite-error-function-consolidation.md) for full cross-family analysis.
- **Function-like helpers:** `TTF_VERSION(X)` / `TTF_VERSION_ATLEAST(X,Y,Z)`-style public macros, if present in the pinned header, are not Item 1/S1-2 Layer 1 output. They remain follow-up Layer 2 / friendly helper candidates governed by an explicit companion-helper policy. See [satellites/sdl2-function-like-macro-consolidation.md](satellites/sdl2-function-like-macro-consolidation.md) for the full cross-family function-like macro catalog.
- **No callbacks, no unions, no platform-conditioned code.**
- **Assumption:** 32 rendering functions returning `SDL_Surface*` all resolve correctly via Core ProjectReference. Core Pattern B handles (SDL_RWops) resolved by-value via consumer-side uniform-opaque.

### Family Identity (per Constitution L146)

- Namespace: `SDL2.Ttf`
- Internal raw ABI class: `SDL_ttfNative`
- Native library: `SDL2_ttf`
- Managed project: `Janset.SDL2.Ttf`

### Reference Docs

- `spikes/binding-generators/docs/satellites/sdl2-ttf-header-analysis.md` — full header analysis

### Reference Code

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_ttf.h`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/` — reference satellite csproj pattern
- `docs/binding-autogen/binding-generator-constitution.md` L246-283 — C `long` policy (TTF called out at L264)

### New Files to Create

- `rsp/sdl2-ttf.rsp` — family RSP with 5 `--exclude` entries (3 deprecated + 2 error macros)
- `rsp/per-header/SDL_ttf.rsp` — per-header RSP for no-SDLCALL calling convention overrides (if needed)
- `src/Janset.SDL2.Ttf/Janset.SDL2.Ttf.csproj` — multi-TFM, ProjectReference→Core
- `src/Janset.SDL2.Ttf/Support/DisableRuntimeMarshalling.cs`
- Update `config/family-config.json` `families.ttf.headers[]` with the single header (Iteration 2 retired `scope/sdl2-ttf.headers.txt`)

---

## Item 5: Expansion — SDL2_mixer

**Goal:** Full Layer 1 raw ABI for SDL2_mixer. Highest runtime risk due to callback delegates — last expansion item after infrastructure is battle-tested.

### Success Criteria

1. `generate_bindings.py --family mixer --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` produces `.g.cs` files including `Handles.g.cs` with `Mix_Music` Pattern B struct in `namespace SDL2.Mixer` (owner mode).
2. `dotnet build Janset.SDL2.Mixer.csproj -c Release` — 0/0 across 5 TFMs.
3. All 6 callback typedefs emit with correct `[UnmanagedFunctionPointer(CallingConvention.Cdecl)]` on Compat, correct `delegate* unmanaged[Cdecl]<...>` on Modern.
4. `Mix_Chunk` struct layout verified (no union — plain 4-field POD).
5. `[Flags]` on `MIX_InitFlags` (same fix as `IMG_InitFlags` from Item 2).
6. `dotnet build Janset.SDL2.{Core,Image,Gfx,Ttf}.csproj` — still 0/0 (no regression).
7. AbiTests: `Mix_OpenAudio`/`Mix_CloseAudio` lifecycle + `Mix_LoadWAV`/`Mix_PlayChannel` chunk playback + callback roundtrip smoke (`Mix_SetPostMix`).

### Current Understanding

- **97 functions**, single header. No C `long`, no `wchar_t`, no variadics, no unions, no platform-conditioned code.
- **Satellite-owned opaque:** `Mix_Music` — same pattern as `TTF_Font`. Owner mode emits `Handles.g.cs` in `namespace SDL2.Mixer`.
- **Callback typedefs:** 6 total. ClangSharp handles them natively — Compat emits delegate types with `[UnmanagedFunctionPointer]`, Modern emits `delegate*` function pointers. No postprocess changes needed for syntax-level emission. **Assumption:** ClangSharp's native callback handling is sufficient for all 6 typedefs. Deep-dive verifies.
- **Mix_Chunk:** Plain 4-field POD struct. NO union (confirmed — contradicting earlier speculation). `[StructLayout(LayoutKind.Sequential)]`. No explicit layout needed.
- **`[Flags]` gap:** `MIX_InitFlags` — same bitmask pattern as `IMG_InitFlags`. Handled by Item 1's `[Flags]` detection mechanism.
- **Error macros:** 4 to exclude (`Mix_SetError`, `Mix_GetError`, `Mix_ClearError`, `Mix_OutOfMemory`). Legacy compat aliases also excluded. See [satellites/sdl2-satellite-error-function-consolidation.md](satellites/sdl2-satellite-error-function-consolidation.md) for full cross-family analysis.
- **Version macros:** `SDL_MIXER_COMPILEDVERSION` should be kept/auto-emitted (same pattern as Image's `SDL_IMAGE_COMPILEDVERSION`). `SDL_MIXER_VERSION(X)` and `SDL_MIXER_VERSION_ATLEAST(X,Y,Z)` are function-like — skipped/reported in Layer 1 and tracked as follow-up Layer 2 / friendly helper candidates, not complete in S1-2. See [satellites/sdl2-function-like-macro-consolidation.md](satellites/sdl2-function-like-macro-consolidation.md) for the full cross-family function-like macro catalog.
- **Callback lifecycle risk:** Callbacks persist across audio frames. Layer 2 must handle delegate rooting. Layer 1 only needs correct signatures — but AbiTests must include callback roundtrip smoke.
- **Assumption:** 7 functions with 8 callback-typed parameters (Mix_RegisterEffect has two callbacks). All use `SDLCALL` → `__cdecl`.

### Family Identity (per Constitution L145)

- Namespace: `SDL2.Mixer`
- Internal raw ABI class: `SDL_mixerNative`
- Native library: `SDL2_mixer`
- Managed project: `Janset.SDL2.Mixer`

### Reference Docs

- `spikes/binding-generators/docs/satellites/sdl2-mixer-header-analysis.md` — full header analysis

### Reference Code

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_mixer.h`
- `spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/` — reference satellite csproj pattern

### New Files to Create

- `rsp/sdl2-mixer.rsp` — family RSP with 8 `--exclude` entries (4 error macros + 4 legacy compat aliases)
- `src/Janset.SDL2.Mixer/Janset.SDL2.Mixer.csproj` — multi-TFM, ProjectReference→Core
- `src/Janset.SDL2.Mixer/Support/DisableRuntimeMarshalling.cs`
- Update `config/family-config.json` `families.mixer.headers[]` with the single header (Iteration 2 retired `scope/sdl2-mixer.headers.txt`)

---

## Implementation Order & Dependencies

```
Iteration 1: Item 1 (per-library infra) ✅ closed
  ├── Cross-cutting (header-list/policy/shims/oracle standardization)
  └── Enables Iteration 2: Config Surface Unification
       └── Enables Item 2 (Image bug fix) ── quick validation
            (also partially delivered by Item 1 S1-6 flags-detect side-effect)
            Enables Item 3 (GFX) ── lowest risk, validates multi-family pipeline
                 Enables Item 4 (TTF) ── C long + satellite-owned opaque handle
                      Enables Item 5 (Mixer) ── callbacks, highest runtime risk
```

Iteration 2 (config surface unification) lands between Item 1 and Item 2. Items 3, 4, 5 are sequential (each builds confidence). Item 2 can run in parallel with late-stage Item 1 since it only touches Image output and Item 1 S1-6 already delivers the underlying `[Flags]` fix; the explicit Item 2 closure remains valuable as a documented regression test. Cross-cutting standardization can be done at any point before Item 3.

---

## What This Roadmap Does NOT Cover

- Layer 2 typed public API projection (deferred — Constitution Roadmap M5)
- Layer 3 friendly overloads (deferred — Roadmap M6)
- Production source flip into `src/Janset.SDL2.*/` (deferred — Roadmap M7)
- `.generated-stamp` reproducibility metadata (deferred — M7)
- 7-RID CI matrix for AbiTests (deferred — production CI gate)
- SDL2_net bindings (not in scope for this expansion wave)
- Alimer-style CppAst comparison evidence (separate slice per ADR-004)
- Package smoke against real native packages (production gate)
