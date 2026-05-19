# Binding Translation Contract

> **Status (2026-05-19):** Canonical translation contract for generated SDL bindings. This document is the source of truth for how pinned SDL public headers become generated C# declarations. Older specs, implementation plans, research notes, and oracle reports are evidence only when they agree with this contract.

## Purpose

The generator must answer two different questions without mixing them:

1. Which SDL declarations belong in the binding surface?
2. How does each C declaration translate into safe, ABI-correct C#?

The current SDL2.Core generated preview is close on function-name coverage: dynapi/export review found no unexpected generated SDL2.Core functions, and the missing dynapi names are known deferrals such as `FILE`, `va_list`, `SDL_main`, and `SDL_DYNAPI_entry`.

The remaining Stage 1 risk is translation correctness: a function can have the right name and still be wrong if its C scalar width, pointer semantics, struct layout, enum backing type, or macro classification is wrong. This document is the constitution for those translation choices.

## Authority Order

Use this hierarchy when sources disagree:

1. Pinned SDL public headers from the exact vcpkg version.
2. Actual packaged native binary exports.
3. SDL dynapi manifests for function-name coverage only.
4. This translation contract.
5. `docs/binding-autogen/binding-api-surface-strategy.md` for public layer shape.
6. Oracle review reports and peer bindings as evidence.
7. Historical specs, plans, and research notes.

Historical docs that describe `long -> nint` as a cross-platform fix are superseded. That mapping is false for Windows LLP64.

## Layer Contract

Generated bindings use three layers:

1. Internal raw ABI externs: generated, unsafe, exact ABI, `internal`, and the only layer with `[DllImport]` or `[LibraryImport]`.
2. Public typed low-level API: generated typed handles, enums, structs, callbacks, and thin callable methods that do not expose extern declarations.
3. Public friendly overloads: generated convenience overloads for strings, UTF-8 spans, out/ref/span patterns, and managed ergonomics.

Rules:

- Public raw `IntPtr` externs are not part of v1 preview.
- Raw ABI mistakes must stay internal, but internal does not mean sloppy. Internal raw signatures still have to be ABI-correct before production flip.
- A declaration that cannot be represented safely is deferred with evidence. Do not emit a success-shaped lie.
- Typed handles expose native pointer values through `nint`; that does not make `nint` the answer for every C scalar.

## Function Surface

Function inclusion uses pinned public headers plus dynapi/export validation.

- A generated SDL2.Core function must trace to a pinned public SDL2.Core header declaration.
- A generated SDL2.Core function name must not be absent from dynapi/export evidence unless there is an explicit configured or documented reason.
- Dynapi validates names only. It does not prove signatures, layouts, typedef widths, or ownership semantics.
- Platform-specific declarations stay in the family raw ABI class and receive platform attribution. Parse-view names do not become public class names.
- `SDL_main`, `SDL_DYNAPI_entry`, and startup/dynapi internals are not ordinary public binding functions.

Current accepted deferrals:

- `FILE`, `_IO_FILE`, `va_list`, and `__va_list_tag` APIs are deferred unless a portable mapping is deliberately designed.
- `SDL_RWFromFP`, `SDL_LogMessageV`, `SDL_vsnprintf`, `SDL_vsscanf`, and `SDL_vasprintf` are Stage 1 accepted deferrals.

## Variadic Functions

C ellipsis functions are not exactly representable in portable C# P/Invoke.

The Stage 1 policy is deliberate fmt-only mapping:

- Non-`va_list` SDL variadic functions may be emitted internally only as fixed-prefix, fmt-only calls when the public/friendly API makes the preformatted-string contract explicit.
- These imports must not be documented or reported as exact C varargs ABI declarations.
- `__arglist` is rejected for this repo because it does not compose with the planned span/string overload tiers and is awkward across old TFMs.
- `va_list` variants remain deferred.

Examples:

- `SDL_Log(byte* fmt)` means “log this already formatted byte string,” not “forward arbitrary C varargs.”
- `SDL_snprintf(byte* text, nuint maxlen, byte* fmt)` and `SDL_sscanf(byte* text, byte* fmt)` require explicit policy evidence before they are promoted beyond internal raw usage.

Required evidence:

- parse report should distinguish fmt-only mapped variadics from normal exact functions;
- friendly wrappers should make formatting behavior visible;
- tests should prevent accidental treatment of `...` as ordinary dropped parameters.

## Scalar Type Translation

Exact-width SDL typedefs map by width:

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

C `long` is platform-sensitive, not pointer-sized.

Facts:

- Windows LLP64: `long` and `unsigned long` are 32-bit on x86, x64, and arm64.
- Unix LP64 target RIDs in this repo: `long` and `unsigned long` are 64-bit on x64 and arm64.
- `nint`/`nuint` are wrong for Windows x64/arm64 C `long` because they become 64-bit where C `long` is 32-bit.
- .NET `System.Runtime.InteropServices.CLong` and `CULong` model this correctly on modern TFMs, but they are not available to all current target frameworks.

