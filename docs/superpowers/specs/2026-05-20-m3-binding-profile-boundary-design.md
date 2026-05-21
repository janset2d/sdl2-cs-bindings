# M3 Binding Profile Boundary Design

**Date:** 2026-05-20
**Status:** Accepted design; executable plan lives in [`../../binding-autogen/milestones/milestone-3-profile-boundary.md`](../../binding-autogen/milestones/milestone-3-profile-boundary.md).
**Scope:** Binding generator Milestone 3 design: CppAst engine, family profiles, and manifest boundary.

## Goal

M3 turns `GenerateBindings` from an SDL2.Core-shaped generator into a family-profile-aware generator foundation without enabling SDL2 satellite output.

The design center is:

```text
Manifest family facts -> Profile resolution -> Engine ABI semantics
```

M3 remains behavior-preserving for SDL2.Core unless a RED characterization test proves a real ABI/API bug. SDL2.Image, SDL2.Mixer, SDL2.Ttf, and SDL2.Gfx must resolve config/profile identity but must not emit source, expand compile-check scope, flip package sources, or run package smoke.

## Peer Lessons

SkiaSharp, ClangSharp, ppy/SDL3-CS, Alimer.Bindings.SDL, and SDL2Sharp all point to the same pragmatic split:

- Human-reviewable family facts and explicit exceptions belong in config.
- ABI semantics belong in code and tests.
- Per-family generation needs named profiles, but those profiles should not become giant branch buckets.
- Per-header and per-platform quirks need executable evidence, not just nicer folders.
- Too many JSON knobs eventually become a sad little compiler.

M3 adopts the discipline, not the exact tooling shape.

## Operating Rules

- No shortcuts, no workaround-shaped architecture, no success-shaped lies.
- No satellite generation in M3.
- No SDL3 implementation in M3; SDL3 profile names may be documented as future/reserved only.
- No JSON ABI policy knobs for scalar widths, bool shape, C `long`, `wchar_t`, pointer classification, callbacks, variadics, macro taxonomy, platform merge, or struct/union layout.
- No `Shared`, `Common`, `Pipeline`, `Runner`, or broad abstraction bucket.
- No `Data` -> `Targets.GenerateBindings` dependency inversion.
- Retire spike remnants instead of preserving backward compatibility for internal generator shapes. `LegacyBindingTypeRefBridge` must be removed during M3, not merely quarantined, because it is transitional spike scaffolding.

## Boundary And Authority

Manifest owns reviewable family facts and explicit declaration exceptions.

Profile code owns defaults, interpretation, and SDL-family policy selection.

Engine code owns ABI mechanics and C-to-C# translation semantics.

If manifest facts conflict with profile expectations, resolution fails closed. There is no silent override model and no last-writer-wins behavior.

The effective generation plan is composed from:

```text
manifest family facts
+ profile-derived defaults and conventions
+ explicit manifest exceptions
```

`profile_id` is a routing key. The manifest says which code-owned profile applies to a family; the profile class decides what that means and validates the manifest facts.

## Manifest Field Decisions

| Field | M3 Decision | Rationale |
| --- | --- | --- |
| `profile_id` | Add to `binding_generation`, required for all families including disabled placeholders. | Explicit routing into code-owned profile policy. |
| `export_macro_names` | Add as family token inventory if implementation/tests prove it pays rent. | Reviewable declaration-visibility token data; interpretation remains code-owned. |
| `raw_abi_class_name` | Do not add in M3. | Derive as `{primary_class_name}Native`; validate derived identifier. |
| `native_import_name` | Do not add in M3. | Derive from existing library/native manifest facts first; validate resolved value. |
| `core_family_id` | Do not add in M3. | Derive from `package_families[].depends_on`; avoid duplicate truth. |
| `core_reference_namespace` | Do not add in M3. | Derive from the resolved core family's binding-generation config. |
| `sdl_major_version` / `bool_wire_type` | Do not add in M3. | Profile selects SDL bool policy; engine owns mapping mechanics. |
| `name_overrides` | Defer. | Peer-validated concept, but not needed for M3 unless a RED test proves it. |
| `profile_version` | Defer. | Useful artifact metadata later, not needed for the initial boundary. |

`export_macro_names` is data, not behavior. Code-owned declaration-visibility strategy decides how those tokens affect parse defines, macro suppression, and export evidence.

