# Binding Generator Grand Roadmap

> **Status (2026-05-23):** Canonical grand roadmap for the binding generator. This document folds the durable facts from temporary design notes, testing research, peer-binding research, and prior SDL2.Core generator work into one milestone plan. Code and pinned SDL public headers remain the behavior authority; update this roadmap when the milestone sequence or exit gates change.
>
> **Toolchain re-evaluation (2026-05-23):** This roadmap was written against the CppAst + Cake-hosted `build/_build/Targets/GenerateBindings/` implementation (ADR-004, accepted 2026-05-14). That ADR is **Reopened** as of 2026-05-23; the implementation is in sunset pending the spike under [`spikes/binding-generators/`](../../spikes/binding-generators/). Milestones **M0–M2 were completed against the sunset Cake implementation** — their detailed plans are archived under [`../parking-lot/binding-autogen-cake-implementation/`](../parking-lot/binding-autogen-cake-implementation/). **M3 (CppAst engine, family profiles)** was paused before implementation and is now subsumed by the spike's toolchain selection work. **M4–M10 describe toolchain-neutral policy work** (raw ABI projection, public typed API, friendly overloads, production flip, satellites, smoke expansion, SDL3) that survives whichever toolchain the spike selects; path/engine specifics below will be revised when the spike concludes.

## Goal

Land a durable generator foundation for:

- SDL2.Core generated public source;
- SDL2 satellites: Image, Mixer, Ttf, Gfx, and later Net;
- future SDL3 core and satellites;
- multi-TFM raw ABI backends;
- public typed low-level APIs;
- friendly string/span/ref/out overloads;
- package-first compile, smoke, oracle, and snapshot evidence.

The target is not a general-purpose binding-generator product. The target is a pragmatic SDL binding generator with clean seams, explicit family policy, and enough tests to refactor without gambling. The selected toolchain (ClangSharp orchestrator + Roslyn postprocess, or single-pass CppAst emitter) lands when the spike under `spikes/binding-generators/` concludes.

## Operating Rules

- Work milestone-by-milestone. No big-bang generator rewrite.
- Use RED/GREEN for behavior changes and bug fixes.
- Capture current output before refactoring. Snapshot diffs must be intentional and reviewed.
- Prefer embedded `.h` fixture integration tests for parser/model/emitter behavior over string-only unit tests.
- Run fast build-host tests inside each refactor slice; run compile/smoke/package gates at milestone boundaries.
- Move/rename files with `git mv`. Content edits use normal patching.
- Keep test topology aligned with production topology during refactors.
- Preserve unique research findings in canonical docs before deleting temporary notes.
- Manifest carries family facts and explicit exceptions. Generator code owns ABI/API policy.
- Do not create catch-all `Shared`/`Common` folders, generic `Pipeline`/`Runner` shells, or ceremonial interfaces.

## Canonical References And Research Baseline

Canonical docs are authoritative for their own scope. Peer projects and historical research are evidence, not authority.

| Source | Use |
| --- | --- |
| [`binding-generator-constitution.md`](binding-generator-constitution.md) | ABI/API rules, manifest-vs-policy boundary, scalar/struct/macro/platform contracts, evidence gates. |
| [`testing-strategy.md`](testing-strategy.md) | Canonical test layer model, smoke taxonomy, fixture policy, upstream SDL test adoption guidance. |
| [`../playbook/binding-output-oracle-validation.md`](../playbook/binding-output-oracle-validation.md) | Multi-oracle generated-output review workflow. |
| [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) | CppAst/libclang trio, synthetic headers, parse options, dynapi, platform catalog maintenance. |
| [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) | ADR-004 — original CppAst decision (2026-05-14, Reopened 2026-05-23). See spike for active re-evaluation. |
| [`../../spikes/binding-generators/`](../../spikes/binding-generators/) | Active toolchain spike comparing ClangSharp + Roslyn postprocess against Alimer-style single-pass CppAst. Outputs under `output/reports/`. |
| Temporary notes promoted in this change | Historical source for typed handles, raw ABI backend split, public/friendly layer strategy, and smoke taxonomy. Unique facts now live in canonical docs. |
| SkiaSharp generator | CppAst discipline, explicit mappings, verification mindset, dual backend precedent. |
| Alimer.Bindings.SDL | SDL/C# ergonomics evidence: typed handles, LibraryImport, string/span overload ideas. Not a platform-correctness oracle. |
| ppy/SDL3-CS | SDL-specific process reference: explicit headers, per-header response overrides, platform-specific files, SDL3 `sdl.json` name checks. |
| Silk.NET / LibGit2Sharp / Vortice.Windows | Generated/native binding confidence patterns: compile gates, real fixture assets, package/runtime probes, manual samples. |

