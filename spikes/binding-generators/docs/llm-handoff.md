# LLM-to-LLM Handoff — Janset.SDL2 Binding Generator Spike

**Date:** 2026-05-21; updated 2026-05-24 (Priority C closure)
**Audience:** The next LLM picking up this spike. Read this **before** writing code or making decisions. The previous LLM hit a concrete sticking point and recorded the full context here so you don't redo work or relitigate decisions.

## TL;DR — 60-second status

- **Project**: ClangSharp-based SDL2 binding generator spike under `spikes/binding-generators/clangsharp/`. Ultimately produces multi-TFM, multi-OS, source-generated `Janset.SDL2.Core` + `Janset.SDL2.Image` library packages.
- **Toolchain evidence**: ppy-style ClangSharp orchestrator (Python) + Microsoft.CodeAnalysis postprocess (C# console app) is the active evidence path. Do **not** relitigate ClangSharp vs CppAst yet; finish the ClangSharp evidence, then build comparable CppAst/Alimer-style evidence before recommending direction.
- **Priority C semantic-ABI: CLOSED 2026-05-24.** All six known platform-sensitive ABI risks (R1 `wchar_t*`, R2 C `long`/`unsigned long`, R3 `SDL_RWops`, R4 `SDL_SysWMinfo`, R5 `SDL_SysWMmsg`, R6 opaque-handle tag leak) resolved; six oracle gap categories at 0 findings. Full evidence + verification table in [`priority-c-closure-summary.md`](priority-c-closure-summary.md) (commit `e62bf92`). Cross-assembly Pattern B contract codified via `[assembly: DisableRuntimeMarshalling]` in Core + Image (commit `4b87037`); Constitution policy at `d0016de`. SDL_GUID → System.Guid substitution + Foreign Type Boundary Policy + BCL-Replaceable Helper Exclusion Policy all landed in scope.
- **Next scope**: **Layer 2 typed low-level public API slice** (Constitution Layer Contract L34-50; Roadmap M5). The Layer 1 raw ABI surface is now stable enough — handle shape, scalar widths, foreign-type boundary all settled — that the typed `SDL2.SDL` projection can be built on top of it without churn. Slice 5 (ppy orchestrator feature parity) remains deferred.
- **Branch state**: 35+ commits ahead of `origin/spike/binding-autogen-sdl2-gfx`; push gate pending Plan Task 19 (final code review + finishing branch decision per AGENTS.md §Approval Gate). Branch is `spike/binding-autogen-sdl2-gfx`.

If you are starting fresh, skip to §"Reading order".

## Reading order

| # | Document | Why |
| --- | --- | --- |
| 1 | This file | Handoff. You are here. |
| 2 | [`priority-c-closure-summary.md`](priority-c-closure-summary.md) | **Authoritative closure record** for the Priority C semantic-ABI slice (six risks resolved + Foreign Type Boundary Policy + Cross-Assembly Pattern B contract). Read this immediately after the TL;DR to anchor on the current evidence baseline before touching anything. |
| 3 | [`spikes/binding-generators/docs/next-iteration-plan.md`](next-iteration-plan.md) | Slice plan (1–5). Slices 1–4 + Oracle Priorities A/B/C all closed; Slice 5 (ppy orchestrator feature parity) deferred. Layer 2 typed API is the next forward scope. |
| 4 | [`spikes/binding-generators/docs/generator-spike-goals.md`](generator-spike-goals.md) | Original charter + current evidence status. |
| 5 | [`spikes/binding-generators/output/reports/iteration-2-comparison.md`](../output/reports/iteration-2-comparison.md) | Decision evidence — comparison table, multi-TFM trajectory, dynapi coherence. |
| 6 | [`spikes/binding-generators/output/reports/clangsharp-failure-buckets.md`](../output/reports/clangsharp-failure-buckets.md) | History of the 8 RSP-fix iterations that took us from 161 errors → 0. Useful when adding new exclude rules. |
| 7 | [`docs/binding-autogen/binding-generator-constitution.md`](../../../docs/binding-autogen/binding-generator-constitution.md) | **Authority** for ABI/API policy. Layer Contract (L34-50), Opaque Handles (L325-363, cross-assembly Pattern B clause from `d0016de`), C `long` policy (L246-283), wchar_t (L300-323), BCL-Replaceable Helper Exclusion Policy (L169-208), Foreign Type Boundary Policy (L365-400), Structs And Unions (L416-425), evidence gates (L376-388). Cited heavily below. |
| 8 | [`docs/binding-autogen/binding-generator-roadmap.md`](../../../docs/binding-autogen/binding-generator-roadmap.md) | Milestone plan. M4 = multi-TFM backends, M5 = public typed, M6 = friendly overloads, M7 = production flip. Next slice corresponds to M5. |
| 9 | ppy local clone at `spikes/binding-generators/references/ppy-SDL3-CS/` — see §"Reference projects". | North-star orchestrator. Patterns adapted, not copied. |

## Working preferences (read this — saves you grief)

The user (Deniz) is bilingual TR/EN, talks like a millennial dev, and pushes back hard. He has rejected several of my drafts during this session. Key feedback patterns to internalize:

- **No conclusion jumping.** When I said "Cake refactor is the only right path" without evidence, I got pushed back. Bring evidence (file:line, agent-scan, build output) before recommendations.
- **No scope creep.** When I added a `StripVarargsRewriter` to the LibraryImport postprocess without asking, I got pushed back. Solved it by checking Constitution L162-167 first (the policy is real, the rewriter is justified). User said: keep rewriter scope **targeted** — variadic methods only, no side effects.
- **No "let me wrap up" framing.** Stay in the work, propose the next step concretely.
- **No timing pressure.** User sets the pace. Don't push "are you ready?" / "should I continue?". Wait for explicit `go` / `başla` / `yap` / `devam` / `ok` / `onayladım`.
- **No magic.** When I proposed a "manual companion" pattern (Slice 5 part of ppy adapt), user rejected: "yapacak bir şey A magic gibi olacak öteki türlü". He prefers visible automated transforms over manual catch-up files.
- **Follow explicit paths.** When user says "use folder X", don't silently reroute. Ask if path is ambiguous.
- **No motive assumptions.** Don't guess his decision criteria. Surface options, recommend one with reasoning, let him pick.
- **No yes-man.** Push back when his suggestion has a flaw. Example: when he proposed transforming `[DllImport]` to `[LibraryImport]` for variadic functions, I pushed back because `Microsoft.Interop.LibraryImportGenerator` rejects `__arglist`. That pushback was welcomed.
- **Memory notes apply.** The user has long-term memory of preferences at `C:\Users\deniz\.claude\projects\E--repos-my-projects-janset2d-sdl2-cs-bindings\memory\MEMORY.md`. Read it. Notable items: "no workarounds/shortcuts", "no punting when solvable", "skip micro-checkpoints during execution", "git mv during migrations", "follow explicit paths".

When you need approval for a non-trivial change (build system, manifest, project files, refactors, git operations): **wait for explicit approval**. Documentation-only edits are pre-approved (per `AGENTS.md` §"Approval Gate").

## Grand vision (what we're building)

Three-layer SDL2 binding API per Constitution Layer Contract (L34-50):

```
┌─────────────────────────────────────────┐
│ Layer 3: Friendly overloads             │ ← Slice 6+ (Roadmap M6)
│  string?, Span<T>, ReadOnlySpan<byte>, │
│  out T, ref T, fmt-only variadic        │
├─────────────────────────────────────────┤
│ Layer 2: Public typed low-level API     │ ← Slice 6+ (Roadmap M5)
│  SDL2.SDL class                         │   (currently uses raw class only)
│  typed handles over nint                │
├─────────────────────────────────────────┤
│ Layer 1: Internal raw ABI               │ ← Slices 1-3 (current)
│  internal partial class SDLNative       │
│  [DllImport] (compat) / [LibraryImport] │
│  + [SupportedOSPlatform] guards         │
└─────────────────────────────────────────┘
```

The raw ABI boundary is container-level: `SDLNative` / `SDL_imageNative` must be internal. Generated methods may remain lexically public inside those internal containers because C# containing-type accessibility keeps them out of the public package API. See the constitution's `Internal Raw ABI: Why / How / What` section before changing this policy.

**Multi-TFM target** (Constitution L379, evidence gate):

- `net10.0`, `net9.0`, `net8.0` — Modern codegen output, `[LibraryImport]`, source-generated marshalling, modern C# (InlineArray, ReadOnlySpan u8 literal, nint/nuint, delegate*<...>)
- `netstandard2.0`, `net462` — Compat codegen output, `[DllImport]`, classic C# (`fixed int dir[N]`, `IntPtr`, `static readonly byte[]`)
- `System.Memory` polyfill package for legacy TFMs (Constitution L46)

**Multi-OS target**: per-platform raw ABI variant for headers whose public API or layout differs per platform. The implemented ClangSharp slice currently applies this only to `SDL_main.h` and `SDL_system.h`; broader SDL2 headers (`SDL_thread.h`, `SDL_rwops.h`, `SDL_syswm.h`, `SDL_stdinc.h`, `SDL_platform.h`, `begin_code.h`) need explicit roadmap decisions before production claims platform completeness.

Where this is going: production flip into `src/Janset.SDL2.<Family>/Generated/` per Roadmap M7. Not part of this spike. ADR-004 (CppAst toolchain decision) will need amendment when M7 lands.

## Code map — where things live

```
spikes/binding-generators/
├── README.md                          # workspace index, decision summary
├── Directory.Build.props              # CPM enabled, spike-local build defaults
├── docs/
│   ├── llm-handoff.md                 # THIS FILE
│   ├── generator-spike-goals.md       # charter + recorded decision
│   ├── next-iteration-plan.md         # active slice plan
│   └── reference-clones.md            # how to clone ppy + alimer references
├── scope/
│   ├── sdl2-core.headers.txt          # 51 SDL2.Core headers
│   ├── sdl2-image.headers.txt         # 1 SDL2_image header
│   └── bootstrap-*.headers.txt        # bring-up slices (mostly unused now)
├── clangsharp/                        # ← THE ACTIVE PROTOTYPE
│   ├── Janset.SDL2.ClangSharpSpike.slnx    # solution: postprocess + Core + Image + AbiTests
│   ├── generate_bindings.py           # Python orchestrator (~1250 LOC)
│   ├── compare_oracle.py              # Cake-preview / dynapi comparison validator
│   ├── oracle.cs                      # Roslyn/file-based raw ABI evidence reporter
│   ├── rsp/
│   │   ├── base.rsp                   # cross-cutting policy (defines, remaps incl. wchar_t* → nint, --with-type SDL_bool=int, -fdeclspec, -U__has_builtin)
│   │   ├── sdl2-core.rsp              # SDL2.Core family identity + exclude lists
│   │   ├── sdl2-image.rsp             # SDL2_image family identity
│   │   └── per-header/                # Priority C: per-header RSP overlays (ppy pattern)
│   │       ├── SDL_hidapi.rsp         # SDL_hid_device_=SDL_hid_device tag canonicalization (R6)
│   │       ├── SDL_mutex.rsp          # SDL_semaphore=SDL_sem tag canonicalization (R6)
│   │       ├── SDL_stdinc.rsp         # BCL-replaceable helper excludes (SDL_lround/SDL_ltoa/SDL_strtol/...)
│   │       ├── SDL_vulkan.rsp         # Foreign Type Boundary: VkInstance/VkSurfaceKHR → IntPtr
│   │       └── SDL_{audio,gamecontroller,haptic,joystick,sensor,surface,system}.rsp
│   ├── policy/
│   │   └── opaque-handle-roster.json  # Pattern B handle roster (14 auto + 3 force-opaque + 11 excluded), sdl2_version-keyed
│   ├── shims/platform-headers/        # Windows-local parse shims only (endian.h, AvailabilityMacros.h, TargetConditionals.h)
│   ├── postprocess/                   # Microsoft.CodeAnalysis console app
│   │   ├── Janset.SDL2.PostProcess.csproj  # net10.0, VersionOverride MS.CodeAnalysis 4.12.0
│   │   ├── Program.cs                 # CLI: platform-delta | strip-varargs | libraryimport | guid-substitute | threadid-dispatch | uniform-opaque
│   │   ├── DllImportToLibraryImportRewriter.cs   # Slice 2 rewriter
│   │   ├── PlatformDeltaPostProcessor.cs         # Slice 3/4 dedupe + guarded SupportedOSPlatform
│   │   ├── StripVarargsRewriter.cs    # Constitution L162-176 fmt-only policy enforcer
│   │   ├── GuidSubstitutionRewriter.cs           # Slice C-C: SDL_GUID → System.Guid
│   │   ├── ThreadIdDualDispatchRewriter.cs       # Slice C-A R2: SDL_threadID hybrid TFM emit (Modern CULong / Compat RuntimeInformation dispatch)
│   │   └── OpaqueHandleEmitRewriter.cs           # Slice C-B Pattern B: emits Handles.g.cs + rewrites SDL_X* → SDL_X at 3 positions (param/return/field)
│   ├── tests/abi-tests/               # Per-TFM ABI runtime smoke (net10/net9/net8/net462) — InternalsVisibleTo Core
│   └── src/
│       ├── Janset.SDL2.Core/
│       │   ├── Janset.SDL2.Core.csproj       # 5 TFMs, Compile Include conditional on TFM
│       │   ├── Support/
│       │   │   ├── NativeTypeNameAttribute.cs       # internal attribute used by [NativeTypeName(...)]
│       │   │   └── DisableRuntimeMarshalling.cs     # [assembly: DisableRuntimeMarshalling] NET7_0_OR_GREATER (Pattern B SYSLIB1051 resolution)
│       │   └── Generated/
│       │       ├── Compat/             # ClangSharp --config compatible-codegen output (netstandard2.0/net462)
│       │       │   ├── Handles.g.cs    # 17 Pattern B handle structs (canonical home, byte-identical to Modern)
│       │       │   ├── SDL_assert.g.cs, SDL_audio.g.cs, ... (51 files)
│       │       │   └── Platforms/      # ← Slice 3 output
│       │       │       └── {WindowsDesktop, WinRT, GDK, Linux, MacOS, IOS, Android}/
│       │       │           ├── SDL_main.g.cs
│       │       │           └── SDL_system.g.cs
│       │       └── Modern/             # ClangSharp --config latest-codegen output (net8+)
│       │           ├── Handles.g.cs    # 17 Pattern B handle structs (byte-identical to Compat)
│       │           ├── SDL_*.g.cs       # postprocessed to [LibraryImport]
│       │           └── Platforms/.../   # same shape, modern-flavour
│       └── Janset.SDL2.Image/
│           ├── Janset.SDL2.Image.csproj   # ProjectReference -> Core (consumer mode for Pattern B handles)
│           ├── Support/
│           │   ├── NativeTypeNameAttribute.cs
│           │   └── DisableRuntimeMarshalling.cs    # [assembly: DisableRuntimeMarshalling] (cross-assembly Pattern B)
│           └── Generated/
│               ├── Compat/SDL_image.g.cs
│               └── Modern/SDL_image.g.cs  # postprocessed to [LibraryImport]
├── alimer-style/                      # RETAINED reference, not the chosen path
│   └── src/Janset.Sdl2.AlimerSpike.Generator/   # CppAst-based single-pass prototype
├── output/
│   └── reports/                       # evidence artifacts (NOT generated outputs)
│       ├── iteration-2-comparison.md  # toolchain decision report
│       ├── clangsharp-failure-buckets.md   # 8 RSP-fix iteration history
│       ├── clangsharp-full.md         # per-header generation report (auto-written by generate_bindings.py)
│       ├── oracle-comparison-clangsharp.md   # ClangSharp vs Cake preview (auto-written by compare_oracle.py)
│       └── oracle-comparison-alimer.md       # Alimer vs Cake preview (final state, not regenerated)
└── references/                        # GITIGNORED — local clones for evidence only
    ├── ppy-SDL3-CS/                   # ★ north star — see §"Reference projects"
    └── alimer-bindings-sdl/           # CppAst patterns reference
```

### Production code worth knowing (don't edit, but reference)

```
build/_build/Targets/GenerateBindings/      # Cake CppAst pipeline — known-good output is the "oracle"
├── PlatformViews/
│   └── PlatformCatalog.cs                  # ★ SDL2 7-view macro/define/undefine map (lines 18-77)
├── ModelBuilding/
│   ├── Types/TypeMappingPolicy.cs          # ★ scalar mapping (Sint8=sbyte, SDL_bool=int, CLong/CULong)
│   ├── Macros/MacroApiPolicy.cs            # macro taxonomy (helper-candidate, NonApi, etc.)
│   ├── Declarations/KnownUnsupportedDeclarationPolicy.cs  # variadic + manifest deferral policy
│   ├── SdlPolicy/SdlNativeTypeSubstitutionPolicy.cs       # SDL_GUID -> Guid
│   └── ...
├── Emit/
│   ├── RawAbi/RawAbiCommandEmitter.cs     # ★ #if NET6_0_OR_GREATER + #if NET5_0_OR_GREATER guard pattern
│   └── Tfm/ModernCIntegerEmissionPolicy.cs # CLong/CULong NET6_0_OR_GREATER guard logic
└── Parse/CppAstParseRunner.cs              # parse defines + clang_args setup
```

`artifacts/generated-bindings-preview/sdl2-core/` is the **Cake-generated oracle** — our spike output is compared against this for correctness signal. Look at `Platform/<View>/Commands.g.cs` to see what platform-specific output should look like; look at `Types/Enums.g.cs`, `Types/Structs.g.cs`, `Types/Handles.g.cs`, `Types/Callbacks.g.cs`, `Constants.g.cs` for the public-typed shape M5 will need.

`build/manifest.json` `library_manifests[0].binding_generation` (around line 183) — production family config: `parse_defines`, `clang_args`, `excluded_functions`, `required_functions`, `deferred_declarations`. Our `rsp/sdl2-core.rsp` mirrors much of this.

## Reference projects (in `references/`, gitignored)

### `ppy-SDL3-CS/` — the north-star orchestrator

ppy is the audio team behind osu! who maintain SDL3-CS bindings. They invented the "ClangSharp + Roslyn source generator + per-header `.rsp`" pattern we're adapting. Worth reading in this order:

1. `SDL3-CS/generate_bindings.py` (~438 LOC) — the master orchestrator. **Our `clangsharp/generate_bindings.py` is a subset of this.** Key sections:
   - Lines 121-178: `headers` list (per-header `Header` instances)
   - Lines 232-257: `get_manually_written_symbols` / `get_typedefs` — **Slice 5 work**, regex feedback from companion .cs files
   - Lines 268-308: `base_command` — what every ClangSharp invocation gets
   - Lines 310-329: `run_clangsharp` — orchestrates per-header invocation with manual-symbol exclude feedback
   - Lines 341-365: `generate_platform_specific_headers` — multi-OS pass, the pattern we're adapting in Slice 3
   - Lines 386-434: `main` — flow control
2. `SDL3-CS/SDL3/SDL3-CS.csproj` and `SDL3-CS/SDL3-CS.SourceGeneration/` — Roslyn SG for friendly overloads. **Slice 6+ reference**, not used yet.
3. `SDL3-CS/SDL3/*.rsp` (9 files) — per-header response files. Patches for the specific header (e.g., `SDL_audio.rsp` does `--with-type SDL_AudioFormat=uint`). **Slice 5 work** — we don't have per-header .rsp lookup in our orchestrator yet.
4. `SDL3-CS/SDL3/*.cs` (companion files) — hand-written constants, macros, ergonomic helpers. ppy has ~25 of these. Companion regex `[Constant]` / `[Typedef]` / `[Macro]` markers feed back into `generate_bindings.py` as `--exclude` / `--remap`. **Slice 5 work**.

### `alimer-bindings-sdl/` — CppAst patterns reference

Not the chosen toolchain. Useful for type-mapping patterns if ClangSharp ever hits a wall we can't get past. The Alimer-style spike under `alimer-style/` was kept as the comparison artifact.

## Approach decision history (don't relitigate)

Earlier in the session, we ran a full comparison spike: Alimer-style CppAst vs ppy-style ClangSharp. The full evidence is in `output/reports/iteration-2-comparison.md`. Decisive numbers:

| Metric | ClangSharp | Alimer | Cake oracle |
| ---: | ---: | ---: | ---: |
| Functions emitted | 889 | 846 | 866 |
| Dynapi hit rate | 98.1% (829/845) | 92.4% (781/845) | reference |
| Iterations to compile-clean (net10) | 8 RSP-fix cycles | 1 | reference |
| Multi-TFM errors before fix | 474 | 12 | 0 (already TFM-aware) |
| Multi-TFM after fix | 0 | (not attempted) | 0 |

**Decision turning point**: ClangSharp `--config compatible-codegen` produces netstandard2.0-safe output (no InlineArray, no UTF-8 u8 literal, no `delegate*<>`). Dual-codegen pass (`compatible` for legacy + `latest` for modern) reduced 474 multi-TFM errors to 0 in a single Python flag. That solved the Constitution L48 backend split goal (DllImport on legacy + LibraryImport on modern via postprocess) better than the Alimer-style CppAst path which would need owned `#if` emitter logic.

**Alimer scaffold retained** under `alimer-style/`. Don't delete. Reference only.

**ADR-004 Reopened (2026-05-23)**. ADR-004 was originally accepted on 2026-05-14 selecting CppAst and was Reopened on 2026-05-23 pending this spike's evidence. The sunset Cake-hosted CppAst implementation under `build/_build/Targets/GenerateBindings/` is in sunset regardless of which toolchain wins — either ClangSharp + Roslyn postprocess (this spike's active prototype) or an Alimer-style single-pass CppAst replaces it. Production flip is Roadmap M7 work and requires an ADR amendment or supersession.

