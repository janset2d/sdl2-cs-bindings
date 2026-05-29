# Binding Generator Constitution

> **Status:** Canonical binding-generator constitution. Pure principles — toolchain-neutral ABI/API policy. Mechanism details and current implementation evidence live in [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).

## Purpose

The generator must answer two questions without mixing them:

1. Which SDL declarations belong in a generated binding family?
2. How does each native declaration become ABI-correct, stable C#?

The internal ABI policy below is the durable contract for any generator implementation. The toolchain-neutral policy decisions catalogued throughout — §"C `long`", §"wchar_t", §"Opaque Handles", §"Foreign Type Boundary Policy", §"Structs And Unions", §"SDL_bool", §"Enums", and the cross-assembly Pattern B contract — are binding requirements any compliant successor implementation must honor.

## Authority Order

Use this document for intended binding-generator policy, but verify current
behavior against the implementation and tests before making behavior claims. When
sources disagree, use this order:

1. Pinned SDL public headers from the exact vcpkg version.
2. Actual packaged native binary exports.
3. Current generator implementation and tests for actual behavior.
4. SDL dynapi manifests for function-name coverage only.
5. This constitution for intended policy.
6. `binding-generator-roadmap.md` for future work sequencing.
7. ADR-004 for the recorded 2026-05-14 toolchain reasoning.
8. Peer bindings and old research as evidence only.

Implementation-mechanism authority (item 3) is captured in [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).

Historical plans, spike reports, and superpowers specs are not policy. If they disagree with this document, this document wins. If the implementation disagrees with this document, treat it as code/docs drift to investigate, not as permission to ignore either source.

## Layer Contract

Generated bindings use three layers:

1. **Internal raw ABI layer**: generated, unsafe where needed, exact ABI, `internal`, and the only layer with `[DllImport]` or `[LibraryImport]`.
2. **Public typed low-level layer**: generated handles, enums, structs, callbacks, constants, and thin public methods that call the internal raw layer without exposing extern declarations.
3. **Public friendly overload layer**: generated conveniences for `string`, `ReadOnlySpan<byte>`, `Span<T>`, `ReadOnlySpan<T>`, `out`, `ref`, bool conversions, and explicit preformatted variadic helpers.

### Internal Raw ABI

**Why:** Generated native imports are volatile implementation detail, not the user-facing compatibility contract. Keeping them internal lets the generator fix C `long`, `wchar_t`, bool wire shape, struct layout, platform attribution, and backend choices without turning every correction into a public breaking change. Public raw bindings are a valid product choice for raw-catalog products; this project is a stable SDL platform layer with typed low-level APIs and friendly overloads on top.

**How:** The raw ABI container type is internal. Generated members may remain lexically `public` when produced by upstream emitters, but they are not effectively public API when their containing type is internal. Public low-level APIs call the internal raw container and expose honest typed handles, `nint` values, spans, pointers, and unsafe overloads where SDL requires them.

**What:** The main package exposes a typed, low-allocation SDL API plus friendly overloads — not generated `[DllImport]` / `[LibraryImport]` classes. Escape hatches belong in typed handles, `DangerousGetHandle()` / `nint`, span/pointer overloads, and deliberately unsafe APIs. If demand appears later, a separate raw package can be designed with an explicit different compatibility promise.

Rules:

- Public raw `IntPtr` externs are not part of v1 preview.
- SDL2-CS compatibility is best-effort. It is an oracle, not the API target.
- Raw ABI mistakes are still bugs even though the raw layer is internal.
- A declaration that cannot be represented honestly is deferred with evidence. Do not emit a success-shaped lie.
- Typed handles expose native pointer values through `nint`; that does not make `nint` the answer for every native scalar.
- Raw ABI backends may split into `DllImport` and `LibraryImport` generated files, but both consume the same semantic/projection truth. File-level TFM guards are preferred over per-function conditional sprawl.
- Compatibility packages such as `System.Memory` are acceptable for public/friendly APIs on `netstandard2.0` and `net462` when package smoke proves the consumer contract. They must not be used to fake ABI primitives whose platform shape is not portable.

## Generator Home

The generator is build infrastructure, not a standalone product project. The production implementation must satisfy these contracts:

- Produce committed `.g.cs` source consumed by managed family csprojs. Generation never runs in consumer builds.
- Reproducible output anchored by a per-family `.generated-stamp` with no wall-clock fields.
- Cross-cutting validators reachable from the build host's PreFlight and Pack stages.
- Persisted binding-generation data contracts reachable from the build host's Data layer.
- Local invocation routed through `tools.cs` so day-to-day developers do not handle generator orchestration directly.
- Linux-canonical parsing for ABI correctness across the 7-RID surface unless a different model is proven equivalent.

Concrete generator home paths and orchestration entry points: see [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).

## Generator Engine And SDL Policy

The generator should separate parser/ABI mechanics from SDL family policy without pretending to be a general binding-generator product.

Rules:

