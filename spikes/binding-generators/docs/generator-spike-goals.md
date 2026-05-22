# Binding Generator Toolchain Spike — Goals And Recorded Decision

**Date:** 2026-05-21; updated 2026-05-22
**Status:** Spike evidence in progress. ClangSharp remains the active prototype path, but do not make a final ClangSharp-vs-CppAst recommendation until the comparable CppAst/Alimer-style evidence pass is complete. Active plan lives in [`next-iteration-plan.md`](next-iteration-plan.md).

## Original Decision Question (now answered)

> Which approach gives us maintainable SDL2.Core plus SDL2 satellite generation while preserving the output contract we actually want?

**Answer (2026-05-21):** ppy-style ClangSharp orchestrator **plus** a Microsoft.CodeAnalysis postprocess pipeline **plus** production-shaped multi-TFM library projects, all under one solution. See "Decision Evidence" below.

## Output Contract (carried into the prototype)

The grand-plan output contract from [`docs/binding-autogen/binding-generator-constitution.md`](../../../docs/binding-autogen/binding-generator-constitution.md) Layer Contract section is unchanged:

- internal raw ABI surface only; no public raw extern class. Generated methods may remain lexically public inside an internal raw container because they are not effectively public package API;
- public generated handles/enums/structs/callbacks/constants;
- opaque SDL handles as strongly typed `readonly` structs over `nint`, not raw `IntPtr` public API;
- multi-TFM raw ABI path, with `[DllImport]` + `[LibraryImport]` file split per Constitution L48;
- header-based generation with OS-specific parse passes where platform declarations require attribution;
- satellite outputs reference core-owned types instead of redeclaring them;
- generated source compiles across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, and `net462` (Constitution L379).

The prototype must hit all of the above to graduate into Phase 4. The current spike state hits some (internal raw ABI, multi-TFM compile via dual codegen) and explicitly does not yet hit others (LibraryImport split, public typed projection, multi-OS pass) — those are slices 2–5 in the active plan.

## Approaches That Were Compared