## Slice progress

| Slice | Status | Summary |
| --- | --- | --- |
| 1 — Layout restructure + library projects | ✅ done | `clangsharp/` folder, `.slnx`, `Janset.SDL2.Core` + `Janset.SDL2.Image` classlibs (5 TFMs each), Generated moved into each library's `Generated/{Compat,Modern}/`. Multi-TFM build clean. |
| 2 — Microsoft.CodeAnalysis postprocess: DllImport → LibraryImport | ✅ done | `postprocess/Janset.SDL2.PostProcess.csproj` console app with `DllImportToLibraryImportRewriter` + `StripVarargsRewriter`. Modern output uses `[LibraryImport]` + `[UnmanagedCallConv]` + `partial`. Constitution L162-176 enforced (variadic `__arglist` stripped, fmt-only fixed-prefix calls). Python pipeline orchestrates the whole flow. |
| 3 — Multi-OS parse pass | ✅ done 2026-05-22 | `SDL_main.h` / `SDL_system.h` platform pass + `platform-delta` dedupe + guarded `[SupportedOSPlatform]`. `--use-platform-header-shims` unblocks Windows-local synthetic platform parsing; native Linux/macOS generation remains the production evidence target. |
| 4 — `[SupportedOSPlatform]` `#if NET5_0_OR_GREATER` guard postprocess | ✅ folded into Slice 3 | `PlatformDeltaPostProcessor` adds `#if NET5_0_OR_GREATER`-guarded `using System.Runtime.Versioning;` and guarded `[SupportedOSPlatform(...)]` by `Platforms/<View>` path. |
| **Priority A** — Raw ABI visibility + Image namespace | ✅ done (`92b893b`) | Generated raw ABI containers internal via ClangSharp class-level access specifiers; Image namespace drift fixed. |
| **Priority B** — Required `SDL.h` surface | ✅ done (`444fada`) | Recovered `SDL_Init`, `SDL_InitSubSystem`, `SDL_Quit`, `SDL_QuitSubSystem`, `SDL_WasInit`, and ten `SDL_INIT_*` constants without re-parsing the umbrella header. |
| **Priority C** — Semantic-ABI completion (six risks) | ✅ **CLOSED 2026-05-24** | See [`priority-c-closure-summary.md`](priority-c-closure-summary.md). Slice C-A scalars (`adb64d0`) + Slice C-B Pattern B uniform opaque (`45fdab6`) + Slice C-C handle canonicalization + SDL_GUID. R1 wchar_t opaque, R2 C `long` hybrid, R3/R4/R5 deferred-layout force-opaque, R6 tag-typedef canonicalization. Foreign Type Boundary Policy + BCL-Replaceable Helper Exclusion Policy + Cross-Assembly Pattern B contract codified. Oracle 0/0/0/0/0/0 findings across the six risk categories; AbiTests 4/4 TFMs Windows + Linux x64 net10 docker. |
| 5 — ppy orchestrator feature parity | ⏳ deferred | Per-header `.rsp` lookup landed (used by Priority C); manual-symbol exclusion feedback regex (`[Constant]` / `[Typedef]` markers in companion .cs files) and full dynapi validation pass still deferred. ppy `generate_bindings.py:232-329` is the reference. Not on the critical path for the Layer 2 next scope. |
| **Next — Layer 2 typed public API** | ⏳ Roadmap M5 | Public typed `SDL2.SDL` projection over the now-stable Layer 1 raw ABI. Handle shape (Pattern B by-value structs), scalar widths, and foreign-type boundary are all settled, so Layer 2 can land without churn on Layer 1. |
| 6+ — Friendly overloads | ⏳ Roadmap M6 | Out of current spike scope until Layer 2 lands. |

