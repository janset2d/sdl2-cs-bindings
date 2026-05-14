# Binding Autogen Spike Findings — SDL2_gfx (ClangSharp + CppAst)

**Date:** 2026-05-12
**Branch:** `spike/binding-autogen-sdl2-gfx`
**Companion docs:**
- [`binding-autogen-approaches.md`](binding-autogen-approaches.md) — toolchain evidence (CppAst → ClangSharp research recommendation, not final decision)
- [`binding-autogen-feasibility.md`](binding-autogen-feasibility.md) — design feasibility (emit rules, testing strategy, open decisions)
- [`../release-strategy.md`](../release-strategy.md) — AST-first stage sequencing

This document captures the **hands-on findings from running BOTH the ClangSharp+RSP pipeline and the CppAst-custom-emitter pipeline on SDL2_gfx**, plus deep-research on toolchain ecosystem practices. Both spikes ran on the same branch + same SDL2_gfx surface for apples-to-apples comparison. The earlier toolchain flip recommendation (CppAst → ClangSharp) was based on industry survey + source-level comparison of ppy/SDL3-CS vs Alimer.Bindings.SDL; this spike validates the evidence, not a final project decision.

## 1. Spike Goal + Scope

**Goal:** Validate BOTH toolchains end-to-end on a small SDL2 satellite (SDL2_gfx, ~100 functions, no callbacks, no opaque-handle ceremony), measure friction + output quality apples-to-apples, surface real-world gotchas before Phase 4 implementation plan.

**In scope (both toolchains, parallel):**

- **ClangSharp side** (§2-§4): install `ClangSharpPInvokeGenerator` as project-local tool, write RSP, generate, compile.
- **CppAst side** (§7): create C# console generator project, NuGet CppAst dep, custom emitter walking AST, generate, compile.
- Both ship raw P/Invoke bindings for SDL2_gfx's 4 headers, compile clean to a standalone .NET 10 library.
- Document friction points + resolutions for each side.
- Side-by-side comparison: hand-written `external/sdl2-cs/src/SDL2_gfx.cs` vs ClangSharp output vs CppAst output.
- Validate (or invalidate) the earlier flip-to-ClangSharp recommendation against our own hands-on data.

**Out of scope (deferred to subsequent spike sub-stages or Phase 4 plan):**

- Test app + actual native call (`Janset.SDL2.Core` package + `Janset.SDL2.Gfx.Native` package + Program.cs port of `giroletm/SDL2_gfx/test/testframerate.c`)
- Friendly overload Roslyn source generator (ppy `FriendlyOverloadGenerator` pattern fork)
- LibraryImport conversion (analyzer-based or post-process, see §5.1)
- Multi-platform parsing (RSP per-RID pass, ppy pattern)
- Multi-TFM emission (`net10/net9/net8/netstandard2.0/net462` dual-emission)

## 2. What We Set Up — ClangSharp Side

### Directory structure

```
tools/binding-spike/clangsharp/
├── .config/dotnet-tools.json     # ClangSharpPInvokeGenerator 21.1.8.3 (local)
├── generator/
│   └── sdl2-gfx.rsp               # 43-line declarative config (verified 2026-05-14)
└── bindings/
    ├── Spike.Bindings.SDL2_gfx.csproj  # net10, IsAotCompatible=true, analyzers off for spike
    ├── NativeTypeNameAttribute.cs       # hand-written ClangSharp helper (consumer responsibility)
    ├── Constants.cs                     # M_PI duplicate workaround
    └── Generated/
        └── SDL2_gfx.g.cs                # 361 lines, 102 P/Invoke + 1 struct + 8 const (verified 2026-05-14)
```

### Final RSP shape (working config, post-iteration)

```text
--file
<absolute path>/SDL2_gfxPrimitives.h
<absolute path>/SDL2_framerate.h
<absolute path>/SDL2_imageFilter.h
<absolute path>/SDL2_rotozoom.h

--include-directory
<absolute path>/vcpkg_installed/x64-windows-hybrid/include

--methodClassName  SDL_gfx
--namespace        Janset.Spike.SDL2.Gfx
--libraryPath      SDL2_gfx
--output           <absolute path>/bindings/Generated/SDL2_gfx.g.cs

--config
latest-codegen
generate-macro-bindings
log-exclusions

--define-macro
SDL_DECLSPEC=
__PRFCHWINTRIN_H=1

--remap
SDL_Renderer*=IntPtr
SDL_Surface*=IntPtr
SDL_Texture*=IntPtr

--exclude
M_PI

--traverse
<absolute path>/SDL2_gfxPrimitives.h
<absolute path>/SDL2_framerate.h
<absolute path>/SDL2_imageFilter.h
<absolute path>/SDL2_rotozoom.h
```

### Generated output volume

| Metric | Value |
|---|---|
| Generated `.cs` lines | 361 |
| P/Invoke functions | 102 |
| Struct definitions | 1 (`FPSmanager`) |
| Auto-emitted constants | 8 (`SDL2_GFXPRIMITIVES_MAJOR/MINOR/MICRO`, `FPS_UPPER_LIMIT/LOWER_LIMIT/DEFAULT`, `SMOOTHING_OFF`, `SMOOTHING_ON`) |
| Hand-written supplement constants | 1 (`M_PI` — dup workaround) |
| C-type-fidelity annotations (`[NativeTypeName]`) | applied to every parameter |
| Calling convention | `Cdecl` + `ExactSpelling = true` |

> Constants count verified 2026-05-14 against current `bindings/Generated/SDL2_gfx.g.cs`. An earlier draft of this doc reported only 2 captured constants based on a pre-`generate-macro-bindings` RSP iteration; the final RSP (with `--config generate-macro-bindings`) captures all 8. See §7.4 for the revised macro-capture comparison.

### Total LoC owned (config + hand-written supplement)

| File | Lines | Purpose |
|---|---|---|
| `sdl2-gfx.rsp` | 44 | Generator config — declarative, edit + re-run |
| `Spike.Bindings.SDL2_gfx.csproj` | 30 | Compile shape, analyzer relaxation for spike |
| `NativeTypeNameAttribute.cs` | 20 | ClangSharp helper attr — consumer responsibility (ppy/terrafx pattern) |
| `Constants.cs` | 10 | M_PI manual supplement (duplicate-define workaround) |
| `.config/dotnet-tools.json` | 13 | Tool pin (ClangSharpPInvokeGenerator 21.1.8.3) |
| **Total** | **~117** | All durable; regenerate-on-SDL-bump only re-runs RSP |

## 3. Iteration Log — ClangSharp Side (Friction → Resolution)

Five iterations to green build. Each captured as both a finding for future reference + a delta against the prior spike-plan in `binding-autogen-feasibility.md` §9.

| # | Friction | Symptom | Resolution |
|---|---|---|---|
| 1 | **Relative paths in RSP don't resolve** | "No such file or directory" on all 4 headers | RSP paths are interpreted relative to **invocation cwd**, not RSP file location. Switched to absolute paths. Future: use a wrapper script that pwd-prefixes paths so RSP stays portable. |
| 2 | **`_m_prefetch` builtin conflict in SDL_endian.h** | `error: definition of builtin function '_m_prefetch'` blocks 3 of 4 headers | SDL_endian.h has a clang+MSVC fallback block defining `_m_prefetch` that conflicts with libclang's builtin (clang ≥ 11). Bypass via `--define-macro __PRFCHWINTRIN_H=1` which pre-satisfies the include guard. |
| 3 | **`--remap` syntax mismatch** | `SDL_Renderer *=IntPtr` (with space) silently dropped; output still emitted `SDL_Renderer*` raw pointers | Use `SDL_Renderer*=IntPtr` (no space before `*`). Documented ppy patterns mix both syntaxes; only no-space form works reliably for typed pointers. |
| 4 | **`NativeTypeNameAttribute` undefined** | 1105 × `CS0246: NativeTypeNameAttribute could not be found` | ClangSharp emits `[NativeTypeName(...)]` everywhere but expects consumer to provide the attribute definition. ppy/terrafx/CsWin32 all hand-roll a small file. Added `NativeTypeNameAttribute.cs` mirroring their pattern, `[Conditional("DEBUG")]`-stripped in Release. |
| 5 | **Duplicate macro definitions** | `error CS0102: 'SDL_gfx' already contains a definition for 'M_PI'` | Both `SDL2_gfxPrimitives.h` AND `SDL2_rotozoom.h` define `#define M_PI 3.14...`. ClangSharp emits both with `generate-macro-bindings` enabled, collides in single output. Resolved via `--exclude M_PI` + manual `Constants.cs` supplement. **Generalizable signal:** any cross-header constant duplication needs this pattern. |
| 6 | **SDK default Compile glob + explicit Compile = duplicate** | `warning CS2002: Source file specified multiple times` + `CS0102` on every const | SDK auto-includes `**/*.cs`; explicit `<Compile Include="Generated/SDL2_gfx.g.cs">` causes dup. Resolved by removing explicit include + relying on SDK glob. **Implication for production:** if generated output lives in a subfolder, the SDK glob still catches it — no special wiring needed. |