| Approach | Reference | Outcome |
| --- | --- | --- |
| Alimer-style CppAst | `amerkoleci/Alimer.Bindings.SDL` | Compile-clean SDL2.Core raw ABI on first try; tight type-mapper (200 LOC). 92.4% dynapi hit. Multi-TFM fix is shallow (Cake's `ModernCIntegerEmissionPolicy` pattern, ~30 LOC). **Retained as reference under `alimer-style/`**, not the chosen path. |
| ppy-style ClangSharp | `ppy/SDL3-CS` | Compile-clean SDL2.Core + SDL2_image after 8 RSP-fix iterations. 98.1% dynapi hit. Multi-TFM solved via dual codegen pass (`compatible-codegen` for legacy, `latest-codegen` for modern) + csproj file filter. **Chosen path.** |

Decision evidence is in [`../output/reports/iteration-2-comparison.md`](../output/reports/iteration-2-comparison.md). The defining factor was multi-TFM coverage — ClangSharp `compatible-codegen` produces netstandard2.0/net462-safe output without owned post-processing, which the spike charter's "Evidence Gate" requires.

## What The Spike Tested

- Full SDL2.Core (51 headers) + SDL2_image (1 header), not a 3-header demo.
- Both approaches against `artifacts/generated-bindings-preview/sdl2-core/` (Cake-generated oracle).
- Both approaches against SDL2's `gendynapi` `SDL2.exports` (845 symbols → ClangSharp emitted 829, Alimer emitted 781).
- Multi-TFM compile across `net10`, `net9`, `net8`, `netstandard2.0`, `net462` (ClangSharp: 0 errors after dual codegen + csproj filter; Alimer: 12 errors all `CLong`/`CULong`).

## What The Spike Did NOT Cover (still open work, tracked in active plan)

These are tracked in [`next-iteration-plan.md`](next-iteration-plan.md). Status as of 2026-05-21:

- ✅ **Library project layout** (Slice 1, done) — `Janset.SDL2.Core` / `Janset.SDL2.Image` `.csproj`s with `ProjectReference`, multi-TFM, System.Memory polyfill, all under `Janset.SDL2.ClangSharpSpike.slnx`.
- ✅ **`[LibraryImport]` emission on modern TFMs** (Slice 2, done) — Microsoft.CodeAnalysis postprocess (`Janset.SDL2.PostProcess.csproj`) rewrites `[DllImport]` → `[LibraryImport]` + `[UnmanagedCallConv]` + `partial` for the modern codegen output. Compat output stays on `[DllImport]` for legacy TFMs. Includes `StripVarargsRewriter` enforcing Constitution L162-176 `__arglist` rejection.
- 🔄 **Multi-OS parse pass** (Slice 3, partial) — ppy `generate_platform_specific_headers` adapt exists for `SDL_main.h` and `SDL_system.h`; `PlatformDeltaPostProcessor` removes duplicate neutral/platform declarations and adds platform attributes. Windows-local non-Windows views can use explicit spike-only platform header shims, but final platform evidence still needs native Linux/macOS generation.
- ✅ **`[SupportedOSPlatform]` `#if NET5_0_OR_GREATER` guards** (Slice 4, folded into Slice 3) — `PlatformDeltaPostProcessor` wraps platform attributes and `System.Runtime.Versioning` using so all five TFMs compile.
- ⏳ **ppy orchestrator feature parity** (Slice 5) — per-header `.rsp` lookup, manual-symbol exclusion feedback, dynapi validation pass.
- ⏳ **Public typed low-level API** (Slice 6+, Roadmap M5).
- ⏳ **Friendly overloads** (Slice 6+, Roadmap M6 — `string?`, `Span<T>`, `out T`, `ref T`).

Latest ClangSharp evidence (2026-05-22): Core + Image build clean across `net462`, `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`; shim-enabled oracle comparison reports 859 Core spike functions, 831/845 dynapi exports emitted, and 8 Cake-oracle functions still missing from the spike output. The generator now fails with exit code 4 when ClangSharp leaves empty platform output files, and `--use-platform-header-shims` exists only to unblock Windows-local spike iteration around missing foreign SDK headers.

## Hardcoding Rules (unchanged — these survived the spike intact)

Hardcoding is acceptable when it is explicit SDL/C ABI policy. It is not acceptable when the same fact is hidden in multiple places.

**Good code-owned policy:**

- C primitive and SDL typedef width mapping (Cake's `TypeMappingPolicy.ExplicitTypedefMap` is the reference shape).
- SDL2 `SDL_bool` shape (int-backed; `--with-type SDL_bool=int` in `base.rsp`).
- C `long` / `unsigned long` handling (`CLong` / `CULong` on NET6_0_OR_GREATER, guarded).
- `wchar_t`, `FILE`, `va_list`, Vulkan, D3D, and other external ABI handling.
- C# keyword escaping.
- Macro taxonomy (compiler-control, printf-format, helper-candidate, build-time).
- SDL-specific substitutions such as `SDL_GUID` → `Guid`.

**Good config-owned facts (RSP files + manifest):**

- library/family identity;
- input headers;
- output namespace and public class name;
- native library import name;
- owned prefixes or explicit owned symbols;
- excluded/deferred declarations;
- manually required declarations or constants that come from excluded umbrella headers.

**Bad patterns:**

- `SDL2` library identity hardcoded in an emitter while also present in config;
- `/SDL2/` path checks scattered through unrelated policies;
- `SDL_` prefix rules duplicated across macro, declaration, and type ownership code;
- turning manifest JSON into a mini ABI policy language.

## Methodology Rules (still active)

- Adapt ppy's `generate_bindings.py` patterns wholesale — it is the **north star** for the SDL2 orchestrator. Do not invent parallel mechanisms.
- Keep the Python orchestrator readable: loops, `if`, `switch`, no DI/profile-registry/planner frameworks.
- Keep Microsoft.CodeAnalysis postprocess as a standalone console app, not a Roslyn source generator. Constitution requires committed output with reproducibility stamp.
- Reference repositories under `references/` are evidence, never authority. Constitution + Roadmap M-series milestones are authority.
- Move from spike layout to production-shaped layout as soon as it earns it — currently planned in Slice 1 of the active plan.

## Promotion Path

Slices 1–5 in [`next-iteration-plan.md`](next-iteration-plan.md) move the prototype from compile-clean SDL2.Core + SDL2_image (current state) to a production-shape candidate. Production flip into `src/Janset.SDL2.<Family>/Generated/` is **not** part of the spike — that's Roadmap M7. Spike work concludes when the prototype has: multi-TFM raw ABI with `LibraryImport` split, multi-OS pass, public typed projection over the raw ABI, and friendly overloads. At that point Phase 4 production work begins and the spike directory can retire.

## Cross-References

- [`docs/binding-autogen/binding-generator-constitution.md`](../../../docs/binding-autogen/binding-generator-constitution.md) — canonical ABI/API law.
- [`docs/binding-autogen/binding-generator-roadmap.md`](../../../docs/binding-autogen/binding-generator-roadmap.md) — Milestone M4 (multi-TFM backends), M5 (public typed), M6 (friendly overloads), M7 (production flip).
- [`docs/decisions/2026-05-14-binding-autogen-toolchain.md`](../../../docs/decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 toolchain decision (CppAst). This spike does not invalidate ADR-004; production Cake `GenerateBindings` continues to use CppAst until Phase 4 flip explicitly approves the prototype. ADR amendment or supersession is a separate work item.