## What got built and why — slice-by-slice rationale

### Slice 1 — Why `Janset.SDL2.Core` + `Janset.SDL2.Image` separate projects

Cake oracle keeps satellite output in separate package families (Constitution §"Family Identity", L130-136). Production layout is `src/Janset.SDL2.<Family>/`. Spike layout pre-emptively mirrors production so Slice 6+ (production flip) is a `git mv` + manifest update, not a re-architecture.

Image library `ProjectReference`s Core because Image generated code uses Core types (`SDL_Surface*`, `SDL_RWops*`, `SDL_Renderer*`). Constitution L84-88 explicitly forbids satellite output redeclaring core types.

5 TFMs: net10.0, net9.0, net8.0, netstandard2.0, net462 — Constitution L379 evidence gate. `System.Memory` polyfill package added to legacy TFMs only via `Condition='$(TargetFramework)' == 'netstandard2.0' Or '$(TargetFramework)' == 'net462'"`.

### Slice 2 — Why a separate postprocess project instead of Roslyn SG

User asked: "can Roslyn modify existing generated .cs files?" Answer: No — Roslyn source generators can only **add** files, never modify existing ones. To transform ClangSharp's `[DllImport]` output into `[LibraryImport]`, we need a separate **build-time tool** that uses `Microsoft.CodeAnalysis.CSharp` Syntax/Rewriter API to load existing `.g.cs`, transform it, write it back.

