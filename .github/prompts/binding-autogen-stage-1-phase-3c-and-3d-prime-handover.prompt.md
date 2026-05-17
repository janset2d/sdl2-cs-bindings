---
name: "Binding autogen Stage 1 — Phase 3C completion + Phase 3D-prime compile validation handover"
description: "Handover prompt for the next agent entering janset2d/sdl2-cs-bindings on branch spike/binding-autogen-sdl2-gfx after the 2026-05-17 Phase 3A → 3B → 3C arc + a mid-3C trust-erosion turn that surfaced (a) a latent Phase 3A `unsafe` modifier defect, (b) a recurring discipline-defect pattern documented in docs/superpowers/agent-discipline.md, and (c) a maintainer-proposed Phase 3D-prime compile-validation slice that should land before Phase 3D translator structural classification."
argument-hint: "Default flow: read agent-discipline.md → read this doc → confirm baseline (build/test/smoke) → present Phase 3C fold-or-split commit choice + Phase 3D-prime scope to Deniz with peer-evidenced analyzer research → wait for go. Override: any alternative slice (Stage 1 Task 7 csproj flag-flip / Stage 2 satellite enable / Phase 3D translator rewrite) by explicit Deniz instruction."
agent: "agent"
model: "Claude Opus 4.7 (1M context) or fresh model — discipline doc + this prompt must be loaded into context before any action"
---

# Binding Autogen Stage 1 — Phase 3C Completion + Phase 3D-prime Compile Validation Handover

> **Update (2026-05-17 later session):** The class-level `unsafe` defect described below was fixed with a TDD emitter test, `tools.cs generate-bindings` regenerated the preview output, and `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj` was added as an isolated diagnostic project. `CS0214` unsafe failures are gone. Remaining compile-check buckets are true generator/model gaps: duplicate unnamed fallback parameters, missing SDL-owned structural types (`SDL_GameControllerButtonBind`, `SDL_GUID`), explicit `va_list` / C runtime types, Vulkan/GDK external native types, and SDL2 `SDL_bool` ABI mapping. Canonical follow-up decisions now live in [`docs/binding-autogen/binding-api-surface-strategy.md`](../../docs/binding-autogen/binding-api-surface-strategy.md) and [`docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md`](../../docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md) §"2026-05-17 stabilization and API-surface correction". Treat the older "What's broken now" section as historical context, not current live state.

You are entering `janset2d/sdl2-cs-bindings` on branch `spike/binding-autogen-sdl2-gfx` after a 2026-05-17 session that:

1. Landed **Phase 3A** (Preview* → real names rename, P3.10 "unsafe modifier drop", committed as `797f1dd`).
2. Landed **Phase 3B** (BindingModel extension with 5 new declaration categories + BindingTypeRef + RequiredConstants config field + Option A for SDL.h-only constants, committed as `5bd563e`).
3. **Implemented Phase 3C** (TypeMappingPolicy + CoreOwnedTypeMap + KnownUnsupportedDeclarationPolicy extraction + tests + spec/plan housekeeping) — **dirty, not committed**, with a known defect carried forward from Phase 3A (see "What's broken now" below).
4. Surfaced a **recurring discipline-defect pattern** (research-before-decide gap, output-shape-validation gap, scope-creep without approval, evidence-free "succeeded" claims) — distilled into `docs/superpowers/agent-discipline.md` as a permanent reference.
5. Maintainer ended the session with: **"giderek güvenim azalmaya başladı bu proje kim bilir daha neleri yanlış yapıyoruz"** — a real trust drop. The proposed remediation is **Phase 3D-prime: compile validation slice**, to be implemented before Phase 3D translator rewrite, so the compile gate catches future emit-shape defects immediately.

This is not a normal handover. Read the discipline doc first. The maintainer's confidence is recoverable but only through visible behavior change in the next slice.

## First Principle

