# Semantic Binding Pipeline Design

> Status: Draft for review.
> Scope: Stage 1 SDL2.Core binding-generator architecture rewrite, with an immediate SDL2.Core replacement readiness lane.
> Translation-rule supersession (2026-05-19): Current scalar, variadic, struct/union, enum, and macro policy lives in [`../../binding-autogen/binding-translation-contract.md`](../../binding-autogen/binding-translation-contract.md). This draft remains architecture background only where it conflicts with that contract.

## Problem Statement

The binding generator has crossed the line from spike infrastructure to production pipeline. The current shape still carries spike-era design debt: type information is split between `build/manifest.json`, hard-coded translation policy, CppAst-specific translator logic, and early managed output strings such as `IntPtr`, `SDL_Surface*`, `uint`, and `Guid`.

That makes every new generated category re-solve the same ABI and API questions:

- Is this native type family-owned or external?
- Is it an opaque SDL handle, concrete struct, enum typedef, primitive typedef, callback, string pointer, array, or deferred type?
- Is the managed representation ABI-correct for SDL2, SDL3, and the target RID family?
- Is a decision family-specific configuration, durable generator policy, or an explicit exception?

The result is a fragile string-first pipeline. Emitters and validators can end up inferring semantic meaning from rendered C# text. That is exactly the wrong direction for an evladiyelik generator: CppAst analysis should produce a semantic binding model first; emitters should only render that model.

## Goals

- Replace string-first type flow with a semantic type model that is the source of truth for translators, emitters, and validators.
- Keep `build/manifest.json` as declarative family configuration and explicit exceptions, not as a policy engine.
- Make ABI and API policy explicit, testable, and code-owned.
- Build a serious layered test architecture, including embedded `.h` fixtures for real CppAst shape drift.
- Keep the rewrite holistic enough to support SDL2.Core replacement, satellites, and SDL3 without another foundational redesign.
- Tie the rewrite directly to an immediate SDL2.Core replacement readiness lane.

## Non-Goals

- Do not flip `src\SDL2.Core` to generated output inside the semantic rewrite itself.
- Do not generate SDL2 satellite packages in this rewrite.
- Do not introduce SDL3 family generation in this rewrite.
- Do not reopen package topology decisions.
- Do not design the full high-level ergonomic wrapper layer beyond already-set public API policies.

## Peer Evidence

The design aligns with the recurring pattern from peer binding generators:

| Peer | Relevant pattern | Lesson |
| --- | --- | --- |
| ClangSharp PInvokeGenerator | Descriptor structs carry native type names and managed output names separately; diagnostics attach source locations. | Preserve native identity separately from emitted C# text. |
| Silk.NET BuildTools | `Type` model carries `Name`, `OriginalName`, pointer depth, arrays, by-ref/out, function pointer signature, and original native metadata. | Use an intermediate semantic model, not only strings. |
| SkiaSharpGenerator | Opaque/native struct classification is stored in a type registry before emission; platform-dependent `long` shapes can hard-fail. | Classification belongs before output; ABI ambiguity should be explicit. |
| ppy/SDL3-CS | Layered ClangSharp output plus source generation; raw output preserves native attributes while friendly overloads are added later. | String/friendly API belongs in overload layer, not raw type classification. |
| Alimer.Bindings.SDL | CppAst generator detects zero-size structs as typed `readonly partial struct(nint)` handles and centralizes type mapping. | Closest peer for typed SDL handles and CppAst output topology. |

The common thread: native identity, type category, and rendered managed text are separate concepts.

## Architecture Overview

The new pipeline is:

1. **Manifest/config load**
   - Load family identity, parse inputs, required/excluded/deferred declarations, and validator toggles.
2. **Header parse**
   - Run CppAst per platform parse view.
   - Preserve parse-view identity and source spans.
3. **Native declaration catalog**
   - Collect raw C declarations into a family-aware catalog.
   - Separate family-owned, external, excluded, and deferred declarations.
4. **Semantic type analysis**
   - Classify every relevant CppAst type into a `NativeTypeRef`.
   - Preserve native name, C kind, pointer depth, array shape, ownership, ABI width, source type, and diagnostics.
5. **Binding model translation**
   - Produce a `BindingModel` whose functions, structs, enums, constants, handles, and callbacks carry semantic type refs.
6. **Validation**
   - Run validators against the semantic model.
   - Validators do not parse generated C# text.
7. **Emission**
   - Emitters consume only the semantic `BindingModel`.
   - Emitters do not know about CppAst nodes or prefix heuristics.
8. **Compile/smoke gates**
   - Compile generated output.
   - Run Docker generator smoke at milestone gates.

## Configuration vs Policy

### Manifest Configuration

`build\manifest.json` carries family-specific, declarative, reviewable facts:

- family identity (`name`, `managed_namespace`, `primary_class_name`)
- parse catalog and parse inputs
- owned prefixes
- header include/exclude rules
- required declarations from skipped umbrella headers
- intentionally excluded functions
- deferred declarations with explicit reasons
- validator enablement
- dynapi source location

### Code-Owned Policy

Generator code owns stable ABI/API semantics:

- SDL2 `SDL_bool` is int-backed.
- SDL3 bool-like values are byte-backed.
- Opaque handle detection is structural, not prefix-based.
- `void*`, `char*`, and `const char*` classifications are type-policy decisions.
- UTF-8 string overload behavior is policy, not manifest config.
- pointer depth, arrays, fixed buffers, callbacks, and function pointers are semantic type rules.
- `nint`/`nuint` are pointer-sized ABI policy for pointer/userdata concepts, not a universal mapping for C `long` / `unsigned long`.
- public raw externs stay internal.

### Exception Rule

Manifest may override policy only through an explicit named exception:

- declaration name
- category
- source header or source reason
- human-readable rationale
- test coverage

There are no silent JSON knobs that change ABI behavior without a named reason.

## Semantic Type Model

The rewrite introduces a semantic type reference as the canonical type carrier. Exact names may change during implementation, but the responsibilities are fixed.

```csharp
internal enum NativeTypeKind
{
    Primitive,
    Enum,
    FlagsEnum,
    ValueTypedef,
    ConcreteStruct,
    Union,
    OpaqueHandle,
    VoidPointer,
    Utf8Pointer,
    TypedPointer,
    FunctionPointer,
    Callback,
    Array,
    ExternalOpaque,
    SubstitutedManagedType,
    Deferred,
    Unsupported,
}

internal sealed record NativeTypeRef(
    string NativeName,
    string ManagedName,
    NativeTypeKind Kind,
    int PointerDepth,
    string? OwningFamilyId,
    string? SourceHeader,
    NativeAbiShape AbiShape,
    NativeTypeRef? ElementType,
    IReadOnlyList<NativeTypeDiagnostic> Diagnostics);
```

Important constraints:

- Native name and managed name are distinct.
- `ManagedName` is a projection, not the only source of truth.
- Pointer depth is structured data, not inferred from `EndsWith("*")`.
- Opaque handles are discovered by structural inspection of forward-declared / zero-size C structs.
- Concrete structs remain concrete even when passed by pointer.
- External/platform native types require explicit policy.
- Deferred and unsupported states are first-class results, not silent `IntPtr` fallbacks.

## Binding Model Shape

The binding declaration model remains category-based, but every category carries semantic types:

- `BindingFunction`
  - native name / entry point
  - source header
  - return `NativeTypeRef`
  - parameter list with `NativeTypeRef`, direction/count metadata, and escaped managed name
- `BindingStruct`
  - native name
  - layout metadata
  - fields with semantic types
  - nested anonymous union/struct declarations
- `BindingEnumeration`
  - native name
  - managed name
  - underlying semantic integer type
  - flags classification
  - members and aliases
- `BindingConstant`
  - native macro name
  - semantic value type
  - literal/computed/string-like category
- `BindingHandle`
  - native opaque type name
  - managed handle name
  - underlying native storage (`nint`)
- `BindingCallback`
  - native typedef name
  - calling convention
  - return and parameter semantic types

Emitters consume this model only.

## Translator Responsibilities

`CppAstToBindingModel` remains an orchestrator, not a policy blob. It coordinates these collaborators:

- `NativeDeclarationCatalogBuilder`
- `NativeTypeClassifier`
- `OwnershipPolicy`
- `UnsupportedDeclarationPolicy`
- `BindingFunctionTranslator`
- `BindingStructTranslator`
- `BindingEnumTranslator`
- `BindingConstantTranslator`
- `BindingHandleTranslator`
- `BindingCallbackTranslator`

No translator should need to parse managed type strings to recover semantics.

## Emitter Responsibilities

Emitters render semantic model categories:

- `CsConstantEmitter`
- `CsEnumEmitter`
- `CsHandleEmitter`
- `CsStructEmitter`
- `CsCallbackEmitter`
- `CsCommandEmitter`
- later friendly-overload emitters

Emitter rules:

- no CppAst dependency
- no prefix-based ownership decisions
- no ABI classification decisions
- deterministic file order and declaration order
- raw ABI remains internal
- public typed low-level surface remains generated from semantic model

## Validation Responsibilities

Validators consume semantic model data:

- dynapi coherence compares expected exports with emitted function entry points and respects explicit exclusions such as `SDL_DYNAPI_entry`.
- required declaration validators check required functions/constants/enums against semantic categories.
- deferred declaration validators ensure configured deferrals are accounted for.
- type consistency validators catch `Unsupported` and unexpected `Deferred` types before emission.
- category validators catch mismatches such as opaque handles emitted as concrete structs or enum typedefs emitted as primitive aliases.