Why not in-place from generate_bindings.py? Python can do regex, but the transform involves nested attribute lists, syntax-tree-preserving trivia, partial method modifier insertion. A Roslyn rewriter does this with `SyntaxRewriter` cleanly — fragile regex would break on edge cases.

Why is `StripVarargsRewriter` here too? Constitution L162-176 rejects `__arglist`. ClangSharp emits it by default. `Microsoft.Interop.LibraryImportGenerator` doesn't support `__arglist` either. We need to drop the `...` tail from variadic signatures so they become fmt-only (`SDL_SetError(byte* fmt)` instead of `SDL_SetError(byte* fmt, __arglist)`). The rewriter does this generically — same `MethodDeclarationSyntax` visitor, scope is targeted (only methods with `__arglist` parameter, no other side effects).

User pushed back on scope creep here. The pattern that survived: rewriter scope is **clearly bounded** (variadic methods only, drop only `__arglist` parameter), justified by Constitution citation, and discussed before implementation.

### Slice 3 — Why `SDL_main.h` and `SDL_system.h` were the first multi-OS pass

I spawned an Explore agent that grepped all 52 SDL2.Core + SDL2_image headers for platform-sensitive macros from Cake `PlatformCatalog.AllPlatformMacros`. Result split:

| Bucket | Headers | Reason |
| --- | --- | --- |
| **Must — public API differs per platform** | `SDL_main.h`, `SDL_system.h` | Entire function families gated (`SDL_Direct3D9GetAdapterIndex` for Windows, `SDL_AndroidGetActivity` for Android, `SDL_iPhoneSetEventPump` for iOS, etc.) |
| **Already handled / skip** | `SDL_syswm.h` (Constitution L292-294 — Stage 1 quarantine), `SDL_platform.h` (macro-only, no functions) | No pass needed |
| **Internal gates only — skip** | `SDL_assert`, `SDL_atomic`, `SDL_endian`, `SDL_rwops`, `SDL_stdinc`, `SDL_thread` | Platform gates affect internal implementation, not public API surface |
| **Platform-neutral** | All other 42 headers | Single neutral parse is enough |

