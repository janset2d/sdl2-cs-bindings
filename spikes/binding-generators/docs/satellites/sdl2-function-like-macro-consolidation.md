# SDL2 Function-Like Macro Consolidation

**Date:** 2026-05-26
**Status:** Research artifact. Feeds companion-helper policy design when Layer 2/3 work begins (Roadmap M5/M6).
**Scope:** All SDL2 families (Core, Image, TTF, Mixer, GFX, Net) — catalog of all function-like C preprocessor macros, peer comparison, and path to companion-helper policy.

---

## 1. Core Finding

SDL2 headers contain function-like macros across all 6 families. The exact count varies by what qualifies as "function-like" (see exclusion categories below), but the catalog here is exhaustive for all macros with parameters or function-call expansions. Roughly **half are pure-evaluable** (math/bit-logic expressions auto-generable as C# `static` methods). The remaining are function-call wrappers, struct fillers, or platform-specific constructs not representable in C#.

The Constitution ([`binding-generator-constitution.md`](../../../../docs/binding-autogen/binding-generator-constitution.md) L547-548) prescribes:

> *"Function-like public SDL macros are Layer 2 / friendly companion-helper candidates. They are not Layer 1 raw ABI declarations and must not be emitted as constants."*
>
> *"Approved function-like macro helpers are manually authored or generated from an explicit companion-helper policy. ClangSharp does not emit them."*

The companion-helper policy does **not yet exist**. This document provides the inventory and evidence needed to design it.

---

## 2. Macro Taxonomy (by Evaluability)

### 2.1 Type A — Pure Evaluable (auto-generable as C# `static` methods)

Pure math/bit-logic expressions with no side effects or native function calls.

| Category | Count | Examples |
|---|---|---|
| Version arithmetic | 8 | `SDL_VERSIONNUM`, `SDL_VERSION_ATLEAST`, `*_VERSION_ATLEAST` (Image/TTF/Mixer/Net) |
| Bit manipulation | 8 | `SDL_FOURCC`, `SDL_DEFINE_PIXELFOURCC`, `SDL_DEFINE_PIXELFORMAT` |
| Enum/field extraction | 12 | `SDL_PIXELTYPE`, `SDL_PIXELLAYOUT`, `SDL_BITSPERPIXEL`, `SDL_BYTESPERPIXEL`, `*_ISPIXELFORMAT_*` |
| Audio format | 9 | `SDL_AUDIO_BITSIZE`, `SDL_AUDIO_ISFLOAT/BIGENDIAN/SIGNED/INT/LITTLEENDIAN/UNSIGNED` |
| Colorspace | 12 | `SDL_DEFINE_COLORSPACE`, `SDL_COLORSPACETYPE/RANGE/CHROMA/PRIMARIES/TRANSFER/MATRIX`, `SDL_ISCOLORSPACE_*` |
| Window position | 4 | `SDL_WINDOWPOS_UNDEFINED_DISPLAY/CENTERED_DISPLAY/ISUNDEFINED/ISCENTERED` |
| Byte swap (if-endian) | 8 | `SDL_SwapLE16/32/64`, `SDL_SwapBE16/32/64`, `SDL_SwapFloatLE/BE` |
| Math/utility | 3 | `SDL_min`, `SDL_max`, `SDL_clamp` (note: double-evaluate in C; C# methods are single-eval) |
| Input/event | 5 | `SDL_BUTTON`, `SDL_SCANCODE_TO_KEYCODE`, `SDL_TICKS_PASSED`, `SDL_HAT_*` (4 combo macros) |
| Misc Boolean | 1 | `SDL_SHAPEMODEALPHA` |
| Time conversion | 0 (SDL2 only in SDL3) | N/A — SDL2 does not have `SDL_SECONDS_TO_NS` etc. |

### 2.2 Type B — Non-Evaluable But Generable (convenience wrappers / struct setters)

Call SDL native functions or fill struct fields. Can be auto-generated as C# methods that delegate to raw ABI.

| Category | Count | Examples |
|---|---|---|
| Struct-fill version helpers | 6 | `SDL_VERSION(x)`, `SDL_IMAGE_VERSION(X)`, `SDL_TTF_VERSION(X)`, `SDL_MIXER_VERSION(X)`, `TTF_VERSION(X)`, `MIX_VERSION(X)`, `SDL_NET_VERSION(X)` |
| Error helpers | 3 | `SDL_OutOfMemory()`, `SDL_Unsupported()`, `SDL_InvalidParamError(param)` |
| File loader wrappers | 4 | `SDL_LoadBMP`, `SDL_SaveBMP`, `SDL_LoadWAV`, `SDL_GameControllerAddMappingsFromFile` |
| Convenience redirects | 4 | `SDL_GetEventState`, `TTF_RenderText/UTF8/UNICODE` (compat aliases), `SDL_iOSSetAnimationCallback`, `SDL_iOSSetEventPump` |
| Iconv helpers | 4 | `SDL_iconv_utf8_locale/ucs2/ucs4`, `SDL_iconv_wchar_utf8` |
| Atomic refcount | 2 | `SDL_AtomicIncRef(a)`, `SDL_AtomicDecRef(a)` |
| SDL_net I/O | 4 | `SDLNet_Write16/32`, `SDLNet_Read16/32` |
| Native-ptr predicates | 2 | `SDL_MUSTLOCK` (dereferences struct flags field), `SDLNet_SocketReady` (calls inline bool check) |
| Mutex compat | 2 | `SDL_mutexP`, `SDL_mutexV` |

### 2.3 Type C — Manual-Only or Skip

Token-pasting, platform ASM, compile-time constructs, or pointer-output semantics.

| Category | Examples | Reason |
|---|---|---|
| Token-pasting | `SDL_NAME(X)`, `SDL_copyp` | Preprocessor concatenation not representable in C# |
| Compile-time only | `SDL_COMPILE_TIME_ASSERT`, `SDL_STRINGIFY_ARG`, `SDL_arraysize` | Compile-time assertion / stringification / sizeof division |
| Platform ASM | All `MemoryBarrier*`, `CPUPauseInstruction*`, `TriggerBreakpoint*` | Platform-specific intrinsics |
| Mem ops with address-of | `SDL_zero`, `SDL_zerop`, `SDL_zeroa` | Takes address of variable |
| Stack alloc | `SDL_stack_alloc`, `SDL_stack_free` | Platform-dependent (alloca vs malloc) |
| Overflow checks | `SDL_size_mul_overflow`, `SDL_size_add_overflow` | Output-pointer semantics |
| Thread creation | `SDL_CreateThread`, `SDL_CreateThreadWithStackSize` | Complex platform-dependent expansion |
| C++ casts | `SDL_reinterpret_cast`, `SDL_static_cast`, `SDL_const_cast` | Not meaningful in C# |

---

## 3. Per-Family Inventory

### 3.1 Core — `SDL_version.h`

| Macro | Type | Expansion |
|---|---|---|
| `SDL_VERSION(x)` | B | Struct fill block (major/minor/patch) |
| `SDL_VERSIONNUM(X, Y, Z)` | A | `X * 1000 + Y * 100 + Z` (SDL2 spacing) |
| `SDL_COMPILEDVERSION` | A | `SDL_VERSIONNUM(SDL_MAJOR_VERSION, SDL_MINOR_VERSION, SDL_PATCHLEVEL)` |
| `SDL_VERSION_ATLEAST(X, Y, Z)` | A | Compound boolean check against version constants |

**SDL2 vs SDL3 spacing note** (Constitution L566-569): SDL2 uses `X * 1000 + Y * 100 + Z` (e.g. `SDL_COMPILEDVERSION` for 2.32.10 = 5210). SDL3 uses `X * 1000000 + Y * 1000 + Z`.

### 3.2 Core — `SDL_stdinc.h`

| Macro | Type | Notes |
|---|---|---|
| `SDL_arraysize(array)` | A | `sizeof(array)/sizeof(array[0])` |
| `SDL_FOURCC(A,B,C,D)` | A | 4-byte construction |
| `SDL_min(x,y)`, `SDL_max(x,y)`, `SDL_clamp(x,a,b)` | A | Double-evaluates args (C caveat, C# method is single-eval) |
| `SDL_zero(x)`, `SDL_zerop(x)`, `SDL_zeroa(x)` | C | `SDL_memset` with address-of |
| `SDL_copyp(dst,src)` | C | `SDL_memcpy` + compile-time assert |
| `SDL_stack_alloc(type,count)`, `SDL_stack_free(data)` | C | Platform-dependent |
| `SDL_size_mul_overflow(a,b,ret)`, `SDL_size_add_overflow(a,b,ret)` | C | Output-pointer semantics |
| `SDL_COMPILE_TIME_ASSERT(name,x)` | C | Compile-time only |
| `SDL_reinterpret_cast`, `SDL_static_cast`, `SDL_const_cast` | C | C++ casts |
| `SDL_STRINGIFY_ARG(arg)` | C | Preprocessor stringification |
| `SDL_iconv_utf8_locale/ucs2/ucs4(S)`, `SDL_iconv_wchar_utf8(S)` | B | Convenience redirects |
| All `SDL_*_CAP`, `SDL_*_FUNC`, thread annotation macros | — | Compiler annotations — skip entirely |

### 3.3 Core — Other Headers

| Header | Macro | Type |
|---|---|---|
| `SDL_pixels.h` | `SDL_DEFINE_PIXELFOURCC`, `SDL_DEFINE_PIXELFORMAT`, `SDL_PIXELFLAG/TYPE/ORDER/LAYOUT/BITSPERPIXEL/BYTESPERPIXEL`, `SDL_ISPIXELFORMAT_INDEXED/PACKED/ARRAY/ALPHA/FOURCC` | A |
| `SDL_audio.h` | `SDL_AUDIO_BITSIZE/ISFLOAT/ISBIGENDIAN/ISSIGNED/ISINT/ISLITTLEENDIAN/ISUNSIGNED`, `SDL_LoadWAV` | A + B |
| `SDL_endian.h` | `SDL_SwapLE16/32/64`, `SDL_SwapBE16/32/64`, `SDL_SwapFloatLE/BE` | A |
| `SDL_video.h` | `SDL_WINDOWPOS_UNDEFINED_DISPLAY/CENTERED_DISPLAY/ISUNDEFINED/ISCENTERED` | A |
| `SDL_surface.h` | `SDL_MUSTLOCK`, `SDL_LoadBMP`, `SDL_SaveBMP` | A + B |
| `SDL_events.h` | `SDL_GetEventState` | B |
| `SDL_mouse.h` | `SDL_BUTTON(X)` | A |
| `SDL_keycode.h` | `SDL_SCANCODE_TO_KEYCODE(X)` | A |
| `SDL_gamecontroller.h` | `SDL_GameControllerAddMappingsFromFile` | B |
| `SDL_timer.h` | `SDL_TICKS_PASSED(A,B)` | A |
| `SDL_shape.h` | `SDL_SHAPEMODEALPHA(mode)` | A |
| `SDL_error.h` | `SDL_OutOfMemory()`, `SDL_Unsupported()`, `SDL_InvalidParamError(param)` | B |
| `SDL_atomic.h` | `SDL_AtomicIncRef(a)`, `SDL_AtomicDecRef(a)` | B |
| `SDL_mutex.h` | `SDL_mutexP(m)`, `SDL_mutexV(m)` | B |
| `SDL_joystick.h` | `SDL_HAT_RIGHTUP` etc. (4 combo OR macros) | A |

### 3.4 SDL_image

| Macro | Type | Notes |
|---|---|---|
| `SDL_IMAGE_VERSION(X)` | B | Struct fill |
| `SDL_IMAGE_COMPILEDVERSION` | A | Computed from version constants |
| `SDL_IMAGE_VERSION_ATLEAST(X,Y,Z)` | A | Standard `_VERSION_ATLEAST` pattern |

**Error macros** (`IMG_SetError`/`IMG_GetError`) are covered in [sdl2-satellite-error-function-consolidation.md](sdl2-satellite-error-function-consolidation.md).

### 3.5 SDL_ttf

| Macro | Type | Notes |
|---|---|---|
| `SDL_TTF_VERSION(X)` | B | Struct fill |
| `TTF_VERSION(X)` | B | Backward-compat alias |
| `SDL_TTF_COMPILEDVERSION` | A | Computed |
| `SDL_TTF_VERSION_ATLEAST(X,Y,Z)` | A | Standard pattern |
| `TTF_RenderText/UTF8/UNICODE(font,text,fg,bg)` | B | Compat aliases → `*_Shaded` variants |

**Error macros** (`TTF_SetError`/`TTF_GetError`) are covered in the error consolidation doc.

### 3.6 SDL_mixer

| Macro | Type | Notes |
|---|---|---|
| `SDL_MIXER_VERSION(X)` | B | Struct fill |
| `MIX_VERSION(X)` | B | Backward-compat alias |
| `SDL_MIXER_COMPILEDVERSION` | A | Computed |
| `SDL_MIXER_VERSION_ATLEAST(X,Y,Z)` | A | Standard pattern |

**Error macros** (`Mix_SetError`/`Mix_GetError`/`Mix_ClearError`/`Mix_OutOfMemory`) are covered in the error consolidation doc.

Object-like endian/platform-computed macros such as `MIX_DEFAULT_FORMAT` are covered separately in [sdl2-endian-platform-macro-consolidation.md](sdl2-endian-platform-macro-consolidation.md).

### 3.7 SDL2_gfx

No function-like macros beyond `M_PI` guard define (simple math constant) and visibility-scope helpers (`SDL2_GFXPRIMITIVES_SCOPE`, etc.). GFX operates entirely through function calls.

### 3.8 SDL_net

| Macro | Type | Notes |
|---|---|---|
| `SDL_NET_VERSION(X)` | B | Struct fill |
| `SDL_NET_COMPILEDVERSION` | A | Computed |
| `SDL_NET_VERSION_ATLEAST(X,Y,Z)` | A | Standard pattern |
| `SDLNet_SocketReady(sock)` | A | Calls inline bool check function |
| `SDLNet_Write16/32(value, areap)` | B | Calls inline write functions |
| `SDLNet_Read16/32(areap)` | B | Calls inline read functions |

---

## 4. Per-Peer Comparison

### 4.1 ppy/SDL3-CS — `[Macro]` Manual Companion Pattern

| Aspect | Detail |
|---|---|
| **Mechanism** | Hand-written C# methods in companion `.cs` files with `[Macro]` attribute (`[Conditional("NEVER")]` — compile-time stripped) |
| **`[Macro]` catalog** | ~115 methods across 15+ companion files |
| **Categories covered** | Version arithmetic, audio format, pixel/colorspace, time conversion, atomic, error helpers, window position, keycode/mouse, surface |
| **What's NOT in `[Macro]`** | `SDL_min`/`max`/`clamp` (not surfaced), `SDL_arraysize` (not surfaced), swap macros (not surfaced) |
| **`[Constant]` pattern** | Separate attribute for manually-defined `const`/`static readonly` fields — read by `generate_bindings.py` to produce `--exclude` lists |
| **Generator integration** | `generate_bindings.py` scans companion `.cs` for `[Constant]`→exclude; `[Macro]` is naturally non-conflicting (methods don't collide with ClangSharp-generated constants) |
| **Satellite `_VERSION_ATLEAST`** | `[Macro]` method in each satellite companion file (3 total) |
| **Satellite `_VERSION(struct)`** | NOT surfaced — ppy does not generate struct-fill version macros for any family |
| **`COMPILEDVERSION`** | Not surfaced — ppy uses `SDL_VERSION` const directly |

**pph Architecture Quirk**: ppy's ClangSharp-generated `.g.cs` is **public** raw API. The `[Macro]` methods in companion `.cs` files extend the same `public partial class SDL3`. This means every macro helper is a public method that coexists with public `[DllImport]` stubs in the same class. Our architecture is inverted — internal raw ABI, public typed Layer 2 projection.

### 4.2 Alimer — Generator Exclusion + Manual Companion Helpers

| Aspect | Detail |
|---|---|
| **Mechanism** | Hardcoded `s_ignoreConstants` set (~70 entries) in `CollectConstants()` + inline name-pattern matching in generator |
| **Generator behavior** | **Excludes most function-like macros** from constant emission. Name-based patterns suppress audio/pixel/colorspace extractors, time conversions, haptic helpers, `SDL_min`/`max`/`clamp`, atomic refcount ops, cast helpers. |
| **What IS emitted** | Simple numeric constants (`const int SDL_MAJOR_VERSION = 3`), string-like macros as `ReadOnlySpan<byte>` properties |
| **Manual helpers** | Companion files (`SDL.cs`, `SDL.Mouse.cs`, `SDL.Pixels.cs`) provide ~20+ hand-written C# methods: `SDL_FOURCC`, `SDL_VERSIONNUM`, `SDL_VERSIONNUM_MAJOR/MINOR/MICRO`, `SDL_VERSION_ATLEAST`, `SDL_WINDOWPOS_*` (4 helpers), `SDL_BUTTON`, `SDL_DEFINE_PIXELFOURCC`, `SDL_SECONDS_TO_NS`/`SDL_NS_TO_SECONDS`/`SDL_MS_TO_NS`/`SDL_NS_TO_MS`/`SDL_US_TO_NS`/`SDL_NS_TO_US` (6), `SDL_SetHint`/`SDL_SetLogPriority` overloads |
| **Satellite scope** | N/A — Alimer only targets SDL3 core, no satellite families |

**Key design choice**: Alimer's generator suppresses function-like macros, but the project ships a curated set of ~20+ hand-written helpers in companion partial classes. This is a middle ground between ppy's full manual surface and a zero-helper raw catalog.

### 4.3 SDL2-CS — Hand-Written C# Methods (Minimal Surface)

| Aspect | Detail |
|---|---|
| **Mechanism** | Manual C# methods (no generation, no attributes) |
| **Surface covered** | `SDL_FOURCC`, `SDL_VERSIONNUM` (1000-based), `SDL_VERSION_ATLEAST`, `SDL_COMPILEDVERSION`, `SDL_VERSION(out SDL_version)` |
| **What's NOT surfaced** | All audio/pixel/colorspace extractors, time conversions, swap macros, `SDL_min`/`max`/`clamp`, window position helpers, button/ticks helpers |
| **Satellite `_VERSION_ATLEAST`** | **Not surfaced** — present in C headers but not in SDL2-CS |
| **Satellite `_VERSION(struct)`** | Struct-fill method per family (`SDL_IMAGE_VERSION`, `SDL_TTF_VERSION`, `SDL_MIXER_VERSION`) |
| **Satellite `_COMPILEDVERSION`** | **Not surfaced** |

**Key observation**: SDL2-CS covers the bare minimum — version helpers and `SDL_FOURCC`. Everything else is left to consumers.

### 4.4 Silk.NET 2.X — Generated Constants + Manual Helpers

| Aspect | Detail |
|---|---|
| **Mechanism** | ClangSharp-generated `public const int/string` for object-like macro constants in `Sdl.gen.cs` |
| **Function-like macros** | Not emitted by ClangSharp's macro binding (only handles object-like macros). A manual `partial class Sdl` companion file provides hand-written C# helpers for version, `SDL_FOURCC`, window position, pixel helpers, and audio format helpers |
| **Output shape** | Generated constants file + manual companion partial class extending `Sdl : NativeAPI` |
| **Satellite bindings** | No SDL_image/ttf/mixer satellite bindings found — core SDL only |
| **`[Flags]`** | Dropped entirely |

**Key observation**: Silk.NET is a raw binding catalog with a thin layer of manual macro helpers. Our Constitution (L45) explicitly differentiates from this model: *"Public raw bindings are a valid product choice for TerraFX/Silk.NET-style raw catalogs; this project is a stable SDL platform layer with typed low-level APIs and friendly overloads on top."*

---

## 5. Cross-Generator Comparison Table

| Aspect | ppy/SDL3-CS | Alimer | SDL2-CS | Silk.NET | Our Constitution |
|---|---|---|---|---|---|
| **Macro detection** | Implicit — not in `.g.cs`; manual `[Macro]` methods | Name-based exclusion patterns in generator | Hand-written C# (no generation) | Generated constants + manual companion partial class | Type A/B/C taxonomy expected |
| **Helper surface** | ~115 `[Macro]` methods, 15+ companion files | ~20+ hand-written methods (SDL.cs + companion files) | ~6 methods (minimal) | Hand-written helpers in `partial class Sdl` | Candidate lane defined |
| **Version helpers** | `[Macro]` methods | Manual `SDL_VERSIONNUM`/`SDL_VERSION_ATLEAST` in SDL.cs | Hand-written | Manual | Candidate lane |
| **Audio/pixel helpers** | `[Macro]` methods | Not surfaced (excluded by generator, not in manual companions) | Not surfaced | Manual | Not in candidate lane |
| **Time conversion** | `[Macro]` methods | Manual `SDL_SECONDS_TO_NS`/etc. in SDL.cs | Not surfaced | Not surfaced | Not in candidate lane |
| **`SDL_FOURCC`** | `[Macro]` method | Manual in SDL.cs | Hand-written method | Manual | Candidate lane |
| **`SDL_min`/`max`/`clamp`** | Not surfaced | Excluded by generator, not in manual companions | Not surfaced | Not surfaced | Not in candidate lane |
| **Swap macros** | Not surfaced | Excluded by generator, not in manual companions | Not surfaced | Not surfaced | Not in candidate lane |
| **`COMPILEDVERSION`** | Not surfaced | Excluded by generator | `static readonly int` | Not surfaced | Not in candidate lane |
| **Satellite `_VERSION_ATLEAST`** | 3 `[Macro]` methods | N/A | Not surfaced | N/A | Not in candidate lane |
| **Satellite `_VERSION(struct)`** | Not surfaced | N/A | Hand-written per family | N/A | Not in candidate lane |
| **Atomic refcount** | `[Macro]` methods | Excluded | Not surfaced | Not surfaced | Not in candidate lane |
| **Maintenance model** | Manual companion files (~27 files) | Hardcoded exclusion list (~70 entries) | Manual (no code sharing) | None (raw constants only) | Auto-generation preferred per "no magic companion" philosophy |

---

## 6. What Our Internal Docs Say

### 6.1 Constitution — `binding-generator-constitution.md`

**Lines 537-570** (`Constants And Macros` section):

| Line | Policy |
|---|---|
| 547 | *"Function-like public SDL macros are Layer 2 / friendly companion-helper candidates. They are not Layer 1 raw ABI declarations and must not be emitted as constants."* |
| 548 | *"Approved function-like macro helpers are manually authored or generated from an explicit companion-helper policy. ClangSharp does not emit them."* |
| 549 | *"Unknown function-like macros are reported and skipped."* |
| 551 | *"String-like SDL macro keys emit as canonical `ReadOnlySpan<byte>` UTF-8 literal properties. Do not duplicate every key as both `const string` and UTF-8 span."* |
| 554-564 | Current macro helper-candidate lane lists: `SDL_BUTTON`, `SDL_VERSION`, `SDL_VERSIONNUM`, `SDL_VERSION_ATLEAST`, `SDL_WINDOWPOS_*` helpers, `SDL_DEFINE_PIXEL*` helpers, `SDL_PIXELTYPE/ORDER/LAYOUT/BITSPERPIXEL/BYTESPERPIXEL`, `SDL_ISPIXELFORMAT_*` |
| 566-569 | SDL2 `SDL_VERSIONNUM(X,Y,Z)` spacing note: `X * 1000 + Y * 100 + Z` (SDL2), NOT `X * 1000000 + Y * 1000 + Z` (SDL3) |

### 6.2 Spike Docs

| Doc | What it says |
|---|---|
| [`satellite-expansion-roadmap.md`](../satellite-expansion-roadmap.md) §Item 4 (L288) | TTF version macros *"remain follow-up Layer 2 / friendly helper candidates governed by an explicit companion-helper policy"* |
| [`satellite-expansion-roadmap.md`](../satellite-expansion-roadmap.md) §Item 5 (L341) | Mixer version macros same treatment |
| [`item-1-per-library-generation-spec.md`](items/item-1-per-library-generation-spec.md) L54 | *"No function-like public macro helper completion... S1-2 only keeps ClangSharp warning-only exits diagnostic... approved helper methods require a later explicit companion-helper policy or manual implementation."* |
| [`next-iteration-plan.md`](../next-iteration-plan.md) L44-45 | Layer 2 (typed public API) deferred to Roadmap M5, Layer 3 (friendly overloads) to M6 |
| [`sdl2-satellite-error-function-consolidation.md`](sdl2-satellite-error-function-consolidation.md) §6 | Error macros are one category within the broader companion-helper policy; open decisions listed |

---

## 7. Recommended Approach

### 7.1 Guiding Principle: "No Magic Companion"

Per Deniz: *"yapacak bir şey A magic gibi olacak öteki türlü"* — explicitly rejected manual companion files as the primary mechanism. The generator should auto-produce macro helpers where possible.

### 7.2 Tiered Strategy

**Tier 1 — Auto-generate (Type A, pure evaluable):**
- Generator detects function-like macros in ClangSharp output that match known Type A patterns
- Emits `public static` C# methods with the computed expression
- Version arithmetic, pixel/audio/colorspace extractors, bit manipulation, window position helpers
- ~half of catalogued function-like macros

**Tier 2 — Auto-generate with raw ABI delegation (Type B, non-evaluable but generable):**
- Generator detects macros that call known SDL functions or fill struct fields
- Emits `public static` C# methods that delegate to Layer 1 raw ABI
- Version struct-fill, error helpers, file loader wrappers, convenience redirects

**Tier 3 — Manual or skip (Type C):**
- Token-pasting (`SDL_NAME`), compile-time assertions, platform ASM, stack allocation, `SDL_arraysize`
- Hand-write or exclude permanently

### 7.3 Open Decisions for Companion-Helper Policy

| Decision | Options | Peer evidence |
|---|---|---|
| Auto-generate or manual? | Auto from pattern-matching (preferred) vs JSON roster vs hand-written | ppy does 100% manual; Alimer does 0%; SDL2-CS does minimal manual |
| Which Type A macros to surface? | All evaluable vs Constitution candidate lane only vs per-family subset | ppy does ~115/115; SDL2-CS does ~6 |
| Which Type B macros to surface? | All generable vs only version helpers + error macros | ppy does all; SDL2-CS does version only |
| Satellite `_VERSION_ATLEAST`? | Generate per-family vs rely on Core `SDL_VERSION_ATLEAST` with different constants | ppy generates per-family; SDL2-CS does NOT surface |
| Satellite `_VERSION(struct)`? | Generate per-family vs skip (consumers use Core `SDL_VERSION` + per-family constants) | SDL2-CS generates; ppy does NOT surface |
| `SDL_min`/`max`/`clamp`? | Generate vs skip (C# has `Math.Min`/`Math.Max`/`Math.Clamp`) | No peer surfaces them |
| Swap macros? | Generate vs skip (C# has `BinaryPrimitives.ReverseEndianness`) | No peer surfaces them |
| **Generation mechanism**? | Postprocess `MacroHelperRewriter` (Roslyn-based) vs `FAMILY_CONFIG`-adjacent JSON roster vs `oracle.cs`-adjacent evidence pass | ppy uses companion file scan; Alimer uses hardcoded set |
| **When**? | After all satellite Layer 1 complete (Roadmap M5/M6) | — |

### 7.4 Layer Assignment

| Macro type | Layer | Rationale |
|---|---|---|
| Type A (pure evaluable) | **Layer 2** (public typed low-level) | No native calls, pure C# math — belongs with typed API surface |
| Type B (delegates to raw ABI) | **Layer 3** (friendly overloads) | Convenience wrappers — thin methods over raw ABI, same pattern as `string` overloads |
| Error function redirects | **Layer 2** | Same visibility as the raw `SDL_SetError`/`SDL_GetError` they wrap — satellite consumers need them for their family's error reporting |

---

## 8. References

- **Constitution (policy authority):**
  - [`binding-generator-constitution.md`](../../../../docs/binding-autogen/binding-generator-constitution.md) L537-570 — Constants And Macros policy
  - Layer Contract L35-59 — three-layer architecture
  - L566-569 — SDL2 `SDL_VERSIONNUM` spacing note
- **Peer reference:**
  - `references/ppy-SDL3-CS/SDL3-CS/SDL_version.cs` — `[Macro]` version helpers (SDL3 1000000-based)
  - `references/ppy-SDL3-CS/SDL3-CS/SDL_pixels.cs` — `[Macro]` pixel/colorspace helpers (30 methods)
  - `references/ppy-SDL3-CS/SDL3-CS/SDL_audio.cs` — `[Macro]` audio format helpers (10 methods)
  - `references/ppy-SDL3-CS/SDL3-CS/MacroAttribute.cs` — attribute definition
  - `references/alimer-bindings-sdl/src/Generator/CsCodeGenerator.Constants.cs` — generator exclusion logic
  - `references/alimer-bindings-sdl/src/Alimer.Bindings.SDL/SDL.cs:162-239` — manual `SDL_FOURCC`, `SDL_VERSIONNUM`, `SDL_VERSION_ATLEAST`, `SDL_WINDOWPOS_*` helpers
  - `references/alimer-bindings-sdl/src/Alimer.Bindings.SDL/SDL.Mouse.cs:38` — `SDL_BUTTON`
  - `references/alimer-bindings-sdl/src/Alimer.Bindings.SDL/SDL.Pixels.cs:12` — `SDL_DEFINE_PIXELFOURCC`
  - `external/sdl2-cs/src/SDL2.cs:1379-1412` — SDL2-CS version macros (SDL2 1000-based)
  - `external/sdl2-cs/src/SDL2.cs:185` — `SDL_FOURCC`
- **Header analyses:**
  - `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_version.h:77-121` — Core version macros
  - `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_stdinc.h:136-862` — Math/utility/cast macros
  - `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_pixels.h:120-171` — Pixel format macros
  - `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_audio.h:77-878` — Audio format macros
  - `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_endian.h:151-384` — Swap macros
  - `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_video.h:137-148` — Window position macros
  - Per-family satellite headers (version macros) — cataloged in §3
- **Previous consolidation:**
  - [`sdl2-satellite-error-function-consolidation.md`](sdl2-satellite-error-function-consolidation.md) — error function macro sub-topic
