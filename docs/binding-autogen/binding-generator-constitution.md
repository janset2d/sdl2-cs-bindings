# Binding Generator Constitution

> **Status (2026-05-20):** Canonical binding-generator constitution. This document is the source of truth for generated SDL binding surface decisions: internal ABI, public API layering, C-to-C# translation, manifest responsibilities, and evidence gates. Code and pinned SDL public headers remain the north star; update this document in the same change when a generator rule changes.

## Purpose

The generator must answer two questions without mixing them:

1. Which SDL declarations belong in a generated binding family?
2. How does each native declaration become ABI-correct, stable C#?

The current SDL2.Core generator has crossed the spike boundary. It has a Cake-hosted `GenerateBindings` target, manifest-driven family configuration, semantic type classification, source-first macro collection, generated handles/enums/structs/callbacks/constants, internal raw ABI command emission, dynapi name validation, and fixture-backed tests for the ABI-sensitive SDL2.Core blockers found on 2026-05-19.

The remaining Stage 1 work is public surface completion and production flip, not re-proving the internal ABI constitution from scratch.

## Authority Order

Use this document for intended binding-generator policy, but verify current
behavior against the implementation and tests before making behavior claims. When
sources disagree, use this order:

1. Pinned SDL public headers from the exact vcpkg version.
2. Actual packaged native binary exports.
3. `build/_build/Targets/GenerateBindings/` implementation and tests for current behavior.
4. SDL dynapi manifests for function-name coverage only.
5. This constitution for intended policy.
6. `docs/binding-autogen/binding-generator-roadmap.md` for future work sequencing.
7. ADR-004 for the CppAst toolchain decision.
8. Peer bindings and old research as evidence only.

Historical plans, spike reports, and superpowers specs are not policy. If they disagree with this document, this document wins. If the implementation disagrees with this document, treat it as code/docs drift to investigate, not as permission to ignore either source.

## Layer Contract

Generated bindings use three layers:

1. **Internal raw ABI layer**: generated, unsafe where needed, exact ABI, `internal`, and the only layer with `[DllImport]` or `[LibraryImport]`.
2. **Public typed low-level layer**: generated handles, enums, structs, callbacks, constants, and thin public methods that call the internal raw layer without exposing extern declarations.
3. **Public friendly overload layer**: generated conveniences for `string`, `ReadOnlySpan<byte>`, `Span<T>`, `ReadOnlySpan<T>`, `out`, `ref`, bool conversions, and explicit preformatted variadic helpers.

Rules:

- Public raw `IntPtr` externs are not part of v1 preview.
- SDL2-CS compatibility is best-effort. It is an oracle, not the API target.
- Raw ABI mistakes are still bugs even though the raw layer is internal.
- A declaration that cannot be represented honestly is deferred with evidence. Do not emit a success-shaped lie.
- Typed handles expose native pointer values through `nint`; that does not make `nint` the answer for every native scalar.
- Raw ABI backends may split into `DllImport` and `LibraryImport` generated files, but both consume the same semantic/projection truth. File-level TFM guards are preferred over per-function conditional sprawl.
- Compatibility packages such as `System.Memory` are acceptable for public/friendly APIs on `netstandard2.0` and `net462` when package smoke proves the consumer contract. They must not be used to fake ABI primitives whose platform shape is not portable.

## Generator Home

The generator is build infrastructure, not a standalone product project.

- Home: `build/_build/Targets/GenerateBindings/`.
- Tests: `build/_build.Tests/Unit/Targets/GenerateBindings/` plus fixture headers under `build/_build.Tests/Fixtures/Data/GenerateBindings/`.
- Cross-cutting validators: `build/_build/Validation/BindingGeneration/`.
- Persisted binding-generation data contracts: `build/_build/Data/BindingGeneration/`.
- Local invocation: `dotnet run --file tools.cs -- generate-bindings`, which runs generation inside the pinned Linux builder container.

The generator is Linux-canonical. It runs inside the pinned Linux container using CppAst/libclang. Non-Linux host execution fails closed; Windows and macOS development flows use Docker through `tools.cs`.

## Generator Engine And SDL Policy

The generator should separate CppAst-to-ABI mechanics from SDL family policy without pretending to be a general binding-generator product.

Rules:

- The core pipeline is a CppAst ABI engine for parsed declarations, native type classification, platform parse-view merge, raw ABI projection, and deterministic file-set emission.
- SDL-specific decisions live behind named policy/profile concepts: owned prefixes, core-owned type references, SDL2 versus SDL3 bool shape, known opaque structs, string-like macro handling, SysWM layout, and satellite-to-core reference rules.
- Keep those concepts target-local under `Targets/GenerateBindings/` until a second real generator target exists. Do not promote them to root `Shared` or standalone `src/` projects for aesthetic symmetry.
- M2 may expose an `SdlPolicy` seam while preserving SDL2.Core output. M3 owns the real profile/config boundary for SDL2 core, SDL2 satellites, SDL3 core, and SDL3 satellites.
- Generic model-building or emission code must not accumulate ad hoc `/SDL2/`, `SDL_`, `SDL2`, or library-name checks once a named policy/profile seam exists. Add a policy collaborator instead.

