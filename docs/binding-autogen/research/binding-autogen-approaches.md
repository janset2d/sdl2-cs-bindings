# Research: C# Binding Auto-Generation Approaches

**Date**: 2026-04-11 (initial) · 2026-05-12 (update — §"2026-05-12 Update")
**Context**: Evaluating tools for auto-generating C# P/Invoke bindings from SDL2/SDL3 C headers.
**Companion**: [`binding-autogen-feasibility.md`](binding-autogen-feasibility.md) — modern .NET marshalling emit targets, vcpkg/header/cross-platform interaction, manual intervention surface, testing strategy.

> **Decision note (2026-05-14):** This remains the research record. The accepted strategy brief and ADR-004 select the CppAst path for Phase 4 planning while preserving ClangSharp as the documented migration path.

## Current Evidence Snapshot

**Initial recommendation (2026-04-11):** CppAst (Alimer approach) — lowest orchestration friction, C#-owned emitter, full customization. It is still libclang-backed; "pure .NET" here means no separate Python/CLI generator layer, not no native parser dependency.

**ClangSharp-favoring research recommendation (2026-05-12 after deeper research):** **ClangSharpPInvokeGenerator** (ppy/SDL3-CS pattern). Driven by:

1. Re-evaluation of the 2026-04-11 decision matrix: **3 of 6 rows were rescored and the weighting model was made explicit** (".NET ecosystem fit" was inverted, "Community backing" understated, "Setup simplicity" anchored on ppy's Python-orchestration-by-choice rather than ClangSharp's actual requirement). Recalculated weighted total: CppAst 43 vs ClangSharp 63. See §2026-05-12 Update — Decision Matrix Re-Validation.
2. Production-peer asymmetry: more C-header binding precedent around ClangSharp / ClangSharp-derived tooling (TerraFX × 3, Silk.NET 3.0, ppy/SDL3-CS) than CppAst (3 in amerkoleci's orbit + SkiaSharp). CsWin32 and win32metadata remain useful Microsoft interop references, but they are `.winmd`/Roslyn metadata pipelines, not C-header ClangSharp consumers.
3. Source-level comparison of ppy/SDL3-CS vs Alimer.Bindings.SDL: ppy's pipeline is ~32 KB total (~1/3 Alimer's ~100 KB), uses declarative RSP overrides instead of imperative C# changes, handles multi-platform parsing via N+1 ClangSharp passes (materially more rigorous for platform-conditioned SDL headers than Alimer's single-pass-with-all-defines). See §2026-05-12 Source-Level Comparison.
4. Active .NET Foundation governance + 3+ active Microsoft committers vs single-maintainer + 3 multi-year-stale issues.

**Current status:** this is research evidence, not a final project decision. The WHY/HOW/WHAT design doc owns the decision. Deniz currently leans CppAst and wants more information, especially around whether production-shape scope makes a single custom emitter more valuable than ClangSharp's smaller raw-binding setup.

**Validation strategy (added 2026-05-12):** Cross-check generator output against existing third-party bindings — `external/sdl2-cs` (already vendored) for SDL2 ground-truth, [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS) for SDL3 ground-truth and ClangSharp reference. They are typing + pattern references, not binding sources. See feasibility doc §Reference Cross-Check Strategy.

## Approaches Compared

### 1. CppAst — Custom .NET Generator (Alimer.Bindings.SDL)

