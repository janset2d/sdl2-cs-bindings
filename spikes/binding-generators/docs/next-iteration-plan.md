# Spike Next-Iteration Plan — ppy-Style ClangSharp Production Prototype

**Date:** 2026-05-21; updated 2026-05-22
**Status:** Active spike plan. Slice 3/4 implementation exists and builds. Windows-local ClangSharp can now use explicit spike-only platform header shims for synthetic Linux/macOS/iOS views, but shim-enabled output is still not final production platform evidence. Do not make a final ClangSharp-vs-CppAst recommendation until comparable CppAst/Alimer-style evidence exists.

## Direction

> **North star: ppy `generate_bindings.py` (~438 LOC).** Our orchestrator (~212 LOC) is a thin subset. The slices below close the gap by adopting ppy's mechanisms wholesale rather than inventing parallel ones.

End-state of the spike prototype:

- One `clangsharp/` folder containing the orchestrator, RSP set, postprocess C# tool, and production-shaped multi-TFM library projects (`Janset.SDL2.Core`, `Janset.SDL2.Image`, …) under a single `.sln`.
- ClangSharp dual codegen (compat + modern) generation → committed `.g.cs` files inside each library project's `Generated/<Codegen>/<Platform>/` tree.
- Microsoft.CodeAnalysis postprocess that:
  - Promotes `[DllImport]` to `[LibraryImport]` for modern-codegen output.
  - Wraps `[SupportedOSPlatform("…")]` attribute usage with `#if NET5_0_OR_GREATER` / `#endif`.
  - (Later slices) Emits public typed wrappers + friendly overloads over the internal raw ABI.
- Multi-OS parse pass mirroring ppy `generate_platform_specific_headers`, adapted for SDL2 host-defined macros (neutral parse → per-platform pass with view defines + cross-contamination undefines + neutral-symbol excludes). Platform attribution is owned by Roslyn `platform-delta`, not ClangSharp `--with-attribute`.
- ppy orchestrator features adapted: per-header `.rsp` lookup, manual-symbol exclusion feedback, sdl.json/dynapi validation pass.

What this prototype does NOT do (out of spike scope; belongs in Roadmap M7+):

- Production source flip into `src/Janset.SDL2.<Family>/Generated/`.
- `.generated-stamp` reproducibility metadata.
- Linux-canonical containerised generation — spike stays Windows-local-first.
- Package smoke against real native packages.

## Slice progress

| Slice | Status | Notes |
| --- | --- | --- |
| 1 — Layout restructure + library projects | ✅ done 2026-05-21 | `clangsharp/` folder + `.slnx` + `Janset.SDL2.Core` and `Janset.SDL2.Image` classlibs (5 TFMs each) + Generated moved into each library's `Generated/{Compat,Modern}/`. Multi-TFM build clean. |
| 2 — Microsoft.CodeAnalysis postprocess: DllImport → LibraryImport | ✅ done 2026-05-21 | `postprocess/Janset.SDL2.PostProcess.csproj` console app + `DllImportToLibraryImportRewriter`. Modern output gets `[LibraryImport]` + `[UnmanagedCallConv]` + `partial`. Includes `StripVarargsRewriter` enforcing Constitution L162-176 `__arglist` rejection — variadic methods drop `...` tail so both Compat and Modern emit fmt-only signatures. Python script orchestrates: regen (compat+modern) → strip-varargs (both) → libraryimport (modern). Multi-TFM build clean. |
| 3 — Multi-OS parse pass | 🔄 partial 2026-05-22 | `SDL_main.h` / `SDL_system.h` platform pass exists. Neutral/platform duplicate removal works. Windows-local non-Windows views still need system-header strategy before platform support is complete. |
| 4 — SupportedOSPlatform `#if NET5_0_OR_GREATER` guards | ✅ folded into Slice 3 2026-05-22 | `PlatformDeltaPostProcessor` adds guarded `using System.Runtime.Versioning;` and guarded `[SupportedOSPlatform(...)]` by `Platforms/<View>` path. |
| 5 — ppy orchestrator feature parity | ⏳ later | Per-header `.rsp`, manual-symbol feedback, dynapi validation. |
| 6+ — Public typed projection + friendly overloads | ⏳ M5/M6 work | Out of current spike scope. |