## Current Baseline

The repo has two implementation surfaces during the toolchain re-evaluation:

**Sunset Cake-hosted CppAst implementation** under `build/_build/Targets/GenerateBindings/` (frozen, M0–M2 completed against it):

- Manifest-driven `binding_generation` configuration in `build/manifest.json` schema `2.2`.
- SDL2.Core enabled; SDL2_image, SDL2_mixer, SDL2_ttf, and SDL2_gfx as disabled Stage 2 placeholders.
- Linux-canonical generation through `tools.cs generate-bindings` and the pinned Linux builder container.
- SDL2.Core platform catalog: Neutral, WindowsDesktop, WinRT, GDK, Linux, MacOS, IOS, Android.
- Semantic model categories: functions, structs, enums, constants, handles, callbacks, and macro report evidence.
- Source-first macro pipeline with helper-candidate reporting, expression evaluation, manual policy handling, duplicate merge, and parse-view evidence.
- Internal raw ABI command emission with `SDLNative` identity, class-level `unsafe`, platform attribution, and modern C integer guards.
- Generated constants, enums, handles, structs, callbacks, and internal commands emitted to `artifacts/generated-bindings-preview/sdl2-core/`.
- Fixture-backed fixes for known SDL2.Core ABI blockers: C `long`, `wchar_t`, `SDL_bool`, opaque handles, callbacks, fixed arrays, and macro leaks.
- Compile-check project at `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj` for generated preview output across `LibraryTargetFrameworks`.

**Active spike implementation** under `spikes/binding-generators/clangsharp/` (ClangSharp + Roslyn postprocess, near-ABI-compatible state):

- ~889 functions emitted across compat (netstandard2.0/net462) and modern (net8+) codegen passes (~98.1% dynapi coherence).
- Internal raw ABI containers (raw visibility A-slice landed).
- SDL.h required surface recovered (B-slice landed): `SDL_Init`, `SDL_Quit`, `SDL_INIT_*` constants.
- Multi-TFM compile clean across all 5 TFMs (net10/net9/net8/netstandard2.0/net462).
- DllImport↔LibraryImport postprocess transform working.
- Multi-OS platform pass for `SDL_main.h` / `SDL_system.h` with `[SupportedOSPlatform]` attribution.
- Known Priority C gap: C `long` width, `wchar_t*` opaque shape, `SDL_RWops` / `SDL_SysWMinfo` / `SDL_SysWMmsg` deferred layouts — semantic-ABI work in progress (see `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md`).

A second comparable evidence pass through Alimer-style single-pass CppAst is the next gate before the spike's toolchain selection lands. The remaining cross-cutting gap (true for either toolchain) is architectural durability: generator layout, policy boundaries, raw-backend split, public wrapper projection, testing strategy, and production flip are not yet on durable foundations.

## Milestone 0: Canonical Plan And Research Consolidation

**Goal:** Promote temporary research into current docs so future work does not depend on chat history or untracked notes.

**References:** temporary binding and testing notes, peer-binding research, this roadmap, [`testing-strategy.md`](testing-strategy.md), [`binding-generator-constitution.md`](binding-generator-constitution.md).

**Scope:**

- Consolidate the grand roadmap into this file.
- Promote testing research into [`testing-strategy.md`](testing-strategy.md).
- Keep policy law in the constitution, not this roadmap.
- Keep operational procedure in playbooks, not this roadmap.
- Delete temporary notes only after their unique facts are represented in canonical docs.

**Exit evidence:**

- Binding auto-generation index points to the canonical docs.
- Root documentation map points to the canonical docs.
- Temporary research files are removed or explicitly superseded.
- `git diff --check` passes.

## Milestone 1: Safety Harness And Baseline

> **Status (2026-05-23):** Completed against the sunset Cake implementation; superseded by the active spike's safety evidence (per-header generation report, oracle comparison, multi-TFM compile-check, dynapi coherence). Original detailed plan archived under [`../parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md`](../parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md). The exit-evidence ideas below remain useful as a checklist for whichever successor implementation lands.

**Goal:** Make the generator behavior safely refactorable before changing architecture.

**Detailed plan (archived):** [`../parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md`](../parking-lot/binding-autogen-cake-implementation/milestone-1-safety-harness-baseline.md).

