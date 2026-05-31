# Layer 2 — Design Decisions & Deferred Scope (Beyond the Core Foundation Plan)

**Date:** 2026-05-30
**Status:** Consolidation / resume anchor. Captures **everything from the Layer 2 design cycle that is NOT in the Core L2 foundation implementation plan** — design decisions, deferred slices, Layer 3 scope, deferred review findings, and the pending Constitution amendments. One durable canonical place so nothing is lost at session close. Summaries only — each item names its **authoritative source**; this doc does not restate full content (no-duplication discipline).
**What IS in the foundation plan (not here):** the Core L2 implementation tasks + all integrated must-fixes — [`../../../../docs/superpowers/plans/2026-05-30-binding-autogen-layer-2-core-foundation.md`](../../../../docs/superpowers/plans/2026-05-30-binding-autogen-layer-2-core-foundation.md).

---

## 1. Ratified design decisions (authoritative: design spec)

Full text + rationale: [`../../../../docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md`](../../../../docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md).

| # | Decision | Summary |
| --- | --- | --- |
| D1 | Scope | Design full L2+L3; implement Core L2 first. |
| D2 | **Litmus A** (L2/L3 seam) | Allocates or changes arity/call-shape vs raw C sig → Layer 3; else Layer 2. |
| D3 | **Safe-Alternative Guarantee** | Public-release surface never forces `unsafe`; raw pointer form may exist but is never the only path. Public-release gate (not per-slice). |
| D4 | Breadth + `T?=null` collapse | Comprehensive safe L3; optional struct pointers → single nullable-with-default overload; one fully-friendly overload per fn; no cartesian. |
| D5 | String axis | Separate `string` + `ReadOnlySpan<byte>`; no unified `Utf8String`; no `ReadOnlySpan<char>`. |
| D6 | `SDL_bool` | Typed int-backed enum at all public positions; no implicit bool, no return-type-overload, no predicate-rule. (L3 affordance — see §4.) |
| D7 | Function-like macros = **#3** | Curated config roster + per-kind code-owned emitters (version, button, pixel-extract, fourcc, windowpos, **RW-file-loader**). Type-A→L2, Type-B→L3, Type-C→hand-author last-resort. ClangSharp does not surface function-like macros. |
| D8 | Endian macros | Auto-seeded roster (differential LE/BE parse seeds; human-audited) + code emits `BitConverter.IsLittleEndian` `static readonly` aliases. `PIXELFORMAT_*32` enum members frozen LE. |
| D9 | Error redirect | Satellites forward to Core's **public** L2 (`IMG_GetError() => SDL.SDL_GetError()`); convention-derived prefix-strip; `[InternalsVisibleTo]` for tests only; `Mix_OutOfMemory` deferred. |
| D10 | Cross-family calls | Core-owned public **types** shared via nested-namespace + ProjectReference (no IVT); Core L2 lands before any satellite L2. |
| — | **Library-agnostic invariants** | Spec §10B: family-blind rewriters, no-op tolerance, universal `[StructLayout(Sequential)]`, clong both paths, cross-family type/alias resolution, sequencing, zero name-normalization. |

## 2. Pending Constitution amendments (authoritative: spec §12)

Eight amendments land with the Layer 2 changeset (Constitution Maintenance Rule): (1) Litmus A in §"Layer Contract"; (2) Safe-Alternative Guarantee; (3) breadth + `T?`-collapse; (4) string axis; (5) function-like-macro #3 + "ClangSharp doesn't surface function-like macros"; (6) cross-family error routing; (7) endian L2 runtime-alias; (8) family-blind mechanism / no-op tolerance / universal StructLayout. Plus the §"SDL_bool" clarification (typed enum at all public positions). **ADR-004 amendment** (toolchain) remains deferred to Layer 2 closure per the roadmap; this cycle's evidence reinforces the ClangSharp choice (committed-postprocess, raw-only PInvokeGenerator).

## 3. Deferred implementation slices + sequence (authoritative: cross-family matrix §4 + roadmap §Layer 2)

Cross-family matrix: [`2026-05-30-layer-2-cross-family-requirements.md`](2026-05-30-layer-2-cross-family-requirements.md). After the Core foundation:

