# Binding Generator Spike Iteration 2 Comparison

**Date:** 2026-05-21
**Host:** Windows
**Triplet:** x64-windows-hybrid
**Cake oracle:** `artifacts/generated-bindings-preview/sdl2-core/`
**Dynapi exports:** `external/vcpkg/buildtrees/sdl2/src/se-2.32.10-3b143ac573.clean/src/dynapi/SDL2.exports` (845 entries)

## Scope Rule

Bootstrap slices were skipped — both approaches went straight to full SDL2.Core (51 headers) + SDL2_image (1 header). Per the spike charter (`docs/generator-spike-goals.md` §Methodology), bootstrap is bring-up evidence only; once an approach can attempt the full scope cleanly, the toolchain decision is informed by full-scope numbers.

Both approaches now have artifact-free full-scope evidence: ClangSharp through eight RSP-fix iterations driven by the compile-test trajectory in `clangsharp-failure-buckets.md`; Alimer-style on the first try by following Cake's `TypeMappingPolicy.cs` and `CppAstParseRunner.cs` reference shapes.

## Approach Snapshot

| Aspect | ClangSharp (ppy-style) | Alimer-style CppAst |
| --- | --- | --- |
| Owned code | 1 generator wrapper script (~190 LOC Python), 3 RSP files (~140 LOC) | 5 small .cs files (~430 LOC C#) |
| External tool | `ClangSharpPInvokeGenerator` 17.0.1 (dotnet tool) | CppAst 0.24.0 + libclang.runtime.win-x64 20.1.2 |
| Categories emitted | Functions, enums, structs (POD + opaque), constants, inline `delegate*` callbacks | Functions only |
| Iterations to first compile-clean output | 8 (RSP-delta cycles documented in `clangsharp-failure-buckets.md`) | 1 |
| Skip discipline | Symbol-list `--exclude` blocks | `SkipReason` enum + per-function audit |
| Customisation surface | Per-family RSP + cross-cutting RSP | Generator code (type mapper, emitter, parser-options) |

## Full-Scope Results — Bindings Emitted

| Metric | ClangSharp | Alimer | Cake Oracle |
| --- | ---: | ---: | ---: |
| Functions | 889 | 846 | 866 |
| Enums | 56 | 0 | 56 |
| POD structs | 97 | 0 | 71 |
| Opaque handles | 22 | 0 | 16 |
| Constants | 151 | 0 | 104 |
| Callbacks | 54 (inline) | 0 | 19 (named) |
| **Output csproj build** | **PASS** (0 warn, 0 err) | **PASS** (0 warn, 0 err) | n/a |

Alimer's 0s in non-function rows are scope (Iteration 2 plan Task 4 limited it to "minimal internal raw ABI emission"), not failure. The mapper, struct-by-value handling, enum/callback emitters, and macro pipeline are not implemented; closing the gap would mean adding the structural counterparts of Cake's `BindingEnumTranslator`, `BindingStructTranslator`, `BindingCallbackTranslator`, `BindingHandleTranslator`, plus a macro pipeline.

## Full-Scope Results — Dynapi Coherence

| Approach | Dynapi exports | Emitted ∩ dynapi | Missing from spike | Extra (not in dynapi) |
| --- | ---: | ---: | ---: | ---: |
| **ClangSharp** | 845 | **829 (98.1%)** | 16 | 60 (59 IMG_*, 1 SDL_main) |
| **Alimer** | 845 | 781 (92.4%) | 64 | 65 (51 IMG_*, 14 inline helpers Cake also skips) |

ClangSharp's 16 missing match exactly the manifest-deferred / constitution-deferred / SDL.h umbrella set the Cake oracle also defers — see `oracle-comparison-clangsharp.md` §Dynapi Coherence.

Alimer's 64 missing fall into three buckets:
- **Function pointer parameters** (~30): `SDL_AddEventWatch`, `SDL_AddHintCallback`, `SDL_DelEventWatch`, `SDL_FilterEvents`, etc. — the mapper returns `SkipReason.FunctionPointer` because `delegate* unmanaged[Cdecl]<...>` emission is not implemented. Cake handles this via `NativeTypeClassifier.FunctionPointer` + `BindingCallbackTranslator`.
- **Pointer-to-pointer / void** parameters (~10): `SDL_AtomicCASPtr`, `SDL_AtomicGetPtr`, `SDL_AtomicSetPtr` — `void**` → `nint*` not yet routed.
- **Explicitly excluded by spike** (~8): `SDL_CreateThread`, `SDL_CreateThreadWithStackSize`, `SDL_DYNAPI_entry`, `SDL_RWFromFP`, the four variadic functions — same deferral set as ClangSharp.

The first two buckets are real Alimer-side work to reach ClangSharp parity; the third is a mutual deferral.

## Multi-TFM Compatibility (compile-only, no runtime check)

Constitution §"Layer Contract" L48 says raw ABI backends may split into `DllImport` and `LibraryImport` generated files but both consume the same semantic/projection truth, and §"Evidence Gates" L379 requires generated preview to compile across `net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, and `net462`. Both spike outputs were originally built only against `net10.0`. A multi-TFM rebuild surfaces the real cost:

| Approach | net10 / net9 / net8 | netstandard2.0 | net462 | Total errors | Failure family |
| --- | :---: | :---: | :---: | ---: | --- |
| Cake oracle (reference) | clean | clean | clean | 0 | `RawAbiCommandEmitter.cs:49-59` emits `#if NET6_0_OR_GREATER` around CLong/CULong members and `#if NET5_0_OR_GREATER` around `[SupportedOSPlatform]` attributes — single output file, file-level TFM guards. |
| ClangSharp spike | clean | fail | fail | **474** | `InlineArray` / `InlineArrayAttribute` (C# 12, net8+ only), `ReadOnlySpan<>` (no netstandard2.0/net462 polyfill in spike), implicit modern-C# language features used inline in struct/method bodies. No mechanism to gate them — they're embedded in struct definitions and constant initializers. |
| Alimer-style spike | clean | fail | fail | **12** | Only `CLong` / `CULong` (`System.Runtime.InteropServices.CLong` is `NET6_0_OR_GREATER`-only). Fix is a one-line emitter change: copy `ModernCIntegerEmissionPolicy.cs:9-19` (`UsesModernCInteger`) into `Sdl2RawAbiEmitter.cs` and wrap the offending `[DllImport]` with `#if NET6_0_OR_GREATER` / `#endif`, same as the oracle. |

### Why the gap is structural, not incremental

The ClangSharp output bakes modern-C# constructs into shapes that don't have a stand-alone declaration to gate:

- `[InlineArray(2)] public partial struct _padding_e__FixedBuffer { public byte e0; }` — fixed-buffer nested struct inside a parent struct. Wrapping just the attribute with `#if NET8_0_OR_GREATER` leaves the parent struct without a member of the right size on old TFMs.
- `public static ReadOnlySpan<byte> SDL_FILE => "..."u8;` — UTF-8 literal expression, no equivalent on TFMs without `ReadOnlySpan<T>`.
- `delegate* unmanaged[Cdecl]<...>` field types inside structs — function pointer types are net5+ language features, no fallback on old TFMs.

Gating these with `#if` requires either (a) deleting the construct entirely on old TFMs (drops API), or (b) emitting an alternate shape inside `#else` (requires a parallel emitter the spike doesn't have, and ClangSharp doesn't surface a hook to inject one). ppy SDL3-CS sidesteps the question by targeting modern TFMs only.

The Alimer-style gap is shallow because the offending shapes (`CLong`, `CULong`) appear only at signature level, on a small, enumerable set of functions. Wrapping those with `#if NET6_0_OR_GREATER` is the exact pattern Cake already validates (`RawAbiCommandEmitter.cs:50-53`). The mapper would also need a fallback signature for old TFMs (e.g., remap C `long` to `int` on Windows LLP64 and `long` on Unix LP64 via a per-platform synthetic Cake-mirroring policy) for true API parity, but compile-clean is reachable today with the guard alone.

## Multi-OS Pass (platform parse views)

| Approach | Mechanism | What it produces |
| --- | --- | --- |
| Cake oracle | `PlatformCatalog` declares `Neutral`, `WindowsDesktop`, `WinRT`, `GDK`, `Linux`, `MacOS`, `IOS`, `Android` views. Each header parsed once per applicable view with platform defines. `RawAbiCommandEmitter` writes `Platform/<View>/Commands.g.cs`; platform-only functions get `[SupportedOSPlatform("…")]` inside `#if NET5_0_OR_GREATER`. | `artifacts/generated-bindings-preview/sdl2-core/Platform/{Neutral,WindowsDesktop,Linux,MacOS,IOS,Android,WinRT,GDK}/Commands.g.cs` — 8 files, attribution preserved. |
| ClangSharp ppy-style | `generate_bindings.py:341-365` `generate_platform_specific_headers`: parse once neutral, then per-platform passes with `--define-macro PLATFORM` + `--exclude <neutral-symbols>` + `--with-attribute "<fn>=SupportedOSPlatform(\"…\")"`. Writes `<header>.<Suffix>.g.cs` files. Reserved for `SDL_main` and `SDL_system` in ppy SDL3-CS. | Per-header platform-suffixed files (e.g., `SDL_system.Linux.g.cs`, `SDL_system.Windows.g.cs`). Each file unconditionally uses `[SupportedOSPlatform]` without a TFM guard. |
| Alimer-style spike (current) | None. Single Windows-local parse view only. | Single `Sdl2Native.g.cs` / `Sdl2ImageNative.g.cs`. |

Both ClangSharp and Alimer can implement multi-OS, and both have a clear template — ClangSharp via ppy's `generate_platform_specific_headers`, Alimer via Cake's `PlatformCatalog` + per-view emit loop. The spike just did not exercise it because the iteration scope was Windows-local-debugging-first.

The multi-OS work is independent of the multi-TFM work above. The two combine: a platform-attributed function that also uses `CLong` would emit both `#if NET6_0_OR_GREATER` (for CLong) and `#if NET5_0_OR_GREATER` (for `SupportedOSPlatform`) on the oracle today.

## Policy Duplication Observed

| Approach | Duplicated Fact | Location | Action |
| --- | --- | --- | --- |
| ClangSharp | `SDL_DECLSPEC=` parse define | `base.rsp:10` + `manifest.json:190` (Cake) | None — manifest is for Cake, spike base.rsp is for ClangSharp. Same fact intentionally appears in both because the two run independently. |
| ClangSharp | `-U__has_builtin` clang arg | `base.rsp:38` + `manifest.json:201` (Cake) | Same as above. |
| ClangSharp | Deferred-declaration list (`SDL_RWFromFP`, `SDL_LogMessageV`, varargs family) | `sdl2-core.rsp:33-40` + `manifest.json:245-258` (Cake) | Same as above. Both lists trace to constitution §"Current accepted deferrals" as the single source of truth. |
| Alimer | Type-width mapping (`Sint8=sbyte`, `SDL_bool=int`, etc.) | `Sdl2TypeNameMapper.cs:18-32` + `TypeMappingPolicy.cs:46-60` (Cake) | None — spike intentionally copies the table verbatim per spike charter's hardcoding rules ("C primitive and SDL typedef width mapping" is good code-owned policy). |
| Alimer | C-long → CLong / CULong | `Sdl2TypeNameMapper.cs:122-123` + `TypeMappingPolicy.cs:153-154` (Cake) | Same as above. |

No silent JSON knob duplicates ABI behaviour. Both spikes keep policy where it belongs.

## Hardcoded Policy Inventory — Surface Comparison

| Policy | ClangSharp location | Alimer location | Cake location |
| --- | --- | --- | --- |
| C primitive width | (none — ClangSharp built-in) | `Sdl2TypeNameMapper.MapPrimitive` (~20 LOC) | `TypeMappingPolicy.MapPrimitive` (~20 LOC) |
| SDL typedef width | `base.rsp` `--remap` block (12 lines) | `Sdl2TypeNameMapper.ExplicitTypedefMap` (~15 LOC) | `TypeMappingPolicy.ExplicitTypedefMap` (~15 LOC) |
| `SDL_bool` enum shape | `base.rsp` `--with-type SDL_bool=int` | `Sdl2TypeNameMapper.ExplicitTypedefMap["SDL_bool"]` | `TypeMappingPolicy.cs:57` + `BindingEnumTranslator.cs:42-44` |
| Opaque-handle pointer | `void*=nint` remap covers most; SDL_*-prefix nint mapping implicit in ClangSharp typedef behaviour | `Sdl2TypeNameMapper.MapPointer` SDL_ prefix branch | `TypeMappingPolicy.MapPointer` + `NativeTypeClassifier` opaque path |
| Compiler-control macro neutralisers | `base.rsp` `--define-macro SDL_SLOW_*` etc. (4 lines) | `CppAstParseRunner.cs` `options.Defines.Add` block (~10 LOC) | manifest `parse_defines` (10 lines) |
| `__has_builtin` undef | `base.rsp` `--additional --undefine-macro=__has_builtin` | `CppAstParseRunner.cs` `options.AdditionalArguments.Add("-U__has_builtin")` | manifest `clang_args` |
| Deferred / excluded functions | `sdl2-core.rsp` `--exclude` block (~60 names) | `Program.cs` `coreExclusions` array (8 names) | manifest `deferred_declarations` + `KnownUnsupportedDeclarationPolicy` + `MacroApiPolicy` |
| Macro classification (compiler-control, printf, helper-candidate) | none — ClangSharp emits everything blindly | none — Alimer doesn't emit macros yet | `MacroApiPolicy.cs` (~110 LOC) |

ClangSharp inverts the cost: every problematic macro/symbol must be enumerated in an `--exclude` list. The Alimer-style and Cake approaches let the parser see everything and have the policy reject things by shape rather than by name.

## Decision Signal

Both approaches reach 0-error compile-clean output on `net10.0`. They diverge sharply when measured against the constitution §"Evidence Gates" L379 multi-TFM requirement (`net10.0`, `net9.0`, `net8.0`, `netstandard2.0`, `net462`).

**ClangSharp ppy-style — strengths**
- Five-category emission (functions, enums, structs, constants, callbacks) works out of the box.
- Dynapi coherence is 98.1% out of the box.
- Owned code is tiny: one Python orchestrator, three RSP files.
- Aligns with ppy SDL3-CS conventions, so SDL3 work can borrow the same machinery (`generate_bindings.py` adaptation).

**ClangSharp ppy-style — weaknesses**
- **Multi-TFM gap is structural, not incremental** (see §Multi-TFM Compatibility above). The output bakes `InlineArray`, `ReadOnlySpan<>`, `delegate*` directly into struct/method bodies. There is no hook to gate these constructs with `#if`; closing the gap requires either dropping old TFMs (ppy's choice — net8+ only) or layering a post-processing pass the spike does not have.
- Eight RSP-fix iterations required (`__has_builtin`, `SDL_bool=int` remap-vs-with-type, `_iobuf*`, `HWND__*`, `IMG_SetError`, `_beginthreadex`/`_endthreadex`, etc.). Each iteration needed a build + read-error + edit cycle.
- External tool dependency (`ClangSharpPInvokeGenerator` 17.0.1 .NET tool) — version drift requires re-running validation.
- Exclude lists grow over time; every new SDL2 patch release may add new macro shapes that need new excludes.
- Some emitted output is misleading (e.g. `SDL_LogMessage(..., __arglist)` — variadic with `__arglist` keyword that won't compile on every target framework).
- Constant emission is undisciplined — 47 more constants than Cake oracle, many of which Cake's `MacroValueClassifier` correctly classifies as NonApi (compiler-control, printf format, etc.). The spike `--exclude` list would have to grow significantly to clean this up to oracle-parity.
- Multi-OS pass is implemented in ppy SDL3-CS but **does not interleave with multi-TFM** — `[SupportedOSPlatform]` attribute is emitted unconditionally, no `#if NET5_0_OR_GREATER` guard.

**Alimer-style CppAst — strengths**
- One-shot compile-clean output on `net10.0`, no fix iterations.
- **Multi-TFM gap is shallow** — only `CLong`/`CULong` signature-level (12 errors). Closing it is a copy of `ModernCIntegerEmissionPolicy.cs:9-19` into `Sdl2RawAbiEmitter.cs` plus a `#if NET6_0_OR_GREATER` wrap, both already validated in the Cake oracle.
- Skip mechanism is enforceable — the mapper returns a `SkipReason` rather than emitting broken code. Audit is per-function, not per-error-message.
- Generator is plain readable C#, no external tool versioning.
- Reuses the same type-mapping decisions Cake already validated against the full SDL2 ABI; the spike type mapper is a 200-LOC direct port.
- Naturally aligns with the constitution's "Layer Contract" — the emitter is forced to refuse rather than emit success-shaped lies.
- Multi-OS pass is reachable by mirroring `PlatformCatalog` + per-view emit loop; the existing emitter already has the right shape to add platform attribution behind `#if NET5_0_OR_GREATER`.

**Alimer-style CppAst — weaknesses**
- Only functions today. Enum, struct, constant, callback emitters are unimplemented work. Each adds a Cake-shaped translator (`BindingEnumTranslator` 142 LOC, `BindingStructTranslator`, `BindingCallbackTranslator`, etc.).
- Dynapi coherence at 92.4% — 48 function pointer / pointer-to-pointer parameter cases would need real emission rather than skip.
- Reaching Cake parity means re-implementing roughly Cake's `ModelBuilding/` ~30-file pipeline. The spike charter calls this out as a rejection criterion ("recreates a custom ClangSharp-sized semantic compiler") — but the multi-TFM and multi-OS findings reframe it: the missing files are exactly the ones with TFM-aware emission baked in. Cake's machinery is large because it has to be; the spike is small because it skipped the constructs that need that machinery.

## Recorded Decision (2026-05-21)

**Direction:** ppy-style ClangSharp orchestrator (Python) **+** Microsoft.CodeAnalysis post-processor (C# console app) **+** production-shaped multi-TFM library projects (`Janset.SDL2.Core`, `Janset.SDL2.Image`, …) all under one `.sln` rooted at `spikes/binding-generators/clangsharp/`.

**Decisive factors:**

1. **Multi-TFM is reachable in a single Python flag, not architectural rework.** A follow-up trial after the comparison table above (recorded 2026-05-21) ran ClangSharp with `--config compatible-codegen` (netstandard2.0/net462-safe output) **and** `--config latest-codegen` (modern output) in dual passes, then split them with csproj `<Compile Include Condition>`. Result: **0 errors across all 5 TFMs**. The earlier "474 errors structural gap" framing assumed a single codegen pass — that framing was incomplete. ClangSharp's `compatible-codegen` is exactly the knob the Evidence Gate needed.
2. **Dynapi coherence 98.1%** — emits 829 of 845 dynapi exports; the missing 16 are all explicit manifest deferrals, SDL.h umbrella required-functions, or variadic accepted deferrals.
3. **Owned generator surface stays small.** ~212 LOC Python + 3 RSP files (will grow toward ppy's ~438 LOC as we adopt missing ppy patterns). Cake's `ModelBuilding/` pipeline is ~30 files and ~3000 LOC achieving the same target.
4. **Microsoft.CodeAnalysis post-processor covers the gaps ClangSharp doesn't.** Issue #427 (LibraryImport emission) is still open in ClangSharp; we close it with a SyntaxRewriter that transforms compat output into LibraryImport variants. `[SupportedOSPlatform]` attribute-with-`#if`-guard pattern (Cake `RawAbiCommandEmitter.cs:55-59`) is the same Rewriter approach.
5. **Alimer-style scaffold retained as reference** under `alimer-style/`, not the chosen path. Its CppAst patterns may be useful for fallback if ClangSharp hits a future wall, but the multi-TFM finding tipped the scale to ClangSharp.

**Prototype status as of 2026-05-21 (slice progress):**

- ADR-004 selects CppAst for the production Cake `GenerateBindings`. The spike does not invalidate ADR-004; production flip into `src/` is Roadmap M7 work and requires a separate ADR amendment or supersession.
- ✅ **Library project layout** (Slice 1, done) — `Janset.SDL2.Core` + `Janset.SDL2.Image` multi-TFM classlibs under `clangsharp/Janset.SDL2.ClangSharpSpike.slnx`.
- ✅ **LibraryImport split** (Slice 2, done, Constitution L48) — Microsoft.CodeAnalysis postprocess rewrites `[DllImport]` → `[LibraryImport]` for modern codegen; Compat stays on `[DllImport]` for legacy TFMs. Includes `StripVarargsRewriter` enforcing Constitution L162-176 `__arglist` rejection.
- 🔄 **Multi-OS pass** (Slice 3, active) — Explore-agent evidence narrowed scope to **just `SDL_main.h` and `SDL_system.h`** (matches ppy SDL3 minimal pattern). Cake `PlatformCatalog` provides the 7-view SDL2 macro/define/undefine map.
- ⏳ **`[SupportedOSPlatform]` `#if NET5_0_OR_GREATER` guards** (Slice 4) — postprocess rewriter for compat-codegen output.
- ⏳ **ppy orchestrator feature parity** (Slice 5) — per-header `.rsp` lookup, manual-symbol feedback, dynapi validation.
- ⏳ **Public typed projection** (Slice 6+, Roadmap M5) and **friendly overloads** (Slice 6+, Roadmap M6).

**Slice plan tracking these open items lives in [`../../docs/next-iteration-plan.md`](../../docs/next-iteration-plan.md).**

## Promotion path

Spike concludes when:

1. ✅ Layout sits under `spikes/binding-generators/clangsharp/` with library projects + postprocess sln. (Slice 1, done 2026-05-21)
2. ✅ Modern-codegen output uses `[LibraryImport]` via postprocess. (Slice 2, done 2026-05-21)
3. 🔄 Multi-OS pass emits per-platform output for `SDL_main.h`/`SDL_system.h` (active). (Slice 3)
4. ⏳ `[SupportedOSPlatform]` attributes wrapped in `#if NET5_0_OR_GREATER` for compat compatibility. (Slice 4)
5. ⏳ Orchestrator adopts ppy's missing patterns (per-header `.rsp`, manual-symbol feedback, dynapi validation). (Slice 5)
6. ⏳ Public typed projection + friendly overloads proven end-to-end. (Slices 6+)

At that point Roadmap M7 (production flip) picks up the prototype and ADR-004 is amended or superseded with explicit ClangSharp/Microsoft.CodeAnalysis rationale.
