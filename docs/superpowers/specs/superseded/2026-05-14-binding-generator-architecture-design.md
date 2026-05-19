# Binding Generator Architecture Design

> **Status (2026-05-17):** ⚠ **SUPERSEDED.** Architecture decisions absorbed into [`../2026-05-16-binding-generator-unified-design.md`](../2026-05-16-binding-generator-unified-design.md). Kept in `superseded/` for historical reference; do not treat as authoritative. The unified design retires the standalone `Preview*` shape, locks `build/manifest.json` as the per-family config center, and reframes the work as a manifest-driven per-family generator.
>
> Original status banner preserved below for context.
>
> **Original status:** Temporary design spec for the Phase 4 binding-generator architecture. Durable decisions from this file should be promoted into canonical binding-autogen docs after the implementation plan is accepted and the first implementation slice lands.
>
> **Revised 2026-05-15:** Generator folded into the Cake build host (was a standalone `src/`-tree console app in the 2026-05-14 draft); Linux-container locked as the canonical determinism contract; stub strategy reframed against ppy/SDL3-CS evidence (preprocessor-macro switching only — no mingw-w64, no Apple SDK); `SDL_syswm.h` typed-union layout deferred to Stage 2; SDL3 generator gated on PD-7. The architecture reuse rules and platform catalog are unchanged at the principle level — the implementation home moved.

## Goal

Design a CppAst-based SDL2 binding-generator architecture that covers SDL2 Core plus all in-scope SDL2 satellite families, hosted inside the Cake build host so it inherits the existing target-centric orchestration patterns from [ADR-002](../../decisions/2026-05-05-target-centric-build-host.md) and the contract-centric data layer from [ADR-003](../../decisions/2026-05-12-build-host-data-layer.md). Keep the first implementation plan focused on the SDL2.Core proof-of-life slice.

## Approved Scope

This spec defines the generator architecture and research boundaries before writing the Stage 1 implementation plan.

In scope:

- SDL2 generator architecture for Core, Image, Mixer, Ttf, Gfx, and Net.
- A bounded SDL2 header/platform research pass across core and satellite public headers.
- A platform/backend model that parses, catalogs, and emits every SDL2 public OS/platform surface the generator can represent at function level.
- A Stage 1 implementation boundary that executes SDL2.Core function-level platform attribution first; `SDL_syswm.h` typed-union layout is deferred to Stage 2.
- A future SDL3 extension seam — kept target-local until SDL3 becomes a real second consumer per ADR-002 §2.4 — gated on PD-7 (SDL2 real-public-release) completion.

Out of scope:

- Reopening ADR-004's CppAst toolchain decision.
- Implementing code, changing project files, changing Cake targets, or changing CI.
- Designing a high-level RAII wrapper layer such as `SdlWindow : IDisposable`.
- Claiming native-package support for OSes/RIDs that the repository does not package yet.
- Starting SDL3 binding work before PD-7 ships.

## Design Summary

The generator is **hosted inside the Cake build host** under `build/_build/Targets/GenerateBindings/`, with cross-cutting validators under `build/_build/Validation/BindingGeneration/`. This is a deliberate reframe from the 2026-05-14 draft, which proposed a standalone `net10` console app at `src/Janset.SDL2.Bindings.Generator/`. Binding generation is a CI/CD concern and the Cake host already owns the equivalent infrastructure (vcpkg state, manifest parsing, validation patterns, tool wrappers, logging, paths, runtime profile) — a parallel orchestration stack would duplicate that surface and break ADR-002 §2.1's target-centric navigation rule.

The split inside the target follows [`AGENTS.md`](../../../AGENTS.md) §"Pure code stays pure" and §"Cake nativeness is a hard rule at build boundaries" simultaneously. "Pure" here means parser/model/emitter policy should not depend on Cake task orchestration or host state. It does **not** mean build-host file, path, or JSON I/O may bypass Cake abstractions; any code that reads headers, writes generated files, or writes `.generated-stamp` uses `ICakeContext`, `Cake.Core.IO` paths, and the host Cake helper extensions.

