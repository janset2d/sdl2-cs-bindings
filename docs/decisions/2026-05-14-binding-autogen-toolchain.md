# ADR-004: Binding Auto-Generation Toolchain

- **Status:** Reopened (re-evaluation underway 2026-05-23)
- **Date:** 2026-05-14 (original); 2026-05-23 (reopened)

> **Re-evaluation note (2026-05-23):** The binding-autogen toolchain decision
> is under active re-evaluation in [`spikes/binding-generators/`](../../spikes/binding-generators/).
> Both **ClangSharp + Roslyn postprocess** and **Alimer-style single-pass CppAst**
> are being measured against the same evidence matrix (multi-TFM compile, multi-OS
> parse, dynapi coherence, semantic ABI correctness across the 7-RID surface).
> Either selected path implies **replacing** the current Cake-hosted implementation
> under `build/_build/Targets/GenerateBindings/` — that pipeline is in sunset
> regardless of which toolchain wins. This ADR remains historical evidence of
> the 2026-05-14 reasoning but does not bind current work until the spike
> concludes; consult the spike outputs (`spikes/binding-generators/output/reports/`)
> for the active comparison evidence.
>
> Original content unchanged below.

## 1. Context

The project needs AST-generated bindings before the first public `-preview.N` wave. `external/sdl2-cs` is transitional and cannot be the stable public API surface: it is hand-written, SDL2-only, loses C type provenance, carries old string/char marshalling behavior, and cannot scale to SDL3 plus satellites.

The binding-autogen research compared two viable libclang-backed paths:

- **ClangSharpPInvokeGenerator**: strongest raw-binding economics, `[NativeTypeName]` provenance, RSP-driven configuration, ppy/SDL3-CS as the closest SDL reference.
- **CppAst custom emitter**: more owned generator code, but one C# codebase can absorb multi-TFM dual emit, typed handles, friendly overloads, platform attribution, and satellite/shared-type rules in a single offline emitter.

Both toolchains were spike-validated on SDL2_gfx. Both generated 102 working P/Invoke declarations, compiled, and passed the same runtime smoke app. The decision is therefore not about whether either tool works; it is about which ownership model fits the production-shape scope.

## 2. Decision

Phase 4 plans and implements a **CppAst-based C# emitter** for Janset.SDL2 bindings, with a parallel SDL3 emitter when Phase 5 activates.

The production toolchain is pinned as a version trio:

```xml
<PackageVersion Include="CppAst" Version="0.24.0" />
<PackageVersion Include="libclang.runtime.*" Version="20.1.2" />
<PackageVersion Include="libClangSharp.runtime.*" Version="20.1.2" />
```

The exact RID-specific runtime package set is finalized by the implementation plan, but every libclang/libClangSharp runtime package used by the generator must stay on the same `20.1.2` line unless CppAst is bumped and the full spike/runtime validation is repeated.

## 3. Locked implementation direction

The generator:

- reads canonical SDL headers from the vcpkg installation tree;
- runs controlled libclang parse views for neutral, Windows, Linux, and macOS surfaces from a single pinned Linux container;
- emits generated `.g.cs` source into each managed family under `Generated/`, committed to git;
- emits `LibraryImport` for `net7+` and `DllImport` fallback for legacy TFMs in the same emitter loop;
- targets the SDL2 TFM matrix `net10` / `net9` / `net8` / `netstandard2.0` / `net462`;
- emits typed `readonly partial struct` handle wrappers over `nint` for opaque native handles;
- emits friendly overloads for UTF-8 strings, spans, `out`, and `ref` shapes alongside raw P/Invoke entry points;
- keeps satellite outputs scoped to satellite-owned symbols while reusing core-owned SDL managed types;
- writes a per-family `.generated-stamp` recording generator, vcpkg, and header-set state;
- integrates vcpkg-state coherence validation before native build work and symbol-existence validation before packaging.

Generated code is never produced in consumer builds. Consumers receive normal NuGet packages with committed generated source compiled into the managed assemblies.

## 4. Why CppAst

At raw P/Invoke-only scope, ClangSharp is leaner. The research deliberately preserves that finding.

The project is not choosing raw P/Invoke-only scope. The accepted strategy requires multi-TFM dual emission, typed handles, friendly overloads, platform-conditioned attribution, and satellite/shared-type topology. Under that scope, ClangSharp grows through coordinated RSP configuration, post-processing, and Roslyn source-generation layers. CppAst grows mostly by adding emitter rules to one C# codebase.

The accepted bet: **single-emitter ownership is cheaper and clearer for this project's production surface than coordinating several smaller generation systems.**

## 5. Consequences

Positive:

- all binding-generation policy lives in ordinary C# code, matching the repository's .NET/Cake-centered toolchain;
- production-shape features can be emitted together rather than layered through post-process steps;
- platform-conditioned parsing and satellite/shared-type reuse can be represented in one generator model;
- generated diffs remain reviewable because generated source is committed.

Tradeoffs:

- the project owns more generator code than a ClangSharp RSP-first path;
- CppAst/libclang/libClangSharp version coupling is real and must be pinned deliberately;
- losing default `[NativeTypeName]` output means the emitter must decide whether and how to preserve C provenance for review;
- Stage 1 must prove the CppAst path at SDL2.Core production shape before satellite migration proceeds.

## 6. Migration door

ClangSharp remains the documented migration path.

Reopen this ADR if:

- CppAst version-trio coupling becomes an operational burden;
- CppAst cannot model a required SDL header shape without fragile custom code;
- the project deliberately reduces scope back toward raw P/Invoke-only output;
- ClangSharp gains first-class support that removes the current post-process / Roslyn-extension coordination tax.

Migration would replace the generator implementation, not the package contract. Generated C# source, package-first consumption, vcpkg-built natives, and D-3seg family versioning remain intact.

## 7. References

- [`../binding-autogen/binding-generator-constitution.md`](../binding-autogen/binding-generator-constitution.md)
- [`../binding-autogen/binding-generator-roadmap.md`](../binding-autogen/binding-generator-roadmap.md)
- [`../release-strategy.md`](../release-strategy.md)
- [`../phases/phase-4-binding-autogen.md`](../phases/phase-4-binding-autogen.md)
- [`2026-05-05-d3seg-and-package-first.md`](2026-05-05-d3seg-and-package-first.md)