- The core pipeline is an ABI engine for parsed declarations, native type classification, platform parse-view merge, raw ABI projection, and deterministic file-set emission.
- SDL-specific decisions live behind named policy/profile concepts: owned prefixes, core-owned type references, SDL2 versus SDL3 bool shape, known opaque structs, string-like macro handling, SysWM layout, and satellite-to-core reference rules.
- Keep those concepts target-local until a second real generator target exists. Do not promote them to root `Shared` or standalone `src/` projects for aesthetic symmetry.
- M2 may expose an `SdlPolicy` seam while preserving SDL2.Core output. M3 owns the real profile/config boundary for SDL2 core, SDL2 satellites, SDL3 core, and SDL3 satellites.
- Generic model-building or emission code must not accumulate ad hoc `/SDL2/`, `SDL_`, `SDL2`, or library-name checks once a named policy/profile seam exists. Add a policy collaborator instead.
- Satellite profiles must be designed against real installed headers before generation is enabled. SDL2.Image / Mixer / Ttf mostly use SDL's `extern DECLSPEC` convention, while SDL2_gfx uses per-header `SDL2_*_SCOPE` export macros and a mixed naming surface; this is profile policy, not a reason to special-case generic CppAst processing.
- `profile_id` is a manifest routing key into code-owned profile policy. It is not a behavior switch that lets JSON redefine ABI rules.

## Generation Determinism Contract

The generator pipeline is a **deterministic function** from a pinned input set to committed `.g.cs` output. Determinism is not a quality-of-life property; it is policy. The following invariants are binding regardless of which toolchain ships.

### Determinism Inputs Principle

Every output byte is determined by a pinned, auditable input set alone (vcpkg-installed native headers, vcpkg triplet, generation engine version, response/config files, the per-family header list and config, postprocess code, orchestrator code). If none of these change, regeneration produces byte-identical output (CRLF aside on Windows).

The determinism unit is the complete selected family artifact: that family's `Generated/` root, including every backend projection and every postprocess output written under that root. Wall-clock fields, machine identifiers, build timestamps, or environment-derived values are **forbidden** in any committed `.g.cs` or in the per-family `.generated-stamp`. Backend projections (e.g. Compat/Modern TFM splits) are projections of the same family artifact; production generation runs them together as a single complete-family unit, never writing partial bootstrap/header-subset output into committed `Generated/` roots.

### Family Isolation

The pipeline guarantees three simultaneous properties:

1. **Targeted-family-only writes.** Targeted single-family regeneration cleans and regenerates only that family's `Generated/` root. Other families' directories are byte-untouched.
2. **Per-family equivalence.** Running each family individually produces the same `.g.cs` output as running the full active set (modulo the activation set; dormant families remain dormant on both paths).
3. **Independent postprocess execution.** Each family's postprocess pipeline executes against that family's own Generated tree only. The only cross-family data flow at postprocess execution time is the config-data pull described under §"Opaque Handles" — a **data-only** pull from per-family config, not a cross-directory file read.

### Dependency Direction

Two distinct dependencies, not to be conflated:

- **Generation → postprocess (forward, runtime).** Generation writes `.g.cs`; postprocess reads `.g.cs`. Postprocess depends on generation's output but does not feed back into generation.
- **Build-time Core ← Satellite (ProjectReference).** A satellite's compiled assembly resolves Core-owned type names against Core's generated handles via ProjectReference. This is a **C# compile-time** dependency; the generator pipeline never reads cross-family `.g.cs` files at generation or postprocess execution time. Core can be regenerated before, after, or independently of any satellite.

A satellite's regeneration consumes Core's required-surface manifest facts only at the manifest level; it does not consume Core's generated `.g.cs` artifacts.

### Native Header Resolution Scope

The generation engine's include directory exposes the entire SDL2 native header tree to every per-family parse invocation. The per-file emit scope scopes **emit** to one header at a time; `#include`'d type declarations from other headers are **resolved for correctness but never emitted** in the family's output. Across the generated output for all families, every public C type is defined exactly **once** (in its owning family's `.g.cs`); satellite `.g.cs` files reference Core types by name and rely on ProjectReference + nested namespace resolution at C# compile time. Duplicate emission of a Core type across families is a **generator bug**, not a coexistence pattern.

### Pure-Inputs Discipline

Generation is not allowed to consume environment-derived inputs beyond the pin set above:

- **No machine-local paths** in committed output (include-directory paths are only used for parse; they do not leak into emitted attribute arguments).
- **No timestamps**, build dates, machine names, user names, or CI run identifiers in committed `.g.cs` content.
- **No conditional behavior on host OS** at generation time except via the explicit platform-view pass. Production generation runs against native platform headers via container or per-RID CI.
- **No network access** at generation time. All inputs must be local files reproducible from the pin set.

### Verification Contract

Every slice that touches generation or postprocess MUST verify the determinism contract in its exit evidence: complete-family idempotency (twice-regenerated diff is empty), family isolation (targeted run leaves others byte-untouched), per-family equivalence (full active-set equals union of per-family runs), and cross-family handle pull preservation (satellite output rewrites Core-owned pointer types to by-value).

Concrete pin-set enumeration, verification commands, and per-toolchain implementation: see [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).

Slices that change the determinism contract itself (e.g. add a new pin-set input, change family isolation semantics) must update **this section** in the same change set, with rationale and the new verification step. Drift between the contract and the implementation is treated as a hard bug.

## Manifest Configuration Vs Code-Owned Policy

`build/manifest.json library_manifests[].binding_generation` is per-family configuration. It is not a hidden policy language.

Manifest owns reviewable facts that vary by family: `enabled`, `profile_id`, `managed_namespace`, `primary_class_name`, `platform_catalog`, `owned_prefixes`, `parse_defines`, `clang_args`, `header_set`, `excluded_functions`, `required_functions`, `required_constants`, `macro_constants.excluded`, `macro_constants.overrides`, `deferred_declarations`, `validators`, and `dynapi`. `export_macro_names` may be added as a declaration-visibility token inventory when tests prove it earns its place — the field lists family-specific macro names; code-owned declaration visibility strategy decides how those names affect parsing, macro suppression, and export evidence.