**Tool**: [CppAst](https://www.nuget.org/packages/CppAst) NuGet package (v0.21.1+)
**Used by**: [amerkoleci/Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL)

**How it works**:

- CppAst is a .NET library that wraps libclang to parse C/C++ headers into a .NET AST
- A custom C# console app reads the AST and generates binding code
- Alimer's generator is ~1000 lines across 6 partial class files

**Project structure**:

```
src/Generator/
├── Generator.csproj           ← net9.0, refs CppAst
├── Program.cs                 ← Parse headers, invoke generator
├── CsCodeGenerator.cs         ← Core type mapping
├── CsCodeGenerator.Commands.cs   ← Functions/callbacks
├── CsCodeGenerator.Constants.cs  ← #define constants
├── CsCodeGenerator.Enum.cs       ← Enums
├── CsCodeGenerator.Handles.cs    ← Opaque handle types
├── CsCodeGenerator.Structs.cs    ← Structs/unions
└── CodeWriter.cs              ← Text output helper
```

**Generation pipeline**:

1. List SDL header files explicitly in Program.cs
2. Call `CppParser.ParseFile()` on each header
3. Walk the `CppCompilation` AST
4. Generate C# code using CodeWriter
5. Output to `Generated/` directory

**Pros**:

- No separate generator CLI or Python layer; orchestration stays in C#
- Full control over generated code
- Easy to debug (it's just C#)
- Stays in .NET ecosystem (no Python, no CLI tools)
- Can generate both `DllImport` and `LibraryImport` variants

**Cons**:

- Custom generator must be maintained
- Type mapping rules are manual (no database of SDL quirks)
- libclang runtime/version coupling still exists and must be pinned with CppAst
- Alimer's example is single-pass; a production CppAst path still needs explicit neutral + platform parse views for platform-conditioned headers

### 2. ClangSharp — Official LLVM .NET Binding (ppy/SDL3-CS)

**Tool**: [ClangSharpPInvokeGenerator](https://github.com/dotnet/ClangSharp) (official .NET Foundation project)
**Used by**: [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS) (osu! team, 405K NuGet downloads)

**How it works**:

- ClangSharp is the official .NET wrapper for LLVM/Clang
- `ClangSharpPInvokeGenerator` is a dotnet tool that generates C# from C headers
- Orchestrated by a Python script (`generate_bindings.py`) with per-header RSP override files
- A Roslyn source generator creates safe string-handling overloads

**Pipeline complexity**:

1. Python script constructs ClangSharp command with 50+ flags
2. Per-header `.rsp` files for type overrides
3. Platform-specific generation (separate runs with `-D WIN32`, `-D __linux__`, etc.)
4. Validates output against SDL's own `sdl.json` API dump
5. Roslyn source generator produces `Unsafe_` → safe overloads

**Pros**:

- Battle-tested on 400K+ download package
- Produces highest quality raw bindings (`[NativeTypeName]` annotations, `[SupportedOSPlatform]`)
- Per-header RSP files allow surgical overrides
- Official LLVM tooling

**Cons**:

- Requires Python for orchestration
- Complex flag configuration
- Per-header RSP maintenance overhead
- Harder to debug (tool is a black box)

### 3. c2ffi — JSON Intermediate Format (flibitijibibo/SDL3-CS)

**Tool**: [c2ffi](https://github.com/rpav/c2ffi) (CLI tool)
**Used by**: [flibitijibibo/SDL3-CS](https://github.com/flibitijibibo/SDL3-CS) (original SDL2-CS author)

**How it works**:

- c2ffi parses C headers and outputs a JSON FFI description
- A custom C# program reads the JSON and generates bindings
- A massive `UserProvidedData.cs` (~93KB) specifies pointer semantics for every parameter

**Unique feature**: Dual output — `SDL3.Core.cs` (modern `LibraryImport`) and `SDL3.Legacy.cs` (traditional `DllImport`).

**Pros**:

- JSON intermediate allows decoupled parsing/generation
- Best `ref`/`out`/`in` parameter accuracy (hand-curated per-function)
- Dual modern/legacy output
- Small generator footprint

**Cons**:

- Requires installing c2ffi CLI (not a NuGet package)
- 93KB hand-curated data file is a maintenance burden
- Single-platform parsing

### 4. c2ffi + c2cs — Full Pipeline (bottlenoselabs/SDL3-cs)

**Tool**: [c2ffi + c2cs](https://github.com/bottlenoselabs) (dotnet global tools)
**Used by**: [bottlenoselabs/SDL3-cs](https://github.com/bottlenoselabs/SDL3-cs)

**How it works**:

- Three-stage pipeline: Extract (per-platform) → Merge (cross-platform) → Generate (C#)
- Runs on multiple platforms in CI, producing platform-specific FFIs that are merged
- **Only project with fully automated CI generation**

**Unique features**:

- True cross-platform extraction (runs on Windows, Linux, macOS in CI)
- High-level OOP wrapper layer on top of raw bindings
- Per-RID NuGet native packages

**Pros**:

- Most architecturally correct approach
- CI-automated generation
- Cross-platform awareness built in

**Cons**:

- Depends on less widely-used tools
- Complex CI setup (3 jobs: extract, merge, generate)
- OOP wrapper adds maintenance surface

## Decision Matrix

| Factor | Weight | CppAst | ClangSharp | c2ffi | c2ffi+c2cs |
|--------|--------|--------|-----------|-------|-----------|
| Setup simplicity | HIGH | 5 | 3 | 2 | 2 |
| .NET ecosystem fit | HIGH | 5 | 3 | 3 | 4 |
| Output quality | MEDIUM | 4 | 5 | 4 | 3 |
| Maintenance burden | HIGH | 3 | 4 | 2 | 4 |
| CI automation | LOW (for now) | 3 | 3 | 2 | 5 |
| Community backing | MEDIUM | 3 | 5 | 3 | 2 |
| **Weighted Total** | | **23** | **23** | **15** | **19** |

> Originally published 2026-04-11 with ClangSharp summed as 22; corrected to 23 on 2026-05-14 (column sums to 3+3+5+4+3+5=23). The "Weighted Total" label is historical — these rows were combined as unweighted sums; the actual HIGH/MEDIUM/LOW weighting was introduced on 2026-05-12 (see §"Decision matrix re-validation" below).

## Migration Path

Start with CppAst → if maintenance becomes painful, migrate to ClangSharp. The generated code is the same format (C# files with P/Invoke declarations), so the migration is about the generator, not the output.

## 2026-05-12 Update — Industry State + Scope Clarifications

Survey re-validated one month after the initial recommendation. The initial CppAst recommendation remained plausible, but later source-level comparison and spikes reframed it as a toolchain candidate rather than a settled decision. Key clarifications that shift implementation expectations:

### CppAst current state

- **v0.24.0 (2025-11-20)**, BSD-2-Clause. Path since v0.21.1: additive only (Objective-C support, `CppInclusionDirective`, libclang 20.1.2 bump). **No breaking changes** that affect generator design.
- Targets `net8.0`. Compatible with our `.NET 10` baseline.

### Alimer.Bindings.SDL — **SDL3 core only, NOT satellites**

Critical clarification: Alimer.Bindings.SDL **covers SDL3 core only**. It does NOT generate bindings for `SDL3_image` / `SDL3_mixer` / `SDL3_ttf`. Source: package scope on NuGet (`Alimer.Bindings.SDL` 3.9.8, 2025-04-04) + repo inspection.

**Lift-and-shift is feasible with caveats:**

- License: MIT (compatible with our distribution).
- Generator scope: `src/Generator/` ≈ 9 files (`Program.cs`, `CsCodeGenerator.cs` + 5 partial files for Commands/Constants/Enum/Handles/Structs, `CodeWriter.cs`, `CsCodeGeneratorOptions.cs`).
- Generator package dep: CppAst 0.21.1 in Alimer (we'd bump to 0.24.0), plus explicit libclang runtime/version pinning in our repo. CppAst removes a separate generator CLI, not libclang coupling.
- No Alimer-specific shared infrastructure; the generator is self-contained.

**What needs added beyond pure copy:**

1. Satellite header sets (sdl2-image/_mixer/_ttf/_gfx/_net + sdl3-image/_mixer/_ttf), per-family generator configs.
2. Multi-TFM emission (Alimer targets a single TFM; we need net10/net9/net8/netstandard2.0/net462 dual-emission of `[LibraryImport]` and `[DllImport]`).
3. SDL2 surface coverage (Alimer is SDL3-only; SDL2 family-naming + API patterns differ).
4. Sibling reference: [`amerkoleci/Vortice.Vulkan`](https://github.com/amerkoleci/Vortice.Vulkan) (same author, identical `src/Generator/` pattern) — confirms the layout's reusability across native libraries.

Realistic effort: **port-and-adapt**, not pure copy. Roughly 1.5–2× Alimer's surface area when satellites + multi-TFM are factored in.

### Silk.NET 3.0 pivot to ClangSharp

[dotnet/Silk.NET 3.0 generation proposal](https://github.com/dotnet/Silk.NET/blob/main/documentation/proposals/Proposal%20-%20Generation%20of%20Library%20Sources%20and%20PInvoke%20Mechanisms.md) explicitly delegates parsing to ClangSharp: *"Silk.NET will no longer do any parsing and interpretation of C headers or XML of C headers, instead we will delegate this to the ClangSharp P/Invoke Generator library."*

Their pipeline `SilkTouch` wraps ClangSharp with a "mod" architecture (init → input prep → ClangSharp run → syntax-tree mods → MSBuild workspace mods). Open-source, MIT, reference-quality.

**Implication for our project:** Confirms ClangSharp as the credible heavyweight alternative when XML/registry-style metadata transformations are needed. **For a focused SDL2+SDL3 binding scope, this is heavier than necessary** — Alimer/CppAst pattern is the proportionate choice. ClangSharp remains the documented future migration target if our custom generator becomes a maintenance burden.

### Production-grade peer survey

Brief landscape of what other 2026 native-binding projects use:

| Project | Generator | Output style |
| --- | --- | --- |
| amerkoleci/Alimer.Bindings.SDL (SDL3) | CppAst (custom) | `LibraryImport`, opaque handle structs, `delegate*` callbacks |
| amerkoleci/Vortice.Vulkan | CppAst (custom, same pattern as Alimer) | Same |
| dotnet/Silk.NET 2.x (SDL2, SDL3, Vulkan, OpenGL, WebGPU…) | SilkTouch / BuildTools | `Ref`/`Ptr` wrapper types replacing overload-explosion |
| dotnet/Silk.NET 3.0 (in-flight) | ClangSharp + SilkTouch mods | Same goal, different parser |
| ppy/SDL3-CS (SDL3 + image + mixer + ttf) | ClangSharpPInvokeGenerator + Dockerfile + Python | High-quality `DllImport` raw bindings + friendly overload source generator |
| flibitijibibo/SDL3-CS (SDL3 core only) | c2ffi JSON + custom C# emitter | Dual output: `Core.cs` (LibraryImport) + `Legacy.cs` (DllImport) |
| bottlenoselabs/SDL3-cs (SDL + image + ttf) | c2cs + c2ffi tools | Auto-generated raw + hand-curated OOP wrapper |
| FFmpeg.AutoGen | CppSharp (formerly ClangSharpUnsafeGenerator) | DllImport-based |
| Microsoft/CsWin32 | Roslyn source generator from `.winmd` metadata | Win32-specific, not applicable here |
| FFmpeg/OpenCV mature wrappers (Emgu.CV, OpenCvSharp) | Hand-written | Hand-written P/Invokes, no auto-generation |

**No new tool entered the production-grade landscape in 2025–2026.** No "Microsoft official PInvokeGenerator," no Roslyn-incremental-generator that ingests raw C headers (header parsing always stays in an out-of-band tool), no widely-adopted LLM-driven binding generator.

### Decision matrix re-validation — recommendation flips

The 2026-04-11 matrix was an unweighted 6-dimension comparison whose columns actually sum to **CppAst 23 vs ClangSharp 23 (tied)**, even though the published row labeled it "23 vs 22" (arithmetic error in the original publication, corrected 2026-05-14 — see note below the original matrix). A deeper source-level review on 2026-05-12 both rescored **3 of the 6 rows** and made the intended HIGH/MEDIUM/LOW weighting explicit. The bigger numeric swing therefore comes from two changes: corrected row scores plus an explicit weighting model.

| Factor | Weight | CppAst (orig → revised) | ClangSharp (orig → revised) | Reason for revision |
| --- | --- | --- | --- | --- |
| Setup simplicity | HIGH | 5 → **4** | 3 → **4** | The "ClangSharp requires Python" claim was anchored on ppy/SDL3-CS's choice. Python is NOT required by ClangSharp itself — PowerShell-only and pure-C# orchestration are equally valid. Both tools tie on setup now. |
| .NET ecosystem fit | HIGH | 5 → **3** | 3 → **5** | **Inverted in the original matrix.** ClangSharp is a .NET Foundation project with Microsoft-paid maintainers (tannergooding, Xamarin team), used by TerraFX, Silk.NET 3.0, and ppy/SDL3-CS for C-header binding work. CsWin32 and win32metadata are Microsoft interop references, but they are `.winmd`/Roslyn metadata pipelines and should not be counted as C-header ClangSharp consumers. CppAst is an independent library that depends on ClangSharp/libclang internally. |
| Output quality | MEDIUM | 4 → **3** | 5 → **5** | ClangSharp ships attribute-rich output OOTB (`[NativeTypeName]`, `[SupportedOSPlatform]`). CppAst ships an AST — you write the emitter. Gap wider than initially scored, although the SDL2_gfx spike found `LibraryImport` conversion still needs a separate decision. |
| Maintenance burden | HIGH | 3 → **3** | 4 → **4** | Direction was right (ClangSharp lower burden); magnitude was understated. Alimer's CppAst generator is ~100 KB / 2-5K LoC of custom code owned forever; ppy's ClangSharp setup is RSP files + thin orchestrator. |
| CI automation | LOW | 3 → **3** | 3 → **4** | ClangSharp is a CLI tool invokable directly from CI; libclang ships as platform-specific runtime NuGet packages. CppAst requires building a C# generator project as a CI step. |
| Community backing | MEDIUM | 3 → **2** | 5 → **5** | CppAst solo-maintained (xoofx, respected but one person), 28 open issues, 3 multi-year-stale critical bugs (#88, #81, #106). ClangSharp has 3+ active Microsoft committers + .NET Foundation governance + tighter response cadence. Gap wider than initially scored. |

**Recalculated weighted total (HIGH=3, MEDIUM=2, LOW=1):**

- CppAst: 3·(4+3+3) + 2·(3+2) + 1·3 = **43**
- ClangSharp: 3·(4+5+4) + 2·(5+5) + 1·4 = **63**

**This flips the research recommendation, not the project decision.** ClangSharpPInvokeGenerator + ppy/SDL3-CS pattern (per-RID RSPs + thin orchestrator) is the strongest raw-binding evidence point for Janset.SDL2/SDL3, but the WHY/HOW/WHAT document still owns the final toolchain decision.

**When CppAst would still be the right call:**

- Use case is "raw bindings + custom-emitted public API on top" (SkiaSharp pattern: ~63 KB SkiaSharpGenerator with JSON config + three-way-conditional emission `[LibraryImport]` / `[DllImport]` / Delegates-with-dlsym for WASM, which ClangSharp's RSP grammar cannot express).
- Use case is "generic C++ AST analysis, not P/Invoke generation" (documentation generators, refactoring tools, code analyzers).
- Maximum customization freedom is more valuable than declarative configuration.

Neither applies to our current scope.

### Validation strategy — Use existing third-party bindings as typing oracles

`external/sdl2-cs` (Ethan Lee's SDL2# imports, zlib license, already vendored in this repo) provides every SDL2 + SDL2_image + SDL2_mixer + SDL2_ttf + SDL2_gfx P/Invoke signature in `[DllImport]` form. Total: 11,105 lines across 5 files. **This is our SDL2 ground truth** for "what type does this header field/argument map to?" — not a binding source, but a cross-check oracle.

[ppy/SDL3-CS](https://github.com/ppy/SDL3-CS) (MIT, auto-generated via ClangSharp + Docker, covers SDL3 core + Image + Mixer + TTF — the exact scope we plan for SDL3) provides the same role for SDL3, **and additionally serves as the ClangSharp reference if that toolchain wins**.

When our generator emits a function signature, we diff against the corresponding ground-truth signature in the reference project. Mismatches surface type-mapping bugs (e.g., `int` vs `SDL_bool`, `IntPtr` vs typed handle struct, `string` vs `byte*`). Captured in detail in [`binding-autogen-feasibility.md`](binding-autogen-feasibility.md) §Reference Cross-Check Strategy.

## 2026-05-12 Source-Level Comparison — ppy/SDL3-CS vs amerkoleci/Alimer.Bindings.SDL

Apples-to-apples comparison of the two closest production SDL3 binding generators in .NET. Both produce SDL3 bindings; they differ in toolchain (ClangSharp vs CppAst) and design philosophy. Investigation date: 2026-05-12.

### Generator surface area

| Aspect | ppy/SDL3-CS (ClangSharp) | Alimer (CppAst) |
| --- | --- | --- |
| Generator location | `SDL3-CS.SourceGeneration/` (Roslyn) + `SDL3-CS/generate_bindings.py` + `*.rsp` config files | `src/Generator/` (standalone CLI program) |
| Total LoC | **~32 KB** (16 KB C# Roslyn + 14 KB Python + 3 KB RSP) | **~100 KB** pure C# (10 files) |
| Languages | C# + Python + RSP | C# only |
| Heavy lifting location | ClangSharp tool (external dependency) | Custom generator project (owned forever) |

ppy is ~1/3 the surface because the C parsing + C# emission live in ClangSharp. Alimer's larger surface is owned code.

### Configuration philosophy

ppy: declarative `.rsp` files. Example from `SDL3-CS/SDL3/SDL_audio.rsp`:

```text
--with-type
SDL_AudioFormat=uint
```

Alimer: imperative C# code. Example from `src/Generator/CsCodeGenerator.cs`:

```csharp
private static readonly Dictionary<string, string> s_csNameMappings = new()
{
    { "Sint8", "sbyte" }, { "Uint8", "byte" }, { "Uint16", "ushort" },
    { "uint32_t", "uint" }, { "Uint32", "uint" }, { "Uint64", "ulong" },
    { "SDL_FunctionPointer", "delegate* unmanaged<void>" },
    { "SDL_GUID", "Guid" },
    // …
};
```

ppy's overrides are RSP grammar (`--remap`, `--exclude`, `--with-attribute`, `--with-using`, `--with-type`). Change behavior = edit RSP, re-run, done. Alimer's overrides are C# `if/else` chains + dictionary literals; change behavior = edit C#, rebuild generator, re-run. ppy's model wins on iteration speed; Alimer's wins on expressive ceiling (arbitrary C# logic).

### Output style — same function side by side

**`SDL_GetError`:**

```csharp
// ppy/SDL3-CS — DllImport + Unsafe_ prefix; friendly string overload synthesized
// by FriendlyOverloadGenerator (Roslyn source generator) at consumer compile time
[DllImport("SDL3", CallingConvention = CallingConvention.Cdecl,
           EntryPoint = "SDL_GetError", ExactSpelling = true)]
[return: NativeTypeName("const char *")]
public static extern byte* Unsafe_SDL_GetError();
```

```csharp
// Alimer — LibraryImport + eager wrapper emission with full doxygen preservation
/// <summary>
/// Retrieve a message about the last error that occurred on the current thread.<br/>
/// …
/// </summary>
[LibraryImport(LibName, EntryPoint = "SDL_GetError")]
public static partial byte* SDL_GetErrorPtr();

public static string? SDL_GetError()
{
    return ConvertToManaged(SDL_GetErrorPtr());
}
```

Style deltas:

- ppy emits `[DllImport]` + `extern`; Alimer emits `[LibraryImport]` + `partial` (AOT-friendlier by default).
- ppy preserves C type provenance via `[NativeTypeName("const char *")]`; Alimer does not.
- ppy synthesizes the friendly `string?` overload at *consumer compile time* via Roslyn source generator; Alimer emits it eagerly in the generated file.
- Alimer preserves doxygen as `<summary>…<br/></summary>`; ppy emits no xmldoc.

Note: ClangSharp 21.1.8.3 still emits `[DllImport]` under `latest-codegen` / `preview-codegen`; those options select modern language/runtime shapes, not `[LibraryImport]`. If a ClangSharp path wins, `LibraryImport` requires a SYSLIB1054 analyzer fix, Roslyn post-process, or another explicit conversion path.

### Multi-platform parsing — sharp asymmetry

ppy runs **N+1 ClangSharp passes per platform-variant header**: one platform-agnostic + one per OS. Each pass emits `*.<Platform>.g.cs` (e.g. `SDL_system.Android.g.cs`, `SDL_system.Linux.g.cs`, `SDL_system.Windows.g.cs`) with `[SupportedOSPlatform("…")]` injected via RSP `--with-attribute`:

```python
generate_platform_specific_headers(sdl_api, system_header, [
    (["SDL_PLATFORM_ANDROID"], "Android", "Android"),
    (["SDL_PLATFORM_IOS"],     "iOS",     "iOS"),
    (["SDL_PLATFORM_LINUX"],   "Linux",   "Linux"),
    (["SDL_PLATFORM_WINDOWS", "SDL_PLATFORM_WIN32"], "Windows", "Windows"),
    (["SDL_PLATFORM_GDK"],     "GDK",     "Windows"),
])
```

Alimer parses **once** with multiple platform defines simultaneously set, emits no `[SupportedOSPlatform]` markers:

```csharp
var options = new CppParserOptions
{
    Defines = { "SDL_PLATFORM_ANDROID", "SDL_PLATFORM_IOS", "SDL_PLATFORM_WINRT" }
};
```

For our **7-RID matrix** (Windows × 3, Linux × 2, macOS × 2), ppy's strategy is materially more rigorous. OS-specific functions (e.g., `SDL_SetWindowsMessageHook`) get correctly attributed compile-time, preventing runtime `DllNotFoundException` on consumer platforms that don't have the symbol.

Important nuance: ppy's `generate_bindings.py` is documented as a manually-run script. Its multi-platform output is produced by multiple ClangSharp invocations with different macro views, not by running the generator on Windows/Linux/macOS and merging native-host outputs. By contrast, bottlenoselabs/SDL3-cs is the stronger true multi-OS extraction reference: its bindgen workflow extracts FFI data on Windows, macOS, and Linux runners, merges those platform artifacts into a cross-platform intermediate, then generates C#. That approach is more rigorous when host/sysroot fidelity is required, but it carries a larger merge/conflict-resolution burden than ppy's pass-and-exclude model.

### Variadic functions — different philosophies

`SDL_Log(const char *fmt, ...)`:

```csharp
// ppy — preserves __arglist (information retained; ergonomics deferred to caller)
[DllImport("SDL3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
public static extern void SDL_Log([NativeTypeName("const char *")] byte* fmt, __arglist);
```

```csharp
// Alimer — drops ellipsis; emits fmt-only overloads (pragmatic; information loss)
[LibraryImport(LibName, EntryPoint = "SDL_Log")]
public static partial void SDL_Log(byte* fmt);

[LibraryImport(LibName, EntryPoint = "SDL_Log")]
public static partial void SDL_Log(ReadOnlySpan<byte> fmt);
```

ppy's choice keeps the C signature visible; if a consumer needs true variadic native calls, they can write IL helpers. Alimer's choice ships pragmatic ergonomics out of the box at the cost of representational fidelity. Trade-off, not a defect on either side.

### Bit-flag enum surprise

When the C type is `Uint64 + #define`-driven (e.g., `SDL_WindowFlags`), **neither generator auto-emits a `[Flags] enum`**. ppy emits constants (`public const ulong SDL_WINDOW_FULLSCREEN = …`). Alimer ignores the C symbol entirely and the maintainer **hand-writes** the enum in `src/Alimer.Bindings.SDL/SDL.cs`. For typedef-enum-style flags, Alimer auto-detects via the `csName.EndsWith("Flags")` heuristic; ppy emits the underlying `int` enum without `[Flags]`.

Implication for our generator: the `[Flags]` attribute will be **either RSP-declared (`--with-attribute SDL_WindowFlags=Flags`) per-type or hand-written wrapper at the consumer layer**. No tool gives this for free.

### SkiaSharpGenerator validates the "CppAst sweet spot = custom emission" hypothesis

`utils/SkiaSharpGenerator/` is ~63 KB C# with a JSON config schema (`ConfigJson/*.cs`). Skia emits a **three-way conditional** per P/Invoke:

```csharp
#if !USE_DELEGATES
#if USE_LIBRARY_IMPORT
    [LibraryImport(SKIA)]
    internal static partial void gr_backendrendertarget_delete(gr_backendrendertarget_t r);
#else
    [DllImport(SKIA, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gr_backendrendertarget_delete(gr_backendrendertarget_t r);
#endif
#else
    // Delegates-with-dlsym mode, for WASM/iOS-static-link/etc.
    // where [DllImport] doesn't work at runtime
    private partial class Delegates { /* … */ }
    private static Delegates.gr_backendrendertarget_delete gr_backendrendertarget_delete_delegate;
    internal static void gr_backendrendertarget_delete(gr_backendrendertarget_t r) =>
        (gr_backendrendertarget_delete_delegate ??= GetSymbol<Delegates.gr_backendrendertarget_delete>("…"))(r);
#endif
```

This emission **cannot be expressed in ClangSharp's CLI grammar**. The Skia generator's raw P/Invoke output (1.1 MB) is consumed by ~200 hand-written idiomatic types (`SKBitmap`, `SKCanvas`, etc.) — the public API is curated, the generator produces only the raw layer.

This validates the framing: **CppAst's value is custom emission strategy, not raw P/Invoke fidelity.** For raw-P/Invoke-only use cases (Janset.SDL2/SDL3 as scoped today), ClangSharp is strictly leaner. For raw-bindings-plus-custom-public-API use cases (Skia), CppAst's freedom wins. If Janset evolves to a Skia-style hybrid (raw bindings auto-generated + curated `SDL2`/`SDL3` C# wrapper layer hand-written), the toolchain decision is worth revisiting.

### Net observations affecting our decision

1. ppy's pipeline is ~1/3 the surface of Alimer's (~32 KB vs ~100 KB).
2. ppy's config is RSP-declarative (one-line per override); Alimer's is C#-imperative (rebuild generator per change).
3. Alimer ships `[LibraryImport]` by default; ppy ships `[DllImport]`. The SDL2_gfx spike found ClangSharp 21.1.8.3 still emits `[DllImport]` under `latest-codegen`, so `LibraryImport` requires analyzer auto-fix, post-processing, or another explicit conversion path.
4. ppy preserves `[NativeTypeName]` C-provenance everywhere; Alimer drops it.
5. Alimer preserves doxygen xmldoc; ppy does not (acceptable trade-off; doc can come from a separate pass).
6. **Multi-platform handling: ppy's N+1 pass is materially more rigorous than Alimer's single-pass-with-all-defines, and this matters for our 7-RID scope.** Critical risk surface.
7. ppy's `FriendlyOverloadGenerator` Roslyn source-generator-on-top-of-ClangSharp-output is a pattern worth adopting (compile-time synthesis of friendly overloads keeps generated files lean).
8. SkiaSharpGenerator (CppAst, custom-emission use case) validates "use the right tool for the use case" framing — Skia's three-way-conditional emission requires CppAst's freedom. If Janset scope stays raw-P/Invoke-only, ClangSharp's leaner default is compelling; if production-shape scope needs custom emission, CppAst becomes more compelling.

## Sources

- [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL)
- [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS)
- [flibitijibibo/SDL3-CS](https://github.com/flibitijibibo/SDL3-CS)
- [bottlenoselabs/SDL3-cs](https://github.com/bottlenoselabs/SDL3-cs)
- [CppAst NuGet](https://www.nuget.org/packages/CppAst)
- [ClangSharp](https://github.com/dotnet/ClangSharp)
