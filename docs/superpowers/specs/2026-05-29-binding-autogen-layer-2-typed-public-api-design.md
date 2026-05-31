# Binding Autogen — Layer 2 Typed Public API Projection (Design Spec)

**Date:** 2026-05-29
**Status:** Design spec (brainstorm output). Designs the **full L2+L3 typed/friendly projection pipeline**; the **first implementation slice is SDL2.Core, Layer 2 only**. Feeds `writing-plans`. No commit until the AGENTS.md approval gate; pending Deniz review.
**Built on:** Phase A peer mapping ([`2026-05-29-layer-2-peer-mapping.md`](../../../spikes/binding-generators/docs/canonical/references/2026-05-29-layer-2-peer-mapping.md)), Constitution §"Layer Contract", Roadmap §"Layer 2 — Typed Public API Projection" / §"Layer 3 — Friendly Overload Projection".
**Scope discipline:** the design covers L2 *and* L3 so the architecture composes and breadth is settled; only **L2 for Core** is implemented in the first slice. L3 + remaining families follow in their own slices.

---

## 0. Ratified decisions (the locked frame)

Settled interactively during the brainstorm. Each is a binding input to the design below.

| # | Decision | Summary |
| --- | --- | --- |
| D1 | **Brainstorm scope = design-whole / implement-L2-first** | Full L2+L3 pipeline designed now; first slice ships Core L2 only. |
| D2 | **Layer 2/3 litmus (Litmus A)** | *"Does the public method allocate, or change arity / call-shape vs the raw C signature?"* No → Layer 2. Yes → Layer 3. |
| D3 | **Safe-Alternative Guarantee** | The public-release surface never forces the caller into `unsafe`. The raw pointer form may exist (L2, zero-overhead) but is never the only way to call a function. |
| D4 | **Breadth = comprehensive-safe; optionals via `T? = null`** | Every pointer position a caller would hold gets a safe form; optional struct pointers collapse to a single `T? = null` overload (no cartesian explosion). |
| D5 | **String axis = separate `string` + `ReadOnlySpan<byte>`** | Two explicit overloads; no unified `Utf8String` ref-struct; no `ReadOnlySpan<char>`. |
| D6 | **SDL_bool = always the typed enum (Option B)** | L2 and L3 return/accept `SDL_bool`; no `bool` conversion, no implicit operator, no predicate rule. |
| D7 | **Function-like macros = #3 (roster + per-kind emitters)** | ClangSharp does not surface function-like macros; a curated config roster (WHICH) + code-owned per-kind emitters (HOW) generate them; genuinely-irregular ones hand-authored as last resort. |
| D8 | **Endian macros = auto-seeded roster + code emitter** | Differential LE/BE parse seeds (audit-time) a config roster; code emits `BitConverter.IsLittleEndian` `static readonly` aliases. Enum-member `PIXELFORMAT_*32` frozen LE. |
| D9 | **Error redirect = convention-derived** | Satellite error helpers (already excluded in `.rsp`) emit redirects to Core's **public** L2 by prefix-strip; `Mix_OutOfMemory` deferred. |
| D10 | **Cross-family calls = Core public L2; IVT for tests only** | Satellites never reach Core's internal raw ABI. Core-owned public types shared via nested-namespace + ProjectReference. |

---

## 1. Scope & slice boundary

**This slice (Core, Layer 2):**

- Public typed methods on `SDL2.SDL` over the internal `SDLNative` raw ABI.
- Reuse the Pattern B handles already emitted by Layer 1's `OpaqueHandleEmitRewriter` (no new handle work).
- Typed enums at return/param positions, including bitmask `Uint32` flag positions.
- Public structs + typed read-only property accessors over enum-typed raw fields.
- Type-A pure-evaluable function-like macros (helper-candidate lane) generated as public `static` methods.
- Endian runtime-aware `static readonly` aliases for the standalone endian-conditioned constants.
- Core's own `SDL_GetError` / `SDL_SetError` / `SDL_ClearError` are ordinary L2 methods (real functions, via `PublicTypedMethodProjection`) — no redirect machinery here; the satellite `ErrorRedirect` pass arrives with the first satellite slice.
- **Pointer-pure:** `byte*` strings, `T*` buffers, `int*` outputs stay as-is. No string/span/out/ref/`T?` overloads in this slice.
- Public API snapshot (`PublicApiGenerator`/`Verify`) introduced and reviewed.

