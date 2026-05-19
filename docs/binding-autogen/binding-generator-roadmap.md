# Binding Generator Roadmap

> **Status (2026-05-20):** Canonical forward roadmap for the binding generator. Historical task transcripts, spike audits, and completed superpowers plans are intentionally removed from the active roadmap. Code is the current-status authority; this file tracks what remains.

## Goal

Finish the generated SDL2.Core surface, replace the transitional SDL2-CS compile path, generate the remaining SDL2 satellites, then extend the same architecture to SDL3 after SDL2's public release path is real.

The generator constitution is [`binding-generator-constitution.md`](binding-generator-constitution.md). It defines what generated output is allowed to mean. This roadmap defines what still needs to be built.

## Current Baseline

The following baseline is already in the repo:

- Cake-hosted `GenerateBindings` target under `build/_build/Targets/GenerateBindings/`.
- Manifest-driven `binding_generation` configuration in `build/manifest.json` schema `2.2`.
- SDL2.Core enabled; SDL2_image, SDL2_mixer, SDL2_ttf, and SDL2_gfx placeholders disabled for Stage 2.
- Linux-canonical generation through `tools.cs generate-bindings` and the pinned Linux builder container.
- SDL2.Core platform catalog: Neutral, WindowsDesktop, WinRT, GDK, Linux, MacOS, IOS, Android.
- Semantic model categories: functions, structs, enums, constants, handles, callbacks, and macro report evidence.
- Source-first macro pipeline with helper-candidate reporting, expression evaluation, manual policy handling, duplicate merge, and parse-view evidence.
- Internal raw ABI command emission with `SDLNative` identity, class-level `unsafe`, platform attribution, and modern C integer guards.
- Generated constants, enums, handles, structs, callbacks, and internal commands emitted to `artifacts/generated-bindings-preview/sdl2-core/`.
- Fixture-backed fixes for the SDL2.Core ABI-sensitive blockers listed in the constitution.
- Compile-check project at `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj` for generated preview output across `LibraryTargetFrameworks`.

The current major gap: generated SDL2.Core is ABI-shaped internally, but the public callable wrapper/friendly layers and production source flip are not complete.

## Stage 1: SDL2.Core Public-Surface Readiness

Stage 1 ends when `src/SDL2.Core` can stop compiling `external/sdl2-cs/src/SDL2.cs` and consume committed generated source for the Core package.

### 1. Public Typed Low-Level Function Wrappers

Add generated public methods on the manifest-driven public class (`SDL2.SDL`) that call internal raw ABI methods.

Requirements:

- Public methods are not extern declarations.
- Public methods reuse typed handles, enums, structs, callbacks, and constants emitted by the current semantic model.
- SDL2 bool-like raw `int` returns convert to `bool` where the public method clearly represents a predicate.
- Unsafe pointer overloads remain available in the low-level layer when that is the honest C shape.
- Platform-only methods carry the same platform attribution as the internal raw ABI member.
- Raw `SDLNative` stays internal.

Acceptance evidence:

- Emitter tests prove public methods call the internal raw methods.
- Compile-check proves the generated public layer builds across `LibraryTargetFrameworks`.
- A small generated-output inspection verifies no public `[DllImport]` / `[LibraryImport]` methods leak.

### 2. Friendly Overload Layer

Add generated overloads for common C# usage.

Baseline overload set:

- `string` for UTF-16 caller input encoded to null-terminated UTF-8.
- `ReadOnlySpan<byte>` for pre-encoded UTF-8, appending a null terminator only when needed.
- `ReadOnlySpan<T>` for counted input buffers when SDL does not retain the pointer.
- `Span<T>` for counted output buffers when SDL writes within caller-provided bounds and does not retain the pointer.
- `out T` for required single-element output pointers.
- `ref T` for required single-element in/out pointers.
- Explicit fmt-only helpers for accepted variadic logging/formatting calls.

Non-goals for Stage 1:

- No owner wrapper layer such as `SdlWindow : IDisposable`.
- No public raw namespace/package.
- No `Memory<T>` overloads unless a concrete consumer need appears.
- No managed callback lifetime helper layer beyond preserving the low-level callback identity.

Acceptance evidence:

- Emitter tests cover UTF-8 string/span terminator behavior, out/ref patterns, span eligibility, bool conversion, and fmt-only variadic helpers.
- Compile-check stays green across all current target frameworks.
- Generated docs/reporting make allocations and preformatted variadic behavior visible.

### 3. Generated Output Production Flip

Move SDL2.Core generated output from preview artifacts into the managed project source tree.

Requirements:

- Generate into `src/SDL2.Core/Generated/`.
- Commit generated `.g.cs` files.
- Remove the SDL2.Core production compile include for `external/sdl2-cs/src/SDL2.cs`.
- Keep `external/sdl2-cs` only as a reference oracle until Stage 2 retires remaining production use.
- Ensure the SDK glob or project file includes generated output without special consumer steps.

Acceptance evidence:

- `dotnet build src/SDL2.Core/SDL2.Core.csproj` succeeds.
- `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj` is either repointed to production output or retired if the project build fully replaces its value.
- Generated output has stable LF line endings and deterministic ordering.

### 4. Stamp And Reproducibility Gate

Turn generated output into a reproducible source contract.

Requirements:

- `.generated-stamp` records generator/toolchain version, vcpkg state, SDL library version, header-set fingerprint, header count, and parse views.
- The stamp contains no wall-clock fields.
- A PreFlight validator fails when committed generated output is stale relative to current binding-generation inputs.
- Regenerating from the same inputs produces a clean diff.

