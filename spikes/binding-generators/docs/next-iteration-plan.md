# Spike Next-Iteration Plan — ppy-Style ClangSharp Production Prototype

**Date:** 2026-05-21; updated 2026-05-26 (Item 1 closure)
**Status:** Active spike plan. Priority A (`92b893b`), Priority B (`444fada`), **Priority C semantic-ABI completion** (`e62bf92`), and **Item 1 per-library generation infrastructure** are closed. See [`priority-c-closure-summary.md`](priority-c-closure-summary.md) and [`satellite-expansion-roadmap.md`](satellite-expansion-roadmap.md). **Next forward scope: Iteration 2 config surface unification, then Items 2–5 satellite Layer 1 completion** — Layer 2 typed public API defers until all five families (Core + Image + TTF + Mixer + GFX) have a stable Layer 1 raw ABI surface, providing a holistic view before designing the public projection. Slice 5 (ppy orchestrator feature parity) remains deferred. Windows-local ClangSharp uses `--use-platform-header-shims` for synthetic Linux/macOS/iOS views; native generation already wired for `x64-linux-hybrid` via the binding-generator docker container. ppy reference analysis recorded in [`ppy-reference-analysis-2026-05-25.md`](ppy-reference-analysis-2026-05-25.md). Do not make a final ClangSharp-vs-CppAst recommendation until comparable CppAst/Alimer-style evidence exists.

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
| 2 — Microsoft.CodeAnalysis postprocess: DllImport → LibraryImport | ✅ done 2026-05-21 | `postprocess/Janset.SDL2.PostProcess.csproj` console app + `DllImportToLibraryImportRewriter`. Modern output gets `[LibraryImport]` + `[UnmanagedCallConv]` + `partial`. Includes `StripVarargsRewriter` enforcing Constitution L162-176 `__arglist` rejection — variadic methods drop `...` tail so both Compat and Modern emit fmt-only signatures. Python script orchestrates: regen (compat+modern) → postprocess pipeline. Multi-TFM build clean. |
| 3 — Multi-OS parse pass | ✅ done 2026-05-22 | `SDL_main.h` / `SDL_system.h` platform pass landed. Neutral/platform duplicate removal works via `PlatformDeltaPostProcessor`. `--use-platform-header-shims` unblocks Windows-local synthetic platform parsing; native Linux generation wired via the binding-generator docker container. |
| 4 — SupportedOSPlatform `#if NET5_0_OR_GREATER` guards | ✅ folded into Slice 3 2026-05-22 | `PlatformDeltaPostProcessor` adds guarded `using System.Runtime.Versioning;` and guarded `[SupportedOSPlatform(...)]` by `Platforms/<View>` path. |
| Oracle Priority A — Raw ABI visibility + Image namespace | ✅ done 2026-05-23 (`92b893b`) | Generated raw ABI containers internal via ClangSharp class-level access specifiers; Image namespace drift fixed. |
| Oracle Priority B — Required `SDL.h` surface | ✅ done 2026-05-24 (`444fada`) | Recovered `SDL_Init`, `SDL_InitSubSystem`, `SDL_Quit`, `SDL_QuitSubSystem`, `SDL_WasInit`, and ten `SDL_INIT_*` constants without re-parsing the umbrella header. |
| **Oracle Priority C — Slice C-A (R1 wchar_t + R2 C long)** | ✅ closed 2026-05-24 (`adb64d0`) | R1: `wchar_t *=nint` + `const wchar_t *=nint` literal-space `--remap` entries in `rsp/base.rsp`. R2: hybrid — `rsp/per-header/SDL_stdinc.rsp` excludes BCL-replaceable helpers (`SDL_lround`/`SDL_lroundf`/`SDL_ltoa`/`SDL_ultoa`/`SDL_strtol`/`SDL_strtoul`); current `ClongDualDispatchRewriter` mode-aware emit (Modern `CULong` + `LibraryImport` vs. Compat managed wrapper + `RuntimeInformation.IsOSPlatform` dispatch). Per-RID AbiTests harness proves `SDL_ThreadID` non-zero on Win net10/9/8/462 + Linux x64 net10 docker. |
| **Oracle Priority C — Slice C-B (R3/R4/R5 Pattern B uniform opaque)** | ✅ closed 2026-05-24 (`45fdab6`) | `OpaqueHandleEmitRewriter` consolidates 17 handles in `Generated/<Codegen>/Handles.g.cs` (14 auto-detected + 3 force-opaque `SDL_RWops` / `SDL_SysWMinfo` / `SDL_SysWMmsg`). Pattern B by-value struct (single `nint` field) rewrites `SDL_X*` → `SDL_X` at all 3 raw-ABI positions: parameter, return, **struct field**. Driven by `config/family-config.json`. Cross-assembly contract via `[assembly: DisableRuntimeMarshalling]` in Core + Image (`Support/DisableRuntimeMarshalling.cs`, commit `4b87037`). Constitution policy codified at `d0016de`. |
| **Oracle Priority C — Slice C-C (R6 tag canonicalization + SDL_GUID)** | ✅ closed 2026-05-24 | Per-header RSP canonicalization: `SDL_hid_device_=SDL_hid_device` in `rsp/per-header/SDL_hidapi.rsp`; `SDL_semaphore=SDL_sem` in `rsp/per-header/SDL_mutex.rsp`. `GuidSubstitutionRewriter` walks `[NativeTypeName("SDL_GUID")]` annotations and rewrites managed type to `System.Guid` (16-byte wire-identical, commit `bb638f9`). Cross-header `_SDL_Joystick` audit confirmed clean. |
| **Oracle Priority C — Overall closure** | ✅ **CLOSED 2026-05-24** (`e62bf92`) | All six risks resolved. Oracle reports 0 findings across `platform-sensitive-wchar`, `platform-sensitive-long`, `deferred-layout-sdl-rwops`, `deferred-layout-sdl-syswminfo`, `deferred-layout-sdl-syswmmsg`, `duplicate-tag-typedef`. Full evidence + verification table in [`priority-c-closure-summary.md`](priority-c-closure-summary.md). Foreign Type Boundary Policy + BCL-Replaceable Helper Exclusion Policy + Cross-Assembly Pattern B contract codified. |
| 5 — ppy orchestrator feature parity | ⏳ deferred | Per-header `.rsp` lookup landed during Priority C (`rsp/per-header/*.rsp` overlays now feed the orchestrator). Companion-file manual-symbol exclusion feedback regex (`[Constant]` / `[Typedef]` markers) and full dynapi validation pass still open. Not on the critical path for satellite expansion. |
| **Item 1 — Per-Library Generation Infrastructure** | ✅ closed 2026-05-26 | All 7 slices (S1-1 through S1-7) shipped. Constitution edits landed; roster JSONs migrated to family-keyed schema 2.0; `ClongDualDispatchRewriter` Roslyn-mutated; `FlagsAttributeRewriter` alimer-style; oracle is 5-family. Determinism Contract D.1–D.5 PASS. AbiTests PASS (`792/792`). Slopwatch clean. |
| **Next — Iteration 2: Config Surface Unification** | ✅ closed 2026-05-27 | Unified into `config/family-config.json`. Python `FAMILY_CONFIG`/platform constants removed; C# postprocess hardcoded family switches eliminated; RSP identity duplication removed; 5 source files retired. Implementation on `spike/iteration-2-config-unification`, pending squash-land. Spec: [`items/iteration-2-config-surface-unification-spec.md`](items/iteration-2-config-surface-unification-spec.md). |
| **Item 2: SDL_image verification** | ✅ closed 2026-05-27 | All criteria satisfied by Item 1 S1-6 + Iteration 2 closure runs. `IMG_InitFlags` has `[Flags]` in both Modern and Compat; image-only regen byte-identical; oracle sdl2-image 0 new findings; multi-TFM build clean (5 TFMs). Documentation-only close — no code work. |
| **Items 3–5: GFX/TTF/Mixer Layer 1 completion** | ⏳ queued | Item 3 adds GFX; Item 4 adds TTF C `long` + `TTF_Font`; Item 5 adds Mixer callbacks + `Mix_Music`. Layer 2 public typed projection defers until all five families have stable Layer 1. |
| Layer 2 — typed public API | ⏳ Roadmap M5 deferred | Deferred until all five families (Core + Image + TTF + Mixer + GFX) have stable Layer 1 raw ABI, providing holistic view before designing the public projection. |
| 6+ — Friendly overloads | ⏳ Roadmap M6 | Out of scope until Layer 2 lands. |

