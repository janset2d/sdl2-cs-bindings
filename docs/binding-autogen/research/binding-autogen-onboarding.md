# Binding Autogen Workstream — LLM Onboarding

**Audience:** an LLM (or fresh human contributor) picking up the AST binding-generation workstream for Janset.SDL2/SDL3 with no prior conversation context.
**Date this onboarding reflects:** 2026-05-14.
**Maintainer:** Deniz İrgin (@denizirgin) — hobby project, sets the pace, communicates in Turkish + English.

## What This Document Is

You're joining a mid-workstream research effort. Two binding-generation toolchains have been investigated, both validated end-to-end on a small SDL satellite (SDL2_gfx), and CppAst has now validated ppy-style neutral + platform-specific parsing plus SDL2_image satellite/shared-type topology. The current strategy brief selects the CppAst path for Phase 4 planning while preserving ClangSharp as the migration path. This doc is your **10-minute orientation**: required reading list (in order), strategic anchors that won't change, open decisions that will. Read this first, then work through the required-reading list.

Don't propose architecture or write code before completing the required reading. Several earlier conversational threads explored paths that turned out to be wrong or incomplete (initial CppAst-vs-ClangSharp matrix needed row rescoring plus explicit weighting; topology refactor parked after closer review); the docs capture both the decisions and the reasoning. Skipping context = repeating the same mistakes.

## Project Context (One Paragraph)

Janset.SDL2 is a .NET 10 / C# 14 project providing modular SDL2 (and planned SDL3) bindings + cross-platform native binaries built from source via vcpkg, distributed as NuGet packages across 7 RIDs (`win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`). The current bindings layer is imported from `external/sdl2-cs/` (Ethan Lee's POC-grade SDL2-CS, deprecated upstream, marked "untrusted for production"). The AST workstream replaces this import with auto-generated bindings — same SDL2 surface, regenerable on SDL upstream bumps, and the precondition for SDL3 support (no upstream SDL3-CS covers our planned satellite scope). AST-first prioritization makes Phase 4 (binding generator) **critical path for v1.0 stable launch** — first public NuGet `-preview.N` wave should ship AST-generated bindings, not `external/sdl2-cs` imports.

## Required Reading — In This Order

Each doc builds on the previous. Don't skip; the later docs assume context from the earlier ones.