> Read `docs/superpowers/agent-discipline.md` BEFORE doing anything else in this repo. Then read this prompt. Then run `git status --short`, `git diff --stat`, `git log --oneline -8`, and inspect the dirty surface against this prompt's claims. Where this prompt and the live state disagree, the live state wins — but flag the divergence explicitly to Deniz before acting on either interpretation.

## Mandatory grounding (read in this order)

1. `CLAUDE.md` (relay) → `AGENTS.md` (the real contract — approval gate is the hard rule)
2. **`docs/superpowers/agent-discipline.md`** — the four disciplines, the anti-patterns, the concrete failures from 2026-05-17 session that prompted this doc. **Non-negotiable.**
3. `docs/onboarding.md`, `docs/plan.md`
4. `docs/decisions/2026-05-05-target-centric-build-host.md` (ADR-002)
5. `docs/decisions/2026-05-12-build-host-data-layer.md` (ADR-003)
6. `docs/decisions/2026-05-14-binding-autogen-toolchain.md` (ADR-004 trio pin)
7. `docs/knowledge-base/extraction-guidelines.md`
8. `docs/knowledge-base/testing-guidelines.md`
9. `docs/knowledge-base/release-guardrails.md`
10. **`docs/superpowers/specs/2026-05-16-binding-generator-unified-design.md`** — current unified design. §8.1 (identity vs category split + peer evidence) + §8.2 (peer table) are the most recent Phase 3C-aligned additions.
11. **`docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md`** — current unified plan. Phase 3C design-intent block + Phase 3B Finding-note "Decided 2026-05-17: Option A" housekeeping landed during this session.
12. `docs/binding-autogen/binding-autogen-strategy-brief.md`
13. `docs/playbook/binding-generator-maintenance.md` — `required_functions` + `required_constants` rationale section landed in Phase 3B (`5bd563e`).
14. Previous handover prompts in `.github/prompts/` for historical context.

## Non-negotiable rules (AGENTS.md + accumulated feedback + 2026-05-17 discipline lessons)

- **Approval gate (AGENTS.md):** no commit, no manifest edit, no production code refactor, no new dependency, no architectural pivot without explicit Deniz approval ("yap / go / başla / proceed / apply"). The two recent additions to this list, from the 2026-05-17 session:
  - **Scope-creep into a behavior-preservation slice is an approval gate.** Phase 3C was framed as lift-and-shift; adding variadic-filter behavior change without asking was a violation.
  - **Architectural pivots within spec-drawn shapes are an approval gate.** Changing `TypeMappingPolicy` from spec-drawn `public sealed class` to `public static class` was a pragmatic call made under analyzer pressure but should have been surfaced with the tradeoff (spec deviation vs. unused-field analyzer suppression vs. forced-usage).
- **No `--no-verify`, no `--no-gpg-sign`.**
- **Research before deciding.** Concrete: when picking a library / pattern / parameter name / naming convention, the first move is `Grep`/`Glob`/`WebFetch`/`gh search` for peer evidence + Microsoft docs + existing codebase pattern. Then propose. Then wait. See `docs/superpowers/agent-discipline.md` §1.
- **Validate output shape, not just counts.** "Per-view function counts byte-identical" does not imply "files byte-identical". Grep for known-tricky shapes (variadic emits, pointer parameter signatures, `unsafe` modifier on class headers, namespace declarations) before claiming preservation. See discipline doc §2.
- **Evidence before "succeeded".** Build output line counts, test summary blocks, slopwatch result text, smoke per-view counts AND structural file inspection. See discipline doc §4.
- **Cake-native build host:** in `build/_build`, no raw `System.IO` at boundaries. Cake `DirectoryPath`/`FilePath`, `ICakeContext`, `CakeFileSystemExtensions`, `CakeJsonExtensions`.
- **ADR-002 task-orchestrator pattern:** Cake tasks ARE orchestrators. No `Runner`/`Pipeline`/`Operation` wrappers. ADR-002 §2.4 forbids re-introducing them.
- **ADR-003 Data layer:** persisted/tool-read contracts live under `build/_build/Data/<Cohort>/`. `BindingGenerationConfigRepository` consumes `IManifestRepository` — no parallel JSON walking, no hand-rolled `JsonDocument` walkers.
- **Manifest single source of truth.** Family-id ↔ library-name from `manifest.PackageFamilies[].LibraryRef`. No hardcoded family switches in code. SDL3 entries (when they land) require no code change.
- **No timing pressure or motive assumptions.** Don't push Deniz on "when?" / "ready?". He sets pace.
- **No workarounds/shortcuts** (auto-memory feedback). Never hack code to make tests pass; never invent justifications; let libraries find their natural defaults; question spike-era assumptions before codifying them in specs.
- **Cross-OS container mount caveat** (auto-memory). Never bind-mount the Windows-host repo into a Linux container running dotnet/vcpkg; Dockerfile uses COPY-into-image + isolated cache volumes.
- **Rider for mass renames** (auto-memory). Agent prepares the rename map; Deniz Rider-executes; agent post-validates.

