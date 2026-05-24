# Research: Semantic ABI Type Classification For SDL Bindings

**Date**: 2026-05-22
**Context**: Targeted fact-check after the ClangSharp SDL2.Core spike A/B slices. This document consolidates multi-agent review findings, official interop references, and current repository evidence around cross-platform ABI type classification.

> This is research evidence, not a spec, roadmap, or implementation plan. The canonical binding-generator policy remains [`docs/binding-autogen/binding-generator-constitution.md`](../binding-autogen/binding-generator-constitution.md), and current implementation truth remains under `build/_build/Targets/GenerateBindings/`.

## Executive Summary

The reviews converged on one central point: the next correctness risk is not simply "fix three structs." The real boundary is semantic ABI type classification: deciding whether a native declaration is an opaque handle, concrete layout, deferred layout, platform-sensitive scalar, platform-sensitive wide-character pointer, or public wrapper candidate.

This document separates two kinds of statements:

- **Verified ABI facts**: platform data models, native type widths, SDL header shapes, and current generated output.
- **Project policy conclusions**: how this repo should project those facts into generated raw ABI, public typed APIs, wrapper ergonomics, and validation gates.

That distinction matters because compile-success and ClangSharp syntax are evidence, not proof of ABI correctness.

High-confidence findings:

1. `nint` / `nuint` are wrong for C `long` / `unsigned long` across this repo's 7 target RIDs. Modern raw ABI should use `System.Runtime.InteropServices.CLong` / `CULong`; downlevel TFMs need guards or platform-specific exact strategy.
2. Shared `wchar_t*` mapped as `ushort*` is ABI-wrong on Linux/macOS. The production CppAst generator's opaque `nint` policy is the correct low-level raw shape.
3. ClangSharp spike output has not solved opaque handles. It emits C tag-shaped empty structs and pointers; the Cake-hosted preview emits the intended public low-level typed handles.
4. `SDL_RWops`, `SDL_SysWMinfo`, and `SDL_SysWMmsg` remain hard ABI/layout issues in the ClangSharp evidence. `SDL_RWops` is especially important because satellites consume it heavily.
5. Roslyn postprocess should stay a syntax/backend cleanup layer. Semantic ABI policy belongs in model/profile/type-classification code and validation evidence.
6. Public low-level opaque handles should remain small readonly value types over `nint`, but implicit conversion to `nint` is questionable before the first public API snapshot.

## Sources Reviewed

Repository evidence:

- [`binding-generator-constitution.md`](../binding-autogen/binding-generator-constitution.md): layer contract, C `long`, `wchar_t`, opaque handles, deferred layout, enum policy.
- [`binding-generator-roadmap.md`](../binding-autogen/binding-generator-roadmap.md): raw ABI backend split, public typed low-level milestone, owner-wrapper non-goals.
- `build/_build/Targets/GenerateBindings/ModelBuilding/Types/TypeMappingPolicy.cs`: current CppAst primitive mapping, including `CLong` / `CULong`.
- `build/_build/Targets/GenerateBindings/ModelBuilding/Types/NativeTypeClassifier.cs`: current `wchar_t* -> nint` and opaque handle classification.
- `build/_build/Targets/GenerateBindings/ModelBuilding/SdlPolicy/SdlWideStringPointerPolicy.cs`: HIDAPI wide-string field/parameter override policy.
- `build/_build/Targets/GenerateBindings/ModelBuilding/SdlPolicy/SdlOpaqueStructPolicy.cs`: current `SDL_RWops` quarantine policy.
- `build/_build/Targets/GenerateBindings/Emit/PublicApi/HandleEmitter.cs`: current preview public handle shape and implicit `nint` conversion.
- `artifacts/generated-bindings-preview/sdl2-core/Types/Handles.g.cs`: current generated preview handle surface.
- `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`: ClangSharp evidence report after A/B slices.

External references:

- Microsoft: [Abstract data models](https://learn.microsoft.com/en-us/windows/win32/winprog64/abstract-data-models) - Windows LLP64 model.
- Microsoft: [Data type ranges](https://learn.microsoft.com/en-us/cpp/cpp/data-type-ranges) - MSVC `long` size.
- Apple: [64-Bit Transition Guide](https://developer.apple.com/library/archive/documentation/Darwin/Conceptual/64bitPorting/transition/transition.html) - macOS LP64 model.
- Arm: [AAPCS64](https://github.com/ARM-software/abi-aa/blob/main/aapcs64/aapcs64.rst) - LP64 versus LLP64 model language for AArch64.
- Microsoft: [Native interoperability best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices) - C/C++ type mapping and `CLong` / `CULong` guidance.
- Microsoft: [`CLong`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.clong) and [`CULong`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.culong) API docs.
- Microsoft: [`char`, `wchar_t`, `char16_t`, `char32_t`](https://learn.microsoft.com/en-us/cpp/cpp/char-wchar-t-char16-t-char32-t) - MSVC `wchar_t` is 16-bit UTF-16LE.
- POSIX: [`stddef.h`](https://pubs.opengroup.org/onlinepubs/9799919799/basedefs/stddef.h.html) - `wchar_t` is implementation-defined.
- Microsoft: [`IntPtr`](https://learn.microsoft.com/en-us/dotnet/api/system.intptr), [`SafeHandle`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle), and [`SafeHandle.DangerousGetHandle`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle.dangerousgethandle) docs.
- Microsoft: [User-defined conversion operators](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/user-defined-conversion-operators), [.NET design guidelines for operator overloads](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/operator-overloads), and [.NET design guidelines for fields](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/field).
- Microsoft .NET Blog: [Improving C# Memory Safety](https://devblogs.microsoft.com/dotnet/improving-csharp-memory-safety/) - future caller-unsafe model and safety documentation direction.

## Finding 1: C `long` Is Not `nint`

### Facts

C `long` is data-model-sensitive, not pointer-sized.

| RID family | Native data model | C `long` | Pointer / `nint` | `nint` matches C `long`? |
| --- | --- | ---: | ---: | --- |
| `win-x86` | ILP32 | 32-bit | 32-bit | Yes, accidentally |
| `win-x64` | LLP64 | 32-bit | 64-bit | No |
| `win-arm64` | LLP64 | 32-bit | 64-bit | No |
| `linux-x64` | LP64 | 64-bit | 64-bit | Yes |
| `linux-arm64` | LP64 | 64-bit | 64-bit | Yes |
| `osx-x64` | LP64 | 64-bit | 64-bit | Yes |
| `osx-arm64` | LP64 | 64-bit | 64-bit | Yes |

Therefore `nint` / `nuint` are not portable mappings for C `long` / `unsigned long`. They happen to match Unix x64/arm64 and win-x86, but they are wrong on win-x64 and win-arm64.

### .NET API Evidence

`System.Runtime.InteropServices.CLong` and `CULong` exist specifically for this problem. Microsoft documents `CLong` as:

- 32-bit storage on all Windows platforms;
- 32-bit storage on 32-bit Unix;
- 64-bit storage on 64-bit Unix.

The .NET interop best-practices documentation recommends `CLong` / `CULong` in .NET 6 and later.

### Repository Evidence

The current production/Cake generator already encodes the correct policy:

- `TypeMappingPolicy.cs` maps `CppPrimitiveKind.Long => "CLong"` and `CppPrimitiveKind.UnsignedLong => "CULong"`.
- `ModernCIntegerEmissionPolicy.cs` guards modern C integer usage.
- `binding-generator-constitution.md` explicitly says not to map C `long` / `unsigned long` directly to `nint` / `nuint`.

The ClangSharp spike currently maps some C `long` symbols to `int` / `uint`, for example SDL stdinc functions such as `SDL_ltoa`, `SDL_ultoa`, `SDL_strtol`, `SDL_strtoul`, `SDL_lround`, and `SDL_lroundf`. That output is useful evidence, but it is not the selected production policy.

### Research Conclusion

Use `CLong` / `CULong` in modern raw ABI. For `net462` and `netstandard2.0`, do not fake a portable `CLong` polyfill unless it proves exact per-platform ABI shape. Either guard affected declarations out of downlevel raw ABI or generate exact platform-specific entrypoints.

Public typed wrappers may normalize to `long` / `ulong` with Windows range checks where an input must fit native C `long` on LLP64.

## Finding 2: Shared `wchar_t*` Must Be Opaque In Raw ABI

### Facts

`wchar_t` is implementation-defined in C/C++.

| Platform family | Typical `wchar_t` width | Managed `ushort*` correctness |
| --- | ---: | --- |
| Windows/MSVC | 2 bytes, UTF-16LE | Correct for Windows-only APIs |
| Linux x64/arm64 | 4 bytes | Wrong |
| macOS x64/arm64 | 4 bytes | Wrong |

C# `char` and `ushort` are 16-bit. They are not a portable representation of `wchar_t` across the target RID set.

### SDL2 Surface Classification

Windows-only:

- `SDL_WinRTGetFSPathUNICODE` is WinRT/Windows-only and can be represented as UTF-16 if a Windows-specific helper is generated.

Shared/cross-platform:

- SDL stdinc wide-string helpers: `SDL_wcslen`, `SDL_wcslcpy`, `SDL_wcslcat`, `SDL_wcsdup`, `SDL_wcsstr`, `SDL_wcscmp`, `SDL_wcsncmp`, `SDL_wcscasecmp`, `SDL_wcsncasecmp`.
- SDL HIDAPI fields and functions: `SDL_hid_device_info.serial_number`, `manufacturer_string`, `product_string`, `SDL_hid_open`, `SDL_hid_get_manufacturer_string`, `SDL_hid_get_product_string`, `SDL_hid_get_serial_number_string`, and `SDL_hid_get_indexed_string`.

The HIDAPI surface is cross-platform. SDL ships Windows, Linux, and macOS HID backends behind the same public API shape. On Unix-like platforms, native code expects 4-byte `wchar_t` elements.

### Repository Evidence

The production/Cake generator already follows the right direction:

- `NativeTypeClassifier.cs` maps `wchar_t*` to `nint`.
- `SdlWideStringPointerPolicy.cs` maps known HIDAPI `wchar_t*` fields/parameters to opaque `nint`.
- The constitution says low-level raw shape uses opaque pointer representation unless a platform-specific helper is generated.

The ClangSharp spike maps `wchar_t*` to `ushort*` in generated stdinc, HIDAPI, and WinRT files. That is ABI-correct only for Windows-only APIs and wrong for shared Linux/macOS APIs.

### Research Conclusion

The raw ABI should treat shared `wchar_t*` as opaque `nint` / `IntPtr`. Public friendly wrappers may decode through platform-aware helpers:

- Windows: UTF-16 code units.
- Linux/macOS: 4-byte wide characters, normally UTF-32-shaped for practical SDL/HIDAPI use.

Do not use header shims to redefine `wchar_t`; that lies to the parser and can poison unrelated declarations.

## Finding 3: Opaque Handles Are Not Solved In The ClangSharp Spike

### Upstream Shape

SDL has many public opaque/incomplete concepts, including:

- `SDL_Window`
- `SDL_Renderer`
- `SDL_Texture`
- `SDL_AudioStream`
- `SDL_Cursor`
- `SDL_Joystick`
- `SDL_GameController`
- `SDL_hid_device`
- `SDL_mutex`
- `SDL_cond`
- `SDL_sem`
- `SDL_Thread`

Several are typedefs over private C struct tags, such as `SDL_hid_device_` or `SDL_semaphore`. The public .NET API should expose the SDL typedef concept, not the parser's private tag spelling.

### Current ClangSharp Spike Shape

The ClangSharp spike emits raw C tag-like shapes such as empty public structs plus pointer signatures:

```csharp
public partial struct SDL_Window
{
}

public static partial SDL_Window* SDL_CreateWindow(...);
```

That can be acceptable as an internal raw syntax artifact, because the native ABI really does pass opaque handles as pointers. It is not acceptable as this project's public low-level API shape, because the public contract is based on semantic SDL handle concepts rather than parser tag spellings.

It also leaks parser tag names in examples such as:

- `SDL_hid_device_` instead of canonical `SDL_hid_device`.
- `SDL_semaphore` instead of canonical `SDL_sem`.

### Current Cake Preview Shape

The Cake-hosted preview emits value handles like:

```csharp
[DebuggerDisplay("SDL_AudioStream = {Value}")]
public readonly partial struct SDL_AudioStream(nint value) : IEquatable<SDL_AudioStream>
{
    public readonly nint Value = value;
    public bool IsNull => Value == 0;
    public bool IsNotNull => Value != 0;
    public static SDL_AudioStream Null => new(0);
    public bool Equals(SDL_AudioStream other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is SDL_AudioStream other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(SDL_AudioStream left, SDL_AudioStream right) => left.Equals(right);
    public static bool operator !=(SDL_AudioStream left, SDL_AudioStream right) => !left.Equals(right);
    public static implicit operator nint(SDL_AudioStream value) => value.Value;
    public static explicit operator SDL_AudioStream(nint value) => new(value);
}
```

This direction is sound for public low-level borrowed handles: small, blittable, allocation-free, equality-friendly, and separate from owner/lifetime semantics.

### Open API Design Concern: `implicit operator nint`

The current preview emits implicit conversion from typed handle to `nint`.

Research conclusion: this should be removed or postponed before the first public API snapshot unless there is a deliberate ergonomics decision to keep it.

This is not a binary-compatibility problem yet: the generated handles are preview artifacts, not shipped NuGet API. It is an API-shape decision to settle before the first public API snapshot.

Why:

- It weakens type safety. Any `SDL_Window` can silently flow into unrelated `nint` / userdata / foreign-pointer APIs.
- It makes mixed typed/raw overload sets harder to reason about. If a future API has both typed-handle and raw-native-pointer entry points, implicit conversion can hide which layer the caller selected.
- .NET design guidelines recommend conversion operators only when users clearly expect them and the conversion is unsurprising.
- `IntPtr` docs warn that pointer/handle values are error-prone and unsafe outside tightly controlled interop boundaries.

Important nuance: the implicit `nint` conversion does not by itself allow `SDL_Window` to pass to a method that requires `SDL_Texture`. The current Cake preview internal raw ABI uses typed handle parameters, for example `SDL_DestroyTexture(SDL_Texture texture)`. The risk appears when an API accepts raw `nint` / `IntPtr`, `void*`-like userdata, or a future overload set where raw and typed layers coexist. So the issue is not "every typed API becomes interchangeable"; the issue is that the escape hatch becomes silent instead of intentional.

Better public low-level shape:

```csharp
[DebuggerDisplay("SDL_Window = {Value}")]
public readonly partial struct SDL_Window(nint value) : IEquatable<SDL_Window>
{
    public nint Value { get; } = value;
    public bool IsNull => Value == 0;
    public bool IsNotNull => Value != 0;
    public static SDL_Window Null => new(0);

    public nint DangerousGetHandle() => Value;

    public bool Equals(SDL_Window other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is SDL_Window other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(SDL_Window left, SDL_Window right) => left.Equals(right);
    public static bool operator !=(SDL_Window left, SDL_Window right) => !left.Equals(right);

    public static explicit operator nint(SDL_Window value) => value.Value;
    public static explicit operator SDL_Window(nint value) => new(value);
}
```

Notes:

- `Value` should probably become a get-only property before public API freeze. Public fields are discouraged by .NET design guidelines and are harder to evolve.
- `DangerousGetHandle()` is a useful deliberately sharp escape hatch, even though these low-level handle structs are not `SafeHandle` instances. The danger is type/lifetime misuse, not `SafeHandle` reference-count misuse. This aligns with the constitution's named escape-hatch language: `DangerousGetHandle()` / `nint`, not public raw externs.
- `SafeHandle` should not be the default low-level shape. It is appropriate for later owner wrappers where ownership and release function are known.
- `nint` syntax is a C# language feature that lowers to `System.IntPtr` metadata. It is acceptable in generated source across this repo's TFMs because the packages are built with a modern SDK/compiler; older consumers see `IntPtr`-shaped metadata.

### Research Conclusion

Keep typed readonly `nint` handle structs for public low-level API. Revisit the implicit conversion before API snapshot. Prefer explicit conversion plus `Value` and/or `DangerousGetHandle()`.

## Finding 4: Deferred Layout Must Stay Quarantined Until Proven

### Affected Types

The ClangSharp oracle and generated output show false confidence around:

- `SDL_RWops`
- `SDL_SysWMinfo`
- `SDL_SysWMmsg`

`SDL_RWops` is public SDL header surface, but its `hidden` union is platform-conditioned. It is also heavily consumed by SDL satellite APIs such as SDL_image, SDL_mixer, and SDL_ttf. This makes it a higher-priority containment issue than many rarely used scalar functions.

`SDL_SysWMinfo` and `SDL_SysWMmsg` are explicitly platform-specific union/layout types. The roadmap already places full SysWM typed layout in Stage 2.

### Current Production Policy

The constitution says:

- Stage 1 keeps `SDL_RWops` opaque/quarantined rather than exposing false full layout.
- Full `SDL_SysWMinfo` / `SDL_SysWMmsg` union layout remains Stage 2.

Current production generator evidence supports this direction:

- `SdlOpaqueStructPolicy.cs` lists `SDL_RWops` as opaque.
- Roadmap Stage 2 owns SysWM full union layout.

### Research Conclusion

The next design work should treat deferred layout and opaque handles as one taxonomy problem, even if implementation happens in smaller slices. The taxonomy should distinguish:

- opaque SDL handle;
- concrete ABI struct;
- concrete-but-quarantined layout;
- platform-conditioned layout;
- foreign opaque type;
- accepted deferral.

## Finding 5: Dynapi Count Difference Is Not A Current Blocker

One review found that the reported ClangSharp generated count versus dynapi count difference is likely a raw occurrence count versus unique export-name count issue. The suspected cause is duplicate dynapi entries such as `SDL_Log` in SDL export evidence.

This should not block the semantic ABI classification work. It is still worth improving oracle reporting later so it distinguishes:

- total dynapi export occurrences;
- unique dynapi names;
- duplicate names;
- generated missing names;
- generated extra names;
- intentionally excluded/deferred names.

Dynapi remains name evidence only. It does not prove parameter order, scalar width, struct layout, enum backing type, or ownership semantics.

## Responsibility Split

| Concern | Best owning layer | Notes |
| --- | --- | --- |
| Opaque handle detection and canonical typedef naming | Model/profile/type classification | Affects type catalog, function signatures, public handles, snapshots, and API docs. Do not hide in Roslyn postprocess. |
| `SDL_RWops` quarantine | Model/profile/type classification | Stage 1 semantic policy. ClangSharp spike may need a temporary bridge, but production should decide before emission. |
| `SDL_SysWMinfo` / `SDL_SysWMmsg` deferral | Profile/manifest deferral plus model validation | Stage 2 owns full layout proof. Do not postprocess partial Windows layout into a fake success. |
| C `long` / `unsigned long` | Type mapping policy and TFM backend projection | Modern raw ABI uses `CLong` / `CULong`; downlevel needs guard or exact platform strategy. |
| Shared `wchar_t*` | Type classifier and SDL wide-string policy | Raw shape opaque `nint`; friendly decoding later. |
| Enum backing and `[Flags]` | Enum translator/profile policy | Semantic source concept, not syntax cleanup. |
| DllImport to LibraryImport | Roslyn postprocess or backend emitter | Syntax/backend transform is appropriate in postprocess for the spike. |
| Varargs stripping / fmt-only mapping | Postprocess plus model evidence | Existing spike postprocess is acceptable as temporary syntax cleanup; production model should report fmt-only semantics. |
| Platform duplicate pruning and attributes | Platform merge/projection plus postprocess | Current spike postprocess is acceptable because it reconciles generated files, but semantic platform attribution belongs in the model. |
| Oracle checks | Validation/reporting | Oracle should fail false-success layouts and scalar-width hazards; it should not become the generator. |
| Friendly strings/spans/lifetime wrappers | Public friendly wrapper generator | Do not repair raw ABI mistakes here. |

## Stable Versus Future .NET Guidance

Stable guidance that applies now:

- Keep raw extern declarations internal.
- Use `DllImport` for compatibility backends and `LibraryImport` for modern backends where source-generated interop is supported.
- Keep raw string-like parameters as bytes/pointers; add friendly `string` / span overloads later with explicit encoding, null-termination, and lifetime policy.
- Use `SafeHandle` only when the managed object owns an unmanaged resource and has a clear release function. Do not use `SafeHandle` as the universal low-level SDL handle type.
- Use typed handles over `nint` for borrowed/identity handles; add owner wrappers later where ownership is known.

Future guidance from the C# memory-safety blog:

- The caller-unsafe model and `safe extern` / `unsafe extern` syntax are preview/future language work, not a production target for this repo today.
- The useful immediate lesson is documentation discipline: public unsafe APIs should eventually document caller obligations, and unsafe boundary methods should use local `// SAFETY:` comments when they discharge obligations.
- Do not redesign generated output around C# 16 syntax yet.

## Consolidated Risk Table

| Area | Current ClangSharp spike status | Current production/Cake generator status | Risk if ignored | Research confidence |
| --- | --- | --- | --- | --- |
| Raw ABI container visibility | Fixed by A-slice | Policy-compliant | Public extern API freeze | High |
| SDL.h required init surface | Fixed by B-slice | Manifest-required path exists | Missing required init APIs | High |
| `SDL_RWops` layout | Emits full layout | Quarantined as opaque | False ABI layout, satellite spread | High |
| SysWM layout | Emits partial/platform-shaped layout | Stage 2 deferred | Wrong native window manager layout | High |
| C `long` | `int` / `uint` in spike | `CLong` / `CULong` modern policy | Wrong Unix ABI or wrong Windows ABI if `nint` is used | High |
| `wchar_t*` | `ushort*` in spike | Opaque `nint` policy | Buffer corruption/garbage on Unix | High |
| Opaque handles | Empty structs + pointers | Typed readonly `nint` handles | Public parser-tag leak, unsafe pointer soup | High |
| `implicit operator nint` | Not applicable to ClangSharp raw | Currently emitted in preview handles | Weakens typed handle safety; cheap to change before public API, expensive after API freeze | Medium now, high after public API freeze |
| Dynapi count delta | Possible count/report issue | Name evidence only | Confusing evidence, not ABI failure | Medium |

## Open Questions For Later Design

These are not answered by this research document:

1. Should the Cake preview handle emitter remove `implicit operator nint` before the first public API snapshot, or keep it as an explicit ergonomics tradeoff?
2. Should handle `Value` be a public get-only property instead of a public readonly field before API freeze?
3. Should `DangerousGetHandle()` be emitted for every public low-level handle, or only for handles that commonly cross foreign APIs?
4. Should the ClangSharp spike implement semantic-ABI rewrites as part of closing its near-ABI-compatible Layer 2 surface (the current intent), and how much of that work belongs in Roslyn postprocess versus the orchestrator's input model? The toolchain selection is open (ADR-004 Reopened 2026-05-23); the spike must produce evidence-quality output regardless of whether it ships as production.
5. What exact downlevel strategy, if any, should expose raw C `long` APIs to `net462` / `netstandard2.0` consumers?
6. How much dynapi delta classification belongs in the oracle before it becomes distracting report polish?

## Validation Ideas Raised By Research

These are candidate evidence gates, not accepted implementation requirements yet:

- Record native data-model evidence for every supported RID: `sizeof(long)`, `sizeof(unsigned long)`, `sizeof(wchar_t)`, pointer size, and representative alignment data.
- Add layout probes for high-risk SDL structs/unions before public layout is emitted: `sizeof`, alignment, and key `offsetof` values for `SDL_RWops`, `SDL_SysWMinfo`, and `SDL_SysWMmsg`.
- Teach the oracle to distinguish compile success from ABI truth by reporting false-success layouts, platform-sensitive scalar mappings, and platform-sensitive wide-character mappings separately.
- Improve dynapi evidence reporting with total occurrence count, unique symbol count, duplicate symbols, generated-missing symbols, generated-extra symbols, and accepted exclusions/deferrals.
- Add public API snapshot checks before the first preview to settle handle escape-hatch shape: implicit conversion, explicit conversion, `Value`, and `DangerousGetHandle()`.

## Bottom Line

The project already has most of the correct **policy** answers in its constitution and proved them feasible through the sunset Cake-hosted CppAst implementation. The active ClangSharp spike re-proves the same policy on a different toolchain and remains valuable evidence because it exposes what goes wrong when raw AST syntax is treated as final API shape. Whichever toolchain the spike under [`spikes/binding-generators/`](../../spikes/binding-generators/) selects (ADR-004 Reopened 2026-05-23), the production implementation must satisfy the same constitution policy.

---

## Appendix — 2026-05-24 Brainstorm Follow-Up Research

**Context:** The Priority C semantic-ABI closure design at [`../superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md`](../superpowers/specs/2026-05-24-clangsharp-priority-c-semantic-abi-design.md) was preceded by a brainstorm session (2026-05-24) that dispatched parallel research agents to verify intervention surfaces, peer patterns, and ABI claims before locking design decisions. The following findings extended the 2026-05-22 research above and directly inform the Priority C design's WHY sections.

### Finding 6 — Typed-Handle-By-Value at the P/Invoke Boundary Is ABI-Equivalent

A `readonly partial struct X(nint value)` with a single pointer-sized field passed by value at the P/Invoke boundary is bit-identical to passing `IntPtr` directly. Confirmed against:

- Microsoft [Native interop best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices) — blittable single-field struct, no marshal copy, no transform.
- Microsoft [Blittable and Non-Blittable Types](https://learn.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types) — formatted value types containing only blittable fields are blittable; passed without conversion.
- Microsoft [LibraryImport StructMarshalling design](https://github.com/dotnet/runtime/blob/main/docs/design/libraries/LibraryImportGenerator/StructMarshalling.md) — user-defined blittable structs flow as-is, no `[MarshalAs]` required.
- SysV x64 ABI — composite type ≤ 8 bytes with single integer-class field classified as INTEGER, passed in same GPR as bare `intptr_t`.
- Arm [AAPCS64](https://github.com/ARM-software/abi-aa/blob/main/aapcs64/aapcs64.rst) and [MSVC ARM64 ABI conventions](https://learn.microsoft.com/en-us/cpp/build/arm64-windows-abi-conventions) — composite ≤ 16 bytes rounded up to 8-byte multiples, passed in `x0`–`x7` identical to `intptr_t`.
- x86 cdecl/stdcall — single 4-byte field pushed on stack identically to `int32`/pointer.

Peer adopters (P/Invoke signatures using typed-handle-by-value):

- Cake `RawAbiCommandEmitter` — `internal static extern SDL_Window SDL_CreateWindow(...)`.
- Alimer.Bindings.SDL — `public static partial void SDL_DestroyWindow(SDL_Window window)` with `readonly partial struct SDL_Window(nint value) : IEquatable<SDL_Window>` at `Handles.cs:1437`.
- TerraFX.Interop.Windows — `readonly unsafe partial struct HWND` over `void*`, same ABI.
- Silk.NET — `unsafe partial struct Instance { public nint Handle; }` (non-readonly variant), still passes by value.

The "use IntPtr only at the P/Invoke boundary" advice in older interop literature predates `nint` (C# 9), `readonly struct` (C# 7.2 with multi-TFM IL compatibility back to net462), and `LibraryImport` (.NET 7+). It never reflected an ABI constraint — only a marshaller-maturity concern resolved over a decade ago.

Multi-TFM compatibility verified for `netstandard2.0` / `net462` with `<LangVersion>12</LangVersion>` and explicit `[StructLayout(LayoutKind.Sequential)]`. No runtime pitfalls; `nint` keyword lowers to `System.IntPtr` IL on all TFMs.

### Finding 7 — `--remap` byte-exact textual lookup (ClangSharp internals)

The `wchar_t*=nint` entry in `base.rsp` did not fire because ClangSharp's `--remap` flag uses `QualifiedNameComparer` for byte-exact textual lookup (only `::` ↔ `.` collapsing). Libclang's type printer renders struct field types with a space (`wchar_t *`), parameter types sometimes without (`void*`). The remap key must match the libclang spelling byte-exact.

Concrete fix verified by ppy SDL3-CS pattern: `--remap "wchar_t *=IntPtr"` (quoted, with space). Same pattern applies to our `nint` target.

### Finding 8 — C `long` peer survey: drop-and-document is the dominant pattern

Surveyed approaches on legacy TFMs (where `CLong` / `CULong` are unavailable):

- **SDL2-CS** — drops all C-`long`-touching surface entirely. `SDL_stdinc.h` helpers, `SDL_thread.h` thread API. Zero hits in 8966-line file. Has shipped this posture for 10+ years without consumer complaint.
- **Silk.NET** — `unsigned long` only appears in Windows-only namespaces (DXGI, Direct3D9, Direct3D12) where `DWORD = 32-bit`. Their cross-platform APIs (Vulkan, OpenGL, OpenAL) use sized C types and sidestep the issue entirely.
- **SkiaSharp** — same as Silk.NET; upstream uses `int32_t` / `int64_t` / `IntPtr` directly. Skia is not a useful peer because the problem never arises.
- **LibGit2Sharp** — targets `net472` + `net8.0` only; deliberately skipped `netstandard2.0`. libgit2's `git_time_t` is typedef'd to `int64_t` explicitly, dodging C `long`.
- **Microsoft official guidance** ([cross-platform data types](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)) — recommends dual-DllImport with `RuntimeInformation.IsOSPlatform` dispatch when the symbol must remain accessible.

Native shim approach (per-RID compat `.so`/`.dll` re-exporting `long`-returning functions with explicit-width returns) was considered and rejected: no peer ships such a shim; the cost/value ratio is wildly disproportionate for SDL2.Core's six convenience helpers.

`SDL_lround`, `SDL_lroundf`, `SDL_ltoa`, `SDL_ultoa`, `SDL_strtol`, `SDL_strtoul` are convenience helpers SDL ships because some embedded/console platforms lack a full libc. `.NET` callers have BCL equivalents. The SDL2 wiki does not document them as user-facing API. Drop-all-TFM is the dominant peer posture.

`SDL_threadID` family is structural (different ID space from `Thread.CurrentThread.ManagedThreadId`); cannot be dropped without losing thread-identification capability. Hybrid emit (modern: `CULong` + `#if NET6_0_OR_GREATER`; legacy: Microsoft's dual-DllImport + `RuntimeInformation.IsOSPlatform` dispatch) is the path forward.

### Finding 9 — `wchar_t*` cross-library peer survey: opaque-or-avoid

Surveyed `wchar_t*` handling across major .NET binding libraries:

- **SDL2-CS** — drops the entire `SDL_hid_*` API (the only place SDL2 has shared `wchar_t*`). Zero hits for `wchar_t` / `wcs` / `hid_*` in 8966-line file.
- **Silk.NET** — generator typemap declares `"wchar_t": "char"`. Emits `char* SerialNumber` for `wchar_t* serial_number` (e.g., `Silk.NET.SDL/Structs/HidDeviceInfo.gen.cs`). Windows-correct, Linux/macOS silently wrong. Anti-pattern.
- **SkiaSharp** — generator declares `// TODO: long double, wchar_t ?`. No `wchar_t` in public C API (UTF-8 everywhere). Sidesteps the question.
- **Vortice.Windows** — Windows-only library; no cross-platform constraint.
- **LibGit2Sharp** — libgit2 uses UTF-8 `char*` everywhere for paths (intentional cross-platform design). Binding never sees `wchar_t`.

Microsoft BCL `[MarshalAs(UnmanagedType.LPWStr)]` / `CharSet.Unicode` / `StringMarshalling.Utf16` are **all hardcoded 16-bit on every platform** per [interop charset docs](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/charset). They do not track POSIX's 32-bit `wchar_t`. There is no portable BCL helper for `wchar_t*` cross-platform interop. `CLong` exists; `CWideChar` does not.

The only ABI-honest pattern observed: bind raw as opaque `nint` / `IntPtr` and decode at the high-level wrapper layer. ppy SDL3-CS (`generate_bindings.py:278` — `"wchar_t *=IntPtr"`) and the sunset Cake generator (per [`docs/binding-autogen/binding-generator-constitution.md`](../binding-autogen/binding-generator-constitution.md) §"`wchar_t`") both use this pattern.

### Finding 10 — Opaque struct patterns peer survey

Surveyed opaque struct handling across major .NET binding libraries:

| Library | Pattern | Platform-conditioned union? |
| --- | --- | --- |
| SDL2-CS | **C** (`IntPtr` everywhere; SDL_RWops truncated with comment, SDL_SysWMinfo brute-force union of 12 platform variants under `[FieldOffset(0)]`) | Brute-force union (sized to worst case) |
| ppy SDL3-CS (`.g.cs`) | **A** (empty struct + typed `*` pointer) | N/A (SDL3 dropped SysWMinfo) |
| ppy SDL3-CS (curated `.cs`) | **B** (`readonly partial struct(nint)`) | N/A |
| Alimer.Bindings.SDL | **B** uniformly; CppAst-detected via `cppClass.SizeOf == 0` | N/A |
| Silk.NET | **D** (`partial struct { nint Handle; }`, mutable, non-readonly) | N/A |
| SkiaSharp | **C** raw + managed class wrappers | N/A |
| LibGit2Sharp | **D** (`SafeHandle` subclass per type) | N/A |
| Vortice.Windows | **D'** (COM `class : ComObject`) | N/A |

**None** of these libraries solves the platform-conditioned-union problem portably for the SDL2 `SDL_RWops` / `SDL_SysWMinfo` shape. SDL2-CS brute-forces it (worst-case union); everyone else benefits from upstream ABIs that already chose opaque pointers. The cleanest answer for Stage 1 of a fresh SDL2 binding is to quarantine to an opaque shape (Pattern B per Decision 1 of the Priority C design) until per-RID layout proof exists.

Alimer's auto-detection criterion (`cppClass.SizeOf == 0` → typed handle) is the right ClangSharp/CppAst signal — forward-declared incomplete types become handles automatically. `SDL_RWops` / `SDL_SysWMinfo` / `SDL_SysWMmsg` fail that filter (they have a size in the current translation unit), so they need an explicit force-opaque override list (Cake's `SdlOpaqueStructPolicy` pattern).

### Cross-cutting note on these findings

Findings 6-10 collectively justify the Priority C design's four policy decisions. Each Constitution section that previously had only "Why / How" added a "What" or "Priority C mechanism" subsection cross-referencing the design and the supporting evidence above. The brainstorm preserved this research at canonical-doc level rather than leaving it in chat history.

The durable lesson is to make semantic ABI type classification the center of the next design discussion. Deferred layouts, opaque handles, `wchar_t`, C `long`, enum flags, and public handle escape hatches should be reviewed as one taxonomy. Implementation can still be sliced narrowly, but the decisions share one root: do not emit a success-shaped C# declaration unless the native ABI shape is honestly represented across the supported RIDs and TFMs.

Short version: compilation success is not ABI correctness; AST syntax is not semantic API.

---

## Appendix B — 2026-05-24 Foreign-Type Boundary Survey

**Context:** During Plan Task 4 implementation (oracle duplicate-tag-typedef detection lane), 10 candidate R6 canonicalization pairs surfaced — 6 SDL-owned plus 4 foreign-boundary types (Vulkan, Microsoft GDK). The user raised a real interop concern: wrapping foreign types in our own typed handle structs would force every cross-binding call site (e.g., `Silk.NET.Vulkan` instance passed to SDL's `SDL_Vulkan_CreateSurface`) to construct our wrapper via `new VkInstance(silkInstance.Handle)`. Six parallel research agents (2026-05-24) surveyed all foreign types in SDL2 public API to inform Decision 5 (Foreign Type Boundary Policy) of the Priority C design spec.

### Finding 11 — Vulkan foreign-type inventory

SDL2's Vulkan surface is exactly two symbols: `VkInstance` (= `struct VkInstance_T *`) and `VkSurfaceKHR` (= `struct VkSurfaceKHR_T *` on 64-bit). Both surface as parameters to `SDL_Vulkan_CreateSurface`:

- Source: `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_vulkan.h:52-53` (typedefs), `:187-188` (parameter sites).
- Current spike emission: empty `partial struct VkInstance_T { }` / `VkSurfaceKHR_T { }` (Compat + Modern), parameters use `VkInstance_T*` / `VkSurfaceKHR_T**`.
- SDL2-CS precedent: `IntPtr instance` / `out ulong surface` at `external/sdl2-cs/src/SDL2.cs:2452-2456`. Zero-friction with `Silk.NET.Vulkan.Instance.Handle` / `Vortice.Vulkan.VkInstance.Handle`.
- ppy/SDL3-CS: keeps raw tag-pointers (`VkInstance_T*`, `VkSurfaceKHR_T**`) with `[NativeTypeName("VkInstance")]` annotation. Counter-evidence to canonicalization but high friction with consumer Vulkan bindings.

**Recommendation:** new `rsp/per-header/SDL_vulkan.rsp` with `--remap` of `VkInstance` / `VkSurfaceKHR *` to `IntPtr` / `IntPtr*`; tag structs `VkInstance_T` / `VkSurfaceKHR_T` left to emit harmlessly as empty stubs (no reference sites remain after the remap).

### Finding 12 — OpenGL / EGL / GLES are not foreign concerns

`SDL_GLContext` is **SDL-owned**, not foreign — `typedef void *SDL_GLContext;` at `SDL_video.h:221`. It flows only through SDL functions (`SDL_GL_CreateContext`, `SDL_GL_MakeCurrent`, `SDL_GL_DeleteContext`), never crosses to external GL bindings. The user's real-world `SDL2-CS + Silk.NET.OpenGL` interop example confirms: function-pointer interop happens via delegate factories (`GL.GetApi(proc => SDL_GL_GetProcAddress(proc))`), not via typed GL handles. SDL_GLContext can safely use Pattern B per Constitution §"Opaque Handles".

Foreign GL/EGL/GLES types from `SDL_opengl*.h`, `SDL_egl.h`, etc. are **excluded from the spike's parse scope** via `spikes/binding-generators/scope/sdl2-core.headers.txt` (50 headers listed; none are Khronos re-exports). Constitution L367 Stage 1 exclusion mechanism. **No RSP work needed for GL.**

### Finding 13 — Windows native (Win32 + Direct3D COM) foreign types

Win32 handles (`HWND`, `HDC`, `HINSTANCE`) are **already handled** by `rsp/sdl2-core.rsp:22-24` remaps of `HWND__* / HDC__* / HINSTANCE__* = nint`. These exploit the Win32 convention `typedef struct HWND__ *HWND;` where the underlying tag-struct pointer remap propagates to the typedef.

Direct3D COM interfaces (`IDirect3DDevice9`, `ID3D11Device`, `ID3D12Device`) **are NOT handled** by existing remaps because SDL forward-declares them with `typedef struct IDirect3DDevice9 IDirect3DDevice9;` (no `__` tag indirection) at `SDL_system.h:77`. Current spike emits empty `partial struct IDirect3DDevice9 / ID3D11Device / ID3D12Device` and returns them as typed pointers from `SDL_RenderGetD3D{9,11,12}Device`. SDL2-CS precedent (`external/sdl2-cs/src/SDL2.cs:8747-8748`): all D3D interfaces return `IntPtr` with `// Refers to an IDirect3DDevice9*` annotation. Vortice.Direct3D11 / TerraFX.Interop.Windows consumers expose `ID3D11Device` with `.NativePointer` returning `IntPtr` — zero-friction with the IntPtr return.

**Recommendation:** new `rsp/per-header/SDL_system.rsp` with `--remap IDirect3DDevice9*=nint`, `ID3D11Device*=nint`, `ID3D12Device*=nint` + `--exclude` of the three tag structs.

Note `MSG` / `tagMSG` is NOT in SDL2's public surface — SDL2's `SDL_WindowsMessageHook` takes `void* hWnd, unsigned int message, Uint64 wParam, Sint64 lParam` (no MSG struct exposure). That's an SDL3 addition. No work needed for SDL2.

### Finding 14 — Apple (Cocoa/UIKit/Metal) foreign types — currently deferred

`SDL_syswm.h` Apple variants (`NSWindow *`, `UIWindow *`, `UIViewController *`) appear at `:266-289` but **are not emitted by the current spike** because `SDL_syswm.h` is not in `PLATFORM_SENSITIVE_HEADERS` (Constitution L292-294 Stage 1 quarantine; only `SDL_main.h` + `SDL_system.h` get the multi-OS pass). Generated output has zero Apple references in `Platforms/MacOS/` and `Platforms/IOS/`.

Metal entry points (`SDL_RenderGetMetalLayer`, `SDL_RenderGetMetalCommandEncoder`, `SDL_Metal_*`) already use `void *` returns at `SDL_render.h:1879-1911` and `SDL_metal.h` — SDL's deliberate "headers don't need to include Metal" design. Already handled by `void*=nint`. No new work needed.

SDL2-CS precedent for the hypothetical Apple `SDL_syswm.h` activation: `INTERNAL_cocoa_wminfo.window = IntPtr; // Refers to an NSWindow*`; `INTERNAL_uikit_wminfo.window = IntPtr; // Refers to a UIWindow*` (at `external/sdl2-cs/src/SDL2.cs:8695-8707`). Per-header RSP entries for `NSWindow`/`UIWindow`/`UIViewController` activate when `SDL_syswm.h` enters the Apple multi-OS pass — **deferred** to a future slice.

### Finding 15 — Linux X11/Wayland/KMSDRM foreign types — currently deferred

Same structural finding as Apple: `SDL_syswm.h` Linux variants (`Display *`, `Window`, `XEvent *`, `wl_display *`, `wl_surface *`, `gbm_device *`, etc.) at `:173,249-302,342` are present in the header but **not emitted** because `SDL_syswm.h` is Stage 1 quarantined. Constitution L367 additionally excludes DirectFB / Mir / Vivante / OS/2 unconditionally.

When SDL_syswm.h enters the Linux multi-OS pass:
- ppy precedent (`SDL_system.Linux.rsp`): `--exclude _XEvent` + `--remap _XEvent*=IntPtr` — minimalist pattern.
- SDL2-CS precedent (`external/sdl2-cs/src/SDL2.cs:8680-8786`): all X11/Wayland/KMSDRM pointers as `IntPtr` with annotated comments.
- **Deferred** to the slice that activates the Linux SysWM pass.

### Finding 16 — Microsoft GDK foreign types — active

`XTaskQueueHandle` (= `XTaskQueueObject *`) and `XUserHandle` (= `XUser *`) at `SDL_system.h:599-630` (GDK pass) **are currently emitted** in `Platforms/GDK/SDL_system.g.cs:10-35` as empty stub structs with `XTaskQueueObject **` / `XUser **` out-parameter signatures. ppy/SDL3-CS keeps the same tag-preserving pattern.

SDL2-CS skips the GDK functions entirely (`SDL_GDKGetTaskQueue`, `SDL_GDKGetDefaultUser` absent from the binding). No SDL2-CS GDK precedent.

**Recommendation:** new RSP entries for `XTaskQueueObject *` / `XUser *` → `nint` per Decision 5. Either share `rsp/per-header/SDL_system.rsp` (with platform-conditioned comments) or split into `rsp/per-header/SDL_system.GDK.rsp` if cleaner. **Active in Task 6 scope.**

### Finding 17 — Android JNI types pre-erased upstream

SDL deliberately pre-erases JNI types to `void *` at the header level (`SDL_system.h:262,278` — "SDL headers avoid including `jni.h`"). Returns of `SDL_AndroidGetJNIEnv` / `SDL_AndroidGetActivity` already surface as `void *` in the header. Spike output uses `nint` with `[NativeTypeName("void*")]`. **Already handled by `base.rsp:18` `void*=nint`. No additional RSP work needed.**

`jobject` / `jstring` / `jclass` / `jint` etc. never appear in SDL2 public API.

### Finding 18 — WinRT IInspectable currently moot

`IInspectable *` appears at `SDL_syswm.h:243` (WinRT union arm), but is gated behind `SDL_GetWindowWMInfo` which is currently excluded via `rsp/sdl2-core.rsp:32`. **Deferred** until SDL_GetWindowWMInfo is un-deferred.

### Cross-cutting synthesis of foreign-type surveys

1. **Most foreign types are dormant.** `SDL_syswm.h`'s entire Linux/macOS/iOS/WinRT/DirectFB union is unreachable in the current spike — only the Windows variant + nothing else. This means the Priority C scope for Decision 5 is genuinely limited to Vulkan + Direct3D + GDK.
2. **Peer consensus on the IntPtr-at-boundary pattern is strong** except for ppy/SDL3-CS, which preserves tag-pointers across-the-board (consistent with their philosophy of minimal RSP overrides).
3. **The user's interop concern is empirically valid.** Silk.NET, Vortice, TerraFX all expose foreign handles via `.Handle` returning pointer-sized integers — friction with `IntPtr` is zero, friction with a typed `VkInstance(nint)` wrapper is one explicit construction per call site.
4. **Manual curation is unavoidable.** No automatic mechanism distinguishes SDL-owned from foreign types. The Decision 5 allow-list grows deliberately in per-header RSPs, citing source-line evidence.

Decision 5 of the Priority C design spec carries these conclusions as binding policy. Constitution §"Foreign Type Boundary Policy" mirrors the policy at canonical-doc level beyond Priority C.
