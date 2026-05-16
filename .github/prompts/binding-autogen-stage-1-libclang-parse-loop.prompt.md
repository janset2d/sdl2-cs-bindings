---
name: "Stage 1 Task 3.5 — libclang+SDL2 parse loop unblock"
description: "Priming prompt for the next agent entering janset2d/sdl2-cs-bindings to resolve a single laser-focused blocker: SDL2 header parsing via CppAst+libclang.runtime.linux-x64 inside the linux-builder container hits an unrecoverable GCC-intrinsic-redeclaration error when all 88 SDL2 headers are parsed in one translation unit. All slice C# infrastructure + Docker + tools.cs orchestration is complete (656 tests passing); only the runtime smoke is broken. Recommended next: refactor parser to per-header invocation matching ppy/SDL3-CS proven pattern."
argument-hint: "Optional override: an alternative non-hack mitigation. Default mission: ship per-header parsing fix and prove `tools.cs generate-bindings` produces output before any other work."
agent: "agent"
model: "Claude Opus 4.7 (1M context)"
---

# Stage 1 Task 3.5 — libclang + SDL2 Parse Loop Unblock

You are entering `janset2d/sdl2-cs-bindings` to **resolve a single laser-focused blocker** that's preventing the Stage 1 Task 3.5 binding-autogen local-output-loop slice from going live. All C# code, Docker infrastructure, and `tools.cs` orchestration are complete and committed-ready; 656 unit + scenario tests pass; build and slopwatch are clean. The only remaining work is making `dotnet run --file tools.cs -- generate-bindings` actually produce output — and that's blocked by a libclang+SDL2-headers interaction that the previous session iterated on for several rounds without landing.

**Your mission is to solve this and only this.** Don't redesign the slice, don't propose parking, don't iterate on other Stage 1 tasks. Make the smoke produce a real `artifacts/generated-bindings-preview/sdl2-core/Platform/*/Commands.g.cs` set and the `parse-views.json` sidecar, then hand back to Deniz for landing review.

## First Principle

> Treat every claim here as **current-as-of-authoring (2026-05-15)** and verify against the live repo, git log, and canonical docs before acting. The previous session iterated 8 times on this problem with progressively-better understanding; some early diagnoses turned out to be wrong (e.g. `-U__has_builtin` was claimed to neutralize GCC's intrinsic headers — verification showed GCC 11's `xmmintrin.h` has no `__has_builtin` guards at all). Re-read the actual headers in the container before trusting any second-hand mechanism claim.

## Non-Negotiable Rules

- **Approval gate** (per `AGENTS.md`): no commits without explicit Deniz approval ("go / apply / proceed / başla / yap"). The slice is staged across ~25 unstaged files and prepared for a single landing commit at the end. Do NOT commit during iteration.
- **No hacks or workarounds.** Deniz caught a spike-era `TargetSystem = string.Empty` hack codified into the strategy brief and called it out explicitly. Memory `feedback_no_workarounds_shortcuts.md` is in force. If a fix feels like a hack — silence warnings, disable a feature to make tests pass, hardcode paths in C# that the library should find naturally — stop and surface it. Real fixes only.
- **No punting when solvable.** Memory `feedback_no_punting_when_solvable.md` is in force. The previous session drifted toward "park this for Task 4-6"; Deniz called it "yan çizmek" and pushed for solving. If you get stuck after 3-4 iterations without a clear path forward, prepare a focused handover for a fresh agent — don't propose deferring.
- **Cake-native build host** (per `AGENTS.md`): in `build/_build`, no raw `System.IO` at build boundaries. Use `Cake.Core.IO.DirectoryPath` / `FilePath`, `ICakeContext`, `CakeFileSystemExtensions`, `CakeJsonExtensions`.
- **Linux-canonical generation.** Only `libclang.runtime.linux-x64` + `libClangSharp.runtime.linux-x64` are pinned (CPM, ADR-004 trio coordination). The `GenerateBindings` Cake target fails-closed on non-Linux triplets.

## What's Already Built (the slice's complete C# + Docker + tools.cs surface)

All code in this list is committed-ready, lint-clean, and test-covered. Don't touch it unless your fix demands it. Verify with `git status --short` before assuming any of this is still there.

### Cake target home — `build/_build/Targets/GenerateBindings/`

