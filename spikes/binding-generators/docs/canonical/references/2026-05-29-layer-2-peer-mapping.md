# SDL2 Layer 2 Peer Mapping

**Date:** 2026-05-29
**Status:** Research artifact (Layer 2 Phase A). Maps how four peer bindings solve the typed-public-API / friendly-overload problem onto our L1/L2/L3 model under the ratified Layer 2/3 litmus, and derives the proposed postprocess-rewriter pipeline. Feeds the Layer 2 brainstorm → spec.
**Scope:** ppy/SDL3-CS, alimer-bindings-sdl, Silk.NET (SDL + SilkTouch), SDL2-CS — mapped across 10 dimensions to our three-layer contract. Extends (does not redo) the macro/error/endian consolidation docs.
**Method:** four parallel read-only research agents, one per peer, each reframing source-generator / companion mechanisms as "what would the equivalent postprocess rewriter over committed `.g.cs` do."

---

## 0. Framing — the locked decisions this mapping is built on

These were ratified before the research and are the lens for every "map to ours" call:

- **Litmus A (Layer 2/3 seam):** *"Does the method allocate, or change arity / call-shape vs the raw C signature?"* — **No → Layer 2** (typed 1:1: typed handles/enums, same arity, pointers stay pointers, no alloc, no marshalling). **Yes → Layer 3** (`string`/span/`out`/`ref`/array — allocates or changes shape). Matches Constitution §"Layer Contract" L3 list and Roadmap §"Layer 3 — Friendly Overload Projection" baseline patterns.
- **Cross-family calls:** satellites call Core's **public Layer 2** (`IMG_GetError() => SDL.SDL_GetError()`), never Core's internal raw. `[InternalsVisibleTo]` is for test projects only; Core-owned public **types** are shared via nested-namespace + ProjectReference (no IVT). Consequence: **Core Layer 2 lands before satellite Layer 2.**
- **Macros/helpers:** prefer **generated** — ideally parser-marked (the `[NativeTypeName("#define ...")]` annotation preserves macro spelling) then postprocess-emitted; hand-written companions are a **last resort** for genuinely un-generatable macros (token-paste, ASM, compile-time). No "magic companion" as the primary mechanism.
- **Mechanism home:** postprocess rewriters over committed `.g.cs`, **not** Roslyn source generators (Constitution §"Generator Home"). Internal raw ABI stays `internal SDLNative`.

Peers are **evidence, not authority** (Constitution §"Authority Order" rank 6). Where a peer conflicts with policy, policy wins.

---

## 1. Headline findings

1. **No peer has a true Layer 2 as we define it.** ppy, Alimer, and Silk all **collapse L1+L2 into a single public class** (the `[DllImport]`/`[LibraryImport]`/calli surface *is* the public API); SDL2-CS collapses raw + wrapper into one public class via an `INTERNAL_` naming (not accessibility) convention. Our **internal-raw + distinct public-typed-L2 split is structurally novel** among these peers. **Implication:** we mine peer *signals* (detection rules) and *overload shapes*, not their output structure — there is no peer L2 to copy.

2. **Our inversion deletes ppy's `Unsafe_` machinery — the cleanest dividend of internal-raw.** ppy must rename every string-returning raw function to `Unsafe_X` with `[DllImport(EntryPoint="X")]` *solely* so the public friendly overload can claim the real name without colliding (its raw is public). Because **our raw is `internal SDLNative`**, the friendly-named method lives at L2/L3 with zero collision and zero rename. We do **not** port the `Unsafe_` convention. (ppy `SDL_error.g.cs:44`; ours `Modern/SDL_error.g.cs:25`.)

3. **The L3 overload pipeline is portable from Silk's two-tier overloader design.** SilkTouch separates **in-place signature rewrites** (same class) from **delegating wrappers** (separate file) *specifically to avoid pointer-vs-span signature collisions* (`SpanOverloader.cs:16`). Our L2/L3 class/file split gives us that collision-avoidance for free and defines the rewriter pipeline (§4).

4. **Macros: our ClangSharp + `[NativeTypeName]` provenance can generate where peers hand-write.** Alimer is forced to hand-author ~25 helpers because CppAst with `ParseMacros=false` only exposes macro tokens (no expansion). ppy hand-writes ~115 `[Macro]` methods behind an **inert** marker attribute. All peers' hand-written sets ≈ our Constitution helper-candidate lane — so their bodies are **expression oracles** for our *generated* Type A emitters. This is concrete evidence **for** the ClangSharp toolchain choice.