## Current git state (verify against `git log --oneline -8`)

Expected branch: `spike/binding-autogen-sdl2-gfx`

Recent commits (newest first):

```text
5bd563e feat(binding-autogen): Phase 3B — BindingModel extension + RequiredConstants (Option A)
797f1dd refactor(binding-autogen): retire Preview* naming; land production type names (PSTH-A, Phase 3A)
795c08b feat(binding-generator): add SDL.h-only constants handling in Phase 3B
7b9fafb feat(binding-autogen): manifest-driven per-family generator + Docker dynapi fix
66c5925 feat(binding-autogen): unified spec + plan; land P2-α design cleanup
9a5f59e feat(binding-autogen): Stage 1 PSTH-H/I/J hardening + VcpkgManifest consolidation
0db0e31 feat(binding-autogen): Stage 1 Task 3.5 + Post-Implementation Review fixes
c00a2cb feat(binding-autogen): Stage 1 scaffold + Task 3.5 local-loop design
```

**Phase 3C is dirty, not committed.** Expected dirty surface (11 files):

```text
 M build/_build/Targets/GenerateBindings/GenerateBindingsTask.cs              (call site flip: config arg)
 M build/_build/Targets/GenerateBindings/Model/CppAstToBindingModel.cs        (slim down — policies extracted, Translate signature flip)
?? build/_build/Targets/GenerateBindings/Model/CoreOwnedTypeMap.cs            (new policy)
?? build/_build/Targets/GenerateBindings/Model/KnownUnsupportedDeclarationPolicy.cs  (new policy)
?? build/_build/Targets/GenerateBindings/Model/TypeMappingPolicy.cs           (new policy, static class)
 M build/_build.Tests/Unit/Targets/GenerateBindings/Model/CppAstToBindingModelTests.cs  (Translate signature update)
RM build/_build.Tests/Unit/Targets/GenerateBindings/Model/CppAstToBindingModelMappingTests.cs
  → build/_build.Tests/Unit/Targets/GenerateBindings/Model/TypeMappingPolicyTests.cs  (git mv + content update)
?? build/_build.Tests/Unit/Targets/GenerateBindings/Model/CoreOwnedTypeMapTests.cs
?? build/_build.Tests/Unit/Targets/GenerateBindings/Model/KnownUnsupportedDeclarationPolicyTests.cs
 M docs/superpowers/specs/2026-05-16-binding-generator-unified-design.md      (§8 variadic framing flipped to manifest-only + peer-evidence)
 M docs/superpowers/plans/2026-05-17-binding-generator-unified-plan.md         (Phase 3B Finding note "Decided 2026-05-17: Option A" housekeeping)
```

Plus (created in this final session turn — outside the Phase 3C scope per se but landed for the trust-recovery work):

```text
?? docs/superpowers/agent-discipline.md
?? .github/prompts/binding-autogen-stage-1-phase-3c-and-3d-prime-handover.prompt.md  (this file)
```

