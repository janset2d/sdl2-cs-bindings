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

The durable lesson is to make semantic ABI type classification the center of the next design discussion. Deferred layouts, opaque handles, `wchar_t`, C `long`, enum flags, and public handle escape hatches should be reviewed as one taxonomy. Implementation can still be sliced narrowly, but the decisions share one root: do not emit a success-shaped C# declaration unless the native ABI shape is honestly represented across the supported RIDs and TFMs.

Short version: compilation success is not ABI correctness; AST syntax is not semantic API.