**References:** [`testing-strategy.md`](testing-strategy.md), [`../playbook/binding-output-oracle-validation.md`](../playbook/binding-output-oracle-validation.md), snapshot testing practice, current generator tests under `build/_build.Tests/Unit/Targets/GenerateBindings/`.

**Scope:**

- Capture a reviewed baseline of current generated output, file set, and `parse-views.json` shape.
- Add compact characterization snapshots for the current generator pipeline.
- Expand embedded `.h` fixture coverage for high-risk ABI/model cases:
  - C `long` / `unsigned long` and `CLong` / `CULong` guards;
  - SDL2 `SDL_bool` int-backed shape;
  - `wchar_t*` opaque handling;
  - opaque handles and duplicate typedef/tag canonicalization;
  - callbacks and function-pointer fields;
  - fixed arrays and anonymous nested struct/union cases;
  - macro taxonomy, helper candidates, manual excludes, overrides, and required constants;
  - platform-only functions and Neutral subtraction.
- Add or tighten scenario tests that prove task orchestration without requiring real libclang/vcpkg for normal unit runs.
- Prepare test folder layout for the future production topology without changing behavior.

**Exit evidence:**

- Build-host tests pass with Verify snapshot infrastructure enabled.
- Reviewed baselines exist for deterministic emitter output, fake task orchestration output, semantic `.h` fixture projections, and opt-in generated-preview file inventory.
- `dotnet run --file tools.cs -- generate-bindings` succeeds and produces 14 SDL2.Core preview files; current dynapi-coherence warnings for `SDL_LogMessageV`, `SDL_RWFromFP`, `SDL_vasprintf`, `SDL_vsnprintf`, and `SDL_vsscanf` are known existing generator gaps, not M1 harness drift.
- Generated-preview inventory snapshot passes when `JANSET_VERIFY_GENERATED_PREVIEW=1` is set.
- Compile-check is run when generated preview exists.

**Non-goals:**

- No raw backend split yet.
- No public wrapper generation yet.
- No production source flip.

## Milestone 2: Behavior-Preserving Topology Refactor

> **Status (2026-05-23):** Completed against the sunset Cake implementation. The Cake-internal `Targets/GenerateBindings/<concept>/` topology this milestone introduced is in sunset; whichever toolchain the spike selects will need its own topology pass. Original detailed plan archived under [`../parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md`](../parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md).

**Goal:** Make the generator understandable from the task entrypoint and align production/test layout around real concepts while keeping output stable.

**Detailed plan (archived):** [`../parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md`](../parking-lot/binding-autogen-cake-implementation/milestone-2-behavior-preserving-topology-refactor.md).

**References:** ADR-002 target-centric build host, ADR-003 data-layer boundary, [`../knowledge-base/extraction-guidelines.md`](../knowledge-base/extraction-guidelines.md), [`../knowledge-base/testing-guidelines.md`](../knowledge-base/testing-guidelines.md).

**Target shape:**

```text
build/_build/Targets/GenerateBindings/
  GenerateBindingsTask.cs
  BindingFamilyGeneration.cs
  HeaderSet/
  SyntheticHeaders/
  Parse/
  PlatformViews/
  Model/
  ModelBuilding/
    Declarations/
    Types/
    Functions/
    Macros/
    SdlPolicy/
  Emit/
    RawAbi/
    PublicApi/
    Reports/
    Tfm/
```

Tests mirror this concept shape under `build/_build.Tests/Unit/Targets/GenerateBindings/` where it adds clarity.

`Profiles/` and `Emit/Friendly/` remain future concepts unless a real M2 type earns those folders; do not create empty architecture placeholders.

**Scope:**

- Extract a named `BindingFamilyGeneration` collaborator from `GenerateBindingsTask`; do not create a generic `Pipeline` or `Runner`.
- Keep the task focused on lifecycle orchestration, Linux-canonical guardrails, config selection, and expected-error translation.
- Split `Translation/` into model-building concepts: declarations, types, functions, and macros.
- Split `Emitting/` into output-contract concepts: raw ABI, public generated artifacts, reports, and TFM policy. Friendly overload topology remains future work.
- Collapse one-line records into cohesive files when they are part of the same concept.
- Convert static one-method policy helpers to instance collaborators only when composition, testing, or profile selection justifies it.
- Move/rename files with `git mv`.

**Exit evidence:**