- **Pure policy code** (`Parsing/`, `Model/`, most of `Emitting/`) — no `ICakeContext`, no Cake aliases, no Cake `Tool<TSettings>` dependencies. Unit-testable as ordinary C# from `build/_build.Tests/Unit/`.
- **Cake-native build boundary code** (`Targets/GenerateBindings/HeaderSet/`, `Data/BindingGeneration/`, generated-file persistence, `GenerateBindingsTask`, `BindingGenerationRunner`, `ServiceCollectionExtensions`) — owns filesystem, JSON, path construction, orchestration, logging, exception translation, and DI registration per ADR-002 §2.2 / §2.5 / §2.6 and ADR-003.

Alimer.Bindings.SDL is the reference for CppAst emitter organization and typed-handle output shape. It is not the reference for platform parsing because its single-pass macro-union style is not strong enough for platform-conditioned SDL headers. ppy/SDL3-CS is the reference for the neutral-plus-platform pass orchestration pattern, executed through CppAst rather than ClangSharpPInvokeGenerator and through preprocessor-macro switching only — no `--target` cross-compile flags, no mingw-w64, no Apple SDK.

Stage 1 will implement only SDL2.Core, but the design covers all SDL2 families so the core-owned type universe and satellite reuse rules are not discovered too late.

This spec intentionally expands one detail from the accepted strategy brief: the brief lists the platform catalog as illustrative; this design fixes the model as a `(OsCondition, BackendCondition[])` tuple catalog of ~7–8 entries for SDL2.Core, and requires the generator to parse, catalog, and emit every public function the catalog covers — while keeping `SDL_syswm.h` typed-union layout outside Stage 1 scope.

## Component Architecture

Planned layout — hosted in the Cake build host, target-local per ADR-002 §2.4 until SDL3 creates a real second consumer:

```text
build/_build/Targets/GenerateBindings/
|-- GenerateBindingsTask.cs                    (Cake task — orchestration, [TaskName], CakeException translation)
|-- GenerateBindingsRequest.cs                 (immutable target input contract per ADR-002 §2.2)
|-- Sdl2CoreGenerationConfig.cs                (Stage 1 family identity + input headers + library name + namespace/class + owned prefixes)
|-- ServiceCollectionExtensions.cs             (AddGenerateBindings() — focused DI registration per ADR-002 §2.5)
|-- BindingGenerationRunner.cs                 (Cake-side runner — invokes the pure emitter, stages outputs through Cake IO)
|-- Parsing/                                   (PURE — no Cake)
|   |-- CppAstParseRunner.cs
|   |-- PlatformCatalog.cs
|   |-- PlatformParseView.cs
|   `-- ParseDiagnosticFormatter.cs
|-- Model/                                     (PURE — no Cake)
|   |-- BindingModel.cs
|   |-- DeclarationCollector.cs
|   |-- DeclarationMergePolicy.cs
|   |-- CoreOwnedTypeMap.cs
|   `-- KnownUnsupportedDeclarationPolicy.cs
|-- Emitting/                                  (PURE — no Cake)
|   |-- CodeWriter.cs
|   |-- CsCodeGenerator.cs
|   |-- CsCommandEmitter.cs
|   |-- CsConstantEmitter.cs
|   |-- CsEnumEmitter.cs
|   |-- CsHandleEmitter.cs
|   |-- CsStructEmitter.cs
|   `-- CsCallbackEmitter.cs
build/_build/Data/BindingGeneration/            (file-backed/tool-read binding contracts per ADR-003)
|-- GeneratedStamp.cs
`-- GeneratedStampRepository.cs

build/_build/Targets/GenerateBindings/HeaderSet/ (target-local SDL header input services)
|-- ResolvedHeaderSet.cs
|-- HeaderSetFingerprint.cs
|-- HeaderSetResolver.cs
`-- HeaderSetFingerprintCalculator.cs