## Test Architecture

The test pyramid is part of the architecture.

### 1. Pure Unit Tests

Use tiny CppAst nodes and pure collaborators:

- `NativeTypeClassifier`
- ownership policy
- deferred/unsupported policy
- enum/flags classification
- constant classification
- ABI width mapping
- safe identifier escaping

### 2. Embedded `.h` Fixture Translator Tests

These are critical and should be substantial, not token coverage. They catch real CppAst shape drift and header-pattern surprises.

Fixtures should cover:

- opaque struct vs concrete struct
- typedef chains
- SDL2 `SDL_bool`
- `char*` and `const char*`
- callbacks and function-pointer typedefs
- fixed arrays and counted buffers
- anonymous unions
- enum and flags patterns
- platform-only declarations
- external/platform SDK handle types

### 3. Emitter Tests

Hand-build semantic `BindingModel` shapes and assert generated `.g.cs` output. This keeps emitter tests stable and independent from CppAst.

### 4. Scenario Tests

Use `FakeCakeWorld` / `TargetTestHost` for task orchestration, config loading, validation wiring, and generated file persistence.

### 5. Integration Gates

Use real commands for milestone validation:

- `dotnet run --file tools.cs -- generate-bindings`
- compile check
- full build-host tests
- slopwatch
- `git diff --check`

Integration gates do not replace unit/translator/emitter tests.

### 6. Snapshot Usage

Full generated output snapshots are not the main oracle. Use snapshots only for selected high-value structured outputs where review value is clear.

## B-Safe Rewrite Execution

This is a one-shot architecture rewrite with checkpointed execution:

1. Write and approve this design.
2. Create an implementation plan.
3. Work in an isolated branch/worktree.
4. Introduce semantic type model and diagnostics.
5. Rebuild type classification.
6. Rebuild translators around semantic types.
7. Rebuild emitters around semantic binding model.
8. Rebuild validators around semantic model.
9. Expand `.h` fixture tests and emitter tests.
10. Regenerate preview output.
11. Run compile-check and smoke gates.

Partial generated output may be broken inside the branch while checkpoints are in progress, but every checkpoint must have a clear test oracle and must not be presented as complete until verified.

## SDL2.Core Replacement Readiness Lane

The rewrite is explicitly tied to SDL2.Core replacement, but the actual project flip is a separate gate.

### Readiness Requirements

Before `src\SDL2.Core` stops compiling `external\sdl2-cs\src\SDL2.cs`, generated SDL2.Core output must include:

- manifest-driven `SDL2.SDL` identity
- internal raw ABI
- public typed low-level surface
- constants, including `SDL_INIT_*`
- string-like UTF-8 macro constants
- enums and flags, including `SDL_WindowFlags`
- typed handles
- structs/unions with fixed-array and anonymous-union policy
- callbacks/function pointers
- SDL2 `SDL_bool` ABI correctness
- critical function coverage
- validator-clean dynapi/required declaration state, with documented explicit exclusions
- compile-check success
- generator smoke success

### Actual Flip Gate

The `.csproj` flip is not part of the semantic rewrite acceptance. It is the immediate follow-on once readiness is proven:

1. compare required SDL2.Core symbol and shape inventory
2. run generated preview compile-check
3. run smoke
4. update `src\SDL2.Core` to compile generated output
5. run SDL2.Core tests/compile checks

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Rewrite diff becomes too large to reason about | Use checkpointed execution and strict test oracles. |
| New semantic model recreates string-first behavior under a new name | Ban string parsing in validators/emitters; test semantic fields directly. |
| Manifest grows into a hidden policy language | Keep config-vs-policy rule explicit and tested. |
| CppAst shape surprises invalidate assumptions | Use embedded `.h` fixture tests heavily. |
| SDL2.Core flip pressure rushes the rewrite | Keep flip as readiness lane, not same acceptance gate. |

## Acceptance Criteria

- Semantic type model is the source of truth for generated binding categories.
- Manifest responsibilities are limited to facts and explicit exceptions.
- Translators populate semantic types for functions, structs, enums, constants, handles, and callbacks.
- Emitters render from semantic model only.
- Validators consume semantic model only.
- `.h` fixture translator tests cover all critical CppAst shapes listed above.
- Generated preview output compiles.
- Generator smoke succeeds.
- Slopwatch reports no new issues.

## Open Design Notes

- Exact type names (`NativeTypeRef`, `NativeTypeKind`, `NativeAbiShape`) may change during implementation if a clearer name emerges.
- The design should prefer fewer, richer semantic records over many single-purpose string wrappers.
- Diagnostics should carry enough source context to explain unsupported/deferred decisions without requiring a developer to inspect CppAst manually.