## Current Evidence Snapshot — 2026-05-26 (Item 1 closure)

- Regeneration command: `python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims`.
- Regeneration result: `--family all`, `--family core`, and `--family image` all regenerate byte-stable output; `git diff --ignore-cr-at-eol` is empty for Core/Image `Generated/`. 7-step conceptual postprocess pipeline: `platform-delta` → `strip-varargs` → `libraryimport` (Modern only) → `flags-detect` → `guid-substitute` → `clong-dispatch` → `uniform-opaque`.
- Build command: `dotnet build spikes/binding-generators/clangsharp/Janset.SDL2.ClangSharpSpike.slnx -c Release`.
- Build result: solution compile clean with 0 warnings / 0 errors across postprocess + Core + Image + AbiTests TFMs.
- Oracle evidence: `dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- --family sdl2-core --family sdl2-image --family sdl2-ttf --family sdl2-mixer --family sdl2-gfx --write-report` writes [`../output/reports/oracle-evidence-clangsharp.md`](../output/reports/oracle-evidence-clangsharp.md). Core + Image have no raw ABI constitution findings; TTF/Mixer/GFX generated rows are intentionally missing until their expansion items activate.
- Runtime ABI smoke: `dotnet test --project spikes/binding-generators/clangsharp/tests/abi-tests/AbiTests.csproj -c Release` passes `792/792` across `net462`, `net8.0`, `net9.0`, and `net10.0` on Windows x64. Full 7-RID proof remains a production CI gate.
- Slopwatch: `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"` reports 0 issues.
- Known platform caveat: `--use-platform-header-shims` supplies minimal `endian.h`, `AvailabilityMacros.h`, and `TargetConditionals.h` from `clangsharp/shims/platform-headers/`. Use for Windows-local spike iteration; production Linux evidence comes from the binding-generator docker container (`docker/binding-generator.Dockerfile`).

