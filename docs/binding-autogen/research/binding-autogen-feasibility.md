# Research: Binding Auto-Generation Feasibility Study

**Date**: 2026-05-12
**Companion**: [`binding-autogen-approaches.md`](binding-autogen-approaches.md) — tool comparison, industry survey, decision matrix.
**Context**: Goes beyond "which tool" to answer "is this actually doable on our stack, what code would the generator emit in 2026, how does it interact with our hybrid-static vcpkg pipeline, what's manual vs automated, how do we know it works."

> **Decision note (2026-05-14, updated 2026-05-15):** This remains the feasibility research record. The accepted strategy brief and ADR-004 select the CppAst path for Phase 4 planning while preserving ClangSharp as the documented migration path.
>
> **Frozen research artifact.** Subsequent corrections live in the strategy brief Decision Audit, not in this file. Notable retractions superseded by 2026-05-15 revisions:
>
> - **mingw-w64 / Apple SDK platform stubs as Stage 1 inputs** — retracted (Decision Audit Error 4). Stage 1 uses preprocessor-macro switching only, with no `--target` cross-compile flag and no platform SDK headers. SDL's public headers carry the necessary cross-platform opaque-type forward declarations. Verified against ppy/SDL3-CS Dockerfile + `generate_bindings.py` (WebFetch 2026-05-15) and the local CppAst spike at `tools/binding-spike/cppast/generator/Program.cs:42-72`.
> - **Standalone `src/Janset.SDL2.Bindings.Generator/` console-app generator** — retracted (Decision Audit Reframe row). The generator is hosted inside the Cake build host under `build/_build/Targets/GenerateBindings/`, target-local per ADR-002 §2.4.
> - **`SDL_syswm.h` as Stage 1 first-class proof target** — moved to Stage 2 with documented exclusion. Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `SDL_SysWMinfo*` parameter; the typed union with `[StructLayout(LayoutKind.Explicit, Size = 64)]` and the forward-declaration stub library (~15–20 types) land in Stage 2.
>
> When a Phase 4 plan or implementation question arises, prefer the strategy brief + the design spec + the Stage 1 plan as canonical sources over this doc.

## 1. Scope

This document covers four feasibility dimensions:

1. **Emit-target patterns** — what modern .NET P/Invoke code should the generator produce in 2026?
2. **Headers & cross-platform feasibility** — can CppAst consume our vcpkg-installed headers cleanly across Windows / Linux / macOS feature sets?
3. **Hybrid-static + symbol visibility interaction** — does our packaging strategy + symbol-hiding work create traps for an AST-driven generator?
4. **Manual intervention surface + testing strategy** — what fraction is automated, what requires hand-curated rules, how do we validate correctness end-to-end?

Out of scope: the actual generator implementation plan (that lands when Phase 4 activates). This is feasibility, not design.

**2026-05-12 toolchain evidence update.** After deeper research (see [`binding-autogen-approaches.md`](binding-autogen-approaches.md) §2026-05-12 Update — Decision Matrix Re-Validation + §2026-05-12 Source-Level Comparison), the research recommendation shifted from CppAst toward **ClangSharpPInvokeGenerator (ppy/SDL3-CS pattern)** for raw bindings. This is not the final project decision; the WHY/HOW/WHAT design doc owns that decision. The output-quality rules in §2 are toolchain-agnostic and unchanged; toolchain-specific sections (§3 multi-platform parsing, §5 reference cross-check, §6 manual intervention surface, §8 effort estimate, §9 open decisions) capture provisional ClangSharp-pattern evidence for comparison against the CppAst spike.

## 2. Modern .NET P/Invoke Emit Target — Generator Rules for 2026