The discipline doc + this handover prompt are part of the wind-down, not Phase 3C content. Decide separately whether they fold into the Phase 3C commit, a `docs(superpowers):` separate commit, or get included with Phase 3D-prime — that's Deniz's call.

## Verification status of dirty surface

Last verified at the end of the 2026-05-17 session (before the discipline turn):

```pwsh
dotnet build build/_build/Build.csproj -c Release          # 0 warning, 0 error
dotnet build build/_build.Tests/Build.Tests.csproj -c Release  # 0 warning, 0 error
dotnet test  build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0
# 760 passed (+19 net new vs Phase 3B baseline of 741), 0 failed, 2 skipped (Non-Windows only)
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"
# 0 issues
dotnet run --file tools.cs -- generate-bindings
# 4m 33s, 9 output files, per-view counts byte-identical to Phase 3B baseline
# (Neutral 836 / WindowsDesktop 8 / WinRT 12 / GDK 12 / Linux 2 / MacOS 0 / IOS 4 / Android 13)
# Variadic emit verified via grep (13 functions, fmt-only shape per SDL2-CS + Alimer peer pattern):
#   SDL_SetError, SDL_Log, SDL_LogCritical, SDL_LogDebug, SDL_LogError, SDL_LogInfo,
#   SDL_LogMessage, SDL_LogMessageV, SDL_LogVerbose, SDL_LogWarn, SDL_asprintf,
#   SDL_snprintf, SDL_sscanf — all with `byte*`/`sbyte*` fmt parameters.
```

## Historical issue — Phase 3A `unsafe` modifier defect (fixed later)

The generated `Commands.g.cs` files did not compile at this point in the handover. Phase 3A's P3.10 "drop `unsafe` modifier" was made without inspecting parameter signatures. SDL2 has pointer-bearing signatures whose parameters are `byte*`/`sbyte*` — these require `unsafe` context. The pre-fix emit was:

```csharp
internal static partial class Sdl2_Neutral             // ← no `unsafe`
{
    [DllImport(LibName, ...)]
    internal static extern void SDL_Log(sbyte* fmt);    // ← compiles with CS0214 (pointer in non-unsafe context)
}
```

**Why this hasn't been noticed:** the `tools.cs generate-bindings` target emits files but does not compile them. Phase 3G ("output wiring + smoke + peer-oracle diff") is the first phase that would surface the failure, which is several slices away. The maintainer caught it during Phase 3C review by asking the right question ("`*` C# pointer değil mi ve sadece unsafe'de kullanılamaz mı").

**Peer pattern check (Alimer + ppy/SDL3-CS + SDL2-CS):**

- **Alimer.Bindings.SDL** (CppAst SDL3): `public static unsafe partial class SDL3` — class-level `unsafe`
- **ppy/SDL3-CS** (ClangSharp SDL3): `public static unsafe partial class SDL3` — class-level `unsafe`
- **SDL2-CS** (hand-written): `private static extern unsafe void INTERNAL_SDL_Log(byte* fmt)` — method-level `unsafe`

Majority pattern (2/3, both auto-generated peers) is **class-level `unsafe partial class`**. Phase 3F friendly overloads will require `fixed (byte* p = ...)` blocks anyway, which need `unsafe` context — so class-level `unsafe` is forward-aligned.

**Maintainer's call (2026-05-17):** "Buna fold" — meaning fold the `unsafe` restore into the Phase 3C commit (not a separate `fix(...)` commit). The Phase 3C commit message must explicitly acknowledge the Phase 3A regression and the fix.

**Fix:**

1. `build/_build/Targets/GenerateBindings/Emitting/CsCommandEmitter.cs` — line emitting the class header. Change:

   ```csharp
   builder.Append("internal static partial class Sdl2_").AppendLf(view.Name);
   ```

   to:

   ```csharp
   builder.Append("internal static unsafe partial class Sdl2_").AppendLf(view.Name);
   ```