Raw ABI class name, native import name, and satellite core-reference identity are derived first from existing manifest facts (`primary_class_name`, `library_manifests[].name`, and `package_families[].depends_on`). Promote them into explicit manifest fields only when a RED test proves the convention is insufficient for a real family.

- **Derivation failure typing.** When derivation fails (e.g., primary-binary metadata too wildcarded to prove the native import name), the failure MUST surface as a typed error citing the family + profile + derivation mode. Silently introducing a new JSON field as workaround is forbidden — derivation failure means the manifest convention is insufficient and the contract requires explicit family-policy resolution.

Generator code owns ABI/API policy: scalar width mapping; SDL2 vs SDL3 bool wire shape; opaque-handle detection; pointer/array/callback/function-pointer/userdata classification; UTF-8 string and span overload behavior; C variadic handling; struct/union layout policy; macro taxonomy and safe expression evaluation; platform parse-view merge and attribution rules; public API layering.

Exception rule:

- Manifest exceptions must name the declaration, category, source/reason, and rationale.
- Silent JSON knobs that change ABI behavior are forbidden.
- Move behavior into the manifest only when it genuinely varies by family or needs an explicit per-family override.
- If every family must obey the same rule, keep it in code and tests.
- A manifest fact may route to a code-owned profile or named exception. It must not encode scalar width, bool wire shape, pointer classification, callback handling, variadic behavior, macro taxonomy, platform merge, or struct/union layout.

### Generation Process Contracts

These are toolchain-neutral correctness contracts any compliant successor implementation must satisfy:

- **Validator-id integrity.** Enabled validator ids unknown to the registered validator set MUST fail closed with the unknown id named in the error message. Silent no-op on unknown ids is forbidden.
- **Cleanup safety.** Per-family regeneration MUST clean prior output before writing new files; the cleanup MUST be bounded by a path-prefix guard pinning it to the family's generated-output root. Cleanup that can wander outside the family root is a process bug.
- **Disabled-family side-effect isolation.** Disabled binding-generation families MUST be resolvable through config/profile checks without invoking parse, model build, emit, validator run, output cleanup, compile-check, or smoke side effects. The audit-plan resolution path exists precisely to enable disabled-satellite manifest hygiene without enabling generation.

### Configurable Scope Vs Policy Mechanism

The binding generator has two kinds of configuration, separated by the question each answers:

- **Config owns the application scope** — answers **"which"** and **"what"**: which families exist, which headers belong to them, which methods are affected by C `long` dispatch, which enum names get `[Flags]`, which families own opaque handles. These per-family facts change when a family is added, an SDL version introduces new types, or an audit expands coverage. Config is reviewable data; it must not encode *how* the policy operates.
- **Code owns the policy mechanism** — answers **"how"**: how a C `long` return gets emitted with the modern source-generator pattern versus the legacy dispatch pattern; how the suffix rule and allow-list combine to decorate `[Flags]`; how Pattern B handle structs are templated; how `SDL_GUID` maps to `System.Guid`. Mechanism is invariant across families. It must not be driven by JSON knobs that silently alter ABI behavior.

Rules:

- Per-family "which symbols / which families" data goes in config.
- Cross-family constants mechanically derived from SDL2 source headers (platform view definitions, platform macro enumeration) are config data.
- Code that transforms generated `.g.cs` output (rewriter implementations, pipeline orchestration) is policy mechanism and stays in code.
- Config is read by both the generator orchestrator and the postprocess toolchain from the same file — no per-consumer parallel copies of the same fact.
- Before moving a fact into config, ask: does this vary by family, or could it vary by family? If yes, config. If "no, same for every family forever" and it concerns *how* a transform operates, code.

Current config field enumeration: see [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md) §"Config Surface Evolution".

## Family Identity

Generated C# identity is family-owned. The public namespace and primary public class are manifest-driven today; the internal raw ABI class and native import library are intended family identity seams and may be code-derived until M3 profile/config work promotes them explicitly.

| Family | Namespace | Public class | Internal raw ABI class |
| --- | --- | --- | --- |
| SDL2 core | `SDL2` | `SDL` | `SDLNative` |
| SDL2_image | `SDL2.Image` | `SDL_image` | `SDL_imageNative` |
| SDL2_mixer | `SDL2.Mixer` | `SDL_mixer` | `SDL_mixerNative` |
| SDL2_ttf | `SDL2.Ttf` | `SDL_ttf` | `SDL_ttfNative` |
| SDL2_gfx | `SDL2.Gfx` | `SDL2_gfx` | `SDL2_gfxNative` |

Parse views such as `Neutral`, `WindowsDesktop`, `Linux`, and `MacOS` are parser metadata and file attribution. They do not become public class names.

## Function Surface

Function inclusion is header-first and validated by available native evidence.

Rules:

- A generated function must trace to a pinned public header declaration or a required manifest declaration from an intentionally excluded umbrella header.
- A generated SDL2.Core function name must match dynapi/export evidence unless explicitly excluded or deferred. Dynapi validates names only — it does not prove parameter order, scalar width, struct layout, enum backing type, or ownership semantics.
- SDL2 satellite function names are validated by a family-specific evidence source because satellites do not ship SDL2.Core's dynapi manifest. Image/Mixer/Ttf use public `extern DECLSPEC` declarations; SDL2_gfx uses per-header `SDL2_*_SCOPE` declaration macros plus harvested binary symbol evidence when available.
- Platform-specific declarations stay in the family raw ABI class and receive platform attribution.
- `SDL_main`, `SDL_DYNAPI_entry`, startup glue, and dynapi internals are not ordinary public binding functions.

Current accepted deferrals: `FILE`, `_IO_FILE`, `va_list`, `__va_list_tag` APIs (deferred unless a portable mapping is deliberately designed); `SDL_RWFromFP`, `SDL_LogMessageV`, `SDL_vsnprintf`, `SDL_vsscanf`, `SDL_vasprintf` (Stage 1 accepted deferrals).

## BCL-Replaceable Helper Exclusion Policy

SDL2 ships convenience helpers that duplicate functionality already in the .NET Base Class Library (BCL). SDL provides these because libsdl targets platforms with incomplete or missing libc primitives; .NET runtimes always carry the BCL, so re-binding these helpers is ceremony with **negative** ergonomic payoff (caller learns a second API to do what BCL already does, plus an extra P/Invoke hop).

**Rule.** A symbol or function family is excluded from the Janset.SDL2 surface when **all three** conditions hold:

1. A direct BCL equivalent exists with equal or better ergonomics (e.g. `System.Math.Round` for `SDL_lround`; `long.Parse` for `SDL_strtol`; `System.Text.Encoding` for `SDL_iconv_*`).
2. SDL2-CS (the reference binding) does not expose the symbol/family.
3. No other SDL2 symbol transitively depends on the excluded symbol (verified by grep across pinned SDL headers).

**Mechanism.** Per-header exclusion in the toolchain's per-header configuration, with a comment block recording the rule's three conditions and the BCL equivalent. Per-toolchain exclusion-routing detail and standing-exclusion config evidence: see [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).

**Standing exclusions (Priority C):**

- `SDL_lround`, `SDL_lroundf`, `SDL_ltoa`, `SDL_ultoa`, `SDL_strtol`, `SDL_strtoul` (SDL_stdinc.h) — BCL: `Math.Round`, `long.Parse`, `ToString()`, `ulong.Parse`.
- `SDL_iconv_open`, `SDL_iconv_close`, `SDL_iconv`, `SDL_iconv_string` (SDL_stdinc.h) — BCL: `System.Text.Encoding` family. Also implicitly excludes `SDL_iconv_t` opaque type at call-site scope (no remaining function references it).

**Non-rule.** Excluding a function does NOT cascade through dependent typedefs automatically. A type stays declared (empty struct) unless explicitly excluded too — with no remaining function consuming it, the empty struct is harmless residue. Document the residue in the family's roster for audit-trail.

## C Variadics

C ellipsis functions are not exactly representable in portable C# P/Invoke. Stage 1 policy is deliberate fmt-only mapping:

- Non-`va_list` SDL variadic functions may be emitted internally only as fixed-prefix, fmt-only calls.
- The public/friendly API must make the preformatted-string contract explicit.
- These imports must not be documented or reported as exact C varargs ABI declarations.
- `__arglist` is rejected for this repo because it does not compose with the planned string/span overload tiers and old TFM support.
- `va_list` variants remain deferred.

Examples: `SDL_Log(byte* fmt)` means "log this already formatted UTF-8 string," not "forward arbitrary C varargs"; `SDL_snprintf` / `SDL_sscanf` require explicit public wrapper policy before promotion beyond internal raw usage.

Required evidence: parse/report distinguishes fmt-only mapped variadics from exact functions; friendly wrappers make formatting behavior visible; tests prevent treating `...` as ordinary dropped parameters.

## Scalar Type Translation

Exact-width SDL typedefs map by width.

| Native type | Managed raw concept |
| --- | --- |
| `Sint8` / `Uint8` | `sbyte` / `byte` |
| `Sint16` / `Uint16` | `short` / `ushort` |
| `Sint32` / `Uint32` | `int` / `uint` |
| `Sint64` / `Uint64` | `long` / `ulong` |
| `size_t` | `nuint` |
| `ptrdiff_t` | `nint` |
| pointer values / `void* userdata` | `nint`, `void*`, or typed pointer by layer |

### C `long` And `unsigned long`

C `long` is platform-sensitive, not pointer-sized. Windows LLP64 makes `long` / `unsigned long` 32-bit on x86/x64/arm64; Unix LP64 target RIDs make them 64-bit on x64/arm64. `nint`/`nuint` are wrong on Windows x64/arm64 because they are 64-bit where C `long` is 32-bit. `System.Runtime.InteropServices.CLong` / `CULong` model this correctly on modern TFMs but are unavailable on all current target frameworks.

Contract:

- Do not map C `long` or `unsigned long` directly to `nint` / `nuint` in shared generated signatures.
- Internal raw ABI uses `CLong` / `CULong` where available, guarded to `NET6_0_OR_GREATER` until a downlevel exact strategy exists.
- Public typed wrappers may normalize values to stable managed shapes such as `long` / `ulong`, with Windows range checks for input parameters when needed.
- Typedefs over C `long` (e.g. `SDL_threadID`) inherit this policy unless a stronger SDL semantic type is introduced.
- Do not introduce a casual downlevel `CLong` / `CULong` polyfill — a same-named portable struct backed by `IntPtr`, `int`, or `long` would be wrong for at least one of Windows LLP64 or Unix LP64. Any downlevel strategy must prove exact per-platform ABI shape before removing guards.
- Satellite profiles (e.g. SDL_ttf `TTF_OpenFontIndex*` / `TTF_FontFaces`) reuse the same C `long` policy.