Contract:

- Do not map C `long` or `unsigned long` directly to `nint` / `nuint` in shared generated signatures.
- Internal raw ABI must use exact platform signatures. For downlevel TFMs, use platform-specific internal extern variants when BCL `CLong`/`CULong` is unavailable.
- Public typed wrappers should normalize C `long` values to stable managed shapes such as `long` / `ulong`, with Windows range checks for parameters when needed.
- Typedefs over C `long`, such as `SDL_threadID`, inherit this policy unless a stronger SDL semantic type is introduced.

High-risk SDL2.Core symbols:

- `SDL_lround`, `SDL_lroundf`
- `SDL_ltoa`, `SDL_ultoa`
- `SDL_strtol`, `SDL_strtoul`
- `SDL_threadID`, `SDL_ThreadID`, `SDL_GetThreadID`

### `SDL_bool`

SDL2 and SDL3 bool-like values are separate policies.

| Family | Native shape | Raw wire type | Friendly shape |
| --- | --- | --- | --- |
| SDL2 | `typedef enum { SDL_FALSE = 0, SDL_TRUE = 1 } SDL_bool` or `typedef int SDL_bool` fallback | `int` | `bool` conversion uses `!= 0` |
| SDL3 | C bool-like 1-byte value | `byte` or byte-backed wrapper | `bool` conversion |

Contract:

- SDL2 `SDL_bool` public enum must be int-backed.
- Mapping SDL2 `SDL_bool` to `byte` is ABI-wrong.
- Mapping SDL2 `SDL_bool` to `uint` preserves width but violates signed C enum/int semantics and this contract.

### `wchar_t`

`wchar_t` is platform-sensitive.

Facts:

- Windows `wchar_t` is 2 bytes.
- Unix-like target RIDs generally use 4-byte `wchar_t`.
- C# `char*` is always 2-byte UTF-16 code units.
- C# `int*` is always 4-byte elements.

Contract:

- Do not map `wchar_t*` to `int*` or `char*` in shared cross-platform generated public structs or signatures.
- Low-level raw shape should use an opaque pointer representation (`nint`, `void*`, or equivalent by layer) unless a platform-specific helper is generated.
- Friendly APIs may decode wide strings only through platform-aware helpers with tests.

High-risk SDL2.Core symbols:

- `SDL_hid_device_info.serial_number`
- `SDL_hid_device_info.manufacturer_string`
- `SDL_hid_device_info.product_string`
- `SDL_hid_open`
- `SDL_hid_get_manufacturer_string`
- `SDL_hid_get_product_string`
- `SDL_hid_get_serial_number_string`
- `SDL_hid_get_indexed_string`
- `SDL_wcs*` functions

## Opaque Handles

SDL-owned opaque handles are public readonly value types wrapping `nint`.

Contract:

- Emit one public handle type per SDL public typedef concept.
- Prefer the public typedef name over the underlying C struct tag.
- Do not emit duplicate public handles for `typedef struct tag Name;` or `typedef struct tag* Name;` patterns.
- The underlying C struct tag is parser evidence, not automatically public .NET API.

Known P0 leaks:

- `SDL_hid_device_` must not be emitted alongside canonical `SDL_hid_device`.
- `SDL_semaphore` must not be emitted alongside canonical `SDL_sem`.

## Structs And Unions

Structs and unions are public only when the emitted layout is honest.

Contract:

- POD structs emit `[StructLayout(LayoutKind.Sequential)]` when every field is translated correctly.
- Unions emit `[StructLayout(LayoutKind.Explicit)]` with verified `FieldOffset` and size.
- Anonymous nested unions are translated from AST shape, not from name allowlists.
- Fixed primitive arrays use C# fixed buffers.
- Fixed arrays of non-fixed-buffer-compatible elements use deterministic generated wrapper structs.
- Platform-conditioned structs require per-platform size/offset proof or conservative layout that is correct for every emitted view.
- Do not under-size public struct storage to make compile-check pass.

`SDL_RWops` contract:

- `SDL_RWops` is public SDL header surface, but its `hidden` union is platform-conditioned.
- The generated `SDL_RWops_hidden` must include enough storage for the Windows/GDK `windowsio` arm or otherwise avoid exposing a false full layout.
- Current `Size = 24` output is wrong on Windows x64 because `windowsio` contains `SDL_bool append`, `void* h`, and nested pointer/`size_t` fields.
- Add layout fixtures for Windows and Unix views before treating `SDL_RWops` as ABI-ready.

`SDL_syswm.h` contract:

- Full typed `SDL_SysWMinfo` / `SDL_SysWMmsg` union layout remains Stage 2.
- Stage 1 may keep those declarations deferred or opaque as documented.

Function-pointer fields:

- Raw struct fields may use `IntPtr`/`nint` for function-pointer slots to preserve blittability across old TFMs.
- Separate typed callback declarations preserve callback identity.
- Friendly helper APIs may later bridge managed delegates to function pointers with explicit lifetime rules.

## Enums

