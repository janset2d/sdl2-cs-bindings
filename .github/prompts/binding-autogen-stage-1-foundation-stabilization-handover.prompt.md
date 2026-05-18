---
name: "Binding autogen Stage 1 — foundation stabilization and native type-classification handover"
description: "Onboarding and priming prompt for the next agent entering janset2d/sdl2-cs-bindings after the 2026-05-17/18 binding-generator foundation reset: stable SDL2Native output, extracted translation collaborators, generic struct emission, SDL_GUID substitution, compile-check stabilization, and the follow-up design discussion around fixed arrays, callbacks, IntPtr vs pointer policy, partial/unsafe structs, embedded C header tests, and SDL2 conformance testing."
argument-hint: "Default flow: read AGENTS.md + canonical docs, inspect staged changes and generated preview, do not continue feature work blindly, then design the next NativeTypeClassification/FixedArrayPolicy/CallbackTranslator slice with embedded-header tests. Override only by explicit Deniz instruction."
agent: "agent"
model: "GPT-5.5 / Claude Opus 4.7 / equivalent high-reasoning model; use multi-agent review for native type classification and SDL2 ABI conformance decisions"
---

# Binding Autogen Stage 1 — Foundation Stabilization and Native Type-Classification Handover

You are entering `janset2d/sdl2-cs-bindings` after a substantial Stage 1 binding-generator stabilization session. This handover is intentionally detailed because the last part of the session surfaced important design risks: the generator now compiles much more output than before, but raw ABI shape, public API shape, callback representation, fixed array representation, and SDL-specific conformance testing must not be treated as solved just because the current preview compiles.

## Mandatory first reads

Read these in order before proposing or changing anything:

1. `AGENTS.md` — repository contract, approval gate, skills, build-host architecture rules.
2. `docs/onboarding.md`
3. `docs/plan.md`
4. `docs/decisions/`
   - especially target-centric build-host ADRs and binding autogen toolchain ADR.
5. `docs/knowledge-base/`
   - especially `extraction-guidelines.md` and `testing-guidelines.md`.
6. `docs/binding-autogen/`
   - `README.md`
   - `binding-api-surface-strategy.md`
   - `binding-autogen-strategy-brief.md`
   - `research/` only as needed for peer evidence.
7. `docs/superpowers/`
   - especially `agent-discipline.md` if present.
   - `plans/2026-05-17-binding-generator-unified-plan.md`
8. `.github/prompts/binding-autogen-stage-1-phase-3c-and-3d-prime-handover.prompt.md` for historical context, but treat this file as the newer handover for the current state.

Where docs and live code disagree, code wins for current behavior. Flag the disagreement instead of silently choosing one.

## Non-negotiable workflow rules

- Approval gate is active. Do not commit, refactor production code, modify project/build files, update manifests, or change CI without explicit "go / yap / proceed / başla".
- Documentation-only edits are allowed, but do not bury architectural decisions in memory only. Durable decisions must land in canonical docs.
- Use process skills before implementation: brainstorming for design, writing-plans for multi-step implementation, TDD for behavior changes, systematic-debugging for failures, verification-before-completion before claiming success, dotnet-slopwatch after code/test/project changes.
- Do not rely on generated output counts alone. Inspect output shape.
- Do not add SDL-specific hardcoded translator branches unless the docs explicitly bless a substitution/defer policy, and tests pin the reason.
- Use peer evidence before deciding interop shape. Relevant peers:
  - SDL2-CS: handwritten SDL2 compatibility oracle, not generator architecture precedent.
  - ppy/SDL3-CS: ClangSharp-generated SDL3, good reference for generated raw ABI.
  - Alimer / Vortice / Hexa.NET.SDL / Sdl3Sharp: useful for CppAst/generator and modern .NET interop shape comparisons.
- Keep build-host architecture target-centric and collaborator-oriented. Avoid private-method soup in `CppAstToBindingModel`.

## Current branch and staged surface at handover

Expected branch:

```text
spike/binding-autogen-sdl2-gfx
```

Recent HEAD observed during handover:

```text
d7fa602 feat(binding-autogen): stabilize SDL2 raw binding generation
5bd563e feat(binding-autogen): Phase 3B — BindingModel extension + RequiredConstants (Option A)
797f1dd refactor(binding-autogen): retire Preview* naming; land production type names (PSTH-A, Phase 3A)
795c08b feat(binding-generator): add SDL.h-only constants handling in Phase 3B
7b9fafb feat(binding-autogen): manifest-driven per-family generator + Docker dynapi fix
66c5925 feat(binding-autogen): unified spec + plan; land P2-α design cleanup
```

Staged changes at the time this handover was prepared covered the binding-generator foundation reset:

```text
build/_build/Targets/GenerateBindings/Emitting/CsCommandEmitter.cs
build/_build/Targets/GenerateBindings/Emitting/CsStructEmitter.cs
build/_build/Targets/GenerateBindings/Model/BindingStruct.cs
build/_build/Targets/GenerateBindings/Translation/BindableDeclarationPolicy.cs
build/_build/Targets/GenerateBindings/Translation/BindingFunctionDeduplicator.cs
build/_build/Targets/GenerateBindings/Translation/BindingFunctionTranslator.cs
build/_build/Targets/GenerateBindings/Translation/BindingStructTranslator.cs
build/_build/Targets/GenerateBindings/Translation/CppAstToBindingModel.cs
build/_build/Targets/GenerateBindings/Translation/ExternalNativeTypePolicy.cs
build/_build/Targets/GenerateBindings/Translation/NeutralFunctionSetBuilder.cs
build/_build/Targets/GenerateBindings/Translation/SdlNativeTypeSubstitutionPolicy.cs
build/_build/Targets/GenerateBindings/Translation/StructFieldTranslator.cs
build/_build/Targets/GenerateBindings/Translation/TypeMappingPolicy.cs
build/_build.Tests/Unit/Targets/GenerateBindings/**/*
docs/binding-autogen/**/*
docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md
```

This prompt file may be unstaged when you start. Check `git status --short` and do not assume the index.

## What this session accomplished

### 1. Raw command output contract stabilized

Generated command files now target:

```csharp
namespace Janset.SDL2.Core;

internal static unsafe partial class SDL2Native
{
    private const string LibName = "SDL2";
}
```

Important contracts:

- Namespace is `Janset.SDL2.Core`.
- Raw ABI class is stable: `SDL2Native`.
- View-specific raw classes such as `Sdl2_Neutral`, `Sdl2_MacOS`, etc. are superseded.
- Parse views affect file location and `[SupportedOSPlatform]` metadata, not class identity.
- `LibName` is emitted once, pinned to the Neutral view when Neutral exists, with fallback to the first emitted view if no Neutral view exists.
- Identical C# extern signatures are deduplicated across platform views after Neutral to avoid duplicate members in the shared partial `SDL2Native`.

### 2. Translator responsibilities extracted

`CppAstToBindingModel` was reduced toward orchestration. New collaborators include:

- `BindableDeclarationPolicy`
- `BindingFunctionTranslator`
- `NeutralFunctionSetBuilder`
- `BindingFunctionDeduplicator`
- `BindingStructTranslator`
- `StructFieldTranslator`
- `SdlNativeTypeSubstitutionPolicy`

This is aligned with `docs/knowledge-base/extraction-guidelines.md`: business rules and branching-heavy behavior belong in named collaborators, not a massive private-method cluster.

### 3. Generic struct discovery replaced name-specific scaffolding

Removed the Stage 1 direction where only known struct names were emitted. Structs/unions are now selected by generic AST ownership/shape:

- defined SDL-owned structs/unions from SDL2 headers
- not explicitly unsupported or substituted
- no `Stage1StructNames` allowlist
- no special flattening branch for `SDL_GameControllerButtonBind`

Anonymous CppAst field classes generate deterministic sibling structs, e.g.:

```text
SDL_GameControllerButtonBind_value
```

### 4. `SDL_GUID` is an explicit substitution

`SDL_GUID` maps to `System.Guid` through `SdlNativeTypeSubstitutionPolicy`.

This is not a random hardcoded translator branch. It is a named substitution policy, documented and tested. It follows SDL2-CS and Alimer-style idiomatic 16-byte GUID mapping. The generated struct collector excludes `SDL_GUID` from `BindingStruct` output.

### 5. Struct emitter now produces compileable raw ABI structs

`CsStructEmitter` emits:

- `[StructLayout(LayoutKind.Sequential)]`
- `[StructLayout(LayoutKind.Explicit, Size = ...)]` for unions
- `unsafe` on structs that contain pointer fields
- primitive fixed buffers, e.g. `fixed byte data[16]`
- `[InlineArray]` wrapper structs for fixed-size arrays whose element type cannot be emitted as a C# fixed buffer element

Example current generated shape:

```csharp
[InlineArray(5)]
public partial struct SDL_MessageBoxColorScheme_colors
{
    private SDL_MessageBoxColor _element0;
}

[StructLayout(LayoutKind.Sequential)]
public partial struct SDL_MessageBoxColorScheme
{
    public SDL_MessageBoxColorScheme_colors colors;
}
```

This is conceptually aligned with ppy/SDL3-CS and Sdl3Sharp for modern .NET, but see the risk section below: public API ergonomics and cross-TFM compatibility are not settled.

### 6. Compile-check went green, but one guard remains pending

The generated preview compiled under the current compile-check project:

```pwsh
dotnet build tests\binding-compile-check\SDL2.Core.CompileCheck.csproj -c Release
```

Known issue: the compile-check project can false-green if no generated `.g.cs` files exist. Pending todo:

```text
binding-compilecheck-guard
```

Recommended minimal guard:

```xml
<Target Name="_GuardNonEmptyGeneratedInputs" BeforeTargets="CoreCompile">
  <Error
    Text="Compile-check requires generated .g.cs inputs under artifacts/generated-bindings-preview/sdl2-core; none were found."
    Condition="@(Compile->Count()) == 0" />
</Target>
```

Before promoting compile-check to a blocking gate, also revisit cross-TFM coverage. The repo targets `net10.0;net9.0;net8.0;netstandard2.0;net462`, while current diagnostic compile-check is focused on the latest TFM.

## Last verification state from this session

The last full verification before this handover was:

```pwsh
dotnet test --project build\_build.Tests\Build.Tests.csproj -c Release --framework net10.0
# 800 total / 0 failed / 788 succeeded / 12 skipped

dotnet run --file tools.cs -- generate-bindings
# Succeeded, wrote 10 files

dotnet build tests\binding-compile-check\SDL2.Core.CompileCheck.csproj -c Release
# Succeeded

slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/binding-spike/**,**/bin/**,**/obj/**"
# Succeeded

git --no-pager diff --check
# Succeeded, with CRLF normalization warnings only
```

A slopwatch run without excluding `tools/binding-spike/**` failed on pre-existing spike project findings. Do not treat those spike findings as introduced by the current slice unless you verify otherwise.

## Important generated-preview observations

Observed after the foundation reset:

- no generated `class Sdl2_`
- `SDL2Native` appears in generated command files
- no generated `struct SDL_GUID`
- GUID signatures use `Guid` in key places
- `[InlineArray]` wrappers exist
- `SDL_MessageBoxColorScheme_colors` exists
- `SDL_GameControllerButtonBind_value` exists

Known generator warnings still exist from `generate-bindings`:

```text
SDL_DYNAPI_entry
SDL_LogMessageV
SDL_RWFromFP
SDL_vasprintf
SDL_vsnprintf
SDL_vsscanf
```

Do not silently "fix" these without tracing the policy and docs.

## The last 4-5 dialog turns: key design discussion

These turns matter. They changed the recommended next step.