build/_build/Validation/BindingGeneration/     (cross-cutting validators per ADR-002 §2.4)
|-- BindingGenerationCoherenceValidator.cs     (PreFlight — .generated-stamp drift detection at Stage 1)
|-- BindingGenerationStampContract.cs
|-- IBindingGenerationCoherenceValidator.cs
|-- BindingGenerationCoherenceReport.cs
`-- (Stage 2) BindingSymbolExistenceValidator.cs (Pack stage — declared-but-not-exported guard)
```

Responsibilities:

- `GenerateBindingsTask` (Cake `[TaskName("GenerateBindings")]`) — reads `BuildContext`, validates target inputs at the task boundary, builds the `GenerateBindingsRequest`, drives the `BindingGenerationRunner`, translates expected failures to `CakeException` with actionable diagnostics.
- `BindingGenerationRunner` — Cake-aware shell. Stages headers through Cake-native paths, invokes the pure emitter, writes generated files through Cake-native IO so logging/dry-run semantics line up with the rest of the build host.
- `GenerateBindingsRequest` — immutable input record (family, header set root, output root, runtime profile). Earned, not mandatory, per ADR-002 §2.2 — present here because the runner takes a stable input contract.
- `Sdl2CoreGenerationConfig` — describes Stage 1 family identity, input headers, library name, output namespace/class, owned prefixes, and deferred declarations. General family config is introduced only when satellite generation creates a real second consumer.
- `HeaderSetResolver` / `HeaderSetFingerprintCalculator` — resolve the canonical vcpkg-installed public headers via Cake filesystem APIs, reject missing or empty header sets, and compute the header fingerprint. They are target-local services because the SDL header input tree is generation input, not a build-host-owned persisted contract.
- `PlatformCatalog` — owns the `(OsCondition, BackendCondition[])` tuple catalog (~7–8 entries for SDL2.Core in Stage 1). Each catalog entry corresponds to exactly one parse view. The catalog is data-driven and reviewable, not magic constants buried in code paths.
- `CppAstParseRunner` — runs one parse view per catalog entry plus a neutral view. Each parse view is a `CppParserOptions` instance with the master undefine-all-platform-macros set, the entry's macro group defined, and no `--target` cross-compile flag.
- `DeclarationCollector` — converts CppAst declarations into a generator-owned model.
- `DeclarationMergePolicy` — deduplicates neutral and platform/backend declarations and fails on incompatible signatures.
- `CoreOwnedTypeMap` — prevents satellites from redeclaring core-owned SDL types.
- `KnownUnsupportedDeclarationPolicy` — explicit allowlist of declarations the generator intentionally omits (variadic functions with no fmt-only safe wrapper, the typed `SDL_SysWMinfo` union at Stage 1, etc.). Any unclassified unsupported declaration fails generation.
- `Cs*Emitter` classes — generate category-specific `.g.cs` files in a single foreach loop per Function.
- `GeneratedStampRepository` — reads and writes generator, toolchain, vcpkg, header, and platform-pass state via `CakeJsonExtensions` / Cake filesystem helpers.
- `BindingGenerationCoherenceValidator` (under `build/_build/Validation/BindingGeneration/`) — PreFlight validator that compares the committed `.generated-stamp` against current vcpkg / header / toolchain state. Joins existing PreFlight validators alongside `HybridStaticOverlayValidator`, manifest schema validators, etc., per ADR-003 cross-cutting validator pattern.

**Pure vs Cake split.** Parser/model/emitter policy code carries no `ICakeContext`, no Cake aliases, no Cake `Tool<TSettings>` dependencies. Header resolution, stamp serialization, generated-file persistence, and task orchestration are build-host I/O boundaries and therefore use Cake-native abstractions. All of this remains target-local under `Targets/GenerateBindings/` until SDL3 creates real reuse pressure (ADR-002 §2.4).

**Tests** use `FakeCakeWorld` / Cake `FakeFileSystem` for any filesystem behavior. Current Stage 1 unit tests live under `build/_build.Tests/Unit/Targets/GenerateBindings/`; future `GenerateBindingsTask` and PreFlight stamp validator scenario tests live under `build/_build.Tests/Scenarios/GenerateBindings/` and use `TargetTestHost`.