## Manifest Configuration Vs Code-Owned Policy

`build/manifest.json library_manifests[].binding_generation` is per-family configuration. It is not a hidden policy language.

Manifest owns reviewable facts that vary by family:

- `enabled`
- `managed_namespace`
- `primary_class_name`
- `platform_catalog`
- `owned_prefixes`
- `parse_defines`
- `clang_args`
- `header_set`
- `excluded_functions`
- `required_functions`
- `required_constants`
- `macro_constants.excluded`
- `macro_constants.overrides`
- `deferred_declarations`
- `validators`
- `dynapi`

Future profile work may promote additional family identity facts, such as raw ABI class name and native import library name, into explicit config. Until that schema exists, treat them as code-owned/profile-bound seams rather than current manifest facts.

Generator code owns ABI/API policy:

- Scalar width mapping.
- SDL2 vs SDL3 bool wire shape.
- Opaque-handle detection.
- Pointer, array, callback, function-pointer, and userdata classification.
- UTF-8 string and span overload behavior.
- C variadic handling.
- Struct/union layout policy.
- Macro taxonomy and safe expression evaluation.
- Platform parse-view merge and attribution rules.
- Public API layering.

Exception rule:

- Manifest exceptions must name the declaration, category, source/reason, and rationale.
- Silent JSON knobs that change ABI behavior are forbidden.
- Move behavior into the manifest only when it genuinely varies by family or needs an explicit per-family override.
- If every family must obey the same rule, keep it in code and tests.

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
- A generated SDL2.Core function name must match dynapi/export evidence unless explicitly excluded or deferred.
- Dynapi validates names only. It does not prove parameter order, scalar width, struct layout, enum backing type, or ownership semantics.
- Platform-specific declarations stay in the family raw ABI class and receive platform attribution.
- `SDL_main`, `SDL_DYNAPI_entry`, startup glue, and dynapi internals are not ordinary public binding functions.

Current accepted deferrals:

- `FILE`, `_IO_FILE`, `va_list`, and `__va_list_tag` APIs are deferred unless a portable mapping is deliberately designed.
- `SDL_RWFromFP`, `SDL_LogMessageV`, `SDL_vsnprintf`, `SDL_vsscanf`, and `SDL_vasprintf` are Stage 1 accepted deferrals.

## C Variadics

C ellipsis functions are not exactly representable in portable C# P/Invoke.

Stage 1 policy is deliberate fmt-only mapping:

- Non-`va_list` SDL variadic functions may be emitted internally only as fixed-prefix, fmt-only calls.
- The public/friendly API must make the preformatted-string contract explicit.
- These imports must not be documented or reported as exact C varargs ABI declarations.
- `__arglist` is rejected for this repo because it does not compose with the planned string/span overload tiers and old TFM support.
- `va_list` variants remain deferred.

Examples:

- `SDL_Log(byte* fmt)` means "log this already formatted UTF-8 string," not "forward arbitrary C varargs."
- `SDL_snprintf(byte* text, nuint maxlen, byte* fmt)` and `SDL_sscanf(byte* text, byte* fmt)` require explicit public wrapper policy before promotion beyond internal raw usage.

Required evidence:

- parse/report output distinguishes fmt-only mapped variadics from exact functions;
- friendly wrappers make formatting behavior visible;
- tests prevent accidental treatment of `...` as ordinary dropped parameters.

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

C `long` is platform-sensitive, not pointer-sized.

Facts:

- Windows LLP64: `long` and `unsigned long` are 32-bit on x86, x64, and arm64.
- Unix LP64 target RIDs in this repo: `long` and `unsigned long` are 64-bit on x64 and arm64.
- `nint`/`nuint` are wrong for Windows x64/arm64 C `long` because they become 64-bit where C `long` is 32-bit.
- `System.Runtime.InteropServices.CLong` and `CULong` model this correctly on modern TFMs but are unavailable to all current target frameworks.

Contract:

- Do not map C `long` or `unsigned long` directly to `nint` / `nuint` in shared generated signatures.
- Internal raw ABI uses `CLong` / `CULong` where available and guards these members to `NET6_0_OR_GREATER` until a downlevel exact strategy exists.
- Public typed wrappers may normalize values to stable managed shapes such as `long` / `ulong`, with Windows range checks for input parameters when needed.
- Typedefs over C `long`, such as `SDL_threadID`, inherit this policy unless a stronger SDL semantic type is introduced.
- Do not introduce a casual downlevel `CLong` / `CULong` NuGet polyfill. A same-named portable struct backed by `IntPtr`, `int`, or `long` would be wrong for at least one of Windows LLP64 or Unix LP64. Any downlevel strategy must prove exact per-platform ABI shape before removing guards.

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
- SDL3 bool policy must not be copied from SDL2.

### `wchar_t`

`wchar_t` is platform-sensitive.

Facts:

- Windows `wchar_t` is 2 bytes.
- Unix-like target RIDs generally use 4-byte `wchar_t`.
- C# `char*` is always 2-byte UTF-16 code units.
- C# `int*` is always 4-byte elements.