### Things that DIDN'T work (rejected paths)

- `--config generate-helper-types` — supposed to auto-emit `NativeTypeNameAttribute`. In 21.1.8.3 it produces nothing extra in our setup; manual file still needed. Possibly works only when paired with other flags or version-specific.
- `--config generate-file-scoped-namespaces` — generates malformed output (`CS1022 brace mismatch`). Reverted; sticking with traditional `namespace { … }` block syntax.
- Adding `--additional --target=x86_64-pc-windows-msvc -fms-compatibility` to fix MMX issue — `--additional` consumed only one arg, rest fell through as positional file paths (interpreted as inputs); would need `--additional` prefix per flag. Bypassed by `__PRFCHWINTRIN_H=1` define instead, which is more targeted.

## 4. Output Quality Observations — ClangSharp Side

### Per-function shape

```csharp
[DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
public static extern int pixelColor(
    [NativeTypeName("SDL_Renderer*")] IntPtr renderer,
    [NativeTypeName("Sint16")] short x,
    [NativeTypeName("Sint16")] short y,
    [NativeTypeName("Uint32")] uint color);
```

**Strengths:**
- C provenance preserved everywhere via `[NativeTypeName(...)]` — useful for code review of generator output diffs (when something changes, you can read the diff and know exactly which C-side type drove the new C# signature)
- `ExactSpelling = true` — no `Ansi/Unicode` auto-mangling (matches modern P/Invoke best practice; sdl2-cs omits this and inherits default `false`)
- Opaque pointers cleanly remapped to `IntPtr` while keeping C provenance — interop-friendly with existing sdl2-cs consumers that also use IntPtr

**Limitations (output is "native-correct but C# low-level"):**
- All buffer parameters emit raw pointers (`short*`, `byte*`) — no `Span<T>` overloads, no `[In]/[Out]` direction hints, no managed-array auto-marshalling
- Strings emit `sbyte*` — caller must encode UTF-8 + pin manually
- Single-element output parameters emit `int*` — no `out int` translation
- Bit-flag intent not detected — `[Flags]` attribute not emitted for typedef-enums OR for `Uint64`-backed flag macros
- `char` C type emits as `sbyte` (correct width) — sdl2-cs uses `char` (Unicode, with default ANSI marshalling — actually wrong for UTF-8 paths)

### What ClangSharp emits "for free" (no config effort)

- Function signatures with type-fidelity attribution
- POD struct definitions (`FPSmanager`)
- `#define` numeric constants (`SMOOTHING_OFF = 0` with `[NativeTypeName("#define ...")]`)
- Unsigned/signed correctness (`short` vs `ushort`, `int` vs `uint`)
- `Cdecl + ExactSpelling = true` for every function

### What requires hand-rolled supplements (currently)

- `NativeTypeNameAttribute` definition (consumer file)
- Friendly overloads (`string`, `ReadOnlySpan<T>`, `out`, `[In]/[Out]`) — Roslyn source generator territory (ppy pattern)
- `[Flags]` attribution per type — `--with-attribute SDL_*Flags=Flags` RSP override
- Duplicate-define workarounds (`M_PI` between two headers)

## 5. ClangSharp Ecosystem Deep Findings

### 5.1 LibraryImport availability — NOT a config flag in 21.1.8.3

Investigated: `dotnet ClangSharpPInvokeGenerator --config help` lists every available config option. **No `generate-libraryimport` or equivalent exists.** The closest options:

- `latest-codegen` — enables modern C#/.NET 10 language features (init properties, collection expressions, etc.) but **still emits `[DllImport]`**
- `preview-codegen` — preview features, same DllImport situation

**ppy/SDL3-CS ships `[DllImport]` everywhere.** Same as our output. They have not switched to `[LibraryImport]` despite being on .NET 8 baseline.

**Production paths for LibraryImport conversion (deferred to Phase 4 plan):**

| Path | Mechanism | Complexity |
|---|---|---|
| **SYSLIB1054 analyzer auto-fix** | Built-in Roslyn analyzer detects `[DllImport]`-with-source-gen-friendly-signature, suggests `[LibraryImport]` replacement; IDE quick-fix or bulk `dotnet format analyzers` applies | Low — uses Microsoft tooling, no custom code |
| **Post-process pipeline** | Regex or Roslyn-syntax-rewriter step in generator pipeline; converts every `[DllImport]` to `[LibraryImport]` + `partial static` + drops `CallingConvention` (uses `[UnmanagedCallConv]` instead) | Medium — owned custom step, but localized |
| **Roslyn source generator extension** | Like ppy's `FriendlyOverloadGenerator` but emits `[LibraryImport]` variants at consumer compile time alongside the `[DllImport]` raw layer | High — owned custom generator, but compositional |

**For Janset.SDL2/SDL3 production:** open decision D9 in feasibility doc captures this. Default tendency: SYSLIB1054 analyzer auto-fix during pre-commit + post-process for missed cases. Decision made at Phase 4 plan time.

### 5.2 Why ppy + Silk.NET pin older ClangSharp versions

ppy uses **ClangSharp 17.0.1** (October 2023). Silk.NET uses **ClangSharp 15.0.2** (older still). Both are years behind 21.1.8.3 latest.

**Rationale: deterministic output > new features.** Once a binding generator produces working, validated output for a project, every libclang upgrade is a potential regen-and-diff exercise:

- C++ attribute parsing details can shift
- Macro expansion edge cases fixed → different output
- New compiler intrinsics surface
- Old intrinsics get deprecated/renamed

Pinning the generator version locks input semantics → byte-identical output across regenerations. Every commit diff represents real SDL header changes, not tool drift.

ppy on 17.0.1: working output, no upgrade pressure. ClangSharp 21.x adds C++20/23 features SDL doesn't use → upgrade has no value.

Silk.NET on 15.0.2: explicit choice ratchet downward to keep MSVC ABI compatibility (see §5.3 — pinning libclang means pinning the toolchain that generates it).

### 5.3 ClangSharp ↔ libclang tight coupling

ClangSharp NuGet has a two-layer structure:

```
ClangSharp 21.1.8.3 (managed wrapper, .NET-side API)
    └── libclang.runtime.win-x64 21.1.x (transitive native dep)
        └── libclang.dll 21.1.x (the actual native LLVM C parser)
```

ClangSharp is the **.NET binding** to libclang (LLVM's C API). The actual C parsing happens in libclang's native binary. ClangSharp NuGet transitively pulls platform-specific `libclang.runtime.<rid>` packages that carry the libclang binaries.

**Implication: pinning ClangSharp implicitly pins libclang.** ClangSharp 15.0.2 → libclang 15.x. ClangSharp 21.x → libclang 21.x.

This matters because **libclang parses system headers too** (`<stdint.h>`, `<windows.h>`, MSVC intrinsics). Each libclang version has implicit ABI compatibility expectations with the host's system header set:

- libclang 15.x expects MSVC 14.34-era headers
- New VS Build Tools (17.5+) ship MSVC 14.35+ headers with new intrinsics
- libclang 15.x can't parse those new intrinsics → errors

Silk.NET ran into exactly this. Their fix: pin VS Build Tools to **17.4.3** on the runner image (matching MSVC 14.34) so libclang 15.x stays compatible. They have a dedicated `runner-setup.md` doc explaining this.

**Net:** libclang pinning + system header coupling means "freeze libclang" requires "freeze the toolchain that libclang reads from." Two pins, not one.

### 5.4 Generation environment practices — Linux container vs Windows runner

Two reference patterns, very different friction profiles:

#### ppy/SDL3-CS — Docker, Ubuntu 24.04 base

- Base image: `ubuntu:24.04`
- Pin layers:
  - `global.json` → .NET SDK 8.0
  - `.config/dotnet-tools.json` → ClangSharpPInvokeGenerator 17.0.1
  - Transitive: `libclang.runtime.*` NuGets (carry libclang binaries)
  - Container layer: Ubuntu 24.04 image digest pins glibc, gcc system headers, apt-supplied .NET SDK
- Workflow: maintainer runs `docker build -t sdl-gen . && docker run --rm -v .:/app -w /app -it sdl-gen python3 SDL3-CS/generate_bindings.py`
- Determinism boundary: the container

**Why Linux pinning is easier:** system headers are part of the distro. Pinning an Ubuntu image digest = pinning glibc + gcc-includes + .NET SDK + everything libclang reads. **Single source of truth.**

#### dotnet/Silk.NET — bespoke Windows runner

- No Docker. Self-hosted Azure F8s v2 Windows Server 2022 Core runner.
- Pin layers:
  - `<PackageReference Include="ClangSharp" Version="15.0.2" />` in BuildTools csproj
  - VS Build Tools 17.4.3 explicitly installed on runner image
  - Multiple .NET SDKs (3.1.302, 5.0.201, 6.0.201, 7.0.102) installed
  - Windows SDK 22621 via `GuillaumeFalourd/setup-windows10-sdk-action`
- Workflow: `.github/workflows/bindings-regeneration.yml` triggers on PR
- Determinism boundary: the runner image (managed manually, no single hash)

**Why Windows is harder:** MSVC Build Tools auto-update via Visual Studio Installer. No `apt pin` equivalent. Pinning requires:
1. Custom runner image setup (manual install of specific Build Tools version)
2. Documentation for contributors (Silk.NET has a whole `runner-setup.md`)
3. Vigilance against auto-update bypassing the pin

macOS analog: Xcode Command Line Tools update via Software Update. Same friction.

#### Trade-off for Janset.SDL2/SDL3

| Approach | Pro | Con |
|---|---|---|
| **`.config/dotnet-tools.json` only** (current spike state) | Single `dotnet tool restore`; works on Windows host today; portable to Linux/macOS CI runners | System headers (MSVC, Windows SDK, glibc) NOT pinned. OS update or VS Build Tools upgrade can shift generated output (Silk.NET's actual experience). |
| **Add Dockerfile (Ubuntu base)** | All layers pinned via image digest; cross-platform maintainer experience identical | Docker Desktop friction on Windows host (WSL2 setup, license for orgs); host volume mount CRLF/permission edge cases; first-run NuGet cache miss per `docker run` unless persisted volume |

**Pragmatic ladder (recommended for spike → production):**

1. **Today:** stay on `.config/dotnet-tools.json` pin (where we are)
2. **Observe:** monitor maintainer regenerate runs for output drift
3. **If drift surfaces:** add Dockerfile alongside (ppy's pattern is actually *two layers* — Dockerfile + tools.json, not either-or)
4. Upgrade path is **additive**, not a rewrite

Captured as Q2 in §9 below ("Where to live: spike-tool-pin (current) vs Dockerfile?"). The feasibility doc's open-decision list is intentionally bounded at D1–D11; this question lives with the spike's Q-list rather than as an orphan D12.

### 5.5 Friendly overload strategies — two distinct philosophies

The "raw P/Invoke vs ergonomic C#" gap exists in every binding generator's output. ppy and Silk.NET solve it differently:

#### ppy — Roslyn source generator emits per-function overloads

Project: `SDL3-CS.SourceGeneration/FriendlyOverloadGenerator.cs`. Implements `ISourceGenerator` (the older Roslyn source generator API, not the incremental `IIncrementalGenerator` API; verified 2026-05-14 against ppy's `master`). If we fork-and-adapt per D10, modernizing to `IIncrementalGenerator` is a separate effort to budget.

Pipeline:
1. **Stage 1 (offline):** ClangSharp generates raw P/Invoke .cs files, committed to git
2. **Stage 2 (consumer compile time):** Roslyn generator runs on the consumer's project, scans the raw declarations, synthesizes friendly overloads

For a function emitted as:

```csharp
[DllImport("SDL3", ...)]
public static extern byte* Unsafe_SDL_GetError();
```

Generator emits at consumer compile time:

```csharp
public static string? SDL_GetError() =>
    Utf8StringMarshaller.ConvertToManaged(Unsafe_SDL_GetError());
```

User experience: both `Unsafe_X(byte*)` AND `X(string)` available without any maintainer-side overload writing. Friendly variant materializes at compile time of every consumer.

Trade-offs:
- **Pro:** more emitted methods, but each method has clear standalone signature; debugger-friendly; idiomatic C#
- **Con:** output compiled assembly larger (≈N × 3 methods for N functions with strings)

#### Silk.NET — `Ref<T>`/`Ptr<T>` wrapper types with implicit conversions

No source generator. Hand-written wrapper structs in `Silk.NET.Core`:

```csharp
public readonly ref struct Ref<T> where T : unmanaged
{
    // Stores a Span<T> or pointer internally
    // Implicit operators: T → Ref<T>, T[] → Ref<T>, Span<T> → Ref<T>, ...
}
```

SilkTouch generator emits **one method per C function**, using `Ref<T>` as the parameter type. Caller has many syntactic options:

```csharp
public static partial void someFunction(Ref<float> data);

// Caller side:
float value = 1.0f;
someFunction(value);             // T → Ref<T>
someFunction(in value);          // ref → Ref<T>
someFunction(new float[10]);     // array → Ref<T>
someFunction(stackalloc float[8]); // Span → Ref<T>
```

Trade-offs:
- **Pro:** fewer methods (≈N × 1), smaller compiled output, single unified abstraction
- **Con:** users must learn `Ref<T>`/`Ptr<T>` abstractions, stack traces show wrapper types, implicit conversion chains harder to read

#### For Janset.SDL2/SDL3 — preference signal

The ppy approach is closer to SDL ecosystem idioms (callers think in `string`, `byte[]`, `IntPtr`, not abstract `Ref<T>`). Silk.NET's wrapper-type abstraction works for game-engine consumers (`Silk.NET` users are typically engine authors), but for "SDL bindings as a library" use case the explicit overload set is more discoverable.

Default tendency: **fork ppy's `FriendlyOverloadGenerator`**, namespace it under `Janset.SDL.SourceGeneration`, adapt for SDL2 method-naming conventions + multi-TFM emission. Open decision D10 in feasibility doc.

## 6. Comparison vs `external/sdl2-cs/src/SDL2_gfx.cs`

sdl2-cs is the hand-written reference (zlib license, Ethan "flibitijibibo" Lee). 390 lines of `[DllImport]` declarations for the same SDL2_gfx surface. **Apples-to-apples comparison illustrates the raw-vs-ergonomic gap.**

### Side-by-side: 7 representative functions

#### A. `polygonColor` — array marshalling philosophy

```csharp
// sdl2-cs (managed-friendly):
public static extern int polygonColor(
    IntPtr renderer, [In] short[] vx, [In] short[] vy, int n, uint color);

// ClangSharp (raw):
public static extern int polygonColor(
    [NativeTypeName("SDL_Renderer*")] IntPtr renderer,
    [NativeTypeName("const Sint16 *")] short* vx,
    [NativeTypeName("const Sint16 *")] short* vy,
    int n,
    [NativeTypeName("Uint32")] uint color);
```

sdl2-cs uses `[In] short[]` (CLR auto-pins managed array). ClangSharp emits `short*` raw pointer (caller responsibility for `fixed` or `stackalloc`).

**Trade-off:** sdl2-cs easier to call (`polygonColor(rdr, new short[]{1,2,3}, ...)`), ClangSharp faster (no managed array allocation + pinning overhead). Friendly overload via Roslyn extension closes the ergonomic gap without losing the fast path.

#### B. `stringColor` — string vs sbyte*

```csharp
// sdl2-cs:
public static extern int stringColor(
    IntPtr renderer, short x, short y, string s, uint color);

// ClangSharp:
public static extern int stringColor(
    [NativeTypeName("SDL_Renderer*")] IntPtr renderer,
    [NativeTypeName("Sint16")] short x,
    [NativeTypeName("Sint16")] short y,
    [NativeTypeName("const char *")] sbyte* s,
    [NativeTypeName("Uint32")] uint color);
```

**sdl2-cs has a latent correctness bug here:** `string` defaults to `LPStr` ANSI marshalling (single-byte, locale-dependent). SDL2_gfx internally expects UTF-8 (or at least ASCII). On non-Latin Windows locales, non-ASCII characters in strings get corrupted before reaching SDL.

ClangSharp `sbyte*` is "uglier" but **correct** — caller knows they're handing over a raw byte buffer, can encode UTF-8 explicitly. The friendly overload (Roslyn-generated) would do `Encoding.UTF8.GetBytes` + pin.

#### C. `characterColor` — char vs sbyte

```csharp
// sdl2-cs:
public static extern int characterColor(
    IntPtr renderer, short x, short y, char c, uint color);

// ClangSharp:
public static extern int characterColor(
    [NativeTypeName("SDL_Renderer*")] IntPtr renderer,
    [NativeTypeName("Sint16")] short x, [NativeTypeName("Sint16")] short y,
    [NativeTypeName("char")] sbyte c,
    [NativeTypeName("Uint32")] uint color);
```

sdl2-cs `char` is 16-bit Unicode. ClangSharp `sbyte` is 8-bit C signed char. **ClangSharp correct, sdl2-cs subtly wrong** — same ANSI marshalling bug as §B.

#### D. `SDL_imageFilterAdd` — array direction hints

```csharp
// sdl2-cs:
public static extern int SDL_imageFilterAdd(
    [In] byte[] src1, [In] byte[] src2, [Out] byte[] dest, uint length);

// ClangSharp:
public static extern int SDL_imageFilterAdd(
    [NativeTypeName("unsigned char *")] byte* Src1,
    [NativeTypeName("unsigned char *")] byte* Src2,
    [NativeTypeName("unsigned char *")] byte* Dest,
    [NativeTypeName("unsigned int")] uint length);
```

sdl2-cs `[Out]` direction hint tells marshaller "copy back after call." ClangSharp emits raw `byte*` — caller buffer-manages.

#### E. `rotozoomSurfaceSize` — out vs int*

```csharp
// sdl2-cs:
public static extern void rotozoomSurfaceSize(
    int width, int height, double angle, double zoom,
    out int dstwidth, out int dstheight);

// ClangSharp:
public static extern void rotozoomSurfaceSize(
    int width, int height, double angle, double zoom,
    int* dstwidth, int* dstheight);
```

sdl2-cs uses `out int` — idiomatic C#. ClangSharp emits `int*` — caller `int w; func(..., &w, &h)`. Friendly overload would generate `out` variants.

#### F. `SDL_initFramerate` — ref struct

```csharp
// sdl2-cs:
public static extern void SDL_initFramerate(ref FPSmanager manager);

// ClangSharp:
public static extern void SDL_initFramerate(FPSmanager* manager);
```

(Verified from generated output.) Same pattern as §E — sdl2-cs idiomatic `ref T`, ClangSharp raw `T*`.

#### G. Numeric `#define` constants

sdl2-cs hand-writes six version + framerate macros:
- `SDL2_GFXPRIMITIVES_MAJOR/MINOR/MICRO` (version macros)
- `FPS_UPPER_LIMIT/LOWER_LIMIT/DEFAULT`

ClangSharp's `--config generate-macro-bindings` captures all six plus `SMOOTHING_OFF` / `SMOOTHING_ON` (8 total). Earlier drafts of this doc claimed only the SMOOTHING_* pair was captured; that was a pre-`generate-macro-bindings` snapshot. Verified 2026-05-14 against the current generated file. **No constant gap vs sdl2-cs hand-written reference.**

### Net Comparison

| Dimension | sdl2-cs hand-written | ClangSharp generated |
|---|---|---|
| **Total lines** | 390 | 361 |
| **Function count** | ~100 | 102 |
| **Type fidelity** | Lost (C type names not preserved) | Preserved via `[NativeTypeName]` |
| **String safety** | Default ANSI (locale-dependent bug on non-Latin Windows) | Explicit `byte*` (caller controls encoding) |
| **Array ergonomics** | Auto (`[In] T[]`) | Raw (`T*` + caller pinning) |
| **`out` parameters** | Idiomatic | Raw `T*` |
| **AOT-compat by default** | Acceptable (DllImport with no marshaller dependencies) | Same (DllImport, same caveats) |
| **Maintenance** | Manual diff against upstream changes | Re-run RSP after upstream bump → diff |

ClangSharp output **more correct, less ergonomic**. Both deliverables are usable; the choice is whether to layer ergonomics via Roslyn source generator OR live with raw signatures + caller boilerplate.

## 7. CppAst Companion Spike

Parallel spike running CppAst-custom-emitter pipeline on the same SDL2_gfx surface for apples-to-apples comparison against the ClangSharp side (§2-§4).

### 7.1 What We Set Up

Directory structure (sibling to ClangSharp side under same spike branch):

```
tools/binding-spike/cppast/
├── generator/
│   ├── generator.csproj            # net10 console, refs CppAst + native runtime packages
│   └── Program.cs                  # 261-line custom emitter (parses, walks, emits) — verified 2026-05-14
└── bindings/
    ├── Spike.Bindings.SDL2_gfx.csproj  # compiles generated output, AOT-compatible
    └── Generated/
        └── SDL2_gfx.cs              # 345 lines, 102 P/Invoke + 1 struct + 8 const
```

Key dependencies (`Directory.Packages.props`):

```xml
<PackageVersion Include="CppAst" Version="0.24.0" />
<PackageVersion Include="libclang.runtime.win-x64" Version="20.1.2" />
<PackageVersion Include="libClangSharp.runtime.win-x64" Version="20.1.2" />
```

**Critical pinning detail:** all three packages MUST agree on a single major version. CppAst 0.24.0 builds against ClangSharp 20.1.2.4; the corresponding `libclang.runtime` + `libClangSharp.runtime` must be **20.1.x**, NOT 21.x. Version mismatch surfaces as a runtime stack overflow during AST visit (see §7.2 friction #5).

### 7.2 Iteration Log — Friction → Resolution

Six iterations to green build, six distinct frictions:

| # | Friction | Symptom | Resolution |
|---|---|---|---|
| 1 | **`dotnet new console --use-program-main` default not overridden** | First `dotnet run` printed "Hello, World!" — my custom emitter never compiled because Write tool requires prior Read | Read `Program.cs` first, then Write overwrite. Process artifact, not a tool design issue. |
| 2 | **Multi-TFM inheritance from root `Directory.Build.props`** | Restore failed: CppAst only ships net8+, but inherited `<TargetFrameworks>` was `net10;net9;net8;netstandard2.0;net462` | Explicitly clear plural (`<TargetFrameworks></TargetFrameworks>`) + pin singular (`<TargetFramework>net10.0</TargetFramework>`). MSBuild prefers plural when both set; without clearing it the singular is ignored. |
| 3 | **`Console.WriteLine(CultureInfo.InvariantCulture, $"…")` overload doesn't exist** | Compile error `CS1503` | Drop CultureInfo argument; the interpolated string doesn't need invariant formatting for a stdout log line. |
| 4 | **`DllNotFoundException: libclang`** at runtime when CppParser.ParseFiles invoked | CppAst's NuGet doesn't carry native libclang binary; consumer adds it separately | `dotnet add package libclang.runtime.win-x64`. CppAst depends on `libclang` native via ClangSharp managed wrapper. |
| 5 | **Stack overflow / infinite recursion in `CppModelBuilder.GetOrCreateDeclarationContainer`** when AST walk begins | Process crashed with `StackOverflowException`-like trace, frames repeating | **Version mismatch.** Initially installed `libclang.runtime.win-x64 21.1.8` (latest); CppAst 0.24.0 expects libclang 20.1.x. AST shape changed between libclang 20 → 21, CppAst's visitor recurses indefinitely on the new shape. Downgraded `libclang.runtime` + `libClangSharp.runtime` to 20.1.2. |
| 6 | **Opaque pointer remap silent fail** (`SDL_Renderer* renderer` in output, not `IntPtr renderer`) | Compile would have failed because `SDL_Renderer` is not defined in our output | Added `CppClass cls when OpaquePointerTypes.Contains(cls.Name) => "IntPtr"` arm to `MapPointer`. CppAst resolves the `typedef struct SDL_Renderer SDL_Renderer` chain to a `CppClass`, not `CppTypedef` — my original switch only handled the typedef case. |

### 7.3 Output Observations

```csharp
// CppAst-spike-generated output for pixelColor:
[DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
public static extern int pixelColor(IntPtr renderer, short x, short y, uint color);
```

Versus ClangSharp output for the same function:

```csharp
[DllImport("SDL2_gfx", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
public static extern int pixelColor(
    [NativeTypeName("SDL_Renderer*")] IntPtr renderer,
    [NativeTypeName("Sint16")] short x,
    [NativeTypeName("Sint16")] short y,
    [NativeTypeName("Uint32")] uint color);
```

**Net differences:**

- CppAst output is **leaner** — no `[NativeTypeName]` provenance attribution, no `NativeTypeNameAttribute.cs` consumer-helper file required.
- CppAst output **loses C-side type information** that ClangSharp preserves — diffs against future regenerations harder to audit.
- Both emit identical structural shape (DllImport, Cdecl, ExactSpelling=true, IntPtr opaque pointers, primitive type mappings).

### 7.4 Macro-Capture Comparison (Revised 2026-05-14)

Wrote a custom `TryEmitMacroConstant` helper in `Program.cs` (~30 lines) that handles int, hex int, double, and string literal macro forms. Result: **8 constants emitted**.

CppAst output:

```csharp
public const int SDL2_GFXPRIMITIVES_MAJOR = 1;
public const int SDL2_GFXPRIMITIVES_MINOR = 0;
public const int SDL2_GFXPRIMITIVES_MICRO = 4;
public const int FPS_UPPER_LIMIT = 200;
public const int FPS_LOWER_LIMIT = 1;
public const int FPS_DEFAULT = 30;
public const int SMOOTHING_OFF = 0;
public const int SMOOTHING_ON = 1;
```

**ClangSharp's `--config generate-macro-bindings` captures the same 8 constants** (verified 2026-05-14 against `tools/binding-spike/clangsharp/bindings/Generated/SDL2_gfx.g.cs`). Earlier drafts of this section claimed ClangSharp only captured `SMOOTHING_OFF` + `SMOOTHING_ON` — that was a pre-`generate-macro-bindings` RSP iteration. Once the flag landed in the final RSP, the gap closed.

**Net macro-capture observation:** at SDL2_gfx scope, both toolchains emit identical constant sets out of the box (CppAst via 30 lines of hand-rolled `TryEmitMacroConstant`, ClangSharp via the `generate-macro-bindings` flag). The earlier "CppAst captures more constants" framing is **not supported by the current spike artifacts** and has been retracted. The custom-emission-ceiling argument for CppAst stands on other grounds (multi-TFM dual emit, friendly overloads, platform attribution in a single loop — see §7.8), not on small-scale macro recognition.

### 7.5 Trade-off Matrix — Real Hands-On Data

Verified against current spike artifacts on 2026-05-14. Line counts will drift as the spike evolves; treat the numbers as a snapshot, the patterns as the takeaway.

| Dimension | ClangSharp (RSP+tool) | CppAst (custom emitter) |
|---|---|---|
| **Config locus** | 43-line RSP file (declarative) | 261-line `Program.cs` (imperative emitter) |
| **Tool dependency** | `.config/dotnet-tools.json` local tool pin (single version) | `Directory.Packages.props` 3-package version pin (CppAst + 2 native runtimes), all must agree |
| **Iteration cycle** | edit RSP + re-run (no compile) | edit `Program.cs` + compile generator + re-run |
| **Generated output line count** | 361 | 345 |
| **Function count** | 102 | 102 |
| **Struct emit** | 1 (`FPSmanager`) | 1 (`FPSmanager`) |
| **Constants captured** | 8 (via `--config generate-macro-bindings`) | 8 (via custom `TryEmitMacroConstant` ~30 lines) |
| **C provenance preserved** | ✅ `[NativeTypeName("Sint16")]` everywhere | ❌ lost |
| **Hand-written supplements needed** | `NativeTypeNameAttribute.cs` (25 lines) + `Constants.cs` for M_PI dedup (11 lines) = ~36 lines | None |
| **Duplicate-define handling** | `--exclude M_PI` flag + manual supplement | `HashSet.Add()` dedup in emit loop, code-level |
| **Iterations to green build** | 5 (path, MMX, remap syntax, attribute, duplicate macro + glob) | 6 (Write order, multi-TFM clear, CultureInfo overload, libclang native pkg, **version-trio mismatch**, opaque-pointer-via-CppClass arm) |
| **Customization ceiling** | RSP grammar (`--remap`, `--with-attribute`, `--exclude`, etc.) | Unbounded C# logic over CppAst's AST model |
| **Owned code to maintain forever** | ~128 lines (RSP 43 + bindings csproj 36 + `NativeTypeNameAttribute.cs` 25 + `Constants.cs` 11 + dotnet-tools.json 13) | ~320 lines (Program.cs 261 + generator csproj + bindings csproj) |
| **Output style** | DllImport + attribute-rich | DllImport + lean |

### 7.6 Key Validating Insight — Version-Trio Coupling is Real

The ClangSharp-ecosystem research findings doc (§5.3 ClangSharp ↔ libclang tight coupling) described the coupling between managed wrapper + native libclang as a hypothetical risk. **The CppAst spike manifested this risk as a real crash** (stack overflow on AST visit) when we initially installed the latest `libclang.runtime` (21.1.8) against CppAst 0.24.0 (built for libclang 20.1.x).

CppAst sits one abstraction layer **above** ClangSharp + libclang, but doesn't actually shield consumers from the version coupling — it propagates it. CppAst maintainer (xoofx) builds against a specific ClangSharp version; consumers MUST pin the matching `libclang.runtime` + `libClangSharp.runtime` versions in their `Directory.Packages.props` (or csproj). The CppAst NuGet's transitive dep resolver gives you a default that happens to work IF you don't override it; the moment you add `libclang.runtime` explicitly with a newer version (e.g., via `dotnet add package` without specifying version), things break.

**Practical implication for production AST workstream:**

- CppAst path requires explicitly pinning all 3 versions in `Directory.Packages.props` with a comment explaining the coupling
- A future CppAst upgrade (0.24 → 0.25) bumps the expected ClangSharp/libclang versions; consumers must manually re-coordinate three pins
- ClangSharp tool path needs only one pin (`.config/dotnet-tools.json`); the tool bundles its own native deps

This isn't a deal-breaker for CppAst, but it confirms the maintenance asymmetry. The ClangSharp-favoring research case (made on industry-survey + source-level comparison grounds) holds against hands-on raw-binding data, while the final toolchain remains open.

### 7.7 Where the Toolchains Genuinely Differ

After running both, the differences that surfaced in practice (not in research):

1. **Iteration speed:** CppAst's "edit + compile generator + re-run" is meaningfully slower than ClangSharp's "edit RSP + re-run." Maybe 30-90 seconds per iteration vs 3-10 seconds. Over many iterations during initial RSP tuning, this compounds.
2. **Setup complexity:** CppAst needed 3 packages (CppAst + libclang.runtime + libClangSharp.runtime), version coordination, and ~261 lines of emitter code. ClangSharp needed 1 tool install + 43 lines of RSP. CppAst's "advantage" of unbounded customization came with a much heavier setup floor.
3. **Output ergonomics:** CppAst output is "cleaner-looking" (no attributes) but loses provenance. For maintainer code review of generator regenerations, ClangSharp's `[NativeTypeName]` provenance is concretely useful.
4. **Macro capture parity at SDL2_gfx scope:** an earlier draft of this section claimed CppAst's hand-rolled 30-line `TryEmitMacroConstant` outperformed ClangSharp's `generate-macro-bindings` flag (8 vs 2). Revised 2026-05-14: both toolchains capture the same 8 constants at this scope (see §7.4). CppAst's custom-emission ceiling is real but does not manifest at this scope.

**None of these settle the toolchain decision.** ClangSharp wins on iteration speed + setup floor; the constants comparison ties. For raw P/Invoke only, ClangSharp's economics dominate; for production-shape output with more custom emission (multi-TFM dual emit, friendly overloads, platform attribution — see §7.8), CppAst's single-emitter elasticity needs more validation.

The earlier "CppAst would be right if Skia-style custom-emission was the use case" framing (in `binding-autogen-approaches.md` §2026-05-12 Source-Level Comparison) stays intact. At SDL2_gfx scope custom emission did not produce a measurable win; if our scope grows to need significant custom emission later (multi-TFM dual emit, friendly overloads at the same emitter, Skia-style wrapper layer), CppAst becomes more competitive.

### 7.8 Scope-Growth Trade-off — CppAst's Linear-Growth Advantage

A refinement on the raw "owned LoC" metric that the spike data surfaces: **the gap between toolchains narrows as the feature scope grows, and at "production-shape" scope, CppAst's single-codebase elasticity may flip the calculus.**

#### Conceptual clarification: CppAst is a library, NOT a Roslyn source generator

These get conflated. Concrete differences:

| Boyut | CppAst (offline console app, library) | Roslyn source generator |
|---|---|---|
| **When does it run** | Maintainer-side, offline (`dotnet run --project generator`) | Consumer compile-time (every build) |
| **What does it parse** | C/C++ headers via libclang | C# source code via Roslyn syntax tree |
| **Where does output go** | Plain `.cs` files on disk, committed to git | Compiled assembly in-memory, never committed |
| **Generator host TFM** | Generator's own choice (`net10`, `net9`, etc.) | Forced `netstandard2.0` (Roslyn API constraint) |
| **Output shape** | String text, free to use any C# feature | IR (`SyntaxNode`) → IL, constrained by Roslyn analyzer pipeline |

**Critical implication:** **the generator's TFM does not constrain the output's TFM.** CppAst console app running on `net10` can emit `.cs` files targeting `net462`, `netstandard2.0`, `net10`, or all of them simultaneously via `#if`-guarded blocks. The emitter is "just writing strings" — language features it uses internally are independent of what the output declares.

#### Single-loop emit pattern — multiple features in one pass

This is where CppAst's free-form emitter wins. Adding new features to the output means **adding iteration logic to one loop**, not coordinating separate systems:

```csharp
foreach (var func in compilation.Functions.Where(InTargetHeader))
{
    // 1. Multi-TFM dual emit (LibraryImport for net7+, DllImport fallback)
    EmitMultiTFMPInvoke(sb, func);

    // 2. Friendly string overload — if func has byte* parameters
    if (func.Parameters.Any(p => IsUtf8StringParam(p)))
    {
        EmitFriendlyStringOverload(sb, func);
    }

    // 3. ReadOnlySpan<T> overload — for byte*/short*/int* + length patterns
    if (HasArrayWithCountPattern(func))
    {
        EmitSpanOverload(sb, func);
    }

    // 4. ref/out idiomatic overload — for pointer-to-primitive output params
    if (func.Parameters.Any(IsOutputPointer))
    {
        EmitRefOutOverload(sb, func);
    }

    // 5. [SupportedOSPlatform] attribute — for platform-conditional functions
    if (IsPlatformConditional(func))
    {
        AnnotateWithPlatformAttribute(sb, func);
    }
}
```

Single codebase, single loop, every feature. **Scope grows linearly with iteration logic, not with architectural coordination.**

#### ClangSharp equivalent — system coordination tax

Reaching the same feature set with ClangSharp requires multiple coordinated systems:

| Feature | ClangSharp surface |
|---|---|
| Raw P/Invoke baseline | ClangSharp tool + RSP file |
| Multi-TFM dual emission (`LibraryImport` net7+ / `DllImport` legacy) | Post-process pipeline (sed/Roslyn-rewriter) over generated `.cs` files |
| Friendly `string` overloads | Roslyn source generator (consumer compile-time) — like ppy's `FriendlyOverloadGenerator` |
| `ReadOnlySpan<T>` overloads | Same Roslyn source generator, additional rules |
| `ref/out` idiomatic overloads | Same Roslyn source generator, more rules |
| `[SupportedOSPlatform]` attribution | RSP `--with-attribute` (limited per-symbol) + Roslyn extension for complex logic |
| Per-platform parse + merge | N+1 ClangSharp tool invocations + merge orchestrator script (ppy pattern) |

**3-4 separate systems to maintain.** Each has its own mental model, debugging surface, and version-coordination story. ClangSharp tool one release cycle, the post-process pipeline another, the Roslyn source generator a third.

#### Owned-LoC growth — scope-vs-ownership projection

Refined picture combining the spike's raw data with scope-growth math:

| Scope tier | ClangSharp ownership | CppAst ownership |
|---|---|---|
| **Raw P/Invoke only** (current spike) | ~75 LoC (RSP + 2 helper files + dotnet-tools.json + csproj) | ~225 LoC (Program.cs + csproj) |
| **+ Multi-TFM dual emit** | + ~150 LoC post-process step (separate project) | + ~50 LoC iteration logic in same Program.cs (~280 LoC total) |
| **+ Friendly string/Span overloads** | + ~400 LoC Roslyn source generator (separate project) | + ~80 LoC iteration logic (~360 LoC total) |
| **+ Per-platform `[SupportedOSPlatform]` attribution** | + ~50 LoC RSP per-platform pass orchestrator | + ~40 LoC iteration logic (~400 LoC total) |
| **+ Custom wrapper layer (Skia-style hand-curated public API)** | Hard — neither tool helps; hand-write outside the generator | + ~100 LoC iteration logic OR separate hand-written file (~500 LoC generator total) |

**ClangSharp ownership grows in architectural steps** (each new feature = new project + new mental model). **CppAst ownership grows linearly** (each new feature = more iterations in the same loop). At "raw P/Invoke" scope ClangSharp wins on absolute LoC; at "production-shape scope" the gap narrows or inverts.

#### Net implication on the toolchain decision

This **does not flip the recommendation** for our SDL2_gfx spike scope — ClangSharp is strictly leaner here. But it sharpens the framing of when CppAst becomes more competitive:

- **If our scope stays at raw P/Invoke** (no friendly overloads, no LibraryImport upgrade, no `[SupportedOSPlatform]` per-symbol attribution, no wrapper layer) — ClangSharp dominates.
- **If our scope grows toward production-shape** (multi-TFM emit + friendly overloads + platform attribution) — the toolchains converge on owned-LoC, and CppAst's single-codebase advantage starts to matter.
- **If our scope grows further to Skia-style hand-curated wrapper API on top** — CppAst becomes structurally better-suited (the framing in `binding-autogen-approaches.md` §2026-05-12 already captured this; this spike refines the gradient).

For Phase 4 implementation plan: **decision sequencing matters.** Choosing ClangSharp today and migrating to CppAst later (if scope grows past the inflection point) costs the existing RSP + Roslyn-source-gen + post-process investment. Choosing CppAst today + writing all 4-5 feature iterations into one loop from the start may pay off if scope is known to grow.

The spike doesn't resolve this — it surfaces the gradient. Phase 4 plan authoring needs to commit on a **scope-trajectory bet**, not just a "what works today" choice. Captured as a new open question (§9 Q8 below).

## 8. Runtime Validation — Both Toolchains End-to-End

The spike capped with a real-runtime test app. Goal: take the bindings off the compile-time-only shelf and actually call into `SDL2_gfx.dll` from C#, render real graphics, validate the entire P/Invoke + native-binary-distribution chain. Result: **both toolchains pass identical behavior tests**.

### 8.1 Test App Architecture

Two test apps under `tools/binding-spike/{clangsharp,cppast}/test/`:

```
clangsharp/test/Spike.TestApp.SDL2_gfx.csproj
├── Imports: ../../../../build/msbuild/Janset.Local.props  (local-feed + version pins)
├── PackageReference: Janset.SDL2.Core 2.32.0-local        (sdl2-cs SDL2 core + native via transitive)
├── PackageReference: Janset.SDL2.Gfx.Native 1.0.0-local   (SDL2_gfx.dll via buildTransitive copy)
└── ProjectReference: ../bindings/Spike.Bindings.SDL2_gfx  (our spike-generated wrappers)

cppast/test/Spike.TestApp.SDL2_gfx.csproj
└── Identical wiring, only the ProjectReference target differs.

Shared Program.cs (Compile Include link):
   clangsharp/test/Program.cs ← cppast/test/Spike.TestApp.SDL2_gfx.csproj imports via <Compile Include="../../clangsharp/test/Program.cs" Link="Program.cs" />
```

**Same Program.cs, different bindings DLL** — the cleanest apples-to-apples runtime test.

### 8.2 Program.cs — testframerate.c Port

The shared `Program.cs` (~110 lines) ports `giroletm/SDL2_gfx/test/testframerate.c` to C#:
- Drops SDLTest harness boilerplate (`SDLTest_CommonCreateState` etc. — not available from C#)
- Uses sdl2-cs SDL2 core directly: `SDL.SDL_Init`, `SDL.SDL_CreateWindow`, `SDL.SDL_CreateRenderer`, `SDL.SDL_PollEvent`, `SDL.SDL_RenderClear`, `SDL.SDL_RenderPresent`, `SDL.SDL_Quit`
- Uses spike-generated SDL2_gfx via `Janset.Spike.SDL2.Gfx.SDL_gfx.*`:
  - `SDL_initFramerate(&fpsm)` / `SDL_setFramerate(&fpsm, rate)` / `SDL_framerateDelay(&fpsm)` (FPSmanager struct passed by pointer)
  - `filledCircleRGBA(renderer, x, y, rad, r, g, b, a)` (drawing primitives)
  - `circleRGBA(...)`, `rectangleRGBA(...)`, `stringRGBA(...)`
- Runtime detection: `Assembly.GetExecutingAssembly().GetName().Name` ends with `.ClangSharp` or `.CppAst` → prints which side is running
- 180 frame loop (~6 seconds @ 30 FPS), bouncing colored circle, white outline, yellow HUD rectangle, UTF-8 frame counter text

UTF-8 string marshalling for `stringRGBA` parameter (`sbyte*` in both generated bindings):

```csharp
var hudText = $"Janset SDL2_gfx spike ({generatorSide}) frame {frame}/{TOTAL_FRAMES}";
unsafe
{
    var bytes = System.Text.Encoding.UTF8.GetBytes(hudText + "\0");
    fixed (byte* bp = bytes)
    {
        SDL_gfx.stringRGBA(renderer, 20, 20, (sbyte*)bp, 255, 255, 255, 255);
    }
}
```

### 8.3 Native Binary Distribution — Validated

`Janset.SDL2.Gfx.Native` package's `buildTransitive/Janset.SDL2.Gfx.Native.targets` + the shared `Janset.SDL2.Native.Common.targets` did exactly what they advertise:

```text
bin/Debug/net10.0/
├── Janset.Spike.SDL2.Gfx.Test.{ClangSharp,CppAst}.dll  (test app)
├── Janset.Spike.SDL2.Gfx.dll                            (spike bindings — different bytes per side)
├── SDL2.Core.dll                                         (sdl2-cs managed wrapper)
└── runtimes/win-x64/native/
    ├── SDL2.dll                                          (from Janset.SDL2.Core.Native via buildTransitive)
    └── SDL2_gfx.dll                                      (from Janset.SDL2.Gfx.Native via buildTransitive)
```

**Zero manual DLL copy logic in csproj.** .NET runtime's native-library probe path automatically searches `runtimes/<rid>/native/` for P/Invoke resolution. Worked first time on both sides.

### 8.4 Apples-to-Apples Run Results

```text
===CLANGSHARP TEST===
Janset SDL2_gfx spike — ClangSharp bindings
  Assembly: Janset.Spike.SDL2.Gfx.Test.ClangSharp
  SDL_Init...
  SDL_CreateWindow OK
  SDL_CreateRenderer OK
  SDL2_gfx framerate initialized at 30 FPS
  Frames rendered: 180
  Total framerate-delay accumulated: 5898ms
  SDL_Quit. Bye.

===CPPAST TEST===
Janset SDL2_gfx spike — CppAst bindings
  Assembly: Janset.Spike.SDL2.Gfx.Test.CppAst
  SDL_Init...
  SDL_CreateWindow OK
  SDL_CreateRenderer OK
  SDL2_gfx framerate initialized at 30 FPS
  Frames rendered: 180
  Total framerate-delay accumulated: 5900ms (← 2ms jitter normal)
  SDL_Quit. Bye.
```

Both windows opened, drew the bouncing circle + HUD, ran for 180 frames at 30 FPS (theoretical 5940ms, observed 5898/5900ms = 0.7% deviation — normal timer jitter), clean shutdown.

**Binary-size delta:** ClangSharp bindings DLL 17 408 bytes, CppAst bindings DLL 13 312 bytes (24% smaller). Source of difference: ClangSharp emits `[NativeTypeName(...)]` attribute metadata on every parameter, return value, struct field — that metadata is embedded in the DLL. CppAst output has no such attributes, ships leaner. Trade-off: provenance information value vs binary size. Both DLLs runtime-equivalent.

### 8.5 What This Validates — Layered

| Layer | Validation outcome |
|---|---|
| **libclang parsing** | Both ClangSharp 21.1.8 + CppAst 0.24.0 (with libclang 20.1.2 native) parsed SDL2_gfx's 4 headers without errors (after the `__PRFCHWINTRIN_H` define workaround) |
| **Type mapping correctness** | `Sint16→short`, `Uint8→byte`, `Uint32→uint`, `SDL_Renderer*→IntPtr`, `const char *→sbyte*`, primitive return types — all correct for runtime P/Invoke; no `EntryPointNotFoundException` or struct-layout corruption |
| **POD struct layout** | `FPSmanager { uint framecount, float rateticks, uint baseticks, uint lastticks, uint rate }` with `[StructLayout(LayoutKind.Sequential)]` is binary-compatible with the C struct — `SDL_initFramerate(&fpsm)` populated all fields correctly, `SDL_framerateDelay(&fpsm)` returned valid millisecond delay values that summed to expected 30 FPS timing |
| **UTF-8 string marshalling** | Manual `byte[]` → pinned `byte*` → cast `(sbyte*)` pattern delivered the HUD text intact; SDL2_gfx rendered the ASCII string correctly on screen |
| **Mixed-toolchain interop** | sdl2-cs's `IntPtr renderer` returned from `SDL_CreateRenderer` was accepted by spike-generated `filledCircleRGBA(IntPtr renderer, ...)` — same managed IntPtr type bridges two independently-generated binding sources |
| **Native distribution** | `Janset.SDL2.Gfx.Native` nupkg's buildTransitive targets correctly copied `SDL2_gfx.dll` to `bin/<TFM>/runtimes/<rid>/native/`; .NET runtime's P/Invoke probe path resolved the DLL without any csproj-side intervention |
| **Apples-to-apples semantic equivalence** | Identical Program.cs, two different binding generation pipelines, identical runtime behavior (180 frames, ~30 FPS exact, identical render output) |

### 8.6 Spike Conclusion

This validates the **entire two-stage AST workstream pipeline** end-to-end:

- **Stage 1 (offline, maintainer-side):** generator (RSP or Program.cs) consumes SDL headers + emits raw P/Invoke .cs files
- **Stage 2 (online, consumer-side):** generated .cs compiled into managed DLL + native binaries distributed via nupkg buildTransitive + .NET runtime resolves P/Invoke at runtime

Both toolchain paths execute Stage 1 with comparable output and Stage 2 with **identical** consumer experience. The toolchain choice does not affect runtime semantics — only affects maintainer-side ergonomics (config locus, iteration cycle, customization ceiling, owned LoC).

**Spike answers in summary:**

1. ✅ Can ClangSharp generate working SDL2_gfx bindings? **Yes, 102 functions, runtime-validated.**
2. ✅ Can CppAst generate working SDL2_gfx bindings? **Yes, 102 functions, runtime-validated.**
3. ✅ Do the two produce semantically equivalent runtime behavior? **Yes, identical (within timer jitter).**
4. ✅ Does our existing native-distribution mechanism (buildTransitive nupkg + buildTransitive) work for spike consumers? **Yes, zero manual copy logic.**
5. ✅ Can we mix sdl2-cs SDL2 core + spike-generated SDL2_gfx in the same consumer project? **Yes, IntPtr interop clean.**

The spike answers the "does this approach work?" question with empirical YES. What remains for Phase 4 plan: **which production-shape features to invest in (multi-TFM emit, friendly overloads, platform attribution, possibly wrapper layer)** and **which toolchain to commit to long-term** given the §7.8 scope-trajectory framing.

### 8.7 Post-Spike Platform-Pass Research Update

Follow-up research on 2026-05-14 sharpened Q4 from "nice to validate" into a core spike acceptance criterion.

Not every comparable project runs multi-platform binding passes:

| Project | What it proves |
| --- | --- |
| ppy/SDL3-CS | Strong SDL-specific reference: host macros are undef'd, then affected headers are regenerated through neutral + platform-specific ClangSharp passes. |
| SkiaSharp | Strong CppAst discipline reference, but not a platform-split reference; Skia's C API is intentionally platform-neutral. |
| Alimer.Bindings.SDL | Useful C# shape reference, but its single CppAst pass with multiple platform macros active at once is an impossible union target for platform-conditioned headers/layout and is not sufficient for that part of our correctness bar. |
| Silk.NET | Uses platform/OS-gated generation tasks, but its Windows runner requirement comes from Windows SDK/MSVC/DirectX parsing needs and should not be generalized to SDL. |

Local SDL2 header inspection confirms the concern is real: `SDL_system.h`, `SDL_main.h`, `SDL_platform.h`, `SDL_config.h`, `SDL_stdinc.h`, and `SDL_syswm.h` contain platform-conditioned declarations or ABI-relevant macros. This is a libclang/preprocessor constraint shared by CppAst and ClangSharp. The next spike should therefore prove neutral + platform-specific passes, dedup, and platform attribution for the selected CppAst-leaning path before the WHY/HOW/WHAT doc locks the toolchain.

**CppAst platform-pass spike result:** CppAst successfully reproduced the ppy-style neutral + platform-specific pass shape for SDL2 `SDL_system.h` and `SDL_main.h`. Windows and Linux symbols were isolated into attributed files, neutral output stayed clean, the macOS pass emitted a deterministic empty platform file for these headers, and the generated net10 binding project compiled. The Windows-hosted Linux pass required minimal C stdlib header stubs, confirming the expected "controlled sysroot/stubs" requirement for cross-target parsing. `SDL_syswm.h` remains deferred as a struct/union-layout-specific follow-up.

**Satellite/shared-type follow-up:** the next correctness boundary is not another platform-only function pass; it is satellite topology. Local `SDL_image.h`, `SDL_ttf.h`, and `SDL_mixer.h` all include SDL core headers and expose core-owned types (`SDL_Surface`, `SDL_Texture`, `SDL_Renderer`, `SDL_RWops`, `SDL_bool`, `SDL_version`, etc.) in satellite signatures. The production generator should emit satellite-owned functions/types while referencing a single core-owned managed type universe. ppy/SDL3-CS follows this shape: `SDL3_image-CS` is separate from `SDL3-CS`, but references the core project and generated image APIs use core SDL types. The next spike target should therefore be `SDL2_image`, with acceptance criteria around no duplicate core declarations, correct type-map reuse, compile, and a small image asset smoke path.

**SDL2_image shared-type spike result:** CppAst generated the current vcpkg `SDL_image.h` surface into a separate image binding project while referencing a separate core-types project for core-owned SDL concepts. The generated image output compiled, emitted 59 `IMG_*` extern declarations, emitted image-owned `ImgInitFlags` plus opaque `ImgAnimation`, and reused core-owned `SDL_version`, `SDL_Surface`, `SDL_Texture`, `SDL_Renderer`, and `SDL_RWops` through analyzer-clean managed names (`SdlVersion`, `SdlSurface`, `SdlTexture`, `SdlRenderer`, `SdlRWops`) instead of redeclaring them in the satellite output. SDL2-CS oracle comparison found the expected version drift: vendored SDL2-CS covers an older SDL_image surface, while the vcpkg 2.8.8 header adds newer decoder probes/loaders (`AVIF`, `JXL`, `QOI`, `SVG`, etc.). The only high-value oracle items absent from generated output were macro aliases (`IMG_GetError`, `IMG_SetError`), confirming macro emission remains a production-generator follow-up rather than a shared-type topology blocker. Runtime image decoding smoke was not run in this slice because the local checkout does not currently contain harvested `SDL2_image` native binaries.

**Generation workflow clarification:** "Platform pass" means a controlled libclang preprocessor/target view, not necessarily a native OS runner. The CppAst platform-pass spike ran on Windows and still produced Linux-specific output by selecting a Linux target view, enabling Linux macros, disabling Windows/macOS macros, and supplying controlled stub include inputs. The current release-grade default should therefore be committed generated source produced by a pinned single host plus multiple parse views, with CI drift checks, native export validation, and consumer smoke before NuGet packaging. True multi-OS extraction and merge, as seen in bottlenoselabs/SDL3-cs, remains an escalation path if controlled single-host parsing cannot model an SDL header correctly.

## 9. Open Questions Deferred to Phase 4 Plan

These surfaced during spike, captured for the Phase 4 implementation plan to resolve:

| # | Question | Default tendency (revisit during plan authoring) |
|---|---|---|
| Q1 | How to expose `LibraryImport` variants? | SYSLIB1054 analyzer auto-fix during pre-commit (Option 1 in §5.1) |
| Q2 | Where to live: spike-tool-pin (current) vs Dockerfile? | Stay on tool-pin until output drift observed, then add Docker (additive) |
| Q3 | Friendly overload strategy: fork ppy's `FriendlyOverloadGenerator`, write our own, or emit offline? | Undecided — ClangSharp likely means Roslyn source generation; CppAst can emit friendly overloads offline from the same generator loop. |
| Q4 | Multi-platform parsing — neutral + platform-specific passes? | Validate the pattern for our 7-RID matrix. This is tool-agnostic libclang/preprocessor behavior: ClangSharp would likely use ppy-style invocations/RSP passes, while CppAst would need equivalent parser-option passes plus custom merge/dedup handling. |
| Q5 | Multi-TFM emission — `[DllImport]` fallback + `[LibraryImport]` primary? | Undecided — ClangSharp likely needs post-processing or merge logic; CppAst can emit per-TFM `#if NET7_0_OR_GREATER` guards directly. |
| Q6 | Missing constants from `generate-macro-bindings` — debug or accept manual supplement? | **Resolved 2026-05-14 for SDL2_gfx scope** — ClangSharp's `--config generate-macro-bindings` captures all 8 SDL2_gfx constants identically to CppAst's hand-rolled macro emitter. Manual `Constants.cs` retained only for the `M_PI` cross-header duplicate workaround. Re-open per-satellite if `generate-macro-bindings` misses constants in a different SDL header set. |
| Q7 | Wrapper layer (Skia-style hand-written types on top of raw bindings) — yes/no/later? | Defer — raw layer + friendly overloads cover SDL idioms; wrapper layer is a separate Phase 5+ decision if API ergonomics need more polish |
| Q8 | **Scope-trajectory bet** — pick ClangSharp for raw P/Invoke today and accept migration cost if scope grows past inflection, OR pick CppAst now to absorb production-shape features (multi-TFM emit, friendly overloads, platform attribution) in a single emitter codebase from the start? | **Undecided until WHY/HOW/WHAT.** Maintainer currently leans CppAst, but wants more information. Validate the CppAst single-emitter path against production-shape needs before locking, while keeping ClangSharp's raw-binding economics as the comparison baseline. |

## 10. Cross-Reference

- [`binding-autogen-approaches.md`](binding-autogen-approaches.md) — toolchain selection (CppAst → ClangSharp flip), industry survey, decision matrix
- [`binding-autogen-feasibility.md`](binding-autogen-feasibility.md) — 11 emit rules, 7-layer testing strategy, 11 open decisions (D1–D11), 4 pending discussion threads
- [`../release-strategy.md`](../release-strategy.md) — Stage 0-5 path, AST-first sequencing, end state at v1.0
- [`../phases/phase-4-binding-autogen.md`](../phases/phase-4-binding-autogen.md) — Phase 4 design brief
- `tools/binding-spike/clangsharp/` — actual spike artifacts (on `spike/binding-autogen-sdl2-gfx` branch)

## Sources Cited

- [ppy/SDL3-CS Dockerfile](https://github.com/ppy/SDL3-CS/blob/master/Dockerfile)
- [ppy/SDL3-CS .config/dotnet-tools.json](https://github.com/ppy/SDL3-CS/blob/master/.config/dotnet-tools.json)
- [ppy/SDL3-CS generate_bindings.py](https://github.com/ppy/SDL3-CS/blob/master/SDL3-CS/generate_bindings.py)
- [dotnet/Silk.NET bindings-regeneration.yml](https://github.com/dotnet/Silk.NET/blob/main/.github/workflows/bindings-regeneration.yml)
- [dotnet/Silk.NET runner-setup.md](https://github.com/dotnet/Silk.NET/blob/main/documentation/for-contributors/runner-setup.md)
- [dotnet/ClangSharp](https://github.com/dotnet/ClangSharp)
- [Microsoft Learn — P/Invoke source generation (SYSLIB1054)](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib1050-1069)
- [Microsoft Learn — Native interoperability best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)
- [Microsoft Learn — Roslyn IIncrementalGenerator](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
