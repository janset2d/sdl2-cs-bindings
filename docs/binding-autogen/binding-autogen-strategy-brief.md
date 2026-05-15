# Binding Auto-Generation Strategy Brief

> Strategy brief accepted for Phase 4 planning. Durable toolchain policy is recorded in [ADR-004](../decisions/2026-05-14-binding-autogen-toolchain.md); remaining implementation details promote into AGENTS.md, release guardrails, and onboarding as Phase 4 ships. Retires when Phase 4 implementation completes and binding generator output supersedes `external/sdl2-cs`.
>
> **Status (2026-05-15):** Accepted strategy brief, revised 2026-05-15 to fold the generator into the Cake build host, lock Linux-container as the canonical determinism contract, correct the stub-strategy framing against ppy/SDL3-CS and the local CppAst spike evidence, defer `SDL_syswm.h` struct/union layout to Stage 2, and gate SDL3 work on PD-7 completion. Sections complete: Decision Hypothesis / WHY / HOW / WHAT / Plan Shape / Current Open Decisions / Decision Audit / Cross-Reference. See [`binding-autogen-onboarding.md`](research/binding-autogen-onboarding.md) for workstream entry point.

## Decision Hypothesis

Phase 4 ships an auto-generated binding surface for SDL2 (core + all in-scope satellites) — SDL3 follows after PD-7 — produced by a CppAst-based emitter **hosted in the Cake build host** under `build/_build/Targets/GenerateBindings/`, that:

- pins CppAst 0.24.0 + libclang.runtime.linux-x64 20.1.2 + libClangSharp.runtime.linux-x64 20.1.2 (version-trio coupling per [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §7.6). Non-Linux runtime packages are intentionally omitted; the generator is **Linux-canonical** and fail-closed on any other host RID;
- consumes vcpkg-installed canonical SDL headers via libclang controlled parse views (neutral + per-OS/backend tuple set — see "Multi-pass parsing strategy" below for the catalog), all executed inside a single Linux container per the ppy/SDL3-CS pattern, with platform-conditioned declarations attributed via `[SupportedOSPlatform]`. Platform separation uses **preprocessor macro switching only** — no `--target` cross-compile flags, no mingw-w64, no Apple SDK; SDL headers' own forward-declarations carry the cross-OS opaque types;
- emits per-family generated `.g.cs` files committed to the repository, dual-shaped for `[LibraryImport]` (net7+) and `[DllImport]` (legacy TFMs) in a single emitter loop;
- emits typed `readonly partial struct` handle types (`SDL_Window`, `SDL_Renderer`, etc.) — zero-cost over `IntPtr` at the wire, type-safe at compile time, AOT-trivial — matching the Alimer / Vortice / Silk.NET ecosystem convention for CppAst-based bindings;
- emits friendly overloads (`string` / `ReadOnlySpan<byte>` / `out` / `ref` / `Span<T>`) alongside the raw P/Invoke layer in the same loop;
- targets the full TFM matrix (`net10` / `net9` / `net8` / `netstandard2.0` / `net462`).

The generator runs offline via a dedicated `regenerate-bindings.yml` workflow (manual trigger, auto-PR via peter-evans/create-pull-request, Silk.NET reference pattern) and locally via `tools.cs generate-bindings` (Docker orchestration: repo-root volume mount, vcpkg cache mount, Cake Release-publish step, the pinned `linux-builder` image). Docker is a **hard prerequisite** for the determinism contract; there is no host-OS fallback path. Two new release guardrails close the binding ↔ native coherence loop: a vcpkg-state coherence validator at PreFlight (catches "natives rebuilt but bindings not regenerated"), and a symbol-existence validator at Pack (catches "binding declares an unexported function").

The toolchain pick rests on the **scope-trajectory bet** ([`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §7.8 + §9 Q8): at the production-shape feature investment this project commits to, CppAst's single-loop emitter has linear ownership growth while ClangSharpPInvokeGenerator + RSP + Roslyn-extension + post-process pipelines grow in architectural steps. ClangSharp remains a documented migration target if CppAst's maintenance burden surfaces in practice.

`external/sdl2-cs` retires when AST-generated SDL2 output passes runtime smoke against `learning-sdl2`. First public `-preview.N` wave ships AST-generated bindings, not sdl2-cs imports, per [`release-strategy.md`](../release-strategy.md) §Sequencing.

## WHY

### Auto-generation is the only sustainable path

Four reasons, each load-bearing on its own:

**SDL3 has no upstream `SDL3-CS` covering our scope.** Per [`release-strategy.md`](../release-strategy.md) §End State at v1.0, the v1.0 stable shape includes SDL3 Core + SDL3_image + SDL3_mixer + SDL3_ttf — all AST-generated, all 7 RIDs. There is no comparable hand-written upstream source: [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS) is itself auto-generated (ClangSharp + Docker, MIT) and [flibitijibibo/SDL3-CS](https://github.com/flibitijibibo/SDL3-CS) covers SDL3 core only via c2ffi. The hand-written option does not exist; we ship a generator or we ship without SDL3. See [`binding-autogen-onboarding.md`](research/binding-autogen-onboarding.md) §Project Context.

**Maintaining 11,105 lines of hand-written P/Invoke is unsustainable.** Verified line count against `external/sdl2-cs/src/` on 2026-05-14:

| File | Lines |
|---|---|
| `SDL2.cs` | 8,966 |
| `SDL2_image.cs` | 316 |
| `SDL2_mixer.cs` | 665 |
| `SDL2_ttf.cs` | 768 |
| `SDL2_gfx.cs` | 390 |
| **Total** | **11,105** |

Source: [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §5 SDL2 oracle table. Adding SDL3 + satellites roughly doubles that surface. The current single-maintainer hobby cadence cannot sustain hand-patched regeneration across SDL minor bumps in both major versions, across 7 RIDs, with the platform-conditioned correctness required (see next bullet). See [`AGENTS.md`](../../AGENTS.md) §Settled Strategic Decisions row "Binding autogen replaces SDL2-CS."

**Regenerate-on-upstream-bump is a settled D-3seg mode.** Per [ADR-001](../decisions/2026-05-05-d3seg-and-package-first.md), the `FamilyPatch` segment of D-3seg versioning is "the repo's own iteration counter," monotonically increasing within a `UpstreamMajor.UpstreamMinor` line, **reset to 0 when either upstream segment changes**. That reset semantic only makes sense if bindings regenerate from new SDL headers — not if a maintainer hand-patches diffs into existing P/Invoke declarations. Per [`release-strategy.md`](../release-strategy.md) §Maintenance Commitment Post-v1.0: "When SDL2.32.x → 2.33.0 or SDL3.4.x → 3.5.0 lands in vcpkg, regenerate AST output + release wave."

**Cross-RID correctness requires platform-conditioned parsing.** The 7-RID matrix (`win-{x64,x86,arm64}`, `linux-{x64,arm64}`, `osx-{x64,arm64}`) parses SDL headers that contain real platform-gated public declarations in `SDL_system.h`, `SDL_main.h`, `SDL_platform.h`, `SDL_config.h`, `SDL_stdinc.h`, `SDL_syswm.h` — see [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §3 "Cross-platform API surface variance" + [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §8.7 platform-pass result. Hand-written bindings cannot model this rigorously; libclang controlled parse views can. This is a libclang/preprocessor constraint, not a CppAst-vs-ClangSharp distinction.

### Why `external/sdl2-cs` cannot ship to v1.0 stable

[`AGENTS.md`](../../AGENTS.md) §Settled Strategic Decisions classifies `external/sdl2-cs` as **transitional, untrusted for production testing**. Four concrete defects justify that classification, each verified during the SDL2_gfx spike:

**No C-side type provenance.** sdl2-cs strips C type names from its `[DllImport]` signatures. Code-review of generator regenerations against future SDL bumps becomes guesswork. ClangSharp's `[NativeTypeName]` (and CppAst's optional equivalent) preserves provenance. See [Microsoft Learn — P/Invoke source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation) for the modern attribute-rich emission contract.

**String marshalling defaults to ANSI on Windows.** Real bug confirmed in [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §6.B: sdl2-cs's `public static extern int stringColor(IntPtr renderer, short x, short y, string s, uint color)` defaults to `LPStr` ANSI marshalling. On non-Latin Windows locales (Turkish, Japanese, Cyrillic, etc.) non-ASCII characters are corrupted before reaching SDL2_gfx, which expects UTF-8. [Microsoft Learn — Native interop best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices) §"String parameters" makes UTF-8 marshalling the modern default; sdl2-cs predates this guidance.

**`char` modeled as 16-bit Unicode where C uses 8-bit.** [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §6.C: sdl2-cs `characterColor(IntPtr renderer, short x, short y, char c, uint color)` declares `char` (UTF-16 in C#) where the C signature is `char` (8-bit signed). The wire width is wrong on Windows non-Latin locales. Cross-platform implications are silent runtime corruption.

**No `[Flags]` attribution.** [`binding-autogen-approaches.md`](research/binding-autogen-approaches.md) §"Bit-flag enum surprise": SDL flag types (`SDL_WindowFlags`, `SDL_InitFlags`, etc.) lose `[Flags]` attribution in sdl2-cs. Caller-side ergonomics (`WindowFlags.Resizable | WindowFlags.Shown`) work, but `ToString()` on combined flags renders integers, debugger display loses semantic info, IDE intellisense doesn't suggest flag composition.

**API churn risk for preview consumers.** Per [`release-strategy.md`](../release-strategy.md) §"Why AST-First, Not Public-Prerelease-First": publishing `-preview.N` packages built on sdl2-cs imports freezes the *wrong* API surface into consumer muscle memory. The eventual AST-generated surface differs (different method-name conventions, typed handles vs IntPtr, friendly overload shapes); rev-bumping to v2 after preview adoption is avoidable migration pain. AST-first sequencing means the first public package targets the eventual stable API from day one.

### Why CppAst — the scope-trajectory argument

Both CppAst and ClangSharpPInvokeGenerator are spike-validated at SDL2_gfx scope. Both produced 102 working P/Invoke declarations, identical runtime behavior (180 frames @ 30 FPS, bouncing-circle render, timer jitter within 0.7%), and end-to-end native-distribution validation via `Janset.SDL2.Gfx.Native` buildTransitive targets. See [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §8 "Runtime Validation — Both Toolchains End-to-End."

**At raw P/Invoke-only scope, ClangSharp's owned LoC is lower** (verified 2026-05-14):

| Toolchain | Owned LoC (raw scope) |
|---|---|
| ClangSharp (RSP + 2 supplement files + dotnet-tools.json + csproj) | ~128 |
| CppAst (Program.cs + generator csproj + bindings csproj) | ~320 |

Source: [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §7.5 trade-off matrix (corrected 2026-05-14).

**But we are explicitly not at raw P/Invoke-only scope.** This brief locks five production-shape features simultaneously, each of which the generator must support:

1. **Full TFM matrix** (`net10 / net9 / net8 / netstandard2.0 / net462`), dual-emit `[LibraryImport]` (net7+) + `[DllImport]` (legacy) per function. See [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §2 Rule 1; [Microsoft Learn — P/Invoke source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation) for the modern contract; [dotnet/runtime LibraryImportGenerator Compatibility](https://github.com/dotnet/runtime/blob/main/docs/design/libraries/LibraryImportGenerator/Compatibility.md) for the breaking-change matrix.
2. **Typed `readonly struct` baseline** per opaque handle, with `IsNull` / `Null` / `IEquatable<T>` / implicit `nint` conversion / `[DebuggerDisplay]`. Verified ecosystem-standard against live code: [Alimer.Bindings.SDL Generated/Handles.cs](https://raw.githubusercontent.com/amerkoleci/Alimer.Bindings.SDL/main/src/Alimer.Bindings.SDL/Generated/Handles.cs), [Vortice.Vulkan Generated/Handles.cs](https://raw.githubusercontent.com/amerkoleci/Vortice.Vulkan/main/src/Vortice.Vulkan/Generated/Handles.cs). See [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §2 Rule 2.
3. **Friendly overloads** (`string` / `ReadOnlySpan<byte>` / `out` / `ref` / `Span<T>`) emitted alongside raw P/Invoke. See [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §2 Rules 4 + 6; [Microsoft Learn — Custom marshalling source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/custom-marshalling-source-generation) for `Utf8StringMarshaller` semantics.
4. **`[SupportedOSPlatform]` attribution** for platform-conditioned symbols, driven by multi-pass parsing. See [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §3 "Multi-platform parsing"; [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §8.7 platform-pass result; [ppy/SDL3-CS generate_bindings.py](https://raw.githubusercontent.com/ppy/SDL3-CS/master/SDL3-CS/generate_bindings.py) as the closest reference pattern.
5. **Satellite/shared-types topology** — core-owned shared SDL type universe referenced from satellite emitters. See [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §3 "Satellite headers — separate outputs, shared core type universe"; [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §8.7 SDL2_image shared-type spike result.

**At this scope, ownership-growth trajectories diverge** — per [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §7.8 "Scope-Growth Trade-off":

| Feature additive on top of raw P/Invoke | ClangSharp surface | CppAst surface |
|---|---|---|
| Multi-TFM dual emit | Post-process pipeline (separate project, Roslyn-syntax-rewriter or regex pass) | ~50 LoC iteration logic in same `Program.cs` |
| Friendly `string` / `Span` / `out` overloads | Roslyn source generator project (ppy's `FriendlyOverloadGenerator` fork + modernize from `ISourceGenerator` to `IIncrementalGenerator` per [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §5.5 corrected) | ~80 LoC iteration logic in same loop |
| `[SupportedOSPlatform]` per-platform attribution | RSP `--with-attribute` (per-symbol) + per-platform pass orchestrator script | ~40 LoC iteration logic in same loop |
| Typed `readonly struct` handle baseline | Not the ClangSharp default (emits `T*`); requires Roslyn post-process to rewrite | Natural emit pattern (see Alimer/Vortice production output) |

**ClangSharp ownership grows in architectural steps** — each feature lands as a separate project + mental model + version-coordination story (RSP grammar, post-process tool, Roslyn extension, per-platform orchestrator). **CppAst ownership grows linearly** — each feature is more iteration logic in the same `foreach` loop in `Program.cs`. The net owned-LoC numbers at production-shape scope:

| Scope tier | ClangSharp owned | CppAst owned |
|---|---|---|
| Raw P/Invoke only (current spike) | ~128 | ~320 |
| + Multi-TFM + Friendly overloads + Platform attribution + Typed struct baseline | ≈ +600 LoC across 3+ separate projects | ≈ +180 LoC same Program.cs (~500 total) |

The gap inverts at our committed scope. CppAst's single-codebase elasticity is the structural advantage we're buying.

**Three corrected facts that the toolchain pick does not rest on** (per Decision Audit section below + corrections committed 2026-05-14):

- The "CppAst captured 8 macros vs ClangSharp's 2" claim was retracted — both toolchains capture all 8 SDL2_gfx constants identically with `--config generate-macro-bindings`. See [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §7.4 (Revised 2026-05-14).
- `FriendlyOverloadGenerator` uses `ISourceGenerator` (older Roslyn API), not `IIncrementalGenerator`. The modernization is real D10 effort if we ever migrate to ClangSharp.
- The original decision matrix arithmetic (CppAst 23 vs ClangSharp 22) summed wrong — both columns total 23. The recalculated weighted matrix (43 vs 63) still favors ClangSharp on raw-binding economics; the scope-trajectory argument overrides at our committed scope.

**Two complementary toolchain-agnostic anchors hold:**

- [Microsoft Learn — Native interop best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices) endorses `[LibraryImport]`, function pointers + `[UnmanagedCallersOnly]` for callbacks, and `[SupportedOSPlatform]` attribution. Our emit rules ([`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §2) follow this guidance regardless of toolchain.
- [Silk.NET 3.0 generation proposal](https://github.com/dotnet/Silk.NET/blob/main/documentation/proposals/Proposal%20-%20Generation%20of%20Library%20Sources%20and%20PInvoke%20Mechanisms.md) explicitly delegates parsing to ClangSharp, demonstrating ClangSharp's credibility for heavyweight multi-graphics-lib scope. For our focused SDL2 + SDL3 scope, the Alimer/Vortice CppAst pattern is the proportionate choice; Silk.NET's pipeline is documented evidence that ClangSharp is the migration target when scope grows past CppAst's tolerance.

**Migration door stays open.** If CppAst's libclang version-trio coupling becomes painful in practice (per [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §7.6 "Version-Trio Coupling is Real"), or if our scope shrinks to raw P/Invoke only and ClangSharp's leaner setup begins to dominate, the ClangSharp + ppy pattern is well-documented in [`binding-autogen-approaches.md`](research/binding-autogen-approaches.md) §2026-05-12 Source-Level Comparison. The generated `.cs` output format is the same on either side; migration is about the generator, not the output. Captured as a Risk row in Plan Shape below with explicit mitigation.

### Why hosted in the Cake build host — not a standalone CLI tool

The 2026-05-14 draft of this brief proposed a standalone `src/Janset.SDL2.Bindings.Generator/` console app invoked from a Cake target via `Tool<TSettings>` subprocess wrapper. The 2026-05-15 revision folds the generator into the Cake build host as a target-local module under `build/_build/Targets/GenerateBindings/`. Five reasons, each independently sufficient:

**1. Binding generation is structurally a CI/CD concern.** Every input the generator consumes is already owned by the build host: vcpkg state resolution (`Data/Manifest/`, `Data/Versions/`), canonical header provisioning (`Tools/VcpkgBootstrapTool`), the canonical RID + triplet mapping (`Host/Runtime`), the release manifest schema (`Data/Manifest/Models/`), the validation patterns that PreFlight already runs (`Validation/`, ADR-003 contract-centric data layer). A separate `src/`-tree console app would have to re-read `vcpkg.json` / `build/manifest.json` / `vcpkg-configuration.json`, re-implement the manifest parser, re-wrap vcpkg invocation, re-emit logs in a different style, and re-discover paths. That is a **parallel orchestration stack** for no incremental value — it duplicates existing surface and creates two places to chase a manifest-schema bump.

**2. ADR-002 §2.1's target-centric navigation rule applies cleanly.** Every other Cake target lives under `build/_build/Targets/<CakeTargetName>/` and is reachable from `tools.cs` / `release.yml` / `--target X`. Binding generation is no exception: it ships as a `[TaskName("GenerateBindings")]` task next to `PreFlightCheck`, `Harvest`, `Package`, `PackageConsumerSmoke`, and `PublishStaging`. A maintainer reading `release.yml` should land in the same code shape regardless of which step they're chasing. Hiding the generator under `src/` or `tools/` breaks that navigation contract.

**3. The release coherence loop closes inside the build host or not at all.** The two binding-related guardrails this brief locks — the `.generated-stamp` PreFlight drift check and the Stage 2 symbol-existence validator — are Cake-stage validators. They read `.generated-stamp`, recompute vcpkg + header state, walk harvested native binaries, and report through the same `ValidationReport` / `ValidationCheck` surface as G14 / G15 / G16 / G19 / G54 / G58. Keeping the generator outside the build host while the validators sit inside it creates a fault line: the thing that **writes** the stamp is in one stack, the thing that **validates** the stamp is in another. Folding the generator in keeps the entire coherence loop in one architecture.

**4. The pure-vs-Cake split is preserved.** AGENTS.md §"Pure code stays pure" + §"Cake nativeness is a hard rule at build boundaries" both apply. The fold honors both: `HeaderSet/`, `Parsing/`, `Model/`, `Emitting/`, `Stamps/` carry no `ICakeContext`, no Cake aliases, no `Tool<TSettings>` dependencies — they're ordinary C# unit-tested from `build/_build.Tests/Unit/`. The Cake-aware shell (`GenerateBindingsTask`, `BindingGenerationRunner`, `ServiceCollectionExtensions`) is the only surface that touches Cake. This split is the **same shape** as `HarvestTask` + `BinaryClosureWalker` or `PreFlightCheckTask` + `HybridStaticOverlayValidator` — proven patterns in the existing build host. A standalone `src/` console app would not need this split, but would lose the ability to share `Tool<TSettings>` wrappers and Cake-native IO.

**5. Test infrastructure already exists.** `build/_build.Tests/` runs TUnit on Microsoft.Testing.Platform with canonical `FakeCakeWorld`, `TargetTestHost`, `FixtureLoader`, and the manifest/versions/csproj seeding helpers per [`testing-guidelines.md`](../knowledge-base/testing-guidelines.md). A standalone generator project would either (a) take a dependency on `build/_build.Tests/`'s infrastructure across project boundaries (which CPM + the existing solution layout don't make easy), or (b) duplicate the infrastructure in `tests/Janset.SDL2.Bindings.Generator.Tests/`. Either path is worse than joining the existing test project.

**Why not ppy-style external orchestrator (Python or PowerShell)?** Same five reasons amplified. ppy/SDL3-CS uses `generate_bindings.py` because their reference toolchain is ClangSharpPInvokeGenerator (a `dotnet tool`) driven by RSP files — a Python orchestrator is the path of least resistance for that stack. Our toolchain is CppAst as a `dotnet add`-able library inside a project we already own (the Cake host). Going through Python or PowerShell would add a runtime + a parsing + a process-boundary layer for zero scope benefit, plus break the §"Cake nativeness" rule.

**Why not bottlenoselabs-style multi-OS native runners?** That pattern (Windows runner + macOS runner + Linux runner + merge job) solves a different problem: header surfaces that genuinely cannot be parsed from a single host. Stage 1's preprocessor-macro-only strategy proves this is not our problem at function level — see "Multi-pass parsing strategy" below. If a future SDL bump introduces a header that defeats single-host parsing, the bottlenoselabs pattern is the documented escalation; until then it's premature multi-runner complexity.

**Local-dev story stays clean.** `tools.cs` is the canonical local-dev orchestration entry point per AGENTS.md §Common Commands. The fold preserves that — `tools.cs generate-bindings` joins `setup` / `ci-sim` / `build --target X` as siblings, runs the Cake `GenerateBindings` target inside the pinned Linux container, and writes regenerated source back to the host via volume mount. Same shape as every other `tools.cs` command; no new mental model.

## HOW

### Toolchain commitment + version-trio pin

Lock CppAst at version-trio:

```xml
<PackageVersion Include="CppAst" Version="0.24.0" />
<PackageVersion Include="libclang.runtime.linux-x64" Version="20.1.2" />
<PackageVersion Include="libClangSharp.runtime.linux-x64" Version="20.1.2" />
```

**Linux-canonical lock.** Only the `linux-x64` runtime variants are pinned. Non-Linux runtime packages (`libclang.runtime.{win-x64, osx-x64, osx-arm64, linux-arm64}`) are intentionally absent. The generator runs **exclusively inside the pinned `linux-builder` container** and fails closed when the host RID is anything other than `linux-x64`. The exact diagnostic shape lives in the Stage 1 plan; the principle is: a maintainer running on a Windows or macOS dev host invokes `tools.cs generate-bindings`, which spins up the Linux container for them — they never see the generator execute on a non-Linux host.

This is a deliberate scope shrink versus the original 2026-05-14 draft, which speculatively pinned five RID variants for "future per-RID parse-host scenarios." Those scenarios are not on the roadmap and the unused pins were dead weight. Re-pin if and when a real second host is required.

**Why the trio matters.** CppAst is a .NET library that wraps `ClangSharp` (the .NET binding to libclang), not libclang directly. CppAst 0.24.0 builds against ClangSharp 20.1.2.4, which requires libclang **20.1.x** native runtime. Installing the latest `libclang.runtime.*` (21.1.x at the time of the spike) against CppAst 0.24.0 surfaces as a `StackOverflowException` during `CppParser.ParseFiles` — verified during the spike, see [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §7.6 "Version-Trio Coupling is Real."

**Bump policy.** Do not bump any of the three packages independently. CppAst version bumps (0.24 → 0.25, when it ships) drive coordinated bumps of both runtime packages. Validation: spike-build the generator against the new trio + run the full SDL2_gfx end-to-end test (per spike-findings §8) before committing the bump. References: [CppAst NuGet](https://www.nuget.org/packages/CppAst), [CppAst v0.24.0 release notes (GitHub)](https://github.com/xoofx/CppAst/releases).

### Generator architecture — Cake build host target

Binding generation is a CI/CD concern, not a separate src-tree tool. The emitter lives **inside the Cake build host** as a target-local module, alongside the rest of the production pipeline (PreFlight, Harvest, ConsolidateHarvest, Package, PackageConsumerSmoke, Publish). This inherits — at zero ceremony cost — the Cake host's vcpkg state resolution (`Data/Manifest/`, `Data/Versions/`), `Tool<TSettings>` wrappers (`Tools/VcpkgBootstrapTool`), validation infrastructure (`Validation/`), path/runtime profile state (`Host/`), and TUnit + MTP test rig (`build/_build.Tests/`). No parallel orchestration stack.

```text
build/_build/Targets/GenerateBindings/            ← Cake target home, target-local until SDL3 creates real reuse pressure
├── GenerateBindingsTask.cs                       ← [TaskName("GenerateBindings")] — orchestration + CakeException translation
├── GenerateBindingsRequest.cs                    ← immutable target input contract per ADR-002 §2.2
├── ServiceCollectionExtensions.cs                ← AddGenerateBindings() — focused DI registration per ADR-002 §2.5
├── BindingGenerationRunner.cs                    ← Cake-side runner; invokes the pure emitter via named adapter
├── HeaderSet/                                    ← vcpkg-installed canonical header resolution + fingerprint
│   ├── HeaderSetResolver.cs
│   └── HeaderSetFingerprint.cs
├── Parsing/                                      ← libclang parse-view orchestration
│   ├── CppAstParseRunner.cs                       ← runs one parse view per platform catalog entry
│   ├── PlatformCatalog.cs                         ← (OsCondition, BackendCondition[]) tuple catalog — see "Multi-pass parsing strategy" below
│   └── ParseDiagnosticFormatter.cs
├── Model/                                        ← generator-owned declaration model
│   ├── BindingModel.cs
│   ├── DeclarationCollector.cs                    ← CppAst → BindingModel translation
│   ├── DeclarationMergePolicy.cs                  ← neutral + per-platform dedup, fail-closed on signature conflict
│   ├── CoreOwnedTypeMap.cs                        ← satellite redeclaration guard
│   └── TypeMappingPolicy.cs                       ← C → managed type mapping rules
├── Emitting/                                     ← per-category C# emitters; single foreach loop per Function
│   ├── CodeWriter.cs                              ← indentation-aware text builder
│   ├── CsCodeGenerator.cs                         ← shared emitter state + dispatch
│   ├── CsCommandEmitter.cs                        ← P/Invoke dual emit + friendly overloads
│   ├── CsHandleEmitter.cs                         ← typed readonly partial struct over nint
│   ├── CsStructEmitter.cs                         ← POD struct + union with [StructLayout]
│   ├── CsEnumEmitter.cs                           ← enum + [Flags] attribution
│   ├── CsConstantEmitter.cs                       ← const vs static readonly categorization
│   └── CsCallbackEmitter.cs                       ← delegate* unmanaged[Cdecl] emit
└── Stamps/                                       ← .generated-stamp writer (deterministic, no wall-clock fields)
    ├── GeneratedStamp.cs
    └── GeneratedStampWriter.cs

build/_build/Validation/BindingGeneration/        ← cross-cutting validators per ADR-002 §2.4
├── BindingGenerationCoherenceValidator.cs        ← PreFlight: .generated-stamp drift detection
└── (later) BindingSymbolExistenceValidator.cs    ← Pack stage: declared-but-not-exported guard (Stage 2)
```

**Cake-free emitter, Cake-orchestrated target.** The `HeaderSet/`, `Parsing/`, `Model/`, `Emitting/`, and `Stamps/` subfolders are **pure C#** — no `ICakeContext`, no Cake aliases, no Cake `Tool<TSettings>` dependencies. They can be unit-tested as ordinary code in `build/_build.Tests/Unit/`. The `GenerateBindingsTask`, `BindingGenerationRunner`, and `ServiceCollectionExtensions` are the Cake-aware shell: they consume Cake context, log through Cake, translate exceptions to `CakeException`, and stage outputs through Cake-native IO. This split honors [`AGENTS.md`](../../AGENTS.md) §"Pure code stays pure" and §"Cake nativeness is a hard rule at build boundaries" simultaneously.

**No standalone `src/Janset.SDL2.Bindings.Generator/` project.** The 2026-05-14 draft proposed a separate console app under `src/`. That framed the generator as a binding-source artifact; the revised framing treats it as build infrastructure that happens to emit binding source. The reframe avoids a parallel C# orchestration stack and reuses the existing build-host architecture.

**SDL3 path.** Stays target-local until SDL3 becomes a real second consumer. At Phase 5 (gated on PD-7 — SDL2 real-public-release), a sibling Cake target (`GenerateSdl3Bindings`) is introduced; only then does shared code, if any, get promoted out of the SDL2 target per ADR-002 §2.4 "code stays target-local by default." Mass cross-extraction up front is forbidden by the same rule.

**Invocation surface — two-layer split.**

```text
build/_build/Targets/GenerateBindings/     ← Cake target — heavy work + business orchestration:
                                              - resolve vcpkg-state (manifest.json + vcpkg.json)
                                              - vcpkg install canonical triplet (materialize headers)
                                              - drive PlatformCatalog parse views (one CppAst pass per entry)
                                              - merge + validate output (no dup core types, no signature conflicts)
                                              - write .generated-stamp (vcpkg-state hash)
                                              - write src/SDL2.<Family>/Generated/

tools.cs generate-bindings [--family X]    ← local convenience. Docker orchestration:
                                              - ensures `linux-builder` image is present
                                              - mounts repo root as volume (.dockerignore excludes vcpkg_installed/ + artifacts/)
                                              - mounts host vcpkg cache for warm restarts
                                              - publishes the Cake host (Release) into a side directory
                                              - runs `dotnet ./cake-host/Build.dll --target GenerateBindings --family X` inside the container
                                              - container writes Generated/*.g.cs back to the host via the mount
```

This split mirrors [`AGENTS.md`](../../AGENTS.md) §Build-Host Reference Pattern + §Common Commands: "Cake build host is a CI-only production pipeline ... Day-to-day dev orchestration lives in `tools.cs`." `tools.cs` stays standalone per ADR-002 §2.3.

The generator is **never** invoked at consumer build time — see "Generation environment" below.

### Emit rules — bound to feasibility §2

The generator implements all 11 emit rules in [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §2 "Modern .NET P/Invoke Emit Target." Rules are toolchain-agnostic; CppAst executes them via custom emitter code in the partial files above. Key locks this brief makes against the rule set:

| Rule | Locked decision |
|---|---|
| Rule 1 — Attribute | `[LibraryImport]` net7+, `[DllImport]` legacy, **dual-emit per function** via `#if NET7_0_OR_GREATER` in the same emitter loop |
| Rule 2 — Opaque handles | **Typed `readonly partial struct Name(nint value)`** per the Decision Hypothesis lock. Alimer/Vortice ecosystem pattern, verified against [Alimer Handles.cs](https://raw.githubusercontent.com/amerkoleci/Alimer.Bindings.SDL/main/src/Alimer.Bindings.SDL/Generated/Handles.cs) and [Vortice.Vulkan Handles.cs](https://raw.githubusercontent.com/amerkoleci/Vortice.Vulkan/main/src/Vortice.Vulkan/Generated/Handles.cs) |
| Rule 3 — Numeric IDs | Typed `enum Name : uint` / `enum Name : ulong` for SDL `*ID` types; per [Microsoft Learn — Best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices) "DO use .NET types that map closest to the native type" |
| Rule 4 — UTF-8 strings | Triple overload (`byte*` / `ReadOnlySpan<byte>` / `string` w/ `StringMarshalling.Utf8`); emitted in same loop as raw P/Invoke (see "Friendly overloads" below). [Microsoft Learn — Custom marshalling source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/custom-marshalling-source-generation) for `Utf8StringMarshaller` semantics |
| Rule 5 — Boolean wire types | **SDL2 `SDL_bool` → int-backed enum/wrapper**; **SDL3 `bool` → 1-byte wrapper struct**. Never raw `bool`. Per [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §2 Rule 5; SDL2/SDL3 ABI is genuinely different |
| Rule 6 — Buffers | `Span<T>` / `ReadOnlySpan<T>` overloads + raw-pointer overload (hot path); `out T` for single-element output; never `Memory<T>` in P/Invoke |
| Rule 7 — Callbacks | `delegate* unmanaged[Cdecl]<...>` + `[UnmanagedCallersOnly]`; never `Delegate` or `Marshal.GetFunctionPointerForDelegate` |
| Rule 8 — Constants | `public const` for literal numerics / strings; `public static readonly` for computed expressions; categorized per `CppMacro` shape |
| Rule 9 — AOT | `<IsAotCompatible>true</IsAotCompatible>` on every generated binding csproj (net8+ TFMs); no `[RequiresDynamicCode]` / `[RequiresUnreferencedCode]` |
| Rule 10 — Modern C# emit | C# 14 features: collection expressions (`CallConvs = [typeof(CallConvCdecl)]`), `params ReadOnlySpan<T>`, ref-struct constraints |
| Rule 11 — Partial-class boundaries | One `partial class SDL2` / `partial class SDL2_image` per family; split across `Commands.g.cs` / `Constants.g.cs` / `Enums.g.cs` / `Handles.g.cs` / `Structs.g.cs` / `Callbacks.g.cs` |

### Multi-pass parsing strategy — preprocessor-macro switching inside one Linux container

Local SDL2 header inspection (verified 2026-05-14, see [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §3) confirms platform-conditioned public surface in `SDL_system.h`, `SDL_main.h`, `SDL_platform.h`, `SDL_config.h`, `SDL_stdinc.h`, `SDL_syswm.h`. This is a libclang/preprocessor constraint — each parse produces the AST for **one** macro configuration.

**Pattern: ppy-style preprocessor-macro switching, CppAst-executed, single Linux container.** Each pass is a `CppParserOptions` instance that:

1. **Undefines** every SDL platform identification macro from a fixed master list (so a prior pass's defines cannot leak in).
2. **Defines** exactly the (OS, backend[]) macro group for the pass being run.
3. Parses against the same vcpkg-installed SDL header set + the Linux container's apt sysroot.

There is **no `--target` cross-compile flag**. There is **no mingw-w64**. There is **no Apple SDK**. SDL's own public headers carry the forward declarations (`typedef struct _NSWindow NSWindow;`, `typedef struct ANativeWindow ANativeWindow;`, `struct gbm_device;`, `struct IInspectable;`, etc.) needed to parse function signatures across every platform branch — confirmed by reading `vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_syswm.h` on 2026-05-15 (lines 86, 94-95, 105-106, 109-110, 120). Function-level platform surface parses without any hand-written platform stubs.

**Evidence for the preprocessor-only approach:**

- **ppy/SDL3-CS** (the closest production peer for our scope, see [`binding-autogen-approaches.md`](research/binding-autogen-approaches.md) §2026-05-12 comparison) uses `--undefine-macro` + `--define-macro` per pass exclusively. Their Dockerfile does not install mingw-w64; their entire stub-header inventory is a single 81-byte `include/process.h` shim. Verified via WebFetch on 2026-05-15 against [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS) Dockerfile + `generate_bindings.py`.
- **Our own CppAst spike** (`tools/binding-spike/cppast/generator/Program.cs` lines 42-72) used the same pattern — `Defines = ["_WIN32=1", "WIN32=1", "__WIN32__=1", "__WINDOWS__=1"]` plus `Undefines = ["linux", "__linux", "__linux__", "__LINUX__", "__APPLE__", "__MACOSX__"]` for the Windows pass, mirror-image for Linux and macOS — and parsed `SDL_system.h` + `SDL_main.h` successfully across three platforms (9 neutral + 8 Windows + 2 Linux functions, runtime-validated). The spike used 10 C-stdlib shim headers (`stdint.h`, `stddef.h`, `ctype.h`, etc.) only because it ran on a Windows host targeting Linux; **inside the Linux container those shims fall away** — the apt sysroot provides every C stdlib header natively.

The earlier draft of this brief speculated about mingw-w64 and Apple SDK header stubs as Stage 1 inputs. That speculation was wrong and has been retracted — see Decision Audit row "Error 4 — toolchain stub strategy speculation" below.

**Platform catalog — (OsCondition, BackendCondition[]) tuple model.** A catalog entry corresponds to one parse view. The catalog is a small data structure (~7–8 entries for SDL2.Core in Stage 1), not a flat macro array. Each entry pairs one OS identification macro group with the `SDL_VIDEO_DRIVER_*` backend macros that ship as compile-time-enabled video drivers on that OS. Stage 1 catalog (illustrative, final shape lives in the Stage 1 plan's `PlatformCatalog.cs`):

| Catalog entry | OS macros defined | Backend macros defined |
|---|---|---|
| Neutral | (none) | (none) |
| Windows desktop | `_WIN32`, `WIN32`, `__WIN32__`, `__WINDOWS__` | `SDL_VIDEO_DRIVER_WINDOWS` |
| WinRT | `_WIN32`, `__WINRT__` | `SDL_VIDEO_DRIVER_WINRT` |
| GDK | `_WIN32`, `__GDK__`, `__WINGDK__` | `SDL_VIDEO_DRIVER_WINDOWS` |
| Linux | `__linux__`, `__LINUX__`, `linux` | `SDL_VIDEO_DRIVER_X11`, `SDL_VIDEO_DRIVER_WAYLAND`, `SDL_VIDEO_DRIVER_KMSDRM` |
| macOS | `__APPLE__`, `__MACOSX__` | `SDL_VIDEO_DRIVER_COCOA` |
| iOS | `__APPLE__`, `__IPHONEOS__` | `SDL_VIDEO_DRIVER_UIKIT` |
| Android | `__ANDROID__` | `SDL_VIDEO_DRIVER_ANDROID` |

Niche backends (DirectFB, Vivante, MIR, OS/2) are not part of the Stage 1 catalog. They are documented exclusions; revisit only if a consumer file an issue against the missing surface.

The "master undefine list" the parser nukes at the start of every pass is the union of all OS macros plus all `SDL_VIDEO_DRIVER_*` macros (~30 entries). That list is hygiene; the **number of passes equals the catalog size, not the master-list size.**

**Output topology:**

```text
src/SDL2.Core/Generated/
├── Commands.g.cs            ← neutral pass: functions present on all platforms
├── Constants.g.cs           ← neutral pass
├── Enums.g.cs               ← neutral pass
├── Handles.g.cs             ← neutral pass — typed readonly structs (Rule 2)
├── Structs.g.cs             ← neutral pass — POD layouts
├── Callbacks.g.cs           ← neutral pass — delegate* aliases
├── .generated-stamp         ← vcpkg-state coherence marker (see "vcpkg-state coherence guardrail" below)
└── Platform/
    ├── Windows/             ← [SupportedOSPlatform("windows")]
    │   ├── SDL_system.Windows.g.cs
    │   └── SDL_main.Windows.g.cs
    ├── WinRT/               ← [SupportedOSPlatform("windows10.0.10240.0")] or similar — exact attribute shape is Stage 1 plan deliverable
    ├── GDK/
    ├── Linux/
    ├── OSX/                 ← [SupportedOSPlatform("osx")]
    ├── IOS/
    └── Android/
```

**Dedup + conflict rules** (per [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §3 multi-platform parsing baseline):

1. Run neutral pass first; emit its symbol set into common files.
2. Run each platform pass with exactly one catalog entry active.
3. Exclude symbols already emitted by the neutral pass from platform files.
4. Emit platform-only symbols into platform-suffixed files with `[SupportedOSPlatform]` attribution.
5. **Fail generation** (do not silently guess) if the same symbol appears in multiple views with incompatible signatures or layout-affecting type differences.

**`SDL_syswm.h` struct/union layout is Stage 2, not Stage 1.** The function-level platform spike intentionally deferred `SDL_SysWMinfo` and `SDL_SysWMmsg` union layout — see [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §8.7. Stage 1 emits `SDL_GetWindowWMInfo` as a function with an opaque `nint` (or `SDL_SysWMinfo*` modeled as opaque) parameter; the typed union layout, the 64-byte fixed-size lock (per `SDL_syswm.h` line 348), and the minimal forward-declaration stub set (~15–20 opaque types — `HWND`/`HDC`/`HINSTANCE`/`Display*`/`Window`/`IInspectable`/`gbm_device`/`wl_display`/`wl_surface`/`xdg_*`/`EGLNativeDisplayType`/`IDirectFB*` and a handful of others) are delivered together at Stage 2. Apple-SDK redistribution concerns do not apply because SDL's header already provides the non-`__OBJC__` `typedef struct _NSWindow NSWindow;` forward declaration directly (`SDL_syswm.h:86`); the Stage 2 stub library is purely forward declarations, not Apple SDK derivatives.

**True multi-OS extraction stays an escalation path.** If a header surface emerges that single-host preprocessor-macro switching cannot model — for example, a hypothetical SDL bump that introduces ABI-affecting compiler-intrinsic dependencies — the `bottlenoselabs/SDL3-cs` pattern (native multi-OS runners + merge) is the documented fallback. See [`binding-autogen-approaches.md`](research/binding-autogen-approaches.md) §2026-05-12 multi-platform parsing comparison.

### Satellite / shared-types topology

SDL satellites are not independent type islands. Local header inspection (per [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §3 satellite-headers table) confirms:

| Satellite header | Includes | Shared core types observed |
|---|---|---|
| `SDL_image.h` | `SDL.h`, `SDL_version.h` | `SDL_Surface`, `SDL_Texture`, `SDL_Renderer`, `SDL_RWops` |
| `SDL_ttf.h` | `SDL.h` | `SDL_Color`, `SDL_Surface`, `SDL_Renderer`, `SDL_Texture`, `SDL_bool`, `SDL_version` |
| `SDL_mixer.h` | `SDL_stdinc.h`, `SDL_rwops.h`, `SDL_audio.h`, `SDL_endian.h`, `SDL_version.h` | `SDL_RWops`, `SDL_bool`, `SDL_AudioSpec`, `SDL_version` |

**Lock: core-owned shared type universe.** `Janset.SDL2.Core` owns every `SDL_*` core struct, enum, handle, callback, and constant. Satellite generators emit only satellite-owned surface (`IMG_*`, `Mix_*`, `TTF_*`, `gfx*`/SDL2_gfx-family symbols, `Net_*`) plus any truly satellite-owned types (e.g., `IMG_Animation`, `Mix_Chunk`, `TTF_Font`). Satellite signatures reference core-owned managed types via cross-csproj reference — never redeclare.

Validation rule (new G-guardrail candidate, see "Symbol-existence validation guardrail" below + WHAT impact inventory): **fail generation if a satellite output redefines a core-owned type name** or lowers a known core type to an untyped fallback because the type map was missing.

**Spike validation** ([`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §8.7 SDL2_image result): CppAst generated `SDL_image.h` into a separate image binding project while referencing a separate core-types project for core SDL concepts. Generated image output compiled, emitted 59 `IMG_*` extern declarations, emitted image-owned `ImgInitFlags` + opaque `ImgAnimation`, and reused core-owned `SDL_version` / `SDL_Surface` / `SDL_Texture` / `SDL_Renderer` / `SDL_RWops` through analyzer-clean managed names — **no duplicate core type declarations.** This is the topology pattern Plan Shape Stage 2 (SDL2 satellite sweep) generalizes. References: [ppy/SDL3-CS package structure](https://github.com/ppy/SDL3-CS) (separate `SDL3_image-CS`, `SDL3_mixer-CS`, `SDL3_ttf-CS` packages referencing `SDL3-CS` core).

### Multi-TFM dual emit — full matrix, single emitter loop

Per [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §2 Rule 1, every P/Invoke is emitted twice in one `foreach` iteration:

```csharp
internal static partial class SDL2
{
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "SDL_CreateWindow", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial SDL_Window SDL_CreateWindow(string title, int x, int y, int w, int h, SDL_WindowFlags flags);
#else
    [DllImport(LibName, EntryPoint = "SDL_CreateWindow",
               CallingConvention = CallingConvention.Cdecl, ExactSpelling = true,
               CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern SDL_Window SDL_CreateWindow(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title, int x, int y, int w, int h, SDL_WindowFlags flags);
#endif
}
```

**Why dual-emit in the same loop (not post-process).** Per the scope-trajectory argument (WHY section above), CppAst's single emitter pass owns this in ~50 LoC of iteration logic inside `Program.cs`. ClangSharp's equivalent would be a separate Roslyn post-process project — see [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §7.8.

**Compatibility deltas under LibraryImport** (drop in net7+ branch): `CallingConvention`, `CharSet`, `BestFitMapping`, `ThrowOnUnmappableChar`, `ExactSpelling`, `PreserveSig`. See [Microsoft Learn — P/Invoke source generation §Differences from DllImport](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation) + the [dotnet/runtime LibraryImportGenerator Compatibility doc](https://github.com/dotnet/runtime/blob/main/docs/design/libraries/LibraryImportGenerator/Compatibility.md).

**TFM scope reach.** Full matrix preserves consumer reach for `.NET Framework 4.6.2+` (relevant for legacy Unity, Xamarin, older game engines) + `netstandard2.0` (Mono / older library consumers) while still landing AOT-clean LibraryImport on `net8+`. The cost of dual emit is paid once in the emitter; the smoke matrix per [`AGENTS.md`](../../AGENTS.md) §Build Host Pipeline already covers all 5 TFMs per family.

**`AllowUnsafeBlocks`** must be on for every generated `.csproj`. AOT-compatible flag (`IsAotCompatible=true`) applies only to net8+ TFMs — `netstandard2.0` / `net462` don't have the concept.

### Friendly overloads — same emitter loop, no separate Roslyn extension

Friendly overloads are emitted **alongside** the raw P/Invoke, in the same iteration over each function. Pattern from [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §7.8 single-loop emit:

```csharp
foreach (var func in compilation.Functions.Where(InTargetHeader))
{
    EmitMultiTFMPInvoke(sb, func);                                                       // raw layer (above)

    if (func.Parameters.Any(IsUtf8StringParam))                                          // Rule 4
        EmitFriendlyStringOverload(sb, func);

    if (HasArrayWithCountPattern(func))                                                  // Rule 6
        EmitSpanOverload(sb, func);

    if (func.Parameters.Any(IsOutputPointer))                                            // Rule 6
        EmitRefOutOverload(sb, func);

    if (IsPlatformConditional(func))                                                     // Rule 4 from multi-pass
        AnnotateWithPlatformAttribute(sb, func);
}
```

**Per-rule emission detail:**

- **UTF-8 string overloads (Rule 4):** triple emit — `byte*` (hot path) / `ReadOnlySpan<byte>` (stackalloc) / `string` (via `StringMarshalling.Utf8` under LibraryImport, via `[MarshalAs(UnmanagedType.LPUTF8Str)]` under DllImport).
- **Span<T> overloads (Rule 6):** for `T*` + length patterns, emit `Span<T>` / `ReadOnlySpan<T>` overload + retain raw `T*` overload for hot paths. Use `[MarshalUsing(CountElementName = nameof(count))]` under LibraryImport.
- **`out T` / `ref T` overloads (Rule 6):** for single-element output pointers (`int*` → `out int`), emit idiomatic overload + retain raw pointer overload.
- **No `Marshal.GetFunctionPointerForDelegate`** — callbacks use `delegate* unmanaged[Cdecl]<...>` + `[UnmanagedCallersOnly]` per Rule 7 + [Microsoft Learn best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices) "DO prefer using function pointers and `[UnmanagedCallersOnlyAttribute]` as opposed to `Delegate` types."

**Why not Roslyn source generator on top of raw output (ppy pattern).** Would work, but adds a second project + second mental model + second version-coordination story for no scope-trajectory gain — see WHY section above. CppAst emits friendly overloads directly because we can; ClangSharp couldn't.

**`Utf8StringMarshaller` availability.** Lives in `System.Runtime.InteropServices.Marshalling` since `net7`. Under `netstandard2.0` / `net462` branches we hand-roll the byte-pinning marshal logic in the same emitter (~15 LoC per overload, generator-emitted, not consumer-side). See [Microsoft Learn — Custom marshalling source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/custom-marshalling-source-generation).

### Generation pipeline — separate workflow, manual trigger, auto-PR

Generation pipeline is **structurally separate** from the release pipeline. Two pipelines, two trigger surfaces, two artifact lineages:

| Pipeline | File | Trigger | Output |
|---|---|---|---|
| Release | [`release.yml`](../../.github/workflows/release.yml) | `workflow_dispatch` (manifest-derived / explicit) | Native + managed `.nupkg` set, staged to GitHub Packages |
| Regenerate Bindings | `.github/workflows/regenerate-bindings.yml` (new) | `workflow_dispatch` (family selector) | Pull request against `master` with regenerated `src/SDL2.<Family>/Generated/*.g.cs` + updated `.generated-stamp` |

**Reference shape:** [Silk.NET's `bindings-regeneration.yml`](https://github.com/dotnet/Silk.NET/blob/main/.github/workflows/bindings-regeneration.yml). It's the closest 2026 production peer using a manual-trigger + auto-PR pattern. Compared to ppy/SDL3-CS and Alimer (both manual-local-run-and-commit), Silk.NET's pattern:

- Surfaces the regen diff as a GitHub PR — review-able, comment-able, blocking-able.
- Aligns with [`AGENTS.md`](../../AGENTS.md) §Approval Gate ("Before Any Commit / Present a summary of changes and a proposed commit message; ask for approval first") — the maintainer reviews and merges the PR manually.
- Aligns with [`release-strategy.md`](../release-strategy.md) §Strategic Stance ("Pride-driven, not deadline-driven ... Don't ship until the maintainer is willing to put their name on it publicly") — no automatic commit-to-master path.

**Workflow shape** (sketch; Phase 4 plan finalizes exact YAML):

```yaml
name: Regenerate Bindings

on:
  workflow_dispatch:
    inputs:
      family:
        description: 'Family to regenerate (sdl2-core / sdl2-image / sdl2-mixer / sdl2-ttf / sdl2-gfx / sdl2-net / sdl3-* / all)'
        required: true
        default: 'all'

permissions:
  contents: write          # peter-evans/create-pull-request needs commit + push to branch
  pull-requests: write     # peter-evans/create-pull-request needs to open the PR

jobs:
  build-cake-host:
    # Same shape as release.yml's build-cake-host job. Phase 4 plan can extract
    # into a reusable composite action if drift between the two surfaces surfaces.
    ...

  generate:
    needs: [build-cake-host]
    runs-on: ubuntu-24.04
    container: ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest
    steps:
      - uses: actions/checkout@v6
        with:
          submodules: recursive
      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json
      - uses: ./.github/actions/vcpkg-setup
        with:
          platform-identity: ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest
          triplet: x64-linux-hybrid
          vcpkg-cache-path: .vcpkg-cache
          vcpkg-feature-flags: binarycaching
      - uses: actions/download-artifact@v8
        with:
          name: cake-host
          path: ./cake-host
      - name: Generate bindings
        run: >
          dotnet ./cake-host/Build.dll
          --target GenerateBindings
          --family ${{ inputs.family }}
      - uses: peter-evans/create-pull-request@v7
        with:
          base: master
          branch: auto-regen/${{ inputs.family }}-${{ github.run_id }}
          title: "regen: ${{ inputs.family }} bindings (${{ github.run_id }})"
          body: |
            Auto-generated by `regenerate-bindings.yml` run ${{ github.run_id }}.

            Family: ${{ inputs.family }}
            CppAst: 0.24.0, libclang: 20.1.2

            Review the diff before merging. See `docs/binding-autogen/binding-autogen-strategy-brief.md`.
          delete-branch: true
```

**Reusable surface from [`release.yml`](../../.github/workflows/release.yml):**

| Component | Reuse strategy |
|---|---|
| `build-cake-host` job | Conceptually identical; Phase 4 plan extracts into composite action OR copies the steps (decision deferred — depends on drift signal across pipelines). |
| [`vcpkg-setup`](../../.github/actions/vcpkg-setup) composite action | Reused as-is. Same vcpkg bootstrap, same cache. Triplet differs (single `x64-linux-hybrid` here vs matrix in release). |
| [`nuget-cache`](../../.github/actions/nuget-cache) composite action | Reused as-is for the cake-host build step. |
| Generated `cake-host` artifact (FDD published) | Reused as-is. Same `dotnet ./cake-host/Build.dll --target X` invocation shape, just different `--target`. |

**peter-evans/create-pull-request@v7** is the ecosystem standard for Action-driven PR creation. Used by Silk.NET, dotnet/runtime, and many others. Maintains branch state idempotency (re-running closes the previous PR + opens a fresh one), supports custom commit messages + body templating + label assignment. Reference: [peter-evans/create-pull-request](https://github.com/peter-evans/create-pull-request).

**No tag-push trigger.** The release pipeline reacts to maintainer-cut release tags; the regeneration pipeline does not. Per [`release-strategy.md`](../release-strategy.md) §Maintenance Commitment Post-v1.0 ("AST regeneration on upstream bumps ... aim ≤4 weeks from upstream stable release to Janset release. No formal SLA"), the cadence is **maintainer-decides**, not upstream-triggered. Future automation (e.g., scheduled regen on SDL upstream tag) is a Phase 5+ enhancement, not a v1.0 blocker.

### Generation environment — Linux container is the canonical determinism contract

**Docker is a hard prerequisite.** The generator runs **only** inside the pinned `linux-builder` container. There is no host-OS fallback: a `tools.cs generate-bindings` invocation on a Windows or macOS dev machine starts the container and runs the generator there. The runtime guard inside `GenerateBindingsTask` fails closed with an actionable diagnostic if the host RID is anything other than `linux-x64`. This is the price of the reproducibility contract — Layer 5 of the test strategy requires byte-identical output across CI and local-dev runs of the same vcpkg/toolchain state, which is only achievable when every parse view executes inside the same OS, apt sysroot, and libclang runtime.

**Container image.** Generation environment reuses [`linux-builder.Dockerfile`](../../docker/linux-builder.Dockerfile) as-is. The image already carries:

- vcpkg deps (X11 / Wayland / EGL / Vulkan / ALSA dev headers + autoconf 2.72 + GCC 11 + cmake + ninja). Required for `vcpkg install` step that materializes SDL headers, and (incidentally) for the `SDL_VIDEO_DRIVER_X11` / `SDL_VIDEO_DRIVER_WAYLAND` / `SDL_VIDEO_DRIVER_KMSDRM` parse passes — the apt sysroot resolves their dev headers natively.
- `git config --system --add safe.directory '*'`. Required for the workflow's `actions/checkout` + workspace ownership and for `tools.cs generate-bindings`'s repo-root volume mount.
- `python3 + python3-pip + python3-jinja2`. Not strictly required for our CppAst-based generator (no Python orchestration), but reused in the same image so CI parity with release.yml stays simple.

**Deliberately not in the image:**

- **`.NET SDK`** — per the Dockerfile's own comment ("`actions/setup-dotnet` handles runtime version pinning per-job (symmetric with Windows / macOS runners)"). `regenerate-bindings.yml`'s setup-dotnet step installs .NET 10 SDK at job start, matching the [`global.json`](../../global.json) pin.
- **libclang native runtime** — pulled in transitively via CppAst's `libclang.runtime.linux-x64` NuGet (version-trio pinned per "Toolchain commitment" above). Deterministic regardless of host apt state.
- **mingw-w64 cross-toolchain.** Intentionally not installed; the preprocessor-macro switching strategy (see "Multi-pass parsing strategy" above) makes it unnecessary.
- **Apple SDK headers.** Not redistributable and not needed. SDL2 headers provide the cross-platform forward declarations the parser requires for macOS/iOS branches.

This means the existing image is **sufficient** for generation — no new Dockerfile required. Image build cadence stays on the existing monthly rebuild cron documented in the Dockerfile preamble.

**Single canonical triplet for headers.** Generation pipeline runs `vcpkg install` for **one** triplet: `x64-linux-hybrid` (Linux container's native target). Rationale:

- vcpkg has no "headers-only" install mode; full install with binary caching is the path. Cold install matches Harvest cost; cached install completes fast.
- Header byte-identity check in [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §3 confirmed: SDL2 public headers are byte-identical (post-CRLF-normalization) across all 7 vcpkg-installed triplets. Parsing one triplet's headers is sufficient input for all OS parse views.
- Cross-target parse views (per "Multi-pass parsing strategy" above) operate on the same canonical header set with different libclang `--target` / macros / sysroot settings.

**Header byte-identity check as a CI step.** Belt-and-suspenders: generation pipeline includes a step that verifies SDL public header SHAs match across triplet outputs if more than one triplet has been installed in the cache. Catches the rare case where a vcpkg port patch differs by triplet. Cheap (~seconds), high signal.

**Local dev — `tools.cs generate-bindings` shorthand.** Local development invokes the same container via Docker. `tools.cs` simulates the CI/CD flow: publishes the Cake host once (Release, side directory), mounts the repo root + vcpkg cache into the container, runs the Cake target, and lets the volume mount carry the regenerated source files back to the host:

```csharp
// tools.cs — sketch; Stage 1 plan owns exact shape per AGENTS.md §"tools.cs stays standalone"
if (subcommand == "generate-bindings")
{
    var image = "ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest";
    var family = args.GetOptional("--family") ?? "all";
    var workspace = Directory.GetCurrentDirectory();
    var vcpkgCache = Path.Combine(workspace, ".vcpkg-cache");
    var cakeHost = Path.Combine(workspace, ".cake-host");

    // 1. Publish the Cake host in Release once. Same artifact shape as release.yml's build-cake-host job.
    Cli.Wrap("dotnet").WithArguments(["publish", "build/_build/Build.csproj", "-c", "Release", "-o", cakeHost])
       .WithWorkingDirectory(workspace).ExecuteAsync().GetAwaiter().GetResult();

    // 2. Run the GenerateBindings Cake target inside the container.
    //    .dockerignore excludes vcpkg_installed/, artifacts/, .vs/, bin/, obj/ — container does its own vcpkg install against the cache.
    var dockerArgs = new[]
    {
        "run", "--rm",
        "-v", $"{workspace}:/workspace",
        "-v", $"{vcpkgCache}:/workspace/.vcpkg-cache",
        "-w", "/workspace",
        image,
        "bash", "-c",
        $"dotnet /workspace/.cake-host/Build.dll --target GenerateBindings --family {family}",
    };

    Cli.Wrap("docker").WithArguments(dockerArgs).ExecuteAsync().GetAwaiter().GetResult();
}
```

Joins the established commands enumerated in [`AGENTS.md`](../../AGENTS.md) §Common Commands (`tools.cs setup`, `tools.cs ci-sim`, `tools.cs build --target X`). The exact arg shape, error handling, and `tools.cs`-side vcpkg-cache bootstrap policy are Stage 1 plan deliverables.

**Local-dev prerequisites:**

| OS | Prerequisite |
|---|---|
| Windows | Docker Desktop + WSL2 backend |
| macOS | Docker Desktop (or Colima / OrbStack) |
| Linux | Native Docker / Podman with docker-cli wrapper |

These get documented in [`local-development.md`](../playbook/local-development.md) as a new section ("Regenerating bindings") at Phase 4 plan time. Reference precedent in the playbook: the existing `tools.cs setup` workflow already requires vcpkg + Cake bootstrap — Docker is an additive prerequisite, not a paradigm shift.

**Output flow.** Container writes generated `.cs` files + `.generated-stamp` to mounted workspace via Docker volume bind. Maintainer reviews `git status` / `git diff` after the run, stages + commits manually ([`AGENTS.md`](../../AGENTS.md) §Approval Gate — even local). The local flow is the same shape as `tools.cs setup` writing `build/msbuild/Janset.Local.props` + `artifacts/packages/`.

### vcpkg-state coherence guardrail

**The drift class this catches.** Two pipelines + one repo + one consumer = a third-state risk:

- t=0: `vcpkg.json` @ baseline-A; bindings regenerated @ A; natives built @ A → coherent.
- t=1: maintainer bumps `vcpkg.json` to baseline-B (or `build/manifest.json library_manifests[].vcpkg_version` ticks to a new SDL release); runs `release.yml` → natives built @ B; **bindings still @ A** because no one ran `regenerate-bindings.yml`.
- t=2: release wave shipped. Consumer installs `Janset.SDL2.Image 2.8.0`. Native payload's SDL_image ABI is @ B; managed binding's P/Invoke signatures are @ A. Mismatch surfaces as `EntryPointNotFoundException`, struct layout corruption, or silent undefined behavior.

[ADR-001](../decisions/2026-05-05-d3seg-and-package-first.md) G54 ("UpstreamMajor.Minor anchored to `manifest.library_manifests[].vcpkg_version`, enforced at PreFlight") catches the coarse case — maintainer-visible SDL minor bumps. It does not catch "manifest unchanged, vcpkg baseline bumped, port patches changed" or "manifest bumped + natives rebuilt + bindings not regenerated."

**New guardrail (locked here; design owned by Phase 4 plan):** `.generated-stamp` file per family + PreFlight validator.

**Stamp file location + schema:**

```text
src/SDL2.Core/Generated/.generated-stamp
src/SDL2.Image/Generated/.generated-stamp
src/SDL2.Mixer/Generated/.generated-stamp
...
src/SDL3.Core/Generated/.generated-stamp
...
```

```json
{
  "generated_at": "2026-05-14T15:00:00Z",
  "generator_version": "Janset.SDL2.Bindings.Generator 1.0.0+<gitsha>",
  "cppast_version": "0.24.0",
  "libclang_version": "20.1.2",
  "vcpkg_state": {
    "vcpkg_json_sha256": "<sha256 of vcpkg.json>",
    "vcpkg_baseline": "<commit-sha from vcpkg-configuration.json builtin-baseline>",
    "manifest_library_version": "2.32.10"
  },
  "headers": {
    "source_triplet": "x64-linux-hybrid",
    "header_set_sha256": "<sha256 of sorted concatenation of every parsed header's content>",
    "header_count": 88
  },
  "platform_passes": ["neutral", "windows", "linux", "osx"]
}
```

Schema is Phase 4 plan's deliverable; the brief locks the **principle** (stamp file per family, contains vcpkg-state hash + generator version + header-set hash). The exact field set above is the proposed starting point.

**Validation (PreFlight stage, before Harvest):**

1. For each family with a `.generated-stamp`, recompute the expected `vcpkg_state` from current `vcpkg.json` + `vcpkg-configuration.json` + `build/manifest.json`.
2. Recompute the expected `header_set_sha256` from the resolved canonical header set (requires a fast vcpkg install for the canonical triplet, or cached headers from `.vcpkg-cache`).
3. If any family's stamp differs from current state → **fail PreFlight** with actionable error:

```
[PreFlight ERROR] Binding-source drift detected for family 'sdl2-image':
  .generated-stamp vcpkg_state.vcpkg_baseline = abc123...
  current vcpkg_state.vcpkg_baseline        = def456...
  Action: Run regenerate-bindings.yml workflow for family 'sdl2-image',
          review the resulting PR, merge before re-running release.yml.
```

**Behavior-first name** (per [`AGENTS.md`](../../AGENTS.md) §"Guardrail IDs are not code names"): `BindingVcpkgCoherenceValidator` (or similar — final at Phase 4 plan). Lives under `build/_build/Validation/` per [ADR-003](../decisions/2026-05-12-build-host-data-layer.md) data-layer pattern. Joins existing PreFlight validators alongside `HybridStaticOverlayValidator`, manifest schema validators, etc.

**Final G-ID** assigned at Phase 6 guardrail-catalog refresh in [`release-guardrails.md`](../knowledge-base/release-guardrails.md). Conceptual sibling to G54 (vcpkg_version anchor) + G58 (cross-family resolvability) + G19 (hybrid-static leak detection). The catalog gains a new row capturing: failure mode, detection mechanism, ownership stage (PreFlight), bypass path (run regenerate-bindings.yml).

**Why this beats just G54 + manual discipline.** G54 alone catches "manifest bumped without regen" (manifest is the source of truth for upstream version). It does NOT catch:

- vcpkg-baseline bump without manifest version change (port patch upgrades that don't shift upstream SDL version)
- generator version bump (CppAst 0.24 → 0.25) without regen — emit-rule changes could ship different signatures from the same headers
- header source drift (vcpkg port patch applied to a header — rare but real)

The stamp file makes all three explicit. Maintenance asymmetry is small (one JSON file per family, written by generator, read by validator), benefit is high (closes the entire "binding ↔ native ABI coherence" failure class).

**Integration with [ADR-001](../decisions/2026-05-05-d3seg-and-package-first.md):** the stamp file is **additive** to D-3seg + family-lock. ADR-001 anchors family version to upstream SDL minor; the stamp anchors binding source to vcpkg state. Same family release wave carries both contracts; PreFlight enforces both.

### Symbol-existence validation guardrail

**Failure mode.** AST parsing surfaces every function *declared* in the header. Not every declared function is *exported* from the compiled satellite. Per [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §4 "Symbol Visibility Risk":

| Platform | Risk level | Why |
|---|---|---|
| Windows | Low | PE format is export-opt-in; SDL2 `DECLSPEC` → `__declspec(dllexport)`. Undecorated header declarations are inline / internal. |
| Linux | Medium | `-fvisibility=hidden` hides everything by default; SDL2 `DECLSPEC` → `__attribute__((visibility("default")))`. Missing `DECLSPEC` annotations cause runtime `EntryPointNotFoundException`. |
| macOS | Medium | Same mechanism as Linux. |

**Concrete precedent:** PD-15 (SDL2_gfx Unix symbol-export regression) is the open-guard-item poster child. SDL2_gfx is third-party, may lack proper `DECLSPEC` annotations under `-fvisibility=hidden`, may export less than headers declare. Currently no automated catch.

**New Pack-stage guardrail (locked here, design owned by Phase 4 plan):** For every emitted `[LibraryImport(EntryPoint = "FuncName")]` (or `[DllImport(EntryPoint = ...)]`), assert `FuncName` exists in the corresponding satellite's exported symbol table:

| Platform | Tool | Command |
|---|---|---|
| Windows | `dumpbin /exports` | `dumpbin /exports SDL2.dll` |
| Linux | `nm -D --defined-only` or `readelf --dyn-syms` | `nm -D libSDL2-2.0.so` |
| macOS | `nm -gU` | `nm -gU libSDL2-2.0.dylib` |

These tools are already wrapped in `build/_build/Tools/` per [`AGENTS.md`](../../AGENTS.md) §Build-Host Reference Pattern (`VcpkgBootstrapTool`'s sibling toolset). New validator joins `G46–G58` in the [release-guardrails.md](../knowledge-base/release-guardrails.md) catalog — behavior-first naming convention per [`AGENTS.md`](../../AGENTS.md) §"Guardrail IDs are not code names"; final ID assigned at Phase 6 guardrail-catalog refresh per the [`phase-planning-methodology.md`](../parking-lot/package-topology/phase-planning-methodology.md) precedent.

**Integration with Phase 2b Linux version scripts.** Per [`symbol-visibility-analysis.md`](../research/symbol-visibility-analysis.md), Phase 2b adds Linux version scripts (`.map` files) per satellite. Once those land, the exported symbol set tightens to glob-pattern-driven (`SDL_*` / `IMG_*` / `Mix_*` / `TTF_*` / `Net_*` / `gfx*`). Statically check generator output against the version script's pattern — simpler than per-symbol dynamic lookup; same failure-mode coverage.

**Pack-stage placement.** The validator runs **after the harvest stage** (so per-RID native binaries are available) and **before the package stage** (so a binding emitting an unexported symbol fails the build before NuGet packaging). Fits the existing 5-stage Cake pipeline per [`AGENTS.md`](../../AGENTS.md) §Build Host Pipeline.

**Two guardrails, two stages, one coherent contract.** The vcpkg-state coherence guardrail above catches "did the bindings come from the right vcpkg state?" at PreFlight (before any building). The symbol-existence guardrail catches "do the bindings reference symbols the natives actually export?" at Pack (after harvesting natives, before publishing). Together they close the binding ↔ native coherence loop for v1.0 stable.

## WHAT

### Impact inventory

The brief reshapes work across project structure, build host, CI surface, generated outputs, retired imports, and downstream docs. Phase 4 implementation plan owns slice sequencing; this table is the mechanical surface that plan slices against.

| Area | Current shape | Expected impact |
| --- | --- | --- |
| `external/sdl2-cs` submodule | Source-of-truth for managed SDL2 P/Invoke; consumed via `<Compile Include="../../external/sdl2-cs/src/<Family>.cs" />` in `src/SDL2.<Family>/<Family>.csproj` | **Retire** after AST output passes runtime smoke (per [`release-strategy.md`](../release-strategy.md) §Sequencing Stage 2). Submodule reference + Compile Include lines drop in the same slice that wires the corresponding generated `Generated/*.g.cs` into the family csproj. |
| `src/Janset.SDL2.Bindings.Generator/` | Does not exist; was proposed in the 2026-05-14 draft | **Do not create.** Per the 2026-05-15 revision, binding generation is folded into the Cake build host (see `build/_build/Targets/GenerateBindings/` row below). A standalone `src/`-tree console app is rejected because it would duplicate Cake's vcpkg/manifest/tool/validation infrastructure. |
| `src/Janset.SDL3.Bindings.Generator/` | Does not exist | **Do not create at Phase 4.** SDL3 binding generation is gated on PD-7 (SDL2 real-public-release). At Phase 5 a sibling Cake target (`GenerateSdl3Bindings`) is introduced; SDL3-specific type-map rules live target-local until SDL3 becomes a real second consumer and ADR-002 §2.4 promotion criteria are met. |
| `src/SDL2.<Family>/Generated/` | Does not exist | **Add** per-family. Receives generated `Commands.g.cs` / `Constants.g.cs` / `Enums.g.cs` / `Handles.g.cs` / `Structs.g.cs` / `Callbacks.g.cs` from the neutral pass + `Platform/<OS>/*.g.cs` from per-OS passes + `.generated-stamp` from the Cake target. Committed to git per Generation environment lock. |
| `src/SDL2.<Family>/<Family>.csproj` | `<Compile Include="../../external/sdl2-cs/src/<Family>.cs" />` plus AOT / TFM / package metadata | **Update**: drop the sdl2-cs Compile Include; SDK glob picks up `Generated/**/*.cs` automatically (per [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §3 finding #6). Add `AllowUnsafeBlocks=true`. Cross-csproj reference from satellite to `Janset.SDL2.Core` retained. |
| `build/_build/Targets/GenerateBindings/` | Does not exist | **Add** new Cake target home per [`AGENTS.md`](../../AGENTS.md) §Build-Host Reference Pattern + ADR-002. Hosts both orchestration (`GenerateBindingsTask`, `BindingGenerationRunner`, `ServiceCollectionExtensions`) and the **pure emitter** (`HeaderSet/`, `Parsing/`, `Model/`, `Emitting/`, `Stamps/` subfolders — Cake-free, unit-testable). Drives vcpkg-state resolution, vcpkg install for canonical triplet (`x64-linux-hybrid`), one CppAst parse view per `PlatformCatalog` entry, output validation, `.generated-stamp` write. |
| `build/_build/Validation/` (vcpkg-state coherence) | Does not exist | **Add** validator joining existing PreFlight validators (`HybridStaticOverlayValidator`, manifest schema validators). Behavior-first name candidate: `BindingVcpkgCoherenceValidator`. Reads `.generated-stamp` per family + current vcpkg state; fails PreFlight with actionable error on drift. |
| `build/_build/Validation/` (symbol existence) | Does not exist | **Add** validator running at Pack stage (after Harvest, before Package). Behavior-first name candidate: `BindingSymbolExistenceValidator`. Cross-platform via existing `build/_build/Tools/` wrappers around `dumpbin /exports` / `nm -D --defined-only` / `nm -gU`. |
| `build/_build/Tools/` | Wraps vcpkg, dumpbin, ldd, otool, tar, cmake, native-smoke per [`AGENTS.md`](../../AGENTS.md) §Build-Host Reference Pattern | **No new wrappers needed**: existing dumpbin / nm / otool wrappers cover Stage 2 symbol-existence validation. The CppAst emitter is invoked directly from the in-host `BindingGenerationRunner`; no separate process boundary so no `Tool<TSettings>` wrapper is required. |
| `tools.cs` | `setup` / `ci-sim` / `build --target X` subcommands per [`AGENTS.md`](../../AGENTS.md) §Common Commands | **Add** `generate-bindings [--family X]` subcommand. Publishes the Cake host (Release), mounts repo root + vcpkg cache into `ghcr.io/janset2d/sdl2-bindings-linux-builder:focal-latest`, runs `dotnet ./.cake-host/Build.dll --target GenerateBindings --family X` inside the container. `.dockerignore` excludes `vcpkg_installed/`, `artifacts/`, `.vs/`, `bin/`, `obj/` so the container's vcpkg install does not collide with host state. |
| `Directory.Packages.props` | CPM pins for production deps; spike-side has `CppAst 0.24.0` + win-x64 runtime pins for `tools/binding-spike/cppast/` | **Add Linux-canonical production pins:** `CppAst 0.24.0` + `libclang.runtime.linux-x64 20.1.2` + `libClangSharp.runtime.linux-x64 20.1.2`. **Non-Linux runtime variants are intentionally absent** — Linux-canonical lock per "Toolchain commitment" above. Comment block explains version-trio coupling. |
| `.dockerignore` | Existing root-level `.dockerignore` (verify and extend) | **Update** to exclude `vcpkg_installed/`, `artifacts/`, `.vs/`, `bin/`, `obj/`, `.cake-host/` so `tools.cs generate-bindings`'s volume mount does not leak host state into the container. |
| `build/manifest.json` schema | `schema_version 2.1` with packaging config + runtimes + package families + system exclusions + library manifests | **No v2.2 bump required** if `.generated-stamp` per family is treated as binding-side state. Phase 4 plan re-evaluates whether a `binding_generator_project` field per `package_families[]` adds value or is redundant. |
| `vcpkg.json` | Cross-platform vcpkg dependency declaration | **Unchanged** by this brief. The vcpkg-state coherence guardrail reads `vcpkg.json` + `vcpkg-configuration.json` + `build/manifest.json` but does not modify them. |
| [`release.yml`](../../.github/workflows/release.yml) | 8-job pipeline: build-cake-host → resolve-versions → preflight → generate-matrix → harvest → consolidate-harvest → pack → consumer-smoke → publish-staging | **Update**: PreFlight job invokes the new `BindingVcpkgCoherenceValidator`; Pack job invokes the new `BindingSymbolExistenceValidator`. Both run as part of the existing Cake target invocations (no new jobs added). |
| `.github/workflows/regenerate-bindings.yml` | Does not exist | **Add** new workflow per "Generation pipeline" lock above. Manual trigger, family selector, runs in `linux-builder` container, opens PR via `peter-evans/create-pull-request@v7`. |
| [`vcpkg-setup`](../../.github/actions/vcpkg-setup) composite action | Bootstrap + cache + binarycaching | **Unchanged**. Reused by both `release.yml` (matrix) and `regenerate-bindings.yml` (single triplet `x64-linux-hybrid`). |
| [`nuget-cache`](../../.github/actions/nuget-cache) composite action | NuGet cache restore | **Unchanged**. Reused. |
| [`linux-builder.Dockerfile`](../../docker/linux-builder.Dockerfile) | Multi-arch ubuntu:20.04 base, vcpkg deps, GCC 11, cmake, ninja, autoconf 2.72 | **Unchanged**. Existing image is sufficient — .NET SDK + libclang runtime come from setup-dotnet + CppAst NuGet transitive deps. |
| [`release-guardrails.md`](../knowledge-base/release-guardrails.md) | G14 / G15 / G16 / G19 / G21–G23 / G46–G58 catalog | **Add** two G-numbered rows at Phase 6 catalog refresh: vcpkg-state coherence (PreFlight) + symbol-existence (Pack). Both join the binding ↔ native coherence guardrail family. |
| [`AGENTS.md`](../../AGENTS.md) §Settled Strategic Decisions | "Binding autogen replaces SDL2-CS" row + "`external/sdl2-cs` is transitional" row | **Update** at Phase 6 promotion: collapse both rows into one citing the new ADR-binding-autogen + this brief; mark the transition as complete. |
| [`AGENTS.md`](../../AGENTS.md) §Common Commands | `dotnet run --file tools.cs -- setup / ci-sim / build` enumerated | **Add** `tools.cs generate-bindings` row when implementation lands. |
| [`AGENTS.md`](../../AGENTS.md) §Configuration File Relationships | Diagram showing vcpkg.json ↔ manifest.json ↔ PreFlight validators | **Update** to include `.generated-stamp` as a binding-side state file validated by PreFlight. |
| [`onboarding.md`](../onboarding.md) | Project framing + glossary + non-goals | **Update** at Phase 6 — Phase 4 status from PLANNED to COMPLETED; glossary adds `.generated-stamp`, `BindingVcpkgCoherenceValidator`, generation pipeline terminology. |
| [`release-strategy.md`](../release-strategy.md) §Maintenance Commitment | "AST regeneration on upstream bumps. When SDL2.32.x → 2.33.0 lands ... regenerate AST output + release wave." | **Update** to reference `regenerate-bindings.yml` explicitly as the regen mechanism. |
| [`testing-guidelines.md`](../knowledge-base/testing-guidelines.md) | Canonical TUnit infra, `FakeCakeWorld`, `TargetTestHost`, `FixtureLoader`, fixture data policy | **Add** section on public API snapshot testing via Verify (Layer 2 of the 7-layer strategy). New tests use existing TUnit infrastructure; no new test scaffolding needed. |
| [`local-development.md`](../playbook/local-development.md) | `tools.cs setup` workflow + feed bootstrap + troubleshooting | **Add** "Regenerating bindings" section: prerequisites (Docker + WSL2 on Windows), command (`tools.cs generate-bindings --family X`), output inspection, commit workflow. |
| [`phase-4-binding-autogen.md`](../phases/phase-4-binding-autogen.md) | Phase 4 design brief, status PLANNED | **Supersede** by this strategy brief + the companion ADR. Phase-4 brief either retires or shrinks to a one-line pointer at Phase 6. |
| `docs/decisions/2026-05-14-binding-autogen-toolchain.md` | Does not exist | **Add** as companion ADR recording the toolchain decision (CppAst lock + version-trio pin + scope-trajectory rationale + migration door). Permanent canonical record per the locked "Strategy brief + companion ADR" doc shape. |

### Test strategy

Maps the 7-layer strategy from [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §7 to canonical project test infrastructure per [`testing-guidelines.md`](../knowledge-base/testing-guidelines.md). No new test scaffolding required at the brief level; Phase 4 plan owns specific test additions per slice.

| Layer | Coverage | Infrastructure | New work |
| --- | --- | --- | --- |
| 1 — Compile-time gate | Structural correctness — every generated `.cs` file compiles under all target TFMs | Existing `dotnet build` in CI (`release.yml` + per-PR build) | None. SDK glob picks up `Generated/**/*.cs`; existing TFM matrix per [`AGENTS.md`](../../AGENTS.md) §Build Host Pipeline covers it. |
| 2 — Public API surface snapshot | Catches accidental API breakage, unintended signature changes, deleted/added public members | TUnit + [Verify](https://github.com/VerifyTests/Verify) + `PublicApiGenerator` (`dotnet-skills:snapshot-testing` skill applies) | New characterization test in `build/_build.Tests` (or a dedicated `tests/Janset.SDL2.Bindings.SurfaceTests/` per family). Verify snapshots live under the test project's `Snapshots/`. |
| 3 — Reference cross-check diff | Catches type-mapping bugs by diffing generator output against `external/sdl2-cs` (SDL2) / `ppy/SDL3-CS` (SDL3) ground-truth signatures | New `build/_build` tool — exact location is Phase 4 plan decision (D3 in feasibility §9 open decisions) | New tool. ~3–5h focused work per feasibility §7 Layer 3 estimate. Output: categorized markdown report under `artifacts/binding-audit/<date>/`. |
| 4 — Symbol-existence validation | Every emitted `[LibraryImport(EntryPoint = "Foo")]` corresponds to an exported symbol in the satellite's native binary | `BindingSymbolExistenceValidator` at Pack stage; existing `build/_build/Tools/` wrappers around `dumpbin` / `nm` / `otool` | New validator. ~4–6h focused work per feasibility §7 Layer 4 estimate. Catches PD-15 failure-mode class. |
| 5 — Reproducibility check | Regenerate from same headers + same generator version → byte-identical output | New CI step in `regenerate-bindings.yml`: regenerate, then `git diff --exit-code src/**/Generated/` | New step. ~2h focused work. Catches non-deterministic emit ordering + stale committed output. |
| 6 — Runtime smoke (consumer-side) | Loading + minimal P/Invoke calls per satellite per RID per TFM | Existing `PackageConsumerSmoke` per [`AGENTS.md`](../../AGENTS.md) §Build Host Pipeline step 4 | Extend existing per-family smoke to exercise representative AST-generated APIs (`SDL_Init`/`SDL_Quit`/`SDL_CreateWindow`/`SDL_DestroyWindow` core lifecycle; per-satellite minimal call). |
| 7 — Semantic / behavior testing | Compiled-correct ≠ semantically-correct — struct field offsets, callback marshalling, threading model | `learning-sdl2` external consumer + occasional manual play-test passes | None new in build-host scope. Cadence at maintainer discretion per [`release-strategy.md`](../release-strategy.md) §Promotion Gates. |

**Test layer ownership across stages:**

- Layers 1, 5, 6 land at Stage 1 (SDL2.Core proof-of-life — CI-time gates + drift check + smoke).
- Layer 2 lands at Stage 1 (snapshot the SDL2.Core public surface before satellite work begins, so satellite-stage diffs are review-able).
- Layers 3, 4 land at Stage 2 (SDL2 satellite sweep — when more than one family exists to diff and validate).
- Layer 7 is continuous from Stage 1 forward, picking up cadence at Stage 4 (Stabilization per [`release-strategy.md`](../release-strategy.md)).

Total upfront tooling cost ~15–25h focused per feasibility §7 estimate.

### Open work items decoupled from acceptance

These are real work items but their resolution does not block this brief's acceptance. They surface in the Phase 4 plan or later slices.

- **Multi-TFM minimum window for SDL3.** Full matrix is locked for SDL2 (legacy Unity / Xamarin consumer reach); SDL3 may legitimately drop `netstandard2.0` / `net462` since SDL3 itself is a 2024+ library with no legacy consumer base. Phase 5 plan re-evaluates.
- **Friendly-overload feature set scope.** Locked: `string` / `ReadOnlySpan<byte>` / `out` / `ref` / `Span<T>`. Open: whether to emit additional overloads (e.g., `Memory<T>` extension methods, ergonomic enum-flag combinators) — Phase 4 plan decides per-rule.
- **`delegate*` callback lifetime helpers.** Rule 7 commits to `[UnmanagedCallersOnly]` static-method pattern. Edge case: do we ship rooting helpers for instance-bound closures? Feasibility §10.2 captured this as a discussion thread; Phase 4 plan resolves.
- **Wrapper layer (`SdlWindow : IDisposable` on top of `SDL_Window` struct).** Q7 deferred past v1.0 per [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §9. Revisit if real consumer feedback signals RAII need that doesn't compose with the typed-struct baseline.

## Plan Shape

This is **not** the implementation plan. It is the stage outline the Phase 4 plan authors against, aligned with [`release-strategy.md`](../release-strategy.md) §Sequencing and the per-phase plan-writing discipline from [`phase-planning-methodology.md`](../parking-lot/package-topology/phase-planning-methodology.md). The implementation plan should write concrete slices only for the next stage being executed, then re-plan the following stage against the code that actually shipped. No speculative "Stage 3 line-by-line instructions" before Stage 1 exists in the repo.

### Stage 1 — SDL2.Core proof-of-life with full platform-function attribution

**Goal:** turn the spike evidence into a production-shaped SDL2.Core generator path, hosted inside the Cake build host, covering every SDL2 platform-conditioned function — but stopping short of `SDL_syswm.h` struct/union layout, which moves to Stage 2.

**Scope:**

- Add the CppAst-based generator under `build/_build/Targets/GenerateBindings/` and `build/_build/Validation/BindingGeneration/` per the HOW section. No standalone `src/`-tree project.
- Generate SDL2.Core from vcpkg-installed headers through neutral + the full `PlatformCatalog` entry set (Windows desktop / WinRT / GDK / Linux / macOS / iOS / Android — ~7–8 passes) inside the pinned Linux container. Preprocessor-macro switching only — no `--target`, no mingw, no Apple SDK.
- Emit committed `src/SDL2.Core/Generated/*.g.cs` for neutral surface + `Generated/Platform/<OS>/*.g.cs` for platform-conditioned functions + `.generated-stamp`. `[SupportedOSPlatform]` attribution drives off catalog entries.
- Emit `SDL_GetWindowWMInfo` as a function with an opaque `nint`-shaped `SDL_SysWMinfo*` parameter. The typed union layout is deliberately excluded and recorded as a documented Stage 2 deliverable in the audit log.
- Wire SDL2.Core to generated output and remove its production dependency on `external/sdl2-cs/src/SDL2.cs`.
- Land Layer 1 compile, Layer 2 public API snapshot, Layer 5 reproducibility, and Layer 6 core runtime smoke coverage from the test strategy.

**Exit criteria:**

- SDL2.Core generated source compiles for `net10` / `net9` / `net8` / `netstandard2.0` / `net462`.
- Regenerating from the same vcpkg state produces a clean `git diff --exit-code src/SDL2.Core/Generated` — proves Layer 5 reproducibility.
- Every catalog entry's platform-only core functions are isolated into the matching `Platform/<OS>/*.g.cs` file and carry `[SupportedOSPlatform]` attribution.
- `SDL_GetWindowWMInfo` is emitted; `SDL_SysWMinfo` and `SDL_SysWMmsg` typed-union surface is recorded as a Stage 2 deliverable in the audit log; no consumer-visible breakage from the deferral because Stage 1 is internal-feed only.
- `.generated-stamp` records generator + vcpkg + header-set state for SDL2.Core, and the PreFlight drift check is active for that family.
- The `GenerateBindings` Cake target refuses to run on a non-`linux-x64` host with an actionable diagnostic message.
- `tools.cs generate-bindings` runs the full pipeline locally on a Windows or macOS dev machine via Docker and produces byte-identical output to CI.
- Package-consumer smoke exercises `SDL_Init`, window creation/destruction, error retrieval, and at least one callback path against the packaged Core family.
- `learning-sdl2` or an equivalent real consumer can run against the internal-feed Core wave without falling back to source/project references.

### Stage 2 — SDL_syswm full union + satellite sweep + `external/sdl2-cs` retirement

**Goal:** complete the SDL2 surface — typed `SDL_SysWMinfo`/`SDL_SysWMmsg` union layout for every platform branch — and extend the production generator across every in-scope SDL2 satellite, then retire `external/sdl2-cs` from the production binding surface.

**Scope:**

- Add a small forward-declaration stub library (~15–20 types: `HWND`, `HDC`, `HINSTANCE`, `IInspectable`, `Display*`, `Window`, `wl_display`/`wl_surface`/`xdg_*`, `gbm_device`, `IDirectFB*`, `EGLNativeDisplayType`, `ANativeWindow`, etc.) to anchor the 64-byte `SDL_SysWMinfo.info` union layout across catalog branches. Stubs are pure forward declarations, not Apple SDK or Windows SDK derivatives; Apple's non-`__OBJC__` `typedef struct _NSWindow NSWindow;` comes directly from `SDL_syswm.h:86` and similar.
- Emit typed `SDL_SysWMinfo` and `SDL_SysWMmsg` with `[StructLayout(LayoutKind.Explicit, Size = 64)]` on the `info` union, every platform branch at `[FieldOffset(0)]`, dummy padding emitted as documented in `SDL_syswm.h:346-348`.
- Generate SDL2.Image, SDL2.Mixer, SDL2.Ttf, SDL2.Gfx, and SDL2.Net with satellite-owned APIs only.
- Reuse the SDL2.Core-owned managed type universe for shared `SDL_*` structs, handles, enums, callbacks, and constants.
- Add duplicate-core-type validation so satellites fail generation if they redeclare core-owned symbols or degrade them to untyped fallbacks.
- Land Layer 3 reference cross-check reports and Layer 4 symbol-existence validation once more than one generated family exists.
- Extend PackageConsumerSmoke per family so targeted package scopes can exercise only the families in scope.
- Remove `external/sdl2-cs` from production compile paths. If it remains temporarily, it is a test/reference oracle only, not a shipping source.

**Exit criteria:**

- `SDL_SysWMinfo` and `SDL_SysWMmsg` layout validates against hand-checked offsets for every platform branch the catalog covers; the 64-byte size lock holds.
- Every SDL2 managed family builds from `Generated/**/*.g.cs`; no production `.csproj` includes files from `external/sdl2-cs/src/`.
- Satellite signatures reuse core-owned managed types and do not duplicate `SDL_Surface`, `SDL_Texture`, `SDL_Renderer`, `SDL_RWops`, `SDL_version`, `SDL_bool`, or equivalent core symbols.
- Reference cross-check output is reviewed and categorizes differences as expected typed-handle deltas, known quirks, or real generator defects.
- Symbol-existence validation runs before packaging and fails on declared-but-unexported functions across Windows, Linux, and macOS.
- Full 7-RID native smoke + package consumer smoke passes for all SDL2 families from packages.
- Stage 2 closes the AST-first requirement for the first public SDL2 `-preview.N` wave: generated bindings, package-first consumption, no sdl2-cs public API churn trap.

### Stage 3 — SDL3 extension (gated on PD-7)

**Goal:** reuse the generator architecture for SDL3 Core + Image + Mixer + Ttf, with SDL3-specific ABI differences treated as first-class rules rather than SDL2 afterthoughts.

**Gating:** Stage 3 does **not** begin until PD-7 (SDL2 real-public-release) is shipped. The SDL3 vcpkg port surface (overlay triplets, port patches, transitive dependency closure, hybrid-static validator updates) is its own substantial scope and must not block SDL2's path to stable. The strategic anchor in [`release-strategy.md`](../release-strategy.md) §Sequencing remains: SDL2 v1.0 stable ships first, SDL3 follows.

**Scope (when Stage 3 activates):**

- Add a sibling `GenerateSdl3Bindings` Cake target under `build/_build/Targets/GenerateSdl3Bindings/`, mirroring the SDL2 layout. Shared code, if any, gets promoted out of the SDL2 target per ADR-002 §2.4 promotion criteria — not before SDL3 becomes a real second consumer.
- Encode SDL3-specific type rules: 1-byte bool-like values, `SDL_IOStream` replacing `SDL_RWops`, SDL3 namespace/library naming, and SDL3 satellite headers.
- Re-evaluate the legacy TFM window for SDL3 before copying SDL2's `netstandard2.0` / `net462` obligations.
- Reuse the regeneration workflow, vcpkg-state stamp, reproducibility gate, reference cross-check, and symbol-existence validation.

**Exit criteria:**

- SDL3 Core + Image + Mixer + Ttf generated output compiles and packages through the internal feed.
- SDL3 package-consumer smoke proves load + minimal P/Invoke calls per generated family across the supported RID/TFM matrix.
- SDL3-specific ABI choices are captured in the companion ADR or a Phase 5 ADR addendum before any public SDL3 preview.
- SDL3_net and SDL3_shadercross remain explicitly out of scope unless upstream/vcpkg availability changes.

### Plan authoring cadence

Write the **Stage 1 implementation plan first**. It should reference this brief, [`AGENTS.md`](../../AGENTS.md), ADR-001, ADR-002, ADR-003, [`release-guardrails.md`](../knowledge-base/release-guardrails.md), [`testing-guidelines.md`](../knowledge-base/testing-guidelines.md), and [`extraction-guidelines.md`](../knowledge-base/extraction-guidelines.md). After Stage 1 ships and commits, write the Stage 2 plan against the actual Stage 1 artifacts. After Stage 2 ships, write the SDL3 extension plan. This mirrors the topology-refactor methodology: strategy brief stays stable, phase plans stay concrete, and implementation agents do not invent symbols that the repo does not yet contain.

## Current Open Decisions

This brief locks the strategy-level direction. It intentionally leaves lower-level choices to the Stage 1 / Stage 2 plans, where the real code shape is visible.

| Decision | Recommended default | Owner / timing |
| --- | --- | --- |
| SDL3 TFM minimum window | Mirror SDL2 only if the compatibility value is real. SDL2 keeps `net10` / `net9` / `net8` / `netstandard2.0` / `net462`; SDL3 may drop legacy TFMs if Phase 5 concludes SDL3 consumers are modern-only. | Phase 5 / Stage 3 plan |
| Friendly-overload first slice | Start with the locked baseline only: `string`, `ReadOnlySpan<byte>`, `Span<T>`, `out`, and `ref`. Do not add `Memory<T>` or wrapper-layer conveniences in Stage 1. | Stage 1 plan |
| Reference cross-check tool shape | Prefer a generator/audit tool invoked by `GenerateBindings` that emits categorized markdown under `artifacts/binding-audit/<date>/`. Promote to a separate Cake target only if a second real consumer appears. | Stage 2 plan |
| Symbol-existence validator details | Implement as a Pack-stage validator under `build/_build/Validation/`, using behavior-first naming such as `BindingSymbolExistenceValidator`. Error messages name family, RID, native binary, entry point, and generated source location. | Stage 2 plan |
| Generator host directory | `build/_build/Targets/GenerateBindings/` for the SDL2 production generator (Cake-host fold per 2026-05-15 revision). At Stage 3, a sibling `build/_build/Targets/GenerateSdl3Bindings/` target. No `src/Janset.SDL2.Bindings.Generator/` standalone project; no `tools/`-hidden generator code. | Stage 1 plan |
| `SDL_syswm.h` platform-layout policy | Stage 1: emit `SDL_GetWindowWMInfo` as a function with opaque `SDL_SysWMinfo*` parameter; log typed-union deferral in the audit log. Stage 2: introduce the ~15–20-type forward-declaration stub library, emit typed `SDL_SysWMinfo` / `SDL_SysWMmsg` with `[StructLayout(LayoutKind.Explicit, Size = 64)]` and platform branches at `[FieldOffset(0)]`. | Stage 2 plan |
| SDL3 binding generation start | Gated on PD-7 completion. SDL3 vcpkg port + overlay triplet work is its own scope and must not block SDL2 v1.0 stable. Stage 3 plan begins after Stage 2 ships and PD-7 lands. | Stage 3 plan / post-PD-7 |
| Variadic function convention | Preserve raw fidelity where the C# compiler/runtime can represent it, but ship fmt-only friendly wrappers for common logging calls. Do not pretend `LibraryImport` fully handles variadic APIs until Stage 1 proves the exact shape. | Stage 1 plan |
| `.generated-stamp` schema | Keep the principle locked: generator version + CppAst/libclang version + vcpkg state + header-set hash. Exact JSON fields and recomputation mechanics are a Stage 1 deliverable. | Stage 1 plan |
| Wrapper layer (`SdlWindow : IDisposable`, etc.) | Defer past v1.0 unless real consumer feedback says raw + friendly overloads are not enough. This is a higher-level API design project, not binding generation. | Post-v1.0 / explicit reopen |
| D11 PowerShell vs Python orchestrator | Under the CppAst decision, this mostly retires. Prefer C# entry points (`tools.cs` → Cake → generator) over PowerShell/Python orchestration. Use shell only for local Docker invocation mechanics if unavoidable. | Stage 1 plan |

Two scaffold items collapse into the rows above: "D8 reference cross-check tool implementation" was a wording drift from the feasibility doc; the relevant decision is D3 / §10.4. "PowerShell vs Python" was a ClangSharp-orchestrator question; the CppAst path makes it non-load-bearing.

## Decision Audit

This section records the 2026-05-14 fact-check so future readers know which earlier claims were corrected and which claims still carry outstanding verification work. The important meta-rule: **the CppAst decision does not depend on any retracted ClangSharp weakness.** It depends on the scope-trajectory argument in WHY.

| Finding | Correction / current state | Why it matters |
| --- | --- | --- |
| **Error 1 — macro-capture parity** | Earlier drafts said ClangSharp captured only 2 SDL2_gfx constants while CppAst captured 8. Current spike artifacts show ClangSharp with `--config generate-macro-bindings` captures the same 8 constants as CppAst. Corrected in [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §2, §6.G, §7.4, §7.5, §7.7, and §9 Q6; mirrored in [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) §9 D6 and [`binding-autogen-onboarding.md`](research/binding-autogen-onboarding.md). | CppAst's case cannot claim a macro-capture advantage at SDL2_gfx scope. The toolchain decision rests on production-shape custom emission, not small-scope constants. |
| **Error 2 — ppy source-generator API** | ppy's `FriendlyOverloadGenerator` implements `ISourceGenerator`, not `IIncrementalGenerator`. Corrected in [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) §5.5. | If the project migrates to ClangSharp + ppy-style friendly overloads, modernizing that generator is real work, not a free copy-paste. |
| **Error 3 — original decision-matrix arithmetic** | The 2026-04-11 matrix summed to CppAst 23 vs ClangSharp 23, not 23 vs 22. Corrected in [`binding-autogen-approaches.md`](research/binding-autogen-approaches.md). The later weighted matrix remains CppAst 43 vs ClangSharp 63, favoring ClangSharp on raw-binding economics. | The brief can acknowledge ClangSharp's raw-binding strength honestly while still choosing CppAst for the broader output scope. No thumb-on-scale math. |
| **Cosmetic drift refresh** | Current spike snapshots: ClangSharp RSP 43 lines, CppAst `Program.cs` 261 lines, `NativeTypeNameAttribute.cs` 25 lines, `Constants.cs` 11 lines. | Keeps line-count comparisons traceable. These are snapshot signals, not eternal truths. |
| **Verified local/live claims** | Verified: `external/sdl2-cs` totals 11,105 lines; SDL2 public header set count is 88 in the checked vcpkg installs; CppAst 0.24.0 + libclang/libClangSharp 20.1.2 trio is the spike pin; Alimer.Bindings.SDL is SDL3 core only; ppy uses ClangSharpPInvokeGenerator 17.0.1 + Dockerfile + `generate_bindings.py` + `FriendlyOverloadGenerator.cs`; Silk.NET 3.0 proposal explicitly delegates parsing to ClangSharp. | These claims are used as support throughout WHY/HOW/WHAT. Future edits should re-check them before updating recommendations. |
| **Outstanding verification items** | Still lower-priority: release-by-release confirmation that CppAst has been additive-only since 0.21.1; exact current SkiaSharpGenerator size and three-way emission details. | Neither blocks this brief. Both are useful if a later ADR wants deeper external precedent evidence. |
| **Error 4 — toolchain stub strategy speculation (corrected 2026-05-15)** | The 2026-05-14 draft claimed Stage 1 would use "MinGW headers or stubs from the container's apt layer" for the Windows pass and "minimal Apple SDK header stubs" for the macOS pass. Both were speculation, not evidence-grounded. WebFetch of ppy/SDL3-CS (Dockerfile + `generate_bindings.py`) on 2026-05-15 confirmed ppy uses **preprocessor-macro switching only** — no mingw-w64, no Apple SDK, one tiny `include/process.h` shim (81 bytes). Local re-read of our CppAst spike at `tools/binding-spike/cppast/generator/Program.cs:42-72` confirmed the same pattern — `Defines`/`Undefines` macro juggling, no `--target`, no platform-specific header stubs (the spike's 10 stubs are pure C-stdlib shims for cross-host parsing and disappear inside the Linux container). Per-OS macro groups in [`SDL_syswm.h`](../../vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_syswm.h) (lines 86, 94-95, 105-106, 109-110, 120) carry their own forward declarations of `NSWindow`, `UIWindow`, `ANativeWindow`, `gbm_device`, etc., so function-level platform surface parses with zero hand-written stubs. | The strategy section "Multi-pass parsing strategy" has been rewritten to reflect the preprocessor-macro-only approach. Stage 1 scope expanded to cover the full 7–8 entry `PlatformCatalog` at function level; `SDL_syswm.h` typed-union layout was the only piece that legitimately needs forward-declaration stubs (~15–20 types), and that work is now scoped to Stage 2. |
| **Reframe — generator hosted in Cake build host (2026-05-15)** | The 2026-05-14 draft proposed a standalone `src/Janset.SDL2.Bindings.Generator/` console app invoked from the Cake target via `Tool<TSettings>` wrapper. Per [`AGENTS.md`](../../AGENTS.md) §Build-Host Reference Pattern + ADR-002 + ADR-003 review, binding generation is unambiguously a CI/CD concern and the Cake build host already owns equivalent infrastructure (vcpkg state, manifest parsing, validation, tool wrappers, logging, paths, runtime profile). A parallel `src/`-tree orchestration stack would duplicate that surface and break ADR-002 §2.1's target-centric navigation rule. | The brief's HOW section was rewritten to fold the generator into `build/_build/Targets/GenerateBindings/` with subfolders for `HeaderSet/`, `Parsing/`, `Model/`, `Emitting/`, `Stamps/`. The pure-emitter rule ("Pure code stays pure" per AGENTS.md) is preserved by keeping those subfolders Cake-free and unit-testable from `build/_build.Tests/`. |

The audit also explains why sibling docs can look directionally different: [`binding-autogen-approaches.md`](research/binding-autogen-approaches.md) and [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) preserve the raw-binding research recommendation that favored ClangSharp; this brief makes the later project decision after locking production-shape features that change the cost curve.

## Cross-Reference

### Internal project anchors

- [`../../AGENTS.md`](../../AGENTS.md) — operating rules, approval gate, settled strategic decisions, build-host reference pattern.
- [`../onboarding.md`](../onboarding.md) — project framing, package topology, glossary, non-goals.
- [`../plan.md`](../plan.md) — tactical roadmap; Phase 4 binding autogen is critical path before first public preview.
- [`../release-strategy.md`](../release-strategy.md) — v1.0 end state, AST-first sequencing, promotion gates, maintenance commitment.
- [`../phases/phase-4-binding-autogen.md`](../phases/phase-4-binding-autogen.md) — Phase 4 brief; this strategy brief supersedes it once accepted.
- [`../phases/phase-5-sdl3-support.md`](../phases/phase-5-sdl3-support.md) — SDL3 monorepo + binding extension context.

### Binding-autogen workstream docs

- [`binding-autogen-onboarding.md`](research/binding-autogen-onboarding.md) — LLM/human workstream entry point and required reading order.
- [`binding-autogen-approaches.md`](research/binding-autogen-approaches.md) — tool survey, decision matrix, source-level comparison of ppy/SDL3-CS and Alimer.Bindings.SDL.
- [`binding-autogen-feasibility.md`](research/binding-autogen-feasibility.md) — 11 emit rules, header/platform feasibility, symbol visibility, 7-layer testing strategy, D1-D11 open decisions.
- [`binding-autogen-spike-findings.md`](research/binding-autogen-spike-findings.md) — SDL2_gfx ClangSharp + CppAst spike, runtime validation, platform-pass and SDL2_image shared-type follow-ups.

### ADRs, guardrails, and planning conventions

- [`../decisions/2026-05-05-d3seg-and-package-first.md`](../decisions/2026-05-05-d3seg-and-package-first.md) — ADR-001 D-3seg versioning + package-first consumer contract.
- [`../decisions/2026-05-05-target-centric-build-host.md`](../decisions/2026-05-05-target-centric-build-host.md) — ADR-002 target-centric build-host architecture.
- [`../decisions/2026-05-12-build-host-data-layer.md`](../decisions/2026-05-12-build-host-data-layer.md) — ADR-003 contract-centric data layer.
- [`../decisions/2026-05-14-binding-autogen-toolchain.md`](../decisions/2026-05-14-binding-autogen-toolchain.md) — ADR-004 CppAst binding-generator toolchain decision.
- [`../knowledge-base/release-guardrails.md`](../knowledge-base/release-guardrails.md) — guardrail catalog; binding vcpkg coherence and symbol existence get final G-IDs here later.
- [`../knowledge-base/testing-guidelines.md`](../knowledge-base/testing-guidelines.md) — canonical TUnit/MTP test infrastructure and fixture policy.
- [`../knowledge-base/extraction-guidelines.md`](../knowledge-base/extraction-guidelines.md) — collaborator extraction and interface discipline.
- [`../parking-lot/package-topology/phase-planning-methodology.md`](../parking-lot/package-topology/phase-planning-methodology.md) — plan-authoring cadence this brief reuses.
- [`../research/symbol-visibility-analysis.md`](../research/symbol-visibility-analysis.md) — Linux/macOS export visibility background for symbol-existence validation.
- [`../playbook/local-development.md`](../playbook/local-development.md) — future home for local `tools.cs generate-bindings` operator guidance.

### External references

- [CppAst NuGet](https://www.nuget.org/packages/CppAst), [CppAst GitHub releases](https://github.com/xoofx/CppAst/releases), and [CppAst source](https://github.com/xoofx/CppAst) — selected parser/emitter foundation.
- [Alimer.Bindings.SDL](https://github.com/amerkoleci/Alimer.Bindings.SDL) and [Generated Handles.cs](https://raw.githubusercontent.com/amerkoleci/Alimer.Bindings.SDL/main/src/Alimer.Bindings.SDL/Generated/Handles.cs) — CppAst SDL3-core reference and typed-handle output pattern.
- [Vortice.Vulkan Handles.cs](https://raw.githubusercontent.com/amerkoleci/Vortice.Vulkan/main/src/Vortice.Vulkan/Generated/Handles.cs) — second amerkoleci typed-handle precedent.
- [ppy/SDL3-CS](https://github.com/ppy/SDL3-CS), [Dockerfile](https://github.com/ppy/SDL3-CS/blob/master/Dockerfile), [`generate_bindings.py`](https://raw.githubusercontent.com/ppy/SDL3-CS/master/SDL3-CS/generate_bindings.py), [`FriendlyOverloadGenerator.cs`](https://github.com/ppy/SDL3-CS/blob/master/SDL3-CS.SourceGeneration/FriendlyOverloadGenerator.cs), and [dotnet-tools.json](https://github.com/ppy/SDL3-CS/blob/master/.config/dotnet-tools.json) — closest ClangSharp SDL reference and migration target.
- [Silk.NET 3.0 generation proposal](https://github.com/dotnet/Silk.NET/blob/main/documentation/proposals/Proposal%20-%20Generation%20of%20Library%20Sources%20and%20PInvoke%20Mechanisms.md), [bindings-regeneration workflow](https://github.com/dotnet/Silk.NET/blob/main/.github/workflows/bindings-regeneration.yml), and [runner setup](https://github.com/dotnet/Silk.NET/blob/main/documentation/for-contributors/runner-setup.md) — heavyweight ClangSharp pipeline and auto-PR precedent.
- [Microsoft Learn — Native interop best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices), [P/Invoke source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation), [custom marshalling source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/custom-marshalling-source-generation), [Native AOT interop](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/interop), and [SYSLIB1054 analyzer reference](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib1050-1069) — modern .NET interop rule base.
- [dotnet/runtime LibraryImportGenerator compatibility notes](https://github.com/dotnet/runtime/blob/main/docs/design/libraries/LibraryImportGenerator/Compatibility.md) — `DllImport` to `LibraryImport` compatibility constraints.
- [peter-evans/create-pull-request](https://github.com/peter-evans/create-pull-request) — regeneration workflow auto-PR mechanism.
- [bottlenoselabs/SDL3-cs](https://github.com/bottlenoselabs/SDL3-cs) — escalation reference for true multi-OS extraction + merge if controlled single-host parse views prove insufficient.