**Deferred to later slices:** Layer 3 friendly overloads (string/span/out/ref/`T?`, Type-B macros), satellites (Image/Mixer/Ttf/Gfx) L2+L3, SysWM typed layout, Production Flip.

**Sequencing:** Core first is forced — satellite error redirects target Core's public L2, so Core's public surface must exist before any satellite L2.

---

## 2. Architecture — the rewriter pipeline

All transforms are **deterministic postprocess rewriters over committed `.g.cs`** — never Roslyn source generators at consumer build (Constitution §"Generator Home"). L2 passes produce the public typed surface that forwards to internal `SDLNative`; L3 passes are **delegating wrappers** that call L2, emitted into a separate file/region to avoid pointer-vs-span signature collisions (the SilkTouch two-tier insight from peer mapping §1).

**Layer 2 passes:**

| Pass | Responsibility |
| --- | --- |
| `PublicTypedMethodProjection` | Public `SDL.X(...)` forwarder per internal raw method — **net-new, the core pass.** |
| *(HandleParameterTyping)* | Already done by L1's `OpaqueHandleEmitRewriter` (Pattern B); L2 reuses unchanged. |
| `TypedEnumProjection` | Typed enum at return/param + bitmask `Uint32` flag positions. |
| `StructFieldAccessorProjection` | Typed read-only property over each enum-typed raw struct field. |
| `MacroHelperGeneration` (Type A) | Pure-evaluable function-like macros → public `static` methods (see §7). |
| `EndianRuntimeAlias` | `static readonly` runtime-aware aliases for endian-conditioned standalone constants (see §8). |
| `ErrorRedirect` | Satellite error helper → Core public L2 forwarder (see §9). **Satellite slices only.** |

> All L2 passes run in the **Core slice** except `ErrorRedirect`, which is satellite-only — Core's own `SDL_*Error` are ordinary L2 methods via `PublicTypedMethodProjection`.

**Layer 3 passes (designed; implemented in the L3 slice):**