This matches ppy SDL3's immediate platform-function pass: ppy `generate_bindings.py:419-434` applies `generate_platform_specific_headers` to `SDL_main.h` and `SDL_system.h` only. Do not overread that as proof that SDL2's platform-sensitive layouts and macros are solved.

The 7 platform views (`WindowsDesktop`, `WinRT`, `GDK`, `Linux`, `MacOS`, `IOS`, `Android`) come from Cake `PlatformCatalog.CreateSdl2Catalog()`. Each view has a defines list + `SupportedOSPlatform` string. Adapted **verbatim** into Python as `SDL2_PLATFORM_VIEWS` in `generate_bindings.py`.

### Critical SDL2 vs SDL3 distinction (don't get bitten)

**SDL3 platform macros** (`SDL_PLATFORM_WINDOWS`, `SDL_PLATFORM_LINUX`, `SDL_PLATFORM_GDK`, …) are SDL-specific; the host compiler never default-defines them. ppy SDL3 therefore doesn't bother with cross-contamination undefines — defining `SDL_PLATFORM_LINUX` for Linux pass is enough.

**SDL2 platform macros** (`_WIN32`, `__APPLE__`, `__ANDROID__`, `__LINUX__`, `__IPHONEOS__`, …) **are host-compiler-defined**. Our parse target `x64-windows-hybrid` makes libclang define `_WIN32` by default. Without explicit `-U_WIN32` in the Linux pass, the Linux view emits the Windows code. Cake `AllPlatformMacros` list (28 macros) is the cross-contamination protection — for every macro **not** in the current view's defines, the per-platform pass emits `--additional --undefine-macro=<X>`. Our Python implementation mirrors this exactly.