| # | Document | What it gives you | Time |
| --- | --- | --- | --- |
| 1 | [`AGENTS.md`](../../../AGENTS.md) | Operating rules, approval gate, communication style (Deniz's preferences), settled strategic decisions, test naming convention, build-host reference pattern | 10 min |
| 2 | [`release-strategy.md`](../../release-strategy.md) | Strategic anchor for v1.0 — end state, NuGet labeling, promotion gates, AST-first sequencing rationale, deferred decisions (topology parked) | 10 min |
| 3 | [`phase-4-binding-autogen.md`](../../phases/phase-4-binding-autogen.md) | Phase 4 design brief — the active phase this workstream belongs to | 5 min |
| 4 | [`binding-autogen-strategy-brief.md`](../binding-autogen-strategy-brief.md) | Accepted WHY/HOW/WHAT strategy brief selecting the CppAst path for Phase 4 planning | 20 min |
| 5 | [`binding-autogen-approaches.md`](binding-autogen-approaches.md) | Toolchain comparison: industry survey of binding-generation tools, Decision Matrix Re-Validation (CppAst → ClangSharp flip), Source-Level Comparison of ppy/SDL3-CS vs Alimer.Bindings.SDL | 20 min |
| 6 | [`binding-autogen-feasibility.md`](binding-autogen-feasibility.md) | Feasibility study: 11 emit rules for modern .NET P/Invoke, headers + cross-platform parsing, hybrid-static + symbol visibility, manual intervention surface, 7-layer testing strategy, 11 open decisions (D1–D11) + 4 pending discussion threads | 25 min |
| 7 | [`binding-autogen-spike-findings.md`](binding-autogen-spike-findings.md) | **Hands-on spike results** — both toolchains validated end-to-end on SDL2_gfx, side-by-side comparison vs hand-written external/sdl2-cs, 8 open questions for Phase 4 plan including the scope-trajectory bet (Q8) | 30 min |

Reading time total: ~100 minutes for proper internalization. Skim takes 30 minutes; you'll miss the decision audit trail.

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

## Current State (As of 2026-05-14)

### Where the code lives

- **Spike branch:** `spike/binding-autogen-sdl2-gfx` — both toolchains validated end-to-end with SDL2_gfx
- **Spike artifacts root:** `tools/binding-spike/` (sibling subdirectories per toolchain)
- **Master branch:** docs only at this point; spike branch carries the code

Files of interest:

```text
tools/binding-spike/                         ← line counts verified 2026-05-14; will drift
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
    │   └── Program.cs                       ← 261-line custom emitter
    ├── bindings/
    │   ├── Spike.Bindings.SDL2_gfx.csproj
    │   └── Generated/SDL2_gfx.cs            ← 345 lines: 102 P/Invoke + FPSmanager + 8 const
    └── test/
        ├── Spike.TestApp.SDL2_gfx.csproj    ← links Program.cs from clangsharp/test via Compile Include
        └── (no own Program.cs)
```

Versioning state: `Directory.Packages.props` carries pinned CppAst 0.24.0 + libclang.runtime.win-x64 20.1.2 + libClangSharp.runtime.win-x64 20.1.2 (the version-trio coupling per `binding-autogen-spike-findings.md` §7.6). Don't bump these casually; CppAst 0.24.0 builds against libclang 20.1.x and newer libclang versions cause AST-visit stack-overflow at runtime.

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

ClangSharp remains the documented migration path if CppAst's version-trio coupling or owned-emitter cost becomes painful in practice. The research docs preserve the raw-binding economics case for ClangSharp so that migration does not require re-discovering the evidence.

### Platform-pass research update (2026-05-14)

Platform-conditioned parsing is now a first-class spike concern. This is not a CppAst or ClangSharp feature distinction; both sit on libclang, where each parse produces the AST for one macro/target/include configuration. Local SDL2 headers contain real platform-gated public declarations in `SDL_system.h`, `SDL_main.h`, `SDL_platform.h`, `SDL_config.h`, `SDL_stdinc.h`, and `SDL_syswm.h`.

Repository comparison changed the reference set:

- **ppy/SDL3-CS** is the strongest SDL-specific platform-pass reference: neutral pass plus platform-specific passes.
- **SkiaSharp** remains a strong CppAst discipline reference, but not a platform-split reference because Skia's C API is platform-neutral.
- **Alimer.Bindings.SDL** is useful for C# output shape ideas, but its single-pass union of multiple platform macros is a cautionary pattern, not our target.
- **Silk.NET** proves why Windows SDK / DirectX generation may require a Windows runner; it does not prove SDL generation needs one.

The CppAst platform-pass spike validated the ppy-style neutral + platform-specific model for selected SDL2 headers. The SDL2_image shared-type spike validated the next boundary: satellite outputs can be generated separately while reusing core-owned SDL concepts instead of duplicating `SDL_Surface`, `SDL_Texture`, `SDL_Renderer`, `SDL_RWops`, and `SDL_version`. Remaining high-value Stage 1/2 plan items are macro alias emission (`IMG_GetError` / `IMG_SetError`), field-level `IMG_Animation` layout, full package-consumer runtime smoke with harvested natives, and then either SDL2_ttf or SDL2_mixer if we want a callback/font/audio-heavy satellite before the SDL2 satellite sweep.

## Open Decisions for Phase 4 Plan

Captured from both feasibility study and spike findings. The Phase 4 plan-authoring slice will work through these.

### From `binding-autogen-feasibility.md` §9

D1 vendor vs vcpkg headers · D2 multi-platform parsing strategy · D3 cross-check tool · D4 symbol-existence guardrail · D5 `IsAotCompatible` · D6 computed macros · D7 variadic functions · D8 generated output check-in · D9 multi-TFM emission · D10 Roslyn source generator (if ClangSharp wins) · D11 PowerShell vs Python orchestrator

### From `binding-autogen-spike-findings.md` §9

Q1 LibraryImport conversion path · Q2 Docker layer when? · Q3 friendly overload strategy · Q4 platform-specific parsing pass · Q5 multi-TFM emission specifics · Q6 missing-macros investigation · Q7 wrapper layer yes/no/later · **Q8 scope-trajectory bet (CppAst single-emitter elasticity vs ClangSharp smaller raw-binding setup)**

### From `binding-autogen-feasibility.md` §10 Pending Discussion Threads

10.1 D1–D11 triage · 10.2 modern P/Invoke deep-dive (SDL2/SDL3 boolean wire types, callback lifetime, multi-TFM strategy, `[Flags]` attribution) · 10.3 testing strategy deep-dive (Layer 4 symbol-existence guardrail design) · 10.4 reference cross-check tool design

## How to Continue

### If Deniz directs you to authoring the Phase 4 implementation plan

Read [`../parking-lot/package-topology/phase-planning-methodology.md`](../../parking-lot/package-topology/phase-planning-methodology.md) for the per-phase plan-authoring convention — even though topology refactor is parked, the methodology itself remains the reference for how detailed plan docs are authored in this project. Apply same discipline: reference matrix to ADRs + knowledge-base, slice-by-slice scope, exit criteria per slice.

### If Deniz directs you to more experimentation

The spike branch is live. Continue under `tools/binding-spike/` with new sub-features:

- **Multi-TFM dual emit:** add `#if NET7_0_OR_GREATER` guards in CppAst's `Program.cs` emit loop (or write a ClangSharp post-process pass). Verify generated code compiles for net10 + netstandard2.0 + net462.
- **Friendly string overloads:** add `EmitFriendlyStringOverload` to CppAst's loop (or fork ppy's `FriendlyOverloadGenerator` Roslyn extension for ClangSharp side). Test that `pixelColor(renderer, x, y, "label")` works without manual UTF-8 encoding.
- **Satellite/shared-type topology follow-up:** SDL2_image compile topology is validated; add macro alias emission and runtime image asset smoke once local harvested natives are available.
- **`SDL_syswm.h` platform layout:** handle or intentionally defer platform-specific struct/union layout after the function-only platform-pass spike.

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
| 2026-04-11 | [`binding-autogen-approaches.md`](binding-autogen-approaches.md) (initial) | Active; enriched 2026-05-12 with toolchain flip + source-level comparison |
| 2026-05-12 | [`release-strategy.md`](../../release-strategy.md) | Active strategic anchor |
| 2026-05-12 | [`binding-autogen-feasibility.md`](binding-autogen-feasibility.md) | Active design-feasibility doc |
| 2026-05-12 | [`binding-autogen-spike-findings.md`](binding-autogen-spike-findings.md) | Active hands-on spike findings (ClangSharp + CppAst) |
| 2026-05-12 | This doc — [`binding-autogen-onboarding.md`](binding-autogen-onboarding.md) | LLM onboarding |
| 2026-05-14 | [`binding-autogen-strategy-brief.md`](../binding-autogen-strategy-brief.md) | Accepted strategy brief selecting the CppAst direction |
| 2026-05-14 | [`ADR-004`](../../decisions/2026-05-14-binding-autogen-toolchain.md) | Durable toolchain decision |