### A. Build-host tests vs SDL2-specific conformance tests

The user challenged whether generated artifact shape guards were becoming too SDL2-library-specific for build-host unit tests.

Current distinction:

- Build-host unit tests should test generic generator behavior:
  - C fixed array translates to a fixed-array binding model.
  - primitive fixed array emits C# `fixed`.
  - non-primitive fixed array follows the selected policy.
  - anonymous unions produce deterministic explicit-layout managed declarations.
  - command emission uses stable namespace/class contracts.
- SDL2-specific ABI/public API conformance tests belong in a separate SDL2 binding conformance layer:
  - `SDL_MessageBoxColorScheme`
  - `SDL_AudioSpec`
  - `SDL_Surface`
  - `SDL_RWops`
  - `SDL_Event`
  - callback-bearing structs and APIs

Do not dump a large list of SDL2-specific sentinel assertions into generic build-host emitter tests.

### B. `SDL_MessageBoxColorScheme` and `[InlineArray]`

The user selected:

```csharp
[StructLayout(LayoutKind.Sequential)]
public partial struct SDL_MessageBoxColorScheme
{
    public SDL_MessageBoxColorScheme_colors colors;
}
```

Native SDL2 header:

```c
typedef struct SDL_MessageBoxColorScheme
{
    SDL_MessageBoxColor colors[SDL_MESSAGEBOX_COLOR_MAX];
} SDL_MessageBoxColorScheme;
```

Peer evidence:

| Project | Shape |
| --- | --- |
| ppy/SDL3-CS | `[InlineArray(5)]` fixed-buffer helper for `SDL_MessageBoxColor[5]` |
| Sdl3Sharp | nested `[InlineArray] ColorsArray` |
| Hexa.NET.SDL | explicit `Colors_0` ... `Colors_4` fields plus `Span` helper |
| SDL2-CS | `[MarshalAs(UnmanagedType.ByValArray)] public SDL_MessageBoxColor[] colors` |

Conclusion:

- Current `[InlineArray]` direction is not random; it is a real generated-interop pattern.
- SDL2-CS is more user-friendly but not raw ABI/blittable generator precedent.
- The current sibling helper type name (`SDL_MessageBoxColorScheme_colors`) is ugly as public API.
- Cross-TFM compatibility is unsettled because this repo includes `netstandard2.0` and `net462`.

Design implication:

Introduce an explicit `FixedArrayPolicy` rather than leaving this as an emitter-local fallback.

Candidate policy:

```text
C field: T name[N]

if T maps to a C# fixed-buffer primitive
  => fixed T name[N]
else if target profile supports InlineArray safely
  => InlineArray wrapper
else
  => explicit fields name_0 ... name_NMinus1 plus optional Span helper
```

Given the repo's TFM matrix, seriously evaluate Hexa-style explicit fields as the safe baseline for public package output, even if `[InlineArray]` remains the latest-TFM preview shape.

### C. Do not hardcode SDL struct names for arrays

The mechanism should be shape-based, not `SDL_MessageBoxColorScheme`-based.

CppAst already exposes `CppArrayType`. The correct design boundary is:

| Step | Owner |
| --- | --- |
| detect `CppArrayType` | translation |
| preserve element type + length in model | `BindingStructField` / new fixed-array model type |
| choose representation | `FixedArrayPolicy` |
| emit syntax | `CsStructEmitter` |

Tests should include embedded `.h` fixtures, not only hand-constructed CppAst objects, so the suite proves how CppAst parses real C declarations.

Recommended embedded-header fixture cases:

- `Uint8 data[16]`
- `SDL_Color colors[5]`
- `void* filters[10]`
- enum constant array length, e.g. `SDL_MESSAGEBOX_COLOR_MAX`
- anonymous union field
- pointer to defined struct vs opaque handle
- function pointer typedef field, e.g. `SDL_AudioCallback callback`

### D. `partial struct` needs a conscious policy

The user asked why many structs are `partial`.

Important answer:

- `partial` has no metadata meaning; it is a source-level extension point.
- Generated interop projects commonly make structs partial to allow generated/manual helper properties, constructors, debug views, span accessors, and friendly augmentations.
- It is acceptable only if documented as intentional.

Decision still needed:

1. Keep all raw ABI structs `partial` because friendly augmentation files will extend them.
2. Emit `partial` only when a type needs generated helper members.
3. Drop `partial` from raw ABI structs until augmentation exists.

Recommendation: keep `partial`, but document the reason and add helpers deliberately, not accidentally.

### E. `unsafe` structs are expected in raw ABI, but should not leak as the only user experience

Generated structs become `unsafe` when they contain pointer fields or C# fixed buffers. This is normal for raw ABI. The public friendly layer should eventually hide most of that from ordinary users.

Do not try to remove `unsafe` by converting everything to `IntPtr`. That loses useful type information and can make correct code harder to write.

### F. `IntPtr` vs pointer is currently a real policy gap

The user noticed that some fields are pointers and others are `IntPtr`.

Current rough behavior:

- primitive pointer -> `byte*`, `sbyte*`, `int*`, etc.
- `void*` -> `IntPtr`
- many `SDL_*` class/typedef pointers -> `IntPtr`
- callbacks/function pointers -> usually `IntPtr`
- external/private C runtime pointers -> `IntPtr`

This is a Stage 1 bridge, not a final type system.

Next design should introduce a `NativeTypeClassifier` or equivalent that classifies:

| Native shape | Raw ABI direction |
| --- | --- |
| primitive buffer/out pointer | typed pointer (`byte*`, `int*`, etc.) |
| `void* userdata` | consistent `IntPtr` or `void*` policy |
| opaque SDL handle pointer (`SDL_Window*`) | typed handle wrapper or `IntPtr`, per handle-emitter decision |
| defined SDL struct pointer (`SDL_Surface*`) | `SDL_Surface*` if public defined struct is emitted |
| function pointer typedef (`SDL_AudioCallback`) | callback type, not `IntPtr` by default |
| external/private C runtime pointer | `IntPtr` or explicit defer |

This classifier should be used by function parameters, return types, struct fields, and fixed-array element mapping. Otherwise each new example will reopen the same "why pointer here and IntPtr there?" discussion.

### G. `SDL_AudioCallback` is missing from generated output

The user noticed:

- `SDL_AudioSpec` exists.
- `SDL_AudioCallback` does not.
- Generated `SDL_AudioSpec.callback` is currently `IntPtr`.
- SDL2-CS defines a delegate:

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void SDL_AudioCallback(IntPtr userdata, IntPtr stream, int len);
```

Native header:

```c
typedef void (SDLCALL * SDL_AudioCallback) (void *userdata, Uint8 * stream, int len);