## Data, Target, And Validation Placement

`Data` loads stable manifest-backed facts. `Targets/GenerateBindings` composes those facts with profile policy into executable plans. `Validation` checks resolved config/model/output invariants but does not choose profile behavior.

Target placement:

```text
build/_build/Data/BindingGeneration/
  Models/
    BindingGenerationConfig.cs
    BindingGenerationConfigEnvelope.cs
    BindingGenerationConfigError.cs
  BindingGenerationConfigRepository.cs

build/_build/Targets/GenerateBindings/
  Profiles/
    BindingGenerationProfileRegistry.cs
    BindingGenerationProfile.cs
    Sdl2CoreProfile.cs
    Sdl2SatelliteProfile.cs
    Sdl2GfxProfile.cs
  Planning/
    BindingGenerationPlanResolver.cs
    ResolvedBindingGenerationConfig.cs
    ResolvedBindingGenerationPlan.cs
    ResolvedBindingGenerationAuditPlan.cs
    BindingGenerationResolutionError.cs

build/_build/Validation/BindingGeneration/
  existing and new config/model/output validators
```

`ResolvedBindingGenerationPlan` is target-local derived orchestration state. It must not live under `Data` if it references `PlatformCatalog`, profile objects, declaration-visibility strategy, bool policy, or core type reference policy.

## Resolution Levels

Data repository methods should separate raw config availability from generation readiness:

```text
LoadConfig(familyId)                    // disabled is valid
EnumerateConfiguredFamilies()
EnumerateGenerationEnabledFamilies()
```

Target-local plan resolver methods should separate resolution modes:

```text
ResolveConfig(familyId)                 // enabled and disabled families
ResolveGenerationPlan(familyId)         // enabled + generation-ready only
ResolveAuditPlan(familyId)              // optional readiness/audit, no emit
```

Config resolution validates identity/profile coherence and derives facts that are meaningful for disabled placeholders.

Generation resolution requires generation prerequisites such as `header_set`, parse inputs, output identity, and enabled validators/oracles.

Audit resolution is optional and exists to test or report satellite readiness without writing generated output.

## Plan Shape

`ResolvedBindingGenerationConfig` should represent config/profile resolution and allow disabled families.

`ResolvedBindingGenerationPlan` should represent enabled, generation-ready families only.

The generation plan should expose resolved facts and policies downstream:

- family id;
- profile id;
- enabled state;
- managed namespace;
- primary class name;
- derived raw ABI class name;
- derived native import name;
- platform catalog;
- header set config;
- owned prefixes;
- enabled validator ids;
- profile-selected bool wire policy;
- declaration visibility strategy;
- core type reference policy for satellites;
- original binding config only as a bridge while existing collaborators migrate.

Do not put the whole `ManifestConfig` aggregate on the plan. If downstream code needs a manifest fact, add that fact explicitly to the plan.

## Profile Model

Initial profiles:

- `sdl2-core`
- `sdl2-satellite`
- `sdl2-gfx`

Future/reserved profile names:

- `sdl3-core`
- `sdl3-satellite`

Profiles are concrete target-local classes composed from policy collaborators. Avoid a large inheritance hierarchy.

Use an interface when it buys a real seam:

- multiple implementations exist in the slice;
- tests need to mock/substitute an expensive or policy-rich collaborator;
- the collaborator is a target orchestration dependency and mocking it keeps scenario tests focused;
- an independent change axis is visible from M3 scope.

Do not create ceremonial `IFoo` / `Foo` pairs for simple records or pure helpers.

Likely seams:

- `IBindingGenerationPlanResolver` for task scenario tests;
- `IDeclarationVisibilityStrategy` because SDL/`DECLSPEC` and SDL2_gfx scope macros are real variants;
- concrete value/enum policy for SDL bool unless tests prove an interface helps.

## Profile Responsibilities

`sdl2-core`:

- SDL2 bool policy: int-backed `SDL_bool`;
- full SDL2 core platform catalog;
- no core-family reference;
- core-owned prefix expectations;
- normal SDL export-token handling.

`sdl2-satellite`:

- SDL2 bool policy: int-backed `SDL_bool`;
- core-family reference behavior derived from `package_families[].depends_on`;
- normal SDL satellite declaration visibility convention;
- neutral/platform-light catalog behavior unless real headers prove otherwise;
- no per-family `if Mixer` / `if Ttf` branching.