## Current status and blockers

### What is now fixed

`clangsharp/generate_bindings.py` now runs a true-neutral pass for `SDL_main.h` / `SDL_system.h`, then one platform-view pass per Cake SDL2 view. It deliberately does **not** use ClangSharp `--with-attribute`; `PlatformDeltaPostProcessor` owns dedupe and path-based platform attribution.

For Windows-local spike iteration, pass `--use-platform-header-shims`. This adds `clangsharp/shims/platform-headers/` as an extra ClangSharp include directory containing minimal `endian.h`, `AvailabilityMacros.h`, and `TargetConditionals.h` shims. These are parse unblockers only, not production SDK substitutes.

The previous `CS0101 SDL_main_func` problem is fixed by Roslyn dedupe rather than by trying to coerce ClangSharp `--exclude` into suppressing top-level types. `PlatformDeltaPostProcessor` collects declarations from neutral output, removes duplicates from `Generated/<Codegen>/Platforms/<View>/`, and adds guarded `[SupportedOSPlatform("...")]` only to surviving platform-only methods.

The previous duplicate modern attributes are fixed in `DllImportToLibraryImportRewriter`: `[UnmanagedCallConv]` now copies only indentation trivia from the host `[LibraryImport]` attribute list, not the whole leading preprocessor block. This prevents the guarded `[SupportedOSPlatform]` trivia from being cloned onto `[UnmanagedCallConv]`.

Verification from this session:

- `dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release` succeeds with 0 warnings / 0 errors.
- `python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims` is the Windows-local spike command when validating synthetic platform views. Latest run wrote `clangsharp-full.md` with `Platform header shims: enabled` and no empty generated outputs.
- `dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release` succeeds across `net462`, `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0` with 0 warnings / 0 errors.
- `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report` writes [`output/reports/oracle-evidence-clangsharp.md`](../output/reports/oracle-evidence-clangsharp.md): family-aware raw ABI evidence for SDL2 Core and SDL2 Image, including surface counts, Cake/SDL2-CS/dynapi comparison, and constitution-risk buckets.
- `git diff --check` reports no whitespace errors; Git may still print CRLF normalization warnings for regenerated files.
- `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"` reports 0 issues. The extra `references/**` exclude is required because those local peer-project clones are gitignored evidence inputs, not repo code.

### What is still not solved

The Windows-local ClangSharp spike cannot honestly claim final platform-view support yet. Defining non-Windows macros is not enough because SDL2 headers then include platform system headers that do not exist in the Windows-local vcpkg/header context. The explicit shim flag is acceptable for local iteration, but not a replacement for native platform generation.

Oracle report repair queue status (evidence-backed gaps from `output/reports/oracle-evidence-clangsharp.md`):

| Priority | Gap group | Status |
| --- | --- | --- |
| A | Raw ABI visibility and SDL2_image family identity | **Done.** Commit `92b893b` made generated raw ABI containers internal via ClangSharp class-level access specifiers and fixed Image namespace drift. |
| B | Missing required `SDL.h` functions and constants | **Done.** Commit `444fada` recovered `SDL_Init`, `SDL_InitSubSystem`, `SDL_Quit`, `SDL_QuitSubSystem`, `SDL_WasInit`, and the ten `SDL_INIT_*` constants without parsing `SDL.h` as a normal umbrella translation unit. |
| C | Deferred layouts and platform-sensitive scalar mappings (`SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg`, `wchar_t`, C `long`, tag/typedef canonicalization, `SDL_GUID`) | **CLOSED 2026-05-24.** All six risks resolved + Foreign Type Boundary Policy + BCL-Replaceable Helper Exclusion Policy + Cross-Assembly Pattern B contract via `[assembly: DisableRuntimeMarshalling]`. Oracle reports 0 findings across all six risk categories. Full evidence + verification table + commit chain (Slice C-A `adb64d0`, Slice C-B `45fdab6`, DisableRuntimeMarshalling `d0016de`, closure summary `e62bf92`) at [`priority-c-closure-summary.md`](priority-c-closure-summary.md). |