## Review Follow-up Backlog — 2026-05-25

Eight read-only reviewer reports were triaged after the Priority C closure record. Those reports are useful raw evidence, but this section is the durable backlog sink so follow-ups do not disappear into temporary review files. Items below do not reopen the settled Layer 1 design direction unless explicitly marked as a handoff blocker.

### Handoff Blockers

Fix before treating the outgoing Spike C branch as clean handoff material.

| Item | Why it matters | Status |
| --- | --- | --- |
| Fail ClangSharp generation when any invocation fails | `generate_bindings.py` records ClangSharp failures but can still return success, so stale generated files can masquerade as a good run. | Fixed 2026-05-25: generation exit-code policy now returns 2 when any ClangSharp invocation fails; self-test covers the policy. The committed generation report is refreshed on the next clean regeneration. |
| Rewrite Pattern B handles inside callback function-pointer signatures, or lower raw callback slots to `nint` deliberately | Modern `SDL_SetWindowHitTest` exposed `delegate* unmanaged[Cdecl]<SDL_Window*, ...>` after `SDL_Window*` method parameters were rewritten to by-value Pattern B handles. That is pointer-sized at the wire level but semantically invites pointer-to-wrapper confusion. | Fixed 2026-05-25: `OpaqueHandleEmitRewriter` now rewrites function-pointer parameter/return slots; Modern `SDL_SetWindowHitTest` emits `delegate* unmanaged[Cdecl]<SDL_Window, ...>`. |
| Remove broken `docs/superpowers/...` references from durable docs and code comments | The Priority C design content now lives inline in the Constitution and closure docs; links to deleted/absent `docs/superpowers` files make future agents chase phantom authority. | Fixed 2026-05-25 for durable docs and code comments. Temporary review reports under `docs/temp/` intentionally keep their original reviewer text. |
| Correct evidence wording around runtime ABI coverage | The closure evidence covers ABI-family smoke, not full 7-RID proof. | Fixed 2026-05-25: AbiTests now include `SDL_GetThreadID(SDL_Thread.Null)` host coverage; docs say "ABI-family smoke now; 7-RID proof before production flip". |