- Snapshot diff is empty except path/name changes that do not alter generated content.
- Build-host unit/scenario tests pass.
- Test topology reflects production topology.
- `GenerateBindingsTask` tells the high-level story without hiding important behavior in anonymous buckets.

**Non-goals:**

- No SDL2 satellite activation.
- No public wrapper generation.
- No behavior changes without RED tests.

## Milestone 3: ABI Engine, Family Profiles, And Manifest Boundary

> **Status (2026-05-23):** Paused before implementation; subsumed by the spike's toolchain selection work. The original CppAst-specific detailed plan is archived under [`../parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md`](../parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md). The toolchain-neutral *intent* of M3 (separate ABI engine from SDL family policy, keep manifest as facts not policy script) is durable and will apply to whichever successor implementation lands.

**Goal:** Localize SDL2/SDL3/satellite policy, keep the ABI engine reusable inside the target, and keep `build/manifest.json` as family facts plus explicit exceptions, not a policy scripting language.

**Detailed plan (archived):** [`../parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md`](../parking-lot/binding-autogen-cake-implementation/milestone-3-profile-boundary.md).

**References:** constitution section "Manifest Configuration Vs Code-Owned Policy", SkiaSharp mapping discipline, Alimer and ppy hardcoded policy caution.

**Approved scope (Option A+, 2026-05-20):** M3 is a behavior-preserving profile-boundary refactor with satellite reconnaissance. It must not enable SDL2.Image / Mixer / Ttf / Gfx generation, but it must design the profile seams against their real installed headers so Stage 2 activation does not force another topology refactor. This is the critical architecture slice; satellites are expected to be simpler than SDL2.Core, but the profile model must know about their different header and export-macro shapes.

**Profile direction:**

- `sdl2-core`
- `sdl2-satellite` for SDL2_image / SDL2_mixer / SDL2_ttf / SDL2_net-style satellites
- `sdl2-gfx` as a special SDL2 satellite profile because its headers use `SDL2_*_SCOPE` export macros and non-umbrella public headers rather than SDL's `DECLSPEC` pattern
- `sdl3-core` later
- `sdl3-satellite` later

**Engine-owned concepts:**

- CppAst parse-result normalization.
- Native declaration catalog construction.
- Platform parse-view merge and neutral subtraction.
- Semantic binding model construction.
- Raw ABI projection inputs that are not SDL-family policy.
- Deterministic file-set assembly and report generation.

**Authority split:**

- Manifest owns reviewable family facts and explicit declaration exceptions.
- Profile code owns defaults, interpretation, and SDL-family policy selection.
- Engine code owns ABI mechanics and C-to-C# translation semantics.
- `profile_id` is a manifest routing key into code-owned profile policy, not a JSON behavior switch.
- Raw ABI class name, native import name, and satellite core-reference identity are derived first from existing manifest facts; they become new manifest fields only when a RED test proves convention insufficient.

**Manifest-owned facts:**

- enabled state;
- profile id;
- managed namespace;
- public class name;
- platform catalog id;
- header set;
- owned prefixes;
- export macro token inventory if M3 tests prove `export_macro_names` earns its place;
- parse defines and clang args;
- required/deferred declarations;
- manual macro excludes/overrides;
- validator opt-ins;
- dynapi or equivalent external oracle locations.

**Code-owned policy:**

- scalar width mapping;
- SDL2 vs SDL3 bool wire shape;
- C `long` strategy;
- opaque-handle detection;
- pointer, array, callback, and userdata classification;
- UTF-8/string/span overload eligibility;
- C variadic handling;
- struct/union layout policy;
- macro taxonomy and safe expression evaluation;
- platform parse-view merge and attribution rules.

**M2 review findings to carry into the detailed M3 plan:**