Priority C hybrid strategy:

**Why:** SDL2's `long`-using API splits into two categories. SDL_stdinc convenience helpers (`SDL_lround`, `SDL_lroundf`, `SDL_ltoa`, `SDL_ultoa`, `SDL_strtol`, `SDL_strtoul`) have direct BCL equivalents and fall under §"BCL-Replaceable Helper Exclusion Policy". Structural symbols (`SDL_threadID` typedef plus `SDL_ThreadID` / `SDL_GetThreadID` functions) identify OS threads; `System.Threading.Thread.ManagedThreadId` is not equivalent (different ID space) and must be preserved.

**How:**

- Convenience helpers (SDL_stdinc family) are excluded from raw ABI emission on every TFM via the BCL-Replaceable Helper Exclusion Policy; consumers use the BCL equivalents.
- Structural thread API symbols use a hybrid emit, mode-split across modern and legacy TFM views: modern views get the source-generator P/Invoke pattern with `CLong` / `CULong` return type; legacy views get a managed wrapper dispatching at runtime between Windows (`unsigned long` = 32-bit) and Unix LP64 (`unsigned long` = 64-bit), normalized to `ulong` at the caller surface — Microsoft's [documented cross-platform `long` dispatch pattern](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices).
- The "any downlevel strategy must prove exact per-platform ABI shape" requirement above is satisfied before production source promotion by a per-RID runtime ABI smoke test calling each retained symbol and asserting non-zero bit-pattern on each supported RID.

**What:** Six SDL_stdinc convenience symbols deferred all-TFM. Three SDL_thread symbols (the `SDL_ThreadID` / `SDL_GetThreadID` functions and the `SDL_threadID` typedef they return) preserved on every TFM via the hybrid emit. Satellite C `long` surface (SDL_ttf `TTF_OpenFontIndex*` / `TTF_FontFaces`) reuses this same hybrid pattern when those satellites enter generation.

High-risk SDL2.Core symbols (Priority C disposition):

- `SDL_lround`, `SDL_lroundf` — **deferred all-TFM** (BCL equivalent: `Math.Round`)
- `SDL_ltoa`, `SDL_ultoa` — **deferred all-TFM** (BCL equivalent: `value.ToString()`)
- `SDL_strtol`, `SDL_strtoul` — **deferred all-TFM** (BCL equivalent: `long.Parse` / `ulong.Parse`)
- `SDL_threadID`, `SDL_ThreadID`, `SDL_GetThreadID` — **kept on every TFM** via hybrid `CLong` + dual-dispatch emit

Per-toolchain C `long` mechanism (exclusion routing, TFM-split layout, dispatch wiring): see [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).

### `SDL_bool`

SDL2 and SDL3 bool-like values are separate policies.

| Family | Native shape | Raw wire type | Friendly shape |
| --- | --- | --- | --- |
| SDL2 | `typedef enum { SDL_FALSE = 0, SDL_TRUE = 1 } SDL_bool` or `typedef int SDL_bool` fallback | `int` | `bool` conversion uses `!= 0` |
| SDL3 | C bool-like 1-byte value | `byte` or byte-backed wrapper | `bool` conversion |

Contract:

- SDL2 `SDL_bool` public enum must be int-backed.
- Mapping SDL2 `SDL_bool` to `byte` is ABI-wrong.
- SDL3 bool policy must not be copied from SDL2.

### `wchar_t`

`wchar_t` is platform-sensitive: Windows `wchar_t` is 2 bytes, Unix-like target RIDs generally use 4 bytes, C# `char*` is always 2-byte UTF-16 code units, and C# `int*` is always 4-byte elements.

Contract:

- Do not map `wchar_t*` to `int*` or `char*` in shared cross-platform generated public structs or signatures.
- Low-level raw shape uses opaque pointer representation unless a platform-specific helper is generated.
- Friendly APIs may decode wide strings only through platform-aware helpers with tests.

Priority C mechanism:

**Why:** Opaque `nint` at the raw ABI layer is the ABI-correct mapping for shared `wchar_t*`, not a Layer 3 friendly-wrapper repair of a "broken" ABI. No portable C# primitive maps correctly across the target RID set; Microsoft's BCL has no portable `wchar_t` story (`[MarshalAs(UnmanagedType.LPWStr)]` and `CharSet.Unicode` are hardcoded 16-bit even on Linux, mismatching POSIX's 32-bit `wchar_t`). Opaque pointer is the only ABI-honest mapping at Layer 1.

**How:** The wide-string type maps to opaque `nint` at the raw ABI layer for every shared signature position (function parameter, return, field). The mapping is realized by the toolchain's type-classifier rule with a postprocess fallback where the type-system path does not reach a specific case. Windows-only API surface like `SDL_WinRTGetFSPathUNICODE` retains its existing `[SupportedOSPlatform("windows")]` attribution.

**What:** Shared `wchar_t*` (HIDAPI fields, `SDL_wcs*` functions, ~15 SDL2.Core symbols) emit as `nint` at the raw ABI layer. Layer 3 friendly wrappers (later slice) provide platform-aware decoders (`Marshal.PtrToStringUni` on Windows; UTF-32 transcode on POSIX) — those are ergonomics, not ABI repair.