Enum translation must preserve the native concept and managed ergonomics.

Contract:

- Use explicit C# underlying types.
- C enums default to `int` unless header evidence or existing strategy proves a different storage concept.
- `SDL_bool` is int-backed for SDL2.
- Add `[Flags]` when header comments, composed aliases, bit values, or API docs prove bitmask semantics.
- Keep alias/composed enum values when they are part of public SDL source compatibility.
- Do not invent `[Flags]` solely because values are powers of two if the SDL concept is not a bitmask.

Known Stage 1 fixes:

- `SDL_Keymod` should be `[Flags]`.
- `SDL_GLcontextFlag` should be `[Flags]`.
- `SDL_RendererFlip` should be reviewed as a flag-like enum because horizontal and vertical can be combined.

## Constants And Macros

Macro translation is source-first and conservative.

Contract:

- Source-visible object-like `SDL_*` macros from public owned headers enter the macro pipeline.
- `binding_generation.required_constants` is a manual seed/include path, not the primary source for normal header macros.
- Literal numeric/string/character macros may emit when the managed type and value are deterministic.
- Deterministic object-like integer expressions may emit only when the evaluator can prove the value from safe syntax.
- Function-like public macros are helper candidates, not constants.
- Unknown function-like macros are reported and skipped.
- C-only, build-time, compiler, include-guard, printf annotation, format, assertion, revision, cast-helper, and platform-control macros are skipped with explicit reasons.
- String-like SDL macro keys emit as canonical `ReadOnlySpan<byte>` UTF-8 literal properties. Do not duplicate every key as both `const string` and UTF-8 span.
- Runtime-sized or runtime-dependent macros must not emit as fake constants.

Known P0 leak:

- `SDL_WINAPI_FAMILY_PHONE` is a platform-control macro and must not be public API.

Known follow-up:

- `SDL_METALVIEW_TAG` should be resolved with Stage 2 `SDL_syswm.h` work unless a clear consumer-facing reason is documented earlier.

Version macro note:

- SDL2 `SDL_VERSIONNUM(X,Y,Z)` is `X * 1000 + Y * 100 + Z`.
- For SDL2 2.32.10, `SDL_COMPILEDVERSION` is `5210`, not `2032010`.

## Platform Views

Platform parsing uses controlled preprocessor views.

Contract:

- Platform views are parser metadata and file attribution, not public class identity.
- One family has one public class and one internal raw ABI class split across partial files.
- Preprocessor-macro switching is the Stage 1 approach; no parse-view-specific public classes.
- DirectFB, Vivante, MIR, and OS/2 remain Stage 1 exclusions unless explicitly reopened.
- Platform-specific functions receive platform attributes and are validated against the view that exposed them.

## Evidence And Gates

No generated preview should be promoted toward production source unless these gates pass or have documented deferrals:

- Generated preview compiles across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, and `net462`.
- No public raw externs or public raw ABI class leak.
- Function-name set matches dynapi/export evidence after accepted exclusions.
- High-risk type translations have fixture coverage: C `long`, `wchar_t`, `SDL_bool`, `size_t`, callbacks, and pointer types.
- Public struct layouts have size/offset proof where layout is platform-conditioned.
- Macro report explains emitted, skipped, unsupported, helper-candidate, overridden, and stale-tolerant entries.
- Oracle validation review records any accepted deltas and current P0 blockers.

## Current P0 Blockers

The 2026-05-19 P0 translation blockers were addressed by fixture-backed generator tests covering opaque handle aliases, platform-sensitive C integers, `wchar_t*`, erased HID wide-string pointers, `SDL_RWops`, platform-control macros, SDL2 `SDL_bool`, and known bitmask enums.

Resolved or intentionally quarantined categories:

1. Duplicate public opaque handles now canonicalize to public typedef names such as `SDL_hid_device` and `SDL_sem`.
2. C `long` / `unsigned long` raw command imports and callback delegates now use `CLong` / `CULong` and are emitted only for `NET6_0_OR_GREATER`; downlevel TFMs intentionally avoid false raw signatures.
3. `SDL_RWops` is Stage 1 opaque/quarantined rather than a public full-layout struct; full typed layout requires later platform-specific size/offset proof.
4. `wchar_t*` and CppAst-erased HID wide-string pointers now map to opaque `nint` storage rather than false `int*` / `char*` signatures.
5. `SDL_WINAPI_FAMILY_PHONE` is classified as a platform-control macro and is not emitted as a public constant.
6. SDL2 `SDL_bool` is int-backed.
7. Known bitmask enums such as `SDL_Keymod`, `SDL_GLcontextFlag`, and `SDL_RendererFlip` emit with `[Flags]`.

Variadic fmt-only imports are not a P0 ABI blocker when clearly documented and reported as mapped variadics, but they remain a policy/reporting cleanup item before production flip.

## Maintenance Rule

When a generator rule changes, update this contract in the same change unless the change is purely mechanical. If a historical spec or plan disagrees with this contract, add a supersession note rather than reviving the old rule.