| File | State | Purpose |
| --- | --- | --- |
| `GenerateBindingsTask.cs` | new, complete | Cake `[TaskName("GenerateBindings")]`, depends on `EnsureVcpkgDependencies`, fail-closed on non-Linux triplet, builds `GenerateBindingsRequest`, calls `IBindingGenerationRunner`, translates failures to `CakeException`. |
| `BindingGenerationRunner.cs` | new, complete | Cake-aware sealed runner + `IBindingGenerationRunner` interface. Wires `HeaderSetResolver` → `CppAstParseRunner` → `CppAstToPreviewModel` → `PreviewEmitter` → Cake-native write via `_context.WriteAllTextAsync`. Asserts libclang version 20.1.x, logs `CONTAINER_DIGEST` env var, asserts neutral view non-empty. **This is the file that will need refactoring to per-header parsing.** |
| `GenerateBindingsRequest.cs` | bumped `internal` → `public` (required by interface accessibility) | Immutable input record. |
| `Sdl2CoreGenerationConfig.cs` | bumped `internal` → `public` | Same reason. |
| `ServiceCollectionExtensions.cs` | modified | Registers `IBindingGenerationRunner → BindingGenerationRunner` and existing collaborators. `PreviewEmitter` is a static utility (not registered). |
| `Parsing/CppAstParseRunner.cs` | existing, modified | Pure parser wrapper. Current settings: `ParserKind = CppParserKind.C`, `TargetSystem = "linux"`, `BaseAdditionalArguments = ["-U__has_builtin"]`. **The per-header refactor likely changes the public surface here.** |
| `Model/PreviewBindingModel.cs` + 4 record files + `CppAstToPreviewModel.cs` translator | new | Pure model. Translator ports the spike's MapType/MapPrimitive/MapPointer logic with neutral-dedup. |
| `Emitting/PreviewEmitter.cs` + 3 record files | new | Pure static emitter. Returns `GeneratedFileSet` with 8 spike-style `Commands.g.cs` files + `parse-views.json`. |

### IPathService temporary additions — `build/_build/Host/Paths/PathService.cs`

| Property | State |
| --- | --- |
| `GenerateBindingsPreviewRoot` (`artifacts/generated-bindings-preview/`) | new, temporary — retires when Task 5/7 lands real emitters + production-location flag-flip |
| `GetGenerateBindingsPreviewFamilyRoot(string family)` | new, temporary |

### Docker — `docker/binding-generator.Dockerfile` + repo-root `.dockerignore`

Multi-layer derived image. `FROM ${BASE_IMAGE}` (ARG sourced from `build/manifest.json runtimes[linux-x64].container_image`). Layer A: .NET SDK via dotnet-install.sh pinned to global.json. Layer B: NuGet restore --locked-mode. Layer C: source COPY. Plus an `ENV CPATH` line currently set to `/usr/lib/gcc/x86_64-linux-gnu/11/include:/usr/include/x86_64-linux-gnu:/usr/include` — **the per-header refactor may make CPATH unnecessary or change what paths matter**; revisit it.

`.dockerignore` at repo root: keep `.git/` IN the context (vcpkg submodule needs `.git/modules/external/vcpkg` for baseline-version resolution — this took two iterations to discover); exclude `bin/`, `obj/`, `vcpkg_installed/`, `artifacts/`, `.vs/`, `.cake-host/`, `.github/`, `external/vcpkg/{buildtrees,packages,downloads}/`.

### tools.cs `generate-bindings` subcommand

Spectre.Console.Cli command. CliWrap orchestration:
1. `docker version` probe (fail-closed at exit 66 if no daemon)
2. Resolve `BASE_IMAGE` from `build/manifest.json`
3. `docker pull` + `docker inspect` for digest (passed to container as `CONTAINER_DIGEST` env var)
4. `docker build --build-arg BASE_IMAGE=...`
5. `docker volume create janset-vcpkg-cache` (idempotent)
6. `docker run` with bind-mounted `/output` + volume `/vcpkg-cache` + `VCPKG_DEFAULT_BINARY_CACHE` env + `VCPKG_BINARY_SOURCES`. Passes `--repo-root /workspace` explicitly to Program.cs (Cake host) because `.git/` is excluded for size; explicit override short-circuits the git rev-parse lookup.