1. **TypedEnumProjection** — typed enum at raw `int`/`uint` flag positions (the Silk gap; `IMG_Init(int flags)`→`IMG_InitFlags`); do **not** synthesize `[Flags]` from bare const-int bitmasks (TTF `TTF_STYLE_*`).
2. **StructFieldAccessorProjection** — typed read-only properties over enum-typed raw fields; handle Image's `T**` (`SDL_Surface** frames`) by leaving it a pointer at L2.
3. **MacroHelperGeneration (#3)** — the macro extractor + roster + per-kind emitters (Type-A→L2, Type-B incl. RW-file-loader→L3); per-family version macros incl. double-aliases (`TTF_VERSION`/`SDL_TTF_VERSION`, `MIX_VERSION`); SDL2 `X*1000+Y*100+Z` spacing.
4. **EndianRuntimeAlias** — `MIX_DEFAULT_FORMAT` (cross-family alias to Core audio), `AUDIO_*SYS`, `SDL_BYTEORDER` runtime aliases (after Core's audio aliases exist).
5. **ErrorRedirect** — satellite error helpers → Core public L2 (Image 2, TTF 2, Mixer 4 incl. `Mix_OutOfMemory`-deferred, GFX 0).
6. **Satellite activation** (config + thin per-family slice, no rewriter change): order **Image → GFX → TTF → Mixer** (simplest first; Mixer last for callbacks). Per-family: add `public_class`, version tokens, error-macro set; run pipeline; snapshot. Cross-namespace Core-type resolution (by-value handle vs by-pointer struct; no `using SDL2;`).
7. **Mixer callback lifetime/rooting** — the 4-element deferred policy (next-iter backlog): Compat delegate rooting, Modern `[UnmanagedCallersOnly]` authoring, dummy-audio smoke, cleanup/unregistration; two lifetime classes (5 persistent audio-thread roots + 1 synchronous `Mix_EachSoundFont`).

## 4. Layer 3 (friendly overload) scope (authoritative: spec §3–§5 + next-iteration-plan)

Deferred to the L3 milestone: `string` + `ReadOnlySpan<byte>` overloads (no `Unsafe_` rename needed — internal raw); `out T`/`ref T`/`in T`/`T?=null`; `Span<T>`/`ReadOnlySpan<T>` (skip handle params + `void*`, Silk rule); mandatory de-dup pass; typed-callback ergonomics (delegate/`[UnmanagedCallersOnly]` over the `nint` L2 callback params); Type-B macro helpers. **`SDL_bool` L3 affordance** (next-iter Forward Backlog): non-overloading `bool ToBoolean(this SDL_bool)` / `IsTrue` — every peer gives a bool path; D6 currently has none. `ReadOnlySpan<byte>` null-termination contract: null-terminated → zero-copy, else small stack/pooled copy.

## 5. Deferred review findings (authoritative: plan "Deferred review findings")

From the dual expert review (2026-05-30, unbiased + context-full): (a) cross-namespace satellite fixture (add at satellite activation — Core can't exercise it as the type owner); (b) spec §1 macro/endian-deferral reconciliation (the foundation re-emits object-like consts but defers function-like macros + endian aliases); (c) nit: `raw_class` comment. The dual lens caught two legacy-TFM compile blockers (`delegate*`-param divergence; `[SupportedOSPlatform]` unconditional emit) — **both integrated as must-fixes in the plan**, not deferred. The source-generator suggestion (unbiased lens) is answered by Constitution §"Generator Home" (committed output + reproducibility stamp — context-full lens confirmed).

## 6. Cross-references

- Design spec (full design): [`../../../../docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md`](../../../../docs/superpowers/specs/2026-05-29-binding-autogen-layer-2-typed-public-api-design.md)
- Core foundation plan (impl): [`../../../../docs/superpowers/plans/2026-05-30-binding-autogen-layer-2-core-foundation.md`](../../../../docs/superpowers/plans/2026-05-30-binding-autogen-layer-2-core-foundation.md)
- Cross-family requirements matrix: [`2026-05-30-layer-2-cross-family-requirements.md`](2026-05-30-layer-2-cross-family-requirements.md)
- Phase A peer mapping: [`2026-05-29-layer-2-peer-mapping.md`](2026-05-29-layer-2-peer-mapping.md)
- Backlog tracking: [`../../next-iteration-plan.md`](../../next-iteration-plan.md) (Active Iteration + Forward Backlog)
- Policy: [`../binding-generator-constitution.md`](../binding-generator-constitution.md), [`../binding-generator-roadmap.md`](../binding-generator-roadmap.md) §"Layer 2"