Contract:

- Do not map `wchar_t*` to `int*` or `char*` in shared cross-platform generated public structs or signatures.
- Low-level raw shape uses opaque pointer representation unless a platform-specific helper is generated.
- Friendly APIs may decode wide strings only through platform-aware helpers with tests.

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
- Stage 1 keeps `SDL_RWops` opaque/quarantined rather than exposing a false full layout.
- Full typed layout requires later platform-specific size/offset proof.

`SDL_syswm.h` contract:

- Full typed `SDL_SysWMinfo` / `SDL_SysWMmsg` union layout remains Stage 2.
- Stage 1 may keep those declarations deferred or opaque as documented.

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
- Add `[Flags]` when header comments, composed aliases, bit values, or API docs prove bitmask semantics.
- Keep alias/composed enum values when they are part of public SDL source compatibility.
- Do not invent `[Flags]` solely because values are powers of two if the SDL concept is not a bitmask.

Known Stage 1 flags include `SDL_Keymod`, `SDL_GLcontextFlag`, and `SDL_RendererFlip`.

## Constants And Macros

Macro translation is source-first and conservative.

Rules:

- Source-visible object-like `SDL_*` macros from public owned headers enter the macro pipeline.
- `binding_generation.required_constants` is a manual seed/include path, not the primary source for normal header macros.
- Literal numeric/string/character macros may emit when the managed type and value are deterministic.
- Deterministic object-like integer expressions may emit only when the evaluator can prove the value from safe syntax.
- Function-like public macros are helper candidates, not constants.
- Unknown function-like macros are reported and skipped.
- C-only, build-time, compiler, include-guard, printf annotation, format, assertion, revision, cast-helper, and platform-control macros are skipped with explicit reasons.
- String-like SDL macro keys emit as canonical `ReadOnlySpan<byte>` UTF-8 literal properties. Do not duplicate every key as both `const string` and UTF-8 span.
- Runtime-sized or runtime-dependent macros must not emit as fake constants.

Current macro helper-candidate lane:

- `SDL_BUTTON`
- `SDL_VERSION`
- `SDL_VERSIONNUM`
- `SDL_VERSION_ATLEAST`
- `SDL_WINDOWPOS_*` helpers
- `SDL_DEFINE_PIXEL*` helpers
- `SDL_PIXELTYPE`, `SDL_PIXELORDER`, `SDL_PIXELLAYOUT`
- `SDL_BITSPERPIXEL`, `SDL_BYTESPERPIXEL`
- `SDL_ISPIXELFORMAT_*`

Version macro note:

- SDL2 `SDL_VERSIONNUM(X,Y,Z)` is `X * 1000 + Y * 100 + Z`.
- For SDL2 2.32.10, `SDL_COMPILEDVERSION` is `5210`, not `2032010`.

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

Current SDL2.Core views:

- Neutral
- WindowsDesktop
- WinRT
- GDK
- Linux
- MacOS
- IOS
- Android

## Evidence Gates

No generated preview should be promoted toward production source unless these gates pass or have documented deferrals:

- Generated preview compiles across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, and `net462`.
- No public raw externs or public raw ABI class leak.
- Function-name set matches dynapi/export evidence after accepted exclusions.
- High-risk type translations have fixture coverage: C `long`, `wchar_t`, SDL2 `SDL_bool`, `size_t`, callbacks, and pointer types.
- Public struct layouts have size/offset proof where layout is platform-conditioned.
- Macro report explains emitted, skipped, unsupported, helper-candidate, overridden, and stale-tolerant entries.
- Oracle validation records any accepted deltas and remaining blockers.
- Package-consumer smoke calls representative generated APIs against packaged natives before public release.

## Current SDL2.Core ABI Status

The 2026-05-19 P0 translation blockers were addressed with fixture-backed generator tests.

Resolved or intentionally quarantined categories:

1. Duplicate public opaque handles now canonicalize to public typedef names such as `SDL_hid_device` and `SDL_sem`.
2. C `long` / `unsigned long` raw command imports and callback delegates now use `CLong` / `CULong` and are emitted only for `NET6_0_OR_GREATER`; downlevel TFMs avoid false raw signatures.
3. `SDL_RWops` is Stage 1 opaque/quarantined rather than a public full-layout struct.
4. `wchar_t*` and CppAst-erased HID wide-string pointers map to opaque `nint` storage rather than false `int*` / `char*` signatures.
5. `SDL_WINAPI_FAMILY_PHONE` is classified as a platform-control macro and is not emitted as public API.
6. SDL2 `SDL_bool` is int-backed.
7. Known bitmask enums such as `SDL_Keymod`, `SDL_GLcontextFlag`, and `SDL_RendererFlip` emit with `[Flags]`.

Variadic fmt-only imports are not a P0 ABI blocker when clearly documented and reported as mapped variadics, but they remain a policy/reporting cleanup item before production flip.

## Maintenance Rule

When a generator rule changes, update this constitution in the same change unless the change is purely mechanical. If code and this document disagree, inspect the code and pinned headers, fix the docs or code, and add tests for the disputed behavior.