### Production Flip Gates

These are not required to keep iterating on Layer 2, but they must be closed or consciously deferred before the generator becomes production source.

| Item | Why it matters | Target / evidence |
| --- | --- | --- |
| 7-RID runtime ABI matrix for retained C `long` symbols | The product contract is all supported RIDs, not only local Windows/Linux x64. Include `SDL_ThreadID` and `SDL_GetThreadID`; prefer a width/sign-sensitive assertion or native sentinel where practical. | `clangsharp/tests/abi-tests`; per-RID CI integration. |
| `SDL_GUID` ABI/value roundtrip smoke | `System.Guid` is sequential and 16 bytes on current .NET, but SDL GUID byte ordering and string semantics are user-visible. | Add `SDL_GUIDToString` / `SDL_GUIDFromString` roundtrip test and keep the Constitution note clear that `Guid.ToString()` is not SDL raw hex rendering. |
| Cross-assembly Pattern B runtime smoke through SDL_image | Compile proves `[DisableRuntimeMarshalling]` accepts Core-owned Pattern B handles in Image signatures, but one runtime satellite call would strengthen evidence. | `IMG_Init` / a low-risk Image function under AbiTests or a future package smoke. |
| Postprocess standalone/idempotency hardening | Several rewriters are correct for the current full pipeline but depend on ordering or source-text details. | Keep `ClongDualDispatchRewriter` self-contained for usings, replace `PlatformDeltaPostProcessor` source-text using insertion, and add a run-twice idempotency harness. |
| Validate SDL2 version in config against the active manifest | The config carries per-family `library_version`, but the postprocess does not enforce that it matches `build/manifest.json`. | Compare family `library_version` in `config/family-config.json` to `build/manifest.json` before owner-mode `uniform-opaque`; allow an explicit spike-only mismatch override if needed. |
| Correct RSP precedence docs and probe duplicate-key behavior | Current comments imply keyed `--remap` / `--with-type` entries can be overridden by later RSP tiers, but ClangSharp rejects duplicate keys. | `generate_bindings.py` comments/self-test and `rsp/per-header/README.md`; describe keyed entries as additive-only unless proven otherwise. |
| Improve evidence report UX | Oracle and generation reports should show failure/check counts directly instead of requiring inference from prose. | Render watched raw ABI checks as explicit `0 finding(s)` rows; derive generated-file counts from actual output; include failure count as a top-level field. |
| Audit Windows pointer-sized callback typedefs | `SDL_SetWindowsMessageHook` currently emits `uint, ulong, long` callback parameters; `WPARAM` / `LPARAM` are pointer-sized and win-x86 is in scope. | Add a platform-width sensor/follow-up before production flip. |
| Emit explicit enum backing for ABI-sensitive enums | `SDL_bool` is currently ABI-correct because C# enum default backing is `int`, but the Constitution says it must be int-backed. | Emit `public enum SDL_bool : int`; consider a general explicit-backing policy for generated enums where native backing is known. |
| Emit explicit `[StructLayout(LayoutKind.Sequential)]` on all generated data structs | ClangSharp emits `LayoutKind.Explicit` for unions but omits `LayoutKind.Sequential` for sequential data structs (`FPSmanager`, `SDL_Color`, `SDL_AudioSpec`, `SDL_Rect`, …). C# default for structs is `Sequential` in practice, and `[assembly: DisableRuntimeMarshalling]` prevents runtime reordering, but ECMA-335 treats layout without `[StructLayout]` as unspecified. Explicit `[StructLayout(LayoutKind.Sequential)]` is the interop hygiene posture taken by ppy, SDL2-CS, and Microsoft's P/Invoke best practices. | Roslyn postprocess rewriter: walk every `*StructDeclarationSyntax` without an existing `[StructLayout]` attribute and prepend `[StructLayout(LayoutKind.Sequential)]` + ensure `using System.Runtime.InteropServices;`. Universal cross-family policy — no per-family config. Evidenced by `FPSmanager` in GFX Item 3 (2026-05-27). |