## Current Evidence Snapshot — 2026-05-22

- Regeneration command: `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`.
- Regeneration result: writes `clangsharp-full.md` with `Platform header shims: enabled` and no empty generated outputs. The shims unblock Windows-local synthetic platform parsing, but they are a spike-only substitute for native Linux/macOS SDK headers.
- Build command: `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release`.
- Build result: Core + Image compile clean across `net462`, `netstandard2.0`, `net8.0`, `net9.0`, `net10.0` with 0 warnings / 0 errors.
- Oracle evidence: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report` writes [`../output/reports/oracle-evidence-clangsharp.md`](../output/reports/oracle-evidence-clangsharp.md). Current snapshot reports SDL2 Core + SDL2 Image surface counts and raw ABI constitution-risk buckets; see the report for full findings.
- Known platform caveat: `--use-platform-header-shims` supplies minimal `endian.h`, `AvailabilityMacros.h`, and `TargetConditionals.h` from `clangsharp/shims/platform-headers/`. Use it for Windows-local spike iteration only; production evidence still needs native Linux/macOS generation.

## Slices


### Slice 1 — Layout restructure and library projects

**Goal:** Move from "two-flavour spike" layout (`clangsharp-style/` + `alimer-style/` + scattered `output/`) to production-shaped layout under `clangsharp/`.

**Target tree:**

```text
spikes/binding-generators/clangsharp/
├── generate_bindings.py                    # moved from clangsharp-style/
├── compare_oracle.py                       # moved from clangsharp-style/
├── rsp/                                    # moved from clangsharp-style/rsp/
│   ├── base.rsp
│   ├── sdl2-core.rsp
│   ├── sdl2-image.rsp
│   └── (Slice 4) per-header .rsp files
├── postprocess/                            # new Microsoft.CodeAnalysis console app
│   ├── Janset.SDL2.PostProcess.csproj
│   ├── Program.cs
│   ├── DllImportToLibraryImportRewriter.cs # Slice 2
│   ├── PlatformAttributeGuardRewriter.cs   # Slice 3
│   └── (Slices 5+) PublicApiEmitter.cs / FriendlyOverloadEmitter.cs
├── src/
│   ├── Janset.SDL2.Core/
│   │   ├── Janset.SDL2.Core.csproj         # multi-TFM, internal namespace, System.Memory polyfill for legacy
│   │   └── Generated/
│   │       ├── Compat/                     # ClangSharp compatible-codegen output
│   │       └── Modern/                     # ClangSharp latest-codegen output (Slice 2: postprocessed to LibraryImport)
│   └── Janset.SDL2.Image/
│       ├── Janset.SDL2.Image.csproj        # ProjectReference Janset.SDL2.Core
│       └── Generated/
│           ├── Compat/
│           └── Modern/
└── Janset.SDL2.ClangSharpSpike.sln         # postprocess + Core + Image
```

**Steps:**

1. `mkdir spikes/binding-generators/clangsharp/`.
2. `git mv` the existing `clangsharp-style/{generate_bindings.py,compare_oracle.py,rsp/}` into `clangsharp/` (preserves history).
3. `dotnet new sln --name Janset.SDL2.ClangSharpSpike --output spikes/binding-generators/clangsharp/`.
4. `dotnet new classlib --name Janset.SDL2.Core --output spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/` — multi-TFM (`net10.0;net9.0;net8.0;netstandard2.0;net462`), `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`, `Compile Include` filtered by `Generated/Compat/` for legacy TFMs and `Generated/Modern/` for modern. `PackageReference System.Memory` for legacy TFMs (Constitution L46).
5. `dotnet new classlib --name Janset.SDL2.Image --output spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/` — same multi-TFM shape, plus `<ProjectReference Include="../Janset.SDL2.Core/Janset.SDL2.Core.csproj" />` so satellite code refers to core types instead of redeclaring them.
6. Move `output/clangsharp/Generated/{Compat,Modern}/core/*.g.cs` → `src/Janset.SDL2.Core/Generated/{Compat,Modern}/`.
7. Move `output/clangsharp/Generated/{Compat,Modern}/image/*.g.cs` → `src/Janset.SDL2.Image/Generated/{Compat,Modern}/`.
8. Update `generate_bindings.py` `output_path_for_header` to write directly into `src/Janset.SDL2.<Family>/Generated/<Codegen>/`.
9. Add both library projects to `Janset.SDL2.ClangSharpSpike.sln` and add the sln to `BindingGeneratorSpikes.slnx` if appropriate, OR retire `BindingGeneratorSpikes.slnx` if the new sln replaces it.
10. Retire `output/clangsharp/` (delete after move is verified). Keep `output/reports/` — those are evidence artifacts.

**Exit evidence:**

- `dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.sln -c Release` succeeds across all 5 TFMs for both Core and Image.
- `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output` regenerates into the new locations.
- `git log --follow` works on the moved files.

### Slice 2 — Microsoft.CodeAnalysis postprocess: `[DllImport]` → `[LibraryImport]`

**Goal:** Modern-codegen output uses `[LibraryImport]` instead of `[DllImport]`. ClangSharp emits only `[DllImport]` (issue #427 still open as of 2026-05); the postprocess closes the gap.

**Steps:**

1. `dotnet new console --name Janset.SDL2.PostProcess --output spikes/binding-generators/clangsharp/postprocess/`.
2. Add `Microsoft.CodeAnalysis.CSharp` + `Microsoft.CodeAnalysis.CSharp.Workspaces` package references (centralised via root `Directory.Packages.props`).
3. Implement `DllImportToLibraryImportRewriter : CSharpSyntaxRewriter`:
   - `VisitMethodDeclaration`: detect `[DllImport(...)]` attribute. Replace with `[LibraryImport(...)]` (drop `CallingConvention` → use `[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]`; drop `ExactSpelling`; keep `EntryPoint`).
   - Add `partial` modifier to method declaration.
   - Drop method body (replace with `;`).
   - `VisitClassDeclaration`: ensure containing class is `partial`.
   - Ensure `using System.Runtime.InteropServices;` is present (and add `using System.Runtime.CompilerServices;` if needed for `UnmanagedCallConvAttribute`).
4. CLI mode: `Janset.SDL2.PostProcess libraryimport --input src/Janset.SDL2.Core/Generated/Modern --output src/Janset.SDL2.Core/Generated/Modern` (in-place transform; preserved by `SyntaxTree.ToFullString()` preserving trivia).
5. Wire into `generate_bindings.py`: after the modern codegen pass for each family, invoke the postprocess CLI on `Generated/Modern/`.

**Exit evidence:**

- Sample modern-codegen `SDL_mouse.g.cs` now contains `[LibraryImport("SDL2", EntryPoint = "SDL_GetMouseState")] [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] internal static partial uint SDL_GetMouseState(int* x, int* y);`.
- Multi-TFM build still succeeds (LibraryImport requires `net7.0+`, but compat-codegen output covers legacy TFMs separately).
- Modern-codegen build target framework gets the source-generated marshalling perf benefit.

### Slice 3 — Multi-OS parse pass

**Goal:** ClangSharp's single neutral parse currently misses platform-only SDL2 functions (Cake oracle has 37 platform-specific functions our output skips). Adopt ppy's `generate_platform_specific_headers` pattern adapted for SDL2 macros, scoped tight by header-level evidence.

**Evidence-based immediate scope (Explore agent scan, 2026-05-21; corrected 2026-05-22):**

The agent grepped all 52 SDL2.Core + SDL2_image headers for platform macros from Cake `PlatformCatalog.AllPlatformMacros`. Result split into three buckets:

| Bucket | Headers | Action |
| --- | --- | --- |
| **Implemented in this slice — public functions differ per platform** | `SDL_main.h` (5 variants: __WIN32__, __WINRT__, __GDK__, __IPHONEOS__, __ANDROID__), `SDL_system.h` (7 variants: same + __LINUX__, __MACOSX__/Metal) | Multi-OS pass mandatory, but Windows-local evidence is still partial until foreign system headers are handled. |
| **Production platform-layout/signature decisions still needed** | `SDL_thread.h`, `SDL_rwops.h`, `SDL_syswm.h`, `SDL_stdinc.h`, `SDL_platform.h`, `begin_code.h` | Broader contract impact: thread signatures, union layout, SysWM layout, platform macros, calling convention/export macros. Do not classify these as solved by the `SDL_main`/`SDL_system` slice. |
| **Skip — platform-neutral** | All other 42 headers | Single neutral parse is enough. |

ppy SDL3 applies `generate_platform_specific_headers` to `SDL_main.h` and `SDL_system.h`, which is the shape this immediate ClangSharp slice follows. SDL2 is rougher than SDL3 because host-defined macros and platform system headers leak into parsing on Windows-local generation.

**Reference:** `references/ppy-SDL3-CS/SDL3-CS/generate_bindings.py` lines 341-365 (`generate_platform_specific_headers`); `build/_build/Targets/GenerateBindings/PlatformViews/PlatformCatalog.cs:18-77` (SDL2 macro/define/undefine set).

**Steps:**

1. Add `SDL2_PLATFORM_VIEWS` and `ALL_PLATFORM_MACROS` constants to `generate_bindings.py`, adapted verbatim from Cake `PlatformCatalog.CreateSdl2Catalog()` and `AllPlatformMacros`. Initial 7 views: `WindowsDesktop` ("windows"), `WinRT` ("windows10.0.10240.0"), `GDK` ("windows"), `Linux` ("linux"), `MacOS` ("macos"), `IOS` ("ios"), `Android` ("android"). Each view carries its define list + `SupportedOSPlatform` string.
2. Implement `generate_platform_specific_headers(family, header, views)` Python function. Per-header flow:
   a. **Neutral pass** (already runs as part of the base pass for every header) — extract emitted SDL_* declaration names from `Generated/<Codegen>/SDL_<header>.g.cs`.
   b. **Per-view pass** for each of the 7 views: invoke ClangSharp with `--define-macro <view.defines>` + cross-contamination `--additional --undefine-macro=<each macro in ALL_PLATFORM_MACROS not in view.defines>` + `--exclude <neutral-symbols>`. Output goes to `Generated/<Codegen>/Platforms/<View>/SDL_<header>.g.cs`.
   c. **Postprocess**: run `platform-delta` over both Compat and Modern output to remove neutral/platform duplicates and add guarded `[SupportedOSPlatform("...")]` attributes based on `Platforms/<View>` path.
3. Wire into `main()`: after the codegen passes complete, for `SDL_main.h` and `SDL_system.h` only, invoke `generate_platform_specific_headers` against both Compat and Modern codegen passes.
3a. Add `--use-platform-header-shims` for Windows-local spike runs. When enabled, ClangSharp receives `clangsharp/shims/platform-headers/` as an extra include directory for missing platform SDK headers. The report records whether shims were enabled.
4. csproj `<Compile Include="Generated/{Compat,Modern}/**/*.cs" />` already globs recursively — picks up `Generated/<Codegen>/Platforms/<View>/*.g.cs` automatically. No csproj change needed.
5. Postprocess pipeline order is `platform-delta` (both codegens) → `strip-varargs` (both codegens) → `libraryimport` (modern only). `libraryimport` must copy only indentation trivia when adding `[UnmanagedCallConv]`; otherwise it duplicates guarded platform attribute trivia.