Remaining items to watch (none of these block the Layer 2 next slice):

- Windows-local non-Windows views still require `--use-platform-header-shims`. The shim flag is local-iteration evidence only; production Linux/macOS evidence must come from native generation (already wired for `x64-linux-hybrid` via the binding-generator docker container; remaining RIDs join on the per-RID CI matrix).
- ClangSharp's `CXError_Failure` non-zero exit around `declspec` for several Windows/Android views still happens but leaves usable output. Inspect output and oracle deltas; do not treat exit code alone as semantic failure.
- GDK-only APIs currently receive `[SupportedOSPlatform("windows")]` because .NET has no first-class GDK platform token. Coarse analyzer guard, not proof of callability from ordinary Windows desktop apps.
- Multi-OS `SDL_system.h` platform-pass parse warnings (`declspec` parse error on iOS/macOS/Android views) are pre-existing and unrelated to Priority C scope; the ABI surface is sourced from the Windows-canonical neutral pass.

Concrete generated evidence:

- `Generated/Modern/Platforms/Android/SDL_system.g.cs` has guarded `[SupportedOSPlatform("android")]`, `[LibraryImport]`, and `[UnmanagedCallConv]` without duplicated platform attributes.
- `Generated/Modern/Platforms/Linux/SDL_system.g.cs` now emits `SDL_LinuxSetThreadPriority` and `SDL_LinuxSetThreadPriorityAndPolicy` when shims are enabled.
- `Generated/Modern/Platforms/IOS/SDL_main.g.cs` / `SDL_system.g.cs` now emit `SDL_UIKitRunApp`, `SDL_iPhoneSetAnimationCallback`, `SDL_iPhoneSetEventPump`, and `SDL_OnApplicationDidChangeStatusBarOrientation` when shims are enabled.
- `SDL_IsTablet` and the six `SDL_OnApplication*` delegate notification methods remain neutral on purpose: `SDL_system.h` declares them outside `__IPHONEOS__`, and `SDL2.exports` includes them unconditionally. Only `SDL_OnApplicationDidChangeStatusBarOrientation` is guarded by `__IPHONEOS__`.

### Header-scope correction

The earlier "only `SDL_main.h` and `SDL_system.h` matter" statement was too narrow for production. It was true for this immediate function-focused slice, but not for the whole binding contract.

Carry these headers into roadmap/planning before production claims platform correctness:

- `SDL_main.h`: platform-gated startup functions and `SDL_main_func` delegate behavior.
- `SDL_system.h`: platform-gated functions, typedefs, enums, and constants.
- `SDL_thread.h`: platform-dependent `SDL_CreateThread` / `SDL_CreateThreadWithStackSize` signatures.
- `SDL_rwops.h`: platform-sensitive `SDL_RWops` union layout.
- `SDL_syswm.h`: major platform-sensitive `SDL_SysWMmsg` / `SDL_SysWMinfo` layout; currently Stage 1 quarantined by the constitution.
- `SDL_stdinc.h`: platform-varying macro constants and non-Windows `strdup` behavior.
- `SDL_platform.h`: platform-identification macros.
- `begin_code.h`: calling convention/export macro behavior.

### Recommended next moves

1. **Layer 2 typed low-level public API slice.** The Layer 1 raw ABI surface is now stable (handle shape, scalar widths, foreign-type boundary, cross-assembly Pattern B contract all settled). Sketch the typed `SDL2.SDL` projection over `SDLNative` — typed handles already exist as Pattern B by-value structs in `Generated/<Codegen>/Handles.g.cs`, so Layer 2 calls become `SDLNative.SDL_DestroyWindow(window.Value)` style. See Constitution Layer Contract L34-50 and Roadmap M5 for the target shape; Cake oracle's `Types/Handles.g.cs` + `Constants.g.cs` + `Emit/PublicTyped/` is a reference for the structural projection (do not copy code, just the shape).
2. **Plan Task 19 — final code review + finishing branch decision.** Per AGENTS.md §Approval Gate, push to remote requires Deniz approval. Branch is 35+ commits ahead of remote; this is the gate before any Layer 2 work starts.
3. Use `--use-platform-header-shims` for Windows-local ClangSharp iteration when platform-view parse coverage matters. Keep native Linux/macOS generation as the production evidence target.
4. Keep the explicit empty-platform-output guard. An empty `.g.cs` from a fatal ClangSharp run should stay a hard evidence item, not something the build can silently accept.
5. Build comparable CppAst/Alimer-style evidence against the same multi-TFM/platform/oracle checklist before making any final toolchain recommendation. ClangSharp won Iteration 2 but the bake-off lane stays open.

## Notable patterns to keep using

### Constitution citations are how decisions get authority

When in doubt, cite Constitution line numbers. Examples used in this session:

- L34-50 (Layer Contract — three-layer API shape)
- L46 (`System.Memory` allowed for legacy TFMs)
- L48 (DllImport/LibraryImport backend split)
- L57 (`SDL_bool` int-backed enum policy)
- L130-136 (Family Identity table)
- L156 (variadic accepted deferrals)
- L162-176 (C variadics fmt-only policy — drives `StripVarargsRewriter`)
- L208-212 (C `long` policy)
- L228-235 (SDL2 `SDL_bool` shape)
- L292-294 (SDL_syswm Stage 1 quarantine)
- L324-348 (macro pipeline)
- L379 (multi-TFM compile evidence gate)