- `TypeMappingPolicy` still mixes generic primitive-width mapping, C# identifier escaping, legacy `BindingTypeRef` projection, and SDL2-specific typedef facts such as `SDL_bool -> int`. M3 should split those responsibilities so SDL2/SDL3 bool shape is selected through an explicit SDL profile/policy seam, while generic C primitive mapping and safe identifier escaping stay engine-owned or move to narrower helpers.
- `LegacyBindingTypeRefBridge` is a spike-era adapter used only to convert manifest `required_functions` string types into `NativeTypeRef` values. M3 must replace it with a purpose-built required-function adapter or config parser that produces semantic native type descriptors directly, then remove the bridge. There is no backward-compatibility requirement for this internal spike remnant.
- `ExternalNativeTypePolicy` is not SDL policy even though it exists because SDL headers mention foreign types. It should be named and placed as an external/foreign ABI policy for C runtime, Vulkan, GDK, and Windows COM handles, then injected or selected by profile only where that profile needs it.
- `PlatformCatalog.AllPlatformMacros` is SDL2.Core-specific macro hygiene, not a generic platform-view law. M3 should move the macro denylist next to the SDL2.Core catalog/profile data and keep the manifest `platform_catalog` id as a family fact that resolves to a named catalog or fails with a staged, clear error for placeholders.
- The 2026-05-22 ClangSharp spike proved that function-only platform scanning is not enough for the production contract. `SDL_main.h` and `SDL_system.h` need platform function passes, but `SDL_thread.h`, `SDL_rwops.h`, `SDL_syswm.h`, `SDL_stdinc.h`, `SDL_platform.h`, and `begin_code.h` also need explicit roadmap decisions for platform-sensitive signatures, union/struct layout, macro constants, and calling convention/export behavior. M3/M4 planning must separate "platform-only functions" from "platform-sensitive ABI layout" instead of treating non-function headers as solved.
- Result-shaped record names in the generator should stay semantically honest. Repo-wide `Result<T,TError>` is for expected success/failure; macro translation records such as `BindingConstantTranslationResult`, `MacroConstantMergeResult`, and `MacroManualPolicyResult` are output bags. M3 may rename those to `*Output` / `*Translation` for clarity, but should not force them into `Result<T,TError>` unless they start representing expected failures.
- M3 cleanup must remain behavior-preserving unless a RED test exposes a real ABI/API bug. Generated SDL2.Core output, compile-check behavior, and the M2 safety harness remain the guardrails while policy seams move.

**Satellite reconnaissance findings (installed x64-windows-hybrid headers, 2026-05-20):**

- `SDL_image.h`: 59 `extern DECLSPEC` declarations. Mostly simple wrappers over core-owned `SDL_Surface*`, `SDL_Texture*`, `SDL_Renderer*`, `SDL_RWops*`, `const char*`, plus one owned concrete type `IMG_Animation` and `IMG_InitFlags`. The only early oddity is `char **xpm` in XPM helpers.
- `SDL_mixer.h`: 97 `extern DECLSPEC` declarations. More callback-heavy than Image (`Mix_MixCallback`, `Mix_MusicFinishedCallback`, `Mix_ChannelFinishedCallback`, `Mix_EffectFunc_t`, `Mix_EffectDone_t`, `Mix_EachSoundFontCallback`) and uses `SDL_bool` return values. Owned concepts are `Mix_Chunk`, `Mix_Music`, `Mix_Fading`, and `Mix_MusicType`; many APIs reference core SDL audio/RWops types.
- `SDL_ttf.h`: 85 `extern DECLSPEC` declarations. Opaque `TTF_Font`, many `SDL_Color` by-value parameters, several C `long` parameters/returns (`TTF_OpenFontIndex*`, `TTF_FontFaces`), `SDL_bool`, legacy `const Uint16*` Unicode APIs, and deprecated functions. This is the satellite most likely to exercise C `long`, by-value core structs, and deprecation reporting before public wrappers.
- `SDL2_gfx` headers: public surface is split across `SDL2_gfxPrimitives.h`, `SDL2_imageFilter.h`, `SDL2_rotozoom.h`, and `SDL2_framerate.h`; declarations use `SDL2_GFXPRIMITIVES_SCOPE`, `SDL2_IMAGEFILTER_SCOPE`, `SDL2_ROTOZOOM_SCOPE`, and `SDL2_FRAMERATE_SCOPE` rather than `extern DECLSPEC`. Function names are not uniformly prefixed by one family token (`pixelColor`, `rotozoomSurface`, `SDL_imageFilter*`, `SDL_initFramerate`, etc.), so owned-prefix/profile configuration needs to handle multi-prefix/multi-header families without falling back to SDL2.Core assumptions.

**Exit evidence:**

- SDL2.Core output stays unchanged unless a RED test exposes a bug.
- Generic model-building/emission code no longer hardcodes `/SDL2/`, `SDL_`, or `SDL2` library identity except through a named profile/config value.
- Disabled SDL2 satellite placeholder configs can resolve a profile or fail with clear staged errors without enabling generation.
- Tests prove SDL2.Core profile behavior and satellite-profile boundary conditions for at least Image/Mixer/Ttf plus the Gfx custom-export-macro case.
- Manifest integration fixtures cover enabled core, disabled satellite placeholders, unknown profiles, enabled families missing generation prerequisites, core-dependency derivation, profile mismatches, and unknown validator ids.
- Linux-specific header/readiness checks use the binding-generator image with focused command overrides where practical, rather than running full generation for narrow audits.
- `LegacyBindingTypeRefBridge` is removed.