2. `build/_build.Tests/Unit/Targets/GenerateBindings/Emitting/CsCommandEmitterTests.cs` — add an assertion that the emitted class header contains `unsafe partial class`.

3. Re-run `dotnet run --file tools.cs -- generate-bindings` and **structurally inspect** the output:

   ```pwsh
   head -20 artifacts/generated-bindings-preview/sdl2-core/Platform/Neutral/Commands.g.cs
   # Expect: `internal static unsafe partial class Sdl2_Neutral`
   ```

4. Re-run `dotnet test` (one CsCommandEmitterTests assertion update should ripple through; test count goes from 760 to whatever +/- the change introduces).

5. Re-run slopwatch (expect 0 issues).

6. The Phase 3C commit message must contain a "**== Phase 3A `unsafe` regression fix (folded) ==**" block explaining:
   - Phase 3A's P3.10 was made on the false premise that the emit had no unsafe operations.
   - Pointer parameters (`sbyte*`/`byte*`) require `unsafe` context.
   - Class-level `unsafe partial class` restored per Alimer + ppy/SDL3-CS peer pattern (majority); SDL2-CS uses method-level which is also valid but auto-generated peers converge on class-level.
   - Pattern: `internal static unsafe partial class Sdl2_<view>` — the same shape Phase 3A had pre-rename (`internal static unsafe partial class Sdl2Preview_<view>`), minus the `Preview` token.

## Recurring discipline-defect pattern (read `docs/superpowers/agent-discipline.md` first)

Across the 2026-05-17 session, four discipline defects recurred — concrete examples, real consequences:

| # | Defect | Phase | Consequence | Lesson |
|---|---|---|---|---|
| 1 | **Decision without research.** "Microsoft.CodeAnalysis.CSharp 3 MB overkill, use hardcoded FrozenSet." | Phase 3C start | Outcome correct (peer-validated), reasoning fabricated (wrong package size, no peer evidence cited). Maintainer pushed back: "belki ben git codeanalysis e ekle diyeceğim. Gidip internetten best practiceleri araştırdın mı, peer'larımız ne yapmış baktın mı sikik". | Research first: package details + peer behavior (Alimer + ClangSharpPInvokeGenerator + Silk.NET all hardcoded). Same outcome via right path. |
| 2 | **Diagnosis without peer check.** "Variadic emit is broken P/Invoke stubs; exclude them in `KnownUnsupportedDeclarationPolicy.IsVariadic`." | Phase 3C start | Scope creep into "lift-and-shift" slice; behavior change of -12 functions snuck in. Maintainer caught it: "hop dur lan bana sormadan ne iş yapıyorsun, variadic bug ne ne yapıyorsun anlamadım." | Peer evidence first: SDL2-CS (`/* Use string.Format for arglists */`) + Alimer both emit fmt-only. Pattern is documented in their source. Surface question + evidence; wait. |
| 3 | **Spec deviation without explicit surfacing.** TypeMappingPolicy spec-drawn as `public sealed class` with ctor + fields → made `public static class` to satisfy CA1822/S4487 analyzer pressure. | Phase 3C implementation | Spec divergence buried in implementation. Right outcome practically (Phase 3C lift-and-shift has no instance state to carry), but architectural pivot wasn't surfaced. | When the spec draws shape X and analyzer pressure pushes toward shape Y, **stop and ask** — even if Y is cleaner. The maintainer can override the spec; the agent cannot. |
| 4 | **"Succeeded" claim without structural verification.** "Phase 3C container smoke 4m 33s; per-view counts byte-identical." | Phase 3C smoke | Counts identical, shape uninspected. Maintainer caught the latent Phase 3A `unsafe` defect by asking "smoke çalıştırdın mı variadicleri doğruladın mı outputtan" — counts were byte-identical, but the broken `unsafe`-less header was preserved across phases. | Counts ≠ shape. `head`/`grep` the output. Check class headers, parameter signatures, namespace declarations. Then claim. |