**Exit evidence:**

- `SDL_AndroidGetActivity` lands in `Platforms/Android/SDL_system.g.cs` with guarded `[SupportedOSPlatform("android")]`; modern output has `[LibraryImport]` + `[UnmanagedCallConv]` without duplicated platform attributes.
- Multi-TFM build is clean across all 5 TFMs because platform attributes are `#if NET5_0_OR_GREATER` guarded.
- Oracle comparison is improved but still incomplete: `compare_oracle.py --approach clangsharp` currently reports 859 Core spike functions, 831/845 dynapi exports emitted, and 8 Cake-oracle functions still missing from the spike output.
- Empty platform output is now a hard generator result: generation exits 4 whenever ClangSharp leaves zero-byte platform files after a fatal parse.
- Windows-local Linux/macOS/iOS views may use spike-only header shims. This is acceptable for local iteration, but the production exit gate still requires native platform generation without these shims.

**Non-goals:**

- Not extending the current ClangSharp implementation beyond `SDL_main` and `SDL_system` until the roadmap decides how to handle `SDL_thread`, `SDL_rwops`, `SDL_syswm`, `SDL_stdinc`, `SDL_platform`, and `begin_code`.
- Not implementing the `--with-attribute` selectivity gating per dynapi visibility — Slice 5 picks that up (depends on per-header dynapi metadata).

