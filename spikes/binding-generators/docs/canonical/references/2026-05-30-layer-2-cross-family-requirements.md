# Layer 2 Cross-Family Requirements Matrix

**Date:** 2026-05-30
**Status:** Analysis artifact feeding the **library-agnostic** Layer 2 implementation plan. Ensures the L2 postprocess rewriters handle **every** SDL2 family's cases from the start (config-driven), so Core-first implementation does not forget satellite cases.
**Built on:** the Layer 2 design spec ([`../../../../docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md`](../../../../docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md)), the Phase A peer mapping ([`2026-05-29-layer-2-peer-mapping.md`](2026-05-29-layer-2-peer-mapping.md)), and five per-family requirement profiles (Core authored from config + generated output; Image/TTF/Mixer/GFX from parallel grounded analysis 2026-05-30).
**Motivation:** *"Library-agnostic olmalı altyapımız… böyle şeyler unutulur."* The clong example crystallized it — Core's clong methods are RETURN-only, so a Core-first design forgets TTF's clong **INPUT-param** Windows range-check. This matrix surfaces every such "Core-shaped assumption breaks on a satellite" case.

---

## 1. The matrix (pass × family)

`✓` = active work · `—` = no-op (must tolerate empty) · `⚠` = family-specific exception

| Pass | Core | Image | TTF | Mixer | GFX |
| --- | --- | --- | --- | --- | --- |
| **PublicTypedMethodProjection** (forwarders) | ✓ ~854 | ✓ 59 | ✓ | ✓ | ✓ 102 |
| ↳ **clong** sub-rule | ✓ 2 (returns) | — | ⚠ **5: 4 INPUT (range-check) + 1 return** | — | — |
| **TypedEnumProjection** ([Flags]) | ✓ allow-list ×5 + suffix | ⚠ IMG_InitFlags (suffix) — substitute at `int` param | — (only non-flags `TTF_Direction`) | ✓ MIX_InitFlags (suffix) | — (zero enums) |
| **StructFieldAccessorProjection** | ✓ | ⚠ IMG_Animation — **only `T**` field** | — (no transparent struct) | ✓ Mix_Chunk (POD) | — (FPSmanager has no enum fields) |
| **StructLayoutSequential** (universal) | ✓ | ✓ | ✓ | ✓ | ⚠ **FPSmanager missing `[StructLayout]`** (evidence) |
| **MacroHelperGeneration** (version + Type-A) | ✓ helper-lane + RW-file-loader | ⚠ version trio (own tokens) | ⚠ version trio + alias + `TTF_Render*` compat aliases | ⚠ version trio + `MIX_VERSION` alias | ⚠ **no version macro — 3 loose consts**; `M_PI` in 2 headers |
| **EndianRuntimeAlias** | ✓ AUDIO_*SYS, BYTEORDER (PIXELFORMAT_*32 frozen) | — | — (BOM ≠ endian, false-positive trap) | ⚠ **MIX_DEFAULT_FORMAT → cross-family alias to Core** | — |
| **ErrorRedirect** (satellite-only) | — (owns real funcs) | ✓ 2 | ✓ 2 | ⚠ **4 (incl. Mix_OutOfMemory)** | — (return-code errors, **0 macros**) |
| **OpaqueHandle / OwnerMode** | ✓ owner, 14+3 | — owns 0, `owner_mode:false` | ✓ owner, TTF_Font | ✓ owner, Mix_Music | — owns 0, `owner_mode:false` |
| ↳ **cross-family Core-type consume** | n/a | ⚠ RWops/Renderer/Texture **by-value** + Surface/version **by-pointer** | ⚠ RWops by-value + Surface*/Color/version*; **dual owner+consumer** | ⚠ RWops by-value; **dual owner+consumer** | ⚠ **Renderer by-value + Surface* by-pointer in same fn** |
| **Callbacks** | ✓ (struct-field fp, force-opaque) | — | — | ⚠ **6 typedefs / 2 lifetime classes (DEFERRED)** | — |
| **Naming normalization** | `SDL_` uniform | ⚠ `IMG_` ≠ class `SDL_image` | `TTF_` | `Mix_`/`MIX_` | ⚠ **mixed: 35 `SDL_` + 67 bare, NO prefix** |

---

## 2. Per-pass exceptions — the "Core-first would forget" catalog

**clong (PublicTypedMethodProjection sub-rule).** Core = 2 RETURNS (lossless `ulong` normalize). **TTF = 4 INPUT params** (`TTF_OpenFontIndex/RW/DPI/DPIRW`, `long index`) requiring a **Windows range-check** on the public wrapper before narrowing to the 32-bit Windows `CLong` (Constitution §"C long" L262: *"with Windows range checks for input parameters when needed"*) + 1 RETURN (`TTF_FontFaces`). The Compat tier already proves INPUT and RETURN are **two distinct rewrite shapes** (`Compat/SDL_ttf.g.cs:35-46` casts the arg vs `:147-159` casts the result). Image/Mixer/GFX = none. **Trap:** `double` (Mixer/GFX) and `unsigned int` (GFX) must NOT be misclassified as clong. → The clong sub-rule must implement **both** the return-normalize and the input-range-check paths from day one, even though Core only exercises returns.