**Non-goals:**

- No giant JSON policy language.
- No SDL2 satellite output emission or package source flip in M3.
- No SDL3 implementation before SDL3 becomes a real consumer.

## Milestone 4: Raw ABI Projection And Multi-TFM Backends

**Goal:** Introduce a raw ABI projection that can emit honest `DllImport` and `LibraryImport` backend files from one semantic model. The active ClangSharp spike has demonstrated one shape of this through dual-codegen passes (`compatible-codegen` for legacy TFMs, `latest-codegen` for modern) plus Roslyn postprocess; an Alimer-style CppAst single-pass emitter would express this through TFM-conditioned emission rules in the engine. Either approach must satisfy the contract below.

**Priority C closure** (active 2026-05-24): [`spikes/binding-generators/docs/priority-c-closure-summary.md`](../../spikes/binding-generators/docs/priority-c-closure-summary.md) records the active ClangSharp spike's Layer 1 raw ABI honesty closure — C `long` hybrid strategy (drop convenience helpers + dual-dispatch `SDL_threadID`), shared `wchar_t*` opaque via RSP `--remap` fix, typed handle struct (Pattern B) uniformly for all opaque handles including the previously-deferred `SDL_RWops` / `SDL_SysWMinfo` / `SDL_SysWMmsg`, tag/typedef canonicalization via per-header RSP, and `SDL_GUID -> System.Guid` substitution. Toolchain-neutral policy decisions feed back into the constitution; implementation lives on the active spike pending the ADR-004 amendment.

**References:** temporary backend vision promoted into the constitution, SkiaSharp dual backend precedent, .NET interop docs, current C `long`/`CLong` findings, spike multi-TFM compile evidence under `spikes/binding-generators/output/reports/`.

**Design direction:**

- Keep a semantic model (CppAst-derived or ClangSharp+postprocess-derived) as the source of truth.
- Add a backend-ready raw ABI projection that records entry point, managed raw wire type, unsafe requirement, platform attribution, backend compatibility, modern C integer requirement, variadic mapping, and source evidence.
- Generate file-level backend splits instead of per-function conditional spaghetti:
  - `Raw/Commands.Common.g.cs`
  - `Raw/Commands.DllImport.g.cs`
  - `Raw/Commands.LibraryImport.g.cs`
- Use `DllImport` for legacy TFMs.
- Use `LibraryImport` for modern TFMs where source-generated interop is supported.
- Do not casually polyfill `CLong` / `CULong` on downlevel TFMs. A fake portable struct cannot model Windows LLP64 and Unix LP64 honestly in one portable asset.
- Compatibility packages such as `System.Memory` are allowed for public/friendly APIs when justified; they are not a license to fake ABI primitives.

**Exit evidence:**

- Emitter tests cover both raw backends.
- Compile-check covers every supported library TFM.
- Snapshot diffs are intentional and limited to backend file layout/import syntax.
- No public `[DllImport]` / `[LibraryImport]` leaks.

**Non-goals:**

- No public friendly overload generation.
- No owner/disposal wrapper layer.

## Milestone 5: Public Typed Low-Level API Projection

**Goal:** Generate public low-level methods over the internal raw ABI while keeping extern declarations internal.

**References:** constitution layer contract, API design extend-only guidance, PublicApiGenerator/Verify API snapshot pattern.

**Scope:**

- Add public methods on the manifest-driven public class, such as `SDL2.SDL`.
- Public methods call the internal raw ABI class.
- Typed handles, enums, structs, callbacks, and constants remain public generated types.
- Unsafe pointer signatures remain available when that is the honest low-level C shape.
- SDL2 bool-like raw `int` values convert to `bool` only when a rule proves the public method is predicate-like.
- Platform-only methods carry the same platform attribution as the raw ABI member.
- Introduce public API snapshot review before first public preview.

**Exit evidence:**

- Emitter tests prove public methods call internal raw methods.
- Compile-check passes across all library TFMs.
- Public API snapshot exists and is reviewed.
- Source inspection or automated check proves no public raw ABI container or effectively public raw extern leaks.