### Slice 4 — SupportedOSPlatform `#if NET5_0_OR_GREATER` guard postprocess (folded into Slice 3)

**Status:** Done as part of `PlatformDeltaPostProcessor` on 2026-05-22. Slice 3 no longer asks ClangSharp to emit `[SupportedOSPlatform]`; the Roslyn postprocess adds it and guards it.

**Goal:** `System.Runtime.Versioning.SupportedOSPlatformAttribute` is net5.0+; it doesn't exist on `netstandard2.0` or `net462`. Platform attributes therefore need `#if NET5_0_OR_GREATER` / `#endif` guards. ClangSharp does not emit those directives; postprocess does.

**Cake reference:** `RawAbiCommandEmitter.cs:55-59` already implements exactly this pattern — emits `#if NET5_0_OR_GREATER` around `[SupportedOSPlatform]` attribute assignment. We mirror the shape in the postprocess rewriter.

**Steps:**

1. `PlatformDeltaPostProcessor` collects neutral declarations, removes duplicates from `Platforms/<View>` output, and adds `[SupportedOSPlatform(...)]` to surviving platform-only methods.
2. `PlatformDeltaPostProcessor` adds guarded `using System.Runtime.Versioning;` when it adds platform attributes.
3. Pipeline order in `generate_bindings.py`: regen → `platform-delta` (both codegens) → `strip-varargs` (both codegens) → `libraryimport` (modern only).

