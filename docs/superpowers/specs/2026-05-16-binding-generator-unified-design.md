# Binding Generator — Unified Design Spec

> **Status (2026-05-16):** Accepted unified design. Supersedes [`2026-05-14-binding-generator-architecture-design.md`](2026-05-14-binding-generator-architecture-design.md) and [`2026-05-15-binding-generator-local-output-loop-design.md`](2026-05-15-binding-generator-local-output-loop-design.md). Both predecessor specs were honest snapshots of their moment — 2026-05-14 captured the production-shape architecture decisions before the implementation surface existed; 2026-05-15 added a scratch local-output loop that intentionally produced a function-only `Preview*` shape so Tasks 4–6 could iterate against real CppAst output. This unified spec absorbs both, retires the `Preview*` scaffolding mindset, locks `build/manifest.json` as the per-family generation configuration center, and reframes the work as a manifest-driven per-family binding generator that finishes Stage 1 (SDL2.Core) and ships the infrastructure for Stage 2 (SDL2 satellites) and Stage 3 (SDL3) without rework.
>
> **Implementation plan:** [`../plans/2026-05-17-binding-generator-unified-plan.md`](../plans/2026-05-17-binding-generator-unified-plan.md).
>
> **Workstream tracking:** [`../../binding-autogen/binding-autogen-strategy-brief.md`](../../binding-autogen/binding-autogen-strategy-brief.md) §Plan Shape carries the Stage 1/2/3 sequencing. [`../../decisions/2026-05-14-binding-autogen-toolchain.md`](../../decisions/2026-05-14-binding-autogen-toolchain.md) (ADR-004) locks the CppAst trio. [`../../decisions/2026-05-05-target-centric-build-host.md`](../../decisions/2026-05-05-target-centric-build-host.md) (ADR-002) and [`../../decisions/2026-05-12-build-host-data-layer.md`](../../decisions/2026-05-12-build-host-data-layer.md) (ADR-003) carry the host-architecture invariants this spec respects.

## 1. Goal

Land Stage 1 SDL2.Core in production shape — real `BindingModel` with all six declaration categories (Functions / Structs / Enums / Constants / Handles / Callbacks), per-category emitters, typed handles, friendly overloads, dual P/Invoke emit — driven from a single source of truth (`build/manifest.json`). Build the infrastructure once, in a shape that absorbs Stage 2 (satellites: SDL2_image / SDL2_mixer / SDL2_ttf / SDL2_gfx / SDL2_net) and Stage 3 (SDL3 core + satellites) by manifest-entry addition only, not by repeated refactor.

## 2. Approved Scope

In scope:

- Retire the `Preview*` scratch shape introduced by the local-output-loop precursor slice; replace it with the real `BindingModel` + per-category emitters by Rider-driven rename + extension (history-preserving, no working-tree non-functional window).
- Make `build/manifest.json` the source of truth for per-family generation config (parse defines, owned prefixes, deferred declarations, excluded/required functions, validator opt-ins, namespace / primary-class identity). Schema bumps to v2.2.
- Refactor `GenerateBindingsTask` to a manifest-driven per-family loop (`--family X` narrows to one). Stage 1 enables only `sdl2-core`; satellites are present in the manifest with `enabled: false` so the schema is exercised but no satellite output yet.
- Refactor validators to family-scoped opt-in via `IBindingFamilyValidator` + `ValidatorId` + manifest flags. Stage 1 wires `BindingPublicApiCoherenceValidator` (now `DynapiCoherenceValidator`) for `sdl2-core` only — satellites + SDL3 do not have a dynapi manifest, and SDL3 retired the dynapi system entirely.
- Preserve the existing local Docker generation loop (`tools.cs generate-bindings` + `docker/binding-generator.Dockerfile`) unchanged.
- Preserve the multi-pass parsing infrastructure (8 platform views, parser options, synthetic headers, header exclusions) that landed in Task 3.5 / `0db0e31`.
- Replace `Sdl2CoreGenerationConfig.Default` (and the placeholder fields P2-α dropped) with manifest-loaded `BindingGenerationConfig` consumed by `CoreOwnedTypeMap` + `KnownUnsupportedDeclarationPolicy` + `CppAstParseRunner` + per-category emitters.
- Wire the cross-family type-reference contract via `CoreOwnedTypeMap`: satellite emitters reference core-owned managed types by qualified namespace (`SDL2.SDL_Surface*` for the current SDL2 Core manifest identity); cross-family compile check is enforced by csc through existing `<ProjectReference>` edges.