No `Janset.SDL.Bindings.Generator.Core` project is created in Stage 1. No `src/Janset.SDL2.Bindings.Generator/` standalone project is created at any stage. Stage 3 (SDL3, gated on PD-7) introduces a sibling `Targets/GenerateSdl3Bindings/` Cake target; shared code, if any, gets promoted out of SDL2's target only when ADR-002 §2.4 promotion criteria are met.

## Toolchain and Orchestration Constraints

The generator uses the ADR-004 version trio, **pinned to Linux-x64 only**:

```xml
<PackageVersion Include="CppAst" Version="0.24.0" />
<PackageVersion Include="libclang.runtime.linux-x64" Version="20.1.2" />
<PackageVersion Include="libClangSharp.runtime.linux-x64" Version="20.1.2" />
```

Non-Linux runtime variants (`libclang.runtime.{win-x64, osx-x64, osx-arm64, linux-arm64}`) are **intentionally absent**. The generator is Linux-canonical: it runs only inside the pinned `linux-builder` container and fails closed when the host RID is anything other than `linux-x64`. This is the price of the reproducibility contract — the Layer 5 test (regenerate → byte-identical output) requires the same OS, apt sysroot, and libclang runtime across every run, in CI and locally.

These versions move as a coordinated set. A CppAst bump drives the libclang/libClangSharp runtime bump and requires repeating the spike/runtime validation before accepting the new trio.

Generation is maintainer-side only:

- Generated source is committed to git.
- Consumer builds never invoke CppAst, libclang, Docker, Python, PowerShell, or platform SDK headers.
- CI verifies committed output by regenerating and requiring a clean generated-source diff.

Planned orchestration surfaces:

- `build/_build/Targets/GenerateBindings/` owns release-grade orchestration: vcpkg state resolution, canonical header provisioning, generator invocation, output validation, and stamp writing.
- `tools.cs generate-bindings [--family X]` is the local shorthand. It publishes the Cake host once (Release, side directory), mounts the repo root + a host vcpkg cache directory into the `linux-builder` container, and runs `dotnet ./.cake-host/Build.dll --target GenerateBindings --family X` inside the container. The container writes regenerated files back through the volume mount; the maintainer reviews `git diff`/`git status` on the host. **Docker is a hard prerequisite — there is no host-OS fallback.**
- `.github/workflows/regenerate-bindings.yml` is the manual CI entry point. It runs generation in the Linux builder container and opens a reviewable PR with regenerated source and updated stamps.

The canonical header source is one vcpkg-installed SDL include tree after header-identity validation. The generator still runs multiple parse views over that same header set because byte-identical headers can produce different ASTs under different preprocessor macro configurations (per the platform catalog).

## Platform and Header Strategy

The platform strategy is broader than the current packaged RID set.

The repository currently packages seven RIDs:

```text
win-x64, win-x86, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
```

The generator should still parse, catalog, and emit every SDL2 public OS/platform surface it can model. This keeps the generated API aligned with SDL2 headers rather than with today's native package inventory. Documentation and package metadata must stay clear that emitted platform APIs do not imply Janset ships native packages for Android, iOS, WinRT, GDK, or other non-packaged platforms.

### Platform catalog

The catalog distinguishes:

- OS conditions: Windows, WinRT, GDK, Linux, macOS, iOS, Android, tvOS, and other SDL2-recognized operating systems.
- Backend conditions: X11, Wayland, KMSDRM, Cocoa, UIKit, Windows video, WinRT video, DirectFB, Vivante, OS/2, and similar `SDL_VIDEO_DRIVER_*` conditions.
- ABI-affecting macro conditions from `SDL_platform.h`, `SDL_config.h`, `SDL_stdinc.h`, `SDL_system.h`, `SDL_main.h`, and `SDL_syswm.h`.

Where a condition maps cleanly to .NET platform analysis, emitted APIs use `[SupportedOSPlatform]`. Where a condition is backend-specific rather than OS-specific, generated output preserves condition metadata in generated comments or internal metadata so reviewers can trace why the member exists.