**Exit evidence:**

- Sample platform output contains:
  ```csharp
  #if NET5_0_OR_GREATER
      [SupportedOSPlatform("windows")]
  #endif
      [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
      public static extern int SDL_RegisterApp(...);
  ```
- Multi-TFM build clean across all 5 TFMs. netstandard2.0 and net462 no longer fail on `SupportedOSPlatform`.

### Slice 5 — ppy orchestrator feature parity

**Goal:** Close remaining gaps with ppy's `generate_bindings.py`. These are mechanisms our spike skipped but ppy uses on every header.

**ppy features still missing in our orchestrator (after Slices 1–4):**

| ppy function | What it does | Why we need it |
| --- | --- | --- |
| `get_sdl_api_dump` | Calls SDL2's `gendynapi.py --dump` → `sdl.json` with per-function metadata | Authoritative source of expected functions per header |
| `check_generated_functions` | Post-gen validation: each sdl.json function found in generated file? Warn if missing | Catch silent generation drops |
| `get_manually_written_symbols` | Reads companion `.cs` `[Constant]` markers → feeds back to ClangSharp `--exclude` | Avoids duplicate emission when we hand-write a constant in a companion file |
| `get_typedefs` | Reads companion `.cs` `[Typedef] public enum Foo : T;` → feeds back to `--remap Foo=Foo` | Preserves typedef-strengthening companion patterns |
| `run_clangsharp` | Per-header `.rsp` lookup + manual-symbol exclude feedback in one call | The actual orchestration of feedback loop |
| `get_string_returning_functions` | `const char*`-returning functions → `Unsafe_` prefix `--remap` | Hook for future friendly-overload generator (M6) |

**Steps:**