All four are encoded as the four disciplines in `docs/superpowers/agent-discipline.md`. **Read it.**

## Deniz's current concerns (verbatim, 2026-05-17 end-of-session)

> "giderek güvenim azalmaya başladı bu proje kim bilir daha neleri yanlış yapıyoruz diye düşünmeden edemiyorum. Bence ilk yapılacak iş artık bir csproj altına bunları ekleyip compile da etmek yani hatta araştıralım bu işe spesific analyzer'lar varsa kullanalım. Abi bu nedir ya? Ne dersin?"

Translation: trust is dropping; he's wondering what else is silently wrong; the proposed remediation is a **compile-validation slice** with **interop-specific analyzers**, before continuing the Phase 3 ladder.

The maintainer's framing is correct. The current `tools.cs generate-bindings` target emits files but does not compile them. The Phase 3A `unsafe` defect is exactly the class of failure that a compile-check would catch immediately. Phase 3G was scheduled to do this implicitly (output wiring + smoke + peer-oracle diff) but is several slices away.

**The proposed remediation is Phase 3D-prime, sketched below.**

## Phase 3D-prime — compile validation slice (recommended next slice after Phase 3C commit)

### Scope

1. **Add a compile-check csproj.** Suggested location: `tests/binding-compile-check/SDL2.Core.CompileCheck.csproj` (or similar — surface alternative to Deniz). Shape:

   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFrameworks>net10.0;net9.0;net8.0;netstandard2.0;net462</TargetFrameworks>
       <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
       <IsPackable>false</IsPackable>
       <!-- Strict analyzer enforcement -->
       <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
       <EnableNETAnalyzers>true</EnableNETAnalyzers>
       <AnalysisLevel>latest-all</AnalysisLevel>
     </PropertyGroup>
     <ItemGroup>
       <Compile Include="..\..\artifacts\generated-bindings-preview\sdl2-core\**\*.g.cs" />
     </ItemGroup>
   </Project>
   ```

2. **Research + enable P/Invoke-specific analyzers.** Candidate list (verify against Microsoft docs + peer projects before adopting — apply discipline 1):
   - **SYSLIB1054** — `[DllImport]` → `[LibraryImport]` migration analyzer (net7+); fires when DllImport could be modernized.
   - **CA1401** — P/Invokes should not be visible (warns on `public` P/Invoke declarations; our emit uses `internal`, should be silent but worth pinning).
   - **CA1416** — Platform compatibility analyzer (validates `[SupportedOSPlatform]`/`[UnsupportedOSPlatform]` usage; our emit attributes platform-specific functions, this is a real check).
   - **CA1838** — Avoid `StringBuilder` parameters for P/Invoke.
   - **CA2101** — Specify marshaling for P/Invoke string arguments.
   - **`Microsoft.Interop.SourceGeneration`** — the LibraryImport source generator itself (Phase 3F will need this once dual-emit lands; landing it now in the compile-check csproj exercises the LibraryImport path early).

   For each: WebFetch / WebSearch the rule's official Microsoft docs, check peer projects (Alimer / ppy/SDL3-CS / SDL2-CS / Silk.NET) for adoption, propose with evidence.

3. **Pipeline integration.** Add a `tools.cs generate-bindings` post-step that builds the compile-check csproj:

   ```
   emit → dotnet build tests/binding-compile-check → assert green
   ```

   If the build fails, the smoke target fails with the compile errors visible. No more silent broken emit.

4. **Phase 3F + 3G forward alignment.** The compile-check csproj is the natural ancestor of Phase 3G's "5-TFM compile of generated SDL2.Core" acceptance gate. Phase 3F friendly overloads + dual P/Invoke emit will be compile-validated automatically once they land.

### Approval gates for Phase 3D-prime

- **csproj structural addition** — new project under `tests/` is a build-host shape change; AGENTS.md approval gate applies. Surface the proposed csproj diff before applying.
- **New analyzer rule activation** — each enabled analyzer that fires warnings/errors is a code-impact change; surface the rule + projected impact (false-positive count) before flipping.
- **`tools.cs` integration** — `tools.cs` is the canonical dev orchestration entry; modifying it requires explicit approval per AGENTS.md §Approval Gate.

### Suggested Phase 3D-prime sequence

1. Open the discussion: surface the proposed scope (csproj + analyzer set + pipeline integration) with evidence. Wait for Deniz's call on scope.
2. **Per discipline 1**: research each candidate analyzer — Microsoft docs + peer adoption + false-positive risk. Bring evidence.
3. **Per discipline 3**: csproj location + analyzer list both need explicit approval before applying.
4. Implement in this order:
   a. Compile-check csproj minimum viable (no analyzers, just compiles)
   b. Pin a baseline: build green with current emit (after `unsafe` fix from Phase 3C fold)
   c. Enable analyzer rules one at a time; each rule that fires should produce a documented diff (manifest fixture / emitter / etc.) or a justified suppression
   d. Pipeline integration: `tools.cs` post-step
5. **Per discipline 4**: verification = `dotnet build` on 5 TFMs all green + smoke pipeline shows compile step + grep'd output is structurally clean.

## Things to be careful about (carried forward from prior handover prompts + 2026-05-17 lessons)

- **Do not reintroduce `IBindingPublicApiCoherenceValidator` or `Sdl2CoreGenerationConfig.Default`.** Retired in Phase 2D.
- **Do not reintroduce a hand-rolled `JsonDocument` walker for binding-generation config.** Strongly-typed `ManifestConfig.LibraryManifests[].BindingGeneration` is canonical.
- **Do not add `--family` CLI arg to `BuildContext` yet.** Stage 1 has only sdl2-core enabled.
- **Do not weaken CI/Docker cache key parity.** `tools.cs ComputeVcpkgCacheKey` mirrors `.github/actions/vcpkg-setup/action.yml`.
- **Do not put cache mounts on paths whose content needs image-layer residency.** BuildKit cache mounts are RUN-scoped.
- **Do not add IsVariadic filter to `KnownUnsupportedDeclarationPolicy`.** Pinned by test — variadic functions emit fmt-only per SDL2-CS + Alimer peer pattern. Phase 3F adds the `string fmtAndArglist` wrapper.
- **Do not adopt `__arglist` for variadic emit.** ClangSharp default; loses variadic capacity in friendly overload trio; not adopted per 2026-05-17 peer review (see plan §Phase 3C design intent + spec §8.2).
- **Do not break peer-aligned identifiers.** `BindingEnumeration` (not `BindingEnum` — CA1711); `CoreOwnedTypeMap` not `CoreOwnedTypeRegistry`; `KnownUnsupportedDeclarationPolicy` not `UnsupportedDeclarationFilter`. Spec-drawn names are stable.
- **Do not commit `artifacts/generated-bindings-preview/`.** Gitignored.
- **Do not run `git commit` unless Deniz explicitly approves.** The Phase 3C dirty surface + `unsafe` fix + this handover doc + agent-discipline doc are significant; commit shape is Deniz's call.
- **Do not skip the verification ritual.** Build + test + slopwatch + smoke + structural output inspection. Every claim of "succeeded" is back-able with evidence text.
- **Do not use `dotnet test --filter` or `dotnet test --nologo`.** TUnit / Microsoft.Testing.Platform doesn't support `--filter`; `--nologo` confuses MTP and produces "Zero tests ran" with exit code 5.

## Useful commands (verify on first session)

```pwsh
# Repo state
git status --short
git diff --stat
git log --oneline -8