**TypedEnumProjection.** **Substitute the flag enum at raw `int`/`uint` positions, not only where the raw type is already the enum** — `IMG_Init(int flags)` is emitted as raw `int` (`Image Modern:39`) yet should be `IMG_InitFlags` (the Silk gap, peer-mapping dim 3). **Do NOT synthesize a `[Flags]` enum from bare bitmask const-ints** — TTF's `TTF_STYLE_*` are loose `const int`, there is no `TTF_FontStyleFlags` type (`SDL_ttf.h:433`). Empty allow-list is normal (Image/Mixer rely on suffix; TTF/GFX have none).

**Structs + universal StructLayout.** **A universal `[StructLayout(LayoutKind.Sequential)]` pass is required across ALL families** — ClangSharp omits it; GFX's `FPSmanager` ships bare (`SDL2_framerate.g.cs:6`), and the same applies to `SDL_Color`/`SDL_Rect`/`SDL_AudioSpec`/etc. (next-iteration-plan backlog, FPSmanager-evidenced). **Image's `IMG_Animation` has the only `T**` field** (`SDL_Surface** frames`) across all families — accessor pass must leave `T**`/`T*` as raw pointers at L2 (Litmus A). Field-accessor generation is a **no-op when a struct has no enum-typed fields** (Image, GFX, Mixer's Mix_Chunk).

**MacroHelperGeneration.** Per-family version tokens (`SDL_IMAGE_*`, `SDL_TTF_*`, `SDL_MIXER_*`) — Core-only emission forgets the satellite trios. **Double-alias** version macros (TTF `TTF_VERSION`/`SDL_TTF_VERSION`; Mixer `MIX_VERSION`/`SDL_MIXER_VERSION`). `*_COMPILEDVERSION` is **already a frozen L1 const** — generator must not double-emit. **GFX has NO function-like version macro** (3 loose `const int` — a synthesized helper must build from consts, not a macro). `M_PI` declared in **two GFX headers** — don't double-emit (use `Math.PI`). SDL2 `X*1000+Y*100+Z` spacing (not SDL3).

**EndianRuntimeAlias.** **Mixer's `MIX_DEFAULT_FORMAT` is a CROSS-FAMILY alias** (= Core's `AUDIO_S16SYS`) — the classifier must resolve alias targets in **other families**, type-widen `int`→`ushort`, and it has a **build-order dependency on Core's audio aliases**. Core's `PIXELFORMAT_*32` are **enum members** → frozen LE (can't be runtime-conditional). **Trap:** TTF's `UNICODE_BOM_NATIVE/SWAPPED` are constant codepoints, NOT endian — a name-token classifier must not false-positive on "SWAPPED"/"NATIVE".

**ErrorRedirect (satellite-only pass).** Per-family **variable-size macro set**: Image 2, TTF 2, Mixer **4** (incl. `Mix_OutOfMemory`→`SDL_OutOfMemory`), GFX **0** (return-code errors). Core never exercises this pass (it owns the real functions). Redirect targets Core's **public** L2 (`IMG_GetError() => SDL.SDL_GetError()`), forwards fmt-only (no `__arglist`). → **forces Core-L2-before-satellite-L2 sequencing.**

**OpaqueHandle / OwnerMode + cross-family consume.** **`owner_mode:false`-AND-owns-zero-handles** (Image, GFX) → the pass emits nothing for the family's own types. **TTF & Mixer are dual owner+consumer** (emit their own `TTF_Font`/`Mix_Music` while consuming Core handles by-value). **Per-Core-type by-value-vs-by-pointer discrimination:** `SDL_RWops`/`SDL_Renderer`/`SDL_Texture` collapse to **by-value** Pattern B handles; `SDL_Surface`/`SDL_version` stay **pointers** (POD, not handles). GFX uses both in one function. **Generated satellite files carry NO `using SDL2;`** → L2 must resolve Core types cross-namespace explicitly. (Non-deps to NOT invent: TTF does not use `SDL_Renderer` (v2.24); Mixer does not use `SDL_AudioSpec`.)

**Callbacks (DEFERRED, Mixer-only at L2 scope).** Mixer = 6 typedefs / 7 registration fns / **two lifetime classes** — 5 persistent audio-thread/background-thread roots (register/unregister + null-to-disable; `Mix_RegisterEffect` carries **two** callbacks with divergent lifetimes) vs 1 synchronous call-scoped (`Mix_EachSoundFont`). Modern `delegate*`+`[UnmanagedCallersOnly]` vs Compat delegate+`GC.KeepAlive` split. **Out of foundation scope** — its own slice (next-iteration-plan Forward Backlog 4-element policy).

**Naming.** **GFX requires ZERO name normalization** — 35 `SDL_`-prefixed + 67 bare-name functions, no family prefix; stripping any prefix aliases Core or corrupts bare names. Image's `IMG_` prefix ≠ class `SDL_image`; raw-class casings are irregular (`SDL_imageNative`). → L2 mirrors generated names 1:1; never derive/strip names from the class name. (`SDL2_*_SCOPE` export macros are resolved at L1 — but no pass may assume `extern DECLSPEC` text.)

---

## 3. Cross-cutting requirements that make the infrastructure library-agnostic

These are the design invariants the matrix forces — the rewriters must satisfy all of them so satellites activate by **config only**, with no rewriter changes:

1. **No-op tolerance.** Every pass accepts an empty per-family work-set without erroring or injecting dead machinery (clong/endian/error/callbacks/enums are all empty for ≥1 family). Image/TTF/GFX are the empty-input regression guards.
2. **Config owns WHICH, code owns HOW** (Constitution §"Manifest Configuration Vs Code-Owned Policy"). Every family-specific fact above is config (`clong_methods`, `flags_enums`, `opaque_handles`, error-macro set, endian roster, version tokens, `owner_mode`, `public_class`); the rewriter mechanism is family-blind code.
3. **Universal StructLayout pass** — `[StructLayout(LayoutKind.Sequential)]` on every layout-free struct, all families (not Core-special-cased).
4. **Cross-family type resolution** — by-value-handle vs by-pointer-struct per Core type; explicit cross-namespace resolution (no `using SDL2;`); consume-don't-re-own for satellites.
5. **Cross-family alias resolution** — Mixer endian alias references Core's audio aliases.
6. **Sequencing (hard):** Core L2 must land before any satellite L2 — error redirects, endian aliases, and handle consumption all resolve into Core's public L2.
7. **clong INPUT range-check path** built from day one (TTF), not deferred.
8. **No name normalization** (GFX mixed naming) — names mirror generated 1:1.

---

## 4. Implication for the implementation plan

The plan stays **Core-first for implementation/test**, but the rewriters are **designed family-complete** (every case above handled via config). Concretely:

- **The foundation rewriters are written family-blind and tested against Core**, with **fixtures that exercise the satellite-only cases** (clong-INPUT, `T**` field, cross-family by-value/by-pointer, empty-config no-op) in the self-tests **even before the satellite is wired** — so the case is implemented, not forgotten.
- **Universal passes** (PublicTypedMethodProjection, StructLayoutSequential, TypedEnumProjection-at-int-positions) land in the foundation.
- **Satellite activation = config + a thin per-family slice** (no rewriter change): add `public_class`, error-macro set, version tokens; run the pipeline; snapshot.
- **Deferred to their own slices:** Mixer callback rooting (4-element policy), full Type-A macro generation (#3), endian cross-family alias (after Core's audio aliases land).
- **Revised slice sequence:** (1) Core L2 foundation — family-blind projection + universal StructLayout + clong (both paths) + API snapshot + self-tests incl. satellite-case fixtures; (2) per-satellite activation (Image → GFX → TTF → Mixer, simplest-first; Mixer last for callbacks); (3) Type-A macros; (4) endian aliases; (5) Mixer callback policy.

---

## 5. References

- [`2026-05-29-layer-2-peer-mapping.md`](2026-05-29-layer-2-peer-mapping.md) — Phase A peer mapping.
- Layer 2 design spec — [`../../../../docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md`](../../../../docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md).
- [`../binding-generator-constitution.md`](../binding-generator-constitution.md) — §"C long" (L262 range-check), §"Manifest Configuration Vs Code-Owned Policy", §"Opaque Handles", §"Structs And Unions".
- Satellite analyses: [`../satellites/sdl2-image-header-analysis.md`](../satellites/sdl2-image-header-analysis.md), [`sdl2-ttf-header-analysis.md`](../satellites/sdl2-ttf-header-analysis.md), [`sdl2-mixer-header-analysis.md`](../satellites/sdl2-mixer-header-analysis.md), [`sdl2-gfx-header-analysis.md`](../satellites/sdl2-gfx-header-analysis.md), [`sdl2-endian-platform-macro-consolidation.md`](../satellites/sdl2-endian-platform-macro-consolidation.md), [`sdl2-satellite-error-function-consolidation.md`](../satellites/sdl2-satellite-error-function-consolidation.md), [`sdl2-function-like-macro-consolidation.md`](../satellites/sdl2-function-like-macro-consolidation.md).
- [`../../next-iteration-plan.md`](../../next-iteration-plan.md) — Forward Backlog (StructLayout pass, Mixer callback 4-element policy, endian aliases).