Per-toolchain wchar_t mechanism (type-classifier rule routing, postprocess fallback wiring): see [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).

## Opaque Handles

SDL-owned opaque handles are public readonly value types wrapping `nint`.

Rules:

- Emit one public handle type per SDL public typedef concept.
- Prefer the public typedef name over the underlying C struct tag.
- Do not emit duplicate public handles for `typedef struct tag Name;` or `typedef struct tag* Name;` patterns.
- The underlying C struct tag is parser evidence, not automatically public .NET API.
- Handle types expose a native value escape hatch for advanced consumers; that is not a license to expose public raw externs.

Resolved examples:

- `SDL_hid_device_` must not be emitted alongside canonical `SDL_hid_device`.
- `SDL_semaphore` must not be emitted alongside canonical `SDL_sem`.

Public typed handle struct shape (Pattern B):

**Why:** A `readonly partial struct X(nint value)` with a single pointer-sized field is ABI-equivalent to passing a bare `IntPtr` at the P/Invoke boundary — a single-integer-field composite is classified identically to that integer in every calling convention this repo targets (SysV x64, AAPCS64, MSVC ARM64, x86). It is blittable; modern source-generator marshalling supports it without `[MarshalAs]`; the legacy marshaller uses the blittable fast path. The "use IntPtr only at the P/Invoke boundary" advice in older interop literature never reflected an ABI constraint.

**How:** Each opaque handle emits as `public readonly partial struct X(nint value) : IEquatable<X>` with explicit `[StructLayout(LayoutKind.Sequential)]`, get-only `Value` property, `IsNull` / `IsNotNull` / `Null` sentinels, `DangerousGetHandle()` escape hatch, full equality contract, and **explicit** `operator nint` / `operator X` only — no implicit operator (implicit conversion weakens type safety; the deliberate-escape case uses `DangerousGetHandle()`). The struct is lexically `public` but appears in `internal` raw ABI signatures inside the family raw container. Raw signatures pass by value; single-pointer references rewrite to by-value at all three raw-ABI positions (method parameter, return, and struct field); double-pointer (`X**`) and `out X` parameter positions are preserved as-is.

**What:** Every SDL opaque concept emits one typed handle struct of this shape — both auto-detected empty-body opaques and force-opaque types from a Constitution-bound allow-list (`SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg` per §"Structs And Unions"). Layer 2 reuses these handle types in its public method projection unchanged.

**Auto-detect criterion.** A type is auto-detected as a Pattern B candidate iff its generated declaration is an empty struct **and** the same name appears as a pointer type in at least one raw ABI signature position (parameter, return, or struct field) within the same TFM view. Detection is purely syntactic. The criterion is family-blind: Core handles and satellite-owned handles (e.g. `TTF_Font`, `Mix_Music`) satisfy the same structural test; the rewriter does not gate on a name prefix.

**Cross-family handle name resolution.** A satellite's postprocess pass must know which names are handle types — not just satellite-owned ones but also Core-owned ones that the satellite consumes by-value at its raw ABI surface. Each satellite's loader merges the satellite's own auto-detect + force-opaque sets with Core's auto-detect + force-opaque sets. The pull is **data-only**: satellite postprocess execution does not read cross-family `.g.cs` files. Drift watchdog stays per-family.

**Force-opaque allow-list rule.** The Stage 1 force-opaque names (`SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg` per §"Structs And Unions" rationale) remain authoritative as policy prose; the machine-readable enumeration lives in the Core family's force-opaque config entry. Changes to either prose or config must update both sides in the same commit. Satellite force-opaque lists start empty and only populate if a satellite ships its own platform-dependent struct whose body is unsafe to expose.

**Cross-assembly Pattern B contract (`[assembly: DisableRuntimeMarshalling]`).** The modern source-generator P/Invoke surface (net7+) emits a diagnostic when a satellite assembly uses a Pattern B handle struct by value that is defined in a *referenced* assembly — the source generator inspects cross-assembly types through metadata and falls back to a "user-defined struct requires runtime marshalling opt-in" path even though Pattern B is blittable. The contract is to apply `[assembly: DisableRuntimeMarshalling]` (guarded `#if NET7_0_OR_GREATER`) to every assembly that exposes modern P/Invoke declarations consuming Pattern B handles. This suppresses the cross-assembly diagnostic, guarantees blittable-only types at every P/Invoke position, and makes any future non-blittable signature fail at build time instead of silently triggering runtime marshalling. The legacy `[DllImport]` backend on downlevel TFMs does not trip the diagnostic and does not need the attribute.

Janset.SDL2's P/Invoke surface is independently blittable-clean by design: strings emit as `byte*` (not `string`), booleans use the `SDL_bool` enum (not `bool`), and aggregate parameters are either pointers or Pattern B structs. The attribute is opt-in formalization of an existing invariant, not a behavior shift.

Implementation mechanism (rewriter wiring, auto-detect criterion realization, canonical roster JSON schema, cross-family loader contract): see [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).

## Foreign Type Boundary Policy

SDL2 references types owned by **external native libraries** (Vulkan, Direct3D / DXGI, Microsoft GDK, Linux X11 / Wayland / KMSDRM, macOS Cocoa / UIKit / Metal, etc.) at parameter positions in its public API. Consumers obtain these from dedicated .NET bindings (`Silk.NET.Vulkan`, `Vortice.Windows`, `Silk.NET.OpenGL`, etc.). The Janset.SDL2 binding must let those external typed handles cross the SDL boundary without explicit-conversion friction.