Acceptance evidence:

- Unit tests cover stamp load/save and drift detection.
- A local generation run followed by a second generation run is diff-clean.
- PreFlight error text names the family, stale field, and remediation command/workflow.

### 5. SDL2.Core Smoke And Surface Evidence

Prove the generated Core family works as a package-consumed binding.

Requirements:

- Package-consumer smoke exercises at least initialization, quit, error retrieval, window creation/destruction where environment permits, and one callback path.
- Public API snapshot or equivalent surface review pins intentional public shape before first public preview.
- Oracle review records known deltas from SDL2-CS as intentional typed-handle/friendly-overload differences or real bugs.

Acceptance evidence:

- Build-host tests pass.
- Generated source compiles across all target frameworks.
- Package-first smoke passes from local package feed.
- No production source path uses `external/sdl2-cs/src/SDL2.cs` for SDL2.Core.

## Stage 2: SDL_syswm And SDL2 Satellite Sweep

Stage 2 ends when all in-scope SDL2 families generate bindings and `external/sdl2-cs` is no longer a production source dependency.

### 1. `SDL_syswm.h` Full Typed Layout

Emit typed `SDL_SysWMinfo` and `SDL_SysWMmsg` only with platform layout proof.

Requirements:

- Add the minimal forward-declaration stub library for platform handle types used by the SysWM unions.
- Emit explicit-layout union storage with the SDL2 64-byte size lock where required.
- Validate field offsets and sizes for every platform branch in the catalog.
- Keep Stage 1 opaque/quarantine behavior until this proof exists.

### 2. SDL2 Satellite Generation

Enable and generate the remaining SDL2 families one at a time.

Families:

- SDL2.Image
- SDL2.Mixer
- SDL2.Ttf
- SDL2.Gfx
- SDL2.Net after its package family enters `build/manifest.json`

Requirements:

- Satellite outputs emit satellite-owned functions and types only.
- Core-owned `SDL_*` structs, handles, enums, callbacks, and constants are referenced from SDL2.Core, never redeclared.
- Satellite `binding_generation` config moves from placeholder to full config in the same slice that enables the family.
- Each satellite gets package-consumer smoke appropriate to its dependency and environment constraints.

### 3. Duplicate Core-Type Guard

Add a generation validator that fails when a satellite redeclares or degrades a core-owned type.

Requirements:

- Error messages name the satellite family, offending type, source header, and expected core-owned reference.
- Tests cover shared types such as `SDL_Surface`, `SDL_Texture`, `SDL_Renderer`, `SDL_RWops`, `SDL_version`, and `SDL_bool`.

### 4. Symbol-Existence Validation

Validate emitted entry points against harvested native binaries before packaging.

Requirements:

- Runs after Harvest and before Package.
- Uses platform-appropriate export tooling through existing build-host tool wrappers.
- Reports family, RID, native binary, entry point, and generated source location.
- Complements dynapi/name validation; it does not claim signature validation.

### 5. Retire SDL2-CS Production Use

Remove remaining managed project compile includes that point at `external/sdl2-cs/src/`.

Acceptance evidence:

- Every SDL2 managed family builds from generated source.
- Full 7-RID native smoke and package-consumer smoke pass for all generated SDL2 families.
- First public SDL2 preview is AST-generated, package-first, and not shaped by SDL2-CS public API inertia.

## Stage 3: SDL3 Extension

Stage 3 is gated on PD-7 and SDL2 public-release progress. SDL3 vcpkg/native packaging work is substantial and must not block SDL2 v1.0.

Requirements:

- Add SDL3 package families and native build support first.
- Introduce SDL3 generation as a sibling Cake target only when SDL3 becomes a real second consumer.
- Promote shared generator code out of SDL2 target-local folders only when ADR-002 reuse criteria are met.
- Encode SDL3-specific ABI rules instead of copying SDL2 behavior:
  - 1-byte bool-like values.
  - `SDL_IOStream` replacing `SDL_RWops`.
  - SDL3 platform macro model.
  - SDL3 namespace/library identity.
- Re-evaluate SDL3 TFM support instead of blindly copying SDL2's legacy `netstandard2.0` / `net462` obligations.

Acceptance evidence:

- SDL3 Core, Image, Mixer, and Ttf generated output compiles and packages through the internal feed.
- SDL3 package-consumer smoke proves load and minimal calls per generated family across the supported RID/TFM matrix.
- SDL3-specific ABI decisions are captured in the constitution or a companion ADR before any public SDL3 preview.

## Continuous Maintenance

These tasks apply throughout all stages:

- Keep `binding-generator-constitution.md` updated with every rule change.
- Keep `docs/playbook/binding-generator-maintenance.md` updated when operation procedure changes.
- Keep ADR-004 as the toolchain decision record; only reopen if CppAst maintenance cost becomes a real problem.
- Keep generated output deterministic and package-first.
- Treat peer bindings as evidence, never authority.
- Prefer explicit deferral over fake ABI success.
- Run Slopwatch after code/test/project changes.

## Retired Active References

The following sources were folded into this roadmap and the constitution, then removed from active documentation:

- binding-generator superpowers specs and plans;
- old local-output-loop and stage-plan transcripts;
- semantic pipeline, macro constants, macro taxonomy, and P0 fix implementation plans;
- binding-autogen research/spike notes;
- the temporary translation contract and separate API/strategy briefs.

Git history remains the archive for historical audit detail.