`sdl2-gfx`:

- SDL2 bool policy where applicable;
- core-family reference behavior;
- SDL2_gfx scope-macro declaration visibility strategy;
- mixed-prefix tolerant symbol/ownership handling;
- no broad `GfxEverythingSpecialCasePolicy`.

Engine-owned policies remain engine-owned:

- primitive scalar mapping;
- C `long` / `CLong`;
- `wchar_t`;
- pointer depth classification;
- callbacks and function pointers;
- variadic suppression/mapping;
- struct/union layout safety;
- macro taxonomy.

Profiles select variants where variants exist; the engine performs the actual ABI translation.

## Disabled Satellite Semantics

Disabled satellite entries must carry enough identity to resolve profile/config.

They may omit generation-only fields such as `header_set`, full parse args, validators, and oracle paths.

Expected behavior:

- `ResolveConfig("sdl2-image")` succeeds when the disabled placeholder is coherent.
- `ResolveGenerationPlan("sdl2-image")` fails fast because the family is disabled.
- `ResolveAuditPlan("sdl2-image")` may validate profile/header readiness without emitting.
- `GenerateBindingsTask` enumerates enabled generation plans only.
- Disabled satellites do not parse, emit, clear output directories, compile-check, package, or smoke by default.

If a satellite is flipped to `enabled: true` too early, the error must name the missing generation prerequisite before any expensive parse or filesystem mutation occurs.

## Target Integration

Current flow loads manifest data more than once and passes family ids into generation orchestration.

M3 should move toward:

```text
GenerateBindingsTask
  -> BindingGenerationPlanResolver.ResolveEnabledGenerationPlans()
  -> BindingFamilyGeneration.GenerateAsync(context, plan, ct)
```

`GenerateBindingsTask` remains lifecycle orchestration: Linux triplet assertion, libclang assertion, container digest logging, enabled plan enumeration, dispatch.

`BindingFamilyGeneration` remains generation orchestration: header resolution, platform views, parse, model building, validators, emit, write.

Existing collaborators may bridge from plan to existing config-based APIs during M3. The bridge must be explicit and temporary.

## Validation Model

Use `Result<T,TError>` when a resolution step either produces one object or fails expectedly.

Use `ValidationReport` for multi-check stage validators and artifact/model checks.

Task boundaries translate expected failures into `CakeException` with family id, profile id, enabled state, resolution mode, missing field, and remediation.

Validation ownership:

| Check | Owner |
| --- | --- |
| JSON shape and family/library mapping | `Data/BindingGeneration` |
| Disabled config loadability | `Data/BindingGeneration` |
| Unknown `profile_id` | profile resolver |
| Profile/family coherence | profile resolver |
| Derived raw ABI class identifier validity | plan resolver or `Validation/BindingGeneration` helper |
| Derived native import name validity | plan resolver, backed by manifest/native metadata where useful |
| Satellite core dependency resolution | plan resolver via `package_families[].depends_on` |
| Missing `header_set` for enabled family | generation resolver |
| Missing `header_set` for disabled placeholder | allowed during config resolution |
| Unknown validator id | pre-parse generation validation |
| Dynapi/required-functions/semantic model checks | existing `Validation/BindingGeneration` validators |

Validators must not become profile policy engines.

## Tests

M3 test strategy has three layers.

Fast tests:

- Data/manifest integration tests with JSON fixtures;
- profile registry tests;
- plan resolver tests;
- validation tests for fail-closed diagnostics;
- GenerateBindings scenario tests with fakes;
- SDL2.Core generated-output parity checks.

Fixture-header tests:

- Enrich embedded `.h` fixture coverage wherever a slice touches parse/model behavior.
- Add fixtures for SDL2_gfx scope macros, SDL2 satellite export tokens, callback-heavy declarations, C `long`, `SDL_bool`, by-value core structs such as `SDL_Color`, and pointer-to-pointer cases such as `char**` when those concerns enter the slice.
- Fixture tests are optional per slice only when the slice does not touch the relevant behavior. If the slice touches it, add the test. No vibes-based confidence.

Linux/container tests:

- Use `docker/binding-generator.Dockerfile` image when Linux/libclang/vcpkg-installed headers are needed.
- Prefer command override against the binding-generator image for focused audit/test commands.
- Do not run full binding generation just to prove a small Linux-specific readiness check.
- Real-header satellite readiness should be audit evidence, not default unit-test tax.

Required manifest integration fixture scenarios:

- SDL2.Core enabled with `profile_id` and any selected export-token field;
- disabled SDL2.Image placeholder resolves config/profile with minimal fields;
- disabled SDL2.Gfx placeholder resolves with `sdl2-gfx` identity;
- unknown `profile_id` fails clearly;
- enabled family without `header_set` fails generation resolution;
- disabled family without `header_set` is allowed for config resolution;
- satellite core relationship derives from `package_families[].depends_on`;
- incoherent manifest/profile facts fail closed;
- unknown validator id fails before parse/generation.

Do not add in M3:

- generated satellite output snapshots;
- satellite compile-check projects;
- satellite package-consumer smoke;
- runtime smoke for satellite APIs;
- public API snapshots for satellites;
- full binary symbol validators for satellites unless only modeled against fake data.

## Delivery Slices

1. **Schema/Data Slice**
   - Add `profile_id` and only add `export_macro_names` if the slice proves it earns its place.
   - Add config load path where disabled is valid.
   - Add manifest integration fixtures.
   - No target behavior change.

2. **Profile Registry Slice**
   - Add target-local profile registry and profile objects.
   - Add profile-resolution tests for core, normal satellites, and gfx.
   - No generation behavior change.

3. **Plan Resolver Slice**
   - Add resolved config/plan contracts.
   - Add config/generation/audit resolution distinction.
   - Derive raw ABI class, native import name, and core reference.
   - Add fail-closed diagnostics.

4. **Task Integration Slice**
   - `GenerateBindingsTask` resolves enabled generation plans.
   - `BindingFamilyGeneration.GenerateAsync` accepts a plan.
   - Existing collaborators use adapters to avoid broad rewrites.
   - SDL2.Core output parity gate.

5. **Policy Injection Slice**
   - Replace raw config assumptions with plan/profile policies where needed.
   - Move SDL bool selection to profile-selected policy.
   - Move declaration visibility/export-token handling behind named strategy.
   - Preserve SDL2.Core output.

6. **Disabled Satellite Audit Slice**
   - Add resolver/audit tests for Image, Mixer, Ttf, and Gfx.
   - Keep no-emit guarantee.
   - Use fixture `.h` tests and focused Docker command overrides where useful.

7. **Spike-Remnant Retirement Slice**
   - Remove `LegacyBindingTypeRefBridge`.
   - Replace manifest-required-function conversion with a purpose-built semantic native type adapter/parser.
   - Keep behavior characterized; no backward-compatibility shim for the retired bridge.

8. **Cleanup Slice**
   - Reduce raw `BindingGenerationConfig` leakage in model-building/emission.
   - Rename output-bag `*Result` types only if it stays mechanical and low-risk.

## Verification

Per focused slice:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~BindingGeneration"
```

GenerateBindings scenario slice:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0 --filter "FullyQualifiedName~Scenarios.GenerateBindings"
```

Milestone managed gate:

```pwsh
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
```

After code/test changes:

```pwsh
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,**/bin/**,**/obj/**"
```

End-of-M3 if feasible:

```pwsh
dotnet run --file tools.cs -- generate-bindings
```

Run compile-check when generated preview exists. Use focused Docker command overrides for Linux-specific header/readiness checks instead of full generation when full generation is unnecessary.

## Exit Criteria

- SDL2.Core generated output is unchanged unless an approved RED test fixes a real bug.
- Enabled SDL2.Core generation flows through the new plan path.
- Disabled Image, Mixer, Ttf, and Gfx resolve config/profile.
- Disabled satellites do not emit, parse, package, compile-check, or smoke by default.
- Unknown profile, missing enabled prerequisites, unknown validator id, and profile/family mismatches fail closed with clear diagnostics.
- `Data` does not depend on `Targets.GenerateBindings`.
- No JSON ABI policy knobs are introduced.
- `LegacyBindingTypeRefBridge` is removed.
- Relevant `.h` fixtures are enriched when behavior touched by M3 needs characterization.
- Linux-specific readiness tests use the binding-generator image with focused command overrides where practical.