### Layer 2 / Layer 3 Follow-ups

These belong after the Layer 1 raw ABI fix queue, mostly in public typed API or friendly-overload work.

| Item | Why it matters | Target / evidence |
| --- | --- | --- |
| Document Stage 1 `SDL_RWops` and `SDL_SysWM*` limitations in consumer-facing docs | Pattern B quarantine is the safe Stage 1 choice, but users need to know custom RWops and native window-manager info are deferred. | Preview docs / release notes / Layer 2 API docs. |
| Add HIDAPI wide-string decoders | Raw `wchar_t* -> nint` is ABI-honest, but consumers need platform-aware decoding helpers to read HID strings safely. | Layer 3 helper: Windows UTF-16, POSIX UTF-32 transcode. |
| Plan Stage 2 typed layout or helper strategy for `SDL_RWops` | Opaque Stage 1 blocks custom managed-backed RWops setup. | Either verified per-platform layout or a managed/native helper such as an `RWops` builder. |
| Guard future `SDL_GetWindowWMInfo` activation | `SDL_SysWMinfo` is not a pointer-like opaque object for that API; it requires caller-allocated concrete layout. | No `SDL_GetWindowWMInfo` emission unless the layout is verified per platform or projected through a deliberate platform-specific wrapper. |

### Accepted Tradeoffs / No Action

| Item | Decision |
| --- | --- |
| Explicit-only Pattern B handle conversion | Keep. This is deliberate type safety; implicit conversion can be added later if preview feedback shows real friction. |
| `VkSurfaceKHR` mapped through `nint` at the foreign boundary | Keep. This is the correct SDL/Vulkan boundary shape for the current raw signature. |
| `SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg` Pattern B quarantine | Keep for Stage 1. It avoids false cross-platform layouts. |
| `CallConvCdecl` on POSIX | Keep. It is the portable source-generated P/Invoke spelling for platform C ABI; add docs only if future reviewers keep tripping over the name. |

## Oracle Repair Queue — Status as of 2026-05-24

The family-aware oracle report originally documented three concrete gap groups. All three closed.

| Priority | Gap group | Status |
| --- | --- | --- |
| A | Raw ABI visibility and SDL2_image family identity | ✅ **Done** (`92b893b`). Core/Image raw ABI containers internal; Image emits under `SDL2.Image` namespace. |
| B | Missing required `SDL.h` functions and constants | ✅ **Done** (`444fada`). `SDL_Init`, `SDL_InitSubSystem`, `SDL_Quit`, `SDL_QuitSubSystem`, `SDL_WasInit`, and the ten `SDL_INIT_*` constants recovered without parsing the umbrella header. |
| C | Deferred layouts and platform-sensitive scalar mappings (`SDL_RWops`, `SDL_SysWMinfo`, `SDL_SysWMmsg`, `wchar_t`, C `long`, tag/typedef canonicalization, `SDL_GUID`) | ✅ **CLOSED 2026-05-24** (`e62bf92`). All six risks resolved across Slice C-A scalars + Slice C-B Pattern B uniform opaque + Slice C-C tag canonicalization + SDL_GUID. Closure record in [`priority-c-closure-summary.md`](priority-c-closure-summary.md) with full risk-resolution table, commit chain, and verification evidence. |

A-slice, B-slice, and C-slice designs all retired with their respective commits (`92b893b`, `444fada`, `e62bf92`). The Priority C closure summary doc is the authoritative record going forward.

## Slices


### Slice 1 — Layout restructure and library projects

**Goal:** Move from "two-flavour spike" layout (`clangsharp-style/` + `alimer-style/` + scattered `output/`) to production-shaped layout under `clangsharp/`.

**Target tree:**

