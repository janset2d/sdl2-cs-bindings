# Binding Autogen Workstream — LLM Onboarding

**Audience:** an LLM (or fresh human contributor) picking up the AST binding-generation workstream for Janset.SDL2/SDL3 with no prior conversation context.
**Date this onboarding reflects:** 2026-05-15.
**Maintainer:** Deniz İrgin (@denizirgin) — hobby project, sets the pace, communicates in Turkish + English.

## What This Document Is

You're joining a mid-workstream research effort that has converged on a concrete implementation plan. Two binding-generation toolchains were investigated, both validated end-to-end on a small SDL satellite (SDL2_gfx); CppAst additionally validated ppy-style neutral + platform-specific parsing plus SDL2_image satellite/shared-type topology. The strategy brief selects the CppAst path; a companion architecture design spec and a Stage 1 implementation plan are accepted alongside. ClangSharp remains the documented migration path.

The workstream went through a meaningful 2026-05-15 revision that this onboarding reflects. Read the "2026-05-15 Architectural Shifts" section below before the rest of the document — it tells you what changed and why, so you don't waste time learning superseded patterns.

This doc is your **15-minute orientation**: required reading list (in order), strategic anchors that won't change, open decisions that will. Read this first, then work through the required-reading list.

Don't propose architecture or write code before completing the required reading. Several earlier conversational threads explored paths that turned out to be wrong or incomplete (initial CppAst-vs-ClangSharp matrix needed row rescoring plus explicit weighting; topology refactor parked after closer review; a 2026-05-14 draft proposed a standalone `src/`-tree console app generator and mingw-w64 / Apple SDK stubs — all four retracted on 2026-05-15). The docs capture both the decisions and the reasoning. Skipping context = repeating the same mistakes.

## 2026-05-15 Architectural Shifts

The 2026-05-14 draft of the strategy brief was accepted, but four pieces of it were retracted on 2026-05-15 after closer review and external evidence. If you've been told something that contradicts what's below, the 2026-05-15 revision is the canonical answer; older claims should be treated as historical context, not active design.

1. **Generator hosted in the Cake build host, not a standalone src/-tree project.** The generator lives under `build/_build/Targets/GenerateBindings/` with cross-cutting validators under `build/_build/Validation/BindingGeneration/`. Pure emitter code (`HeaderSet/`, `Parsing/`, `Model/`, `Emitting/`, `Stamps/`) stays Cake-free; the Cake-aware shell (`GenerateBindingsTask`, `BindingGenerationRunner`, `ServiceCollectionExtensions`) owns Cake context, logging, and IO. Why: binding generation is a CI/CD concern; the Cake host already owns vcpkg state, manifest parsing, tool wrappers, validators, paths, and the TUnit + FakeCakeWorld test infrastructure. A parallel `src/`-tree orchestration stack would duplicate that surface and break ADR-002 §2.1's target-centric navigation rule. See the brief's WHY §"Why hosted in the Cake build host — not a standalone CLI tool" for the full five-reason argument.

2. **Linux-canonical determinism contract.** Only `libclang.runtime.linux-x64` + `libClangSharp.runtime.linux-x64` are pinned in the version trio — non-Linux runtime variants are intentionally absent. The `GenerateBindings` Cake target fails closed on any non-`linux-x64` host. Local invocation from Windows/macOS dev hosts routes through `tools.cs generate-bindings`, which provisions the pinned `linux-builder` Docker container automatically. Docker is a hard prerequisite; there is no host-OS fallback. Why: Layer 5 reproducibility (regenerate → byte-identical output) only holds when every parse view runs against the same OS, apt sysroot, and libclang runtime.

3. **Preprocessor-macro switching only — mingw-w64 / Apple SDK stub speculation retracted.** Platform separation uses `--undefine-macro` + `--define-macro` only. No `--target` cross-compile flag, no mingw-w64 cross-toolchain, no Apple SDK header stubs. SDL's public headers carry their own forward declarations for cross-platform opaque types (`typedef struct _NSWindow NSWindow;` at `SDL_syswm.h:86`, `typedef struct ANativeWindow ANativeWindow;` at `:105`, `struct gbm_device;` at `:120`, etc.). Verified against ppy/SDL3-CS Dockerfile + `generate_bindings.py` (WebFetch 2026-05-15) and the local CppAst spike at `tools/binding-spike/cppast/generator/Program.cs:42-72`. Neither uses mingw-w64; ppy's entire stub-header inventory is a single 81-byte `include/process.h` shim. See the brief's HOW §"Multi-pass parsing strategy" + Decision Audit Error 4 row.