**Toolchain note (2026-05-12 update):** These rules describe the generated *output*. ClangSharpPInvokeGenerator achieves most via RSP flags (`--remap`, `--with-attribute`, `--with-type`, `--exclude`, `--with-using`) + a Roslyn source generator extension (e.g., ppy/SDL3-CS's `FriendlyOverloadGenerator` for compile-time-synthesized friendly overloads). CppAst would achieve the same output via custom emitter code. The rules below are toolchain-agnostic; what differs is *how* you express them in generator configuration.

Synthesized from a fresh survey of Microsoft Learn ([P/Invoke source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation), [native interop best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices), [custom marshalling source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/custom-marshalling-source-generation), [Native AOT interop](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/interop)), [dotnet/runtime compatibility docs](https://github.com/dotnet/runtime/blob/main/docs/design/libraries/LibraryImportGenerator/Compatibility.md), and verification against the current generated code in [Alimer.Bindings.SDL Handles.cs](https://github.com/amerkoleci/Alimer.Bindings.SDL/blob/main/src/Alimer.Bindings.SDL/Generated/Handles.cs) + [Commands.cs](https://github.com/amerkoleci/Alimer.Bindings.SDL/blob/main/src/Alimer.Bindings.SDL/Generated/Commands.cs) and [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS).

### Rule 1 — Default attribute: `LibraryImport` (net7+) with `DllImport` fallback

Microsoft's position is explicit: *"DO use `[LibraryImport]`, if possible, when targeting .NET 7+ … Using `DllImport` isn't an option for platforms that require full Native AOT scenarios"* (P/Invoke source-gen doc). The IL stub for `DllImport` is JIT-generated at runtime — invisible to the AOT compiler and trimmer.

Our multi-TFM matrix is `net10` / `net9` / `net8` / `netstandard2.0` / `net462`. Generator should emit dual:

```csharp
internal static partial class SDL3
{
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "SDL_CreateWindow")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial SDL_Window SDL_CreateWindow(byte* title, int w, int h, SDL_WindowFlags f);
#else
    [DllImport(LibName, EntryPoint = "SDL_CreateWindow",
               CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern unsafe SDL_Window SDL_CreateWindow(byte* title, int w, int h, SDL_WindowFlags f);
#endif
}
```

Class is `partial`. Methods are `partial`+`static` on the LibraryImport branch (the source generator fills the body), `extern`+`static` on the DllImport branch. `AllowUnsafeBlocks` must be on. Compatibility deltas under LibraryImport (drop these attributes): `CallingConvention`, `CharSet`, `BestFitMapping`, `ThrowOnUnmappableChar`, `ExactSpelling`, `PreserveSig`. Unsupported types/idioms under LibraryImport: `StringBuilder`, `HandleRef`, `CriticalHandle`, `UnmanagedType.I[Unknown|Dispatch|Inspectable]`. Our scope hits none of those (SDL is C, not COM).

### Rule 2 — Opaque handle types: `readonly partial struct`, never `SafeHandle`

For C opaque pointers (`SDL_Window*`, `SDL_Renderer*`, `SDL_Texture*`, `SDL_AudioStream*`, `SDL_GPUDevice*`, etc.) emit a value-type wrapper:

```csharp
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly partial struct SDL_Window(nint value) : IEquatable<SDL_Window>
{
    public readonly nint Value = value;
    public bool IsNull => Value == 0;
    public static SDL_Window Null => new(0);
    public bool Equals(SDL_Window other) => Value == other.Value;
    public static bool operator ==(SDL_Window l, SDL_Window r) => l.Equals(r);
    public static bool operator !=(SDL_Window l, SDL_Window r) => !l.Equals(r);
    public override int GetHashCode() => Value.GetHashCode();
    public override bool Equals(object? obj) => obj is SDL_Window other && Equals(other);
    private string DebuggerDisplay => string.Format(CultureInfo.InvariantCulture, "SDL_Window [0x{0:X}]", Value);
}
```

This is **blittable** (single `nint` field → zero marshalling), **AOT-trivial**, **type-safe** (compile-time error if you pass `SDL_Renderer` where `SDL_Window` is expected), and **zero overhead vs raw `IntPtr`**.

**Why not `SafeHandle`?** Microsoft's best-practices doc says "DO use SafeHandle." The mature SDL3 bindings (Alimer, ppy, Silk.NET) **all reject it for SDL's opaque pointers**. SafeHandle's per-call lock + atomic-ref-count overhead is wasted on SDL handles, which are application-scoped and owned by deterministic `IDisposable` wrappers. Reserve SafeHandle for OS-level resources owned across long async lifetimes (file handles, registry keys) where finalization is mandatory. SDL handles are not those.

### Rule 3 — Numeric IDs: typed `enum : uint` / `enum : ulong`

For SDL ID types (`SDL_AudioDeviceID`, `SDL_JoystickID`, `SDL_TouchID`, `SDL_DisplayID`, etc.) emit a typed enum even when the C type is a bare `Uint32`/`Uint64`:

```csharp
public enum SDL_AudioDeviceID : uint { }   // typed value, zero runtime cost
```

Type-safe at the call site, blittable across P/Invoke, no enum members needed for ID-style types (the value space is opaque to the consumer).

### Rule 4 — UTF-8 strings — triple overload pattern

SDL uses UTF-8 strings everywhere. Generator emits three overloads per `const char*` in-parameter:

```csharp
[LibraryImport(LibName, EntryPoint = "SDL_CreateWindow")]
public static partial SDL_Window SDL_CreateWindow(byte* title, int w, int h, SDL_WindowFlags f);

[LibraryImport(LibName, EntryPoint = "SDL_CreateWindow")]
public static partial SDL_Window SDL_CreateWindow(ReadOnlySpan<byte> title, int w, int h, SDL_WindowFlags f);

[LibraryImport(LibName, EntryPoint = "SDL_CreateWindow", StringMarshalling = StringMarshalling.Utf8)]
public static partial SDL_Window SDL_CreateWindow(string title, int w, int h, SDL_WindowFlags f);
```

`StringMarshalling.Utf8` invokes the built-in [`Utf8StringMarshaller`](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/custom-marshalling-source-generation), which uses a stateful `ManagedToUnmanagedIn` with a stackalloc buffer (≤ 256 bytes) and falls back to `NativeMemory.Alloc` for longer strings. Free is automatic. Hot paths use `byte*` or `ReadOnlySpan<byte>` to skip the allocation entirely.

For `const char*` **return values where SDL owns the memory** (`SDL_GetError`, `SDL_GetWindowTitle`): never decorate return as `string` — emit `byte*` and provide a helper:

```csharp
[LibraryImport(LibName, EntryPoint = "SDL_GetError")]
public static partial byte* SDL_GetError();

public static string? GetErrorString() => Utf8StringMarshaller.ConvertToManaged(SDL_GetError());
```

For `char*` **return values where caller must `SDL_free`** the buffer: never emit `string` return with `StringMarshalling.Utf8` (marshaller calls `NativeMemory.Free`, which mismatches `SDL_free`). Emit `byte*` and write a hand-rolled `[CustomMarshaller(..., MarshalMode.ManagedToUnmanagedOut, ...)]` whose `Free` calls `SDL_free`. Microsoft's best-practices doc is explicit: *"Match the allocator: never mix `malloc`/`free` with `CoTaskMemAlloc`/`CoTaskMemFree`."*

### Rule 5 — Boolean wire types — model SDL2 and SDL3 separately, never raw `bool`

SDL2 and SDL3 do not expose the same boolean ABI. C# `bool` defaults to 4-byte `BOOL` marshalling under interop, so the generator should never emit raw `bool` in P/Invoke signatures.

SDL3 uses C `bool` / `_Bool`-style 1-byte values. Raw SDL3 bindings should use a byte-backed wrapper:

```csharp
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly struct SdlBool8(byte value) : IEquatable<SdlBool8>
{
    public readonly byte Value = value;
    public bool AsBool => Value != 0;
    public static SdlBool8 True => new(1);
    public static SdlBool8 False => new(0);
    public static implicit operator bool(SdlBool8 b) => b.AsBool;
    public static implicit operator SdlBool8(bool b) => new((byte)(b ? 1 : 0));
    public bool Equals(SdlBool8 other) => Value == other.Value;
}
```

SDL2's `SDL_bool` is different: in current SDL2 headers it is either `typedef int SDL_bool` for ARM compiler compatibility or an enum with values `SDL_FALSE = 0` and `SDL_TRUE = 1`. SDL2 also asserts enum size equals `sizeof(int)`. Raw SDL2 bindings should therefore model `SDL_bool` as an int-backed enum or int-backed wrapper, not the 1-byte SDL3 wrapper. Implicit conversions can still hide the wire type from callers in both families, but the wire size must stay family-specific.

### Rule 6 — Span / Memory / buffers

`LibraryImport` natively supports `Span<T>` and `ReadOnlySpan<T>` of blittable element types via the [collection marshaller shape](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/custom-marshalling-source-generation). `DllImport` does not — must lower to `T*` + length manually under that branch.

Generator emit rules:

- **Single-element output**: `out T` (lowers to `T*` with implicit pin) — never `Span<T>` of length 1.
- **Multi-element output filled by callee, variable count**: `Span<T>` with `[MarshalUsing(CountElementName = nameof(count))]` plus the explicit length out-parameter.
- **Caller stackalloc hot path**: keep raw-pointer overload alongside the Span overload so callers can write `Span<SDL_Rect> rects = stackalloc SDL_Rect[8]; SDL_GetWindowSurfaceClipRects(window, rects, rects.Length);` without marshalling overhead.
- **Avoid `Memory<T>` in P/Invoke signatures**: non-contiguous-friendly type; expose only in hand-written wrappers if needed (`MemoryMarshal.GetReference` + `MemoryHandle pin = m.Pin()` ceremony).
- **Arrays of opaque handles**: use the `Span<SDL_Window>` form — the handle struct is blittable (single `nint`), so the standard span marshaller works without extra ceremony.

### Rule 7 — Function pointers + callbacks: `delegate*` + `[UnmanagedCallersOnly]`

Microsoft best-practices: *"DO prefer using function pointers and `[UnmanagedCallersOnly]` as opposed to `Delegate` types when passing callbacks to unmanaged functions in C#."*

For SDL functions that take a callback (event filters, audio callbacks, log handlers):

```csharp
[LibraryImport(LibName, EntryPoint = "SDL_SetEventFilter")]
public static partial void SDL_SetEventFilter(
    delegate* unmanaged[Cdecl]<nint, SDL_Event*, SdlBool8> filter,
    nint userdata);
```

Callers write a `[UnmanagedCallersOnly]` static method and pass `&Method`:

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
static SdlBool8 EventFilter(nint userdata, SDL_Event* e) { /* … */ return SdlBool8.True; }

SDL_SetEventFilter(&EventFilter, IntPtr.Zero);
```

**No allocation, no GC pinning, no lifetime worry.** Static method's address is rooted by the runtime. AOT-safe under both CoreCLR and NativeAOT.

The generator should **never silently emit `Marshal.GetFunctionPointerForDelegate`** — that requires the caller to root the delegate themselves and is a frequent source of crashes. If a caller needs an instance-bound closure they write it themselves (rare in practice).

### Rule 8 — Constants (`#define`, `enum`, `static readonly`)

CppAst surfaces `#define` macros as `CppMacro` entries. Generator emits them as `public const` (for literal numerics / strings) or `public static readonly` (for computed expressions). Categorize by macro pattern:

- `#define SDL_INIT_AUDIO 0x00000010u` → `public const uint SDL_INIT_AUDIO = 0x00000010u;`
- `#define SDL_AUDIO_DEVICE_DEFAULT_OUTPUT ((SDL_AudioDeviceID)0xFFFFFFFFu)` → `public static readonly SDL_AudioDeviceID SDL_AUDIO_DEVICE_DEFAULT_OUTPUT = new(0xFFFFFFFFu);`
- `#define SDL_HINT_AUDIO_CATEGORY "SDL_AUDIO_CATEGORY"` → `public const string SDL_HINT_AUDIO_CATEGORY = "SDL_AUDIO_CATEGORY";`

C `enum` types map to C# `enum` with explicit underlying type (`enum Foo : uint`) — match the C width. Bit-flag enums get `[Flags]`.

### Rule 9 — Trimming + AOT

Emit `<IsAotCompatible>true</IsAotCompatible>` in the generated .csproj. Never emit `[RequiresDynamicCode]` or `[RequiresUnreferencedCode]` — the LibraryImport-generated marshalling is AOT-safe by construction. SafeHandle marshalling under LibraryImport is also AOT-safe (`SafeHandleMarshaller<T>` shape is source-generated) — though we don't use SafeHandle anyway per Rule 2.

Never emit `ICustomMarshaler` (reflection-based, blocks AOT). Always use `[CustomMarshaller]` (source-generation-friendly).

### Rule 10 — C# 13 / C# 14 emit style

The generated code lives in the same .NET 10 / C# 14 baseline as the rest of the project. Emit:

- `CallConvs = [typeof(CallConvCdecl)]` (collection expression, C# 12+).
- `params ReadOnlySpan<T>` (C# 13) for variadic wrappers if any.
- `field`-backed properties + partial properties (C# 14) if generator wants per-handle metadata.
- `ref struct` constraint `where T : allows ref struct` (C# 13) for marshaller types that handle Span.
- Drop `ExactSpelling` (gone under LibraryImport, always exact).

### Rule 11 — Convention for partial-class boundaries

Emit one `partial class` per SDL family scope (e.g. `SDL3` for core, `SDL3_image` for image, etc.), split across multiple .cs files by category:

```text
src/SDL3.Core/Generated/
├── Commands.cs       ← function P/Invoke declarations
├── Constants.cs      ← #define / static constants
├── Enums.cs          ← C enums → C# enums
├── Handles.cs        ← opaque pointer wrappers
├── Structs.cs        ← C structs / unions
└── Callbacks.cs      ← delegate* type aliases for documented callback signatures
```

Mirrors Alimer's `CsCodeGenerator.{Commands,Constants,Enum,Handles,Structs}.cs` generator-side organization in the output.

### Summary of 11 emit rules

| Rule | Pattern |
| --- | --- |
| 1 | `[LibraryImport]` for net7+, `[DllImport]` for legacy TFMs, dual via `#if` |
| 2 | Opaque pointers → `readonly partial struct Name(nint value)` |
| 3 | Numeric IDs → typed `enum NameID : uint` |
| 4 | UTF-8 strings in → triple overload (`byte*` / `ReadOnlySpan<byte>` / `string` w/ `StringMarshalling.Utf8`) |
| 5 | Booleans → SDL2 int-backed enum/wrapper, SDL3 1-byte wrapper; never raw `bool` |
| 6 | Buffers → `Span<T>`/`ReadOnlySpan<T>` + raw-pointer overload; never `Memory<T>` in P/Invoke; `out T` for single-element |
| 7 | Callbacks → `delegate*` + `[UnmanagedCallersOnly]`, never `Delegate` |
| 8 | Constants → `public const` (literal) or `public static readonly` (computed) |
| 9 | AOT → `<IsAotCompatible>true</IsAotCompatible>`, source-generated marshallers only |
| 10 | Style → modern C# 13/14 (collection expressions, params ReadOnlySpan, etc.) |
| 11 | Output → one `partial class` per family, split by category (Commands / Constants / Enums / Handles / Structs / Callbacks) |

## 3. Headers & Cross-Platform Feasibility

### Where headers live

vcpkg installs library headers into `<vcpkg-installed>/<triplet>/include/<library>/`. For our build, that's e.g. `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL.h` and friends. Each triplet has its own `include/` tree.

**Headers are expected to be the same upstream source** across triplets for SDL's public headers: vcpkg installs the same library version into each triplet directory, and the per-triplet differentiation is normally in the **compiled artifacts** (lib/, bin/), not the public declarations. Do not turn that into an unchecked axiom, though. Ports can apply patches, generated config headers can exist in some ecosystems, line endings can differ, and platform SDK/system headers are absolutely not the same.

Implication: generator should not parse every RID's vcpkg include tree by default. **One canonical SDL include tree can be the declaration source** after a cheap header-identity check against the Linux/WSL and macOS provisioned checkouts for the in-scope SDL public headers. That does not remove the need for platform parse passes: even byte-identical headers produce different ASTs once libclang sees different target macros, architecture flags, and system include/sysroot inputs.

Local verification against provisioned Windows, WSL/Linux, and macOS checkouts confirmed the expected shape for SDL2: all three had the same 88 public SDL2 header filenames, and all checked binding-relevant public headers (`SDL.h`, `SDL_platform.h`, `SDL_stdinc.h`, `SDL_system.h`, `SDL_main.h`, `SDL_syswm.h`, `SDL_image.h`, `SDL_ttf.h`, `SDL_mixer.h`) matched byte-for-byte after CRLF normalization. `SDL_config.h` differed, as expected, because it is the generated/configured platform feature macro surface.

### Cross-platform API surface variance

SDL is designed for cross-platform consumption, but the AST produced from its headers is not platform-independent. libclang parses one preprocessed translation unit at a time; macros, target triple, architecture flags, and include paths decide which declarations exist in that AST. This is a C/preprocessor constraint, not a CppAst-vs-ClangSharp distinction.

Concrete local verification against `vcpkg_installed\x64-windows-hybrid\include\SDL2` shows platform-conditioned public surface:

| Header section | Variance |
| --- | --- |
| `SDL_platform.h` | Normalizes host and target macros such as `__LINUX__`, `__ANDROID__`, `__MACOSX__`, `__IPHONEOS__`, `__WIN32__`, `__WINRT__`, and `__GDK__`. |
| `SDL_system.h` | Contains Windows-only `SDL_SetWindowsMessageHook`, D3D/DXGI helpers, Linux-only `SDL_LinuxSetThreadPriority*`, iOS, Android, WinRT, and GDK blocks. |
| `SDL_main.h` | Switches startup/main handling and exposes Windows/GDK/iOS/WinRT entry helpers behind platform macros. |
| `SDL_config.h` | Computes `SIZEOF_VOIDP` from `_WIN64`, `__LP64__`, and `_LP64`. |
| `SDL_stdinc.h` | Chooses integer-format macros differently for Windows/GDK, LP64 Unix, Apple, and fallback cases. |
| `SDL_syswm.h` | `SDL_SysWMinfo` has per-window-system variants and may require special handling or exclusion depending on support scope. |

The variance is concentrated, but real. A single Linux-host parse can produce a useful platform-neutral subset; it cannot prove complete Windows/Linux/macOS coverage unless the generator also runs controlled platform-specific parses.

### Header source, parse view, and host runner are separate axes

Three concepts are easy to conflate and should stay separate in the Phase 4 design:

| Axis | Meaning | Current evidence / tendency |
| --- | --- | --- |
| Header source | Which `SDL*.h` files are read as declaration input | One canonical vcpkg SDL include tree appears viable after header-identity checks. |
| Parse view / pass | Which preprocessor macros, target system, architecture, and include/sysroot view libclang sees | Neutral + Windows + Linux + macOS views are required for platform-conditioned headers. |
| Host runner | Where the generator executable runs | A pinned Linux container is the simplest deterministic host candidate, but it can still produce Windows/Linux/macOS parse views. |

The CppAst platform-pass spike proved the distinction: it ran on Windows but produced a Linux-specific output file by parsing with a Linux target view, Linux macros enabled, Windows/macOS macros disabled, and minimal system-header stubs supplied. That does **not** mean native Linux/macOS runners are useless; they remain valuable for reproducibility checks, native export validation, package consumer smoke, and escalation if a controlled cross-target parse view cannot model a header correctly. It means "platform pass required" should not be read as "the generator must run on that platform."

### vcpkg feature variance — `vcpkg.json` platform conditions

Our `vcpkg.json` declares some features as platform-conditional:

```jsonc
{ "name": "sdl2", "features": [
    { "name": "vulkan" },
    { "name": "alsa", "platform": "linux" },
    { "name": "dbus", "platform": "linux" },
    { "name": "ibus", "platform": "linux" },
    { "name": "samplerate" },
    { "name": "wayland", "platform": "linux" },
    { "name": "x11", "platform": "!windows" }
]}
```

These features affect the **compiled .so/.dll** (whether ALSA/DBus/X11/Wayland code is baked in), not the header surface. SDL2's headers declare all subsystems unconditionally; the implementation gates on `SDL_VIDEO_DRIVER_X11` etc. at link time.

**Implication**: the AST generator does not need feature-aware parsing. The header surface is single-source-of-truth regardless of which vcpkg features are enabled per triplet.

### Multi-platform parsing — validate platform-specific pass strategy

Neither CppAst nor ClangSharp exposes an official "multi OS pass" feature. Both are adapters over libclang parse arguments:

- CppAst exposes `Defines`, include folders, target options, and `AdditionalArguments`; a second platform view means a second `CppParser.Parse*` call with a different options instance.
- ClangSharpPInvokeGenerator exposes `-D`, `-I`, `--additional`, and target arguments; a second platform view means a second tool invocation.

There are two recorded approaches in comparable repositories:

| Approach | Pattern | Trade-off |
| --- | --- | --- |
| **Single pass with multiple platform defines simultaneously set** (Alimer.Bindings.SDL pattern) | Parse once with Android/iOS/WinRT-style defines all active, emit one output set | Simpler pipeline. Works for much of SDL's neutral surface, but produces an impossible union target for platform-conditioned headers, loses platform attribution, and can silently miss mutually-exclusive `#elif` branches. Useful as a cautionary reference, not sufficient for platform-conditioned correctness across our 7-RID matrix. |
| **Neutral + platform-specific passes** (ppy/SDL3-CS pattern) | One platform-agnostic pass, then one pass per platform with that platform's macros; exclude symbols already emitted by the neutral pass and write platform-specific files/attributes | More moving parts in the generator pipeline. Correctly attributes OS-specific symbols and keeps generated output reviewable by platform. This is the current validation target regardless of whether the selected implementation is CppAst or ClangSharp. |
| **True multi-OS extraction + merge** (bottlenoselabs/SDL3-cs pattern) | Windows, Linux, and macOS CI runners each extract an FFI/API view; a later job merges those views into one cross-platform intermediate and generates C# | Strongest isolation from host/sysroot assumptions. Much heavier: merge semantics must classify common symbols, platform-only symbols, signature conflicts, and layout conflicts before C# emission. Treat as an escalation path unless SDL headers prove a controlled single-host strategy insufficient. |

**Current validation target:** for our 7-RID matrix (`win-{x64,x86,arm64}`, `linux-{x64,arm64}`, `osx-{x64,arm64}`), correctly attributing OS-specific symbols is high-value. The CppAst platform-pass spike proved ppy-equivalent neutral + platform passes for selected SDL2 platform headers. ClangSharp remains the comparison baseline because ppy already proves the same pattern for SDL3.

```python
# ppy/SDL3-CS pattern, paraphrased
generate_platform_specific_headers(sdl_api, system_header, [
    (["SDL_PLATFORM_WINDOWS", "SDL_PLATFORM_WIN32"], "Windows", "Windows"),
    (["SDL_PLATFORM_LINUX"],   "Linux",   "Linux"),
    (["SDL_PLATFORM_MACOS"],   "macOS",   "OSX"),
    (["SDL_PLATFORM_IOS"],     "iOS",     "iOS"),
])
```

Output topology:

```text
src/SDL3.Core/Generated/
├── ClangSharp/                  ← Platform-agnostic ClangSharp output
│   ├── SDL.g.cs                 ← functions present on all platforms
│   ├── SDL_video.g.cs
│   └── …
├── ClangSharp.Windows/          ← Windows-only symbols + [SupportedOSPlatform("Windows")]
│   └── SDL_system.Windows.g.cs
├── ClangSharp.Linux/
│   └── SDL_system.Linux.g.cs
├── ClangSharp.OSX/
│   └── SDL_system.OSX.g.cs
└── (etc.)
```

Risk: macros that change *struct layout* per platform (e.g., `SDL_SysWMinfo` union) need per-platform handling. ppy's existing config is the closest SDL reference; if we choose CppAst, we still need equivalent per-symbol attribution and merge/dedup logic.

Baseline dedup and conflict rules:

1. Generate the neutral pass first.
2. Generate each platform pass with exactly one platform view active.
3. Exclude symbols already emitted by the neutral pass from platform files.
4. Emit neutral-only symbols into common generated files.
5. Emit platform-only symbols into platform-suffixed files with `[SupportedOSPlatform]`.
6. Fail generation, rather than guessing, if the same symbol appears in multiple views with incompatible signatures or layout-affecting type differences.

### Generated source as a release input

Generated C# binding source should be treated as a versioned release input, not a hidden build-time side effect. The release-grade workflow should be:

1. Update SDL/vcpkg/manifest version inputs.
2. Build or provision native libraries for the target matrix.
3. Run the pinned generator with the pinned libclang/toolchain and canonical headers.
4. Commit the generated `.g.cs` diff for review.
5. In CI, run the generator again and fail if `git diff --exit-code` is not clean.
6. Validate emitted P/Invoke entry points against actual native exports (`dumpbin`, `nm`, `otool`).
7. Run package-consumer smoke tests with real assets.
8. Pack and publish only after generated-source drift, export validation, and runtime smoke pass.

This keeps consumer builds simple: installing `Janset.SDL2.*` packages does not require CppAst/ClangSharp, libclang, Python/PowerShell generator scripts, or platform SDK headers. It also keeps binding diffs reviewable when upstream SDL versions change.

### SDL3 surface — same shape, more APIs

SDL3 adds GPU API, Camera, Storage, Dialog, AsyncIO surfaces — all platform-agnostic at the binding level. Same vcpkg + parse strategy applies; just more headers.

### Satellite headers — separate outputs, shared core type universe

SDL satellites are not independent type islands. Local header inspection shows:

| Satellite | Header relationship | Shared core types observed |
| --- | --- | --- |
| `SDL_image.h` | Includes `SDL.h` and `SDL_version.h` | `SDL_Surface`, `SDL_Texture`, `SDL_Renderer`, `SDL_RWops` |
| `SDL_ttf.h` | Includes `SDL.h` | `SDL_Color`, `SDL_Surface`, `SDL_Renderer`, `SDL_Texture`, `SDL_bool`, `SDL_version` |
| `SDL_mixer.h` | Includes `SDL_stdinc.h`, `SDL_rwops.h`, `SDL_audio.h`, `SDL_endian.h`, `SDL_version.h` | `SDL_RWops`, `SDL_bool`, `SDL_AudioSpec`, `SDL_version` |

Production generation should therefore use a **core-owned shared type universe**:

- `SDL2.Core` owns `SDL_*` core structs, enums, handles, callbacks, and constants.
- Satellite generators emit only satellite-owned surface (`IMG_*`, `Mix_*`, `TTF_*`, `gfx*`/`SDL2_gfx` family symbols) plus any truly satellite-owned structs/enums.
- Satellite signatures reference core-owned managed types instead of regenerating duplicates.
- Validation should fail if a satellite output redefines a core-owned type name or lowers a known core type to an untyped fallback because the type map was missing.

This matches the observable ppy/SDL3-CS package shape: `SDL3_image-CS` is a separate satellite project/package, but it references `SDL3-CS` and its generated `IMG_*` declarations use core types such as `SDL_Surface*`, `SDL_IOStream*`, `SDL_Renderer*`, and `SDL_Texture*`. For our next spike, `SDL2_image` is the best risk slice because it exercises shared core types, satellite-owned functions, image asset smoke tests, and package dependency shape without the larger callback surface of SDL_mixer.

## 4. Hybrid-Static + Symbol Visibility Interaction

### Hybrid-static recap

Per [`vcpkg-overlay-triplets/_hybrid-common.cmake`](../../../vcpkg-overlay-triplets/_hybrid-common.cmake):

- Default library linkage: **static** (transitive deps like zlib, libpng, FreeType are baked into satellite shared libraries).
- SDL family linkage: **dynamic** (we ship `SDL2.dll` / `libSDL2.so` / `libSDL2-2.0.dylib` as dependencies).
- CRT linkage: **dynamic** (preserves ucrtbase / libc interop).
- Unix C/C++ flags: `-fvisibility=hidden` (hides transitive-dep symbols from satellite export tables).
- Linux-only: `VCPKG_FIXUP_ELF_RPATH` (rpath resolution for libSDL2.so at runtime).

### Generated bindings target the satellites

Generated `[LibraryImport(LibName, ...)]` declarations target `SDL2.dll` / `SDL2_image.dll` / `SDL2_mixer.dll` / etc. — the shared satellites we ship. They do NOT target transitive deps (zlib, FreeType, etc.) because those are statically baked. **No conflict with hybrid-static at the bindings layer**.

### Symbol visibility risk for the generator

The risk: AST parsing surfaces every function **declared** in the header. Not every declared function is **exported** from the compiled satellite.

| Platform | Risk level | Why |
| --- | --- | --- |
| Windows | **Low** | PE format is export-opt-in. SDL2 uses `DECLSPEC` macro → `__declspec(dllexport)` for exported symbols. Header-declared but undecorated functions are inline / internal / not exported. AST might emit P/Invoke for them; runtime would fail with `EntryPointNotFoundException`. |
| Linux | **Medium** | `-fvisibility=hidden` hides everything by default. SDL2's `DECLSPEC` expands to `__attribute__((visibility("default")))` for exported symbols. AST sees the declaration; if SDL2 misses a `DECLSPEC` annotation, generator emits P/Invoke for a hidden symbol; runtime fails. |
| macOS | **Medium** | Same mechanism as Linux. macOS two-level namespaces mean satellites don't conflict, but missing exports still fail at consumer call time. |

**PD-15 (SDL2_gfx Unix symbol-export regression)** is a concrete example: SDL2_gfx is third-party, may lack proper `DECLSPEC` annotations on some functions, may export less under `-fvisibility=hidden` than the headers declare. Currently tracked as an open guard item.

### Mitigation — Symbol-existence validation

Post-generation validation: for every emitted `[LibraryImport(LibName, EntryPoint = "FuncName")]`, assert `FuncName` exists in the corresponding satellite's exported symbol table. Cross-platform tooling:

| Platform | Tool | Command |
| --- | --- | --- |
| Windows | `dumpbin /exports` | `dumpbin /exports SDL2.dll` |
| Linux | `nm -D --defined-only` or `readelf --dyn-syms` | `nm -D libSDL2-2.0.so \| grep ' T '` |
| macOS | `nm -gU` | `nm -gU libSDL2-2.0.dylib` |

This validation is a **new guardrail candidate** for the Pack stage (joining G46-G58). Owns the failure mode "generator emits binding for a non-exported function." Detail design lands in the Phase 4 implementation plan.

### Interaction with Phase 2b Linux version scripts

[`../research/symbol-visibility-analysis.md`](../../research/symbol-visibility-analysis.md) records the decision to add Linux version scripts (`.map` files) per satellite in Phase 2b. Once those land, the **exported symbol set tightens further** — only `SDL_*` / `IMG_*` / `Mix_*` / `TTF_*` prefixed symbols stay exported, everything else is `local: *`.

For the AST generator, this **simplifies validation**: the exported set is now glob-pattern-driven (`SDL_*`), so we can statically check generator output against the version script's pattern rather than per-symbol dynamic lookup. Win for repeatability.

## 5. Reference Cross-Check Strategy

### Why cross-check at all

CppAst + manual type-mapping rules can produce subtly wrong signatures: `int` where `SDL_bool` is intended, `IntPtr` where a typed handle struct is intended, `string` where `byte*` is the right wire format. These bugs compile fine and may run fine in simple paths — they surface as `EntryPointNotFoundException` or undefined behavior in edge cases.

**Existing third-party bindings have already typed every SDL function.** Use them as cross-check oracles, not source.

### SDL2 oracle: `external/sdl2-cs`

Already vendored at [`external/sdl2-cs/src/`](../../../external/sdl2-cs/src/). Coverage:

| File | Lines |
| --- | --- |
| `SDL2.cs` | 8,966 |
| `SDL2_image.cs` | 316 |
| `SDL2_mixer.cs` | 665 |
| `SDL2_ttf.cs` | 768 |
| `SDL2_gfx.cs` | 390 |
| **Total** | **11,105** |

Format: hand-written `[DllImport]` declarations with manual UTF-8 marshalling helpers (`Utf8Size`, `Utf8Encode`, `Utf8EncodeHeap`, `UTF8_ToManaged`). Older patterns (no `LibraryImport`, no `delegate*`). License: zlib.

**Cross-check methodology:**

1. For each emitted SDL2 P/Invoke, locate the corresponding sdl2-cs declaration.
2. Diff the function signature: parameter count, parameter types (after normalizing IntPtr ↔ typed handle, byte* ↔ string), return type.
3. Flag mismatches for human review.
4. Capture the diff outcome (match / typed-handle delta / known-quirk / true bug) in a generator-rule audit log.

We are **not** copying sdl2-cs declarations or migrating to its format. We are using it as a typing reference — "the SDL2 community already wrote this signature; if my generator disagrees, one of us is wrong, let's check."

### SDL3 oracle + toolchain reference: ppy/SDL3-CS

[ppy/SDL3-CS](https://github.com/ppy/SDL3-CS) is MIT, auto-generated via ClangSharp + Dockerfile, and covers SDL3 core + SDL_image + SDL_mixer + SDL_ttf — the exact scope we plan for SDL3. It serves a second role as the ClangSharp-pattern reference if that toolchain wins.

This makes cross-check meaningfully tighter: when our generated output for an SDL3 function differs from ppy's, the difference reduces to "different RSP config" rather than "different parser, different emitter, different everything." Diffs become directly actionable — locate the RSP delta, decide whether their choice or ours is right.

Modern format: ClangSharp + RSP-driven, `[DllImport]` by default in ppy's current config, `[NativeTypeName]`-attributed, `delegate*` callbacks. The SDL2_gfx spike found `LibraryImport` requires analyzer auto-fix, post-processing, or another explicit conversion path. Cross-check methodology mirrors SDL2: locate corresponding declaration, diff, flag mismatches.

### Caveat — Reference projects may be wrong too

sdl2-cs has known issues (it's POC-grade, "untrusted for production" per [`AGENTS.md`](../../../AGENTS.md)). ppy/SDL3-CS is high-quality but generator-emitted bindings can contain edge cases. Cross-check tells us **which signatures need human review**, not **which signatures are correct by definition**.

Triangulation against multiple oracles + the C header itself produces a stronger signal than any single oracle. For SDL3 cross-check, also consult Alimer.Bindings.SDL (SDL3 core only) as a secondary reference.

## 6. Manual Intervention Surface Area

**Toolchain note:** Surface is calibrated against the **ppy/SDL3-CS pattern (ClangSharpPInvokeGenerator)** post-2026-05-12 flip. Reference: ppy's generator is ~32 KB total (16 KB C# Roslyn source-gen + 14 KB Python orchestrator + 3 KB RSP config) for SDL3 + Image + TTF + Mixer scope. We port the Python piece to PowerShell + extend RSP for SDL2 satellites; total surface estimate ~40-60 KB.

ClangSharp does substantially more out of the box than CppAst would — type mapping defaults, `[NativeTypeName]` attribution, doc-pass-through (Roslyn extension), `[SupportedOSPlatform]` via RSP `--with-attribute`. Manual surface shrinks compared to a CppAst-custom-emitter approach.

### Type mapping rules (~30–60 RSP `--remap` / `--with-type` lines)

Examples (RSP syntax for ClangSharpPInvokeGenerator):

- Boolean wire types: SDL2 `SDL_bool` → int-backed enum/wrapper; SDL3 bool-like values → byte-backed wrapper (Rule 5). Do not use a single `SDLBool` remap across both families.
- Numeric type fixes: `--remap Uint8=byte`, `--remap Sint64=long`, `--with-type SDL_AudioFormat=uint` (Rule 3)
- `const char *` UTF-8 handling: handled via ClangSharp's `byte*` default + a Roslyn extension (ppy's `FriendlyOverloadGenerator`) that synthesizes `string` overloads at consumer compile time (Rule 4)
- Opaque pointer recognition: ClangSharp emits empty `partial struct SDL_Window { }` automatically from `typedef struct SDL_Window SDL_Window`; we extend with a Roslyn source generator pass that wraps it as the value-type handle pattern from Rule 2 (or accept ppy's raw-pointer style and revisit at Phase 4)
- Callback typedef: ClangSharp's `latest-codegen` emits `delegate* unmanaged[Cdecl]<...>` automatically (Rule 7)
- Bit-flag enum attribution: `--with-attribute SDL_WindowFlags=Flags` per type-name; ClangSharp does NOT auto-detect bit-flag intent (neither does any tool — see §approaches doc 2026-05-12 source-level comparison)
- Per-field struct overrides: family-specific boolean override (`--with-type SomeStruct.someField=<bool-wire-type>`) when ClangSharp cannot infer the desired wrapper.

### Function-specific overrides

Examples:

- `SDL_Log(const char *fmt, ...)` — variadic. Emit as accepting a single pre-formatted string; document that variadic native call from C# requires manual stack manipulation.
- `SDL_RWFromFile` and friends (deprecated in SDL3; replaced by `SDL_IOStream`) — generator emits both for SDL2, only `SDL_IOStream` for SDL3.
- `SDL_GetError` — already covered by Rule 4 callee-owned return; lift to a wrapper method on top.
- `SDL_SetMainReady` / `SDL_main` — Windows entry-point quirk; manually exclude or emit no-op for cross-platform consumers.

### Constants (CppAst limitation)

CppAst surfaces `#define` macros as `CppMacro`. **Computed-expression macros** (e.g., `#define SDL_AUDIO_FRAMESIZE(x) ((x).format_size * (x).channels)`) cannot be easily emitted as C# constants. Manual override needed for ~20–40 of these per family.

### Header pre-processing

Some SDL headers use idiomatic C that CppAst handles imperfectly: `SDL_FORCE_INLINE` macros, varargs with specific platform attributes, packed structs with explicit alignment requirements. Manual review post-parse.

### Estimated auto/manual split (ClangSharp pattern)

| Category | Auto (ClangSharp default + RSP) | Manual (RSP overrides / Roslyn ext / hand-written) |
| --- | --- | --- |
| Function P/Invoke declarations | ~95% | ~5% (variadic, deprecated, excludes) |
| Struct definitions | ~95% | ~5% (packed/unions with platform variance) |
| Enums | ~90% | ~10% (`[Flags]` attribution via `--with-attribute`, underlying type fixes) |
| Constants | ~80% | ~20% (computed macros via `generate-macro-bindings` + manual handling for genuinely-computed values) |
| Opaque handle types | ~70% (raw `partial struct` default) | ~30% if upgrading to value-type wrappers via Roslyn source-gen extension |
| Callback typedefs | ~95% (`delegate*` automatic) | ~5% (lifetime-edge-case overrides) |
| String marshalling overloads | ~50% (ClangSharp emits `byte*`; friendly overloads need Roslyn ext like ppy's `FriendlyOverloadGenerator`) | ~50% (Roslyn extension is one-time write, then automatic) |
| Multi-platform attribution | ~95% (RSP `--with-attribute SDL_xxx=SupportedOSPlatform("Windows")` per-symbol) | ~5% (manual review of platform-conditional union types) |
| **Overall weighted** | **~90%** | **~10%** |

Manual surface is ~10% — smaller than the ~15% estimate under the prior CppAst recommendation. Locus also shifts: under CppAst, manual surface lived in **C# emitter code** (rebuild generator per change); under ClangSharp, manual surface lives in **RSP config + one-time Roslyn extension** (edit, re-run, no rebuild).

Captured in a generator-rule audit log (per Phase 4 plan). Subsequent SDL version bumps (regenerate from new headers) re-hit ~3% of that 10% on average — most rules are stable.

## 7. Testing Strategy — Multi-Layer

No single test layer catches all binding bugs. Use defense-in-depth.

### Layer 1 — Compile-time gate

Every generated `.cs` file must compile under all target TFMs (`net10` / `net9` / `net8` / `netstandard2.0` / `net462`). CI step: `dotnet build src/SDL2.Core/SDL2.Core.csproj` and equivalents. Catches: invalid syntax, missing using-directives, type mapping errors that produce undefined symbols.

Cost: ~free (it's a build step). Coverage: structural correctness only.

### Layer 2 — Public API surface snapshot

Use [Verify](https://github.com/VerifyTests/Verify) (already available — `dotnet-skills:snapshot-testing` skill) to snapshot every public type, method, property, enum, constant. Diff on every regeneration; human review of the diff.

```csharp
[Test]
public async Task SDL2_PublicSurface_Snapshot()
{
    var assembly = typeof(SDL2.SDL).Assembly;
    var surface = PublicApiGenerator.GeneratePublicApi(assembly);
    await Verifier.Verify(surface);
}
```

Catches: accidental API breakage, unintended signature changes, deleted/added public members. Cost: one CI step + occasional `*.received.txt` review.

### Layer 3 — Reference cross-check diff

Per §Reference Cross-Check, programmatic diff between generated signatures and sdl2-cs (SDL2) / ppy/SDL3-CS (SDL3) signatures. Tool: a small `build/_build` task that reads both sets, emits a categorized diff report (match / typed-handle-delta / parameter-count-mismatch / known-quirk / true-bug).

Run cadence: per regeneration. Output: a markdown report under `artifacts/binding-audit/<date>/`. Reviewed before merge.

Catches: type-mapping bugs, parameter-count mismatches, missed callback typedefs. Cost: one-time tool implementation (~3-5h), per-run automated.

### Layer 4 — Symbol-existence validation

Per §4, post-generation validation: every emitted `[LibraryImport(EntryPoint = "Foo")]` has a corresponding `Foo` in the satellite's export table. Cross-platform via `dumpbin` / `nm` / `otool`.

New Pack-stage guardrail candidate. Owns the failure mode "binding references unexported symbol." Output: a Pack-stage assertion alongside G46-G58.

Catches: bindings for declared-but-not-exported functions (PD-15 risk class). Cost: one-time guardrail implementation (~4-6h), per-Pack run automated.

### Layer 5 — Reproducibility check

Regenerate from same headers + same generator version → byte-identical output. Snapshot the generated `.cs` files in git; if regeneration produces a diff, either headers changed (legitimate) or generator is non-deterministic (bug — investigate).

Implementation: CI step that runs the generator + compares against committed output. Cost: one-time gate (~2h), per-CI-run automated.

Catches: non-deterministic generation (dictionary iteration order, hash-set ordering), stale committed output. Eliminates a whole class of "works on my machine" generator bugs.

### Layer 6 — Runtime smoke (existing NativeSmoke + ConsumerSmoke)

Already in place. NativeSmoke loads each satellite, calls a representative function, asserts the runtime payload extracts correctly. ConsumerSmoke restores generated packages, runs TUnit per TFM, exercises the binding surface.

For the generator-output layer specifically, extend ConsumerSmoke with TUnit cases that exercise:

- `SDL_Init` + `SDL_Quit` (core lifecycle).
- `SDL_CreateWindow` + `SDL_DestroyWindow` with both UTF-8 input forms (`string`, `ReadOnlySpan<byte>`).
- `SDL_GetError` after a forced failure (callee-owned UTF-8 return).
- `SDL_SetEventFilter` with a `[UnmanagedCallersOnly]` callback (function pointer Rule 7).
- `IMG_LoadTexture` / `Mix_LoadWAV` / `TTF_OpenFont` (per-satellite minimal exercise).

Cost: ~1-2h per family for the smoke harness. Output: green-or-red signal per RID per TFM, already wired into CI.

### Layer 7 — Semantic / behavior testing

Compiled-correct ≠ semantically-correct. Some bugs (struct field offset wrong, callback marshalling subtly broken, threading model violated) surface only under real usage.

Three sources for this layer:

1. **learning-sdl2** (Deniz's external consumer) — already serves as a real-world consumer running real SDL apps. After every public-feed wave, `learning-sdl2` validates the wave against actual rendering / audio / font scenarios.
2. **Manual play-test pass** — occasional human-driven exercise. Cadence at maintainer discretion; recommended before each `-rc.N` promotion per [`release-strategy.md`](../../release-strategy.md) §Promotion Gates.
3. **Sample applications** under `samples/` (planned per [`plan.md`](../../plan.md) Phase 3) — small SDL programs that exercise common patterns. Build + smoke-run gates on CI.

### Test layer summary

| Layer | Cost (impl) | Coverage | Cadence |
| --- | --- | --- | --- |
| 1. Compile-time gate | Free | Structural | Every CI run |
| 2. Public API snapshot | 1-2h | API surface drift | Every regeneration + every CI run |
| 3. Reference cross-check | 3-5h | Type mapping correctness | Every regeneration |
| 4. Symbol-existence validation | 4-6h | Header→export consistency | Every Pack run (new guardrail) |
| 5. Reproducibility | 2h | Generator determinism | Every CI run |
| 6. Runtime smoke (extend existing) | 1-2h × 5 families | Loading + minimal calls | Every Pack consumer-smoke matrix |
| 7. Semantic / behavior | learning-sdl2 (external) + manual pass | Real-usage correctness | Before each -rc.N + ad-hoc |

Total upfront tooling cost: ~15-25h focused work. Per-regeneration cost: zero (all automated).

## 8. Provisional ClangSharp-Pattern Effort Estimate

**Recalibrated 2026-05-12 against the ClangSharp/ppy pattern instead of CppAst/Alimer.** This is a comparison point, not the final Phase 4 estimate. Reference points: ppy/SDL3-CS's generator surface is ~32 KB total; a ClangSharp path would port the Python orchestrator to PowerShell + extend RSP coverage to SDL2 satellites; the heavy lifting (parsing + emission) lives in ClangSharpPInvokeGenerator which we don't maintain.

| Stage | Generator work | Testing infrastructure | Total focused | Calendar (hobby) |
| --- | --- | --- | --- | --- |
| Stage 1 — proof-of-life (SDL2.Core via ClangSharp, end-to-end build) | 0.5–1 week | Layers 1, 2, 5 set up (~5h) | ~1 week | 0.5–1.5 months |
| Stage 2 — SDL2 sweep (extend RSP to all 5 SDL2 satellites + retire `external/sdl2-cs`) | 1–2 weeks | Layers 3, 4 set up (~10h), Layer 6 per family | ~2–3 weeks | 1.5–3 months |
| Stage 3 — SDL3 extension (RSP coverage for SDL3 + Image + Mixer + TTF) | 1–2 weeks | Layer 6 per SDL3 family, Layer 7 cadence | ~1.5–2.5 weeks | 1.5–2.5 months |
| **Total** | **~3–5 weeks generator** | **~15–25h testing infra (front-loaded)** | **~5–8 weeks focused** | **3.5–7 months at typical hobby cadence** |

Total drops from prior CppAst estimate (~8–10 weeks focused / 5–9 months calendar) to **~5–8 weeks focused / 3.5–7 months calendar**. The ~3 week / 1.5 month savings come from not writing + maintaining a custom emitter — ClangSharp + ppy's pattern do that work for us.

[`release-strategy.md`](../../release-strategy.md) §Effort Calibration estimates total work across all 5 stages at **~8–14 weeks focused / ~10–17 months calendar**, of which the Phase 4 portion (stages 1–3 — proof-of-life, SDL2 sweep, SDL3 extension) is **~5–8 weeks focused / ~3.5–7 months calendar**. The ClangSharp-pattern analysis above lands on the same Phase 4 number; the two docs agree once Phase 4 is separated from Stabilization + Big Bang. Verified 2026-05-14.

## 9. Open Decisions and Risks

### Open decisions (Phase 4 implementation plan owns these)

| # | Decision | Default tendency |
| --- | --- | --- |
| D1 | Vendor SDL headers in repo vs read from vcpkg installation tree | **Read from vcpkg** — keeps version anchor authoritative; vendoring duplicates source-of-truth |
| D2 | Multi-platform parsing strategy | **Single pinned generation host + multiple parse views first.** Follow ppy/SDL3-CS's neutral + platform-specific pass pattern using either ClangSharp invocations/RSP files or CppAst parser-option passes. Keep true multi-OS extraction + merge as an escalation path if controlled parse views cannot model a header correctly. Single-pass-with-multiple-defines (Alimer pattern) is a risk because it creates an impossible union target and can mis-attribute or miss OS-specific symbols across our 7-RID matrix. |
| D3 | Reference cross-check tool | **Lightweight `dotnet` script** in `build/_build` that locates corresponding declaration in `external/sdl2-cs` (SDL2) / cloned-`ppy/SDL3-CS` (SDL3), diffs signatures, emits a markdown report categorized as match / typed-handle delta / parameter-count mismatch / known-quirk / true-bug. Because we share toolchain with ppy/SDL3-CS for SDL3, diffs reduce to "different RSP config" — tractable. |
| D4 | Symbol-existence validation as Pack-stage guardrail | **Yes** — joins G46-G58 with a behavior-first name and IDs assigned at the guardrail-catalog refresh. Cross-platform via `dumpbin /exports` (Windows), `nm -D --defined-only` (Linux), `nm -gU` (macOS). |
| D5 | `<IsAotCompatible>true</IsAotCompatible>` on all generated csprojs | **Yes** — exact emission mechanism depends on the selected toolchain, but AOT-readiness should be signaled from day one. |
| D6 | Computed-expression macros — emit as `static readonly` or skip | **Validate both toolchains** — at SDL2_gfx scope, ClangSharp's `--config generate-macro-bindings` and CppAst's ~30-line custom `TryEmitMacroConstant` both capture the same 8 numeric `#define` macros (verified 2026-05-14; see [`binding-autogen-spike-findings.md`](binding-autogen-spike-findings.md) §7.4). Open question: how each toolchain handles genuinely computed expressions (e.g., function-style macros, expression-bodied macros) on larger SDL headers. Manual override list remains the fallback. |
| D7 | Variadic functions (`SDL_Log`, etc.) | **Preserve `__arglist` per ppy pattern** (information retained, true variadic available via IL helper if needed). Hand-write fmt-only wrappers in the consumer-facing partial class for ergonomics — best of both: lossless raw layer + ergonomic wrapper. |
| D8 | Should generator output get checked into git, or generated on build? | **Checked into git with CI drift check** — same committed-source shape as ppy/SDL3-CS and Alimer.Bindings.SDL, plus a release guard that regenerates and fails on dirty diff. Allows code review of binding diffs, simplifies consumer build (no generator tool transitively), and survives offline build scenarios. |
| D9 | Multi-TFM emission strategy (`net10/net9/net8/netstandard2.0/net462`) | **Spike both shapes before deciding** — ClangSharp likely needs post-processing or multi-pass merge; CppAst can emit conditional branches directly from one emitter. Also revisit whether `net462` / `netstandard2.0` remain in scope. |
| D10 | Friendly overload generation strategy | **Undecided** — if ClangSharp wins, fork or adapt ppy's MIT-licensed Roslyn `FriendlyOverloadGenerator`; if CppAst wins, emit friendly overloads offline from the custom emitter. |
| D11 | Python orchestrator vs PowerShell | **PowerShell** — keeps us in-ecosystem with existing `tools.cs` + Cake build host. Translation of ppy's `generate_bindings.py` is mechanical (~14 KB Python → ~10-15 KB PowerShell). Risk: PowerShell on macOS/Linux runners works but is less mature than Python; if pain surfaces during spike, fall back to Python without ideological resistance. |

### Risks

| Risk | Likelihood | Mitigation |
| --- | --- | --- |
| SDL header drift between vcpkg-installed and upstream introduces type-mapping surprises mid-stage | Medium | Snapshot test on header content (Layer 5 reproducibility extends to "header SHA + ClangSharp version + RSP SHA → output hash") |
| ClangSharp chokes on a specific SDL header idiom (variadic + `__attribute__` combination, complex unions) | Low-medium | ClangSharp battle-tested on Win32 + Vulkan + DirectX surfaces, larger than SDL. Fallback: RSP `--exclude` the specific function + hand-write in a separate `.cs` file |
| libclang version skew between dev machine and CI produces non-deterministic output | Medium | Pin `ClangSharpPInvokeGenerator` tool version in `dotnet-tools.json`; pin libclang runtime NuGet versions; Layer 5 reproducibility validates byte-identical output |
| Symbol visibility regressions in upstream SDL minor bumps (PD-15 class) | Medium-low | Layer 4 symbol-existence validation catches at Pack time; no consumer-facing leak |
| Generator output non-determinism shows up only on a different OS/runtime | Low | Layer 5 reproducibility check runs on Linux + Windows + macOS CI runners |
| Reference cross-check oracle (sdl2-cs / ppy) has a wrong signature we're "validating" against | Medium | Triangulation: cross-check against multiple oracles + the C header; bug-list maintained as known-quirks |
| Multi-TFM `LibraryImport` + `DllImport` dual emission produces subtle behavioral divergence | Low-medium | TUnit consumer smoke runs on every TFM independently (existing). D9 spike validates the post-processing approach before commit. |
| PowerShell orchestrator (D11) hits a portability snag on Linux/macOS runners | Low-medium | Fall back to Python (ppy's choice) if PowerShell pain surfaces — no ideological commitment, mechanical translation either way |
| ppy's `FriendlyOverloadGenerator` Roslyn extension doesn't fit our multi-TFM model cleanly | Medium | D10 fork-and-adapt path planned; worst case we write the friendly-overload pass ourselves (~200-400 lines Roslyn) |
| Phase 4 effort overruns by 2x | Low (down from prior Medium) | ClangSharp pattern is smaller surface than CppAst pattern; ppy/SDL3-CS as working reference de-risks integration. Per [`release-strategy.md`](../../release-strategy.md), hobby cadence already accommodates worst-case 2-3x calendar variance |
| Generator becomes maintenance burden (the case where CppAst migration would be considered) | Low | If RSP grammar can't express something we need at SDL scope, it's edge-case enough to handle in a Roslyn extension pass. Migration to CppAst would be considered if/when we decide to ship a Skia-style curated public API on top of raw bindings — not before. |

## 10. Pending Discussion Threads — WHY/HOW/WHAT Cycle

Four discussion threads deferred from this feasibility study, queued for the Phase 4 implementation plan's WHY/HOW/WHAT cycle. Each is a thread to open, not a question with a default answer. Captured here so future-Phase-4-plan-authoring picks them up.

### 10.1 — Open Decisions D1–D11 Triage

§9 enumerates 11 open decisions. The Phase 4 plan-authoring slice works through them. Structural ones first (these shape the generator skeleton): **D1** (vendor SDL headers in repo vs read from vcpkg installation tree — reproducibility, version pinning, offline build implications), **D2** (multi-platform parsing strategy — default tendency is neutral + platform-specific passes per ppy/SDL3-CS pattern; confirm against our 7-RID matrix before committing), **D8** (generated output check-in — both ppy and Alimer check generated code into git; revisit whether that's right for our test/CI scenarios). D3–D7 + D9–D11 fold in as supporting decisions.

### 10.2 — Modern P/Invoke Deep-Dive (§2 emit rules)

Several rules deserve standalone design conversations:

- **SDL2/SDL3 boolean wire types (Rule 5).** Layout, implicit conversion ergonomics, AOT cost, compatibility with `bool` in struct fields. SDL2 `SDL_bool` must remain int-backed; SDL3 bool-like values can use a 1-byte wrapper similar to Alimer's `[StructLayout(Size=1)] readonly struct SDLBool(byte)`.
- **`delegate*` callback lifetime patterns (Rule 7).** SDL retains callback pointers across frames; the `[UnmanagedCallersOnly]` static-method pattern is rooting-free but constrains the caller. Do we ship rooting helpers for instance-bound closures, or document the static-method-only constraint?
- **Multi-TFM strategy (Rule 1).** Currently planning `net10 / net9 / net8 / netstandard2.0 / net462`. Question: is `net462` genuinely required, or does the baseline shift to `net8` (drops `LibraryImport`/`DllImport` dual-emission complexity, drops `System.Memory` polyfill on netstandard2.0)? TFM scope decision affects RSP config complexity, testing surface, and consumer ecosystem reach.
- **`[Flags]` attribution for bit-flag enums (§approaches doc finding).** Neither ClangSharp nor CppAst auto-detects bit-flag intent on `Uint64 + #define`-style declarations. Where does `[Flags]` live — RSP per-type-name overrides (`--with-attribute SDL_WindowFlags=Flags`), or hand-written wrapper enum at the consumer layer (Alimer's approach for `SDL_WindowFlags`)?

### 10.3 — Testing Strategy Deep-Dive (§7 7-layer strategy)

Layer prioritization order — all 7 are valuable but land in stages. Specific design threads to anchor at Phase 4 plan time:

- **Layer 4 design — symbol-existence validation as new Pack-stage guardrail.** Where does it live under `build/_build/Validation/`? Error message shape (filename, function name, target satellite, cross-platform tool output)? Integration with G46–G58 (joins as new entry or extends an existing one)? Behavior-first naming convention for the `release-guardrails.md` catalog entry?
- **Layer 7 — Semantic / behavior testing coverage gap.** `learning-sdl2` covers real-usage scenarios maintainer cares about. Gap: code paths `learning-sdl2` doesn't exercise. Do we want sample apps under `samples/` (per [`plan.md`](../../plan.md) Phase 3) to anchor specific scenarios (audio playback, font rendering, image loading, GPU API surface for SDL3)?

### 10.4 — Reference Cross-Check Tool Design (§5 + §7 Layer 3)

§5 + §7 Layer 3 sketch the methodology but don't design the tool. Open design questions:

- **Where does it live?** `build/_build` Cake task (joins existing target-centric structure), `tools.cs` subcommand (closer to dev workflow), or standalone `dotnet` script under `tools/`?
- **Input format.** Read `external/sdl2-cs` source files live + clone `ppy/SDL3-CS` to a known cache location, or pre-extract signature manifests at known cadence?
- **Diff algorithm.** Per-function lexical diff vs semantic diff (normalize `IntPtr` ↔ `nint` ↔ typed-handle-struct equivalences). Lexical is simpler but produces lots of false positives; semantic requires a small type-equivalence rule set.
- **Output format.** Markdown report under `artifacts/binding-audit/<date>/` (per §7 Layer 3 sketch), categorized as match / typed-handle-delta / parameter-count-mismatch / known-quirk / true-bug? Terminal-only? Both?
- **Triage workflow.** When the diff surfaces a real bug, what's the fix mechanism — RSP edit + regenerate, or manual override list, or hand-patched in a non-generated `.cs` file?
- **False-positive handling.** sdl2-cs uses `IntPtr` where we use typed handle structs; ppy preserves `[NativeTypeName]` attributes we may drop. The tool must normalize known structural differences away or category-label them explicitly so they don't drown out the real signal.

## 11. Cross-Reference

- [`binding-autogen-approaches.md`](binding-autogen-approaches.md) — tool comparison, industry survey, decision matrix (the companion to this doc)
- [`../research/symbol-visibility-analysis.md`](../../research/symbol-visibility-analysis.md) — hybrid-static symbol leakage analysis; Layer 4 validation builds on this
- [`../release-strategy.md`](../../release-strategy.md) — Stage 1-5 sequencing, effort calibration, promotion gates
- [`../phases/phase-4-binding-autogen.md`](../../phases/phase-4-binding-autogen.md) — Phase 4 design brief; this feasibility informs the eventual Phase 4 implementation plan
- [`../phases/phase-5-sdl3-support.md`](../../phases/phase-5-sdl3-support.md) — Phase 5 SDL3 brief; AST generator extends to SDL3 in Stage 3
- [`../knowledge-base/release-guardrails.md`](../../knowledge-base/release-guardrails.md) — guardrail catalog; symbol-existence validation (Layer 4) is a new candidate
- [`../knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — canonical test infrastructure; Layers 1-6 use existing TUnit + FakeCakeWorld + ConsumerSmoke seams
- [`../../AGENTS.md`](../../../AGENTS.md) — operating rules; `external/sdl2-cs` is transitional, retires when the AST-generated binding surface ships

## Sources Cited

- [Microsoft Learn — P/Invoke source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation)
- [Microsoft Learn — Native interop best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)
- [Microsoft Learn — Custom marshalling source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/custom-marshalling-source-generation)
- [Microsoft Learn — Native code interop with Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/interop)
- [dotnet/runtime — LibraryImportGenerator compatibility doc](https://github.com/dotnet/runtime/blob/main/docs/design/libraries/LibraryImportGenerator/Compatibility.md)
- [Stephen Toub — Improvements in native code interop in .NET 5.0](https://devblogs.microsoft.com/dotnet/improvements-in-native-code-interop-in-net-5-0/)
- [Silk.NET — Opaque handle / NativeAOT story (2.17 release notes)](https://dotnet.github.io/Silk.NET/blog/apr-2023/silk2170/)
- [Silk.NET 3.0 generation proposal](https://github.com/dotnet/Silk.NET/blob/main/documentation/proposals/Proposal%20-%20Generation%20of%20Library%20Sources%20and%20PInvoke%20Mechanisms.md)
- [Alimer.Bindings.SDL generator source](https://github.com/amerkoleci/Alimer.Bindings.SDL/tree/main/src/Generator)
- [Alimer.Bindings.SDL Handles.cs](https://github.com/amerkoleci/Alimer.Bindings.SDL/blob/main/src/Alimer.Bindings.SDL/Generated/Handles.cs)
- [Alimer.Bindings.SDL Commands.cs](https://github.com/amerkoleci/Alimer.Bindings.SDL/blob/main/src/Alimer.Bindings.SDL/Generated/Commands.cs)
- [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS)
- [xoofx/CppAst](https://github.com/xoofx/CppAst)
- [Meziantou — Stop using IntPtr for system handles](https://www.meziantou.net/stop-using-intptr-for-dealing-with-system-handles.htm)
- [C# 13 — ref struct interfaces](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-13.0/ref-struct-interfaces)
- [Microsoft Learn — SYSLIB1054 analyzer reference](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib1050-1069)