```text
spikes/binding-generators/clangsharp/
├── generate_bindings.py                    # moved from clangsharp-style/
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
2. `git mv` the existing `clangsharp-style/{generate_bindings.py,rsp/}` into `clangsharp/` (preserves history).
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
- `python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute` regenerates into the new locations and cleans selected `Generated/` roots first.
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

### Slice 3 — Multi-OS parse pass (CLOSED 2026-05-22)

**Status:** Closed. `SDL_main.h` / `SDL_system.h` platform pass + `PlatformDeltaPostProcessor` dedupe + guarded `[SupportedOSPlatform]` all landed. Native Linux generation wired via the binding-generator docker container.

**Goal (historical):** ClangSharp's single neutral parse misses platform-only SDL2 functions (Cake oracle has 37 platform-specific functions our output skips). Adopt ppy's `generate_platform_specific_headers` pattern adapted for SDL2 macros, scoped tight by header-level evidence.

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
- Legacy oracle comparison was improved but still incomplete: the previous report showed 859 Core spike functions, 831/845 dynapi exports emitted, and 8 Cake-oracle functions still missing from the spike output.
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

1. **sdl.json source**: SDL2 has a similar mechanism via `gendynapi.pl` producing `SDL2.exports`. The legacy oracle-comparison path already parsed the exports file (`external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports`). Decide: either move that parser inline into `generate_bindings.py`, or call SDL2's gendynapi to produce a real `sdl2.json` for parity with ppy.
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

- [`priority-c-closure-summary.md`](priority-c-closure-summary.md) — **authoritative Priority C closure record** (2026-05-24): six risks resolution table, Foreign Type Boundary Policy, BCL-Replaceable Helper Exclusion Policy, Cross-Assembly Pattern B contract, full verification evidence.
- [`ppy-reference-analysis-2026-05-25.md`](ppy-reference-analysis-2026-05-25.md) — ppy/SDL3-CS reference analysis: satellite generation architecture, companion class layer mapping, adaptation recommendations.
- [`generator-spike-goals.md`](generator-spike-goals.md) — original charter and recorded decision.
- [`../output/reports/iteration-2-comparison.md`](../output/reports/iteration-2-comparison.md) — decision evidence (function counts, dynapi coherence, multi-TFM trajectory).
- [`../output/reports/clangsharp-failure-buckets.md`](../output/reports/clangsharp-failure-buckets.md) — RSP delta history from the 8 fix iterations.
- [`../output/reports/oracle-evidence-clangsharp.md`](../output/reports/oracle-evidence-clangsharp.md) — current family-aware raw ABI evidence snapshot; Core/Image are clean and dormant TTF/Mixer/GFX generated rows are missing as expected.
- `Review Follow-up Backlog — 2026-05-25` in this file — durable sink distilled from the eight read-only reviewer reports.
- [`../../../docs/binding-autogen/binding-generator-constitution.md`](../../../docs/binding-autogen/binding-generator-constitution.md) — Layer Contract L34-50 (three-layer API + DllImport/LibraryImport split), Opaque Handles L325-363 (Pattern B + cross-assembly contract), BCL-Replaceable Helper Exclusion Policy L169-208, C `long` L246-283, wchar_t L300-323, Structs And Unions L416-425, Foreign Type Boundary Policy L365-400, Evidence Gates L376-388, Macro pipeline L324-348.
- [`../../../docs/binding-autogen/binding-generator-roadmap.md`](../../../docs/binding-autogen/binding-generator-roadmap.md) — M4 (multi-TFM backends), M5 (public typed), M6 (friendly overloads), M7 (production flip).
- [`../../../docs/research/semantic-abi-type-classification-research.md`](../../../docs/research/semantic-abi-type-classification-research.md) (2026-05-22) — semantic ABI classification research backing for the six risks.
- [`../clangsharp/config/family-config.json`](../clangsharp/config/family-config.json) — unified family-keyed config (opaque handles, flags enums, C long methods, platform views, header inventories).
- ppy `references/ppy-SDL3-CS/SDL3-CS/generate_bindings.py` — north star for the orchestrator.
- ppy `references/ppy-SDL3-CS/SDL3-CS.SourceGeneration/FriendlyOverloadGenerator.cs` — reference for postprocess Syntax API patterns (note: ppy uses Roslyn SG; we use standalone console). Different runtime, same API surface.