**Why:** Wrapping foreign types in our own typed handle structs (Pattern B per §"Opaque Handles") forces users to write `new VkInstance(silkInstance.Handle)` at every cross-binding call site. The pragmatic peer convention — `IntPtr` / `nint` at foreign parameter positions — ships zero-friction interop with any .NET binding that exposes a pointer-sized handle (`.Handle` accessors returning `IntPtr` / `nint` are the standard). The §"Opaque Handles" typed-handle policy is binding for **SDL-owned** types only; foreign types are not ours to own, rename, or wrap. Distinguishing SDL-owned from foreign types is a manual policy decision — no automatic mechanism exists.

**How:** Foreign types at SDL parameter positions emit as opaque `nint` / `IntPtr`. A `[NativeTypeName("VkInstance")]` annotation (or equivalent for the toolchain) preserves provenance for documentation and downstream postprocess sensors. The mechanism is realized by the toolchain's per-header configuration (byte-exact textual match for the typedef / pointer spelling, plus suppression of the parser tag struct emission). The allow-list lives in per-header configuration close to the header that references the foreign type, with comments citing the SDL header source line of the typedef and the upstream owner library.

**What:** The Priority C survey identifies these foreign-type categories in SDL2 public API. Activation/deferral disposition per category, with per-header source line references and per-toolchain configuration mechanism, lives in [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md).

| Category | Owning header |
| --- | --- |
| Vulkan (`VkInstance`, `VkSurfaceKHR`) | `SDL_vulkan.h` |
| Direct3D COM (`IDirect3DDevice9`, `ID3D11Device`, `ID3D12Device`) | `SDL_system.h` (Windows pass) |
| Microsoft GDK (`XTaskQueueHandle`, `XUserHandle`) | `SDL_system.h` (GDK pass) |
| Win32 handles (`HWND`, `HDC`, `HINSTANCE`) | `SDL_syswm.h` |
| Android JNI (`JNIEnv`, `jobject`, pre-erased to `void*` by SDL) | `SDL_system.h` |
| C stdlib (`FILE`, `va_list`) | `SDL_rwops.h`, `SDL_log.h`, `SDL_stdinc.h` |
| Linux X11 (`Display`, `Window`, `XEvent`) | `SDL_syswm.h` |
| Linux Wayland (`wl_display`, `wl_surface`, `wl_egl_window`, `xdg_*`) | `SDL_syswm.h` |
| Linux KMSDRM (`gbm_device`) | `SDL_syswm.h` |
| macOS Cocoa (`NSWindow`) | `SDL_syswm.h` (Apple pass) |
| iOS UIKit (`UIWindow`, `UIViewController`) | `SDL_syswm.h` (iOS pass) |
| Metal (`void *`, already opaque upstream) | `SDL_render.h`, `SDL_metal.h` |
| WinRT (`IInspectable`) | `SDL_syswm.h` (WinRT pass) |
| OpenGL / EGL / GLES (Khronos types) | `SDL_opengl*.h`, `SDL_egl.h` |
| DirectFB / Mir / Vivante / OS/2 | `SDL_syswm.h` (Stage 1 exclusions per §"Platform Views") |

The §"Function Surface" accepted deferrals cover the C variadic / file-pointer surface independently of this foreign-type allow-list. When future slices activate additional platform passes, the corresponding deferred categories transition to active and earn their own per-header configuration entries. The allow-list is grown deliberately; no auto-detection sweep silently expands it.

## Structs And Unions

Structs and unions are public only when the emitted layout is honest.

Rules:

- POD structs emit `[StructLayout(LayoutKind.Sequential)]` when every field is translated correctly.
- Unions emit `[StructLayout(LayoutKind.Explicit)]` with verified `FieldOffset` and size.
- Anonymous nested unions are translated from AST shape, not from name allowlists.
- Fixed primitive arrays use C# fixed buffers.
- Fixed arrays of non-fixed-buffer-compatible elements use deterministic generated wrapper structs, not `[InlineArray]`, so output remains safe across the supported TFM matrix.
- Platform-conditioned structs require per-platform size/offset proof or conservative layout that is correct for every emitted view.
- Do not under-size public struct storage to make compile-check pass.

`SDL_RWops` contract:

- `SDL_RWops` is public SDL header surface, but its `hidden` union is platform-conditioned.
- Stage 1 keeps `SDL_RWops` opaque/quarantined rather than exposing a false full layout. The quarantine emits as a typed handle struct following the public Opaque Handles shape above (`readonly partial struct SDL_RWops(nint value)`), referenced by value in raw ABI signatures rather than as `SDL_RWops*` pointer.
- Full typed layout requires later platform-specific size/offset proof.

`SDL_syswm.h` contract:

- Full typed `SDL_SysWMinfo` / `SDL_SysWMmsg` union layout remains Stage 2.
- Stage 1 may keep those declarations deferred or opaque as documented. Stage 1 quarantine emits both as typed handle structs following the public Opaque Handles shape, referenced by value in raw ABI signatures.

Function-pointer fields:

- Raw struct fields may use `nint` for function-pointer slots to preserve blittability across old TFMs.
- Separate typed callback declarations preserve callback identity.
- Friendly helper APIs may later bridge managed delegates to function pointers with explicit lifetime rules.