**Non-goals:**

- No SDL2-CS compatibility freeze.
- No `SafeHandle` / `IDisposable` owner wrappers.
- No callback lifetime helper layer beyond preserving low-level callback identity.

## Milestone 6: Friendly Overload Projection

**Goal:** Add ergonomic overloads through explicit, tested projection rules rather than ad hoc emitter special cases.

**References:** constitution friendly overload contract, temporary UTF-8/span notes, Alimer overload evidence, `System.Memory` compatibility package posture.

**Baseline overload patterns:**

- `string` caller input encoded to null-terminated UTF-8.
- `ReadOnlySpan<byte>` for pre-encoded UTF-8.
- `ReadOnlySpan<T>` for counted input buffers when SDL does not retain the pointer.
- `Span<T>` for counted output buffers when SDL writes within caller-provided bounds.
- `out T` for required single-element output pointers.
- `ref T` for required single-element in/out pointers.
- Explicit fmt-only helpers for accepted variadic logging/formatting calls.

**Compatibility posture:**

- Use `System.Memory` or similar package dependencies for `netstandard2.0` / `net462` when the dependency is deliberate and package-smoke validated.
- Prefer stack allocation for small UTF-8 buffers and pooled arrays for larger hot-path buffers when the implementation pattern becomes performance-sensitive.
- Do not infer ownership or lifetime from pointer shape alone.

**Exit evidence:**

- RED/GREEN tests for every overload pattern.
- Compile-check passes across all library TFMs.
- Package-consumer smoke exercises representative string/path/resource pairs.
- Generated docs/reporting make allocation behavior and fmt-only variadic behavior visible.

**Non-goals:**

- No giant handwritten wrapper layer.
- No automatic lifetime-safe callback wrapper until callback pinning/lifetime policy is designed.
- No owner wrapper layer such as `SdlWindow : IDisposable` in this milestone.

## Milestone 7: SDL2.Core Production Flip And Reproducibility

**Goal:** Move SDL2.Core from preview artifacts to committed production generated source and retire SDL2-CS production use for Core.

**References:** package-first release strategy, generated stamp contract, old Stage 1 production flip notes.

**Scope:**

- Generate into `src/SDL2.Core/Generated/`.
- Commit generated `.g.cs` files.
- Remove the SDL2.Core production compile include for `external/sdl2-cs/src/SDL2.cs`.
- Keep `external/sdl2-cs` only as a reference oracle until remaining production uses retire.
- Add `.generated-stamp` with generator/toolchain version, vcpkg state, SDL library version, header-set fingerprint, header count, and parse views.
- Ensure the stamp has no wall-clock fields.
- Add stale-generated-output validation before expensive native/package work.
- Stale-output diagnostics name the family, stale field, and remediation command/workflow.
- Require regeneration from the same inputs to be diff-clean.
- Generated output uses stable LF line endings and deterministic ordering.

**Exit evidence:**

- `dotnet build src/SDL2.Core/SDL2.Core.csproj` succeeds.
- Compile-check is repointed or retired only if project build fully replaces its value.
- Package-first smoke passes from local package feed and covers at least init, quit, error retrieval, environment-permitting window create/destroy, and one deterministic callback path.
- No production source path uses `external/sdl2-cs/src/SDL2.cs` for SDL2.Core.

## Milestone 8: SDL2 SysWM And Satellite Generation

**Goal:** Generate all in-scope SDL2 families and remove remaining production dependency on SDL2-CS.

**References:** old Stage 2 notes, SDL_image upstream test model, satellite smoke strategy, package family manifest topology.

**Scope:**

- Implement full typed `SDL_SysWMinfo` and `SDL_SysWMmsg` only with platform layout proof.
- Add minimal forward-declaration stubs for platform handle types used by SysWM unions.
- Enable one SDL2 satellite at a time:
  - SDL2.Image;
  - SDL2.Mixer;
  - SDL2.Ttf;
  - SDL2.Gfx;
  - SDL2.Net after its package family enters `build/manifest.json`.
- Each satellite's `binding_generation` config moves from placeholder to full config in the same slice that enables the family.
- Satellite outputs emit satellite-owned functions and types only.
- Core-owned `SDL_*` structs, handles, enums, callbacks, and constants are referenced from SDL2.Core, never redeclared.
- Add duplicate core-type guard with errors naming the satellite family, offending type, source header, and expected core-owned reference.
- Add symbol-existence validation after Harvest and before Package using platform-appropriate export tooling.
- Split package smoke per family when partial-scope smoke support lands.

