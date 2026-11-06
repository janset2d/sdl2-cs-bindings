# Binding ABI and API Policy

Date: 2026-11-06

This document owns native-to-managed translation and public API shape.
[Generation](binding-generation.md) owns input authority, determinism, configuration and promotion
evidence. Change policy and its tests together; investigate code/policy drift through
[workflow](../engineering/workflow.md#deviation), not an undocumented exception.

## Binding tier and friendly tier

Each family has one public `static unsafe partial class`, split across partial files.
[ADR-0006](../adr/0006-one-public-class-per-family.md) records the decision. Typed, C-named public
P/Invokes form the **binding tier**. Ergonomic overloads form the **friendly tier** in the same class.
Use a private extern behind a public managed method only when that member's public shape cannot
be the extern, such as platform-varying C `long`. This is **per-member ABI demotion**, not a second
raw container or a set of uniform forwarding wrappers.

Allocation, changed arity or changed call shape identifies a friendly operation. Type substitution
that preserves arity and wire shape belongs in the binding tier. Pointer-preserving forms remain
available where honest. Before public preview, every public function must also have a safe
alternative; a caller must not be forced into unsafe as the only path.
SDL2-CS compatibility is best-effort evidence, not the API target. Never emit a declaration that
misrepresents the ABI merely to claim coverage.

Public namespace and class identity come from family configuration. Parse views are attribution
metadata, not public class names. Every function has one public signature across TFMs; import
attributes and implementation details may differ. [ADR-0009](../adr/0009-target-frameworks.md)
owns the target-framework policy; read evaluated values from [build properties](../../Directory.Build.props).
Compatibility packages such as System.Memory are acceptable when package smoke proves their use;
they must not fake a platform-sensitive ABI primitive.

## Names and UTF-8 conveniences

1. The managed `string?` form owns the C name. A `byte*` parameter or return form uses `<CName>Ptr`.
2. No two overloads with the same name and arity may both accept the `null` literal at the same position.
3. Generation fails when an emitted name collides with a native symbol. Otherwise preserve C names,
   including gfx's mixed prefixed and unprefixed functions; do not derive names from the class prefix.

Use managed strings for text and `ReadOnlySpan<byte>` for caller-owned UTF-8. Do not introduce a
unifying Utf8String wrapper, UTF-16 span overloads or an Unsafe_ renaming convention.
A byte-span sibling applies only to a parameter that is non-nullable in C. Recognize an existing
terminator; otherwise make a bounded terminated copy without reading past caller-owned memory.
Keep allocation behavior visible. Convert eligible parameters together instead of creating a
cartesian overload set. Optional struct pointers use nullable value-type defaults where appropriate.

Counted input/output buffers may use ReadOnlySpan/Span only when bounds and retention semantics
are proven. Required single-element outputs may use `out`; required in/out elements may use `ref`.
Pointer shape alone never establishes ownership or lifetime. String conveniences do not take
ownership of returned native storage unless the SDL contract explicitly requires it.

## Function inclusion and variadics

Trace functions to pinned public headers, or an explicitly required declaration from an excluded
umbrella header. Core dynapi/export evidence validates function **names**, not signatures or layouts.
Satellites use their public declaration/export evidence: Image/Mixer/Ttf use DECLSPEC; gfx uses
per-header scope macros. Attribute platform-only functions to their exposing view. Startup glue,
SDL_main, SDL_DYNAPI_entry and dynapi internals are not ordinary public functions.

Exclude a BCL-replaceable helper only when all conditions hold: a direct BCL equivalent is at least
as ergonomic; SDL2-CS does not expose it; and no SDL symbol transitively depends on it in pinned
headers. Record the three proofs and BCL equivalent in per-header configuration. Standing examples
are SDL_lround/lroundf/ltoa/ultoa/strtol/strtoul and SDL_iconv_open/close/iconv/iconv_string.
An excluded function does not automatically exclude its typedef; classify any harmless residue.

C ellipsis is not a portable C# P/Invoke contract. A deliberately mapped variadic exposes only its
fixed prefix and an explicit preformatted UTF-8 contract. It is not reported as exact varargs ABI.
Do not use `__arglist`. Keep `FILE`, `_IO_FILE`, `va_list` and `__va_list_tag` APIs deferred unless
portable mapping is deliberately designed. This includes SDL_RWFromFP, SDL_LogMessageV,
SDL_vsnprintf, SDL_vsscanf and SDL_vasprintf. SDL_snprintf/sscanf need explicit wrapper policy;
dropping ellipsis is not by itself a safe public API.

## Scalars

| C concept | Managed wire concept |
|---|---|
| Sint8 / Uint8 | sbyte / byte |
| Sint16 / Uint16 | short / ushort |
| Sint32 / Uint32 | int / uint |
| Sint64 / Uint64 | long / ulong |
| size_t | nuint |
| ptrdiff_t | nint |
| Pointer/userdata | Honest typed pointer, void pointer or nint; never infer a scalar width from pointer size |

### C long and unsigned long

Windows LLP64 uses 32-bit C long on every supported Windows architecture. Unix LP64 targets use
64 bits. nint/nuint are therefore wrong for shared C-long signatures on Windows x64/ARM64.
CLong/CULong model the native width on modern TFMs, but are not available on every compatibility TFM.

Normalize the public member to managed long/ulong where required, behind private exact-width
imports. Range-check Windows input parameters before narrowing. Compatibility dispatch selects
the correct Windows or Unix width at runtime; do not invent a same-named portable CLong polyfill.
Typedefs such as SDL_threadID inherit this policy. Preserve thread identifiers: ManagedThreadId is
a different identifier space, not a substitute. TTF_OpenFontIndex variants require the **input** path;
TTF_FontFaces and Core thread identifiers also exercise returns. Test both from the foundation.

### SDL_bool

[ADR-0007](../adr/0007-sdl-bool-abi-struct.md) requires an ABI-exact struct with implicit conversions
to and from bool. SDL2 storage is int-sized; false is zero and nonzero converts to true.
Use the ABI-exact struct rather than an enum. Apply this meaning only to native SDL_bool, never to an
arbitrary integer return. SDL3's native bool is one byte and needs a separate exact representation.

### wchar_t

Windows wchar_t is two bytes; Unix targets generally use four. Neither char* nor int* is a portable
shared mapping. Use opaque nint for shared wchar_t pointer parameters, returns and fields.
Windows-only APIs retain platform attribution. Friendly decoding must choose UTF-16 on Windows
and UTF-32 on POSIX with tests; it does not repair a falsely typed ABI. The
[generation mechanism](binding-generation.md#wchar_t-and-foreign-types) owns the actual remap path.

## Opaque handles and ownership

An SDL-owned opaque handle is a public readonly partial value type over one nint field, with
sequential layout, Value, IsNull/IsNotNull/Null, DangerousGetHandle, full equality and explicit
conversions to/from nint. No implicit handle conversion weakens its type safety.
Pass a single-pointer handle by value at parameter, return and field positions; retain double
pointers and out positions where required. The one-pointer storage must remain ABI-equivalent.

Prefer the public typedef name over the struct tag: SDL_hid_device, not a second SDL_hid_device_;
SDL_sem, not a second SDL_semaphore. Detect empty structs used as pointers without a Core-prefix
assumption. Force-opaque exceptions have config entries and rationale that change with this policy.
SDL_RWops, SDL_SysWMinfo and SDL_SysWMmsg remain quarantined rather than exposing unproven layouts.

Satellite generation resolves Core handles from configuration, not another family's generated files.
Owner families emit their own handles; consumers reuse them. SDL_Surface and SDL_version are
transparent structs and remain pointers where the C signature is a pointer, not by-value handles.

[ADR-0008](../adr/0008-no-safehandle-in-v1.md) excludes SafeHandle types from the v1 binding.
Ownership and resource lifetime belong to Janset2D; struct handles are the only binding handle form.
Do not infer automatic disposal or a high-level resource wrapper from an opaque pointer.

For modern imports consuming cross-assembly blittable handles, apply DisableRuntimeMarshalling
to each declaring assembly under its modern-TFM guard. The attribute does not flow from a referenced
assembly. Keep wire signatures blittable: UTF-8 pointers rather than managed strings, ABI-exact boolean
storage, pointers or proven unmanaged aggregates. [Generation](binding-generation.md#handles-and-cross-assembly-imports)
owns the support-file mechanism and diagnostic context.

## Foreign types

Foreign Vulkan, Direct3D/GDK/Win32, X11/Wayland/KMSDRM, Cocoa/UIKit/Metal and Khronos types are
not SDL-owned handles. Preserve their opaque pointer-sized boundary rather than wrapping them in
competing Janset handle types. Keep NativeTypeName provenance, with explicit per-header mappings
and suppression of duplicate parser tags. Grow the allow-list deliberately from pinned headers;
no automatic sweep silently activates a foreign category. SDL_GLContext is SDL-owned, unlike the
foreign graphics APIs. C FILE/varargs deferrals are independent of this ownership classification.

SDL_GUID may substitute System.Guid only with 16-byte layout/byte-sequence proof. Test raw bytes
and string conversion separately; equal size alone does not prove semantic formatting equivalence.

## Structs, unions and callbacks

Use sequential layout for honest POD structs and every otherwise layout-free struct, across all
families. Verified unions use explicit layout, field offsets and size. Translate anonymous nesting
from AST shape, not a name allow-list. Fixed primitive arrays use fixed buffers; other fixed arrays
use deterministic wrapper structs, not InlineArray, across the compatibility matrix.
Prove platform-conditioned size and offsets. Never undersize storage to make compilation pass.
Full RWops and SysWM layouts require platform proof before replacing quarantine.

Callback parameters, pointer-to-callback slots, returns and fields use typed
`delegate* unmanaged[Cdecl]<…>` signatures on **every TFM**. One public signature per function is
the contract; only import implementation differs. Callback identity does not prove rooting or
registration lifetime. Managed callback helpers need explicit registration/unregistration, lifetime,
thread and null-to-disable policy. Mixer persistent background callbacks and synchronous
call-scoped callbacks are separate lifetime classes; do not silently unify them.

## Enums, constants and macros

Use explicit enum underlying types, normally int for C enums unless header evidence proves otherwise.
Preserve aliases and composed values. SDL_bool is the struct exception above.
Apply Flags when the name ends with case-sensitive `Flags` or appears in the family's explicit
allow-list. Powers-of-two values alone are not evidence of flags. Preserve KMOD_CTRL-style aliases;
do not invent a flag enum from loose TTF_STYLE constants.

Object-like public macros enter a source-first pipeline. Required constants seed intentionally
unreachable declarations, not ordinary header coverage. Emit literal numeric/string/character
values and only integer expressions the evaluator can prove from safe syntax.
String-like keys such as SDL_HINT_* are ReadOnlySpan<byte> UTF-8 literal properties, not duplicated
string/span catalogs. Runtime-sized or dependent macros never become fake constants.

Function-like macros are helpers, not native imports or constants. A curated symbol/kind roster
selects reusable code-owned emitters: pure evaluable helpers, delegating friendly helpers, and
irregular hand-authored or explicitly skipped cases. Report unknown candidates. ClangSharp's
object-like macro output cannot recover function-like bodies. Preserve per-family version tokens,
double aliases and gfx's loose version constants; avoid duplicate COMPILEDVERSION or M_PI emission.
SDL2 VERSIONNUM uses `X * 1000 + Y * 100 + Z`, not an SDL3 formula.

Skip compiler/build toggles, include guards, assertions, printf annotations, revisions, cast helpers
and platform-control macros with reasons. Endian aliases such as SDL_BYTEORDER, AUDIO_*SYS and
MIX_DEFAULT_FORMAT need runtime-aware aliases, not frozen public constants. Explicit LSB/MSB
constants remain constants. PIXELFORMAT_*32 enum members use the supported little-endian value;
TTF Unicode BOM constants are codepoints, not runtime byte-order aliases.

## Cross-family invariants and platform views

- Empty family work sets are valid no-ops and inject no machinery. Prove satellite-only shapes
  before activation, including TTF C-long inputs and IMG_Animation double-pointer fields.
- Config owns which symbols/families; code owns how each translation works.
- Satellites consume Core types with explicit cross-namespace resolution; never re-own them.
- Resolve cross-family aliases and error redirects through Core's public surface. Core public
  binding support precedes satellite activation. InternalsVisibleTo is for test assemblies only.
- Preserve mixed native names and apply universal layout rules without a Core special case.
- Platform views attribute functions but never create public view-specific classes. Emit neutral
  functions once; subtract duplicates from later views. Fail on incompatible signatures across views.
- DirectFB, Vivante, MIR and OS/2 stay excluded unless explicitly reopened.

[Output review](../engineering/binding-output-review.md) owns multi-oracle interpretation;
[binding testing](../engineering/binding-testing.md) owns fixture and runtime proof.