# Build + test (Phase 3C verification baseline)
dotnet build build/_build/Build.csproj -c Release
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0

# Slopwatch
slopwatch analyze --fail-on warning --exclude "artifacts/**,external/**,vcpkg_installed/**,tools/**,**/bin/**,**/obj/**"

# Container smoke (~5 min)
dotnet run --file tools.cs -- generate-bindings

# Structural output verification (must do after smoke per discipline 2 + 4)
head -20 artifacts/generated-bindings-preview/sdl2-core/Platform/Neutral/Commands.g.cs
grep -n "SDL_Log\b\|SDL_SetError\|SDL_sscanf\|SDL_snprintf\|SDL_asprintf\|SDL_LogVerbose" artifacts/generated-bindings-preview/sdl2-core/Platform/Neutral/Commands.g.cs
grep -n "unsafe partial class" artifacts/generated-bindings-preview/sdl2-core/Platform/Neutral/Commands.g.cs

# Guard greps (verify Phase 3C policy extraction is in place)
grep -rE "Sdl2CoreGenerationConfig|BindingPublicApiCoherenceValidator" build/_build build/_build.Tests   # should be 0 matches
grep -n "MapType\|MapPrimitive\|MapTypedef\|MapPointer\|SafeIdentifier" build/_build/Targets/GenerateBindings/Model/CppAstToBindingModel.cs   # should be 0 — moved to TypeMappingPolicy
grep -n "public sealed class TypeMappingPolicy\|public static class TypeMappingPolicy" build/_build/Targets/GenerateBindings/Model/TypeMappingPolicy.cs   # confirm static class shape
```

## Final steering note for the next agent

Two things matter most:

1. **Read `docs/superpowers/agent-discipline.md` before doing anything.** The four disciplines aren't aspirational — they're encoded from real defects in the prior session. The maintainer's trust is recoverable but visible through the next slice's behavior. Don't repeat the four anti-patterns.

2. **The Phase 3D-prime compile-validation slice is the trust-recovery action.** Once Phase 3C lands (with the `unsafe` fix folded), Phase 3D-prime is the next slice — before Phase 3D translator structural classification. Phase 3D-prime adds the compile gate that catches the class of defect Phase 3A introduced silently. After Phase 3D-prime, every subsequent emit-shape change (Phase 3D, 3E, 3F, 3G) lands under compile validation. That's the structural answer to "kim bilir daha neleri yanlış yapıyoruz."

If the Phase 3D-prime scope or any part of this handover prompt seems wrong, **flag the divergence to Deniz before acting**. The right move when in doubt is to ask, surface evidence, and wait — not to optimize for speed.

After Phase 3D-prime fully lands (its own slice + commit), the queued work returns to:

| Slice | Plan section | Notes |
|---|---|---|
| **Phase 3D** | Unified plan §"Phase 3D — CppAstToBindingModel translator refactor" | Translator rewrite to populate 6 categories + structural classification of opaque handles (replacing the SDL_-prefix → IntPtr fallback in MapPointer). |
| **Phase 3E** | Unified plan §"Phase 3E — Per-category emitters" | 6 per-category emitters + `BindingEmitter` dispatcher + `EmitContext`. `CsConstantEmitter` Literal/Computed split. `CsHandleEmitter` Rule 2 feature set. |
| **Phase 3F** | Unified plan §"Phase 3F — Friendly overloads + dual P/Invoke emit" | string / Span / out / ref overloads in `CsCommandEmitter` + `#if NET7_0_OR_GREATER` dual emit. Variadic friendly wrapper (SDL2-CS `string fmtAndArglist` pattern) lands here. |
| **Phase 3G** | Unified plan §"Phase 3G — Output wiring + smoke + peer-oracle diff" | Final acceptance: 5-TFM compile (now automated via Phase 3D-prime's csproj), all three family validators pass, peer-oracle visual diff. |

Each is its own slice and its own handover prompt. Hold the line on ADR-002 / ADR-003 boundaries, the manifest-as-single-source-of-truth principle, the four disciplines, and the cache-key parity with CI. Skip micro-checkpoints during execution; respect approval gates at boundaries. Sequence over speed. **When the plan and the code disagree, fix the plan or ask before changing the code.**

Iyi şanslar — Deniz iyi maintainer, dürüst feedback verir, hak ettiği saygıyı göster.