### Parse views

The generator runs:

1. A neutral parse view with every SDL platform identification macro undefined.
2. One parse view per SDL2 platform/backend catalog entry that affects public declarations or layouts. Stage 1's catalog targets ~7–8 entries (Neutral, Windows desktop, WinRT, GDK, Linux, macOS, iOS, Android). DirectFB/Vivante/MIR/OS-2 are documented exclusions in Stage 1.

Each parse view is a `CppParserOptions` instance with:

- The master "undefine-all-platform-macros" set applied first, so a prior pass's defines cannot leak into the next pass.
- The catalog entry's `(OsCondition, BackendCondition[])` macro group defined.
- The vcpkg-installed canonical SDL header set on the include path, plus the Linux container's apt sysroot for transitive `<stdint.h>`/`<stddef.h>`/`<X11/Xlib.h>`/`<wayland-client.h>`/etc.

**Important — no `--target` cross-compile flag.** Platform separation is **preprocessor-driven only**. The 2026-05-14 draft of this spec implied a sysroot/stub-directory pattern (mingw-w64 for Windows, Apple SDK stubs for macOS); that framing was retracted on 2026-05-15 after ppy/SDL3-CS Dockerfile + `generate_bindings.py` were re-verified by WebFetch and the historical local CppAst platform-pass spike was re-read. SDL2's public headers carry their own forward declarations for cross-platform opaque types (`typedef struct _NSWindow NSWindow;`, `typedef struct ANativeWindow ANativeWindow;`, `struct gbm_device;`, etc.), so function-level platform surface parses without any hand-written platform stubs.

The exact catalog lives in `PlatformCatalog.cs` as data and is reviewed at Stage 1 plan landing.

### SDL2.Core risk headers

The bounded audit found platform risk concentrated in SDL2.Core:

- `SDL_syswm.h` contains platform/backend-conditioned includes, structs, unions, and `SDL_GetWindowWMInfo`.
- `SDL_system.h` contains Windows, Linux, iOS, Android, WinRT, GDK, and neutral declarations.
- `SDL_main.h` contains platform-specific main-handling declarations.
- `SDL_platform.h`, `SDL_config.h`, and `SDL_stdinc.h` define ABI-relevant platform macros and primitive typedef behavior.
- GL/EGL headers include platform-specific handles and external platform headers.

**`SDL_syswm.h` function-level surface is Stage 1; typed-union layout is Stage 2.** Stage 1 emits `SDL_GetWindowWMInfo` as a function with an opaque `nint`-shaped `SDL_SysWMinfo*` parameter and records the typed-union deferral in the audit log. Stage 2 delivers:

- A small forward-declaration stub library (~15–20 opaque types: `HWND`, `HDC`, `HINSTANCE`, `IInspectable`, `Display*`, `Window`, `IDirectFB*`, `wl_display`/`wl_surface`/`xdg_*`, `gbm_device`, `EGLNativeDisplayType`, etc.).
- Typed `SDL_SysWMinfo` and `SDL_SysWMmsg` with `[StructLayout(LayoutKind.Explicit, Size = 64)]` on the `info`/`msg` unions; every catalog branch at `[FieldOffset(0)]`; the 64-byte fixed-size lock from `SDL_syswm.h:346-348` honored.

Apple-SDK redistribution concerns do not apply because Apple's non-`__OBJC__` `typedef struct _NSWindow NSWindow;` is provided directly by `SDL_syswm.h:86` (and the equivalent for `UIWindow` at `:94-95`, `ANativeWindow` at `:105`). The Stage 2 stub library is pure forward declarations, not Apple SDK or Windows SDK derivatives.

The 2026-05-14 draft framed `SDL_syswm.h` as a first-class Stage 1 proof target with the instruction "if the generator cannot model it safely, stop and return to design review." That framing was correct for the typed-union work but wrong about its stage placement — the historical function-level platform-pass spike intentionally deferred the union work, and Stage 1's production-shape proof is already adequate at function level (multi-pass orchestration, dedup, fail-closed merge, platform attribution, dual emit, typed handles, friendly overloads, AOT-clean signatures). The "stop and return to design review" guidance now applies to Stage 2.

