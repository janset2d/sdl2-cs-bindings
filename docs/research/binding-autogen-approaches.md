# Research: C# Binding Auto-Generation Approaches

**Date**: 2026-04-11
**Context**: Evaluating tools for auto-generating C# P/Invoke bindings from SDL2/SDL3 C headers.

## Conclusion

**Recommended: CppAst** (Alimer approach) for initial implementation. Lowest friction, purely .NET, full customization. Consider ClangSharp (ppy approach) as a future upgrade path if the custom generator becomes a maintenance burden.

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
- No external dependencies (NuGet package only)
- Full control over generated code
- Easy to debug (it's just C#)
- Stays in .NET ecosystem (no Python, no CLI tools)
- Can generate both `DllImport` and `LibraryImport` variants

**Cons**:
- Custom generator must be maintained
- Type mapping rules are manual (no database of SDL quirks)
- Single-platform parsing (depends on platform-specific preprocessor defines)

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
| **Weighted Total** | | **23** | **22** | **15** | **19** |

## Migration Path

Start with CppAst → if maintenance becomes painful, migrate to ClangSharp. The generated code is the same format (C# files with P/Invoke declarations), so the migration is about the generator, not the output.

## Sources

- [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL)
- [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS)
- [flibitijibibo/SDL3-CS](https://github.com/flibitijibibo/SDL3-CS)
- [bottlenoselabs/SDL3-cs](https://github.com/bottlenoselabs/SDL3-cs)
- [CppAst NuGet](https://www.nuget.org/packages/CppAst)
- [ClangSharp](https://github.com/dotnet/ClangSharp)