4. **`SDL_syswm.h` typed-union layout deferred to Stage 2.** Stage 1 emits `SDL_GetWindowWMInfo` as a function with opaque `nint`-shaped `SDL_SysWMinfo*` parameter; `SDL_SysWMinfo` and `SDL_SysWMmsg` typed-union surface is recorded in `UnsupportedDeclarations.g.json` with category `deferred-to-stage-2`. Stage 2 introduces a small forward-declaration stub library (~15–20 opaque types: `HWND`, `HDC`, `HINSTANCE`, `IInspectable`, `Display*`, `Window`, `wl_display`/`wl_surface`/`xdg_*`, `gbm_device`, `IDirectFB*`, `EGLNativeDisplayType`, `ANativeWindow`) and emits the typed union with `[StructLayout(LayoutKind.Explicit, Size = 64)]` and platform branches at `[FieldOffset(0)]`, honoring the 64-byte size lock from `SDL_syswm.h:346-348`. Why: stub-library work and the typed union are a Stage 2 scope; Stage 1 already proves production-shape at function level (multi-pass orchestration, dedup, fail-closed merge, platform attribution, dual emit, typed handles, friendly overloads, AOT-clean signatures).

5. **SDL3 binding generation gated on PD-7.** SDL3 work does not begin until PD-7 (SDL2 real-public-release) ships. The SDL3 vcpkg port + overlay triplet + transitive dependency closure work is its own substantial scope and must not block SDL2 v1.0 stable. Phase 5 plan starts after Stage 2 ships and PD-7 lands.

The strategy brief's Decision Audit table records two retraction rows for these shifts: **Error 4 — toolchain stub strategy speculation (corrected 2026-05-15)** and **Reframe — generator hosted in Cake build host (2026-05-15)**. The brief's Plan Shape, Current Open Decisions, and WHAT impact inventory have all been re-aligned with these shifts.

## Project Context (One Paragraph)

Janset.SDL2 is a .NET 10 / C# 14 project providing modular SDL2 (and planned SDL3) bindings + cross-platform native binaries built from source via vcpkg, distributed as NuGet packages across 7 RIDs (`win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`). The current bindings layer is imported from `external/sdl2-cs/` (Ethan Lee's POC-grade SDL2-CS, deprecated upstream, marked "untrusted for production"). The AST workstream replaces this import with auto-generated bindings — same SDL2 surface, regenerable on SDL upstream bumps, and the precondition for SDL3 support (no upstream SDL3-CS covers our planned satellite scope). AST-first prioritization makes Phase 4 (binding generator) **critical path for v1.0 stable launch** — first public NuGet `-preview.N` wave should ship AST-generated bindings, not `external/sdl2-cs` imports.

## Required Reading — In This Order

Each doc builds on the previous. Don't skip; the later docs assume context from the earlier ones.