Flags: `--rebuild-image` (passes `--no-cache` to docker build), `--no-cache` (skips vcpkg binary cache volume).

### Tests — all green

- Unit: `PathConstructionTests` +3, `PreviewEmitterTests` +8, `PreviewParseViewReportTests` +1, `CppAstParseRunnerTests` (existing, modified +2 assertions), `ServiceCollectionExtensionsSmokeTests` +1.
- Scenario: `GenerateBindingsTaskScenarioTests` — 3 fail-closed paths (non-Linux triplet, runner CakeException propagation, runner non-CakeException wrap).
- Full suite: **656 passing / 2 skipped / 0 failed** (baseline was 640+2; +16 new).
- Slopwatch: clean.
- `dotnet build build/_build/Build.csproj -c Release`: 0 warnings, 0 errors.

### Documentation — all updated

Per design spec §9 — done by previous session:
- `docs/binding-autogen/README.md` — status block + folder layout cross-references for the slice spec + plan.
- `docs/binding-autogen/binding-autogen-strategy-brief.md` — Stage 1 precursor note.
- `docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md` — Documentation+Sequencing note about precursor slice.
- `docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md` — new "Task 3.5" cross-reference between Task 3 and Task 4.
- `docs/playbook/binding-generator-maintenance.md` — Trio Pinning table + Post-Stage-1 Revalidation item + Local Generation Loop section + 2 new checklist items.
- `docs/parking-lot.md` — two new entries (Post-Stage-1 trio revalidation, Sysroot/glibc deep-dive).
- `.gitignore` — already covers `artifacts/` blanket (verified).

### Slice design + plan docs (committed in previous wave at SHA `c00a2cb`)

- `docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md` — the design spec, sections approved by Deniz one-by-one.
- `docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md` — the implementation plan with 13 tasks.

## The Blocker — In Detail

The slice's manual smoke (Task 11) is: `dotnet run --file tools.cs -- generate-bindings`. The orchestration works through to the parse step. `EnsureVcpkgDependencies` succeeds. Header resolution succeeds (88 SDL2 headers found at `vcpkg_installed/x64-linux-hybrid/include/SDL2/`). libclang version assertion passes (20.1.2). Container digest captured. Then:

```
Parsing view 'Neutral' (0 defines, 27 undefines).
Error: GenerateBindings failed: CppAst parse failed for Neutral:
  /usr/lib/gcc/x86_64-linux-gnu/11/include/ia32intrin.h(112, 1): error:
definition of builtin function '__rdtsc'
  /usr/lib/gcc/x86_64-linux-gnu/11/include/xmmintrin.h(821, 1): error:
definition of builtin function '_mm_getcsr'
  /usr/lib/gcc/x86_64-linux-gnu/11/include/xmmintrin.h(853, 1): error:
definition of builtin function '_mm_setcsr'
  /usr/lib/gcc/x86_64-linux-gnu/11/include/xmmintrin.h(1296, 1): error:
definition of builtin function '_mm_sfence'
  /usr/lib/gcc/x86_64-linux-gnu/11/include/emmintrin.h(1520, 1): error:
definition of builtin function '_mm_clflush'
  /usr/lib/gcc/x86_64-linux-gnu/11/include/emmintrin.h(1526, 1): error:
definition of builtin function '_mm_lfence'
  /usr/lib/gcc/x86_64-linux-gnu/11/include/emmintrin.h(1532, 1): error:
definition of builtin function '_mm_mfence'
  /usr/lib/gcc/x86_64-linux-gnu/11/include/xmmintrin.h(1329, 1): error:
definition of builtin function '_mm_pause'
```

### Root-cause analysis (do not re-derive; verify and build on it)

