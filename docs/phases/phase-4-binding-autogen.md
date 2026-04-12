# Phase 4: Binding Auto-Generation

**Status**: PLANNED
**Depends on**: Phase 3 (SDL2 Complete) — can overlap for R&D

## Objective

Replace the current SDL2-CS imported bindings with auto-generated C# bindings, establishing a pipeline that can generate bindings for both SDL2 and SDL3 from their C headers.

## Why Auto-Generation?

1. **SDL3 requires it**: SDL3's API is fundamentally different from SDL2. There is no SDL3-CS equivalent of SDL2-CS that we can simply import. We need our own generation pipeline.
2. **Maintenance**: Manually maintaining ~11,000 lines of P/Invoke declarations across 6+ libraries is unsustainable.
3. **Version updates**: When SDL2 or SDL3 releases new versions with API additions, we want to regenerate rather than hand-patch.
4. **Quality**: Auto-generators can produce consistent marshalling, null checks, and string handling across all bindings.

## Approach: CppAst-Based Generator

After researching four different approaches (see [research/binding-autogen-approaches.md](../research/binding-autogen-approaches.md)), the recommended approach is **CppAst** (the same approach used by [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL)).

### Why CppAst Over Alternatives

| Criterion | CppAst | ClangSharp | c2ffi | c2ffi+c2cs |
|-----------|--------|-----------|-------|-----------|
| External dependencies | None (NuGet pkg) | Python + dotnet tool | CLI install | 2 dotnet tools |
| Stays in .NET ecosystem | Yes | No (Python) | No (CLI) | Partial |
| Customization | Full (custom C# generator) | RSP files + flags | Custom C# generator | JSON config |
| Learning curve | Low | Medium | Medium | High |
| CI automation | Easy | Medium | Medium | Best |

CppAst provides the best balance of simplicity (pure .NET, NuGet package) and flexibility (full AST access for custom generation logic).

## Scope

### 4.1 Generator Project

Create `src/Generator/` as a standalone .NET console application:

```
src/Generator/
├── Generator.csproj           ← Targets net9.0, references CppAst NuGet
├── Program.cs                 ← Entry point: parse headers → generate C#
├── CsCodeGenerator.cs         ← Core type mapping and generation logic
├── CsCodeGenerator.Enums.cs   ← Enum generation (partial class)
├── CsCodeGenerator.Structs.cs ← Struct generation
├── CsCodeGenerator.Functions.cs ← Function/P/Invoke generation
├── CsCodeGenerator.Constants.cs ← Constant generation
├── CodeWriter.cs              ← Text output helper
└── include/                   ← Vendored SDL2/SDL3 headers (or sourced from submodule)
```

### 4.2 Generation Pipeline

```
SDL2/SDL3 C headers (from submodule or vendored)
    ↓
CppAst.CppParser.ParseFile()  (libclang-based parsing)
    ↓
CppCompilation AST  (types, functions, enums, structs, constants)
    ↓
Custom CsCodeGenerator  (type mapping, marshalling rules, naming conventions)
    ↓
Generated/*.cs  (one file per category or per-header)
    ↓
src/SDL2.Core/Generated/  (or src/SDL3.Core/Generated/)
```

### 4.3 SDL2 Migration

1. Generate SDL2 bindings from `external/sdl2-cs/` headers (or directly from SDL2 headers)
2. Validate generated output matches current SDL2-CS functionality
3. Replace `<Compile Include="../../external/sdl2-cs/src/SDL2.cs" />` with generated files
4. Run smoke tests to verify everything still works

### 4.4 SDL3 Preparation

The same generator should handle SDL3 headers with minimal configuration changes:

- Different header include paths
- Different library name for `DllImport` (`SDL3` vs `SDL2`)
- Different type mappings (SDL3 changed bool semantics, removed SDL_RWops, etc.)

## Exit Criteria

- [ ] Generator project builds and produces C# bindings from SDL2 headers
- [ ] Generated SDL2 bindings compile and pass smoke tests
- [ ] SDL2-CS imports replaced with generated code
- [ ] Generator can also produce SDL3 bindings (validated by compilation)
- [ ] Generation is documented and reproducible
- [ ] Generated code is committed to repo (not generated at build time)

## Open Questions

1. **LibraryImport vs DllImport**: Modern `[LibraryImport]` (net7.0+) vs traditional `[DllImport]` (all targets). May need both for multi-TFM support.
2. **String marshalling**: SDL functions use UTF-8 strings. Need consistent approach (custom marshaller, Unsafe_ prefix + source generator, or explicit encoding).
3. **Header source**: Vendor SDL headers in the repo (Alimer approach) or parse from submodule?
4. **Safe wrappers**: Generate only raw P/Invoke, or also generate safe overloads (ref/out parameters, span-based, string-returning)?

## References

- [Alimer.Bindings.SDL Generator](https://github.com/amerkoleci/Alimer.Bindings.SDL/tree/main/src/Generator)
- [CppAst NuGet Package](https://www.nuget.org/packages/CppAst)
- [ppy/SDL3-CS ClangSharp approach](https://github.com/ppy/SDL3-CS)
- [research/binding-autogen-approaches.md](../research/binding-autogen-approaches.md)
