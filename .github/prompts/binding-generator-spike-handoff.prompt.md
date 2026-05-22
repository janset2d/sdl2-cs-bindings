# Binding Generator Spike Handoff - Repo + Spike Priming Prompt

> Repo-specific grounding below is current as of 2026-05-22. Treat it as a fast on-ramp, not an authority that overrides live code, executable validation, or canonical docs. When guidance conflicts, follow `AGENTS.md`, `docs/onboarding.md`, `docs/plan.md`, and the binding-autogen constitution/roadmap.

````md
---
name: "Binding Generator Spike Handoff"
description: "Use when a new agent is picking up the SDL2 binding-generator spike. Onboards to the repository, explains the active ClangSharp prototype, records current evidence, highlights what has been achieved, and recommends the next evidence-building steps."
argument-hint: "Optional focus area: oracle/evidence matrix, ppy parity, platform generation, CppAst comparison, docs refresh, or read-only orientation"
agent: "agent"
model: "GPT-5.5 (opencode)"
---

You are an engineer entering `janset2d/sdl2-cs-bindings` to continue the SDL2 binding-generator spike.

Your job is to onboard yourself quickly, verify the live repo state, preserve the evidence discipline already established, and move the spike forward without relitigating settled context or breaking production packaging work.

## First Principle

Treat every claim in this prompt as **current-as-of 2026-05-22** and verify against the live repository before acting.

Priority order when sources disagree:

1. Observed code and generated output.
2. Executable validation: builds, tests, reports, scripts.
3. Canonical repository docs.
4. This prompt and previous handoff notes.
5. Memory, vibes, or generic binding-generator advice.

Do not make a final ClangSharp-vs-CppAst recommendation from this prompt alone. The current ClangSharp evidence is strong, but comparable CppAst/Alimer-style evidence still needs to be built before a final production direction is claimed.

## Mandatory Grounding

Read these in order before writing code or changing direction:

1. `docs/onboarding.md` - project mission, package topology, glossary.
2. `AGENTS.md` - operating rules, approval gate, settled decisions, skills discipline.
3. `docs/plan.md` - current roadmap and Phase 4 binding-autogen status.
4. `docs/binding-autogen/binding-generator-constitution.md` - ABI/API law, layer contract, scalar/macro/platform policy, evidence gates.
5. `docs/binding-autogen/binding-generator-roadmap.md` - grand generator roadmap and production flip sequence.
6. `spikes/binding-generators/README.md` - spike entrypoint and quick commands.
7. `spikes/binding-generators/docs/llm-handoff.md` - detailed LLM-to-LLM spike history and current caveats.
8. `spikes/binding-generators/docs/next-iteration-plan.md` - active slice plan.
9. `spikes/binding-generators/output/reports/iteration-2-comparison.md` - decision-quality comparison evidence.
10. `spikes/binding-generators/output/reports/clangsharp-full.md` and `spikes/binding-generators/output/reports/oracle-comparison-clangsharp.md` - latest generated evidence.

If the user asks for an immediate status summary, read at least `docs/onboarding.md`, `AGENTS.md`, `docs/plan.md`, and `spikes/binding-generators/docs/llm-handoff.md` before answering. Do not rely on chat memory.

## Repo Snapshot

This repository builds modular C# bindings for SDL2 and future SDL3, including cross-platform native libraries built from source via vcpkg and packaged as NuGet packages.

Current strategic shape:

1. SDL2 is first priority; SDL3 comes later.
2. Target native coverage is 7 RIDs: `win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.
3. Native builds are vcpkg-based, hybrid-static/dynamic-core, LGPL-free where relevant.
4. NuGet topology is managed package plus per-family `.Native` package.
5. Binding auto-generation is critical path before the first public preview wave.
6. `external/sdl2-cs` is transitional and should retire once generated bindings cover SDL2 families.
7. Public generated API target is not SDL2-CS compatibility. The target shape is internal raw ABI, public typed low-level API, then friendly overloads.

Operational rules that matter most for this spike:

1. Do not modify production build system, CI, project files, manifests, or vcpkg files without explicit user approval.
2. Documentation-only edits are allowed, but still verify links and avoid stale claims.
3. Do not commit without approval. Before a commit, present summary and proposed message.
4. Do not revert unrelated dirty worktree changes.
5. For behavior changes, prefer tests or executable evidence. For generated binding work, build/report evidence is mandatory.
6. After LLM-authored code/project/test changes, run Slopwatch with the repo-specific excludes.

## Binding Autogen Target

The binding generator end-state has three layers:

```text
Layer 3: Friendly overloads
  string, Span<T>, ReadOnlySpan<byte>, out/ref helpers, fmt-only variadic helpers

Layer 2: Public typed low-level API
  public SDL2.SDL-style API, typed handles, safer low-level declarations