5. **SDL_bool, error-redirect, and endian are policy-validated by peers** — encode them as identity/exclusion passes, not novel design. SDL2-CS is the literal oracle for the error-redirect locked decision and the endian runtime-alias shape.

---

## 2. Cross-peer matrix

Posture legend: **PR** = public-raw (no L1/L2 split); **HW** = hand-written; **gen** = generated.

| # | Dimension | ppy/SDL3-CS | alimer | Silk.NET | SDL2-CS | → Ours (target) |
|---|---|---|---|---|---|---|
| 1 | Raw ABI visibility / cross-asm | PR, `public partial class SDL3`, `[DllImport]` public | PR, one `public partial class SDL3`, `[LibraryImport]` public | PR, calli via SG at consumer build, effectively public | flat: `public extern` + `private INTERNAL_` in one public class | **internal `SDLNative`** raw + distinct **public `SDL` L2**; Compat `[DllImport(ExactSpelling)]` + Modern `[LibraryImport]` |
| 2 | Opaque handles | empty struct used as `SDL_Window*` (pointer-as-handle) | **Pattern B `readonly struct X(nint):IEquatable` + implicit ops** | empty struct used as `Window*` | **`IntPtr` everywhere** (zero type safety) | Pattern B value struct over nint, **explicit ops only**, pointer→by-value rewrite |
| 3 | Typed enums | ClangSharp enums + HW `[Typedef]` mirror enums; `[Flags]` HW | gen enums, huge HW prefix/rename tables; `[Flags]` HW allowlist | gen `enum:int`, **`[Flags]` selective (7/56)**; flag *params* stay `uint` | HW enums, implicit int backing; `[Flags]` HW | gen enums **explicit backing**, **auto-`[Flags]`** (suffix + family allow-list) |
| 4 | Struct passing | pointer; HW typed-property accessors over fields | value/pointer; `__FixedBuffer` indexer; **over-maps Rect→Drawing.Rectangle** | value/pointer; nullable-ctor helper baked in; **over-maps Rect→Maths.Rectangle<int>** | `[StructLayout(Sequential)]`; `ref`/`out`; `PtrToStructure` return-hop | `[StructLayout]` POD/union; pointers stay at L2; typed-property accessors **generated** at L2 |
| 5 | SDL_bool | `SDLBool` **byte**-backed + implicit bool (SDL3-correct) | `SDLBool` struct + **implicit bool** (cautionary) | plain `enum SdlBool:int` 1:1 ✓ | plain `enum SDL_bool` (int) 1:1 ✓ | L2 returns typed **`SDL_bool:int` enum**; bool conversion = L3; predicate-rule may promote at L2 |
| 6 | String overloads | SG: `Utf8String` param + `PtrToStringUTF8` return; **`Unsafe_` rename** | gen: 3 `[LibraryImport]` siblings (`byte*`/span/string) + `Ptr`+`ConvertToManaged` | gen: `byte*`/`ref readonly byte`/`LPUTF8Str string` + `*S` return; SG body | HW `INTERNAL_` hop: `Utf8Encode`/`UTF8_ToManaged`; **no span**; inconsistent strategy | L3 rewriters: `string`+`ReadOnlySpan<byte>` over L2 `byte*`; **no `Unsafe_` needed** |
| 7 | out/ref/Span | **almost none** (only `SDLArray<T>` HW wrappers) | HW out-heuristic on raw + HW Span returns | **cartesian ptr×ref explosion** (16/func) + Span extensions | `out`/`ref` scalars; **`IntPtr`-slot optional-output quartets**; **no Span** | L2 keeps pointer (null=null); L3 **selective** `out`/`ref`/`Span` via curated heuristic; skip handles/`void*` |
| 8 | Function-like macros | ~115 HW `[Macro]` (inert marker) | ~25 HW (CppAst can't gen) | ~8 HW | ~6 HW (minimal) | **generate Type A** (parser-marked `[NativeTypeName("#define")]`); HW only irregular (Type C) |
| 9 | Error redirect | core-only (no redirect); `__arglist` macro helpers (rejected) | core-only (none) | core-only (incidental `SdlException`) | **`IMG_GetError()=>SDL.SDL_GetError()`** ✓ (the oracle) | satellite L2 redirect to Core public L2; exclude macro from L1 |
| 10 | Endian/platform macros | **`static readonly` + `BitConverter.IsLittleEndian`** ✓ | core-only, endian header not parsed | dropped → BCL `BinaryPrimitives`; **host-snapshot consts (determinism hazard)** | **`BitConverter.IsLittleEndian` runtime alias** ✓ (the oracle) | L1 raw frozen-but-auditable; **L2 runtime-aware alias** via `[NativeTypeName]` classifier |

---

## 3. Dimension synthesis — what each means for our mechanism

**1. Visibility / cross-assembly.** The peer consensus (public-raw) is exactly what our Evidence Gates forbid. SDL2-CS's `INTERNAL_`-private + public-wrapper *is* our L1→L2/L3 relationship, collapsed into one class — our split makes the same boundary an accessibility boundary. Satellite cross-family: SDL2-CS already calls Core *public* `SDL.SDL_GetError()`, validating our locked decision.

**2. Handles.** **Alimer is the closest precedent** — it emits nearly our exact Pattern B (`readonly partial struct SDL_Window(nint value) : IEquatable<SDL_Window>`, `IsNull`/`Null`/equality) — validating the shape. **But it adds implicit `nint↔X` conversions; we keep explicit-only** (implicit lets a raw `nint` silently become any handle — type-safety erosion, Constitution §"Opaque Handles"). ppy/Silk's empty-struct-as-pointer is closer to our *raw* representation than our public handle. SDL2-CS's blanket `IntPtr` is the central divergence to reverse. **Foreign types** (Vulkan/Win32/X11) → `nint` for all of them (SDL2-CS's blanket-IntPtr *accidentally* matches our §"Foreign Type Boundary Policy" here); Alimer/Silk **over-map SDL-owned** structs to foreign/BCL types (`SDL_Rect`→`Rectangle<int>`) — we do not.

**3. Enums.** All peers substitute typed enums into signatures (our L2 target). Our differentiators stay: **explicit underlying type** + **auto-`[Flags]`** (suffix + family allow-list) vs peers' hand-applied/heuristic flags. SDL2-CS's `[Flags]` set (`IMG_InitFlags`, `MIX_InitFlags`) is a useful oracle confirming our suffix rule fires. **Silk substitutes the enum at enum-typed positions but leaves bitmask `Uint32` params as raw `uint`** — we want the typed flag enum at those positions too.

**4. Structs.** Pointer/value passing 1:1 = our L2. ppy's **typed-property-over-raw-field** accessors (`SDL_CommonEvent.Type => (SDL_EventType)type`) are a **generatable L2 pattern** for us (from the enum roster + `[NativeTypeName]`), where ppy hand-writes them. String-returning field accessors (`GetText()`) allocate → L3. Reject the foreign over-mapping and the nullable-ctor-baked-into-struct (that's an L3 concern, not an L2 type change).

**5. SDL_bool.** Silk + SDL2-CS validate our default exactly (typed `enum:int`, returned 1:1, no auto-bool). **Two traps:** ppy's `SDLBool` is **byte-backed** (correct for SDL3, **ABI-wrong for our int-backed SDL2 `SDL_bool`**); Alimer bakes **implicit `bool`** into the L2 type and ends up with a confusing coexisting hand-written enum — validating our "bool conversion is L3, L2 returns the typed enum."

**6. Strings.** SDL2-CS proves the body template (`Utf8Size → stackalloc → Utf8Encode → call → [UTF8_ToManaged]`); Silk/Alimer prove the `Ptr`-rename + `ConvertToManaged`(+`SDL_free` when non-const) return template. All string overloads are **L3** under Litmus A. Our wins: **no `Unsafe_` rename** (internal raw), a **uniform UTF-8 policy** (SDL2-CS inconsistently mixes `stackalloc`/heap/`[MarshalAs(LPStr)]` — the last is ANSI, a latent bug), and a **`ReadOnlySpan<byte>` tier** SDL2-CS lacks entirely.

**7. out/ref/Span — the densest mechanism finding.** Litmus A puts `out`/`ref`/`Span`/array all in **L3**; L2 keeps the pointer (NULL = `null`). **Anti-pattern (Silk + SDL2-CS):** the combinatorial overload explosion — Silk emits 2^N overloads per N pointer params (16 for `GetOriginalMemoryFunctions`), SDL2-CS hand-enumerates `IntPtr`-slot quartets for optional outputs (`SDL_GetMouseState`, `SDL_RenderCopyEx`). Both are artifacts of having **no pointer-typed public layer**. Our pointer-preserving L2 covers all-NULL/any-NULL with **one** signature, so L3 adds `out`/`ref`/`Span` **selectively** via a curated output/buffer heuristic. **Adopt verbatim:** Silk's `RefOverloader` rule to **skip opaque-handle params and `void*`** (`RefOverloader.cs:18-29`) and the **mandatory de-dup pass** (`Overloader.cs:155`). SDL2-CS's blittable multi-`out` (`SDL_GetWindowSize(out int w, out int h)`) and `out IntPtr`→ our typed `out SDL_Window` show the L3 by-ref shapes.

**8. Function-like macros.** (Extends `sdl2-function-like-macro-consolidation.md`.) Peer spectrum: ppy ~115 → Alimer ~25 → Silk ~8 → SDL2-CS ~6, **all hand-written**, all ≈ our helper-candidate lane. The decisive evidence: **Alimer can't generate them** (CppAst `ParseMacros=false` → tokens only), so it's forced to hand-author — whereas **our ClangSharp path preserves `[NativeTypeName("#define ...")]`**, making a parser-marked → postprocess-templated path feasible for the regular Type A shapes (`SDL_PIXELTYPE`, `SDL_ISPIXELFORMAT_*`, `SDL_WINDOWPOS_*`, `SDL_VERSIONNUM`). Reserve hand-authoring for genuinely irregular bodies (`SDL_BYTESPERPIXEL`'s YUY2 branch). Use peer bodies as **expression oracles** — but **not** ppy's SDL3 `SDL_VERSIONNUM` (`X*1000000+Y*1000+Z`); SDL2 is `X*1000+Y*100+Z` (Constitution §"Constants And Macros").

**9. Error redirect.** (Extends `sdl2-satellite-error-function-consolidation.md`.) SDL2-CS is the literal oracle of our locked decision: `IMG_GetError() => SDL.SDL_GetError()` (`SDL2_image.cs:256`), satellite macro excluded from L1 raw (already in `rsp/sdl2-image.rsp`). Surface `Mix_ClearError`, defer `Mix_OutOfMemory`. ppy/Alimer/Silk are core-only → no cross-family evidence, confirming the consolidation doc stands alone. **Trap:** ppy's error/log macro helpers use `__arglist`, which Constitution §"C Variadics" L233 **explicitly rejects** — use fmt-only fixed-prefix helpers.

**10. Endian/platform macros.** (Extends `sdl2-endian-platform-macro-consolidation.md`.) ppy + SDL2-CS both validate the recommendation: keep L1 raw evaluated-but-auditable, add an **L2 runtime-aware `static readonly` alias** via `BitConverter.IsLittleEndian` (`SDL2_mixer.cs:62`, ppy `SDL_pixels.cs:150`). A `[NativeTypeName]`-classifier pass detects the endian aliases (`AUDIO_*SYS`, `MIX_DEFAULT_FORMAT`, `SDL_PIXELFORMAT_*32`, `SDL_BYTEORDER`). **Determinism note:** Silk snapshots host platform `#define`s as consts — a hazard under our §"Generation Determinism Contract"; do not project host-evaluated platform macros as runtime truth.

---

## 4. Proposed postprocess-rewriter pipeline (the "how")

The mechanism that falls out of the mapping. Each transform is a deterministic rewriter over committed `.g.cs`. **L2 passes** produce the public typed 1:1 surface; **L3 passes** are *delegating-wrapper* rewriters (separate file/region, SilkTouch-style, to avoid signature collisions). Order matters; a final de-dup pass is mandatory.

**Layer 2 (typed 1:1 — no alloc, same arity):**
1. **`PublicTypedMethodProjection`** — for each internal `SDLNative` method, emit a public method on the family class (`SDL`, `SDL_image`, …) that forwards to it, with typed handles/enums already substituted and pointers preserved. (No peer has this; it's the core net-new pass.)
2. **`HandleParameterTyping`** — already largely done by Layer 1's `OpaqueHandleEmitRewriter` (Pattern B, single-pointer→by-value); L2 reuses the handle types unchanged. Keep **explicit-only** conversions (reject Alimer's implicit).
3. **`TypedEnumProjection`** — substitute typed enums at return/param positions, including bitmask `Uint32` flag positions (where Silk stops short).
4. **`StructFieldAccessorProjection`** — emit typed read-only properties over raw struct fields whose type maps to an enum (ppy's hand-written pattern, generated from the roster). String-returning accessors are deferred to L3.
5. **`MacroHelperGeneration` (Type A)** — template pure-evaluable function-like macros from the `[NativeTypeName("#define ...")]` allowlist into public `static` methods.
6. **`EndianRuntimeAlias`** — emit `static readonly` runtime-aware aliases for endian/platform-conditioned constants; keep L1 raw constant auditable.
7. **`ErrorRedirect` (satellites)** — emit satellite error methods forwarding to Core's public L2 (`IMG_GetError() => SDL.SDL_GetError()`).

**Layer 3 (friendly — allocates or changes shape; delegating wrappers calling L2):**
8. **`StringOverload`** — `string` + `ReadOnlySpan<byte>` over L2 `byte*` (uniform UTF-8; `byte*`-return → `string?`). No `Unsafe_` rename needed.
9. **`RefOutOverload`** — `out T`/`ref T`/`in T` over single-element pointer params; **skip handle params and `void*`** (Silk rule).
10. **`SpanOverload`** — `Span<T>`/`ReadOnlySpan<T>` over counted-buffer params via `GetPinnableReference()` delegation.
11. **`BoolPredicateOverload`** — `bool` overloads / predicate-promotion over `SDL_bool`-returning methods.
12. **`MacroHelperGeneration` (Type B)** — convenience macros that delegate to raw (version struct-fill, file loaders).
13. **`DeduplicateOverloads`** — mandatory collision/dup removal across passes 8–12 (Silk's `RemoveDuplicates`).

**Type C macros** (token-paste, ASM, compile-time, `__arglist` variadics): hand-authored or skipped — last resort only, with the rationale recorded.

This stays consistent with §"Manifest Configuration Vs Code-Owned Policy": **which** symbols/macros/flags a pass touches is config-scope (per-family fact); **how** each pass emits is code-owned policy.

---

## 5. Anti-patterns rejected & traps to encode

| Item | Source | Why rejected / trap | Our stance |
|---|---|---|---|
| Public raw ABI | all peers | violates §"Internal Raw ABI" / Evidence Gates | internal `SDLNative` |
| `Unsafe_` rename machinery | ppy | pure public-raw tax | obviated by internal raw |
| Combinatorial ptr×ref / `IntPtr`-slot overload explosion | Silk, SDL2-CS | surface bloat (16/func); artifact of no pointer-typed L2 | pointer-preserving L2 + **selective** L3 by-ref |
| Implicit conversions (`nint↔handle`, `bool↔SDLBool`) | Alimer, ppy | erodes type safety; pulls L3 conversion into L2 type | explicit-only handle ops; bool conversion at L3 |
| `SDLBool` byte-backing | ppy | SDL3-correct, **ABI-wrong for SDL2 int-backed `SDL_bool`** | int-backed enum |
| `__arglist` variadics | ppy | Constitution §"C Variadics" L233 rejects | fmt-only fixed-prefix |
| Foreign over-mapping (`SDL_Rect`→`Rectangle<T>`) | Alimer, Silk | replaces SDL-owned types with foreign/BCL | emit our own SDL structs |
| Reflection marshalling (`PtrToStructure`, `[MarshalAs(LPArray)]`) | SDL2-CS | forbidden on Modern by `[assembly: DisableRuntimeMarshalling]` | blittable + span/`fixed` |
| Host-snapshot platform consts | Silk | determinism hazard (§"Generation Determinism Contract") | runtime alias / drop |
| Hand-maintained rename/exclude tables | Alimer | per-version maintenance cost; no provenance | parser-marked, provenance-preserving |

---

## 6. Corrections to prior assumptions

- **Silk.NET does NOT "drop `[Flags]` entirely."** The onboarding prompt's premise is stale for the checked-out version: Silk emits `[Flags]` selectively (7/56 enums, incl. `WindowFlags`, `WindowFlags.gen.cs:13`). It's heuristic / un-suffixed (no allow-list discipline), so it's evidence **for** our `[Flags]` policy, not against. Adjust any prior reasoning that leaned on "Silk drops Flags."
- **ppy's `[Macro]` attribute is inert** (`[Conditional("NEVER")]`, scanned by nothing) — a documentation marker that drives no generation. Reinforces our "a parser annotation that drives emission beats an inert marker."

---

## 7. Open decisions for the Layer 2 brainstorm

1. **SDL_bool seam edge** — pin the "predicate-like" rule that promotes specific functions to `bool` at L2 (vs default typed `SDL_bool` enum). Name the detection signal.
2. **Implicit handle conversion** — confirm explicit-only (current `next-iteration-plan` "Accepted Tradeoffs" keeps explicit; Alimer is the implicit counter-evidence). Revisit only on preview friction.
3. **Macro generation scope** — which Type A macros to generate now vs defer; which are irregular enough to hand-author (Type C). Seed from the helper-candidate lane + peer oracle bodies.
4. **Satellite `_VERSION_ATLEAST` / `_VERSION(struct)`** — generate per-family (ppy) or rely on Core (SDL2-CS doesn't surface)? Companion-helper-policy decision.
5. **`Mix_OutOfMemory`** — defer (SDL2-CS conservative) or surface.
6. **Overload-generation heuristic for `out`/`ref`/`Span`** — the curated rule for *which* pointer positions become by-ref/span at L3 (output/buffer detection), avoiding the combinatorial explosion. Borrow Silk's Flow/const signals; bound them.
7. **First family + sequencing** — Core first (forced: satellites redirect to Core public L2). Confirm the per-family rollout order.
8. **Layer 2 API snapshot review** — when to introduce the PublicApiGenerator/Verify snapshot (Roadmap exit evidence) and its multi-agent review gate.

---

## 8. References

**Policy (authority):**
- [`binding-generator-constitution.md`](../binding-generator-constitution.md) — §"Layer Contract", §"Opaque Handles", §"Enums", §"Constants And Macros", §"Foreign Type Boundary Policy", §"C Variadics", §"SDL_bool", §"Generation Determinism Contract", §"Manifest Configuration Vs Code-Owned Policy", §"Evidence Gates".
- [`binding-generator-roadmap.md`](../binding-generator-roadmap.md) — §"Layer 2 — Typed Public API Projection", §"Layer 3 — Friendly Overload Projection".

**Prior consolidation (this doc extends, does not redo):**
- [`../satellites/sdl2-function-like-macro-consolidation.md`](../satellites/sdl2-function-like-macro-consolidation.md) — macro taxonomy (Type A/B/C), per-peer comparison.
- [`../satellites/sdl2-satellite-error-function-consolidation.md`](../satellites/sdl2-satellite-error-function-consolidation.md) — error-redirect policy.
- [`../satellites/sdl2-endian-platform-macro-consolidation.md`](../satellites/sdl2-endian-platform-macro-consolidation.md) — endian/platform runtime-alias policy.
- [`ppy-reference-analysis-2026-05-25.md`](ppy-reference-analysis-2026-05-25.md) — prior ppy companion-class baseline.

**Active tracking & decisions:**
- [`../../next-iteration-plan.md`](../../next-iteration-plan.md) — Layer 2 framing + Forward Backlog.
- [`../../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 (Reopened); this mapping reinforces the ClangSharp choice (dim 8).

**Peer sources (file:line evidence in the per-peer research; clones under `spikes/binding-generators/references/`, `external/sdl2-cs/`):**
- ppy/SDL3-CS — `SDL3-CS.SourceGeneration/FriendlyOverloadGenerator.cs`, `SDL3-CS/Utf8String.cs`, companion `.cs`, `generate_bindings.py`.
- alimer-bindings-sdl — `src/Generator/CsCodeGenerator*.cs`, `src/Alimer.Bindings.SDL/SDL*.cs`.
- Silk.NET — `src/Core/Silk.NET.SilkTouch/` (Overloader pipeline), `src/Windowing/Silk.NET.SDL/` (`*.gen.cs`).
- SDL2-CS — `external/sdl2-cs/src/SDL2.cs` + satellite files.