typedef struct SDL_AudioSpec
{
    ...
    SDL_AudioCallback callback;
    void *userdata;
} SDL_AudioSpec;
```

Docs already mention callbacks and `BindingCallback` exists, but translator/emitter support is incomplete. Treat this as an unfinished feature, not a correct final shape.

Modern raw ABI direction to evaluate:

```csharp
public unsafe delegate* unmanaged[Cdecl]<IntPtr, byte*, int, void> callback;
```

But because the package targets old TFMs too, legacy delegate emission/friendly overloads may be required. Do not decide this without checking TFM support and Microsoft interop guidance.

## Recommended next session goal

Do not continue by adding a broad generated-output smoke test as the main next move. The user's concern has shifted toward type-shape correctness.

Recommended next design slice:

```text
Native type classification + fixed-array policy + callback typedef translation,
backed by embedded C header fixtures and a small SDL2 conformance-test plan.
```

Concrete design outputs to produce before implementation:

1. `NativeTypeClassifier` design:
   - primitive pointers
   - `void*`
   - opaque handles
   - defined SDL struct pointers
   - function pointer typedefs
   - external/private native types
2. `FixedArrayPolicy` design:
   - primitive fixed buffers
   - non-primitive arrays
   - pointer arrays
   - enum/constant lengths
   - cross-TFM strategy (`InlineArray` vs explicit fields)
3. `CallbackTranslator` design:
   - collect function-pointer typedefs into `BindingCallback`
   - use callback types in struct fields and function signatures
   - decide modern `delegate*` vs legacy delegate shapes by TFM
4. Embedded `.h` fixture test plan:
   - parse real mini headers with CppAst rather than only hand-building CppAst nodes.
5. SDL2 conformance test plan:
   - separate from build-host generic tests.
   - sentinel `Unsafe.SizeOf<T>()`, `Marshal.OffsetOf`, callback shape, and generated/public API approval where useful.

## Testing strategy going forward

Use three layers. Keep them separate.

### 1. Build-host generator unit tests

Purpose: generic generator policy.

Examples:

- `CppArrayType` -> fixed-array model.
- primitive array -> `fixed`.
- struct array -> selected fixed-array policy.
- function-pointer typedef -> callback model.
- opaque handle vs defined struct pointer classification.
- anonymous union -> explicit layout declaration.

These tests should live under:

```text
build/_build.Tests/Unit/Targets/GenerateBindings/
```

### 2. Embedded C header fixture tests

Purpose: prove actual CppAst parsing shapes, not just handmade CppAst objects.

These can still live in build-host tests, but should parse small local fixture headers.

Keep fixtures minimal and behavior-specific. Avoid copying large SDL headers.

### 3. SDL2 binding conformance tests

Purpose: prove generated SDL2 output matches the real SDL2 ABI/public contract.

Possible location:

```text
tests/binding-conformance/
```

or another name agreed with Deniz.

Candidate sentinel checks:

- `SDL_MessageBoxColorScheme` size and `colors` offset/length behavior.
- `SDL_AudioSpec` size/field offsets and callback field shape.
- `SDL_Surface` pointer fields and internal/public exposure strategy.
- `SDL_RWops` callback fields and hidden union shape.
- `SDL_Event` union size/field offsets.

Do not block all generator work on a complete conformance suite. Start with sentinels that directly cover current uncertainty.

## Open design questions

These are not settled. Ask/brainstorm before implementing.

1. Should non-primitive fixed arrays default to `[InlineArray]`, explicit fields, or TFM-conditioned output?
2. Should fixed-array helper structs be nested inside the parent type instead of emitted as public sibling types?
3. Should raw ABI structs always be `partial`?
4. Should `void*` be `IntPtr` or `void*` in raw ABI structs and functions?
5. Should opaque SDL handles become typed `readonly partial struct` wrappers now or later?
6. Should defined SDL struct pointers become `T*` in raw ABI now?
7. How should callback typedefs be represented across `net10/net9/net8/netstandard2/net462`?
8. Which generated layer is public in the first package preview: raw ABI types only, public low-level wrappers, friendly overloads, or a staged subset?

## Suggested immediate next tasks

If Deniz says "go", use brainstorming/writing-plans first, then TDD:

1. Add compile-check non-empty guard.
2. Design `NativeTypeClassifier`, `FixedArrayPolicy`, and `CallbackTranslator`.
3. Add embedded-header parser fixtures for fixed arrays, callbacks, opaque/defined struct pointers, and anonymous unions.
4. Implement classifier/model changes behind tests.
5. Re-evaluate `InlineArray` vs explicit fields with cross-TFM compile-check.
6. Add the first SDL2 conformance sentinels only after the generic generator policy is explicit.

## Conventional commit candidate for the staged foundation reset

If committing the staged generator/docs/test changes only:

```text
feat(binding-autogen): stabilize SDL2 structural emission
```

If this handover prompt is included in the same commit, prefer:

```text
feat(binding-autogen): stabilize SDL2 structural emission

Document the next native type-classification risks and handoff context.
```

Before any commit, present the final staged diff summary and ask Deniz for approval. Include the required co-author trailer in the actual commit message.