### Satellite topology

Satellite headers are mostly platform-neutral, but they are not independent type islands. The local audit confirmed the expected shared core-type dependencies:

| Header | Approximate function declarations | Core SDL types observed |
| --- | ---: | --- |
| `SDL_image.h` | 59 | `SDL_Renderer`, `SDL_RWops`, `SDL_Surface`, `SDL_Texture`, `SDL_version` |
| `SDL_mixer.h` | 97 | `SDL_bool`, `SDL_RWops`, `SDL_version` |
| `SDL_ttf.h` | 83 | `SDL_bool`, `SDL_Color`, `SDL_RWops`, `SDL_Surface`, `SDL_version` |
| `SDL2_gfxPrimitives.h` | 59 | `SDL_Renderer`, `SDL_Surface` |
| `SDL2_rotozoom.h` | 5 | `SDL_Surface` |
| `SDL2_framerate.h` | 5 | none from the audited core-type set |
| `SDL_net.h` | 34 | `SDL_version` |

The SDL2.Core generator owns every core SDL type. Satellite generation emits only satellite-owned declarations and references core-owned managed types. A satellite generation run fails if it redeclares a core-owned type or lowers a known core type to an untyped fallback.

## Emitted Source Contract

Generated output is committed to the repository and compiled into managed packages. Consumers never run the generator.

Per family output:

```text
src/SDL2.<Family>/Generated/
|-- Commands.g.cs
|-- Constants.g.cs
|-- Enums.g.cs
|-- Handles.g.cs
|-- Structs.g.cs
|-- Callbacks.g.cs
|-- Platform/
|   |-- <ConditionName>/
|   |   `-- *.g.cs
`-- .generated-stamp
```

### Five production-shape anchors

The emitter is not a raw P/Invoke-only generator. It must carry the five production-shape anchors from the accepted strategy:

1. **Full TFM matrix with dual P/Invoke emit.** SDL2 output targets `net10`, `net9`, `net8`, `netstandard2.0`, and `net462`. Every function gets a `[LibraryImport]` branch for `net7+` and a `[DllImport]` branch for legacy TFMs in the same emitter loop. This follows `docs/binding-autogen/research/binding-autogen-feasibility.md` Rule 1, Microsoft Learn P/Invoke source-generation guidance, and dotnet/runtime LibraryImport compatibility notes.
2. **Typed `readonly partial struct` baseline for opaque handles.** SDL opaque pointers become small value wrappers over `nint`, not raw `IntPtr` and not `SafeHandle`. Each handle shape includes `IsNull`, `Null`, `IEquatable<T>`, equality operators, implicit conversion to/from `nint`, and `[DebuggerDisplay]`. Alimer.Bindings.SDL and Vortice.Vulkan both validate this ecosystem pattern. Vortice also distinguishes non-dispatchable `ulong` handles for Vulkan; SDL opaque pointers remain pointer-sized `nint`.
3. **Friendly overloads emitted beside raw P/Invoke.** `string`, `ReadOnlySpan<byte>`, `Span<T>`, `out`, and `ref` overloads are generated alongside raw pointer signatures. UTF-8 string handling follows Microsoft custom-marshalling guidance and must not repeat SDL2-CS's ANSI/default-marshalling mistakes.
4. **Platform attribution from multi-pass parsing.** `[SupportedOSPlatform]` and preserved backend condition metadata come from neutral plus platform/backend parse views, not from hand-written lists. ppy/SDL3-CS is the closest reference for the pass shape; Janset executes it through CppAst.
5. **Satellite/shared-type topology.** SDL2.Core owns the shared SDL type universe. Satellite emitters reference core-owned managed types and only emit satellite-owned symbols. The SDL2_image CppAst spike proved this pattern by generating image-owned APIs without redeclaring core types.

Additional emitted-source rules:

- SDL2 `SDL_bool` modeled as int-backed, never as raw C# `bool`.
- SDL3 bool behavior is not shared with SDL2.
- Numeric SDL ID types are emitted as typed `enum : uint` or `enum : ulong` when the C value space is opaque.
- C type provenance is preserved in generated output or adjacent metadata so reviewers can trace managed signatures back to native declarations during SDL version bumps.
- Callback shapes use function pointers and `[UnmanagedCallersOnly]` patterns where representable.
- Variadic functions are not pretended to be fully representable. Stage 1 must define safe fmt-only or raw-fidelity handling for SDL logging-style calls before emitting them.
- `Memory<T>` is not emitted in P/Invoke signatures; use `Span<T>`/`ReadOnlySpan<T>` plus raw pointer overloads.
- Projects consuming generated output require `AllowUnsafeBlocks=true`; `IsAotCompatible=true` applies where the target framework supports it.
- Constants distinguish literal `const` values from computed `static readonly` values.
- Enums preserve explicit underlying type and bit-flag intent.
- AOT-safe source-generated marshalling patterns are preferred; reflection-based `ICustomMarshaler` is out of scope.
- Constants, enums, flags, structs, unions, and callbacks are emitted into category files with deterministic ordering and modern C# syntax already allowed by the repo baseline.

## State and Coherence Artifacts

Each generated family writes `Generated/.generated-stamp`. The exact schema belongs to the Stage 1 implementation plan, but it must capture:

- generator identity and git/version information;
- CppAst/libclang/libClangSharp versions;
- vcpkg state, including `vcpkg.json`, vcpkg baseline, and manifest library version;
- canonical header-set fingerprint and header count;
- platform/backend parse views used for the family.

The stamp is part of the binding/native coherence contract. A PreFlight validator recomputes the current vcpkg/header/toolchain state and fails with an actionable message when committed generated source is stale.

## Failure Policy

The generator fails closed. It does not emit success-shaped fallbacks when it cannot prove a safe mapping.

Hard failures:

- CppAst parse errors in a required parse view.
- Unknown C type mapping for a public declaration.
- Satellite redeclaration of a core-owned SDL type.
- A platform/backend declaration with incompatible signatures across parse views.
- A layout-affecting platform/backend type that cannot be represented deterministically.
- Nondeterministic output ordering.
- Missing required output files.
- `.generated-stamp` mismatch during reproducibility validation.

Non-target OS declarations are not failures. They are emitted with platform metadata when modelable, while native package support remains documented separately.

## Validation Strategy

Stage 1 validation for SDL2.Core:

- Build generated SDL2.Core across `net10`, `net9`, `net8`, `netstandard2.0`, and `net462`.
- Regenerate from the same vcpkg/toolchain/header state and require a clean generated-source diff.
- Snapshot the public API surface so future generator changes are reviewable.
- Use the existing TUnit/MTP test infrastructure; snapshot testing should use Verify/PublicApiGenerator unless the implementation plan finds a better existing repo pattern.
- Run package-consumer smoke against the packaged Core family.
- Exercise representative APIs: `SDL_Init`, `SDL_Quit`, window create/destroy, UTF-8 error retrieval, and at least one callback path.
- Prove at least one real package-first consumer path, such as `learning-sdl2` or an equivalent smoke app, without source/project references.
- Emit a header/platform audit artifact that records parse views, platform/backend conditions, and any preserved condition metadata.

Stage 2 validation for satellites:

- Duplicate core-owned type validation for every satellite.
- Reference cross-check against SDL2-CS as a migration oracle, with differences categorized as match, typed-handle delta, known quirk, or true generator defect. For SDL3, ppy/SDL3-CS is the primary cross-check oracle and Alimer can be a secondary CppAst shape reference.
- Symbol-existence validation before packaging once multiple generated families and harvested native payloads are available.
- Per-family package-consumer smoke.