Layer 1: Internal raw ABI
  internal SDLNative-style externs, platform attribution, DllImport/LibraryImport split
```

Current spike work is mostly Layer 1 evidence plus infrastructure that will support Layer 2 and Layer 3 later.

Multi-TFM target:

1. `net10.0`, `net9.0`, `net8.0`: modern output using `[LibraryImport]`, source-generated marshalling, modern C# constructs.
2. `netstandard2.0`, `net462`: compatibility output using `[DllImport]`, classic C# constructs, and `System.Memory` where needed.

Platform target:

1. Neutral parse output for platform-neutral declarations.
2. Per-platform parse output for headers whose function surface or ABI shape differs.
3. Roslyn postprocess deduplicates neutral/platform declarations and adds guarded `[SupportedOSPlatform]` attributes.

## Spike Direction

The active prototype is under `spikes/binding-generators/clangsharp/`.

Current evidence path:

1. Python orchestrator inspired by ppy/SDL3-CS.
2. ClangSharp dual codegen: compatible and latest.
3. RSP files for reusable ClangSharp policy.
4. Microsoft.CodeAnalysis postprocess tool for syntax-safe transforms.
5. Production-shaped library projects: `Janset.SDL2.Core` and `Janset.SDL2.Image`.
6. Generated `.g.cs` files committed under each spike project, not generated into temp-only output.

Current non-decision:

1. Do not declare ClangSharp the final production winner yet.
2. Do not delete the Alimer/CppAst scaffold.
3. Build comparable CppAst/Alimer-style evidence against the same checklist before recommending a final toolchain direction.
4. ADR-004 currently selects CppAst for the production generator. Changing that requires explicit evidence and a later ADR amendment.

## What Has Been Achieved

ClangSharp spike accomplishments as of 2026-05-22:

1. `clangsharp/` layout exists with solution, orchestrator, RSP policy, postprocess project, and generated Core/Image library projects.
2. `Janset.SDL2.Core` and `Janset.SDL2.Image` compile across all five TFMs.
3. Dual codegen solved the legacy-TFM problem: compat output for `netstandard2.0`/`net462`, modern output for `net8.0+`.
4. `DllImportToLibraryImportRewriter` converts modern output to `[LibraryImport]`, adds `[UnmanagedCallConv]`, marks methods/classes partial, and preserves platform guards correctly.
5. `StripVarargsRewriter` enforces the constitution's fmt-only variadic policy by removing `__arglist` tails from raw signatures.
6. `PlatformDeltaPostProcessor` removes neutral/platform duplicates, copies neutral files when input/output dirs differ, and adds `#if NET5_0_OR_GREATER` guarded `[SupportedOSPlatform]` attributes.
7. `DeclarationKey.Field(...)` includes declaring type, preventing cross-type field dedupe collisions.
8. Windows-local platform header shims exist for spike-only parsing: `endian.h`, `AvailabilityMacros.h`, and `TargetConditionals.h`.
9. `--use-platform-header-shims` unblocks synthetic Linux/macOS/iOS platform-view generation on Windows-local runs.
10. Shim-enabled generation emits previously missing platform APIs such as `SDL_LinuxSetThreadPriority`, `SDL_UIKitRunApp`, `SDL_iPhoneSetAnimationCallback`, and related iOS callbacks.
11. Empty generated output reporting exists; fatal zero-byte generated files cause generator exit code `4`.
12. `compare_oracle.py` currently compares Core output against the Cake-generated preview and SDL2 dynapi exports.
13. Latest reported Core numbers: 859 spike functions, 866 Cake-oracle functions, 845 dynapi exports, 831 dynapi exports emitted.
14. Latest known verification succeeded for postprocess build, generated Image/Core multi-TFM build, `git diff --check`, and Slopwatch.
15. Docs were refreshed across the spike README, handoff, next plan, spike goals, and binding-generator roadmap.

Key verification commands from the latest slice:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --scope full --codegen both --execute --clean-output --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims

dotnet build spikes/binding-generators/clangsharp/postprocess/Janset.SDL2.PostProcess.csproj -c Release

dotnet build spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Janset.SDL2.Image.csproj -c Release

python spikes/binding-generators/clangsharp/compare_oracle.py --approach clangsharp

git diff --check

slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,spikes/binding-generators/references/**,**/bin/**,**/obj/**"
````

## Current Caveats

Do not overclaim the spike.

Current limitations:

1. `--use-platform-header-shims` is Windows-local spike evidence only. It is not final Linux/macOS platform evidence.
2. Native Linux/macOS generation still needs to be run before claiming production platform completeness.
3. ClangSharp still records some warning-like non-zero `CXError_Failure` invocations around `declspec`; inspect generated output and report deltas instead of trusting exit code alone.
4. `compare_oracle.py` is still Core-first and not yet family-aware.
5. Cake preview oracle exists only for SDL2 Core under `artifacts/generated-bindings-preview/sdl2-core/`.
6. Satellite families currently lack an equivalent Cake-generated preview oracle.
7. `external/sdl2-cs` is useful as stale coverage evidence for Core/Image/Mixer/TTF/GFX, but it is not authoritative production truth.
8. SDL2 dynapi exports are useful for Core runtime symbol truth, but satellites likely need native export-table evidence instead.
9. The implemented platform pass currently targets `SDL_main.h` and `SDL_system.h`; broader platform-sensitive headers still need explicit roadmap decisions.
10. `SDL_thread.h`, `SDL_rwops.h`, `SDL_syswm.h`, `SDL_stdinc.h`, `SDL_platform.h`, and `begin_code.h` remain important for platform-sensitive ABI/layout/macro policy.

Remaining Core gaps currently worth triaging:

1. `SDL_Init`
2. `SDL_InitSubSystem`
3. `SDL_Quit`
4. `SDL_QuitSubSystem`
5. `SDL_WasInit`
6. `SDL_CreateThread`
7. `SDL_CreateThreadWithStackSize`
8. `SDL_GetWindowWMInfo`

## Recommended Next Step

Default next step: make the evidence comparison family-aware before adding more generator behavior.

Why:

1. Current Core-only comparison blurs different evidence sources into one report.
2. Image output now exists and builds, but the report does not explain how it compares to SDL2-CS or native exports.
3. Future Mixer/TTF/GFX work will be harder to evaluate without a reusable evidence matrix.
4. Better evidence will clarify whether the next generator work should be ppy parity, platform native generation, or CppAst comparison.

Suggested evidence matrix shape:

| Family | Spike output | Cake preview | SDL2-CS reference | Runtime export source |
| --- | --- | --- | --- | --- |
| SDL2 Core | `clangsharp/src/Janset.SDL2.Core/Generated` | `artifacts/generated-bindings-preview/sdl2-core/` | `external/sdl2-cs/src/SDL2.cs` | SDL2 dynapi or native exports |
| SDL2_image | `clangsharp/src/Janset.SDL2.Image/Generated` | none | `external/sdl2-cs/src/SDL2_image.cs` | native export table |
| SDL2_mixer | future generated output | none | `external/sdl2-cs/src/SDL2_mixer.cs` | native export table |
| SDL2_ttf | future generated output | none | `external/sdl2-cs/src/SDL2_ttf.cs` | native export table |
| SDL2_gfx | future generated output | none | `external/sdl2-cs/src/SDL2_gfx.cs` | native export table |
| SDL2_net | pending | none | verify availability | native export table |

Report semantics should be explicit:

1. `Cake preview` is trusted prior generated evidence, but Core-only.
2. `SDL2-CS` is stale coverage evidence, not authority.
3. `dynapi` is runtime export evidence for Core, not header/signature evidence.
4. `native exports` are runtime symbol evidence for satellites, not signature evidence.
5. `headers` remain source truth but need parse/context handling before being treated as complete expected-symbol lists.

If the user asks for implementation, start by redesigning `spikes/binding-generators/clangsharp/compare_oracle.py` around these family/evidence concepts, then regenerate the reports and update docs.

## Other Valid Next Steps

Alternative A: ppy orchestrator parity.

Scope:

1. Add per-header `.rsp` lookup.
2. Add manual symbol feedback only when companion files exist or a concrete duplicate requires it.
3. Add typedef feedback only when public typed companion policy is real.
4. Investigate SDL2 equivalent of ppy `sdl.json` metadata.
5. Add string-return `Unsafe_` remap as a future friendly-overload hook only when evidence says it is time.

Risk: without better family-aware reports, it is easy to add ppy mechanisms that look sophisticated but do not explain current coverage gaps.

Alternative B: native platform generation evidence.

Scope:

1. Run Linux/macOS generation on native environments or provisioned containers.
2. Compare outputs with and without Windows-local shims.
3. Separate real platform APIs from shim-induced parse artifacts.

Risk: requires environment availability and can turn into native toolchain yak-shaving. Worth doing, but it is not the fastest way to improve the evidence model.

Alternative C: comparable CppAst/Alimer-style evidence.

Scope:

1. Bring Alimer-style output up to the same multi-TFM/platform/report checklist.
2. Compare ownership cost, output quality, coverage, and postprocess burden against ClangSharp.
3. Use this to decide whether ADR-004 should remain CppAst or be amended.

Risk: important, but should be done after the report model is honest enough to compare both paths fairly.

## ppy Concepts To Understand Before Porting

Do not cargo-cult ppy. Understand these concepts first:

1. `sdl.json` is a metadata dump mapping SDL functions to headers and return types. It is more useful than a flat export list.
2. Header-by-header generation isolates failure, policy, and output ownership.
3. RSP policy keeps ClangSharp options out of Python control flow.
4. Per-header `.rsp` avoids turning family-level RSP files into soup.
5. Manual symbol feedback excludes symbols that are intentionally hand-written in companion files.
6. Typedef feedback preserves stronger C# typedef shapes from companion declarations.
7. String-return `Unsafe_` remap creates raw pointer ABI methods that a friendly string overload can wrap later.
8. ppy validation checks expected functions per header, while current `compare_oracle.py` mostly compares symbol sets after generation.

## Files You Will Touch Most Often

Primary spike files:

1. `spikes/binding-generators/clangsharp/generate_bindings.py`
2. `spikes/binding-generators/clangsharp/compare_oracle.py`
3. `spikes/binding-generators/clangsharp/rsp/base.rsp`
4. `spikes/binding-generators/clangsharp/rsp/sdl2-core.rsp`
5. `spikes/binding-generators/clangsharp/rsp/sdl2-image.rsp`
6. `spikes/binding-generators/clangsharp/postprocess/Program.cs`
7. `spikes/binding-generators/clangsharp/postprocess/DllImportToLibraryImportRewriter.cs`
8. `spikes/binding-generators/clangsharp/postprocess/PlatformDeltaPostProcessor.cs`
9. `spikes/binding-generators/clangsharp/postprocess/StripVarargsRewriter.cs`
10. `spikes/binding-generators/output/reports/clangsharp-full.md`
11. `spikes/binding-generators/output/reports/oracle-comparison-clangsharp.md`

Reference/evidence files:

1. `artifacts/generated-bindings-preview/sdl2-core/`
2. `external/sdl2-cs/src/SDL2.cs`
3. `external/sdl2-cs/src/SDL2_image.cs`
4. `external/sdl2-cs/src/SDL2_mixer.cs`
5. `external/sdl2-cs/src/SDL2_ttf.cs`
6. `external/sdl2-cs/src/SDL2_gfx.cs`
7. `external/vcpkg/buildtrees/sdl2/src/*/src/dynapi/SDL2.exports`
8. `spikes/binding-generators/references/ppy-SDL3-CS/SDL3-CS/generate_bindings.py`
9. `spikes/binding-generators/references/alimer-bindings-sdl/`

Docs to keep current when behavior/evidence shifts:

1. `spikes/binding-generators/README.md`
2. `spikes/binding-generators/docs/llm-handoff.md`
3. `spikes/binding-generators/docs/next-iteration-plan.md`
4. `spikes/binding-generators/docs/generator-spike-goals.md`
5. `docs/binding-autogen/binding-generator-roadmap.md`

## Working Rules For The Next Agent

Follow these rules unless Deniz explicitly redirects you:

1. Start by verifying `git status`, but do not revert unrelated changes.
2. Prefer the smallest correct change.
3. Do not add compatibility code unless there is a concrete shipped/persisted/external reason.
4. Do not wire spike code into production targets, CI, manifests, or package projects without explicit approval.
5. Keep spike references under `spikes/binding-generators/references/` gitignored and unvendored.
6. Generated reports are evidence. Update them when generator behavior changes.
7. Generated `.g.cs` output is part of the spike evidence. If regeneration changes it, inspect diffs before claiming success.
8. `--use-platform-header-shims` is allowed for Windows-local iteration but must be labeled as shim-enabled evidence.
9. Treat satellite export checks as a separate problem from Core dynapi checks.
10. Ask before changing strategy. Do not surprise the user with a toolchain pivot.

## Definition Of Done For A Non-Trivial Spike Slice

A slice is not done until you have:

1. Updated implementation or docs as needed.
2. Regenerated relevant reports when evidence changed.
3. Built the postprocess project if postprocess code changed.
4. Built `Janset.SDL2.Image.csproj` to exercise Core and Image across all five TFMs when generated output changed.
5. Run `compare_oracle.py` when symbol evidence changed.
6. Run `git diff --check`.
7. Run Slopwatch if code/project/test files changed.
8. Updated `spikes/binding-generators/docs/llm-handoff.md` if the next agent would otherwise inherit stale context.
9. Summarized what changed, what was verified, and what remains.
10. Asked for approval before committing.

## Final Steering Note

The current spike is in a good place: the ClangSharp path now produces real multi-TFM Core/Image output, platform attribution is no longer hand-wavy, and the postprocess architecture has earned its keep. The remaining risk is not "can this generate code?". It can. The remaining risk is whether the evidence model is honest enough to justify a production toolchain decision.

So keep the next move boring in the best way: make the evidence matrix family-aware, label each source correctly, and let the reports tell us where the generator actually stands. No magic, no vibes, no victory lap before the receipts.
````