Out of scope (deferred to follow-up slices, explicitly named so they don't ambush the next agent):

- Stage 1 Task 7 — wiring `src/SDL2.Core/SDL2.Core.csproj` away from `external/sdl2-cs/src/SDL2.cs` (emission-location flag-flip from `artifacts/generated-bindings-preview/sdl2-core/` to `src/SDL2.Core/Generated/`). The unified slice keeps the output under `artifacts/` so reviewers can compare visually against the existing spike output during the conversion; flag-flip is a separate slice once the real shape ships clean.
- Stage 1 Task 8 — `BindingGenerationCoherenceValidator` PreFlight stamp-drift validator. `IGeneratedStampRepository` + `HeaderSetFingerprintCalculator` are Task 8 forward-looking infrastructure per [`../../binding-autogen/binding-autogen-strategy-brief.md`](../../binding-autogen/binding-autogen-strategy-brief.md) §"vcpkg-state coherence guardrail". The unified slice leaves them untouched.
- Stage 1 Task 9 — public-API snapshot testing via Verify/PublicApiGenerator. Lands after Task 7 flip.
- Stage 2 — satellite generators (`sdl2-image` / `sdl2-mixer` / `sdl2-ttf` / `sdl2-gfx` / `sdl2-net`). The unified spec leaves manifest entries with `enabled: false` so the schema and code paths are exercised; activating satellites is a per-family slice each.
- Stage 3 — SDL3 binding generation. Gated on PD-7 per [`../../release-strategy.md`](../../release-strategy.md) §Sequencing.
- Visibility rollback per legacy P2.6 (cascade-locked by the public Cake Frosting Task convention across 10 sibling tasks). Requires its own Task-visibility-convention slice; not part of this unified slice.
- Stub-library forward declarations for SDL_syswm typed-union layout (HWND/HDC/Display\*/Window/etc.). Stage 2 deliverable.

## 3. Architecture Summary

The generator is a **manifest-driven per-family binding generator hosted inside the Cake build host** under `build/_build/Targets/GenerateBindings/`, with the persisted contract layer at `build/_build/Data/BindingGeneration/` and family-scoped validators at `build/_build/Validation/BindingGeneration/`. One `[TaskName("GenerateBindings")]` Cake task processes one or more families in a single container run; each family is parsed, translated, validated, and emitted in isolation; cross-family type dependencies are resolved at csc time through ordinary `<ProjectReference>` edges plus a runtime `CoreOwnedTypeMap` that enforces "satellites reference, never redeclare" against the model.

**Pure-vs-Cake split (unchanged from predecessor specs).** Parser / model / emitter policy code carries no `ICakeContext`, no Cake aliases, no Cake `Tool<TSettings>` dependencies. Header resolution, generated-file persistence, generation-config repository, and task orchestration are build-host I/O boundaries and use Cake-native abstractions. Tests use `FakeCakeWorld` / `TargetTestHost` per [`../../knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md).

**Peer alignment.** Architecture decisions converge with `amerkoleci/Alimer.Bindings.SDL` (CppAst-based, single SDL3 family) on the unified `BindingModel` shape, typed `readonly partial struct(nint)` handles, and 5–6-file per-category output topology. They converge with `ppy/SDL3-CS` on multi-pass parsing for platform-conditioned headers and on the dynapi-coherence post-emit check. They converge with SkiaSharp on keeping raw P/Invoke internal rather than public. They converge with Silk.NET on span/count metadata and generated low-level overload ideas, but not on Silk.NET's vtable/source-generator machinery. The public API surface decision is canonical in [`../../binding-autogen/binding-api-surface-strategy.md`](../../binding-autogen/binding-api-surface-strategy.md).

## 4. manifest.json v2.2 — `binding_generation` block per family

The single source of truth for per-family generation config. Schema bump from v2.1 → v2.2 introduces one optional block on `library_manifests[]` entries:

```jsonc
{
  "name": "SDL2",
  "vcpkg_name": "sdl2",
  "vcpkg_version": "2.32.10",
  "vcpkg_port_version": 0,
  "native_lib_name": "SDL2.Core.Native",
  "core_lib": true,
  "primary_binaries": [ ... ],

  "binding_generation": {
    "enabled": true,
    "managed_namespace": "SDL2",
    "primary_class_name": "SDL",

    "owned_prefixes": ["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"],

    "parse_defines": [
      "SDL_DECLSPEC=",
      "SDL_DISABLE_IMMINTRIN_H=1",
      "SDL_DISABLE_MMINTRIN_H=1",
      "SDL_DISABLE_XMMINTRIN_H=1",
      "SDL_DISABLE_EMMINTRIN_H=1",
      "SDL_DISABLE_PMMINTRIN_H=1",
      "SDL_DISABLE_MM3DNOW_H=1",
      "SDL_DISABLE_LSX_H=1",
      "SDL_DISABLE_LASX_H=1",
      "SDL_DISABLE_ARM_NEON_H=1"
    ],
    "clang_args": ["-fdeclspec", "-U__has_builtin"],

    "platform_catalog": "sdl2-core",

    "header_set": {
      "include_dir_glob": "include/SDL2",
      "header_glob": "*.h",
      "excluded_headers": [
        "SDL.h",
        "begin_code.h",
        "close_code.h",
        "SDL_opengl.h",
        "SDL_opengles.h",
        "SDL_opengles2.h",
        "SDL_egl.h"
      ],
      "excluded_header_prefixes": ["SDL_test", "SDL2_"]
    },

    "excluded_functions": ["SDL_main", "SDL_DYNAPI_entry"],

    "required_functions": [
      {
        "name": "SDL_Init",
        "return_type": "int",
        "parameters": [{ "type": "uint", "name": "flags" }],
        "source_header": "SDL.h"
      },
      { "name": "SDL_InitSubSystem", "return_type": "int", "parameters": [...], "source_header": "SDL.h" },
      { "name": "SDL_QuitSubSystem", "return_type": "void", "parameters": [...], "source_header": "SDL.h" },
      { "name": "SDL_WasInit", "return_type": "uint", "parameters": [...], "source_header": "SDL.h" },
      { "name": "SDL_Quit", "return_type": "void", "parameters": [], "source_header": "SDL.h" }
    ],

    "deferred_declarations": {
      "SDL_SysWMinfo": {
        "category": "deferred-to-stage-2",
        "reason": "SDL_syswm typed-union layout requires the platform-handle forward-declaration stub library (HWND/HDC/Display*/Window/etc.) and [StructLayout(LayoutKind.Explicit, Size = 64)] emission. Stage 1 emits SDL_GetWindowWMInfo with an opaque SDL_SysWMinfo* parameter; the typed union lands in Stage 2."
      },
      "SDL_SysWMmsg": { "category": "deferred-to-stage-2", "reason": "Same as SDL_SysWMinfo." }
    },

    "validators": {
      "dynapi-coherence": true,
      "neutral-view-non-empty": true,
      "required-functions-emitted": true
    },

    "dynapi": {
      "exports_glob": "buildtrees/sdl2/src/*/src/dynapi/SDL2.exports"
    }
  }
}
```

The `binding_generation` block is **optional** at the schema level — its absence implies `enabled: false`. Stage 1 entries are written for all five SDL2 families; `sdl2-core` carries the full config; satellites carry placeholder entries with `enabled: false` and a `// stage-2-placeholder` comment, so the schema is exercised across all families even while only core is wired.

**Rationale-comment housekeeping.** The `parse_defines` and `clang_args` entries each carry a paragraph of rationale in the current `_baseDefines` source comments (SDL2 line citations, peer convergence notes, defense-in-depth chain). JSON cannot host that depth. The rationale moves to [`../../playbook/binding-generator-maintenance.md`](../../playbook/binding-generator-maintenance.md) §"Parse-time configuration surface (per family)" as the durable reference — same content, better home. The plan defers the JSONC alternative (file rename `manifest.json` → `manifest.jsonc` for inline `// comments`) unless the playbook split proves insufficient in practice.

**Repository layer.** `build/_build/Data/BindingGeneration/BindingGenerationConfig.cs` carries the typed record set (`BindingGenerationConfig` + `HeaderSetConfig` + `RequiredFunctionConfig` + `DeferredDeclarationConfig` + `BindingValidatorsConfig` + `DynapiConfig`). `BindingGenerationConfigRepository.cs` reads the per-family block from `manifest.json` via `CakeJsonExtensions.ToJsonAsync` + central `DeserializeJson`. Single sync method `Load(string familyId)` returning `Result<BindingGenerationConfig, BindingGenerationConfigError>` — fail-closed shapes: `FamilyNotFound`, `Disabled`, `InvalidConfig`.

**`Sdl2CoreGenerationConfig` retires entirely.** The `.Default` static factory and its placeholder fields (the P2-α drop) are replaced by manifest-driven loading. Tests pin behaviour ("loads correctly for `sdl2-core`", "rejects `enabled: false`", "validates required-functions schema"), not the record shape (P3.12 resolution).

## 5. Per-family fan-out — `GenerateBindingsTask` shape

```csharp
[TaskName("GenerateBindings")]
[TaskDescription("Regenerates SDL2 family bindings driven by manifest.library_manifests[].binding_generation. Default: every family with binding_generation.enabled=true. --family <id>: only the named family.")]
public sealed class GenerateBindingsTask(
    BindingGenerationConfigRepository configRepo,
    HeaderSetResolver headerResolver,
    ICppAstParseRunner parseRunner,
    ILibclangVersionAsserter libclangVersionAsserter,
    CppAstToBindingModel translator,
    BindingEmitter emitter,
    IEnumerable<IBindingFamilyValidator> validators,
    IBindingOutputWriter outputWriter,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        AssertLinuxTriplet(context);
        _libclangVersionAsserter.Assert();

        var families = ResolveTargetFamilies(context); // honors --family arg + enabled flag
        foreach (var familyId in families)
        {
            var configResult = _configRepo.Load(familyId);
            if (configResult.TryGetError(out var error)) { throw new CakeException(error.Reason); }
            var config = configResult.Value;

            _log.Information("Generating '{0}' bindings (namespace {1}, primary class {2}).",
                familyId, config.ManagedNamespace, config.PrimaryClassName);

            var headerSet = _headerResolver.Resolve(config, context.Runtime.Triplet, context.Paths);
            var catalog = PlatformCatalog.For(config.PlatformCatalogId);

            var parseResults = catalog.ParseViews
                .AsParallel().AsOrdered().WithCancellation(context.CancellationToken)
                .Select(view => _parseRunner.Parse(config, headerSet, view))
                .ToList();

            var model = _translator.Translate(parseResults, config);
            EnsureNeutralViewNonEmpty(model);

            await RunFamilyValidatorsAsync(model, config, context.CancellationToken).ConfigureAwait(false);

            var fileSet = _emitter.Emit(model, config);
            var outputRoot = context.Paths.GetGenerateBindingsOutputRoot(familyId);
            await _outputWriter.WriteAsync(context, fileSet, outputRoot, context.CancellationToken).ConfigureAwait(false);

            _log.Information("Wrote {0} files to '{1}'.", fileSet.Files.Count, outputRoot.FullPath);
        }
    }

    private async Task RunFamilyValidatorsAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        var enabledIds = config.Validators
            .Where(kv => kv.Value)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.Ordinal);

        var toRun = _validators.Where(v => enabledIds.Contains(v.ValidatorId)).ToList();

        foreach (var validator in toRun)
        {
            var report = await validator.ValidateAsync(model, config, ct).ConfigureAwait(false);
            LogReport(report, validator.ValidatorId);
            if (!report.IsValid) { throw new CakeException(BuildErrorMessage(validator.ValidatorId, report)); }
        }
    }
}
```

Notes:

- The task signature drops `IDynapiManifestRepository` + `IBindingPublicApiCoherenceValidator` direct dependencies; both are now hidden behind the family-scoped validator dispatch.
- `ResolveTargetFamilies` reads `--family X` from `BuildContext` (CLI arg added per existing `Host/Cli/Options/` pattern); when unset, returns every family with `binding_generation.enabled: true`. Validates that an explicit `--family` value names a known family with `enabled: true`; fail-closed otherwise.
- The `foreach` loop processes families sequentially within one task invocation (peer parity with PackageTask / ConsolidateHarvestTask; matches AGENTS.md §Build Host Pipeline). Sequential dispatch keeps libclang state isolated per family; cross-family parallelism is out of scope for Stage 1.

## 6. `BindingModel` + six declaration categories

The unified in-memory model. Rename-extends the current `PreviewBindingModel` rather than constructing a new shape:

```csharp
namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingModel(
    IReadOnlyList<BindingParseView> Views,
    IReadOnlyList<BindingStruct> Structs,
    IReadOnlyList<BindingEnum> Enums,
    IReadOnlyList<BindingConstant> Constants,
    IReadOnlyList<BindingHandle> Handles,
    IReadOnlyList<BindingCallback> Callbacks);

public sealed record BindingParseView(
    string Name,
    string? SupportedOsPlatform,
    IReadOnlyList<BindingFunction> Functions);

public sealed record BindingFunction(
    string Name,
    BindingTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters,
    string SourceHeader,
    bool IsVariadic);

public sealed record BindingParameter(BindingTypeRef Type, string Name);

public sealed record BindingTypeRef(
    string ManagedName,           // emitted text, e.g. "SDL_Surface*" or "ReadOnlySpan<byte>"
    string? OwningFamilyId,        // null for primitive/built-in; "sdl2-core" for core-owned; per-family for owned
    bool IsPointer,
    bool IsOpaqueHandle);

public sealed record BindingStruct(
    string Name,
    IReadOnlyList<BindingStructField> Fields,
    StructLayoutKind Layout,
    int? ExplicitSize);

public sealed record BindingStructField(string Name, BindingTypeRef Type, int? FieldOffset);

public sealed record BindingEnum(
    string Name,
    BindingTypeRef UnderlyingType,
    IReadOnlyList<BindingEnumMember> Members,
    bool IsFlags);

public sealed record BindingEnumMember(string Name, string Value /* literal */);

public sealed record BindingConstant(
    string Name,
    BindingTypeRef Type,
    string Value,
    ConstantKind Kind);   // Literal => const, Computed => static readonly

public sealed record BindingHandle(string Name);   // emits readonly partial struct Name(nint value)

public sealed record BindingCallback(
    string Name,
    BindingTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters);
```

The functions list stays per-parse-view (driven by the platform catalog), since `[SupportedOSPlatform]` attribution requires the view boundary. Structs / Enums / Constants / Handles / Callbacks are flat (no per-view split) — they don't have platform-conditioned variants at the SDL2 surface in Stage 1; if a future header bump introduces one, the model gains a view dimension on that category at that time.

The model is the canonical input to **all** emitters. Per the cross-family contract, satellite-stage translators populate `BindingTypeRef.OwningFamilyId` so emitters can decide whether to emit a core-owned type as a qualified reference (`Janset.SDL2.SDL_Surface*`) or a satellite-owned type as a local declaration.

## 7. Per-category emitter classes

```text
build/_build/Targets/GenerateBindings/Emitting/
├── BindingEmitter.cs              (dispatcher; orchestrates emitter chain + assembles GeneratedFileSet)
├── EmitContext.cs                 (shared state: BindingGenerationConfig, CoreOwnedTypeMap, TypeMappingPolicy, KnownUnsupportedDeclarationPolicy, BindingModel reference)
├── CodeWriter.cs                  (indentation-aware text builder; LF-only via AppendLf)
├── CsCommandEmitter.cs            (functions: dual P/Invoke emit + friendly overloads)
├── CsStructEmitter.cs             (POD struct + union with [StructLayout])
├── CsEnumEmitter.cs               (enum + [Flags] attribution)
├── CsConstantEmitter.cs           (const vs static readonly categorization)
├── CsHandleEmitter.cs             (readonly partial struct Name(nint value) with full Rule-2 feature set)
└── CsCallbackEmitter.cs           (delegate* unmanaged[Cdecl] + [UnmanagedCallersOnly] where representable)
```

Each emitter is a `public sealed class` with a single `Emit(BindingModel model, EmitContext ctx) → IReadOnlyList<GeneratedFile>` (or partial — `BindingEmitter` collects all results). Per AGENTS.md §"Prefer small public sealed classes with explicit collaborators": the per-category emitter pattern earns its name-cost because each emitter has independent unit-test surface, distinct change-axes (struct layout rules vs P/Invoke shape vs friendly-overload generation), and clear collaborator boundaries.

This is the architecture-spec pattern, **not** Alimer's partial-class pattern. Peer evidence is split (Alimer single-class partial, c2ffi separate generators per node type); we pick the more extracted shape because [`../../knowledge-base/extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) prefers it.

`BindingEmitter` dispatches in a deterministic order — Constants → Enums → Handles → Structs → Callbacks → Commands — so that earlier categories can be assumed-already-defined when emitter prose references types from a previous category. This is a determinism convention, not a structural requirement (each emitter consumes the `BindingModel` directly).

### Friendly overloads (architecture spec Rule 4 + 6)

`CsCommandEmitter` emits, for each function with a `byte*` / `Span<byte>`-friendly parameter:

1. **Internal raw ABI extern** — `byte*` for UTF-8 strings, `T*` for buffers, typed SDL handle structs for SDL-owned opaque handles, ABI-correct primitive wire types.
2. **Public low-level wrapper** — calls (1) without exposing `[DllImport]` / `[LibraryImport]` publicly; keeps unsafe pointer overloads where they are the honest zero-allocation shape.
3. **`ReadOnlySpan<byte>` overload** — accepts pre-encoded UTF-8 and appends a null terminator in temporary storage only when needed.
4. **`string` overload** — convenience layer; encodes UTF-16 to UTF-8 and may allocate.

For each function with output-pointer parameters: emits `out T` overload that dispatches to raw pointer signature. For each function with non-string buffer parameters: emits `Span<T>` / `ReadOnlySpan<T>` overload that pins + dispatches.

The single-loop emission keeps raw ABI externs, public low-level wrappers, and friendly overloads generated from the same model. See [`../../binding-autogen/binding-api-surface-strategy.md`](../../binding-autogen/binding-api-surface-strategy.md) for the canonical string/span/handle policy.

### Dual P/Invoke emit (architecture spec Rule 1)

`CsCommandEmitter` emits both `[LibraryImport]` (`net7+`) and `[DllImport]` (legacy TFMs) for every function in the same emitter pass, guarded by `#if NET7_0_OR_GREATER`. Method names disambiguate via partial-method extension: the `[LibraryImport]` shape declares `partial`; the `[DllImport]` shape (in the same partial class) uses `extern` with `EntryPoint` matching the C symbol. The CppAst single-codebase elasticity (vs ClangSharp + post-process pipeline) is what justifies our ADR-004 choice — this section is the concrete payoff.

## 8. Type-mapping + unsupported-declaration policies (extracted)

```csharp
namespace Build.Targets.GenerateBindings.Model;

public sealed class TypeMappingPolicy
{
    private readonly IReadOnlyDictionary<string, string> _explicitMap;  // Sint8 → sbyte, size_t → nuint, ...
    private readonly CoreOwnedTypeMap _ownedTypes;
    private const int MaxTypedefDepth = 16;   // P2.8 — typedef recursion depth guard

    public TypeMappingPolicy(BindingGenerationConfig config, CoreOwnedTypeMap ownedTypes) { ... }
    public BindingTypeRef Map(CppType type, BindingGenerationConfig familyConfig);
    public BindingTypeRef MapPrimitive(CppPrimitiveType prim);
    public BindingTypeRef MapPointer(CppPointerType ptr, BindingGenerationConfig familyConfig);
    public BindingTypeRef MapTypedef(CppTypedef td, BindingGenerationConfig familyConfig, int depth = 0);
    public string SafeIdentifier(string name);  // P2.9 — Roslyn SyntaxFacts.GetKeywordKind-based reserved set
}

public sealed class KnownUnsupportedDeclarationPolicy
{
    private readonly IReadOnlyDictionary<string, DeferredDeclarationConfig> _deferred;
    // Built from manifest.binding_generation.deferred_declarations + C-variadic baseline.

    public KnownUnsupportedDeclarationPolicy(BindingGenerationConfig config) { ... }
    public bool IsUnsupported(CppFunction func, out string reason);
    public bool IsUnsupported(string declarationName, out string reason);   // for types/structs
}

public sealed class CoreOwnedTypeMap
{
    private readonly IReadOnlyList<string> _ownedPrefixes;
    private readonly string _coreFamilyId;
    private readonly string _coreManagedNamespace;

    public CoreOwnedTypeMap(BindingGenerationConfig config) { ... }
    public bool IsOwned(string identifier);
    public string QualifiedManagedReference(string identifier);
    // e.g. "SDL_Surface" → "Janset.SDL2.SDL_Surface" for satellite emit contexts
}
```

`TypeMappingPolicy.MapPrimitive` corrects the legacy `Long → "int"` (P0.2 — Linux LP64 is 64-bit, was wrong) to `Long → "nint"` (platform-sized, round-trips on both LP64 and LLP64). `MapTypedef` chain-resolves through nested typedefs before falling back to `SDL_`-prefix → `IntPtr` (P0.1 — `SDL_AudioFormat`/`SDL_SpinLock`/`SDL_GameControllerButton` now emit their underlying primitive width). `Map`'s catch-all branch raises a typed warning rather than silently emitting `IntPtr` (P0.3).

`KnownUnsupportedDeclarationPolicy` is **manifest-driven only** — it consumes `BindingGenerationConfig.DeferredDeclarations` (e.g. `SDL_SysWMinfo` deferred to Stage 2 typed-union shape) without any hard-coded baseline. C-variadic functions are intentionally **not filtered** here: per the 2026-05-17 peer-evidence review (see §8.2), the idiomatic SDL-family pattern is to emit variadic functions as fmt-only raw P/Invoke (the `...` tail is dropped, the `const char* fmt` parameter survives). SDL2-CS (hand-written, Ethan Lee) literal-comments this approach (`/* Use string.Format for arglists */`); Alimer.Bindings.SDL (CppAst SDL3) follows the same shape. Phase 3F's friendly-overload track adds the SDL2-CS-style `string fmtAndArglist` wrapper that makes the pre-format expectation explicit at the API surface. `__arglist` (ClangSharp / ppy/SDL3-CS default) is deliberately not adopted because it loses variadic capacity across the friendly-overload trio (Span/string wrappers cannot forward `__arglist` to the raw call) and Ethan Lee's bilinçli choice for SDL2-CS converged on the same fmt-only conclusion.

`CoreOwnedTypeMap.QualifiedManagedReference` is the bridge that lets satellite emitters write `Janset.SDL2.SDL_Surface*` instead of redeclaring `SDL_Surface`. Stage 1 only uses `IsOwned` (during translation, to reject any satellite-side declaration of a core-owned type); the qualified-reference path activates at Stage 2.

### 8.1 Two-axis split — identity (prefix) vs category (structural)

The current Stage 1 translator (`CppAstToBindingModel.MapPointer`) collapses both axes into a string-prefix fallback:

```csharp
// pre-Phase-3C — fragile
CppTypedef td when td.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
CppClass  cls when cls.Name.StartsWith("ID",   StringComparison.Ordinal) => "IntPtr",
CppClass  cls when cls.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
```

That's load-bearing while Stage 1 emits functions only (every SDL_-prefixed pointer becomes `IntPtr`, which round-trips correctly through P/Invoke), but it conflates two independent questions: **(a)** "does this family own the identifier?" and **(b)** "is this type an opaque handle, a value typedef, or a struct?". Phase 3F's typed-handle emit + Stage 2's cross-family qualified references both require these axes to be answered separately, by different machinery, against different inputs.

Phase 3C splits them:

| Axis | Question | Mechanism | Input |
|---|---|---|---|
| **Identity** | "Does family X own identifier Y?" | `CoreOwnedTypeMap.IsOwned(name)` — prefix scan against manifest's `owned_prefixes` array (cheap, manifest-declared, family-scoped) | `string identifier` |
| **Category** | "Is this typedef an opaque handle, a value typedef, an enum-typedef, or a struct typedef?" | Phase 3D translator: **structural inspection** of `CppTypedef.ElementType` — empty `CppClass` (forward decl only) → handle; primitive / typedef-chain resolving to primitive → value typedef; `CppEnum` → enum-typedef | `CppType` (full AST node) |

Identity stays prefix-based because manifest already declares it (`["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"]` for sdl2-core, `["IMG_"]` for sdl2-image, etc.) and the names are well-disciplined within each family. Category MUST NOT be prefix-based: `SDL_Window` (opaque handle) and `SDL_AudioFormat` (typedef of `Uint16`) both start with `SDL_` but emit through different paths. Stage 1's MapTypedef order trick (explicit-width typedefs first, then SDL_-prefix→IntPtr fallback) only works because the emitter is function-only — Phase 3F's typed-handle struct emit will pressure this distinction.

### 8.2 Peer evidence

Every viable peer ships an explicit category catalog separate from any naming convention. Prefix-only category detection is not the pattern in the ecosystem:

| Project | Toolchain | Identity (which family?) | Category (handle vs value type?) |
|---|---|---|---|
| **Janset (this spec)** | CppAst | `CoreOwnedTypeMap.IsOwned` — manifest `owned_prefixes` array | Phase 3D translator: structural inspection of `CppTypedef.ElementType` |
| **`amerkoleci/Alimer.Bindings.SDL`** | CppAst (SDL3) | Single-family generator — no identity check needed (every `SDL_*` is owned) | `_handleTypes` `HashSet<string>` built up at translation time via structural inspection of typedef target |
| **`ppy/SDL3-CS`** | ClangSharp (SDL3) | RSP file `--with-namespace` directive | Per-type `--with-class` / `--with-pointer-class` directives explicit in RSP; libclang's `CXTypeKind` for the structural fallback |
| **`mono/SkiaSharp`** | Custom | Per-project type registry in `Generated/api.json` | Per-handle base class declaration (`ISKHandle`); explicit in generator config |
| **`dotnet/Silk.NET`** | Generic | `BindTask.TypeMaps` per-library JSON | Same JSON carries handle declarations |

The pattern: **manifest carries identity, structural inspection carries category, neither leans on string-prefix for typed-handle emit.** Our Phase 3C extraction follows the same shape — `CoreOwnedTypeMap` does identity, Phase 3D translator does category, the legacy MapPointer SDL_-prefix fallback retires once `BindingTypeRef.IsOpaqueHandle` is properly populated by the translator.

## 9. Validator wiring — `IBindingFamilyValidator`

```csharp
namespace Build.Validation.BindingGeneration;

public interface IBindingFamilyValidator
{
    string ValidatorId { get; }
    Task<ValidationReport> ValidateAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct);
}

public sealed class DynapiCoherenceValidator(IDynapiManifestRepository dynapiRepo) : IBindingFamilyValidator
{
    public string ValidatorId => "dynapi-coherence";

    public async Task<ValidationReport> ValidateAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        if (config.Dynapi is null) { return ValidationReport.Empty; /* family-level structural opt-out */ }

        var manifestResult = await dynapiRepo.LoadAsync(config.Dynapi.ExportsGlob, ct);
        if (manifestResult.TryGetError(out var err)) { return ValidationReport.FromError(err.Reason); }

        // ... existing emitted-vs-manifest cross-check logic from BindingPublicApiCoherenceValidator ...
    }
}

public sealed class NeutralViewNonEmptyValidator : IBindingFamilyValidator
{
    public string ValidatorId => "neutral-view-non-empty";
    public Task<ValidationReport> ValidateAsync(BindingModel model, BindingGenerationConfig config, CancellationToken ct)
    {
        var neutral = model.Views.FirstOrDefault(v => string.Equals(v.Name, "Neutral", StringComparison.Ordinal));
        // Returns Error report if neutral view is missing or has 0 functions.
    }
}

public sealed class RequiredFunctionsEmittedValidator : IBindingFamilyValidator
{
    public string ValidatorId => "required-functions-emitted";
    // Cross-checks that every config.RequiredFunctions entry is present in the emitted Neutral view.
}
```

The existing `BindingPublicApiCoherenceValidator` renames to `DynapiCoherenceValidator` and implements `IBindingFamilyValidator`. Its task-body invocation block (P2.1 — orchestrator-vs-business-logic violation) collapses naturally into the `GenerateBindingsTask.RunFamilyValidatorsAsync` dispatch loop; no separate `BindingPublicApiCoherenceChecker` collaborator is needed because the dispatch loop already plays that role.

`DI registration` lists every concrete validator under `Build.Validation.BindingGeneration` against `IEnumerable<IBindingFamilyValidator>`; the task's filter step picks the manifest-enabled subset per family per call. New validators added in follow-up slices only need the manifest opt-in flag flip to activate per family.

## 10. Cross-family type references — `CoreOwnedTypeMap`

Satellite-generated source references core-owned managed types via qualified namespace plus a `<ProjectReference>` from the satellite `.csproj` to `SDL2.Core.csproj`. The `manifest.json package_families[].depends_on = ["sdl2-core"]` graph already encodes the dependency; the satellite-stage emit work adds the corresponding `using` directives at the top of each generated file:

```csharp
// In src/SDL2.Image/Generated/Commands.g.cs (Stage 2):
namespace Janset.SDL2.Image;

using SDL_Surface = Janset.SDL2.SDL_Surface;
using SDL_Texture = Janset.SDL2.SDL_Texture;
using SDL_Renderer = Janset.SDL2.SDL_Renderer;
using SDL_RWops = Janset.SDL2.SDL_RWops;
using SDL_version = Janset.SDL2.SDL_version;

internal static unsafe partial class Sdl2Image_Neutral
{
    [LibraryImport("SDL2_image", EntryPoint = "IMG_Load")]
    internal static partial SDL_Surface IMG_Load(byte* file);

    [LibraryImport("SDL2_image", EntryPoint = "IMG_LoadTexture")]
    internal static partial SDL_Texture IMG_LoadTexture(SDL_Renderer renderer, byte* file);
}
```

Public satellite wrappers and friendly overloads call these internal raw ABI methods; the extern declarations themselves are not public.

**Generator-side enforcement.** When the satellite-stage `CppAstToBindingModel.Translate` encounters a typedef whose name is core-owned per `CoreOwnedTypeMap.IsOwned`, the translator does **not** populate `model.Handles` / `model.Structs` for that type — it records a `BindingTypeRef` whose `OwningFamilyId = "sdl2-core"` and `ManagedName = CoreOwnedTypeMap.QualifiedManagedReference(name)`. Emitters consume the qualified `ManagedName` directly.

**Compile-side enforcement.** If the qualified namespace + project-reference graph is wrong (e.g. satellite csproj missing the core project-reference), csc fails at satellite-build time. The cross-family contract is therefore enforced twice: once in the generator (`CoreOwnedTypeMap` invariant), once at compile time (csc). The double check is intentional — the generator-side check catches translator bugs early; the compile-side check catches consumer-csproj-config drift.

Stage 1 only exercises `CoreOwnedTypeMap.IsOwned` (translator-side invariant; satellites' `OwningFamilyId` defaults to the family being processed). The qualified-reference path activates at Stage 2 satellite generation.

## 11. Output topology

```text
artifacts/generated-bindings-preview/sdl2-core/      ← Stage 1 unified-slice output (gitignored)
├── Commands.g.cs
├── Constants.g.cs
├── Enums.g.cs
├── Handles.g.cs
├── Structs.g.cs
├── Callbacks.g.cs
├── Platform/
│   ├── WindowsDesktop/Commands.g.cs   ← platform-only function overlays
│   ├── WinRT/Commands.g.cs
│   ├── GDK/Commands.g.cs
│   ├── Linux/Commands.g.cs
│   ├── MacOS/Commands.g.cs
│   ├── IOS/Commands.g.cs
│   └── Android/Commands.g.cs
└── parse-views.json   ← audit sidecar; per-view function counts, defines/undefines, OS attribution

src/SDL2.Core/Generated/   ← Stage 1 Task 7 output home (not unified-slice scope; flag-flip later)
```

The `artifacts/generated-bindings-preview/sdl2-core/` location is intentionally preserved for the unified slice so reviewers can `git diff` against the existing spike output during the rename + extend conversion. Task 7 (separate slice) flips emission to `src/SDL2.Core/Generated/`. The temporary `IPathService.GetGenerateBindingsPreviewFamilyRoot(family)` helper becomes `IPathService.GetGenerateBindingsOutputRoot(family)` and reads `binding_generation.output_root` (added to the manifest schema at Task 7 time, not in the unified slice).

The `Platform/<View>/Commands.g.cs` layout keeps the platform-overlay structure unchanged from the current spike. Constants / Enums / Handles / Structs / Callbacks are emitted from the Neutral view only — these declaration categories have no platform-conditioned variants in SDL2.Core at the public-surface level today.

## 12. Five production-shape anchors preserved

Architecture-spec Rules 1–11 (per [`../../binding-autogen/binding-autogen-strategy-brief.md`](../../binding-autogen/binding-autogen-strategy-brief.md) §"Emit rules — bound to feasibility §2") apply unchanged. The five anchors that interact most directly with the unified slice:

1. **Full TFM matrix with dual emit** (Rule 1) — `[LibraryImport]` net7+ + `[DllImport]` legacy in same emitter loop (§7 above).
2. **Typed `readonly partial struct` opaque-handle baseline** (Rule 2) — `CsHandleEmitter` produces Alimer-pattern handles with `IsNull` / `Null` / `IEquatable<T>` / implicit `nint` / equality operators / `DebuggerDisplay`.
3. **Friendly overloads emitted alongside raw P/Invoke** (Rules 4 + 6) — `CsCommandEmitter` (§7 above).
4. **Platform attribution from multi-pass parsing** (preserved from Task 3.5 / `0db0e31`; see §13).
5. **Satellite/shared-type topology** — `CoreOwnedTypeMap` invariant + qualified namespace refs (§10 above).

SDL2 `SDL_bool` → int-backed raw ABI with friendly bool conversion (Rule 5); SDL3 bool-like values → 1-byte wrapper/byte raw policy (Rule 5 SDL3 variant). These mappings are intentionally separate; a shared bool wire wrapper is incorrect.

## 13. Multi-pass parsing infrastructure (preserved from Task 3.5)

The 8-view `PlatformCatalog` (Neutral / WindowsDesktop / WinRT / GDK / Linux / MacOS / IOS / Android), parser-options + `_baseDefines` set, synthetic-header library (5 mandatory + 3 defensive: `process.h`, `windows.h`, `Inspectable.h`, `AvailabilityMacros.h`, `TargetConditionals.h` + `winapifamily.h`, `directfb.h`, `os2.h`), `HeaderSetResolver.ExcludedHeaders` set, `-fdeclspec`, `-U__has_builtin`, and the `SDL_DISABLE_*MMINTRIN_H` family of defines all survive intact. They land in the current codebase in commit `0db0e31` (Task 3.5 implementation + Post-Implementation Review P0/P1 fixes).

The unified slice **does refactor where these live**:

- `_baseDefines` + `clang_args` move from the hardcoded `CppAstParseRunner` constants into `manifest.binding_generation.parse_defines` + `clang_args`. The runner reads them from the per-family config.
- `MacOS view's MAC_OS_X_VERSION_MIN_REQUIRED=1070` + `iOS view's TARGET_OS_IPHONE=1` move into the catalog data structure (`PlatformCatalog.For("sdl2-core")` builds them at catalog-instantiation time; the catalog is referenced by id from `manifest.binding_generation.platform_catalog`).
- `HeaderSetResolver.ExcludedHeaders` + `ExcludedHeaderPrefixes` move from the hardcoded resolver into `manifest.binding_generation.header_set.excluded_headers` + `excluded_header_prefixes`. The resolver becomes per-family configurable (PSTH-C, PSTH-E).

Synthetic headers themselves remain in-tree under `build/_build/Targets/GenerateBindings/SyntheticHeaders/` (they're file content, not config; the manifest references the directory via `SyntheticHeadersRoot` resolved by `IPathService`).

## 14. Local Docker loop preserved

`docker/binding-generator.Dockerfile`, `tools.cs GenerateBindingsCommand`, `--rebuild-image` / `--no-cache` / `--cpus` / `--memory` flags, the `janset-vcpkg-cache` Docker volume, the `BASE_IMAGE` ARG sourced from `manifest.runtimes[linux-x64].container_image`, and the `CONTAINER_DIGEST` env-var audit trail all survive unchanged.

The cross-OS bind-mount constraint (COPY-into-image, not bind-mount the Windows host repo; isolated cache volumes for vcpkg state) per the existing 2026-05-15 design + the [`feedback_cross_os_container_mount`](../../../C:/Users/deniz/.claude/projects/) auto-memory remains in force. The unified slice does not touch the container infrastructure.

## 15. Stage 1 vs Stage 2 boundary

| Stage | Scope | Output | Activation trigger |
|---|---|---|---|
| **Stage 1** (unified slice + current commits + Task 7 / 8 / 9 follow-ups) | SDL2.Core production-shape binding generator | `src/SDL2.Core/Generated/*.g.cs` committed; PreFlight stamp-drift validator; public-API snapshot tests | Lands when Task 9 closes (post-unified slice) |
| **Stage 2** (per-satellite slices, gated on Stage 1 completion) | SDL2 satellites: `sdl2-image`, `sdl2-mixer`, `sdl2-ttf`, `sdl2-gfx`, `sdl2-net` | `src/SDL2.<Family>/Generated/*.g.cs` per satellite; `external/sdl2-cs` retires | Each satellite flips `binding_generation.enabled: false → true` in manifest; per-family commit |
| **Stage 3** (SDL3, gated on PD-7) | SDL3 core + satellites | `src/SDL3.Core/Generated/`, `src/SDL3.Image/Generated/`, etc. | Per-release-strategy.md PD-7 dependency |

The unified slice **belongs to Stage 1**. It lands the production shape but does not flip the SDL2.Core consumer csproj away from `external/sdl2-cs` (that's Task 7) and does not wire the PreFlight stamp-drift validator (that's Task 8). Stage 2 is satellite generation; activating a satellite is a per-family slice that flips one `enabled` flag and writes the per-family config block.

The `SDL_syswm` typed-union deferral (P2-α-seeded into `KnownUnsupportedDeclarationPolicy`) **remains deferred to Stage 2** — Stage 2 introduces the platform-handle stub library; Stage 2 emits typed `SDL_SysWMinfo` / `SDL_SysWMmsg` with `[StructLayout(LayoutKind.Explicit, Size = 64)]`. Stage 1 emits `SDL_GetWindowWMInfo` with an opaque `SDL_SysWMinfo*` parameter.

## 16. P0/P1/P2/P3/PSTH absorption map

Every item from the predecessor specs' post-implementation review is accounted for:

| Bucket | Status | Disposition |
|---|---|---|
| **P0.1–P0.4** (wire-format bugs) | ✅ Landed in `0db0e31` | Re-verified during Phase 3 model refactor; `TypeMappingPolicy` carries the P0.1 + P0.2 fixes as its baseline contract |
| **P1.1–P1.8** (determinism / concurrency / version-match) | ✅ Landed in `0db0e31` | Phase 3 PLINQ + log-accumulator stress test (P1.4/P1.5 hardening) becomes a unit-test seed |
| **PSTH-A** (Preview→Real naming purge) | Folds into **Phase 3 Commit 3a** | Rider-driven mass rename |
| **PSTH-B** (family-based binding generation) | Folds into **Phase 2** | = per-family fan-out (§5) |
| **PSTH-C** (header-set per-family) | Folds into **Phase 2** | HeaderSetResolver takes per-family config; `header_set` block in manifest |
| **PSTH-D** (manifest centralization) | Folds into **Phase 2** | = manifest.json v2.2 (§4) |
| **PSTH-E** (ExcludedHeaders configurable) | Folds into **Phase 2** | natural consequence of C+D |
| **PSTH-F** (code-side doc-reference cleanup) | Folds into **Phase 1 docs + Phase 3 refactors** | P2.14–P2.18 cleanup happens during the refactor passes |
| **PSTH-G** (AGENTS.md post-Stage-2 update) | Out of unified-slice scope | After Stage 2 wires the first satellite |
| **PSTH-H/I/J** | ✅ Shipped in `9a5f59e` | OverlayPortVersionCoherenceValidator + vcpkg-setup multi-path cache + regenerate-bindings.yml |
| **P2.1** (validator orchestration extract) | Folds into **Phase 2** | Absorbed by `IBindingFamilyValidator` dispatch loop (no separate `BindingPublicApiCoherenceChecker` needed) |
| **P2.2** (Sdl2CoreGenerationConfig DI) | Subsumed by **Phase 2** | Sdl2CoreGenerationConfig retires entirely |
| **P2.3** ✅ | Already done (P2-α) | Values preserved in manifest + policy classes |
| **P2.4** ⚠ retracted | Out of unified-slice scope | Task 8 forward-looking; keep as-is |
| **P2.5** ⚠ retracted | Out of unified-slice scope | Task 8 forward-looking; keep as-is |
| **P2.6** 🟡 cascade-locked | Out of unified-slice scope | Needs Task-visibility-convention slice |
| **P2.7** (TypeMappingPolicy extract) | Folds into **Phase 3 Commit 3b** | §8 above |
| **P2.8** (typedef recursion depth guard) | Folds into **Phase 3 Commit 3b** | TypeMappingPolicy hardening |
| **P2.9** (SafeIdentifier Roslyn SyntaxFacts) | Folds into **Phase 3 Commit 3b** | TypeMappingPolicy.SafeIdentifier |
| **P2.10** (Environment.GetEnvironmentVariable Cake-native) | Folds into **Phase 2 or Phase 3** | small Cake-idiom fix |
| **P2.11** (GetSdl2DynapiExportsGlob string vs FilePath) | Folds into **Phase 2** | Accessor candidate for retirement once `dynapi.exports_glob` is in manifest |
| **P2.12** (multi-segment .Combine cosmetic) | Folds into **polish track** | Either Phase 2 sweep or separate P3-polish slice |
| **P2.13** ✅ | Already done (P2-α) | PathService SDL2-Core-only doc note preserved |
| **P2.14** (CppAstParseRunner stale comment) | Folds into **Phase 3** | During CppAstParseRunner refactor for manifest config |
| **P2.15** (GenerateBindingsTask entrypoint comment lie) | Folds into **Phase 3** | During task refactor |
| **P2.16** (test comment references deleted IBindingGenerationRunner) | Folds into **Phase 3** | Test cleanup during rename |
| **P2.17** (SyntheticHeaders DeferredDeclarations filter ghost) | Folds into **Phase 3 Commit 3b** | Updated to reference `KnownUnsupportedDeclarationPolicy` |
| **P2.18** (PreviewBindingModelData wrong SDL_Init source) | Folds into **Phase 3 Commit 3a** | Test data fixup during rename |
| **P2.19** (HeaderSetResolver false SDL.h comment) | Folds into **Phase 3** | During HeaderSetResolver per-family refactor |
| **P3.1–P3.13** (polish) | Mix of fold + parallel track | P3.10 → Phase 3 rename; P3.12 → Phase 2 test rewrite; P3.13 → Phase 3 testing; remainder = polish-track items at maintainer discretion |

The Stage 1 plan retires to `superseded/` as part of Phase 1; its corrections and Task 4 implementation seeds inherit into the unified plan as ground truth.

## 17. Failure policy / determinism / validation / peer-oracle comparison

**Failure policy unchanged from predecessor spec.** Hard failures: CppAst parse errors in a required view, unknown C-type mapping for a public declaration, satellite redeclaration of a core-owned type, platform/backend declaration with incompatible signatures across parse views, layout-affecting platform/backend type that cannot be represented deterministically, nondeterministic output ordering, missing required output files, dynapi-coherence validator emit-vs-export mismatch, neutral-view-non-empty validator zero-function failure, required-functions-emitted validator missing-function failure. Non-target-OS declarations (`Android`, `iOS`, `WinRT`, `GDK`) are emitted with `[SupportedOSPlatform]` metadata; they are not failures even though Janset does not ship native packages for those RIDs.

**Determinism contract.** Generated `.g.cs` content is byte-identical across Windows-host and Linux-host generation. Force LF line endings (already done via `CodeWriter.AppendLf`); force forward-slash path separators in any path string emitted into source. `BindingModel` collections are sorted by stable key (function name + source header for Functions; declaration name for Structs/Enums/Constants/Handles/Callbacks) before emit. Per-view function counts and the `parse-views.json` audit sidecar capture the parse evidence for review.

**Validation strategy:**

- **Phase 2 acceptance gate** — manifest-driven generation produces output byte-identical to the pre-pivot Preview emit (Sdl2-Core only). The Preview→Real refactor has not started yet at Phase 2 boundary, so the Preview emitter is still the active path; the test asserts Phase 2 changes are behavior-preserving.
- **Phase 3 acceptance gate** — real BindingModel + per-category emitters produce output that compiles cleanly under all 5 TFMs (`net10`, `net9`, `net8`, `netstandard2.0`, `net462`), passes the three family-validators (`dynapi-coherence`, `neutral-view-non-empty`, `required-functions-emitted`), and matches the per-category file inventory (Commands.g.cs / Structs.g.cs / Enums.g.cs / Constants.g.cs / Handles.g.cs / Callbacks.g.cs + Platform/<View>/Commands.g.cs × 7 views).
- **Visual peer-oracle comparison** — after Phase 3, manually diff representative output against (a) `external/sdl2-cs/src/SDL2.cs` (current hand-written shape, untrusted-for-production-testing but legitimate API surface oracle); (b) `tools/binding-spike/cppast-platform/bindings/Generated/*.g.cs` (our own pre-Stage-1 spike output); (c) `Alimer.Bindings.SDL` `Generated/Commands.cs` + `Handles.cs` (production CppAst SDL3 reference); (d) `ppy/SDL3-CS Generated/` (production ClangSharp SDL3 reference). Differences fall into four categories: typed-handle delta (we emit, they emit raw `IntPtr`), friendly-overload delta (we emit, peer doesn't), known SDL2-vs-SDL3 API drift, true generator defect.
- **Symbol-existence validation at Pack stage** (Stage 2 deliverable) — `BindingSymbolExistenceValidator` cross-checks every emitted `EntryPoint` against harvested native binary symbol tables using platform-appropriate tooling (`dumpbin`, `nm`/`readelf`, `nm -gU`). Closes the declared-but-not-exported failure mode. Not in unified-slice scope.

## 18. References

### Operating rules + ADRs

- [`AGENTS.md`](../../../AGENTS.md) — operating rules, approval gate, build-host reference pattern, §"Pure code stays pure", §"Cake nativeness is a hard rule at build boundaries", §"Configuration File Relationships" (manifest = single source of truth).
- [`docs/decisions/2026-05-05-target-centric-build-host.md`](../../decisions/2026-05-05-target-centric-build-host.md) (ADR-002) — target-centric build host.
- [`docs/decisions/2026-05-12-build-host-data-layer.md`](../../decisions/2026-05-12-build-host-data-layer.md) (ADR-003) — contract-centric data layer.
- [`docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../decisions/2026-05-14-binding-autogen-toolchain.md) (ADR-004) — CppAst trio + Linux-canonical lock.
- [`docs/decisions/2026-05-05-d3seg-and-package-first.md`](../../decisions/2026-05-05-d3seg-and-package-first.md) (ADR-001) — D-3seg per-family versioning + package-first contract.

### Knowledge base

- [`docs/knowledge-base/extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) — collaborator extraction discipline (basis for §7's per-category-emitter pattern over Alimer's partial-class shortcut).
- [`docs/knowledge-base/testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — canonical TUnit/MTP test infrastructure.
- [`docs/knowledge-base/release-guardrails.md`](../../knowledge-base/release-guardrails.md) — guardrail catalog (G60 OverlayPortVersionCoherenceValidator §2.4; binding-coherence + symbol-existence guardrails receive G-IDs at Phase 6 catalog refresh).

### Strategy + plans

- [`docs/binding-autogen/binding-autogen-strategy-brief.md`](../../binding-autogen/binding-autogen-strategy-brief.md) — accepted strategy brief (revised 2026-05-15; Stage 1/2/3 sequencing + plan shape).
- [`docs/binding-autogen/binding-api-surface-strategy.md`](../../binding-autogen/binding-api-surface-strategy.md) — canonical API surface decision: internal raw ABI, public typed low-level API, friendly overloads, peer matrix, string/span/handle/`SDL_bool` policy.
- [`docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md`](../plans/2026-05-17-binding-generator-unified-plan.md) — implementation plan for this spec.
- [`docs/playbook/binding-generator-maintenance.md`](../../playbook/binding-generator-maintenance.md) — per-family maintenance + parser-options rationale (the durable home for `parse_defines` per-entry rationale that doesn't fit in manifest.json).

### Superseded (retired in Phase 1)

- [`2026-05-14-binding-generator-architecture-design.md`](2026-05-14-binding-generator-architecture-design.md) — architecture decisions absorbed into §§3, 6–12, 17.
- [`2026-05-15-binding-generator-local-output-loop-design.md`](2026-05-15-binding-generator-local-output-loop-design.md) — Docker loop, multi-pass parsing, parse-time configuration surface absorbed into §§13–14.
- [`../plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`](../plans/2026-05-14-sdl2-core-binding-generator-stage-1.md) — Task 4/5 implementation seeds (CoreOwnedTypeMap factory, KnownUnsupportedDeclarationPolicy factory) inherit into the unified plan; P0–P3/PSTH inventory absorbed into §16.
- [`../plans/2026-05-15-binding-generator-local-output-loop.md`](../plans/2026-05-15-binding-generator-local-output-loop.md) — Task 11.5 deliverables (AST inline filter + `-U__has_builtin` + dynapi cross-check) shipped in `0db0e31`; documented as historical record.

### Peer oracles

- [`amerkoleci/Alimer.Bindings.SDL`](https://github.com/amerkoleci/Alimer.Bindings.SDL) — CppAst SDL3 reference. Source-verified 2026-05-16: unified `CsCodeGenerator` with 5 per-category collections, per-category partial-file output, typed `readonly partial struct(nint value)` handles with full Rule-2 feature set. Architectural reference; we adopt the unified-model pattern but diverge to separate per-category emitter classes per extraction discipline.
- [`ppy/SDL3-CS`](https://github.com/ppy/SDL3-CS) — ClangSharp SDL3 reference. Source-verified 2026-05-16: per-header `.g.cs` files in flat `SDL` namespace, multi-pass preprocessor-macro switching via Python tuples, `check_generated_functions` post-emit validation against SDL gendynapi-sourced API JSON (our `DynapiCoherenceValidator` is the direct analogue).
- [`dotnet/Silk.NET`](https://github.com/dotnet/Silk.NET) — multi-library generator reference. Source-verified 2026-05-16: single `generator.json` with per-library `BindTask` array including TypeMaps for cross-library type sharing — validates our manifest-driven Option A pattern. We diverge on per-family NuGet release independence (D-3seg + SkiaSharp model vs Silk.NET's synchronized release).
- [`bottlenoselabs/c2ffi`](https://github.com/bottlenoselabs/c2ffi) — JSON-intermediate generator reference. Source-verified 2026-05-16: parse-once-into-JSON, emit-from-JSON pattern with DI-resolved per-node-type generators. Considered and rejected for our scope (overhead > benefit when emitting one language from one toolchain). Kept as a documented migration option if Stage 3 introduces multi-language emission needs.

### Auto-memory

- `feedback_cross_os_container_mount.md` — COPY-into-image, not bind-mount, for cross-OS Linux container dev loops.
- `feedback_no_workarounds_shortcuts.md` — never hack code to make tests pass; question spike-era workarounds codified into specs (motivation for the Preview→Real conversion).
- `feedback_rider_for_mass_renames.md` — Phase 3 Commit 3a rename is Rider-driven.
- `project_refactoring_doc_lifecycle.md` — superseded specs/plans move to `superseded/` rather than deleted.

### Header inputs the spec references directly

- `vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_syswm.h` — typed-union lock at line 346–348 (Stage 2 scope).
- `vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_cpuinfo.h` — line 118–133 `SDL_DISABLE_*MMINTRIN_H` escape-hatch contract.
- `vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_stdinc.h` — line 127–131 `_SDL_HAS_BUILTIN` macro, line 822 + 853 `_SDL_size_*_overflow_builtin` gates (basis for `-U__has_builtin` flag).
- `vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL.h` — line 145 / 162 / 184 / 200 / 224 → `SDL_Init` / `SDL_InitSubSystem` / `SDL_QuitSubSystem` / `SDL_WasInit` / `SDL_Quit` (the 5 SDL.h-only declarations that drive the `required_functions` config).