| Pass | Responsibility |
| --- | --- |
| `StringOverload` | `string` + `ReadOnlySpan<byte>` over L2 `byte*`; `byte*`-return → `string?`. |
| `RefOutOverload` | `out T` / `ref T` over single-element scalar pointers; `in T` / `T? = null` over struct pointers. **Skip handle params and `void*`** (Silk's opaque-skip rule). |
| `SpanOverload` | `Span<T>` / `ReadOnlySpan<T>` over `(ptr, count)` buffer pairs via `GetPinnableReference()` delegation. |
| `MacroHelperGeneration` (Type B) | Function-like macros that delegate to raw (version struct-fill, RW-file-loader). |
| `DeduplicateOverloads` | Mandatory collision/dup removal across L3 passes. |

> `BoolPredicateOverload` was **removed** by D6 (SDL_bool stays the typed enum; no bool conversion).

**Config vs code:** *which* symbols/macros/flags/handles a pass touches is per-family **config** (a fact that varies by family); *how* each pass emits is code-owned **policy** (Constitution §"Manifest Configuration Vs Code-Owned Policy").

---

## 3. Layer 2 / Layer 3 seam — Litmus A

> **Does the public method allocate, or change arity / call-shape vs the raw C signature?** No → **Layer 2**. Yes → **Layer 3**.

| Form | Layer | Rationale |
| --- | --- | --- |
| typed handle / enum / struct, pointers preserved, same arity | **L2** | typed but 1:1, no alloc |
| `SDL_bool` return/param (typed enum, D6) | **L2** | type substitution, no conversion |
| Type-A pure-eval macro | **L2** | pure C# math, no alloc |
| endian `static readonly` alias | **L2** | typed constant projection |
| `string` / `ReadOnlySpan<byte>` | **L3** | encode allocates / shape change |
| `out T` / `ref T` / `in T` / `T? = null` | **L3** | call-shape change |
| `Span<T>` / `ReadOnlySpan<T>` | **L3** | shape change |
| Type-B delegating macro | **L3** | convenience wrapper over raw |

`SDL_bool` edge (from the brainstorm): C# forbids return-type-only overloading, so the choice is exclusive per function; D6 chooses the typed `SDL_bool` enum everywhere (no `bool`). Consumers compare `== SDL_bool.SDL_TRUE`.

---

## 4. Breadth & the Safe-Alternative Guarantee

**Guarantee (D3):** no public function forces the caller into `unsafe`. The L2 pointer form remains for zero-overhead power users (who are already in `unsafe`), but L3 provides a safe alternative for every pointer position.

| Raw position | Safe public form |
| --- | --- |
| output scalar `int*` | `out int` |
| in/out scalar `T*` | `ref T` |
| required struct `const T*` | `in T` |
| **optional/nullable struct `T*`** | **`T? = null`** |
| input buffer `(const T*, int count)` | `ReadOnlySpan<T>` |
| output buffer `(T*, int count)` | `Span<T>` |
| string `const char*` | `string` + `ReadOnlySpan<byte>` |
| handle `X*` | Pattern B by-value (already safe, L2) |
| `void*` / foreign | `nint` (opaque, no `unsafe`) |

**Optional-pointer collapse (D4).** `SDL_Rect? = null` nullable-with-default collapses all null-combinations into one overload — no nullability metadata needed (defaulting to nullable is always safe-to-call) and no cartesian explosion:

```csharp
// L2 (power user): pointer form
SDL_RenderCopy(SDL_Renderer r, SDL_Texture t, SDL_Rect* src, SDL_Rect* dst)
// L3 (safe, default): ONE overload covers all 4 null-combinations
SDL_RenderCopy(SDL_Renderer r, SDL_Texture t, SDL_Rect? src = null, SDL_Rect? dst = null)
```

The generated L3 method is `unsafe` **internally** (pins / takes addresses of stack locals); its public signature is safe. `SDL_Rect?` is `Nullable<SDL_Rect>` (value type, stack) — **zero heap alloc**; the P/Invoke boundary still sees only blittable `SDL_Rect*`, compatible with `[assembly: DisableRuntimeMarshalling]`.

**Anti-explosion rule.** One fully-friendly overload per function (all eligible params converted together). The only multiplicity is the string axis (`string` + `ReadOnlySpan<byte>` = ×2). So a function gets ~1–2 L3 overloads, never 2ⁿ. (Peer mapping §5 documents the rejected Silk cartesian and SDL2-CS `IntPtr`-slot enumeration.)

**Public-release gate, not per-slice.** Because Core ships L2 first, pointer functions have no safe form until the L3 slice. So the guarantee is an **exit gate for the first public preview**, not for the L2-only milestone. It shapes exit evidence (§10).

**Allocation posture under the new breadth:** every hot path has a zero-heap form (L2 pointer; `out`/`ref`/`in`/`T?` stack-only; `Span`/`ReadOnlySpan<T>`/`ReadOnlySpan<byte>` caller-owned). Only opt-in string conveniences allocate (`string` param: stackalloc for small / pooled for large; `string?` return: managed string). Aligns with Constitution "low-allocation" + Roadmap "stack for small UTF-8, pooled for large".

**Peer validation (D4):** Silk uses nullable value-type params (`Point?`, `Rectangle<int>?`) — validates the technique; SDL2-CS reaches the same no-`unsafe` goal via `ref` + `IntPtr`-sentinel enumeration (4 overloads for `SDL_RenderCopy`), which our single `T?` overload supersedes.

---

## 5. String axis

- Per `const char*` param: separate **`string`** (encode to null-terminated UTF-8) and **`ReadOnlySpan<byte>`** (pre-encoded, zero-copy) overloads. No unified `Utf8String` ref-struct (ppy's implicit-conversion type, rejected per our explicit/no-magic posture). No `ReadOnlySpan<char>` (UTF-16 transcode, low value for UTF-8 SDL).
- `const char*` return → `string?` (managed string).
- **No `Unsafe_` rename** (ppy's public-raw tax): our raw is `internal SDLNative`, so the friendly-named L3 method claims the real name with zero collision (peer mapping headline #2).
- **`ReadOnlySpan<byte>` contract:** the overload always presents a null-terminated `const char*` to SDL — **zero-copy when the span is already null-terminated** (`span.Length > 0 && span[^1] == 0`), otherwise a small stack (or pooled, for large) copy that appends the terminator. It never reads past the caller's span. (Exact buffer-threshold mechanism confirmed in `writing-plans`.)

---

## 6. SDL_bool (D6)

- L2 **and** L3 return/accept the typed `SDL_bool` enum (int-backed per Constitution §"SDL_bool").
- No `bool` conversion, no implicit `operator bool`, no predicate-promotion rule. The internal raw stays `SDL_bool` (ABI-honest); the public surface presents the same typed enum.
- Consumer idiom: `if (SDL.SDL_HasIntersection(a, b) == SDL_bool.SDL_TRUE)`.
- Removes the `BoolPredicateOverload` pass entirely.
- Raw `int` returns are **not** guessed into bool (avoids the roadmap's "predicate-like" ambiguity — the `SDL_bool` *type* is the only signal).

---

## 7. Function-like macros (D7 — strategy #3)

**Constraint:** ClangSharp's `generate-macro-bindings` emits **object-like** (value) macros only. **Function-like** macros (`SDL_BUTTON(X)`, `SDL_VERSIONNUM(X,Y,Z)`, `SDL_ISPIXELFORMAT_*(f)`) are reported as helper-candidates and **not emitted** — no `.g.cs` body, no `[NativeTypeName("#define …")]` provenance. So "parser-mark then emit" is unavailable for them; a custom extractor or curated roster is required.

**Mechanism (#3 — roster + per-kind emitters):**

- **Config roster** (WHICH): per-family list naming each generated function-like macro and its *kind*.
- **Code-owned per-kind emitters** (HOW): one emitter per macro *kind*, invariant across families.
- **Kinds:** version-arithmetic (`SDL_VERSIONNUM`, `*_VERSION_ATLEAST`, `*_COMPILEDVERSION`), button-shift (`SDL_BUTTON`), pixel-format-bit-extract (`SDL_PIXELTYPE/ORDER/LAYOUT`, `SDL_BITSPERPIXEL`, `SDL_BYTESPERPIXEL`, `SDL_ISPIXELFORMAT_*`, `SDL_DEFINE_PIXELFORMAT`), fourcc (`SDL_FOURCC`), windowpos (`SDL_WINDOWPOS_*`), **RW-file-loader** (`SDL_LoadBMP`/`SDL_SaveBMP`/`SDL_LoadWAV` → `X(string path) => X_RW(SDL_RWFromFile(path, mode), 1)`; roster carries `mode`).
- **Layer:** Type-A (pure-eval) → **L2**; Type-B (delegate-to-raw, incl. RW-file-loader, struct-fill `SDL_VERSION(out v)`) → **L3**; Type-C (token-paste, ASM, compile-time, `__arglist`) → hand-author last-resort or skip, with rationale recorded.
- **Peer bodies are expression oracles** for the emitters — but **not** ppy's SDL3 `SDL_VERSIONNUM` (`X*1000000+…`); SDL2 is `X*1000 + Y*100 + Z` (Constitution §"Constants And Macros").
- **Core L2 scope:** the Constitution helper-candidate lane (`SDL_VERSIONNUM`, `FOURCC`, `BUTTON`, `PIXELTYPE/ORDER/LAYOUT`, `BITSPERPIXEL`, `BYTESPERPIXEL`, `ISPIXELFORMAT_*`, `WINDOWPOS_*`). Type-A subset here; Type-B (RW-file-loader etc.) in the L3 slice.
- **Satellite version macros** generated per-family (`SDL_IMAGE_VERSION_ATLEAST`, etc.).
- `[Macro]`-style inert markers are **not** adopted (peer mapping: a marker that drives nothing is worse than a roster that drives emission).

---

## 8. Endian / platform-computed macros (D8)

**Extent (grounded in generated Core/Mixer output):** ~14 in Core — `AUDIO_{U16,S16,S32,F32}SYS` (4), `SDL_BYTEORDER` + `SDL_FLOATWORDORDER` (2), `SDL_PIXELFORMAT_*32` (8) — plus `MIX_DEFAULT_FORMAT` (1, Mixer, aliases the Core `AUDIO_S16SYS`). Overwhelmingly Core; reinforces Core-first. The `*SYS` suffix (system/native endian) is the signal; non-`SYS` aliases (`AUDIO_S16 = AUDIO_S16LSB`) are fixed, not endian-dependent.

**Constraint:** the `#if SDL_BYTEORDER` is preprocessed away before ClangSharp emits, so the conditionality cannot be recovered from emitted output alone.

**Mechanism (auto-seeded roster):**

- **Differential LE/BE parse = audit-time seeder** (not per generation): parse headers with `SDL_BYTEORDER` forced LE then BE; constants whose value differs are the endian-dependent set → auto-suggests the roster. (Platform-identity macros don't change under byteorder-flip, so the set stays clean.)
- **Config roster** (the 3rd roster kind, alongside `opaque_handles` and `flags_enums`): the audited endian-alias list + each entry's LE/BE member pair.
- **Code-owned emitter:** `public static readonly ushort MIX_DEFAULT_FORMAT = BitConverter.IsLittleEndian ? AUDIO_S16LSB : AUDIO_S16MSB;`
- **`PIXELFORMAT_*32` are enum members** → cannot be runtime-conditional in place → **frozen LE** (correct for all 7 LE RIDs; documented). Runtime-alias applies only to the ~6–7 standalone constants (`AUDIO_*SYS`, `MIX_DEFAULT_FORMAT`, `SDL_BYTEORDER`, `SDL_FLOATWORDORDER`).
- L1 raw evaluated constants stay **auditable** (do not `.rsp`-exclude them — avoids the `MIX_MAJOR_VERSION` mistake).
- Platform-**identity** macros (`__WINDOWS__` etc.) are **not** projected as runtime truth.
- Peer-validated: ppy + SDL2-CS both use `BitConverter.IsLittleEndian` (peer mapping §3 dim 10).

---

## 9. Error redirect (D9)

- Satellite error helpers (`IMG_GetError`/`IMG_SetError`, `TTF_*`, `Mix_GetError/SetError/ClearError`) are `#define` macros aliasing Core's `SDL_*Error` — **already excluded from L1** in the family `.rsp` files.
- **Convention-derived emit:** for each excluded `{PREFIX}_{Get|Set|Clear}Error`, emit `{PREFIX}_{X}Error() => SDL.SDL_{X}Error()` by prefix-strip — forwarding to Core's **public** L2 (ProjectReference + nested-namespace resolution; no IVT). No manual mapping roster (Constitution: derive from convention, promote to explicit only on proven insufficiency).
- `Mix_OutOfMemory` (→ `SDL_OutOfMemory`, itself a macro) is **deferred** (SDL2-CS also omits it).
- `SDL_GetError` is string-returning → Core L2 `byte*` + L3 `string?`; satellite redirect mirrors both layers.
- `SDLNet_*Error` are real exported functions (do **not** exclude) — relevant only once SDL2_net enters scope.

---

## 10. Exit evidence

**Core L2 slice exit (this slice):**

- Multi-TFM compile clean: `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, `net462`.
- Emitter tests prove every public L2 method forwards to the internal raw method (1:1).
- No public raw ABI container or effectively-public raw extern leak (lexically-public members inside `internal SDLNative` are acceptable).
- Public API snapshot (`PublicApiGenerator`/`Verify`) exists and is reviewed.
- Determinism: complete-family idempotent regeneration (twice-regenerated diff empty); family isolation preserved (Constitution §"Generation Determinism Contract").
- Macro/endian/error passes report what they emitted/skipped (reviewable surface).
- **Does NOT need** to prove the Safe-Alternative Guarantee (that is an L3 + public-release gate).

**Public preview / release exit (later slices):**

- Safe-Alternative Guarantee holds: no public function forces `unsafe`.
- Layer 3 overloads present with RED/GREEN tests per pattern.
- Package-consumer smoke exercises representative typed + friendly calls against packaged natives.

---

## 10B. Library-agnostic invariants (cross-family)

The L2 rewriters are **family-blind**: every SDL2 family's case is handled from the start, driven by config, so satellites activate without rewriter changes. Derived from the cross-family requirements matrix ([`../../../spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-cross-family-requirements.md`](../../../spikes/binding-generators/docs/canonical/references/2026-05-30-layer-2-cross-family-requirements.md)):

1. **No-op tolerance** — every pass accepts an empty per-family work-set without erroring or injecting dead machinery (clong/endian/error/callbacks/enums are empty for ≥1 family; Image/TTF/GFX are the empty-input guards).
2. **Universal `[StructLayout(LayoutKind.Sequential)]` pass** across all families — ClangSharp omits it (GFX `FPSmanager` evidence; equally covers Core `SDL_Color`/`SDL_Rect`/`SDL_AudioSpec`).
3. **clong covers BOTH paths from day one** — return-normalize (Core `SDL_ThreadID`/`SDL_GetThreadID`) AND input-param Windows range-check (TTF `TTF_OpenFontIndex*`, `long index`). Core is returns-only, so a Core-first design forgets the input path.
4. **Cross-family type resolution** — by-value handle (`SDL_RWops`/`SDL_Renderer`/`SDL_Texture`) vs by-pointer struct (`SDL_Surface`/`SDL_version`) per Core type; explicit cross-namespace resolution (generated satellites carry no `using SDL2;`); satellites consume-don't-re-own.
5. **Cross-family alias resolution** — Mixer `MIX_DEFAULT_FORMAT` → Core audio aliases (`int`→`ushort`), build-order-dependent on Core.
6. **Hard sequencing** — Core L2 lands before any satellite L2 (error redirects, endian aliases, handle consumption all resolve into Core's public L2).
7. **Zero name normalization** — GFX mixes 35 `SDL_`-prefixed + 67 bare-name functions; names mirror generated 1:1, never derived/stripped from the class name.
8. **Per-family variable sets** — error-macro set size (0/2/4), flags allow-list (often empty), version tokens, `owner_mode` are all per-family config facts.

**Enforcement:** foundation self-tests carry satellite-case fixtures (clong-INPUT, `T**` field, cross-family by-value/by-pointer, empty-config no-op) **before** any satellite is wired, so each case is implemented and regression-guarded — not forgotten. This section is **Constitution amendment-queue item 8** (family-blind mechanism), applied with the Layer 2 changeset.

---

## 11. Non-goals

- No `SafeHandle` / `IDisposable` owner wrappers; no callback lifetime layer (Roadmap L2/L3 non-goals).
- No SDL2-CS compatibility freeze (`IntPtr`-everywhere is not the target).
- No unified `Utf8String` ref-struct (explicit `string` + `ReadOnlySpan<byte>` instead).
- No implicit conversions (handles or `SDL_bool`).
- No cartesian overload explosion; no `IntPtr`-sentinel optional-output enumeration.
- No `[InternalsVisibleTo]` to satellites (tests only).
- No foreign-type over-mapping (`SDL_Rect` stays `SDL_Rect`, not `System.Drawing`/`Silk.NET.Maths`).
- No reflection marshalling (`PtrToStructure`/`[MarshalAs(LPArray)]`) — incompatible with `[assembly: DisableRuntimeMarshalling]`.
- No `__arglist` variadics (Constitution §"C Variadics").

---

## 12. Constitution amendments (apply with the Layer 2 changeset)

Per the Constitution Maintenance Rule ("update the constitution in the same change"), these land coherently when the Layer 2 work lands — not piecemeal. Drafted here as the amendment queue:

1. **Layer 2/3 litmus** — add Litmus A to §"Layer Contract" as the operational seam test.
2. **Safe-Alternative Guarantee** — new contract: the public-release surface never forces `unsafe`; the raw pointer form is never the only public path. (Public-release gate.)
3. **Breadth + `T? = null` collapse** — comprehensive safe L3; optional struct pointers as a single nullable-with-default overload; one fully-friendly overload per function; no cartesian.
4. **String axis** — separate `string` + `ReadOnlySpan<byte>`; no unified `Utf8String`; no `ReadOnlySpan<char>`.
5. **Function-like macro strategy** — record that ClangSharp does not surface function-like macros; adopt the #3 roster (3rd config roster kind) + per-kind emitters; Type A→L2, Type B→L3, Type C→last-resort.
6. **Cross-family error routing** — satellites forward to Core public L2; IVT for tests only; make the convention-derived redirect explicit.
7. **Endian L2 runtime-alias** — auto-seeded roster + `BitConverter.IsLittleEndian` emit; `PIXELFORMAT_*32` enum members frozen LE; L1 raw constants stay auditable.

Also clarify §"SDL_bool" / §"Enums": SDL2 `SDL_bool` is presented as the typed enum at all public positions (no `bool` conversion in the generated surface) — supersedes the roadmap's "predicate-like" phrasing.

---

## 13. Multi-agent review checkpoints

Mirror the consolidation's checkpoint discipline at smaller scale:

- **Post-spec (now):** Deniz review of this document.
- **Post-plan:** review the `writing-plans` output before execution.
- **Post-implementation (Core L2):** adversarial multi-agent review — verify (a) every L2 method forwards 1:1 to internal raw, (b) no public raw leak, (c) determinism/idempotency, (d) the API snapshot is complete and honest, (e) macro/endian/error passes match this spec.

---

## 14. References

- [`2026-05-29-layer-2-peer-mapping.md`](../../../spikes/binding-generators/docs/canonical/references/2026-05-29-layer-2-peer-mapping.md) — Phase A peer mapping (evidence base).
- [`binding-generator-constitution.md`](../../../spikes/binding-generators/docs/canonical/binding-generator-constitution.md) — §"Layer Contract", §"Opaque Handles", §"Enums", §"SDL_bool", §"Constants And Macros", §"Foreign Type Boundary Policy", §"C Variadics", §"Generation Determinism Contract", §"Manifest Configuration Vs Code-Owned Policy", §"Evidence Gates", §"Generator Home".
- [`binding-generator-roadmap.md`](../../../spikes/binding-generators/docs/canonical/binding-generator-roadmap.md) — §"Layer 2 — Typed Public API Projection", §"Layer 3 — Friendly Overload Projection".
- [`binding-generator-implementation-notes.md`](../../../spikes/binding-generators/docs/canonical/binding-generator-implementation-notes.md) — postprocess pipeline mechanism, classification labels.
- [`next-iteration-plan.md`](../../../spikes/binding-generators/docs/next-iteration-plan.md) — Forward Backlog (Production Flip gates, L2/L3 follow-ups).
- ADRs: [`2026-05-05-target-centric-build-host.md`](../../../docs/decisions/2026-05-05-target-centric-build-host.md) (ADR-002), [`2026-05-12-build-host-data-layer.md`](../../../docs/decisions/2026-05-12-build-host-data-layer.md) (ADR-003), [`2026-05-14-binding-autogen-toolchain.md`](../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) (ADR-004, Reopened — reinforced by §7 ClangSharp evidence).
- Knowledge base: [`extraction-guidelines.md`](../../../docs/knowledge-base/extraction-guidelines.md), [`testing-guidelines.md`](../../../docs/knowledge-base/testing-guidelines.md).
