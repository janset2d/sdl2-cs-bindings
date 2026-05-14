# Phase 4: Binding Auto-Generation

**Status**: PLANNED — **critical path for v1.0** per [`../release-strategy.md`](../release-strategy.md)
**Order**: Lands **before** Phase 3 ship. First public `-preview.N` wave consumes AST-generated bindings, not the deprecated `external/sdl2-cs` imports. See [`../release-strategy.md`](../release-strategy.md) §Sequencing for rationale.

## Objective

Replace the current SDL2-CS imported bindings with auto-generated C# bindings, establishing a pipeline that can generate bindings for both SDL2 and SDL3 from their C headers.

## Why Auto-Generation?

1. **SDL3 requires it**: SDL3's API is fundamentally different from SDL2. There is no SDL3-CS equivalent of SDL2-CS that we can simply import. We need our own generation pipeline.
2. **Maintenance**: Manually maintaining ~11,000 lines of P/Invoke declarations across 6+ libraries is unsustainable.
3. **Version updates**: When SDL2 or SDL3 releases new versions with API additions, we want to regenerate rather than hand-patch.
4. **Quality**: Auto-generators can produce consistent marshalling, null checks, and string handling across all bindings.

## Approach: Toolchain Not Yet Decided

The Phase 4 toolchain is intentionally undecided until the WHY/HOW/WHAT design doc is accepted. Current work validates spike evidence for two viable candidates:

| Candidate | Strength | Risk / Cost |
| --- | --- | --- |
| CppAst custom emitter | Single C# codebase can absorb multi-TFM emission, friendly overloads, platform attribution, and custom macro handling in one offline generator. | More owned generator code and explicit CppAst/libclang version coordination. |
| ClangSharpPInvokeGenerator | Smaller raw-binding setup, strong `[NativeTypeName]` provenance, RSP-driven overrides, and ppy/SDL3-CS as a close reference. | Production-shape ergonomics may require coordinated RSP, post-processing, and Roslyn source-generation layers. |

The current goal is not to crown a winner; it is to validate the spike, identify missing evidence, and make the WHY/HOW/WHAT document decide with a clear trade-off record.

## Scope

### 4.1 Generator Project

Create or wire the selected generator tooling after the WHY/HOW/WHAT decision. File layout is decided at implementation time and must keep generated output committed, reproducible, and reviewable.

### 4.2 Generation Pipeline

```
SDL2/SDL3 C headers (from submodule or vendored)
    ↓
Selected AST parser/toolchain  (CppAst or ClangSharp/libclang)
    ↓
CppCompilation AST  (types, functions, enums, structs, constants)
    ↓
Generator/emission layer  (type mapping, marshalling rules, naming conventions)
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
- [ ] Platform-conditioned SDL headers are parsed through controlled neutral + platform-specific passes, with OS-only symbols attributed or isolated appropriately
- [ ] Generation is documented and reproducible
- [ ] Generated code is committed to repo (not generated at build time)

## Open Questions

1. **LibraryImport vs DllImport**: Modern `[LibraryImport]` (net7.0+) vs traditional `[DllImport]` (all targets). May need both for multi-TFM support.
2. **String marshalling**: SDL functions use UTF-8 strings. Need consistent approach (custom marshaller, Unsafe_ prefix + source generator, or explicit encoding).
3. **Header source**: Vendor SDL headers in the repo (Alimer approach) or parse from submodule?
4. **Safe wrappers**: Generate only raw P/Invoke, or also generate safe overloads (ref/out parameters, span-based, string-returning)?
5. **Platform passes**: Implement ppy-style neutral + platform-specific passes in ClangSharp orchestration, or implement equivalent pass orchestration in a CppAst emitter?

## References

- [Alimer.Bindings.SDL Generator](https://github.com/amerkoleci/Alimer.Bindings.SDL/tree/main/src/Generator)
- [CppAst NuGet Package](https://www.nuget.org/packages/CppAst)
- [ppy/SDL3-CS ClangSharp approach](https://github.com/ppy/SDL3-CS)
- [binding-autogen/README.md](../binding-autogen/README.md)