| # | Document | What it gives you | Time |
| --- | --- | --- | --- |
| 1 | [`AGENTS.md`](../../../AGENTS.md) | Operating rules, approval gate, communication style (Deniz's preferences), settled strategic decisions, test naming convention, build-host reference pattern | 10 min |
| 2 | [`release-strategy.md`](../../release-strategy.md) | Strategic anchor for v1.0 — end state, NuGet labeling, promotion gates, AST-first sequencing rationale, deferred decisions (topology parked) | 10 min |
| 3 | [`phase-4-binding-autogen.md`](../../phases/phase-4-binding-autogen.md) | Phase 4 design brief — the active phase this workstream belongs to | 5 min |
| 4 | [`binding-autogen-strategy-brief.md`](../binding-autogen-strategy-brief.md) | Accepted WHY/HOW/WHAT strategy brief (revised 2026-05-15). Read the new WHY §"Why hosted in the Cake build host" and the Decision Audit Error 4 / Reframe rows carefully. | 25 min |
| 5 | [`../../superpowers/specs/2026-05-14-binding-generator-architecture-design.md`](../../superpowers/specs/2026-05-14-binding-generator-architecture-design.md) | Architecture design spec (revised 2026-05-15). Cake-host component layout (`build/_build/Targets/GenerateBindings/`), platform catalog tuple model, pure-vs-Cake split. | 15 min |
| 6 | [`../../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`](../../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md) | Stage 1 implementation plan (revised 2026-05-15). Task-by-task TDD-style scaffolding under the Cake host. Read the revision header + Scope Decisions Locked section before the per-task detail. | 25 min |
| 7 | [`binding-autogen-approaches.md`](binding-autogen-approaches.md) | Toolchain comparison: industry survey of binding-generation tools, Decision Matrix Re-Validation (CppAst → ClangSharp flip), Source-Level Comparison of ppy/SDL3-CS vs Alimer.Bindings.SDL. Historical research; some claims about platform stubs superseded by 2026-05-15 retractions in the strategy brief Decision Audit. | 20 min |
| 8 | [`binding-autogen-feasibility.md`](binding-autogen-feasibility.md) | Feasibility study: 11 emit rules for modern .NET P/Invoke, headers + cross-platform parsing, hybrid-static + symbol visibility, manual intervention surface, 7-layer testing strategy, 11 open decisions (D1–D11) + 4 pending discussion threads. Historical research; mingw-w64 + Apple SDK stub framing was retracted on 2026-05-15. | 25 min |
| 9 | [`binding-autogen-spike-findings.md`](binding-autogen-spike-findings.md) | **Hands-on spike results** — both toolchains validated end-to-end on SDL2_gfx, side-by-side comparison vs hand-written external/sdl2-cs, 8 open questions for Phase 4 plan including the scope-trajectory bet (Q8). | 30 min |

Reading time total: ~165 minutes for proper internalization. Skim takes 45 minutes; you'll miss the decision audit trail and the Cake-host architecture rationale.

## Strategic Anchors (Won't Change)

These are settled. Don't relitigate.

| Decision | Source of truth |
| --- | --- |
| **Big-bang v1.0 covering SDL2 + SDL3 + all in-scope satellites**, AST-generated, distributed via nuget.org stable | [`release-strategy.md`](../../release-strategy.md) §End State at v1.0 |
| **AST-first prioritization** — Phase 4 (binding generator) lands BEFORE first public `-preview.N` wave; `external/sdl2-cs` retires when AST output validated | [`release-strategy.md`](../../release-strategy.md) §Sequencing |
| **Package topology refactor — DEFERRED** (research preserved in [`../parking-lot/package-topology/`](../../parking-lot/package-topology/) with unpark triggers) | [`release-strategy.md`](../../release-strategy.md) §Deferred Decisions |
| **D-3seg versioning + V1 family-lock** — `<UpstreamMajor>.<UpstreamMinor>.<FamilyPatch>`, all packages in a family share one version | [`../decisions/2026-05-05-d3seg-and-package-first.md`](../../decisions/2026-05-05-d3seg-and-package-first.md) (ADR-001) |
| **vcpkg-built hybrid-static natives across 7 RIDs** — strategy encoded in vcpkg overlay triplets | [`../../AGENTS.md`](../../../AGENTS.md) §Settled Strategic Decisions + [`../../vcpkg-overlay-triplets/_hybrid-common.cmake`](../../../vcpkg-overlay-triplets/_hybrid-common.cmake) |
| **Pride-driven, not deadline-driven** — quality bar > calendar; Deniz sets the pace, says when topics are done | [`release-strategy.md`](../../release-strategy.md) §Strategic Stance + memory `feedback_no_timing_pressure_or_motive_assumptions.md` |
| **Reference cross-check oracles** — `external/sdl2-cs` for SDL2, `ppy/SDL3-CS` for SDL3 — generator output diffed against these as typing references (NOT binding sources) | [`binding-autogen-approaches.md`](binding-autogen-approaches.md) §Validation Strategy |

## Current State (As of 2026-05-15)

### Where the code lives

- **Spike branch:** `spike/binding-autogen-sdl2-gfx` — both toolchains validated end-to-end with SDL2_gfx; this is the active workstream branch.
- **Spike artifacts root:** `tools/binding-spike/` (sibling subdirectories per toolchain) — preserved for reference even after Stage 1 lands in `build/_build/Targets/GenerateBindings/`.
- **Production target home (Stage 1 destination):** `build/_build/Targets/GenerateBindings/` + `build/_build/Validation/BindingGeneration/`. Not created yet — that is Stage 1 Task 1 work.
- **Master branch:** docs only at this point; spike branch carries the code and the doc revisions.

Files of interest (spike snapshot — line counts verified 2026-05-14; will drift):

```text
tools/binding-spike/
├── clangsharp/
│   ├── .config/dotnet-tools.json          ← ClangSharpPInvokeGenerator 21.1.8.3 pinned
│   ├── generator/sdl2-gfx.rsp              ← 43-line RSP declarative config
│   ├── bindings/
│   │   ├── Spike.Bindings.SDL2_gfx.csproj
│   │   ├── NativeTypeNameAttribute.cs      ← ClangSharp helper (consumer responsibility)
│   │   ├── Constants.cs                    ← M_PI duplicate workaround
│   │   └── Generated/SDL2_gfx.g.cs         ← 361 lines: 102 P/Invoke + FPSmanager + 8 const
│   └── test/
│       ├── Spike.TestApp.SDL2_gfx.csproj
│       └── Program.cs                       ← testframerate.c port, ~169 lines, SHARED
└── cppast/
    ├── generator/
    │   ├── generator.csproj
    │   └── Program.cs                       ← 261-line custom emitter (Windows-host spike;
    │                                          preprocessor-macro switching verified at lines 42-72)
    ├── bindings/
    │   ├── Spike.Bindings.SDL2_gfx.csproj
    │   └── Generated/SDL2_gfx.cs            ← 345 lines: 102 P/Invoke + FPSmanager + 8 const
    └── test/
        ├── Spike.TestApp.SDL2_gfx.csproj    ← links Program.cs from clangsharp/test via Compile Include
        └── (no own Program.cs)
```

The spike ran on a Windows host with C-stdlib shim headers (`stdint.h`, `stddef.h`, etc. — not platform-OS stubs). When the generator moves into the Linux-canonical Cake host in Stage 1, those C-stdlib shims disappear because the apt sysroot provides them natively. The preprocessor-macro switching pattern in `Program.cs:42-72` is the spike contribution that survived intact.

Versioning state (Stage 1 production scope): `Directory.Packages.props` will carry pinned CppAst 0.24.0 + libclang.runtime.linux-x64 20.1.2 + libClangSharp.runtime.linux-x64 20.1.2 (the version-trio coupling per `binding-autogen-spike-findings.md` §7.6). **Non-Linux runtime variants are intentionally absent** per the 2026-05-15 Linux-canonical lock. Don't bump these casually; CppAst 0.24.0 builds against libclang 20.1.x and newer libclang versions cause AST-visit stack-overflow at runtime.

### What works today

- ✅ Both toolchains generate working SDL2_gfx bindings (102 functions, FPSmanager struct, 8 constants)
- ✅ Both bindings compile clean as a standalone .NET 10 library
- ✅ Both test apps run end-to-end: open window, init framerate, draw bouncing circle + HUD, 180 frames @ 30 FPS, clean shutdown
- ✅ Native distribution via `Janset.SDL2.Core.Native` + `Janset.SDL2.Gfx.Native` packages' `buildTransitive` targets — zero manual DLL copy
- ✅ Mixed-toolchain interop: sdl2-cs SDL2 core (from `Janset.SDL2.Core` package) + spike-generated SDL2_gfx in the same consumer csproj — `IntPtr renderer` bridges both
- ✅ CppAst neutral + platform-specific pass spike works for SDL2 `SDL_system.h` / `SDL_main.h`: Windows and Linux symbols isolate correctly, neutral output stays clean, generated bindings compile, and the Linux pass proves the need for controlled sysroot/stub inputs when cross-target parsing from Windows
- ✅ CppAst SDL2_image shared-type spike works: image bindings compile separately while reusing core-owned SDL concepts through a core-types project, with no duplicate core type declarations in the satellite output

### Current toolchain stance

**Strategy decision:** CppAst is the selected Phase 4 planning direction. The strategy brief chooses it on the scope-trajectory argument: once multi-TFM dual emit, friendly overloads, typed handles, platform attribution, and satellite/shared-type topology are all in scope, CppAst's single C# emitter grows more linearly than a ClangSharp + RSP + post-process + Roslyn-extension stack.

The key drivers:

- Single-codebase emitter scales linearly to convenience-layer features (multi-TFM dual emit + friendly `string` overloads + `ReadOnlySpan<T>` overloads + `[SupportedOSPlatform]` attribution all in the same `foreach` loop)
- No separate Roslyn source generator project required for friendly overloads
- `[NativeTypeName]` C-provenance attribute (ClangSharp's strongest unique deliverable) is "nice to have" not "load-bearing" — debug/audit value, no runtime impact
- Custom-emission ceiling unbounded (matches Skia pattern if/when scope grows toward curated wrapper API on top of raw bindings)

**Generator home (2026-05-15 lock):** `build/_build/Targets/GenerateBindings/` inside the Cake build host, target-local per ADR-002 §2.4 until SDL3 creates a real second consumer. No standalone `src/`-tree console app. Pure emitter code (`HeaderSet/`, `Parsing/`, `Model/`, `Emitting/`, `Stamps/`) stays Cake-free; the Cake-aware shell (`GenerateBindingsTask`, `BindingGenerationRunner`, `ServiceCollectionExtensions`) owns Cake context, logging, and IO.

ClangSharp remains the documented migration path if CppAst's version-trio coupling or owned-emitter cost becomes painful in practice. The research docs preserve the raw-binding economics case for ClangSharp so that migration does not require re-discovering the evidence.

### Platform-pass research update (current, post 2026-05-15)

Platform-conditioned parsing is a first-class spike concern. This is not a CppAst or ClangSharp feature distinction; both sit on libclang, where each parse produces the AST for one macro/target/include configuration. Local SDL2 headers contain real platform-gated public declarations in `SDL_system.h`, `SDL_main.h`, `SDL_platform.h`, `SDL_config.h`, `SDL_stdinc.h`, and `SDL_syswm.h`.

**Pattern: preprocessor-macro switching only — no `--target`, no mingw-w64, no Apple SDK.** Each pass undefines every SDL platform identification macro, then defines exactly one `(OsCondition, BackendCondition[])` tuple's macros. SDL's public headers carry the cross-platform opaque-type forward declarations the parser needs (`typedef struct _NSWindow NSWindow;` at `SDL_syswm.h:86`, similar for `UIWindow`, `ANativeWindow`, `gbm_device`). Function-level platform surface parses without any hand-written platform stubs. Verified against:

- **ppy/SDL3-CS** Dockerfile + `generate_bindings.py` (WebFetch 2026-05-15) — Ubuntu 24.04 container, no mingw-w64, single 81-byte `include/process.h` shim, preprocessor `--define-macro`/`--undefine-macro` orchestration.
- **Local CppAst spike** `tools/binding-spike/cppast/generator/Program.cs:42-72` — `Defines`/`Undefines` macro juggling for `_WIN32`/`linux`/`__MACOSX__` triplet, no `--target`, parsed `SDL_system.h` + `SDL_main.h` successfully across all three platforms.

Stage 1's `PlatformCatalog` is a ~8-entry `(OsCondition, BackendCondition[])` tuple model: Neutral + Windows desktop + WinRT + GDK + Linux + macOS + iOS + Android. Backends compile-enabled on each OS (Linux's X11/Wayland/KMSDRM) are bundled into that OS's pass. DirectFB / Vivante / MIR / OS-2 are documented Stage 1 exclusions. The master "undefine-all-platform-macros" hygiene list (~30 macros) is used per-pass to ensure clean isolation; pass count equals catalog size, not the hygiene list size.

Repository comparison reference set:

- **ppy/SDL3-CS** is the strongest SDL-specific platform-pass reference: neutral pass plus platform-specific passes via preprocessor macros only.
- **SkiaSharp** remains a strong CppAst discipline reference, but not a platform-split reference because Skia's C API is platform-neutral.
- **Alimer.Bindings.SDL** is useful for C# output shape ideas, but its single-pass union of multiple platform macros is a cautionary pattern, not our target.
- **Silk.NET** uses a single Windows runner pinned to specific Visual Studio Build Tools — heavyweight, not applicable to our SDL focus.
- **bottlenoselabs/SDL3-cs** uses native multi-OS runners + merge — documented escalation path if single-host preprocessor-macro switching ever fails to model a future SDL header surface.

The CppAst platform-pass spike validated the ppy-style neutral + platform-specific model for selected SDL2 headers; `SDL_syswm.h` typed-union layout was intentionally deferred and now sits in Stage 2. The SDL2_image shared-type spike validated the satellite/core-types boundary for Stage 2's SDL2 satellite sweep.

## Open Decisions — Status After 2026-05-15

Many of the open decisions originally captured here have been resolved by the strategy brief, design spec, and Stage 1 plan. The table below shows the current status. Decisions still genuinely open are in the rightmost column; treat the rest as closed unless code execution surfaces evidence to reopen.

| Decision | Original source | Status as of 2026-05-15 |
| --- | --- | --- |
| D1 vendor vs vcpkg headers | feasibility §9 | **Closed** — vcpkg-installed canonical headers (linux-x64-hybrid triplet inside container). |
| D2 multi-platform parsing strategy | feasibility §9 | **Closed** — preprocessor-macro switching only, no `--target`, no stubs at function level. See brief HOW §"Multi-pass parsing strategy". |
| D3 cross-check tool | feasibility §9 | **Stage 2** — reference cross-check against external/sdl2-cs (SDL2) lands at Stage 2 satellite sweep. |
| D4 symbol-existence guardrail | feasibility §9 | **Stage 2** — Pack-stage `BindingSymbolExistenceValidator` lands at Stage 2; Stage 1 preserves `EntryPoint` metadata for that later validator. |
| D5 `IsAotCompatible` | feasibility §9 | **Closed** — `IsAotCompatible=true` on net8+ generated csprojs per brief HOW Rule 9. |
| D6 computed macros | feasibility §9 | **Closed** — `public const` for literal values, `public static readonly` for computed expressions per brief HOW Rule 8. |
| D7 variadic functions | feasibility §9 | **Closed** — classified in `KnownUnsupportedDeclarationPolicy` and omitted with audit entry; fmt-only friendly wrappers for common logging calls per Stage 1 plan. |
| D8 generated output check-in | feasibility §9 | **Closed** — committed to git per ADR-004 + brief HOW. |
| D9 multi-TFM emission | feasibility §9 | **Closed** — dual `[LibraryImport]` (net7+) + `[DllImport]` (legacy) emit in the same loop, full TFM matrix. |
| D10 Roslyn source generator | feasibility §9 | **Closed (N/A)** — CppAst path doesn't require it; only relevant on ClangSharp migration. |
| D11 PowerShell vs Python orchestrator | feasibility §9 | **Closed (N/A)** — Cake-host fold makes external orchestrators unnecessary; tools.cs (C# file-based app) is the local orchestration entry. |
| Q1 LibraryImport conversion path | spike findings §9 | **Closed** — dual emit. |
| Q2 Docker layer when? | spike findings §9 | **Closed** — Stage 1, Linux-canonical. Docker hard prereq from day one. |
| Q3 friendly overload strategy | spike findings §9 | **Closed** — `string` / `ReadOnlySpan<byte>` / `Span<T>` / `out` / `ref` in the same emitter loop. |
| Q4 platform-specific parsing pass | spike findings §9 | **Closed** — `(OsCondition, BackendCondition[])` tuple `PlatformCatalog`, ~8 entries for Stage 1 SDL2.Core. |
| Q5 multi-TFM emission specifics | spike findings §9 | **Closed** — see D9 + brief HOW Rule 1. |
| Q6 missing-macros investigation | spike findings §9 | **Closed** — Error 1 corrected; both toolchains capture all 8 SDL2_gfx constants with `--config generate-macro-bindings`. |
| Q7 wrapper layer (`SdlWindow : IDisposable`) | spike findings §9 | **Deferred past v1.0** — explicit reopen if real consumer feedback says raw + friendly overloads aren't enough. |
| Q8 scope-trajectory bet | spike findings §9 | **Resolved** — CppAst selected, ClangSharp documented migration path per ADR-004. |
| `SDL_syswm.h` typed-union layout | brief §Multi-pass parsing | **Stage 2** — Stage 1 emits opaque pointer only. |
| SDL3 binding generation start | brief §Plan Shape | **Gated on PD-7** (SDL2 real-public-release). Stage 3 work begins after Stage 2 ships and PD-7 lands. |

Genuinely open Stage 1 plan-level decisions live in the Stage 1 plan itself — read it for the per-task scope. Genuinely open Stage 2 plan-level decisions wait until Stage 1 implementation ships (Stage 2 plan is written against actual Stage 1 code shape, not speculatively).

## How to Continue

### If Deniz directs you to executing Stage 1

Read [`../../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`](../../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md) and start at Task 1. The plan is task-by-task TDD-style. Approval gate is a hard rule: present each commit summary + proposed message to Deniz before committing. Skill `superpowers:subagent-driven-development` or `superpowers:executing-plans` is the recommended sub-skill for working through the plan.

### If Deniz directs you to authoring the Stage 2 plan

Wait until Stage 1 implementation ships first. The Stage 2 plan is written against actual Stage 1 code shape (folder names, type names, validator wiring), not speculatively. The strategy brief's Plan Shape Stage 2 section lists the scope: `SDL_syswm.h` full union + forward-declaration stub library + satellite sweep (Image, Mixer, Ttf, Gfx, Net) + `external/sdl2-cs` retirement. When the time comes, read [`../parking-lot/package-topology/phase-planning-methodology.md`](../../parking-lot/package-topology/phase-planning-methodology.md) for plan-authoring conventions (the methodology survives even though the topology refactor is parked).

### If Deniz directs you to more experimentation

The spike branch is live. New experiments under `tools/binding-spike/` are fine, but **don't replicate Stage 1 plan work** — implement Stage 1 in `build/_build/Targets/GenerateBindings/` instead. Useful spike work:

- **Wrapper layer prototype:** explore `SdlWindow : IDisposable` shape on top of typed handle baseline if there's real consumer pressure (Q7 deferred).
- **Reference cross-check tool prototype:** sketch the diff tool for Stage 2 (D3 / feasibility §10.4) so the Stage 2 plan inherits a working pattern.

### If Deniz directs you to a different area entirely

Respect the redirect; don't push the AST workstream forward unilaterally. He sets the pace. Per the memory file `feedback_no_timing_pressure_or_motive_assumptions.md`: don't ask "when do we get back to AST?" — he'll signal.

## Working With Deniz — Communication Notes

Lifted from existing memory files (read these for full context):

- **[`feedback_session_pacing`]** — After a slice commits, default to "what's next?" not "let me wrap up." Don't write priming prompts unilaterally.
- **[`feedback_skip_micro_checkpoints`]** — Batch routine task completions silently. Stop only at significant boundaries.
- **[`feedback_follow_explicit_paths`]** — When Deniz names a path/folder, use it exactly. Never silently reroute to a discovered "better fit" — ask first.
- **[`feedback_no_timing_pressure_or_motive_assumptions`]** — Don't push timing ("when do we?", "are we ready?"). Don't presume decision criteria. He sets the pace and says when a topic is done.
- **[`feedback_plan_doc_references`]** — Every plan/phase/refactor doc must cross-reference relevant ADRs + knowledge-base guidelines.

Communication style per AGENTS.md: bilingual TR/EN, conversational, Gen Y vibe with practical humor, challenge decisions when needed but don't perform-agreement when you disagree. Approval gate is a hard rule — no code changes without explicit "go / apply / proceed / başla / yap."

## Document Map — All AST Workstream Research

In chronological-creation order (which approximates intellectual-buildup order):

| Date | Document | Status |
| --- | --- | --- |
| 2026-04-11 | [`binding-autogen-approaches.md`](binding-autogen-approaches.md) (initial) | Active; enriched 2026-05-12 with toolchain flip + source-level comparison. Pre-2026-05-15 content; see strategy brief Decision Audit for corrections. |
| 2026-05-12 | [`release-strategy.md`](../../release-strategy.md) | Active strategic anchor |
| 2026-05-12 | [`binding-autogen-feasibility.md`](binding-autogen-feasibility.md) | Active design-feasibility doc; pre-2026-05-15 mingw-w64 / Apple-SDK speculation superseded by strategy brief Error 4 retraction. |
| 2026-05-12 | [`binding-autogen-spike-findings.md`](binding-autogen-spike-findings.md) | Active hands-on spike findings (ClangSharp + CppAst). |
| 2026-05-12 | This doc — [`binding-autogen-onboarding.md`](binding-autogen-onboarding.md) | LLM onboarding (revised 2026-05-15). |
| 2026-05-14 | [`binding-autogen-strategy-brief.md`](../binding-autogen-strategy-brief.md) | Accepted strategy brief; revised 2026-05-15 (Cake-host fold, Linux-canonical, stub retraction, SysWM Stage 2 deferral, SDL3 PD-7 gating). |
| 2026-05-14 | [`ADR-004`](../../decisions/2026-05-14-binding-autogen-toolchain.md) | Durable toolchain decision |
| 2026-05-14 | [`../../superpowers/specs/2026-05-14-binding-generator-architecture-design.md`](../../superpowers/specs/2026-05-14-binding-generator-architecture-design.md) | Architecture design spec; revised 2026-05-15 for Cake-host component layout + Linux-canonical + SysWM Stage 2 defer. Retires when Stage 1 ships. |
| 2026-05-14 | [`../../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md`](../../superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md) | Stage 1 implementation plan; revised 2026-05-15 for Cake-host fold + 8-entry PlatformCatalog + in-process pipeline + tools.cs Docker orchestration. Retires when Stage 1 ships. |

Related canonical references:

- [`AGENTS.md`](../../../AGENTS.md) — operating rules
- [`release-strategy.md`](../../release-strategy.md) — strategic anchor
- [`plan.md`](../../plan.md) — tactical roadmap
- [`onboarding.md`](../../onboarding.md) — project framing + glossary
- [`phase-4-binding-autogen.md`](../../phases/phase-4-binding-autogen.md) — Phase 4 design brief
- [`phase-5-sdl3-support.md`](../../phases/phase-5-sdl3-support.md) — Phase 5 SDL3 brief
- [`ADR-001`](../../decisions/2026-05-05-d3seg-and-package-first.md) — D-3seg + package-first
- [`ADR-002`](../../decisions/2026-05-05-target-centric-build-host.md) — target-centric build host (the pattern the Cake-host fold inherits)
- [`ADR-003`](../../decisions/2026-05-12-build-host-data-layer.md) — contract-centric data layer (the pattern the binding validators extend)
- [`ADR-004`](../../decisions/2026-05-14-binding-autogen-toolchain.md) — binding autogen toolchain
- [`release-guardrails.md`](../../knowledge-base/release-guardrails.md) — guardrail catalog
- [`testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — test infra + fixture policy
- [`extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) — collaborator extraction policy
- [`parking-lot.md`](../../parking-lot.md) — parked single-line items
- [`parking-lot/package-topology/`](../../parking-lot/package-topology/) — parked multi-file research (topology refactor)

External references:

- [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL) — CppAst reference implementation (SDL3 core only)
- [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS) — ClangSharp reference implementation (SDL3 + Image + Mixer + TTF — same scope we plan). Dockerfile + `generate_bindings.py` are the closest production peers for our preprocessor-macro-only multi-pass orchestration.
- [dotnet/Silk.NET](https://github.com/dotnet/Silk.NET) — alternative ClangSharp pipeline (heavyweight, multi-graphics-lib scope, Windows-runner-bound)
- [bottlenoselabs/SDL3-cs](https://github.com/bottlenoselabs/SDL3-cs) — native multi-OS runners + merge; documented escalation path if single-host preprocessor-macro switching ever fails to model a future SDL header surface.
- [xoofx/CppAst](https://github.com/xoofx/CppAst) — CppAst NuGet
- [dotnet/ClangSharp](https://github.com/dotnet/ClangSharp) — ClangSharp + ClangSharpPInvokeGenerator
- [giroletm/SDL2_gfx test/](https://github.com/giroletm/SDL2_gfx/tree/master/test) — C test apps ported to spike test/Program.cs

## You're Ready

Start with `AGENTS.md`, read the strategy brief and ADR-004 before the research docs, then read the design spec + Stage 1 plan to see how the strategy lands in code. Use `binding-autogen-spike-findings.md` for the empirical evidence. By then you'll have the context to execute Stage 1 (or plan Stage 2 after Stage 1 ships). If anything in the existing docs contradicts itself or seems wrong, raise it explicitly — don't quietly route around it. The strategy brief's Decision Audit is your reference for what was corrected and why.