## Enums

Enum translation must preserve the native concept and managed ergonomics.

Rules:

- Use explicit C# underlying types.
- C enums default to `int` unless header evidence or existing strategy proves a different storage concept.
- SDL2 `SDL_bool` is int-backed.
- Keep alias/composed enum values when they are part of public SDL source compatibility.

`[Flags]` auto-decoration policy:

- A postprocess step adds `[Flags]` to an enum iff **(a)** the enum's name ends with the `Flags` suffix (case-sensitive — catches naming conventions across Core and satellites: `SDL_RendererFlags`, `IMG_InitFlags`, `MIX_InitFlags`, `TTF_FontStyleFlags`, etc.), **or (b)** the enum's name appears in the family-keyed allow-list within `family-config.json`. The allow-list follows the same family-keyed schema discipline as the opaque-handle roster, audited per SDL2/satellite release.
- **Heuristics over bit values alone are rejected.** Enums whose values happen to be powers of two are not automatically decorated — `SDL_bool` (`SDL_FALSE = 0`, `SDL_TRUE = 1`) would otherwise false-positive and break the int-backed bool contract above.
- Composed alias values (`KMOD_CTRL = KMOD_LCTRL | KMOD_RCTRL`) are preserved as enum members; the bitmask semantics flow from the allow-list entry, not from value analysis. The auto-decoration policy can decorate `SDL_Keymod` because the family's allow-list lists it, not because the postprocess parses the OR expression.

Known Stage 1 flag enum data per family (suffix-decorated and allow-list-decorated names): see [`binding-generator-implementation-notes.md`](binding-generator-implementation-notes.md) §"Known Flag Enums Data".

## Constants And Macros

Macro translation is source-first and conservative.

Rules:

- Source-visible object-like `SDL_*` macros from public owned headers enter the macro pipeline.
- `binding_generation.required_constants` is a manual seed/include path, not the primary source for normal header macros.
- Literal numeric/string/character macros may emit when the managed type and value are deterministic.
- Deterministic object-like integer expressions may emit only when the evaluator can prove the value from safe syntax.
- Function-like public SDL macros are Layer 2 / friendly companion-helper candidates. They are not Layer 1 raw ABI declarations and must not be emitted as constants.
- Approved function-like macro helpers are manually authored or generated from an explicit companion-helper policy. ClangSharp does not emit them.
- Unknown function-like macros are reported and skipped. Accepted macro names must stay visible in generation reports so the public-surface gap is reviewable rather than silently swallowed.
- C-only, build-time, compiler, include-guard, printf annotation, format, assertion, revision, cast-helper, and platform-control macros are skipped with explicit reasons.
- String-like SDL macro keys emit as canonical `ReadOnlySpan<byte>` UTF-8 literal properties. Do not duplicate every key as both `const string` and UTF-8 span.
- Runtime-sized or runtime-dependent macros must not emit as fake constants.

Current macro helper-candidate lane: `SDL_BUTTON`, `SDL_VERSION`, `SDL_VERSIONNUM`, `SDL_VERSION_ATLEAST`, `SDL_WINDOWPOS_*`, `SDL_DEFINE_PIXEL*`, `SDL_PIXELTYPE`, `SDL_PIXELORDER`, `SDL_PIXELLAYOUT`, `SDL_BITSPERPIXEL`, `SDL_BYTESPERPIXEL`, `SDL_ISPIXELFORMAT_*`.

Version macro note: SDL2 `SDL_VERSIONNUM(X,Y,Z)` is `X * 1000 + Y * 100 + Z`. For SDL2 2.32.10, `SDL_COMPILEDVERSION` is `5210`, not `2032010`.

## Platform Views

Platform parsing uses controlled preprocessor views.

Rules:

- Platform views are parser metadata and file attribution, not public class identity.
- One family has one public class and one internal raw ABI class split across partial files.
- Preprocessor-macro switching is the Stage 1 approach; no parse-view-specific public classes.
- DirectFB, Vivante, MIR, and OS/2 remain Stage 1 exclusions unless explicitly reopened.
- Platform-specific functions receive platform attributes and are validated against the view that exposed them.
- Neutral functions are emitted once; platform views emit only platform-only functions after deduplication.
- Generation fails rather than guessing if the same function appears in multiple views with incompatible signatures.

Current SDL2.Core views: Neutral, WindowsDesktop, WinRT, GDK, Linux, MacOS, IOS, Android.

## Evidence Gates

No generated preview should be promoted toward production source unless these gates pass or have documented deferrals:

- Generated preview compiles across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, and `net462`.
- No public raw ABI class or effectively public raw extern leak. Lexically public generated members inside an internal raw container are acceptable because the containing type blocks public API exposure.
- Function-name set matches dynapi/export evidence after accepted exclusions.
- High-risk type translations have fixture coverage: C `long`, `wchar_t`, SDL2 `SDL_bool`, `size_t`, callbacks, and pointer types.
- Public struct layouts have size/offset proof where layout is platform-conditioned.
- Macro report explains emitted, skipped, unsupported, helper-candidate, overridden, and stale-tolerant entries.
- Oracle validation records any accepted deltas and remaining blockers.
- Package-consumer smoke calls representative generated APIs against packaged natives before public release.

## Maintenance Rule

When a generator rule changes, update this constitution in the same change unless the change is purely mechanical. If code and this document disagree, inspect the code and pinned headers, fix the docs or code, and add tests for the disputed behavior.