1. **NuGet `libclang.runtime.linux-x64` 20.1.2 ships only `libclang.so`** — no clang resource directory (no `lib/clang/20.1.2/include/`). Verified by `find ~/.nuget/packages/libclang.runtime.linux-x64/20.1.2 -type f`. The same is true of `libClangSharp.runtime.linux-x64` 20.1.2. This is the documented design per ClangSharp maintainer Tanner Gooding (issue #414): "ClangSharp defers to libClang and does not introduce special state — users must explicitly pass all desired include directories."

2. **Without a resource directory, libclang has no clang-side intrinsic headers** (no clang's own `xmmintrin.h`, `stddef.h`, etc.). So when transitive includes pull intrinsic headers, libclang resolves them to GCC's installed headers at `/usr/lib/gcc/x86_64-linux-gnu/11/include/`.

3. **GCC 11's intrinsic headers (`xmmintrin.h`, `emmintrin.h`, `ia32intrin.h`) declare functions like `_mm_pause`, `_mm_getcsr`, `__rdtsc` as `extern __inline` with bodies calling `__builtin_ia32_*`.** Verified by `docker run --rm --entrypoint sh <linux-builder> -c "cat /usr/lib/gcc/x86_64-linux-gnu/11/include/xmmintrin.h"`. **There are no `__has_builtin` guards in these declarations** — they're unconditional `extern __inline` bodies.

4. **libclang has those SAME symbols as INTERNAL builtin functions** in its builtin table. When clang sees a regular declaration of `_mm_pause` (without the `[[clang::builtin]]` attribute that clang's OWN headers use), it produces the "definition of builtin function" error.

5. **An earlier hypothesis (`-U__has_builtin` neutralizes GCC headers) was wrong.** The previous session added `BaseAdditionalArguments = ["-U__has_builtin"]` to `CppAstParseRunner.cs`. It's still useful for the SDL-side `_SDL_HAS_BUILTIN(x)` macro in `SDL_stdinc.h`, but it does NOT neutralize GCC's intrinsic headers because they have no `__has_builtin` guards. Verify before claiming it does.

### Why ppy/SDL3-CS succeeds with the same NuGet libclang

**ppy parses one header at a time.** Their `generate_bindings.py` calls `ClangSharpPInvokeGenerator --file <single-header.h>` per header in a loop. Each invocation creates an isolated translation unit whose transitive includes are minimal. Most SDL3 headers don't transitively reach `xmmintrin.h`.

For the few that do (e.g. `SDL_stdinc.h`), they apply targeted `--additional --undefine-macro=__has_builtin` via a per-header RSP file. This specifically neutralizes SDL's `_SDL_HAS_BUILTIN(x)` probe inside SDL3, NOT GCC's headers — but it's enough to short-circuit SDL3's transitive include chain before it reaches GCC's intrinsic surface.

**Plus SDL3's `SDL_stdinc.h` is structurally narrower than SDL2's** — SDL3 dropped `<sys/types.h>` and the `SDL_config.h`-driven `HAVE_*` conditional include thicket. SDL2's chain is wider and more likely to transitively reach intrinsics.

ppy's Dockerfile sets `CPLUS_INCLUDE_PATH` (NOT `CPATH`) at entrypoint — which only affects C++ mode. They run in C++ mode by default (ClangSharpPInvokeGenerator default). We're now running in C mode (`ParserKind = CppParserKind.C`).

**Our current `CppParser.ParseFiles(allHeaders)` call passes ALL 88 SDL2 headers in one invocation.** CppAst internally creates one synthetic translation unit that `#include`s every header. That means every transitive include from any SDL2 header pollutes the same parse session — including the intrinsic chain.

## The Path Forward — Option 2 (per-header parsing, ppy pattern)

This is the agreed direction. Deniz approved it explicitly after rejecting:

- **Option 1 (install clang-20 via apt + pass `-resource-dir`):** small fix but introduces a second source of truth for libclang (apt clang + NuGet libclang), violates ADR-004 trio shape, papers over the "all 88 in one TU" anti-pattern.
- **Option 3 (park the smoke):** rejected as drift / "yan çizmek".

### The shape of the refactor

`CppAstParseRunner` currently exposes `Parse(ResolvedHeaderSet headerSet, PlatformParseView parseView) → CppAstParseResult`. Internally it calls `CppParser.ParseFiles(headerSet.Headers.Select(...).ToList(), options)`.

The refactor should make `CppAstParseRunner` parse **each header in its own translation unit** and merge results. Concretely:

```csharp
public CppAstParseResult Parse(ResolvedHeaderSet headerSet, PlatformParseView parseView)
{
    var mergedCompilation = ... // new aggregating shape
    foreach (var header in headerSet.Headers)
    {
        var options = CreateOptions(headerSet, parseView);
        var singleResult = CppParser.ParseFile(header.FullPath, options);
        if (singleResult.HasErrors)
        {
            // ... aggregate diagnostics, decide whether to fail-fast or continue
        }
        // merge functions / types / etc. into the aggregate result
    }
    return new CppAstParseResult(parseView, mergedCompilation);
}
```

Key design questions to resolve as you implement:

1. **What's the right aggregate result type?** `CppCompilation` from CppAst isn't trivially mergeable. Options:
   - (a) Replace `CppAstParseResult.Compilation` with `IReadOnlyList<CppCompilation>` (one per header) and update `CppAstToPreviewModel.Translate` to iterate over all of them.
   - (b) Manually merge `CppCompilation.Functions / Classes / Enums / etc.` into a new accumulator type. Likely more work.
   
   Lean (a) — minimal model changes, cleaner separation.

2. **How to deduplicate symbols seen in multiple headers?** SDL_stdinc.h declares functions that are also accessible via the umbrella `SDL.h`. With per-header parsing, you'll see them twice. Solution: dedupe by `SourceFile + Name` when translating to the preview model. The current `CppAstToPreviewModel.IsSdl2Header` filter handles cross-include source-file attribution; extend that logic.

3. **Do we still need ParseFiles at all?** Possibly not. The current implementation is wrong in spirit — parse should be per-translation-unit.

4. **Per-header headers with intrinsic problems:** even with per-header parsing, `SDL_stdinc.h` alone might still pull `<sys/types.h>` → `<endian.h>` → intrinsics chain on Linux. Test this empirically. If it does, you may need targeted `-U__has_builtin` (already in `BaseAdditionalArguments`) OR additional mitigation specifically for that header.

5. **CPATH may become redundant.** With targeted per-header parsing, many transitive paths simplify. Re-evaluate the `ENV CPATH` in `binding-generator.Dockerfile` after the refactor — it may be entirely unnecessary.

### Acceptance criteria

The fix is done when:

1. `dotnet build build/_build/Build.csproj -c Release` is clean.
2. `dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0` is 656+ passing / 2 skipped / 0 failed (test count may rise slightly if new translator tests are added; that's fine).
3. `slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"` is clean.
4. `dotnet run --file tools.cs -- generate-bindings` produces:
   - `artifacts/generated-bindings-preview/sdl2-core/Platform/{Neutral,WindowsDesktop,WinRT,GDK,Linux,MacOS,IOS,Android}/Commands.g.cs` (8 files)
   - `artifacts/generated-bindings-preview/sdl2-core/parse-views.json`
5. Re-running the smoke produces byte-identical output (`diff -r run1 run2` is empty).
6. Eyeball-diff against `tools/binding-spike/cppast-platform/bindings/Generated/SDL2.Platform.*.g.cs` shows recognizable shape (bare `[DllImport(LibName, ...)]` stubs, `LibName = "SDL2"`, `[SupportedOSPlatform]` on platform views).

After acceptance, hand back to Deniz for landing commit approval. Do NOT commit yourself.

## What Has Been Tried (and Why It Didn't Land — read before attempting)

Each iteration below was a real run. Don't repeat these without a specific reason; build on what was learned.

| Iteration | Change | Result | Diagnosis |
| --- | --- | --- | --- |
| 1 | First smoke run | `.git/` excluded → `git rev-parse` fails inside container, repo root falls back to `bin/` | Excluded `.git/` from `.dockerignore` — incompatible with `Shared.ResolveRepoRootAsync` |
| 2 | Pass `--repo-root /workspace` from tools.cs | vcpkg submodule fails: `not a git repository` for `external/vcpkg/.git` (pointer file) | `.git/modules/external/vcpkg` is required by vcpkg for baseline-version resolution — excluding `.git/` breaks vcpkg too |
| 3 | Keep `.git/` IN the build context | `SDL_stdinc.h:36 error: 'sys/types.h' file not found` | NuGet libclang has no sysroot defaults; needed explicit include paths |
| 4 | Add hardcoded `LinuxBuilderSystemIncludes` array to `CppAstParseRunner` with GCC + glibc paths | Intrinsic redeclaration errors (`_mm_pause` etc.) | GCC intrinsic headers conflict with libclang's internal builtin table |
| 5 | Remove GCC path from hardcoded list, keep `/usr/include` + multiarch | `sys/types.h(144): error: 'stddef.h' file not found` | stddef.h is a compiler-shipped header; lives in GCC or clang resource dir, not glibc |
| (Deniz pushback) | "TargetSystem = string.Empty bu çok kötü bir hack" | Removed `TargetSystem = string.Empty` from `CppAstParseRunner`. Removed hardcoded sysroot list. | Codified spike-era workaround in spec — caught and removed per `feedback_no_workarounds_shortcuts` |
| 6 | TargetSystem default + no sysroot in parser code + CPATH env var in Dockerfile (glibc paths only) | `sys/types.h` found, but transitively wants `stddef.h` | Still missing compiler-shipped headers |
| 7 | TargetSystem default + CPATH with GCC path added back | `_MSC_VER`-conditioned `#include <sal.h>` at `SDL_stdinc.h:357` | CppAst's default TargetSystem detection yielded MSVC-compat on the .NET host |
| 8 | `TargetSystem = "linux"` explicit + CPATH with GCC path | Intrinsic redeclaration errors again (same as iter 4) | linux target correctly skips `_MSC_VER`; GCC intrinsic conflict re-surfaces |
| 9 | Add `-U__has_builtin` to AdditionalArguments + ParserKind = C | Same intrinsic errors | `-U__has_builtin` doesn't help: GCC 11's xmmintrin.h has NO `__has_builtin` guards (verified by `docker run cat /usr/lib/gcc/.../xmmintrin.h`) |

**Current state of `CppAstParseRunner`:**
- `ParseMacros = true`
- `ParserKind = CppParserKind.C`
- `TargetSystem = "linux"`
- `SystemIncludeFolders = { headerSet.IncludeRoot.FullPath }` (no hardcoded sysroot)
- `BaseAdditionalArguments = ["-U__has_builtin"]` applied to every parse view
- `Defines = ["SDL_DECLSPEC=", "__PRFCHWINTRIN_H=1"]` + per-view defines from `PlatformCatalog`
- Per-view `Undefines` mapped to `-U<macro>` flags

**Current state of `docker/binding-generator.Dockerfile` CPATH:**
```
ENV CPATH="/usr/lib/gcc/x86_64-linux-gnu/11/include:/usr/include/x86_64-linux-gnu:/usr/include"
```

The refactor to per-header parsing may make some of these redundant or wrong; re-evaluate everything after the refactor lands.

## Current Git State

Run `git status --short` to verify. Expected at the time of writing:

```
HEAD: c00a2cb feat(binding-autogen): Stage 1 scaffold + Task 3.5 local-loop design

Unstaged modifications:
  M build/_build.Tests/Unit/CompositionRoot/ServiceCollectionExtensionsSmokeTests.cs
  M build/_build.Tests/Unit/Host/Paths/PathConstructionTests.cs
  M build/_build.Tests/Unit/Targets/GenerateBindings/Parsing/CppAstParseRunnerTests.cs
  M build/_build/Host/Paths/PathService.cs
  M build/_build/Targets/GenerateBindings/GenerateBindingsRequest.cs
  M build/_build/Targets/GenerateBindings/Parsing/CppAstParseRunner.cs
  M build/_build/Targets/GenerateBindings/Sdl2CoreGenerationConfig.cs
  M build/_build/Targets/GenerateBindings/ServiceCollectionExtensions.cs
  M tools.cs
  M docs/binding-autogen/README.md
  M docs/binding-autogen/binding-autogen-strategy-brief.md
  M docs/parking-lot.md
  M docs/playbook/binding-generator-maintenance.md
  M docs/superpowers/plans/2026-05-14-sdl2-core-binding-generator-stage-1.md
  M docs/superpowers/specs/2026-05-14-binding-generator-architecture-design.md

Untracked:
  ?? .dockerignore
  ?? build/_build.Tests/Scenarios/GenerateBindings/
  ?? build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/
  ?? build/_build/Targets/GenerateBindings/BindingGenerationRunner.cs
  ?? build/_build/Targets/GenerateBindings/Emitting/
  ?? build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs
  ?? build/_build/Targets/GenerateBindings/Model/
  ?? docker/binding-generator.Dockerfile
  ?? .github/prompts/binding-autogen-stage-1-libclang-parse-loop.prompt.md
```

Branch: `spike/binding-autogen-sdl2-gfx`. Do NOT push; do NOT commit unless Deniz says so.

## Mandatory Grounding (read in this order)

1. `AGENTS.md` — operating rules, approval gate, settled decisions.
2. `docs/onboarding.md` — project overview.
3. `docs/superpowers/specs/2026-05-15-binding-generator-local-output-loop-design.md` — design spec for this slice.
4. `docs/superpowers/plans/2026-05-15-binding-generator-local-output-loop.md` — implementation plan (mostly executed; per-header refactor will deviate from it).
5. `docs/binding-autogen/research/binding-autogen-spike-findings.md` — spike findings including the libclang/ClangSharp tight-coupling story (§5.3, §7.2 friction #5) and the Linux pinning section (§6).
6. `docs/playbook/binding-generator-maintenance.md` — Trio Pinning section explains the version pin model that must stay intact.
7. `docs/decisions/2026-05-14-binding-autogen-toolchain.md` — ADR-004 trio coordination rule (CppAst + libclang.runtime + libClangSharp.runtime move together).
8. `docs/decisions/2026-05-05-target-centric-build-host.md` — ADR-002 target-centric pattern (which `GenerateBindings/` follows).
9. `docs/knowledge-base/testing-guidelines.md` — TUnit/MTP fixture rules, `--treenode-filter` syntax (`/*/*/<ClassName>/*` — NOT `dotnet test --filter`).
10. **ppy/SDL3-CS** — read their `Dockerfile`, `generate_bindings.py`, and `SDL3-CS/SDL3/SDL_stdinc.rsp`. These are the proven working pattern.
11. **CppAst.NET** — check `CppParserOptions.cs` and `CppParser.cs` on GitHub to understand the parsing surface available.

## Locked Policies (verify before violating)

- **ADR-002 §2.4:** target-local until reuse pressure. The new types added by the per-header refactor stay under `build/_build/Targets/GenerateBindings/` unless they earn promotion.
- **ADR-003:** persisted contracts under `Data/`. `.generated-stamp` is NOT in scope for this slice; don't touch `Data/BindingGeneration/`.
- **ADR-004:** trio version coordination. The CPM pins (CppAst 0.24.0 + libclang.runtime.linux-x64 20.1.2 + libClangSharp.runtime.linux-x64 20.1.2) move together. Bumping individually is forbidden.
- **Cake nativeness at build boundaries.** Use `_context.WriteAllTextAsync`, `_context.EnsureDirectoryExists`, Cake `DirectoryPath` / `FilePath`. The existing runner already follows this.
- **Pure code stays pure.** `Model/`, `Emitting/`, `Parsing/` are PURE — no `ICakeContext`, no Cake aliases. The per-header refactor must preserve this.
- **Test naming:** `<MethodName>_Should_<Verb>_<optional When/If/Given>`.
- **No commits without explicit approval.**

## What Solving This Looks Like

You'll know you're on the right path when:

- `CppAstParseRunner.Parse` invokes `CppParser.ParseFile` (singular) in a loop, OR exposes a new method like `ParseAll(headerSet, parseView)` returning an aggregated result.
- The runner / translator / emitter pipeline handles the new aggregate shape without leaking it into the public interface boundary.
- `dotnet run --file tools.cs -- generate-bindings` produces output. The Neutral view has hundreds of functions (SDL2.Core has ~400+ public functions); each platform view adds platform-only functions on top.
- Re-running the smoke is deterministic.

You'll know you've drifted if you find yourself:

- Adding clang-20 to the Dockerfile (Option 1 was rejected).
- Hardcoding sysroot paths back into `CppAstParseRunner` (the user explicitly rejected this).
- Suggesting "park this and move on" (memory `feedback_no_punting_when_solvable.md` will be in your face).
- Codifying a spike-era workaround in a spec (memory `feedback_no_workarounds_shortcuts.md` covers this).

## Final Steering Note

This slice has been engineered cleanly through 13 tasks of TDD-style work. The C# infrastructure is done. The only thing left is to make `CppParser.ParseFiles(all_88)` into `for each header: CppParser.ParseFile(one)` and reconcile the result shape. ppy/SDL3-CS proves this works with the EXACT same NuGet libclang we have. The previous session drifted toward "let's defer" after multiple environmental detours; **don't repeat that mistake**. Solve it. Then hand back.

Hold the line.