If you don't cite, you don't have authority. Find the citation before recommending.

### Cake oracle as comparison floor

When a question is "what should this look like?", look at:

- `artifacts/generated-bindings-preview/sdl2-core/` — generated output (Cake's known-good)
- `build/_build/Targets/GenerateBindings/` — the policy code that produced it

Cake's `RawAbiCommandEmitter.cs:49-59` (CLong guard, SupportedOSPlatform guard with `#if NET5_0_OR_GREATER`) and `PlatformCatalog.cs:18-77` (SDL2 view definitions) are the two most-cited files in this spike. Reference them when in doubt.

### Explore agent for evidence

When the user says "you're guessing — go look", spawn an Explore agent with a precise prompt. Done once in this session (header platform sensitivity scan, 2026-05-21) — yielded the evidence that narrowed Slice 3 from "all 51 headers" to "only SDL_main + SDL_system". Pattern: give the agent the exact files to scan, the exact macros to look for, the exact output format, and a length budget.

### Multi-TFM build is the truth gate

Don't trust "0 errors on net10.0" — that's a 1/5 result. Always build the full sln with `dotnet build .../Janset.SDL2.ClangSharpSpike.slnx -c Release` to exercise all 5 TFMs. The user caught me framing single-TFM success as "all clean" once; pushed back; rightly so.

### Commands you'll run constantly

```pwsh
# Regenerate everything: compat + modern × per-family + multi-OS pass + 6-step postprocess pipeline
# Postprocess order (see generate_bindings.py): platform-delta → strip-varargs → libraryimport (Modern only) → guid-substitute → threadid-dispatch → uniform-opaque
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

# Build all 5 TFMs (the truth gate)
dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release

# Compare against Cake oracle + dynapi
python spikes/binding-generators/clangsharp/compare_oracle.py --approach clangsharp

# Raw ABI oracle/evidence report (Roslyn, family-aware) — the authoritative six-risks oracle
dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --write-report

# Per-TFM ABI runtime smoke (proves SDL_ThreadID dispatch works on real native lib)
dotnet test spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release --framework net10.0
# Linux x64 net10 via docker (see README §"Per-TFM ABI runtime smoke")

# Just rerun a single postprocess step on existing output (no regen)
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- libraryimport spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern
dotnet run --project spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release -- uniform-opaque spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern
```

## Anti-patterns I fell into (don't repeat)

1. **Recommending without evidence.** Wait for the comparison data; cite the file; show the diff. Don't say "the right path is X" before establishing why.

2. **Adding rewriters when a config flag would do.** I added `StripVarargsRewriter` before checking if ClangSharp had a flag to handle variadics. Answer: it doesn't, the rewriter was justified — but the user rightly asked me to verify before piling on code.

3. **Two-pass when one-pass would do.** I designed `generate_platform_specific_headers` as 2-pass per view (first emit, then re-emit with attributes) because I didn't have `sdl.json`. The actual implemented shape is one platform invocation plus Roslyn `platform-delta` for dedupe/attributes. Reach for "postprocess the generated syntax" before asking ClangSharp to do SDL-specific attribution magic.

4. **Ignoring host-compiler default macros.** Cake's `AllPlatformMacros` undefines exist for a reason — host compiler defines `_WIN32` etc. by default and they leak into every view unless explicitly undefined. SDL3 doesn't need this because SDL_PLATFORM_* macros aren't host-defined; SDL2 does. Don't drop the undefines just because ppy doesn't have them.

5. **Glossing over partial failures.** ClangSharp diagnostics often land on stdout, and it can leave behind empty `.g.cs` files while returning non-zero. The generator now records those platform-view non-zero exits, but the next agent should still inspect generated file contents and oracle deltas instead of trusting compile success alone.

## Cross-references and authoritative sources

| Topic | Authority |
| --- | --- |
| ABI/API policy | `docs/binding-autogen/binding-generator-constitution.md` |
| Milestone plan | `docs/binding-autogen/binding-generator-roadmap.md` |
| Priority C closure record | `spikes/binding-generators/docs/priority-c-closure-summary.md` (six risks resolved + Foreign Type Boundary Policy + Cross-Assembly Pattern B contract, 2026-05-24) |
| Priority C durable policy | `docs/binding-autogen/binding-generator-constitution.md` |
| Priority C follow-up backlog | `spikes/binding-generators/docs/next-iteration-plan.md` §"Review Follow-up Backlog — 2026-05-25" |
| Semantic ABI research | `docs/research/semantic-abi-type-classification-research.md` (2026-05-22) |
| Opaque-handle roster (single source of truth) | `spikes/binding-generators/clangsharp/policy/opaque-handle-roster.json` (sdl2_version 2.32.10) |
| Toolchain ADR | `docs/decisions/2026-05-14-binding-autogen-toolchain.md` |
| Build host pattern | `docs/decisions/2026-05-05-target-centric-build-host.md` |
| Active slice plan | `spikes/binding-generators/docs/next-iteration-plan.md` |
| Spike charter | `spikes/binding-generators/docs/generator-spike-goals.md` |
| Decision evidence | `spikes/binding-generators/output/reports/iteration-2-comparison.md` |
| RSP fix history | `spikes/binding-generators/output/reports/clangsharp-failure-buckets.md` |
| Family-aware raw ABI evidence | `spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md` |
| User memory | `C:\Users\deniz\.claude\projects\E--repos-my-projects-janset2d-sdl2-cs-bindings\memory\MEMORY.md` |
| Agent contract | `AGENTS.md` (root) |

When you finish your session, **update this file**. Particularly §"Current status and blockers" and §"Slice progress". The next LLM after you will read this first, just as you did.

Good luck. Don't add the magic.