1. **sdl.json source**: SDL2 has a similar mechanism via `gendynapi.pl` producing `SDL2.exports`. Our `compare_oracle.py` already parses the exports file (`external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports`). Decide: either move that parser inline into `generate_bindings.py`, or call SDL2's gendynapi to produce a real `sdl2.json` for parity with ppy.
2. **Per-header `.rsp` lookup**: For header `SDL_pixels.h`, if `rsp/SDL_pixels.rsp` exists, append it after `base.rsp` + `<family>.rsp`. ppy `Header.rsp_files()` pattern.
3. **Manual-symbol exclusion feedback**: Companion `.cs` files (one per header that needs hand-written constants/typedefs) live alongside generated output. Python regex `\[Constant]\s*public (const|static readonly) \w+ (\w+_\w+) = ` (ppy verbatim) extracts symbols → feeds `--exclude`. Typedef regex `\[Typedef]\s*public enum (\w+_\w+)` → `--remap Foo=Foo`.
4. **Post-gen validation**: After each header generation, call equivalent of ppy `check_generated_functions` against the SDL2 dynapi list. Emit `[⚠️ Warning] Function X not found in generated file` warnings to the report.
5. **String-return remap** (hook for M6 friendly overloads): For functions where dynapi `retval` is `const char*` or `char*`, add `--remap Func=Unsafe_Func`. The `Unsafe_` prefix is the contract postprocess-side friendly-overload emitter looks for later.

**Exit evidence:**

- Orchestrator LOC approaches ppy's ~438 (currently ~212).
- Companion `.cs` files for high-value headers (`SDL_pixels`, `SDL_stdinc`, `SDL_init` umbrella) exist and feed back correctly.
- Generation report includes a "Missing from dynapi" section equivalent to ppy's warning output.

### Slice 6+ — Public typed projection and friendly overloads (M5/M6)

Out of scope for this active plan document. Will be added after Slice 5 stabilises.

## Open questions to resolve as slices land

- **LibraryImport on netstandard2.0 / net462?** No — `LibraryImport` is `net7.0+`. Slice 2 postprocess only touches `Generated/Modern/`. Legacy TFMs stay on `[DllImport]` (ClangSharp `compatible-codegen` already emits the right shape).
- **`System.Memory` package for friendly overloads on legacy TFMs?** Yes (Constitution L46). Added in Slice 1 csproj.
- **Roslyn source generator anywhere?** No. Constitution + Roadmap M7 require committed `.g.cs` outputs. Postprocess is a build-time console app, not a Roslyn SG.
- **When does the spike retire?** When Slice 5 completes AND a separate Roadmap M7 plan picks up production flip. The spike's job is to prove the pattern; production flip is its own milestone.

## Cross-references

- [`generator-spike-goals.md`](generator-spike-goals.md) — original charter and recorded decision.
- [`../output/reports/iteration-2-comparison.md`](../output/reports/iteration-2-comparison.md) — decision evidence (function counts, dynapi coherence, multi-TFM trajectory).
- [`../output/reports/clangsharp-failure-buckets.md`](../output/reports/clangsharp-failure-buckets.md) — RSP delta history from the 8 fix iterations.
- [`../../../docs/binding-autogen/binding-generator-constitution.md`](../../../docs/binding-autogen/binding-generator-constitution.md) — Layer Contract L48 (DllImport/LibraryImport split), Evidence Gates L379 (multi-TFM compile), Macro pipeline L324-348.
- [`../../../docs/binding-autogen/binding-generator-roadmap.md`](../../../docs/binding-autogen/binding-generator-roadmap.md) — M4 (multi-TFM backends), M5 (public typed), M6 (friendly overloads), M7 (production flip).
- ppy `references/ppy-SDL3-CS/SDL3-CS/generate_bindings.py` — north star for the orchestrator.
- ppy `references/ppy-SDL3-CS/SDL3-CS.SourceGeneration/FriendlyOverloadGenerator.cs` — reference for postprocess Syntax API patterns (note: ppy uses Roslyn SG; we use standalone console). Different runtime, same API surface.