Symbol-existence validation belongs at the Pack stage after native harvest and before NuGet packaging. It checks every emitted `EntryPoint` against the corresponding exported symbol table using the platform-appropriate tool (`dumpbin`, `nm`/`readelf`, or `nm -gU`). This closes the declared-but-not-exported failure mode, especially for Unix visibility and SDL2_gfx-style satellite risks.

## Documentation and Sequencing

This spec feeds the Stage 1 implementation plan. The plan should implement only the SDL2.Core proof-of-life slice:

1. Add the SDL2 generator module to the Cake build host project.
2. Add enough platform catalog and parse-view infrastructure to prove SDL2.Core, especially `SDL_syswm.h`.
3. Generate committed SDL2.Core output and `.generated-stamp`.
4. Wire SDL2.Core away from `external/sdl2-cs/src/SDL2.cs`.
5. Add Stage 1 validation.

**Precursor slice (2026-05-15):** A narrow local-output loop lands before Task 4. It establishes the Docker-based iteration surface (Cake `GenerateBindings` target + `tools.cs generate-bindings` subcommand + derived `binding-generator.Dockerfile`) so subsequent model + emitter design (Tasks 4-6) iterates against real CppAst-output artifacts. Output lands gitignored under `artifacts/generated-bindings-preview/sdl2-core/`; production-location flag-flip is Task 7. See [`2026-05-15-binding-generator-local-output-loop-design.md`](2026-05-15-binding-generator-local-output-loop-design.md).

The Stage 2 plan is written after Stage 1 lands and uses the real Stage 1 code shape. The SDL3 plan is written later when Phase 5 activates.

## References

Strategy and decision context:

- `docs/binding-autogen/binding-autogen-strategy-brief.md` — accepted strategy brief (revised 2026-05-15 in lockstep with this spec).
- `docs/decisions/2026-05-14-binding-autogen-toolchain.md` — ADR-004 CppAst toolchain decision.
- `docs/decisions/2026-05-05-target-centric-build-host.md` — ADR-002 target-centric build-host architecture; the host pattern this spec inherits.
- `docs/decisions/2026-05-12-build-host-data-layer.md` — ADR-003 contract-centric data layer; cross-cutting validator pattern this spec extends.
- `docs/decisions/2026-05-05-d3seg-and-package-first.md` — ADR-001 D-3seg versioning + package-first contract.
- `docs/binding-autogen/research/binding-autogen-feasibility.md` — 11 emit rules + multi-platform parsing baseline.
- `docs/binding-autogen/research/binding-autogen-spike-findings.md` — ClangSharp + CppAst spikes + platform-pass spike (§8.7).
- `docs/binding-autogen/research/binding-autogen-approaches.md` — toolchain survey and ppy/Alimer/Silk.NET comparison.
- `docs/phases/phase-4-binding-autogen.md` — Phase 4 design brief.

Operating rules and knowledge base:

- `AGENTS.md` — operating rules, approval gate, build-host reference pattern, "Pure code stays pure", "Cake nativeness is a hard rule at build boundaries".
- `docs/knowledge-base/extraction-guidelines.md` — collaborator extraction and interface discipline.
- `docs/knowledge-base/testing-guidelines.md` — canonical TUnit/MTP test infrastructure, `FakeCakeWorld`, `TargetTestHost`, `FixtureLoader`, fixture data policy.
- `docs/knowledge-base/release-guardrails.md` — guardrail catalog; binding vcpkg coherence and symbol existence will receive final G-IDs here at Phase 6 catalog refresh.

Header inputs the spec references directly:

- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_system.h`
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_main.h`
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_syswm.h` — note lines 86, 94-95, 105-106, 109-110, 120, 346-348 (forward declarations + 64-byte union size lock).
- `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_platform.h`

External:

- <https://github.com/amerkoleci/Alimer.Bindings.SDL> — CppAst SDL3 core reference for emitter organization and typed-handle shape.
- <https://github.com/ppy/SDL3-CS> — ClangSharp SDL3 reference for preprocessor-macro-driven multi-pass orchestration (Dockerfile + `generate_bindings.py`).
- Historical local CppAst platform-pass spike — Defines/Undefines macro juggling.