Related canonical references:

- [`AGENTS.md`](../../../AGENTS.md) — operating rules
- [`release-strategy.md`](../../release-strategy.md) — strategic anchor
- [`plan.md`](../../plan.md) — tactical roadmap
- [`onboarding.md`](../../onboarding.md) — project framing + glossary
- [`phase-4-binding-autogen.md`](../../phases/phase-4-binding-autogen.md) — Phase 4 design brief
- [`phase-5-sdl3-support.md`](../../phases/phase-5-sdl3-support.md) — Phase 5 SDL3 brief
- [`ADR-001`](../../decisions/2026-05-05-d3seg-and-package-first.md) — D-3seg + package-first
- [`ADR-002`](../../decisions/2026-05-05-target-centric-build-host.md) — target-centric build host
- [`ADR-003`](../../decisions/2026-05-12-build-host-data-layer.md) — contract-centric data layer
- [`ADR-004`](../../decisions/2026-05-14-binding-autogen-toolchain.md) — binding autogen toolchain
- [`release-guardrails.md`](../../knowledge-base/release-guardrails.md) — guardrail catalog
- [`testing-guidelines.md`](../../knowledge-base/testing-guidelines.md) — test infra + fixture policy
- [`extraction-guidelines.md`](../../knowledge-base/extraction-guidelines.md) — collaborator extraction policy
- [`parking-lot.md`](../../parking-lot.md) — parked single-line items
- [`parking-lot/package-topology/`](../../parking-lot/package-topology/) — parked multi-file research (topology refactor)

External references:

- [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL) — CppAst reference implementation (SDL3 core only)
- [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS) — ClangSharp reference implementation (SDL3 + Image + Mixer + TTF — same scope we plan)
- [dotnet/Silk.NET](https://github.com/dotnet/Silk.NET) — alternative ClangSharp pipeline (heavyweight, multi-graphics-lib scope)
- [xoofx/CppAst](https://github.com/xoofx/CppAst) — CppAst NuGet
- [dotnet/ClangSharp](https://github.com/dotnet/ClangSharp) — ClangSharp + ClangSharpPInvokeGenerator
- [giroletm/SDL2_gfx test/](https://github.com/giroletm/SDL2_gfx/tree/master/test) — C test apps ported to spike test/Program.cs

## You're Ready

Start with `AGENTS.md`, read the strategy brief and ADR-004 before the research docs, then use `binding-autogen-spike-findings.md` for the empirical evidence. By then you'll have the context to make Phase-4-plan-grade decisions. If anything in the existing docs contradicts itself or seems wrong, raise it explicitly — don't quietly route around it.