**Exit evidence:**

- Every SDL2 managed family builds from generated source.
- Full 7-RID native smoke and package-consumer smoke pass for generated SDL2 families.
- First public SDL2 preview is AST-generated, package-first, and not shaped by SDL2-CS public API inertia.

## Milestone 9: Smoke And Asset-Backed Testing Expansion

**Goal:** Turn the testing strategy into durable CI/release confidence without importing upstream SDL wholesale.

**References:** [`testing-strategy.md`](testing-strategy.md), upstream SDL2 core tests, SDL_image test runner, SDL_mixer/SDL_ttf/SDL_net samples, SkiaSharp/LibGit2Sharp fixture patterns.

**Scope:**

- Create `tests/smoke-tests/assets/` with fixture policy and provenance.
- Replace root branding image usage in smoke tests with tiny generated fixtures.
- Add SDL2 core BMP/WAV/RWops real-file checks.
- Expand SDL_image load checks for mandatory image formats: PNG, JPEG, WebP, TIFF, AVIF, and optional QOI only if promoted.
- Add SDL_ttf real font open/render checks with a clean-license tiny font.
- Add SDL_gfx pixel mutation assertions.
- Add SDL_mixer real-file load checks after the LGPL-free codec contract and MIDI/Timidity story are settled.
- Keep manual diagnostic apps outside CI gates.

**Exit evidence:**

- CI smoke remains headless and deterministic.
- Smoke fixtures are tiny, committed, generated or explicitly licensed, and test-owned.
- Package-consumer smoke validates representative generated API calls, not only native load/init.

## Milestone 10: SDL3 Extension

**Goal:** Add SDL3 as a real second consumer after SDL2 public-release progress and native packaging support exist.

**References:** ADR-004, ppy/SDL3-CS, Alimer SDL3 evidence, SDL3-specific upstream headers and `sdl.json`.

**Scope:**

- Add SDL3 package families and native build support first.
- Introduce SDL3 generation only when SDL3 becomes a real second consumer.
- Promote shared generator code out of SDL2 target-local folders only when ADR-002 reuse criteria are met.
- Encode SDL3-specific ABI rules instead of copying SDL2 behavior:
  - 1-byte bool-like values;
  - `SDL_IOStream` replacing `SDL_RWops`;
  - SDL3 platform macro model;
  - SDL3 namespace and native library identity.
- Re-evaluate SDL3 TFM support instead of blindly copying SDL2's `netstandard2.0` / `net462` obligations.

**Exit evidence:**

- SDL3 Core, Image, Mixer, and Ttf generated output compiles and packages through the internal feed.
- SDL3 package-consumer smoke proves load and minimal calls per generated family across the supported RID/TFM matrix.
- SDL3-specific ABI decisions are captured in the constitution or a companion ADR before any public SDL3 preview.

## Continuous Maintenance

- Keep [`binding-generator-constitution.md`](binding-generator-constitution.md) updated with every generator rule change.
- Keep [`testing-strategy.md`](testing-strategy.md) updated when test layer boundaries or fixture policy change.
- Keep [`../playbook/binding-generator-maintenance.md`](../playbook/binding-generator-maintenance.md) updated when operational procedure changes.
- Keep [`../playbook/binding-output-oracle-validation.md`](../playbook/binding-output-oracle-validation.md) updated when oracle review lanes or evidence sources change.
- ADR-004 (Reopened 2026-05-23) holds the original toolchain decision record; the active selection happens through the spike under `spikes/binding-generators/`. Update ADR-004's status or supersede it with a follow-up ADR once the spike concludes.
- Treat peer bindings as evidence, never authority.
- Prefer explicit deferral over fake ABI success.
- Run Slopwatch after code/test/project changes. Documentation-only changes do not require Slopwatch unless they alter embedded code or project snippets in a way that should be linted by the tool.

## Retired Active References

The following were folded into this roadmap, the constitution, and the testing strategy, then removed from active documentation:

- temporary raw ABI / public wrapper / multi-TFM notes under `docs/binding-autogen/temp/temp.txt`;
- temporary testing strategy notes under `docs/binding-autogen/temp/testing/`;
- old binding-generator superpowers specs and plans;
- old local-output-loop and stage-plan transcripts;
- semantic pipeline, macro constants, macro taxonomy, and P0 fix implementation plans;
- binding-autogen research/spike notes.

Git history remains the archive for historical audit detail.
